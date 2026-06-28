#include "frame_streamer.h"
#include "config.h"
#include "secrets.h"
#include "secure_session.h"
#include "camera_pins.h"
#include "ir_cut_controller.h"
#include <esp_camera.h>

namespace frame_streamer
{
  struct StreamStats
  {
    uint32_t frameCount;
    uint32_t failedSends;
    uint32_t captureFailures;
    uint32_t captureReady;
    uint64_t captureUsTotal;
    uint64_t encryptUsTotal;
    uint64_t sendUsTotal;
    uint32_t captureUsMax;
    uint32_t encryptUsMax;
    uint32_t sendUsMax;
    uint64_t frameBytesTotal;
    uint32_t frameBytesMax;
    unsigned long windowStartMs;
  };

  static StreamStats _stats = {};

  static void resetStats(unsigned long now)
  {
    _stats.frameCount = 0;
    _stats.failedSends = 0;
    _stats.captureFailures = 0;
    _stats.captureReady = 0;
    _stats.captureUsTotal = 0;
    _stats.encryptUsTotal = 0;
    _stats.sendUsTotal = 0;
    _stats.captureUsMax = 0;
    _stats.encryptUsMax = 0;
    _stats.sendUsMax = 0;
    _stats.frameBytesTotal = 0;
    _stats.frameBytesMax = 0;
    _stats.windowStartMs = now;
  }

  static void logLightStatus()
  {
    char buf[64];
    snprintf(buf, sizeof(buf), "[LIGHT] %d -> %s",
             ir_cut_controller::lastValue(),
             ir_cut_controller::isNight() ? "NIGHTTIME" : "DAYTIME");
    DEBUG_PRINT(buf);
  }

  static void maybeLogStats()
  {
    if (!DEBUG_ON)
      return;

    unsigned long now = millis();
    if (_stats.windowStartMs == 0)
    {
      _stats.windowStartMs = now;
      return;
    }
    if ((now - _stats.windowStartMs) < STREAM_STATS_LOG_INTERVAL_MS)
      return;

    if (_stats.frameCount == 0)
    {
      char buf[160];
      snprintf(buf, sizeof(buf),
               "[STREAM] no frames sent in last %lums | capture failures %u | fb_count %d",
               (unsigned long)(now - _stats.windowStartMs),
               _stats.captureFailures,
               STREAM_CAMERA_FB_COUNT);
      DEBUG_PRINT(buf);
      logLightStatus();
      resetStats(now);
      return;
    }

    float elapsedSec = (now - _stats.windowStartMs) / 1000.0f;
    float fps = _stats.frameCount / elapsedSec;
    uint32_t avgCaptureMs = (uint32_t)((_stats.captureUsTotal / _stats.frameCount) / 1000ULL);
    uint32_t avgEncryptMs = (uint32_t)((_stats.encryptUsTotal / _stats.frameCount) / 1000ULL);
    uint32_t avgSendMs = (uint32_t)((_stats.sendUsTotal / _stats.frameCount) / 1000ULL);
    uint32_t maxCaptureMs = _stats.captureUsMax / 1000;
    uint32_t maxEncryptMs = _stats.encryptUsMax / 1000;
    uint32_t maxSendMs = _stats.sendUsMax / 1000;
    uint32_t avgKB = (uint32_t)((_stats.frameBytesTotal / _stats.frameCount) / 1024ULL);
    uint32_t maxKB = _stats.frameBytesMax / 1024;
    float bufReadyPct = 100.0f * _stats.captureReady / _stats.frameCount;

    char buf[256];
    snprintf(buf, sizeof(buf),
             "[STREAM] %.1f FPS | cap %u/%ums | enc %u/%ums | send %u/%ums | %uKB avg %uKB max | fb %d cap, bufReady %.0f%% | frames %u failed %u capFail %u",
             fps,
             avgCaptureMs, maxCaptureMs,
             avgEncryptMs, maxEncryptMs,
             avgSendMs, maxSendMs,
             avgKB, maxKB,
             STREAM_CAMERA_FB_COUNT,
             bufReadyPct,
             _stats.frameCount,
             _stats.failedSends,
             _stats.captureFailures);
    DEBUG_PRINT(buf);
    logLightStatus();

    resetStats(now);
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
    config.xclk_freq_hz = 30000000;
    config.pixel_format = PIXFORMAT_JPEG;
    config.frame_size = CAMERA_RESOLUTION;
    config.jpeg_quality = 12;
    config.fb_count = STREAM_CAMERA_FB_COUNT;
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
      sensor->set_framesize(sensor, CAMERA_RESOLUTION);
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

    ir_cut_controller::applyColorMode();

    return true;
  }

  bool startSession(const DeviceCredentials &creds)
  {
    return secure_session::begin(creds, ServerTcpPort);
  }

  bool isSessionActive()
  {
    return secure_session::isActive();
  }

  void endSession()
  {
    secure_session::end();
  }

  void tick()
  {
    if (!secure_session::isActive())
    {
      delay(50);
      return;
    }

    unsigned long captureStartUs = micros();
    camera_fb_t *fb = esp_camera_fb_get();
    uint32_t captureUs = (uint32_t)(micros() - captureStartUs);

    if (fb == nullptr)
    {
      DEBUG_PRINT("Camera capture failed");
      _stats.captureFailures++;
      maybeLogStats();
      delay(20);
      return;
    }

    secure_session::FrameTiming timing = {};
    bool sendOk = secure_session::sendFrame(fb->buf, fb->len, (uint32_t)fb->width, (uint32_t)fb->height, &timing);
    if (!sendOk)
    {
      DEBUG_PRINT("Secure frame send failed");
      _stats.failedSends++;
    }

    _stats.frameCount++;
    if (captureUs < STREAM_CAPTURE_READY_THRESHOLD_US)
      _stats.captureReady++;
    _stats.captureUsTotal += captureUs;
    if (captureUs > _stats.captureUsMax)
      _stats.captureUsMax = captureUs;
    _stats.encryptUsTotal += timing.encryptUs;
    if (timing.encryptUs > _stats.encryptUsMax)
      _stats.encryptUsMax = timing.encryptUs;
    _stats.sendUsTotal += timing.sendUs;
    if (timing.sendUs > _stats.sendUsMax)
      _stats.sendUsMax = timing.sendUs;
    _stats.frameBytesTotal += fb->len;
    if (fb->len > _stats.frameBytesMax)
      _stats.frameBytesMax = (uint32_t)fb->len;

    esp_camera_fb_return(fb);

    maybeLogStats();
  }
}
