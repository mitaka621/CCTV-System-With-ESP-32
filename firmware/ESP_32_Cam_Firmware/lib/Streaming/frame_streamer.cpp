#include "frame_streamer.h"
#include "config.h"
#include "secrets.h"
#include "data_channel.h"
#include "camera_pins.h"
#include "ir_cut_controller.h"
#include "sensor_hub.h"
#include <esp_camera.h>
#include <math.h>

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

  static constexpr size_t TELEMETRY_PAYLOAD_LEN = 44;
  static constexpr uint8_t TELEMETRY_VERSION = 2;

  static constexpr size_t RESOLUTION_HEADER_LEN = 8;
  static constexpr size_t TELEMETRY_LEN_FIELD = 2;
  static constexpr size_t FRAME_HEADER_LEN = RESOLUTION_HEADER_LEN + TELEMETRY_LEN_FIELD;

  static uint8_t _telemetryBuf[TELEMETRY_PAYLOAD_LEN] = {TELEMETRY_VERSION};
  static uint8_t _frameHeader[FRAME_HEADER_LEN] = {};

  static uint16_t clampU16(uint32_t v)
  {
    return v > 0xFFFF ? (uint16_t)0xFFFF : (uint16_t)v;
  }

  static void putBE16(uint8_t *p, uint16_t v)
  {
    p[0] = (uint8_t)(v >> 8);
    p[1] = (uint8_t)v;
  }

  static void putBE32(uint8_t *p, uint32_t v)
  {
    p[0] = (uint8_t)(v >> 24);
    p[1] = (uint8_t)(v >> 16);
    p[2] = (uint8_t)(v >> 8);
    p[3] = (uint8_t)v;
  }

  static void buildTelemetry(float fps,
                             uint32_t avgCaptureMs, uint32_t maxCaptureMs,
                             uint32_t avgEncryptMs, uint32_t maxEncryptMs,
                             uint32_t avgSendMs, uint32_t maxSendMs,
                             uint32_t avgKB, uint32_t maxKB,
                             uint8_t bufReadyPct)
  {
    uint8_t *b = _telemetryBuf;
    b[0] = TELEMETRY_VERSION;
    putBE16(b + 1, (uint16_t)(fps * 10.0f + 0.5f));
    putBE16(b + 3, clampU16(avgCaptureMs));
    putBE16(b + 5, clampU16(maxCaptureMs));
    putBE16(b + 7, clampU16(avgEncryptMs));
    putBE16(b + 9, clampU16(maxEncryptMs));
    putBE16(b + 11, clampU16(avgSendMs));
    putBE16(b + 13, clampU16(maxSendMs));
    putBE16(b + 15, clampU16(avgKB));
    putBE16(b + 17, clampU16(maxKB));
    b[19] = bufReadyPct;
    putBE32(b + 20, _stats.frameCount);
    putBE32(b + 24, _stats.failedSends);
    putBE32(b + 28, _stats.captureFailures);

    int16_t lightValue = ir_cut_controller::sensorPresent() ? (int16_t)ir_cut_controller::lastValue() : 0;
    putBE16(b + 32, (uint16_t)lightValue);

    uint8_t flags = 0;
    if (ir_cut_controller::isNight())
      flags |= 0x01;
    if (ir_cut_controller::sensorPresent())
      flags |= 0x02;
    b[34] = flags;

    int16_t tempCenti = 0;
    uint16_t humCenti = 0;
    int16_t dewCenti = 0;
    if (sensor_hub::shtPresent())
    {
      tempCenti = (int16_t)lroundf(sensor_hub::temperatureC() * 100.0f);
      humCenti = (uint16_t)lroundf(sensor_hub::humidityPct() * 100.0f);
      dewCenti = (int16_t)lroundf(sensor_hub::dewPointC() * 100.0f);
    }
    putBE16(b + 35, (uint16_t)tempCenti);
    putBE16(b + 37, humCenti);
    putBE16(b + 39, (uint16_t)dewCenti);

    uint8_t sensorFlags = 0;
    if (sensor_hub::shtPresent())
      sensorFlags |= 0x01;
    if (sensor_hub::mpuPresent())
      sensorFlags |= 0x02;
    if (sensor_hub::caseSwitchPresent())
      sensorFlags |= 0x04;
    b[41] = sensorFlags;

    uint8_t statusFlags = 0;
    if (sensor_hub::isCaseOpen())
      statusFlags |= 0x01;
    if (sensor_hub::isMotionActive())
      statusFlags |= 0x02;
    b[42] = statusFlags;

    b[43] = sensor_hub::motionEventMask();
  }

  static void refreshLiveSecurityTelemetry()
  {
    uint8_t statusFlags = 0;
    if (sensor_hub::isCaseOpen())
      statusFlags |= 0x01;
    if (sensor_hub::isMotionActive())
      statusFlags |= 0x02;
    _telemetryBuf[42] = statusFlags;
    _telemetryBuf[43] = sensor_hub::motionEventMask();
  }

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
    if (ir_cut_controller::isForceBlackAndWhite())
    {
      DEBUG_PRINT("[LIGHT] forced -> BLACK & WHITE");
      return;
    }
    if (!ir_cut_controller::sensorPresent())
    {
      DEBUG_PRINT("[LIGHT] no sensor -> COLOR");
      return;
    }
    snprintf(buf, sizeof(buf), "[LIGHT] %d -> %s",
             ir_cut_controller::lastValue(),
             ir_cut_controller::isNight() ? "NIGHTTIME" : "DAYTIME");
    DEBUG_PRINT(buf);
  }

  static void maybeRefreshTelemetryAndLog()
  {
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
      buildTelemetry(0.0f, 0, 0, 0, 0, 0, 0, 0, 0, 0);

      if (DEBUG_ON)
      {
        char buf[160];
        snprintf(buf, sizeof(buf),
                 "[STREAM] no frames sent in last %lums | capture failures %u | fb_count %d",
                 (unsigned long)(now - _stats.windowStartMs),
                 _stats.captureFailures,
                 STREAM_CAMERA_FB_COUNT);
        DEBUG_PRINT(buf);
        logLightStatus();
      }

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

    buildTelemetry(fps, avgCaptureMs, maxCaptureMs, avgEncryptMs, maxEncryptMs,
                   avgSendMs, maxSendMs, avgKB, maxKB, (uint8_t)(bufReadyPct + 0.5f));

    if (DEBUG_ON)
    {
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

      uint8_t evt = sensor_hub::motionEventMask();
      char sbuf[224];
      snprintf(sbuf, sizeof(sbuf),
               "[SENSORS] temp %.1fC | hum %.1f%% | dew %.1fC | case %s | motion %s | evt 0x%02X%s%s%s%s | sht %d mpu %d sw %d",
               sensor_hub::temperatureC(),
               sensor_hub::humidityPct(),
               sensor_hub::dewPointC(),
               sensor_hub::isCaseOpen() ? "OPEN" : "closed",
               sensor_hub::isMotionActive() ? "ACTIVE" : "idle",
               evt,
               (evt & sensor_hub::EVT_MOVE) ? " MOVE" : "",
               (evt & sensor_hub::EVT_IMPACT) ? " IMPACT" : "",
               (evt & sensor_hub::EVT_FALL) ? " FALL" : "",
               (evt & sensor_hub::EVT_ROTATE) ? " ROTATE" : "",
               sensor_hub::shtPresent(),
               sensor_hub::mpuPresent(),
               sensor_hub::caseSwitchPresent());
      DEBUG_PRINT(sbuf);

      logLightStatus();
    }

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
    return data_channel::Begin(creds, ServerTcpPort);
  }

  bool isSessionActive()
  {
    return data_channel::IsActive();
  }

  void endSession()
  {
    data_channel::End();
  }

  void tick()
  {
    if (!data_channel::IsActive())
    {
      delay(50);
      return;
    }

    maybeRefreshTelemetryAndLog();

    unsigned long captureStartUs = micros();
    camera_fb_t *fb = esp_camera_fb_get();
    uint32_t captureUs = (uint32_t)(micros() - captureStartUs);

    if (fb == nullptr)
    {
      DEBUG_PRINT("Camera capture failed");
      _stats.captureFailures++;
      delay(20);
      return;
    }

    refreshLiveSecurityTelemetry();

    putBE32(_frameHeader, (uint32_t)fb->width);
    putBE32(_frameHeader + 4, (uint32_t)fb->height);
    putBE16(_frameHeader + RESOLUTION_HEADER_LEN, (uint16_t)TELEMETRY_PAYLOAD_LEN);

    data_channel::Segment segments[3] = {
        {_frameHeader, FRAME_HEADER_LEN},
        {_telemetryBuf, TELEMETRY_PAYLOAD_LEN},
        {fb->buf, fb->len}};

    data_channel::SendTiming timing = {};
    bool sendOk = data_channel::SendSegments(segments, 3, &timing);
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
  }
}
