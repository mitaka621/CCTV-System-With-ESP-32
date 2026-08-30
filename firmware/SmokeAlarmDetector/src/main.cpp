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

static constexpr size_t DETECTOR_PAYLOAD_LEN = 15;

RTC_DATA_ATTR static uint32_t _bootCount = 0;
RTC_DATA_ATTR static uint32_t _secondsSinceReport = 0;
RTC_DATA_ATTR static uint32_t _secondsSinceAlarm = 0;
RTC_DATA_ATTR static bool _alarmActive = false;

static DeviceCredentials _creds;
static uint8_t _payload[DETECTOR_PAYLOAD_LEN];
static bool _provisioning = false;
static bool _radioStarted = false;
static bool _configChanged = false;

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

static void BuildPayload(DetectorEvent event, float percent, float volts, float chargeSenseVolts, bool charging, bool sounding, uint32_t beeps)
{
  _payload[0] = DETECTOR_PAYLOAD_VERSION;
  _payload[1] = (uint8_t)event;
  _payload[2] = (percent < 0.0f) ? 0xFF : (uint8_t)lroundf(constrain(percent, 0.0f, 100.0f));
  PutBE16(_payload + 3, (uint16_t)lroundf(constrain(volts, 0.0f, 65.0f) * 1000.0f));
  _payload[5] = (charging ? 0x01 : 0x00) | (sounding ? 0x02 : 0x00);
  PutBE32(_payload + 6, _bootCount);
  _payload[10] = (uint8_t)min(beeps, (uint32_t)255);
  PutBE16(_payload + 11, (uint16_t)lroundf(constrain(chargeSenseVolts, 0.0f, 65.0f) * 1000.0f));
  PutBE16(_payload + 13, (uint16_t)lroundf(constrain(battery_gauge::GetChargeSenseThreashold(), 0.0f, 65.0f) * 1000.0f));
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
  const char* nvsKey=battery_gauge::GetChargeSenseNVSKey();

  if (!doc[nvsKey].is<float>())
  {
    DEBUG_PRINT(String("Config: no numeric ") + nvsKey + " in payload, ignoring");
    return;
  }

  float newChargeSenseThresholdVoltage = doc[nvsKey].as<float>();

  if (fabsf(newChargeSenseThresholdVoltage - battery_gauge::GetChargeSenseThreashold()) < CHARGE_SENSE_THRESHOLD_EPSILON_VOLTS)
  {
    DEBUG_PRINT("Config: charge sense threshold already in effect, ignoring");
    return;
  }

  if(!battery_gauge::SetNewChargeSenseThreashold(newChargeSenseThresholdVoltage))
    return;

  String stringThreashold=String(newChargeSenseThresholdVoltage);
  secret_store::saveToNvs(nvsKey,stringThreashold);

  DEBUG_PRINT(String("New charge sense threshold saved: ") + stringThreashold);

  _configChanged = true;
}

static void HandleCommand(DeviceCommand command, const uint8_t *payload, size_t payloadLen)
{
  switch (command)
  {
  case DeviceCommand::SaveNewConfig:
    ApplyNewConfig(payload, payloadLen);
    if (_configChanged)
    {
      ESP.restart();
      return;
    }
    break;
  default:
    DEBUG_PRINT(String("Command: ignored code ") + (int)command);
    break;
  }
}

