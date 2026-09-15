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

  // at around 3.70 V is the min cell voltage since then the onboard regulator produces 3V
  const CurvePoint _socCurve[] = {
      {4.20f, 100.0f}, {4.15f, 93.0f}, {4.10f, 86.0f}, {4.05f, 80.0f}, {4.00f, 73.0f}, {3.95f, 66.0f}, {3.90f, 57.0f}, {3.85f, 46.0f}, {3.80f, 35.0f}, {3.78f, 26.0f}, {3.75f, 16.0f}, {3.72f, 8.0f}, {3.65f, 0.0f}};

  const size_t _socPointCount = sizeof(_socCurve) / sizeof(_socCurve[0]);
}

namespace battery_gauge
{
  const char* _chargeSenseNVSKey="ChargeSense";
  static float _chargeSenseThresholdVoltage=CHARGE_SENSE_THRESHOLD_VOLTS;

  const char* GetChargeSenseNVSKey()
  {
    return _chargeSenseNVSKey;
  }

  float GetChargeSenseThreashold()
  {
    return _chargeSenseThresholdVoltage;
  }

  bool SetNewChargeSenseThreashold (float newThreashold)
  {
    if(newThreashold<0 || newThreashold>6)
    {
      return false;
    }

    _chargeSenseThresholdVoltage=newThreashold;

    return true;
  }

  void Begin()
  {
    //enabeling gnd for the battery divider (it is normally floating to save power)
    rtc_gpio_hold_dis((gpio_num_t)BATTERY_DIVIDER_GROUND_PIN);

    rtc_gpio_deinit((gpio_num_t)BATTERY_DIVIDER_GROUND_PIN);

    analogReadResolution(12);
    analogSetPinAttenuation(BATTERY_ADC_PIN, ADC_11db);
    analogSetPinAttenuation(CHARGE_SENSE_PIN, ADC_11db);
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

  float ReadChargeSenseVolts()
  {
    uint32_t sum = 0;
    for (int i = 0; i < CHARGE_SENSE_SAMPLE_COUNT; i++)
    {
      sum += analogReadMilliVolts(CHARGE_SENSE_PIN);
      delayMicroseconds(200);
    }

    const float pinVolts = (sum / (float)CHARGE_SENSE_SAMPLE_COUNT) / 1000.0f;
    return pinVolts * CHARGE_SENSE_DIVIDER_RATIO;
  }

  bool IsCharging()
  {
    return ReadChargeSenseVolts() >= _chargeSenseThresholdVoltage;
  }

  void PrepareForSleep()
  {
    rtc_gpio_isolate((gpio_num_t)BATTERY_DIVIDER_GROUND_PIN);
  }
}
