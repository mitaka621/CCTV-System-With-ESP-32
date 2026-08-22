#pragma once

#include <Arduino.h>

enum class DetectorEvent : uint8_t
{
  None = 0,
  FireAlarm = 1,
  AlarmLowBatteryChirp = 2,
  BatteryWelfare = 3,
  BatteryCharging = 4,
  Boot = 5,
  ManualCheck = 6
};
