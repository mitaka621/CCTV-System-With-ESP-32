#include "config.h"
#include "device_command.h"
#include "vent_state.h"
#include "fan_relay.h"
#include "fan_tachometer.h"
#include "temperature_sensor.h"
#include "vent_door.h"
#include "led_indicator.h"
#include "secret_store.h"
#include "wifi_manager.h"
#include "provision_ap.h"
#include "preprovision_client.h"
#include "data_channel.h"
#include <Arduino.h>
#include <ArduinoJson.h>

enum AppState
{
  STATE_LOAD_CREDENTIALS,
  STATE_AP_PROVISIONING,
  STATE_CONNECT_WIFI,
  STATE_PREPROVISION,
  STATE_CONNECTED,
  STATE_LOCAL,
  STATE_FATAL_ERROR
};

static constexpr size_t VENT_PAYLOAD_LEN = 11;
static const char FAN_RUNNING_KEY[] = "fanRunning";

static AppState _state = STATE_LOAD_CREDENTIALS;
static DeviceCredentials _creds;
static uint8_t _payload[VENT_PAYLOAD_LEN];

static bool _fanShouldRun = false;
static bool _localControlled = false;
static bool _configResumed = false;
static uint8_t _channelAttempts = 0;

static bool _resetButtonHeld = false;
static unsigned long _resetButtonPressStart = 0;
static unsigned long _channelRetryAt = 0;
static unsigned long _networkRetryAt = 0;
static unsigned long _lastReportAt = 0;

static void PutBE16(uint8_t *out, uint16_t value)
{
  out[0] = (uint8_t)(value >> 8);
  out[1] = (uint8_t)value;
}

static void PutBE32(uint8_t *out, uint32_t value)
{
  out[0] = (uint8_t)(value >> 24);
  out[1] = (uint8_t)(value >> 16);
  out[2] = (uint8_t)(value >> 8);
  out[3] = (uint8_t)value;
}

static void EnterState(AppState next)
{
  _state = next;

  switch (next)
  {
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
  case STATE_CONNECTED:
    led_indicator::setState(led_indicator::STREAMING);
    break;
  case STATE_LOCAL:
    led_indicator::setState(led_indicator::ERROR_NETWORK);
    _configResumed = false;
    _networkRetryAt = millis() + LOCAL_RETRY_INTERVAL_MS;
    break;
  case STATE_FATAL_ERROR:
    led_indicator::setState(led_indicator::ERROR_FATAL);
    break;
  }
}

static bool LoadStoredFanState()
{
  String stored;
  if (!secret_store::loadFromNvs(FAN_RUNNING_KEY, stored))
  {
    return false;
  }

  return stored == "1";
}

static void SaveFanState(bool running)
{
  String value = running ? "1" : "0";
  if (!secret_store::saveToNvs(FAN_RUNNING_KEY, value))
  {
    DEBUG_PRINT("Failed to save the fan state");
  }
}

static void ApplyFanState(bool running)
{
  if (!running)
  {
    fan_relay::SetActive(false);
    vent_door::Close();
    return;
  }

  if (!vent_door::Open())
  {
    DEBUG_PRINT("Door would not open, keeping the fan off");
    fan_relay::SetActive(false);
    return;
  }

  fan_relay::SetActive(true);
}

static void RunLocalThermostat()
{
  if (!temperature_sensor::IsValid())
  {
    return;
  }

  const float celsius = temperature_sensor::LastCelsius();

  if (!_fanShouldRun && celsius >= LOCAL_START_TEMPERATURE_C)
  {
    DEBUG_PRINT(String("Local mode: ") + celsius + " C, starting the fan");
    _fanShouldRun = true;
    _localControlled = true;
    ApplyFanState(true);
    return;
  }

  if (_fanShouldRun && celsius <= LOCAL_STOP_TEMPERATURE_C)
  {
    DEBUG_PRINT(String("Local mode: ") + celsius + " C, stopping the fan");
    _fanShouldRun = false;
    _localControlled = true;
    ApplyFanState(false);
  }
}

