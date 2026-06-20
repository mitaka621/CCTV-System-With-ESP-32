#include "status_led.h"
#include "config.h"
#include <Arduino.h>

namespace status_led
{
  static Mode _mode = OFF;
  static bool _ledOn = false;
  static unsigned long _lastToggleMs = 0;

  static void writeLed(bool on)
  {
    _ledOn = on;
    digitalWrite(STATUS_LED_PIN, on ? HIGH : LOW);
  }

  void begin()
  {
    pinMode(STATUS_LED_PIN, OUTPUT);
    _mode = OFF;
    writeLed(false);
  }

  void setMode(Mode mode)
  {
    if (_mode == mode)
    {
      return;
    }
    _mode = mode;
    _lastToggleMs = millis();
    writeLed(mode == SOLID);
  }

  void tick()
  {
    switch (_mode)
    {
    case OFF:
      if (_ledOn)
      {
        writeLed(false);
      }
      break;
    case SOLID:
      if (!_ledOn)
      {
        writeLed(true);
      }
      break;
    case BLINKING:
      if (millis() - _lastToggleMs >= STATUS_LED_BLINK_INTERVAL_MS)
      {
        _lastToggleMs = millis();
        writeLed(!_ledOn);
      }
      break;
    }
  }
}