static bool WaitForAck(uint32_t timeoutMs)
{
  uint8_t message[DATA_CHANNEL_MAX_MESSAGE_SIZE];
  size_t messageLen = 0;
  const uint32_t start = millis();

  while ((millis() - start) < timeoutMs)
  {
    while (data_channel::Receive(message, sizeof(message), messageLen))
    {
      if (messageLen < 2 || message[0] != DEVICE_COMMAND_VERSION)
      {
        DEBUG_PRINT("Command: unsupported message envelope");
        continue;
      }

      const DeviceCommand command = (DeviceCommand)message[1];

      if (command == DeviceCommand::PayloadAck)
      {
        DEBUG_PRINT(String("Server acknowledged the payload after ") + (millis() - start) + " ms");
        return true;
      }

      HandleCommand(command, message + 2, messageLen - 2);
    }

    if (!data_channel::IsActive())
    {
      DEBUG_PRINT("Data channel dropped before the acknowledgement arrived");
      return false;
    }

    delay(5);
  }

  DEBUG_PRINT(String("No acknowledgement within ") + timeoutMs + " ms, treating the report as lost");
  return false;
}

static bool LoadCredentials()
{
  if (secret_store::loadFromCompileTime(_creds))
    return true;
  return secret_store::loadFromNvs(_creds);
}

static void LoadConfigFromNVS()
{
  String voltage;

  if(!secret_store::loadFromNvs(battery_gauge::GetChargeSenseNVSKey(), voltage))
  {
    DEBUG_PRINT(String("Config: no stored charge sense threshold, using ") + CHARGE_SENSE_THRESHOLD_VOLTS + " V");
    return;
  }
  
    battery_gauge::SetNewChargeSenseThreashold(voltage.toFloat());

  DEBUG_PRINT(String("Config: charge sense threshold restored from storage: ") + voltage);
}

static bool DeliverPayload()
{
  for (int attempt = 1; attempt <= MAX_SEND_ATTEMPTS; attempt++)
  {
    if (!data_channel::IsActive() && !data_channel::Begin(_creds, ServerTcpPort))
    {
      DEBUG_PRINT(String("Attempt ") + attempt + ": data channel would not open");
    }
    else
    {
      data_channel::SendTiming timing;
      if (!data_channel::Send(_payload, DETECTOR_PAYLOAD_LEN, &timing))
      {
        DEBUG_PRINT(String("Attempt ") + attempt + ": data channel rejected the payload");
      }
      else
      {
        DEBUG_PRINT(String("Attempt ") + attempt + ": queued, encrypt " + timing.encryptUs + " us");
        if (WaitForAck(ACK_TIMEOUT_MS))
          return true;
      }
    }

    data_channel::End();

    if (attempt < MAX_SEND_ATTEMPTS)
    {
      DEBUG_PRINT(String("Retrying in ") + SEND_RETRY_DELAY_MS + " ms");
      delay(SEND_RETRY_DELAY_MS);
    }
  }

  DEBUG_PRINT(String("Gave up after ") + MAX_SEND_ATTEMPTS + " attempts, the report will be retried on the next wake");
  return false;
}

static bool SendEvent(DetectorEvent event, float percent, float volts,float chargeSenseVolts, bool charging, bool sounding, uint32_t beeps)
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

  BuildPayload(event, percent, volts, chargeSenseVolts, charging, sounding, beeps);

  if (DEBUG_ON)
  {
    String hex;
    for (size_t i = 0; i < DETECTOR_PAYLOAD_LEN; i++)
    {
      if (_payload[i] < 0x10)
        hex += '0';
      hex += String(_payload[i], HEX);
      hex += ' ';
    }
    DEBUG_PRINT(String("Payload ") + DETECTOR_PAYLOAD_LEN + " bytes: " + hex);
  }

  const bool acknowledged = DeliverPayload();
  data_channel::End();
  return acknowledged;
}

