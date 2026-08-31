#pragma once

#include <Arduino.h>

namespace fan_tachometer
{
  void Begin();

  void Tick();

  uint16_t Rpm();
}
