#include "sensor_hub.h"
#include "config.h"
#include <Wire.h>
#include <math.h>
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>

namespace sensor_hub
{
  static constexpr float ACCEL_LSB = 4096.0f;
  static constexpr float GYRO_LSB = 65.5f;

  static bool _shtPresent = false;
  static bool _mpuPresent = false;

  static volatile int16_t _tempCenti = 0;
  static volatile uint16_t _humCenti = 0;
  static volatile bool _shtValid = false;

  static volatile bool _caseOpen = false;
  static volatile bool _caseSwitchInstalled = CASE_SWITCH_INSTALLED;

  static volatile uint8_t _eventMask = 0;
  static volatile bool _motionActive = false;

  static volatile float _moveThresholdG = MOTION_MOVE_G;
  static volatile float _rotateThresholdDps = MOTION_ROTATE_DPS;

  static float _baseX = 0.0f;
  static float _baseY = 0.0f;
  static float _baseZ = 1.0f;
  static uint32_t _freefallStart = 0;
  static bool _baseSeeded = false;
  static uint32_t _mpuStartMs = 0;

  static int _caseRawLast = -1;
  static uint32_t _caseStableSince = 0;

  static bool writeReg(uint8_t addr, uint8_t reg, uint8_t val)
  {
    Wire.beginTransmission(addr);
    Wire.write(reg);
    Wire.write(val);
    return Wire.endTransmission() == 0;
  }

  static bool readBurst(uint8_t addr, uint8_t reg, uint8_t *buf, size_t len)
  {
    Wire.beginTransmission(addr);
    Wire.write(reg);
    if (Wire.endTransmission(false) != 0)
      return false;
    size_t got = Wire.requestFrom((int)addr, (int)len);
    if (got != len)
      return false;
    for (size_t i = 0; i < len; i++)
      buf[i] = Wire.read();
    return true;
  }

  static uint8_t crc8Sht(const uint8_t *data, size_t len)
  {
    uint8_t crc = 0xFF;
    for (size_t i = 0; i < len; i++)
    {
      crc ^= data[i];
      for (int b = 0; b < 8; b++)
      {
        crc = (crc & 0x80) ? (uint8_t)((crc << 1) ^ 0x31) : (uint8_t)(crc << 1);
      }
    }
    return crc;
  }

  static bool detectSht()
  {
    Wire.beginTransmission(SHT3X_I2C_ADDR);
    return Wire.endTransmission() == 0;
  }

  static bool readSht()
  {
    Wire.beginTransmission(SHT3X_I2C_ADDR);
    Wire.write(0x24);
    Wire.write(0x00);
    if (Wire.endTransmission() != 0)
      return false;

    delay(20);

    uint8_t raw[6];
    size_t got = Wire.requestFrom((int)SHT3X_I2C_ADDR, 6);
    if (got != 6)
      return false;
    for (int i = 0; i < 6; i++)
      raw[i] = Wire.read();

    if (crc8Sht(raw, 2) != raw[2] || crc8Sht(raw + 3, 2) != raw[5])
      return false;

    uint16_t tRaw = (uint16_t)((raw[0] << 8) | raw[1]);
    uint16_t hRaw = (uint16_t)((raw[3] << 8) | raw[4]);

    float t = -45.0f + 175.0f * (float)tRaw / 65535.0f;
    float h = 100.0f * (float)hRaw / 65535.0f;
    if (h < 0.0f)
      h = 0.0f;
    if (h > 100.0f)
      h = 100.0f;

    _tempCenti = (int16_t)lroundf(t * 100.0f);
    _humCenti = (uint16_t)lroundf(h * 100.0f);
    _shtValid = true;
    return true;
  }

  static void scanI2c()
  {
    if (!DEBUG_ON)
      return;
    Serial.println("sensor_hub: I2C scan...");
    uint8_t count = 0;
    for (uint8_t addr = 1; addr < 127; addr++)
    {
      Wire.beginTransmission(addr);
      if (Wire.endTransmission() == 0)
      {
        Serial.printf("  found device at 0x%02X\n", addr);
        count++;
      }
    }
    Serial.printf("sensor_hub: I2C scan done, %u device(s)\n", count);
  }

  static bool initMpu()
  {
    Wire.beginTransmission(MPU6050_I2C_ADDR);
    if (Wire.endTransmission() != 0)
    {
      if (DEBUG_ON)
        Serial.printf("sensor_hub: MPU no ACK at 0x%02X\n", MPU6050_I2C_ADDR);
      return false;
    }

    uint8_t who[1];
    if (DEBUG_ON && readBurst(MPU6050_I2C_ADDR, 0x75, who, 1))
      Serial.printf("sensor_hub: MPU WHO_AM_I = 0x%02X\n", who[0]);

    if (!writeReg(MPU6050_I2C_ADDR, 0x6B, 0x01))
      return false;
    writeReg(MPU6050_I2C_ADDR, 0x1B, 0x08);
    writeReg(MPU6050_I2C_ADDR, 0x1C, 0x10);
    writeReg(MPU6050_I2C_ADDR, 0x1D, 0x03);
    writeReg(MPU6050_I2C_ADDR, 0x1A, 0x03);
    return true;
  }

  static void registerEvent(uint8_t bit)
  {
    _eventMask |= bit;
    _motionActive = true;
  }

