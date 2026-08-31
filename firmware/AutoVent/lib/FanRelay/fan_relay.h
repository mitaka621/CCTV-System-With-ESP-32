#pragma once

#include <Arduino.h>

namespace fan_relay
{
  void Begin();

  void SetActive(bool active);

  bool IsActive();
}
