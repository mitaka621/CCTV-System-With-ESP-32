#include "config.h"
#include "device_command.h"
#include "led_indicator.h"
#include "status_led.h"
#include "secret_store.h"
#include "wifi_manager.h"
#include "provision_ap.h"
#include "preprovision_client.h"
#include "data_channel.h"
#include "buzzer.h"
#include <Arduino.h>
#include <ArduinoJson.h>

enum AppState
{
  STATE_BOOT,
  STATE_LOAD_CREDENTIALS,
  STATE_AP_PROVISIONING,
  STATE_CONNECT_WIFI,
  STATE_PREPROVISION,
  STATE_SENDING,
  STATE_NETWORK_RECOVERY,
  STATE_FATAL_ERROR
};

static constexpr size_t READING_PAYLOAD_LEN = 8;

static AppState _state = STATE_BOOT;
static DeviceCredentials _creds;

static unsigned long _resetButtonPressStart = 0;
static bool _resetButtonHeld = false;

static unsigned long _preprovisionRetryAt = 0;
static unsigned long _channelRetryAt = 0;
static unsigned long _lastReadingAt = 0;

static uint8_t _readingBuf[READING_PAYLOAD_LEN] = {READING_PAYLOAD_VERSION};

static void putBE16(uint8_t *out, uint16_t value)
{
  out[0] = (uint8_t)(value >> 8);
  out[1] = (uint8_t)value;
}

static void putBE32(uint8_t *out, uint32_t value)
{
  out[0] = (uint8_t)(value >> 24);
  out[1] = (uint8_t)(value >> 16);
  out[2] = (uint8_t)(value >> 8);
  out[3] = (uint8_t)value;
}

static void buildReading()
{
  _readingBuf[0] = READING_PAYLOAD_VERSION;
  putBE32(_readingBuf + 1, (uint32_t)(millis() / 1000UL));
  putBE16(_readingBuf + 5, (uint16_t)(ESP.getFreeHeap() / 1024));
  _readingBuf[7] = buzzer::isActive() ? 0x01 : 0x00;
}

static void applyNewConfig(const uint8_t *json, size_t len)
{
  JsonDocument doc;
  DeserializationError err = deserializeJson(doc, json, len);
  if (err)
  {
    DEBUG_PRINT(String("Config: parse failed ") + err.c_str());
    return;
  }

  const char *label = doc["label"] | "unnamed";
  DEBUG_PRINT(String("Config: label=") + label);
}

static void handleCommand(DeviceCommand command, const uint8_t *payload, size_t payloadLen)
{
  switch (command)
  {
  case DeviceCommand::ResetSecurityAlarm:
    DEBUG_PRINT("Command: reset security alarm");
    buzzer::setActive(false);
    break;
  case DeviceCommand::ActivateBuzzerAlarm:
    DEBUG_PRINT("Command: trigger security alarm");
    buzzer::setActive(true);
    break;
  case DeviceCommand::SaveNewConfig:
    DEBUG_PRINT("Command: save new config");
    applyNewConfig(payload, payloadLen);
    break;
  default:
    DEBUG_PRINT(String("Command: unknown code ") + (int)command);
    break;
  }
}

static void receiveCommands()
{
  uint8_t message[DATA_CHANNEL_MAX_MESSAGE_SIZE];
  size_t messageLen = 0;

  while (data_channel::Receive(message, sizeof(message), messageLen))
  {
    if (messageLen < 2 || message[0] != DEVICE_COMMAND_VERSION)
    {
      DEBUG_PRINT("Command: unsupported message envelope");
      continue;
    }

    handleCommand((DeviceCommand)message[1], message + 2, messageLen - 2);
  }
}

static void handleResetButton()
{
  int buttonState = digitalRead(RESET_BUTTON_PIN);

  if (buttonState == LOW && !_resetButtonHeld)
  {
    _resetButtonHeld = true;
    _resetButtonPressStart = millis();
    return;
  }

  if (buttonState == HIGH)
  {
    _resetButtonHeld = false;
    return;
  }

  if ((millis() - _resetButtonPressStart) >= RESET_BUTTON_HOLD_MS)
  {
    DEBUG_PRINT("Reset button held: clearing NVS and restarting");
    led_indicator::setState(led_indicator::ERROR_FATAL);
    led_indicator::tick();
    secret_store::clearAll();
    delay(500);
    ESP.restart();
  }
}

static void enterState(AppState next)
{
  _state = next;
  status_led::setMode(next == STATE_AP_PROVISIONING ? status_led::BLINKING : status_led::OFF);
  switch (next)
  {
  case STATE_BOOT:
  case STATE_LOAD_CREDENTIALS:
    led_indicator::setState(led_indicator::BOOTING);
    break;
  case STATE_AP_PROVISIONING:
    led_indicator::setState(led_indicator::AP_PROVISIONING);
    break;
  case STATE_CONNECT_WIFI:
    led_indicator::setState(led_indicator::WIFI_CONNECTING);
    break;
  case STATE_PREPROVISION:
    led_indicator::setState(led_indicator::PAIRING);
    break;
  case STATE_SENDING:
    led_indicator::setState(led_indicator::STREAMING);
    break;
  case STATE_NETWORK_RECOVERY:
    led_indicator::setState(led_indicator::ERROR_NETWORK);
    break;
  case STATE_FATAL_ERROR:
    led_indicator::setState(led_indicator::ERROR_FATAL);
    break;
  }
}

