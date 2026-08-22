#pragma once

#include <Arduino.h>

enum class AlarmVerdict : uint8_t
{
  Silent = 0,
  LowBatteryChirp = 1,
  FireAlarm = 2
};
