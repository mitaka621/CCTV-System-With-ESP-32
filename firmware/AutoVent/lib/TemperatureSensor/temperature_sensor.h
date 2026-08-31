#pragma once

#include <Arduino.h>

namespace temperature_sensor
{
  void Begin();

  void Tick();

  bool IsValid();

  float LastCelsius();
}
