#include "fan_relay.h"
#include "config.h"

namespace fan_relay
{
  static bool _active = false;

  void SetActive(bool active)
  {
    _active = active;
    digitalWrite(FAN_RELAY_PIN, active ? FAN_RELAY_ACTIVE_LEVEL : FAN_RELAY_IDLE_LEVEL);
  }

  void Begin()
  {
    pinMode(FAN_RELAY_PIN, OUTPUT);
    SetActive(false);
  }

  bool IsActive()
  {
    return _active;
  }
}
