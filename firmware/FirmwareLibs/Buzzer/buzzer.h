#pragma once

#include <Arduino.h>

namespace buzzer
{
  void begin();
  void setActive(bool active);
  bool isActive();
}
