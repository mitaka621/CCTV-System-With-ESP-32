#include "config.h"
#include "device_command.h"
#include "detector_event.h"
#include "alarm_sensor.h"
#include "battery_gauge.h"
#include "secret_store.h"
#include "wifi_manager.h"
#include "provision_ap.h"
#include "preprovision_client.h"
#include "data_channel.h"
#include <Arduino.h>
#include <ArduinoJson.h>
#include <WiFi.h>
#include <esp_sleep.h>

static constexpr size_t DETECTOR_PAYLOAD_LEN = 12;

RTC_DATA_ATTR static uint32_t _bootCount = 0;
RTC_DATA_ATTR static uint32_t _secondsSinceReport = 0;
RTC_DATA_ATTR static uint32_t _secondsSinceAlarm = 0;
RTC_DATA_ATTR static bool _alarmEverReported = false;
RTC_DATA_ATTR static bool _alarmOngoing = false;

static DeviceCredentials _creds;
static uint8_t _payload[DETECTOR_PAYLOAD_LEN];
static bool _provisioning = false;
static bool _radioStarted = false;

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

static void BuildPayload(DetectorEvent event, float percent, float volts, bool charging, bool sounding, uint32_t beeps)
{
  _payload[0] = DETECTOR_PAYLOAD_VERSION;
  _payload[1] = (uint8_t)event;
  _payload[2] = (percent < 0.0f) ? 0xFF : (uint8_t)lroundf(constrain(percent, 0.0f, 100.0f));
  PutBE16(_payload + 3, (uint16_t)lroundf(constrain(volts, 0.0f, 65.0f) * 1000.0f));
  _payload[5] = (charging ? 0x01 : 0x00) | (sounding ? 0x02 : 0x00);
  PutBE32(_payload + 6, _bootCount);
  _payload[10] = (uint8_t)min(beeps, (uint32_t)255);
  _payload[11] = 0;
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

  const char *label = doc["label"] | "unnamed";
  DEBUG_PRINT(String("Config: label=") + label);
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

static void PollCommands()
{
  uint8_t message[DATA_CHANNEL_MAX_MESSAGE_SIZE];
  size_t messageLen = 0;
  const uint32_t start = millis();

  while ((millis() - start) < COMMAND_POLL_MS)
  {
    while (data_channel::Receive(message, sizeof(message), messageLen))
    {
      if (messageLen < 2 || message[0] != DEVICE_COMMAND_VERSION)
      {
        DEBUG_PRINT("Command: unsupported message envelope");
        continue;
      }
      HandleCommand((DeviceCommand)message[1], message + 2, messageLen - 2);
    }
    delay(10);
  }
}

static bool LoadCredentials()
{
  if (secret_store::loadFromCompileTime(_creds))
    return true;
  return secret_store::loadFromNvs(_creds);
}

static bool SendEvent(DetectorEvent event, float percent, float volts, bool charging, bool sounding, uint32_t beeps)
{
  if (!wifi_manager::connect(_creds.wifiSsid, _creds.wifiPass, WIFI_CONNECT_TIMEOUT_MS))
  {
    DEBUG_PRINT("WiFi connect failed, dropping this report");
    _radioStarted = true;
    return false;
  }
  _radioStarted = true;

  if (!secret_store::isPaired())
  {
    if (preprovision_client::verify(_creds) != preprovision_client::SUCCESS)
    {
      DEBUG_PRINT("Preprovision verification failed");
      return false;
    }
    secret_store::setPaired(true);
  }

  if (!data_channel::Begin(_creds, ServerTcpPort))
  {
    DEBUG_PRINT("Data channel start failed");
    return false;
  }

  BuildPayload(event, percent, volts, charging, sounding, beeps);
  const bool sent = data_channel::Send(_payload, DETECTOR_PAYLOAD_LEN);
  if (!sent)
  {
    DEBUG_PRINT("Event send failed");
  }

  PollCommands();
  data_channel::End();
  return sent;
}

static void Sleep(uint32_t seconds, bool alarmMayBeSounding)
{
  // An instantaneous read lands in an 84 ms gap three times out of four, so a
  // train that is still running has to be measured over a window.
  const bool measureWindow = alarmMayBeSounding || _alarmOngoing;
  const bool sounding = measureWindow
                            ? (alarm_sensor::CountBeeps(ALARM_STILL_SOUNDING_MS) > 0)
                            : alarm_sensor::IsBeeping();
  _alarmOngoing = sounding;

  const bool buttonDown = digitalRead(RESET_BUTTON_PIN) == LOW;

  uint64_t wakeMask = 0;
  if (!sounding)
    wakeMask |= 1ULL << ALARM_PIN;
  if (!buttonDown)
    wakeMask |= 1ULL << RESET_BUTTON_PIN;

  if (wakeMask != 0)
    esp_sleep_enable_ext1_wakeup(wakeMask, ESP_EXT1_WAKEUP_ANY_LOW);

  if (sounding)
  {
    seconds = ALARM_BUSY_SLEEP_SECONDS;
    DEBUG_PRINT("Alarm still sounding, short sleep without the alarm wake armed");
  }
  else if (buttonDown)
  {
    seconds = ALARM_BUSY_SLEEP_SECONDS;
    DEBUG_PRINT("Button still held, short sleep without the button wake armed");
  }

  const uint32_t awakeSeconds = millis() / 1000UL;
  _secondsSinceReport += seconds + awakeSeconds;
  _secondsSinceAlarm += seconds + awakeSeconds;

  if (_radioStarted)
  {
    WiFi.disconnect(true);
    WiFi.mode(WIFI_OFF);
    _radioStarted = false;
  }

  battery_gauge::PrepareForSleep();

  esp_sleep_enable_timer_wakeup((uint64_t)seconds * 1000000ULL);

  DEBUG_PRINT(String("Sleeping for ") + seconds + " s");
  if (DEBUG_ON)
  {
    Serial.flush();
    delay(SERIAL_DRAIN_MS);
  }

  esp_deep_sleep_start();
}

static void ReportAndSleep(DetectorEvent event, uint32_t beeps, bool sounding)
{
  const float volts = battery_gauge::ReadVolts();
  const float percent = battery_gauge::VoltsToPercent(volts);
  const bool charging = battery_gauge::IsCharging();

  DEBUG_PRINT(String("Event ") + (int)event + ": " + percent + " %, " + volts + " V, charging=" + charging);

  if (!NO_SERVER && LoadCredentials() && SendEvent(event, percent, volts, charging, sounding, beeps))
  {
    _secondsSinceReport = 0;
  }

  Sleep(SLEEP_INTERVAL_SECONDS, sounding);
}

static void HandleAlarmWake()
{
  const uint32_t beeps = alarm_sensor::CountBeeps(ALARM_LISTEN_MS);
  const AlarmVerdict verdict = alarm_sensor::Classify(beeps);

  DEBUG_PRINT(String("Alarm wake: ") + beeps + " beeps in " + ALARM_LISTEN_MS + " ms");

  if (verdict == AlarmVerdict::Silent)
  {
    DEBUG_PRINT("Nothing heard, treating as a spurious wake");
    Sleep(SLEEP_INTERVAL_SECONDS, false);
    return;
  }

  const bool coolingDown = _alarmEverReported && (_secondsSinceAlarm < ALARM_REPEAT_SECONDS);
  if (coolingDown)
  {
    DEBUG_PRINT("Within the repeat window, not sending again");
    Sleep(SLEEP_INTERVAL_SECONDS, true);
    return;
  }

  const DetectorEvent event = (verdict == AlarmVerdict::FireAlarm)
                                  ? DetectorEvent::FireAlarm
                                  : DetectorEvent::AlarmLowBatteryChirp;

  _alarmEverReported = true;
  _secondsSinceAlarm = 0;

  ReportAndSleep(event, beeps, true);
}

static void HandleTimerWake()
{
  const bool charging = battery_gauge::IsCharging();
  const bool welfareDue = _secondsSinceReport >= WELFARE_INTERVAL_SECONDS;

  if (!charging && !welfareDue)
  {
    DEBUG_PRINT("Nothing to report this wake");
    Sleep(SLEEP_INTERVAL_SECONDS, false);
    return;
  }

  ReportAndSleep(charging ? DetectorEvent::BatteryCharging : DetectorEvent::BatteryWelfare, 0, false);
}

static void HandleButtonWake()
{
  const uint32_t pressStart = millis();

  while (digitalRead(RESET_BUTTON_PIN) == LOW)
  {
    if ((millis() - pressStart) >= RESET_BUTTON_HOLD_MS)
    {
      DEBUG_PRINT("Reset button held, clearing stored credentials");
      secret_store::clearAll();
      delay(500);
      ESP.restart();
      return;
    }
    delay(10);
  }

  DEBUG_PRINT("Reset button tapped, sending a manual report");
  ReportAndSleep(DetectorEvent::ManualCheck, 0, false);
}

static bool ResetButtonHeld()
{
  const uint32_t windowStart = millis();
  uint32_t pressStart = 0;

  while ((millis() - windowStart) < RESET_BUTTON_WINDOW_MS)
  {
    if (digitalRead(RESET_BUTTON_PIN) == LOW)
    {
      if (pressStart == 0)
        pressStart = millis();
      else if ((millis() - pressStart) >= RESET_BUTTON_HOLD_MS)
        return true;
    }
    else
    {
      pressStart = 0;
    }
    delay(10);
  }

  return false;
}

static void HandleColdBoot()
{
  if (ResetButtonHeld())
  {
    DEBUG_PRINT("Reset button held, clearing stored credentials");
    secret_store::clearAll();
    delay(500);
    ESP.restart();
    return;
  }

  if (LoadCredentials() || NO_SERVER)
  {
    ReportAndSleep(DetectorEvent::Boot, 0, false);
    return;
  }

  DEBUG_PRINT("No credentials stored, starting the setup access point");
  if (!provision_ap::begin())
  {
    DEBUG_PRINT("Failed to start the setup access point, sleeping instead");
    Sleep(SLEEP_INTERVAL_SECONDS, false);
    return;
  }

  _radioStarted = true;
  DEBUG_PRINT("Join Wi-Fi: " + provision_ap::apSsid() + " and scan the wizard code with your phone");
  _provisioning = true;
}

static void DoProvisioning()
{
  DeviceCredentials received;
  if (provision_ap::tick(received) != provision_ap::RECEIVED)
    return;

  _creds = received;
  if (!secret_store::saveToNvs(_creds))
  {
    DEBUG_PRINT("Failed to save credentials to storage");
  }
  secret_store::setPaired(false);

  delay(1500);
  provision_ap::end();
  _provisioning = false;

  ReportAndSleep(DetectorEvent::Boot, 0, false);
}

void setup()
{
  const esp_sleep_wakeup_cause_t wakeCause = esp_sleep_get_wakeup_cause();
  const bool coldBoot = (wakeCause != ESP_SLEEP_WAKEUP_TIMER) && (wakeCause != ESP_SLEEP_WAKEUP_EXT1);

  _bootCount++;

  if (DEBUG_ON)
  {
    Serial.begin(115200);
    if (coldBoot)
    {
      const uint32_t serialStart = millis();
      while (!Serial && (millis() - serialStart) < 3000)
      {
        delay(10);
      }
      delay(300);
    }
  }

  DEBUG_PRINT("=================================");
  DEBUG_PRINT("Smoke alarm detector " FIRMWARE_VERSION);
  DEBUG_PRINT(String("Wake cause ") + (int)wakeCause + ", boot " + _bootCount);
  DEBUG_PRINT("=================================");

  alarm_sensor::Begin();
  battery_gauge::Begin();
  pinMode(RESET_BUTTON_PIN, INPUT_PULLUP);

  if (!secret_store::begin())
  {
    DEBUG_PRINT("Failed to open the credential storage namespace");
  }

  if (wakeCause == ESP_SLEEP_WAKEUP_EXT1)
  {
    const uint64_t wokeBy = esp_sleep_get_ext1_wakeup_status();
    if ((wokeBy & (1ULL << RESET_BUTTON_PIN)) != 0)
      HandleButtonWake();
    else
      HandleAlarmWake();
    return;
  }

  if (wakeCause == ESP_SLEEP_WAKEUP_TIMER)
  {
    // A sounding alarm keeps the level wake disarmed, so the short timer is the
    // only thing that fires. Without this the alarm would never be re-examined.
    if (_alarmOngoing)
      HandleAlarmWake();
    else
      HandleTimerWake();
    return;
  }

  HandleColdBoot();
}

void loop()
{
  if (_provisioning)
  {
    DoProvisioning();
    return;
  }

  delay(100);
}