static void BuildPayload()
{
  const uint16_t rawTemperature =
      temperature_sensor::IsValid()
          ? (uint16_t)(int16_t)lroundf(constrain(temperature_sensor::LastCelsius(), -100.0f, 100.0f) * 100.0f)
          : (uint16_t)TEMPERATURE_INVALID_RAW;

  _payload[0] = VENT_PAYLOAD_VERSION;
  _payload[1] = (uint8_t)vent_door::State();
  PutBE16(_payload + 2, rawTemperature);
  PutBE16(_payload + 4, fan_tachometer::Rpm());
  _payload[6] = (fan_relay::IsActive() ? 0x01 : 0x00) | (_localControlled ? 0x02 : 0x00);
}

static void ApplyNewConfig(const uint8_t *json, size_t len)
{
  JsonDocument doc;
  DeserializationError err = deserializeJson(doc, json, len);
  if (err)
  {
    DEBUG_PRINT(String("Config: parse failed ") + err.c_str());
    return;
  }

  if (!doc["isRunning"].is<bool>())
  {
    DEBUG_PRINT("Config: no isRunning field, the fan state is left alone");
    return;
  }

  const bool running = doc["isRunning"].as<bool>();
  DEBUG_PRINT(String("Config: isRunning=") + (running ? "true" : "false"));

  _fanShouldRun = running;
  _localControlled = false;
  SaveFanState(running);
  ApplyFanState(running);
}

static void HandleCommand(DeviceCommand command, const uint8_t *payload, size_t payloadLen)
{
  switch (command)
  {
  case DeviceCommand::SaveNewConfig:
    ApplyNewConfig(payload, payloadLen);
    break;
  default:
    DEBUG_PRINT(String("Command: ignored code ") + (int)command);
    break;
  }
}

static void ReceiveCommands()
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

    HandleCommand((DeviceCommand)message[1], message + 2, messageLen - 2);
  }
}

static void HandleResetButton()
{
  const int buttonState = digitalRead(RESET_BUTTON_PIN);

  if (buttonState == HIGH)
  {
    _resetButtonHeld = false;
    return;
  }

  if (!_resetButtonHeld)
  {
    _resetButtonHeld = true;
    _resetButtonPressStart = millis();
    return;
  }

  if ((millis() - _resetButtonPressStart) < RESET_BUTTON_HOLD_MS)
  {
    return;
  }

  DEBUG_PRINT("Reset button held, clearing stored credentials and the saved fan state");
  led_indicator::setState(led_indicator::ERROR_FATAL);
  led_indicator::tick();
  secret_store::clearAll();
  delay(500);
  ESP.restart();
}

static void DoLoadCredentials()
{
  if (secret_store::loadFromCompileTime(_creds) || secret_store::loadFromNvs(_creds))
  {
    EnterState(STATE_CONNECT_WIFI);
    return;
  }

  DEBUG_PRINT("No credentials stored, starting the setup access point");

  if (!provision_ap::begin())
  {
    DEBUG_PRINT("Failed to start the setup access point");
    EnterState(STATE_FATAL_ERROR);
    return;
  }

  DEBUG_PRINT("Join Wi-Fi: " + provision_ap::apSsid() + " and scan the wizard code with your phone");
  EnterState(STATE_AP_PROVISIONING);
}

static void DoApProvisioning()
{
  DeviceCredentials received;
  if (provision_ap::tick(received) != provision_ap::RECEIVED)
  {
    //local mode until preprovision is complated 
    RunLocalThermostat();
    return;
  }

  led_indicator::setState(led_indicator::PROVISION_RECEIVED);
  for (int i = 0; i < 10; i++)
  {
    led_indicator::tick();
    delay(100);
  }

  _creds = received;

  if (!secret_store::saveToNvs(_creds))
  {
    DEBUG_PRINT("Failed to save credentials to storage");
  }
  secret_store::setPaired(false);

  delay(1500);
  provision_ap::end();

  EnterState(STATE_CONNECT_WIFI);
}

static void DoConnectWifi()
{
  if (!wifi_manager::connect(_creds.wifiSsid, _creds.wifiPass, WIFI_CONNECT_TIMEOUT_MS))
  {
    DEBUG_PRINT("Wi-Fi connect failed, running the vent locally");
    EnterState(STATE_LOCAL);
    return;
  }

  EnterState(secret_store::isPaired() ? STATE_CONNECTED : STATE_PREPROVISION);
}