static void doLoadCredentials()
{
  if (secret_store::loadFromCompileTime(_creds))
  {
    DEBUG_PRINT("Loaded credentials from compile-time secrets.h");
    enterState(STATE_CONNECT_WIFI);
    return;
  }

  if (secret_store::loadFromNvs(_creds))
  {
    DEBUG_PRINT("Loaded credentials from NVS");
    enterState(STATE_CONNECT_WIFI);
    return;
  }

  DEBUG_PRINT("No credentials present, starting soft-AP provisioning");
  if (!provision_ap::begin())
  {
    DEBUG_PRINT("Failed to start provisioning AP");
    enterState(STATE_FATAL_ERROR);
    return;
  }

  DEBUG_PRINT("Join Wi-Fi: " + provision_ap::apSsid() + " and scan the wizard QR with your phone");
  enterState(STATE_AP_PROVISIONING);
}

static void doApProvisioning()
{
  DeviceCredentials received;
  if (provision_ap::tick(received) != provision_ap::RECEIVED)
  {
    status_led::setMode(provision_ap::clientConnected() ? status_led::SOLID : status_led::BLINKING);
    return;
  }

  status_led::setMode(status_led::OFF);
  led_indicator::setState(led_indicator::PROVISION_RECEIVED);
  for (int i = 0; i < 10; i++)
  {
    led_indicator::tick();
    delay(100);
  }

  _creds = received;

  if (!secret_store::saveToNvs(_creds))
  {
    DEBUG_PRINT("Failed to save credentials to NVS");
  }
  secret_store::setPaired(false);

  delay(1500);
  provision_ap::end();

  enterState(STATE_CONNECT_WIFI);
}

static void doConnectWifi()
{
  if (!wifi_manager::connect(_creds.wifiSsid, _creds.wifiPass, WIFI_CONNECT_TIMEOUT_MS))
  {
    DEBUG_PRINT("WiFi connect failed; will retry");
    enterState(STATE_NETWORK_RECOVERY);
    return;
  }

  enterState(secret_store::isPaired() ? STATE_SENDING : STATE_PREPROVISION);
}

static void doPreprovision()
{
  if (_preprovisionRetryAt != 0 && millis() < _preprovisionRetryAt)
  {
    delay(100);
    return;
  }

  if (preprovision_client::verify(_creds) == preprovision_client::SUCCESS)
  {
    DEBUG_PRINT("Preprovision verification accepted by server");
    secret_store::setPaired(true);
    _channelRetryAt = 0;
    enterState(STATE_SENDING);
    return;
  }

  DEBUG_PRINT("Preprovision failed; will retry. Hold the reset button (5s) to start over.");
  _preprovisionRetryAt = millis() + PREPROVISION_RETRY_DELAY_MS;
}

static void doSending()
{
  if (!wifi_manager::isConnected())
  {
    data_channel::End();
    enterState(STATE_NETWORK_RECOVERY);
    return;
  }

  if (!data_channel::IsActive())
  {
    if (_channelRetryAt != 0 && millis() < _channelRetryAt)
    {
      delay(50);
      return;
    }

    if (!data_channel::Begin(_creds, ServerTcpPort))
    {
      DEBUG_PRINT("Data channel start failed; backing off");
      _channelRetryAt = millis() + CHANNEL_RETRY_DELAY_MS;
      return;
    }
    _channelRetryAt = 0;
  }

  receiveCommands();

  if ((millis() - _lastReadingAt) < READING_INTERVAL_MS)
  {
    delay(10);
    return;
  }
  _lastReadingAt = millis();

  buildReading();
  if (!data_channel::Send(_readingBuf, READING_PAYLOAD_LEN))
  {
    DEBUG_PRINT("Reading send failed");
  }
}

static void doNetworkRecovery()
{
  if (!wifi_manager::connect(_creds.wifiSsid, _creds.wifiPass, WIFI_CONNECT_TIMEOUT_MS))
  {
    DEBUG_PRINT("WiFi reconnect failed; backing off");
    unsigned long start = millis();
    while (millis() - start < WIFI_RETRY_DELAY_MS)
    {
      handleResetButton();
      led_indicator::tick();
      delay(50);
    }
    return;
  }

  enterState(secret_store::isPaired() ? STATE_SENDING : STATE_PREPROVISION);
}

void setup()
{
  pinMode(RESET_BUTTON_PIN, INPUT_PULLUP);

  if (DEBUG_ON)
  {
    Serial.begin(115200);
    unsigned long serialStart = millis();
    while (!Serial && (millis() - serialStart) < 3000)
    {
      delay(10);
    }
    delay(300);
  }

  DEBUG_PRINT("=================================");
  DEBUG_PRINT("CamPortal device firmware " FIRMWARE_VERSION);
  DEBUG_PRINT("=================================");

  led_indicator::begin();
  led_indicator::setState(led_indicator::BOOTING);
  status_led::begin();
  buzzer::begin();

  if (!secret_store::begin())
  {
    DEBUG_PRINT("Failed to open NVS namespace");
  }

  enterState(STATE_LOAD_CREDENTIALS);
}

void loop()
{
  handleResetButton();

  if (DEBUG_ON)
    led_indicator::tick();

  status_led::tick();

  switch (_state)
  {
  case STATE_BOOT:
  case STATE_LOAD_CREDENTIALS:
    doLoadCredentials();
    break;
  case STATE_AP_PROVISIONING:
    doApProvisioning();
    break;
  case STATE_CONNECT_WIFI:
    doConnectWifi();
    break;
  case STATE_PREPROVISION:
    doPreprovision();
    break;
  case STATE_SENDING:
    doSending();
    break;
  case STATE_NETWORK_RECOVERY:
    doNetworkRecovery();
    break;
  case STATE_FATAL_ERROR:
    delay(500);
    break;
  }
}
