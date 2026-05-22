#pragma once

#define FIRMWARE_VERSION "2.0.0"

#define DOMAIN_TAG "campr-provision-v1"

#define RESET_BUTTON_PIN 14
#define RESET_BUTTON_HOLD_MS 5000

#define RGB_LED_PIN 48
#define RGB_LED_COUNT 1

#define NVS_NAMESPACE "settings"

#define WIFI_CONNECT_TIMEOUT_MS 20000
#define WIFI_RETRY_DELAY_MS 5000

#define PREPROVISION_RETRY_DELAY_MS 30000
#define STREAM_RETRY_DELAY_MS 10000

#define PROVISION_AP_SSID_PREFIX "CamPortal-Setup-"
#define PROVISION_AP_PASSWORD "campsetup1234"
#define PROVISION_AP_CHANNEL 6
#define PROVISION_AP_HIDDEN false
#define PROVISION_AP_MAX_CLIENTS 4
#define PROVISION_HTTP_PORT 80

#define ENCRYPTION_BUFFER_SIZE (1024 * 1024)

#define DEBUG_ON true

#define DEBUG_PRINT(x)   \
  do                     \
  {                      \
    if (DEBUG_ON)        \
      Serial.println(x); \
  } while (0)
