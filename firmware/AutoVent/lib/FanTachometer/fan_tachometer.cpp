#include "fan_tachometer.h"
#include "config.h"

namespace fan_tachometer
{
  static volatile uint32_t _pulseCount = 0;
  static unsigned long _lastSampleAt = 0;
  static uint16_t _rpm = 0;

  static void IRAM_ATTR OnPulse()
  {
    _pulseCount++;
  }

  void Begin()
  {
    pinMode(FAN_TACH_PIN, INPUT_PULLUP);
    attachInterrupt(digitalPinToInterrupt(FAN_TACH_PIN), OnPulse, FALLING);
    _lastSampleAt = millis();
  }

  void Tick()
  {
    const unsigned long now = millis();
    const unsigned long elapsed = now - _lastSampleAt;

    if (elapsed < FAN_TACH_SAMPLE_MS)
    {
      return;
    }

    noInterrupts();
    const uint32_t pulses = _pulseCount;
    _pulseCount = 0;
    interrupts();

    _lastSampleAt = now;
    _rpm = (uint16_t)((pulses * 60000UL) / (FAN_TACH_PULSES_PER_REVOLUTION * elapsed));
  }

  uint16_t Rpm()
  {
    return _rpm;
  }
}