static void DoPreprovision()
{
  if (preprovision_client::verify(_creds) == preprovision_client::SUCCESS)
  {
    DEBUG_PRINT("Preprovision verification accepted by the server");
    secret_store::setPaired(true);
    _channelAttempts = 0;
    _channelRetryAt = 0;
    EnterState(STATE_CONNECTED);
    return;
  }

  DEBUG_PRINT("Preprovision failed, running the vent locally until the next attempt");
  EnterState(STATE_LOCAL);
  _networkRetryAt = millis() + PREPROVISION_RETRY_DELAY_MS;
}

static void DoConnected()
{
  if (!wifi_manager::isConnected())
  {
    DEBUG_PRINT("Wi-Fi dropped, running the vent locally");
    data_channel::End();
    EnterState(STATE_LOCAL);
    return;
  }

  if (!data_channel::IsActive())
  {
    _configResumed = false;

    if (_channelRetryAt != 0 && millis() < _channelRetryAt)
    {
      return;
    }

    if (!data_channel::Begin(_creds, ServerTcpPort))
    {
      _channelAttempts++;
      DEBUG_PRINT(String("Data channel start failed, attempt ") + _channelAttempts);

      if (_channelAttempts >= CHANNEL_MAX_ATTEMPTS)
      {
        EnterState(STATE_LOCAL);
        return;
      }

      _channelRetryAt = millis() + CHANNEL_RETRY_DELAY_MS;
      return;
    }

    _channelAttempts = 0;
    _channelRetryAt = 0;
  }

  if (!_configResumed)
  {
    _configResumed = true;
    _fanShouldRun = LoadStoredFanState();
    _localControlled = false;

    DEBUG_PRINT(String("Server reachable, resuming saved fan state ") + (_fanShouldRun ? "running" : "stopped"));
    ApplyFanState(_fanShouldRun);
  }

  ReceiveCommands();

  if ((millis() - _lastReportAt) < VENT_REPORT_INTERVAL_MS)
  {
    return;
  }
  _lastReportAt = millis();

  BuildPayload();

  if (!data_channel::Send(_payload, VENT_PAYLOAD_LEN))
  {
    DEBUG_PRINT("Vent report send failed");
  }
}

static void DoLocal()
{
  RunLocalThermostat();

  if (millis() < _networkRetryAt)
  {
    return;
  }
  _networkRetryAt = millis() + LOCAL_RETRY_INTERVAL_MS;

  if (!wifi_manager::isConnected() &&
      !wifi_manager::connect(_creds.wifiSsid, _creds.wifiPass, WIFI_CONNECT_TIMEOUT_MS))
  {
    return;
  }

  _channelAttempts = 0;
  _channelRetryAt = 0;
  EnterState(secret_store::isPaired() ? STATE_CONNECTED : STATE_PREPROVISION);
}

void setup()
{
  pinMode(RESET_BUTTON_PIN, INPUT_PULLUP);

  if (DEBUG_ON)
  {
    Serial.begin(115200);
    const unsigned long serialStart = millis();
    while (!Serial && (millis() - serialStart) < 3000)
    {
      delay(10);
    }
    delay(300);
  }

  DEBUG_PRINT("=================================");
  DEBUG_PRINT("Auto vent fan " FIRMWARE_VERSION);
  DEBUG_PRINT("=================================");

  led_indicator::begin();
  led_indicator::setState(led_indicator::BOOTING);

  fan_relay::Begin();
  fan_tachometer::Begin();
  temperature_sensor::Begin();
  vent_door::Begin();

  if (!secret_store::begin())
  {
    DEBUG_PRINT("Failed to open the credential storage namespace");
  }

  if (!vent_door::Close())
  {
    DEBUG_PRINT("Door calibration failed, the vent will report an unknown position");
  }

  EnterState(STATE_LOAD_CREDENTIALS);
}

void loop()
{
  HandleResetButton();
  led_indicator::tick();
  temperature_sensor::Tick();
  fan_tachometer::Tick();

  switch (_state)
  {
  case STATE_LOAD_CREDENTIALS:
    DoLoadCredentials();
    break;
  case STATE_AP_PROVISIONING:
    DoApProvisioning();
    break;
  case STATE_CONNECT_WIFI:
    DoConnectWifi();
    break;
  case STATE_PREPROVISION:
    DoPreprovision();
    break;
  case STATE_CONNECTED:
    DoConnected();
    break;
  case STATE_LOCAL:
    DoLocal();
    break;
  case STATE_FATAL_ERROR:
    delay(500);
    break;
  }

  delay(10);
}
