#pragma once

#include <Arduino.h>

enum class DeviceCommand : uint8_t
{
  None = 0,
  ResetSecurityAlarm = 1,
  ActivateBuzzerAlarm = 2,
  SaveNewConfig = 3
};
