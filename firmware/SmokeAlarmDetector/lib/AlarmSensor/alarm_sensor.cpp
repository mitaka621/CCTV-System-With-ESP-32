#include "alarm_sensor.h"
#include "config.h"

namespace alarm_sensor
{
  void Begin()
  {
    pinMode(ALARM_PIN, INPUT_PULLUP);
  }

  bool IsBeeping()
  {
    return digitalRead(ALARM_PIN) == LOW;
  }

  uint32_t CountBeeps(uint32_t windowMs)
  {
    const uint32_t start = millis();
    uint32_t beeps = 0;
    uint32_t lastLowAt = 0;
    bool insideBeep = false;

    while ((millis() - start) < windowMs)
    {
      const uint32_t now = millis();

      if (digitalRead(ALARM_PIN) == LOW)
      {
        lastLowAt = now;
        if (!insideBeep)
        {
          insideBeep = true;
          beeps++;
        }
      }
      else if (insideBeep && (now - lastLowAt) >= ALARM_BEEP_RELEASE_MS)
      {
        insideBeep = false;
      }

      delayMicroseconds(50);
    }

    return beeps;
  }

  AlarmVerdict Classify(uint32_t beepCount)
  {
    if (beepCount == 0)
      return AlarmVerdict::Silent;
    if (beepCount >= ALARM_MIN_BEEPS_FOR_FIRE)
      return AlarmVerdict::FireAlarm;
    return AlarmVerdict::LowBatteryChirp;
  }
}
