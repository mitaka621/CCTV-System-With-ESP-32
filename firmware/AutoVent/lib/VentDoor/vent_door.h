#pragma once

#include "vent_state.h"
#include <Arduino.h>

namespace vent_door
{
  void Begin();

  bool Open();

  bool Close();

  VentState State();
}
