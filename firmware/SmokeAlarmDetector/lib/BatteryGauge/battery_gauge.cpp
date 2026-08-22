#include "battery_gauge.h"
#include "config.h"
#include "driver/rtc_io.h"

namespace
{
  struct CurvePoint
  {
    float volts;
    float percent;
  };

  const CurvePoint _socCurve[] = {
      {4.20f, 100.0f}, {4.15f, 95.0f}, {4.10f, 90.0f}, {4.05f, 85.0f}, {4.00f, 80.0f}, {3.95f, 75.0f}, {3.90f, 68.0f}, {3.85f, 60.0f}, {3.80f, 52.0f}, {3.78f, 45.0f}, {3.75f, 38.0f}, {3.72f, 32.0f}, {3.70f, 26.0f}, {3.65f, 20.0f}, {3.60f, 15.0f}, {3.55f, 10.0f}, {3.50f, 7.0f}, {3.45f, 5.0f}, {3.40f, 3.0f}, {3.30f, 1.0f}, {3.20f, 0.0f}};

  const size_t _socPointCount = sizeof(_socCurve) / sizeof(_socCurve[0]);
}

namespace battery_gauge
{
  void Begin()
  {
    // rtc_gpio_isolate leaves the pad held, and hold survives deep sleep. Without
    // releasing it here the ground leg stays disconnected on every wake after the
    // first, the divider floats to the battery, and the reading saturates.
    rtc_gpio_hold_dis((gpio_num_t)BATTERY_DIVIDER_GROUND_PIN);
    rtc_gpio_deinit((gpio_num_t)BATTERY_DIVIDER_GROUND_PIN);

    analogReadResolution(12);
    analogSetPinAttenuation(BATTERY_ADC_PIN, ADC_11db);
    pinMode(CHARGE_SENSE_PIN, INPUT);
    pinMode(BATTERY_DIVIDER_GROUND_PIN, INPUT);
  }

  float ReadVolts()
  {
    pinMode(BATTERY_DIVIDER_GROUND_PIN, OUTPUT);
    digitalWrite(BATTERY_DIVIDER_GROUND_PIN, LOW);
    delay(BATTERY_SETTLE_MS);

    uint32_t sum = 0;
    for (int i = 0; i < BATTERY_SAMPLE_COUNT; i++)
    {
      sum += analogReadMilliVolts(BATTERY_ADC_PIN);
      delayMicroseconds(200);
    }

    pinMode(BATTERY_DIVIDER_GROUND_PIN, INPUT);

    const float pinVolts = (sum / (float)BATTERY_SAMPLE_COUNT) / 1000.0f;
    return pinVolts * BATTERY_DIVIDER_RATIO * BATTERY_CALIBRATION_FACTOR;
  }

  float VoltsToPercent(float volts)
  {
    if (volts >= _socCurve[0].volts)
      return 100.0f;
    if (volts <= _socCurve[_socPointCount - 1].volts)
      return 0.0f;

    for (size_t i = 1; i < _socPointCount; i++)
    {
      if (volts >= _socCurve[i].volts)
      {
        const CurvePoint &high = _socCurve[i - 1];
        const CurvePoint &low = _socCurve[i];
        const float position = (volts - low.volts) / (high.volts - low.volts);
        return low.percent + position * (high.percent - low.percent);
      }
    }
    return 0.0f;
  }

  bool IsCharging()
  {
    return digitalRead(CHARGE_SENSE_PIN) == HIGH;
  }

  void PrepareForSleep()
  {
    rtc_gpio_isolate((gpio_num_t)BATTERY_DIVIDER_GROUND_PIN);
  }
}