static void Sleep(uint32_t seconds)
{
  uint64_t wakeMask = 0;
  if (!_alarmActive)
    wakeMask |= 1ULL << ALARM_PIN;

  wakeMask |= 1ULL << RESET_BUTTON_PIN;

  if (wakeMask != 0)
    esp_sleep_enable_ext1_wakeup(wakeMask, ESP_EXT1_WAKEUP_ANY_LOW);

  if (_alarmActive)
  {
    seconds = ALARM_BUSY_SLEEP_SECONDS;
    DEBUG_PRINT("Alarm still sounding, short sleep without the alarm wake armed");
  }

  const uint32_t awakeSeconds = millis() / 1000UL;
  _secondsSinceReport += seconds + awakeSeconds;
  _secondsSinceAlarm += seconds + awakeSeconds;

  if (_radioStarted)
  {
    delay(NETWORK_SETTLE_MS);
    DEBUG_PRINT(String("Radio settle ") + NETWORK_SETTLE_MS + " ms done, powering Wi-Fi down");
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

static void ReportAndSleep(DetectorEvent event, uint32_t beeps)
{
  const float volts = battery_gauge::ReadVolts();
  const float chargeSenseVolts = battery_gauge::ReadChargeSenseVolts();
  const float percent = battery_gauge::VoltsToPercent(volts);
  const bool charging = battery_gauge::IsCharging();

  DEBUG_PRINT(String("Event ") + (int)event + ": " + percent + " %, " + volts + " V, charge sense " + chargeSenseVolts + " V, charging=" + charging);

  const bool delivered = !NO_SERVER && LoadCredentials() && SendEvent(event, percent, volts, chargeSenseVolts, charging, _alarmActive, beeps);

  if (delivered)
  {
    _secondsSinceReport = 0;

    if (event == DetectorEvent::FireAlarm || event == DetectorEvent::AlarmLowBatteryChirp)
    {
      _secondsSinceAlarm = 0;
    }
  }

  Sleep(SLEEP_INTERVAL_SECONDS);
}

static void HandleAlarmWake()
{
  const uint32_t beeps = alarm_sensor::CountBeeps(ALARM_LISTEN_MS);
  const AlarmVerdict verdict = alarm_sensor::Classify(beeps);

  DEBUG_PRINT(String("Alarm wake: ") + beeps + " beeps in " + ALARM_LISTEN_MS + " ms");

  if (verdict == AlarmVerdict::Silent)
  {
    _alarmActive = false;

    DEBUG_PRINT("Nothing heard, alarm not active");

    const bool charging = battery_gauge::IsCharging();

    ReportAndSleep(charging ? DetectorEvent::BatteryCharging : DetectorEvent::None, 0);
    return;
  }

  const DetectorEvent event = (verdict == AlarmVerdict::FireAlarm)
                                  ? DetectorEvent::FireAlarm
                                  : DetectorEvent::AlarmLowBatteryChirp;

  if(event==DetectorEvent::FireAlarm)
  {
    _alarmActive = true;
  }
  else
  {
    _alarmActive = false;
  }

  ReportAndSleep(event, beeps);
}

static void HandleTimerWake()
{ 
  const bool charging = battery_gauge::IsCharging();
  const bool welfareDue = _secondsSinceReport >= WELFARE_INTERVAL_SECONDS;

  if (!charging && !welfareDue)
  {
    DEBUG_PRINT("Nothing to report this wake");
    Sleep(SLEEP_INTERVAL_SECONDS);
    return;
  }

  ReportAndSleep(charging ? DetectorEvent::BatteryCharging : DetectorEvent::BatteryWelfare, 0);
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

  ReportAndSleep(battery_gauge::IsCharging() ? DetectorEvent::BatteryCharging : DetectorEvent::None, 0);
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
    ReportAndSleep(battery_gauge::IsCharging() ? DetectorEvent::BatteryCharging : DetectorEvent::None, 0);
    return;
  }

  DEBUG_PRINT("No credentials stored, starting the setup access point");
  if (!provision_ap::begin())
  {
    DEBUG_PRINT("Failed to start the setup access point, sleeping instead");
    Sleep(SLEEP_INTERVAL_SECONDS);
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

  ReportAndSleep(battery_gauge::IsCharging() ? DetectorEvent::BatteryCharging : DetectorEvent::None, 0);
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

  LoadConfigFromNVS();

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
    if (_alarmActive)
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
