
#include <Arduino.h>
#include "mbedtls/md.h"
#include <WiFi.h>
#include <esp_camera.h>
#include "camera_pins.h"
#include "secrets.h"

#define CAMERA_MODEL_AI_THINKER


#define TCP_SERVER_IP "192.168.1.100"
#define TCP_SERVER_PORT 12345
#define DEBUG_ON true

#define DEBUG_PRINT(x) do { if (DEBUG_ON) Serial.println(x); } while(0)

bool initCamera();
bool connectWiFi();
bool sendFrameToServer(camera_fb_t *fb, WiFiClient &client);


WiFiClient client;

void setup()
{
  Serial.begin(115200);
  delay(1000);
  DEBUG_PRINT("Booting...");
  if (!connectWiFi())
  {
    DEBUG_PRINT("WiFi connection failed. Restarting...");
    delay(5000);
    ESP.restart();
  }
  if (!initCamera())
  {
    DEBUG_PRINT("Camera init failed. Restarting...");
    delay(5000);
    ESP.restart();
  }
  DEBUG_PRINT("Setup complete.");
}


void loop()
{
  if (!client.connected())
  {
    DEBUG_PRINT("Connecting to TCP server...");
    if (!client.connect(TCP_SERVER_IP, TCP_SERVER_PORT))
    {
      DEBUG_PRINT("TCP connection failed.");
      delay(2000);
      return;
    }
    DEBUG_PRINT("TCP connected.");
  }
  camera_fb_t *fb = esp_camera_fb_get();
  if (!fb)
  {
    DEBUG_PRINT("Camera capture failed");
    delay(100);
    return;
  }
  if (!sendFrameToServer(fb, client))
  {
    DEBUG_PRINT("Failed to send frame");
    client.stop();
  }
  else
  {
    DEBUG_PRINT("Frame sent");
  }
  esp_camera_fb_return(fb);
  delay(100);
}


bool connectWiFi()
{
  DEBUG_PRINT("Connecting to WiFi...");
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASS);
  unsigned long start = millis();
  while (WiFi.status() != WL_CONNECTED)
  {
    delay(500);
    if (millis() - start > 15000) return false;
  }
  DEBUG_PRINT("WiFi connected. IP: " + WiFi.localIP().toString());
  return true;
}

bool initCamera()
{
  camera_config_t config;
  config.ledc_channel = LEDC_CHANNEL_0;
  config.ledc_timer = LEDC_TIMER_0;
  config.pin_d0 = Y2_GPIO_NUM;
  config.pin_d1 = Y3_GPIO_NUM;
  config.pin_d2 = Y4_GPIO_NUM;
  config.pin_d3 = Y5_GPIO_NUM;
  config.pin_d4 = Y6_GPIO_NUM;
  config.pin_d5 = Y7_GPIO_NUM;
  config.pin_d6 = Y8_GPIO_NUM;
  config.pin_d7 = Y9_GPIO_NUM;
  config.pin_xclk = XCLK_GPIO_NUM;
  config.pin_pclk = PCLK_GPIO_NUM;
  config.pin_vsync = VSYNC_GPIO_NUM;
  config.pin_href = HREF_GPIO_NUM;
  config.pin_sccb_sda = SIOD_GPIO_NUM;
  config.pin_sccb_scl = SIOC_GPIO_NUM;
  config.pin_pwdn = PWDN_GPIO_NUM;
  config.pin_reset = RESET_GPIO_NUM;
  config.xclk_freq_hz = 20000000;
  config.pixel_format = PIXFORMAT_JPEG;
  config.frame_size = FRAMESIZE_HD;
  config.jpeg_quality = 12;
  config.fb_count = 1;
  DEBUG_PRINT("Initializing camera...");
  esp_err_t err = esp_camera_init(&config);
  if (err != ESP_OK)
  {
    DEBUG_PRINT("Camera init failed with error " + String(err));
    return false;
  }
  return true;
}

bool sendFrameToServer(camera_fb_t *fb, WiFiClient &client)
{
  uint32_t len = fb->len;
  uint8_t sizeBuf[4] =
  {
    (uint8_t)(len >> 24),
    (uint8_t)(len >> 16),
    (uint8_t)(len >> 8),
    (uint8_t)(len)
  };
  if (client.write(sizeBuf, 4) != 4) return false;
  if (client.write(fb->buf, fb->len) != fb->len) return false;
  return true;
}