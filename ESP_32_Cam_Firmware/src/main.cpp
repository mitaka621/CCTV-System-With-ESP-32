#include "config.h"
#include "secrets.h"
#include "led_indicator.h"
#include "status_led.h"
#include "secret_store.h"
#include "wifi_manager.h"
#include "provision_ap.h"
#include "preprovision_client.h"
#include "frame_streamer.h"
#include "ir_cut_controller.h"
#include <Arduino.h>
#include <nvs_flash.h>

enum AppState
{
  STATE_BOOT,
  STATE_LOAD_CREDENTIALS,
  STATE_AP_PROVISIONING,
  STATE_CONNECT_WIFI,
  STATE_PREPROVISION,
  STATE_INIT_STREAMING_CAMERA,
  STATE_STREAMING,
  STATE_NETWORK_RECOVERY,
  STATE_FATAL_ERROR
};

static AppState _state = STATE_BOOT;
static DeviceCredentials _creds;
static bool _credsSource_compileTime = false;

static unsigned long _resetButtonPressStart = 0;
static bool _resetButtonHeld = false;

static unsigned long _preprovisionRetryAt = 0;
static unsigned long _sessionRetryAt = 0;

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

  if (buttonState == LOW && _resetButtonHeld &&
      (millis() - _resetButtonPressStart) >= RESET_BUTTON_HOLD_MS)
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
    led_indicator::setState(led_indicator::BOOTING);
    break;
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
  case STATE_INIT_STREAMING_CAMERA:
    led_indicator::setState(led_indicator::PAIRED);
    break;
  case STATE_STREAMING:
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

void setup()
{
  pinMode(RESET_BUTTON_PIN, INPUT_PULLUP);

  Serial.begin(115200);
  unsigned long serialStart = millis();
  while (!Serial && (millis() - serialStart) < 3000)
  {
    delay(10);
  }
  delay(300);

  DEBUG_PRINT("=================================");
  DEBUG_PRINT("CamPortal device firmware " FIRMWARE_VERSION);
  DEBUG_PRINT("=================================");

  led_indicator::begin();
  led_indicator::setState(led_indicator::BOOTING);
  status_led::begin();
  ir_cut_controller::begin();

  if (!secret_store::begin())
  {
    DEBUG_PRINT("Failed to open NVS namespace");
  }

  enterState(STATE_LOAD_CREDENTIALS);
}

static void doLoadCredentials()
{
  if (secret_store::loadFromCompileTime(_creds))
  {
    DEBUG_PRINT("Loaded credentials from compile-time secrets.h");
    _credsSource_compileTime = true;
    enterState(STATE_CONNECT_WIFI);
    return;
  }

  if (secret_store::loadFromNvs(_creds))
  {
    DEBUG_PRINT("Loaded credentials from NVS");
    _credsSource_compileTime = false;
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
  provision_ap::TickResult result = provision_ap::tick(received);

  if (result != provision_ap::RECEIVED)
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
  _credsSource_compileTime = false;

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
  if (wifi_manager::connect(_creds.wifiSsid, _creds.wifiPass, WIFI_CONNECT_TIMEOUT_MS))
  {
    if (secret_store::isPaired())
    {
      enterState(STATE_INIT_STREAMING_CAMERA);
    }
    else
    {
      enterState(STATE_PREPROVISION);
    }
    return;
  }

  DEBUG_PRINT("WiFi connect failed; will retry");
  enterState(STATE_NETWORK_RECOVERY);
}

static void doPreprovision()
{
  if (_preprovisionRetryAt != 0 && millis() < _preprovisionRetryAt)
  {
    delay(100);
    return;
  }

  preprovision_client::Result result = preprovision_client::verify(_creds);

  if (result == preprovision_client::SUCCESS)
  {
    DEBUG_PRINT("Preprovision verification accepted by server");
    secret_store::setPaired(true);
    enterState(STATE_INIT_STREAMING_CAMERA);
    return;
  }

  DEBUG_PRINT("Preprovision failed; will retry. Hold the reset button (5s) to start over.");
  _preprovisionRetryAt = millis() + PREPROVISION_RETRY_DELAY_MS;
}

static void doInitStreamingCamera()
{
  if (!frame_streamer::beginCamera())
  {
    DEBUG_PRINT("Camera init for streaming failed; will retry after reboot");
    delay(3000);
    ESP.restart();
    return;
  }
  _sessionRetryAt = 0;
  enterState(STATE_STREAMING);
}

static void doStreaming()
{
  if (!wifi_manager::isConnected())
  {
    frame_streamer::endSession();
    enterState(STATE_NETWORK_RECOVERY);
    return;
  }

  if (!frame_streamer::isSessionActive())
  {
    if (_sessionRetryAt != 0 && millis() < _sessionRetryAt)
    {
      delay(50);
      return;
    }
    if (!frame_streamer::startSession(_creds))
    {
      DEBUG_PRINT("Secure session start failed; backing off");
      _sessionRetryAt = millis() + STREAM_RETRY_DELAY_MS;
      return;
    }
    _sessionRetryAt = 0;
  }

  frame_streamer::tick();
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

  if (secret_store::isPaired())
  {
    enterState(STATE_STREAMING);
  }
  else
  {
    enterState(STATE_PREPROVISION);
  }
}

void loop()
{
  handleResetButton();

  if (DEBUG_ON)
    led_indicator::tick();

  status_led::tick();
  ir_cut_controller::tick();

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
  case STATE_INIT_STREAMING_CAMERA:
    doInitStreamingCamera();
    break;
  case STATE_STREAMING:
    doStreaming();
    break;
  case STATE_NETWORK_RECOVERY:
    doNetworkRecovery();
    break;
  case STATE_FATAL_ERROR:
    delay(500);
    break;
  }
}
