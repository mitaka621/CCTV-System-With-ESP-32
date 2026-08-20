#pragma once

namespace ir_cut_controller
{
  void begin();
  void tick();
  void applyColorMode();
  bool isNight();
  bool isForceBlackAndWhite();
  bool sensorPresent();
  int lastValue();
}
