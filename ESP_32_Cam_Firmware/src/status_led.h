#pragma once

namespace status_led
{
  enum Mode
  {
    OFF,
    BLINKING,
    SOLID
  };

  void begin();
  void setMode(Mode mode);
  void tick();
}
