#pragma once

#include <Arduino.h>

namespace sensor_hub
{
  enum EventBits : uint8_t
  {
    EVT_MOVE = 0x01,
    EVT_IMPACT = 0x02,
    EVT_FALL = 0x04,
    EVT_ROTATE = 0x08,
  };

  void begin();

  bool shtPresent();
  bool mpuPresent();
  bool caseSwitchPresent();

  float temperatureC();
  float humidityPct();
  float dewPointC();

  bool isCaseOpen();
  bool isMotionActive();
  uint8_t motionEventMask();
  void resetMotion();

  void setCaseSwitchInstalled(bool installed);

  void setMotionTuning(float moveOffsetG, float rotateOffsetDps);
}
