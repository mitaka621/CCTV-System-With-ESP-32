#pragma once

#include "alarm_verdict.h"
#include <Arduino.h>

namespace alarm_sensor
{
  void Begin();

  bool IsBeeping();

  uint32_t CountBeeps(uint32_t windowMs);

  AlarmVerdict Classify(uint32_t beepCount);
}