  static void sampleMpu()
  {
    uint8_t raw[14];
    if (!readBurst(MPU6050_I2C_ADDR, 0x3B, raw, sizeof(raw)))
      return;

    int16_t ax = (int16_t)((raw[0] << 8) | raw[1]);
    int16_t ay = (int16_t)((raw[2] << 8) | raw[3]);
    int16_t az = (int16_t)((raw[4] << 8) | raw[5]);
    int16_t gx = (int16_t)((raw[8] << 8) | raw[9]);
    int16_t gy = (int16_t)((raw[10] << 8) | raw[11]);
    int16_t gz = (int16_t)((raw[12] << 8) | raw[13]);

    float fx = ax / ACCEL_LSB;
    float fy = ay / ACCEL_LSB;
    float fz = az / ACCEL_LSB;
    float total = sqrtf(fx * fx + fy * fy + fz * fz);

    uint32_t now = millis();

    if (!_baseSeeded)
    {
      _baseX = fx;
      _baseY = fy;
      _baseZ = fz;
      _baseSeeded = true;
      _mpuStartMs = now;
    }

    const float alpha = 0.02f;
    _baseX += alpha * (fx - _baseX);
    _baseY += alpha * (fy - _baseY);
    _baseZ += alpha * (fz - _baseZ);
    float acx = fx - _baseX;
    float acy = fy - _baseY;
    float acz = fz - _baseZ;
    float acMag = sqrtf(acx * acx + acy * acy + acz * acz);

    float gMag = sqrtf((float)gx * gx + (float)gy * gy + (float)gz * gz) / GYRO_LSB;

    if ((now - _mpuStartMs) < MOTION_WARMUP_MS)
      return;

    if (total >= MOTION_IMPACT_G)
      registerEvent(EVT_IMPACT);
    if (acMag >= _moveThresholdG)
      registerEvent(EVT_MOVE);
    if (gMag >= _rotateThresholdDps)
      registerEvent(EVT_ROTATE);

    if (total <= MOTION_FREEFALL_G)
    {
      if (_freefallStart == 0)
        _freefallStart = now;
      else if (now - _freefallStart >= MOTION_FREEFALL_MS)
        registerEvent(EVT_FALL);
    }
    else
    {
      _freefallStart = 0;
    }
  }

  static void sampleCase()
  {
    if (!_caseSwitchInstalled)
      return;

    int raw = digitalRead(CASE_SWITCH_PIN);
    uint32_t now = millis();

    if (raw != _caseRawLast)
    {
      _caseRawLast = raw;
      _caseStableSince = now;
      return;
    }

    if ((now - _caseStableSince) >= CASE_SWITCH_DEBOUNCE_MS)
    {
      _caseOpen = (raw == HIGH);
    }
  }

  static void samplerTask(void *)
  {
    uint32_t lastShtMs = 0;
    for (;;)
    {
      if (_mpuPresent)
        sampleMpu();

      sampleCase();

      uint32_t now = millis();
      if (_shtPresent && (now - lastShtMs) >= SHT3X_SAMPLE_INTERVAL_MS)
      {
        lastShtMs = now;
        readSht();
      }

      vTaskDelay(pdMS_TO_TICKS(MPU6050_SAMPLE_INTERVAL_MS));
    }
  }

  void begin()
  {
    if (_caseSwitchInstalled)
    {
      pinMode(CASE_SWITCH_PIN, INPUT_PULLUP);
      _caseRawLast = digitalRead(CASE_SWITCH_PIN);
      _caseStableSince = millis();
      _caseOpen = (_caseRawLast == HIGH);
    }
    else
    {
      _caseOpen = false;
    }

    Wire.begin(I2C_SDA_PIN, I2C_SCL_PIN, I2C_CLOCK_HZ);

    scanI2c();

    _shtPresent = detectSht();
    _mpuPresent = initMpu();

    if (DEBUG_ON)
    {
      Serial.printf("sensor_hub: SHT3x %s, MPU6050 %s\n",
                    _shtPresent ? "present" : "absent",
                    _mpuPresent ? "present" : "absent");
    }

    xTaskCreatePinnedToCore(samplerTask, "sensor-hub", 4096, nullptr, 2, nullptr, 1);
  }

  bool shtPresent() { return _shtPresent; }
  bool mpuPresent() { return _mpuPresent; }
  bool caseSwitchPresent() { return _caseSwitchInstalled; }

  float temperatureC() { return _shtValid ? _tempCenti / 100.0f : 0.0f; }
  float humidityPct() { return _shtValid ? _humCenti / 100.0f : 0.0f; }

  float dewPointC()
  {
    if (!_shtValid)
      return 0.0f;
    float t = _tempCenti / 100.0f;
    float rh = _humCenti / 100.0f;
    if (rh <= 0.0f)
      return 0.0f;
    const float a = 17.62f;
    const float b = 243.12f;
    float g = logf(rh / 100.0f) + (a * t) / (b + t);
    return (b * g) / (a - g);
  }

  bool isCaseOpen() { return _caseOpen; }

  bool isMotionActive()
  {
    return _motionActive;
  }

  uint8_t motionEventMask()
  {
    return _eventMask;
  }

  void resetMotion()
  {
    _eventMask = 0;
    _motionActive = false;
  }

  void setMotionTuning(float moveOffsetG, float rotateOffsetDps)
  {
    if (moveOffsetG < 0.0f)
      moveOffsetG = 0.0f;
    if (rotateOffsetDps < 0.0f)
      rotateOffsetDps = 0.0f;

    _moveThresholdG = MOTION_MOVE_G + moveOffsetG;
    _rotateThresholdDps = MOTION_ROTATE_DPS + rotateOffsetDps;
  }

  void setCaseSwitchInstalled(bool installed)
  {
    if (installed == _caseSwitchInstalled)
      return;

    _caseSwitchInstalled = installed;

    if (installed)
    {
      pinMode(CASE_SWITCH_PIN, INPUT_PULLUP);
      _caseRawLast = digitalRead(CASE_SWITCH_PIN);
      _caseStableSince = millis();
      _caseOpen = (_caseRawLast == HIGH);
    }
    else
    {
      _caseOpen = false;
    }
  }
}
