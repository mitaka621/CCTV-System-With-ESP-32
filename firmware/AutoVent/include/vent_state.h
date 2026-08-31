#pragma once

#include <Arduino.h>

enum class VentState : uint8_t
{
  Unknown = 0,
  Closed = 1,
  Opening = 2,
  Open = 3,
  Closing = 4
};
