#pragma once

#include <Arduino.h>

namespace battery_gauge
{
  void Begin();

  float ReadVolts();

  float VoltsToPercent(float volts);

  float ReadChargeSenseVolts();

  bool IsCharging();

  void PrepareForSleep();
}
