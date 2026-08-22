#pragma once

#define FIRMWARE_VERSION "1.0.0"

#define BATTERY_ADC_PIN 1
#define BATTERY_DIVIDER_GROUND_PIN 6
#define BATTERY_DIVIDER_RATIO 2.0f
#define BATTERY_CALIBRATION_FACTOR 1.0f
#define BATTERY_SETTLE_MS 3
#define BATTERY_SAMPLE_COUNT 32

// Measured pattern: 253 ms beep, 84 ms gap, repeating with no long pause.
// The release window separates beeps, so it must sit above the 285 microsecond
// piezo period and below the 84 ms gap.
#define ALARM_PIN 4
#define ALARM_BEEP_RELEASE_MS 50
#define ALARM_LISTEN_MS 5000
#define ALARM_STILL_SOUNDING_MS 600
#define ALARM_MIN_BEEPS_FOR_FIRE 3

#define CHARGE_SENSE_PIN 5

#define RESET_BUTTON_PIN 7
#define RESET_BUTTON_HOLD_MS 5000
#define RESET_BUTTON_WINDOW_MS 6000

#define SLEEP_INTERVAL_SECONDS 1800
#define WELFARE_INTERVAL_SECONDS 86400
#define ALARM_BUSY_SLEEP_SECONDS 60
#define ALARM_REPEAT_SECONDS 300

#define NVS_NAMESPACE "settings"

#define WIFI_CONNECT_TIMEOUT_MS 20000

#define PROVISION_AP_SSID_PREFIX "SmokeDetector-Setup-"
#define PROVISION_AP_PASSWORD "campsetup1234"
#define PROVISION_AP_CHANNEL 6
#define PROVISION_AP_HIDDEN false
#define PROVISION_AP_MAX_CLIENTS 4
#define PROVISION_HTTP_PORT 80
#define PREPROVISION_RETRY_DELAY_MS 30000

// must match the server, do not change per device
#define DOMAIN_TAG "campr-provision-v1"
#define DATA_CHANNEL_DOMAIN_TAG "CAMPR-STREAM-V1"
#define DATA_CHANNEL_HKDF_INFO "CAMPR-STREAM-V1-derived"

#define DATA_CHANNEL_BUFFER_SIZE 4096
#define DATA_CHANNEL_HANDSHAKE_TIMEOUT_MS 10000
#define DATA_CHANNEL_SEND_TIMEOUT_MS 5000
#define DATA_CHANNEL_MAX_SESSION_MESSAGES 4000000000ULL
#define DATA_CHANNEL_MAX_SESSION_DURATION_MS (120UL * 60UL * 1000UL)
#define DATA_CHANNEL_MAX_MESSAGE_SIZE 256

#define DEVICE_COMMAND_VERSION 1
#define DETECTOR_PAYLOAD_VERSION 1
#define COMMAND_POLL_MS 0

#define ServerHttpsPort 7010
#define ServerTcpPort 7000

// Hardware serial hands bytes to a background task rather than the wire, so a
// short wake can reach deep sleep with the last lines still queued. Debug builds
// wait for that task to drain.
#define SERIAL_DRAIN_MS 150

#define DEBUG_ON true

//used for testing to display all of the functionallity in the Serial monitor by skipping preprovision and not sending anything to a server
#define NO_SERVER false

#define DEBUG_PRINT(x)   \
  do                     \
  {                      \
    if (DEBUG_ON)        \
      Serial.println(x); \
  } while (0)
