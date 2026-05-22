#include "frame_streamer.h"
#include "config.h"
#include "wifi_manager.h"
#include "secrets.h"
#include "camera_pins.h"
#include <WiFi.h>
#include <esp_camera.h>
#include <esp_system.h>
#include <mbedtls/gcm.h>

namespace frame_streamer
{
  static WiFiClient _client;
  static String _serverIp;
  static uint8_t _sessionKey[32];
  static size_t _sessionKeyLen = 0;
  static uint8_t *_encryptionBuffer = nullptr;
  static unsigned long _lastConnectAttemptMs = 0;

  static bool encryptAesGcm(uint8_t *input, size_t inputLen,
                            uint8_t *output, uint8_t *iv, uint8_t *tag,
                            const uint8_t *key)
  {
    mbedtls_gcm_context gcm;
    mbedtls_gcm_init(&gcm);
    mbedtls_gcm_setkey(&gcm, MBEDTLS_CIPHER_ID_AES, key, 256);

    int ret = mbedtls_gcm_crypt_and_tag(&gcm, MBEDTLS_GCM_ENCRYPT,
                                        inputLen,
                                        iv, 12,
                                        NULL, 0,
                                        input, output,
                                        16, tag);
    mbedtls_gcm_free(&gcm);
    return ret == 0;
  }

  bool beginCamera()
  {
    if (!psramFound())
    {
      DEBUG_PRINT("PSRAM not detected. Streaming requires PSRAM.");
      return false;
    }

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
    config.frame_size = FRAMESIZE_FHD;
    config.jpeg_quality = 12;
    config.fb_count = 3;
    config.fb_location = CAMERA_FB_IN_PSRAM;
    config.grab_mode = CAMERA_GRAB_LATEST;

    esp_err_t err = esp_camera_init(&config);
    if (err != ESP_OK)
    {
      DEBUG_PRINT("Camera init failed: " + String(err));
      return false;
    }

    sensor_t *sensor = esp_camera_sensor_get();
    if (sensor != nullptr)
    {
      sensor->set_framesize(sensor, FRAMESIZE_FHD);
      sensor->set_quality(sensor, 12);
      sensor->set_whitebal(sensor, 1);
      sensor->set_awb_gain(sensor, 1);
      sensor->set_exposure_ctrl(sensor, 1);
      sensor->set_aec2(sensor, 1);
      sensor->set_gain_ctrl(sensor, 1);
      sensor->set_bpc(sensor, 1);
      sensor->set_wpc(sensor, 1);
      sensor->set_raw_gma(sensor, 1);
      sensor->set_lenc(sensor, 1);
      sensor->set_dcw(sensor, 1);
    }

    if (_encryptionBuffer == nullptr)
    {
      _encryptionBuffer = (uint8_t *)heap_caps_malloc(ENCRYPTION_BUFFER_SIZE, MALLOC_CAP_SPIRAM);
      if (_encryptionBuffer == nullptr)
      {
        DEBUG_PRINT("Failed to allocate encryption buffer in PSRAM");
        return false;
      }
    }

    return true;
  }

  void setServerIp(const String &serverIp)
  {
    _serverIp = serverIp;
  }

  void setSessionKey(const uint8_t *key, size_t keyLen)
  {
    if (keyLen != 32)
    {
      _sessionKeyLen = 0;
      return;
    }
    memcpy(_sessionKey, key, 32);
    _sessionKeyLen = 32;
  }

  bool hasSessionKey()
  {
    return _sessionKeyLen == 32;
  }

  static bool ensureConnected()
  {
    if (_client.connected())
    {
      return true;
    }
    if (_serverIp.length() == 0)
    {
      return false;
    }
    unsigned long now = millis();
    if (now - _lastConnectAttemptMs < STREAM_RETRY_DELAY_MS)
    {
      return false;
    }
    _lastConnectAttemptMs = now;

    DEBUG_PRINT("TCP connecting to " + _serverIp + ":" + String(ServerTcpPort));
    if (!_client.connect(_serverIp.c_str(), ServerTcpPort))
    {
      DEBUG_PRINT("TCP connect failed");
      return false;
    }
    _client.setNoDelay(true);
    DEBUG_PRINT("TCP connected");
    return true;
  }

  static bool sendFrame(camera_fb_t *fb)
  {
    if (_encryptionBuffer == nullptr || !hasSessionKey())
    {
      return false;
    }
    if (fb->len > ENCRYPTION_BUFFER_SIZE)
    {
      DEBUG_PRINT("Frame too large: " + String(fb->len));
      return false;
    }

    uint8_t mac[6];
    WiFi.macAddress(mac);

    uint8_t iv[12];
    esp_fill_random(iv, sizeof(iv));

    uint8_t tag[16];

    if (!encryptAesGcm(fb->buf, fb->len, _encryptionBuffer, iv, tag, _sessionKey))
    {
      return false;
    }

    uint32_t totalLen = 6 + sizeof(iv) + sizeof(tag) + fb->len;
    uint8_t header[4 + sizeof(mac) + sizeof(iv) + sizeof(tag)];
    header[0] = (uint8_t)(totalLen >> 24);
    header[1] = (uint8_t)(totalLen >> 16);
    header[2] = (uint8_t)(totalLen >> 8);
    header[3] = (uint8_t)(totalLen);
    memcpy(header + 4, mac, sizeof(mac));
    memcpy(header + 4 + sizeof(mac), iv, sizeof(iv));
    memcpy(header + 4 + sizeof(mac) + sizeof(iv), tag, sizeof(tag));

    if (_client.write(header, sizeof(header)) != sizeof(header))
    {
      return false;
    }
    if (_client.write(_encryptionBuffer, fb->len) != fb->len)
    {
      return false;
    }
    return true;
  }

  void tick()
  {
    if (!hasSessionKey())
    {
      delay(50);
      return;
    }

    if (!ensureConnected())
    {
      delay(50);
      return;
    }

    camera_fb_t *fb = esp_camera_fb_get();
    if (fb == nullptr)
    {
      DEBUG_PRINT("Camera capture failed");
      delay(20);
      return;
    }

    if (!sendFrame(fb))
    {
      DEBUG_PRINT("Frame send failed; will retry on next tick");
      _client.stop();
    }

    esp_camera_fb_return(fb);
  }
}
