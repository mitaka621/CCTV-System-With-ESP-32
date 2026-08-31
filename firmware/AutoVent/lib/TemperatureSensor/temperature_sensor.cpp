#include "temperature_sensor.h"
#include "config.h"
#include <DallasTemperature.h>
#include <OneWire.h>

namespace temperature_sensor
{
  static OneWire _oneWire(TEMPERATURE_SENSOR_PIN);
  static DallasTemperature _sensors(&_oneWire);
  static float _lastCelsius = NAN;
  static unsigned long _nextActionAt = 0;
  static bool _converting = false;

  static void StartConversion()
  {
    _sensors.requestTemperatures();
    _converting = true;
    _nextActionAt = millis() + TEMPERATURE_CONVERSION_MS;
  }

  void Begin()
  {
    _sensors.begin();
    _sensors.setResolution(TEMPERATURE_RESOLUTION_BITS);
    _sensors.setWaitForConversion(false);
    StartConversion();
  }

  void Tick()
  {
    if ((long)(millis() - _nextActionAt) < 0)
    {
      return;
    }

    if (!_converting)
    {
      StartConversion();
      return;
    }

    const float celsius = _sensors.getTempCByIndex(0);
    _lastCelsius = (celsius == DEVICE_DISCONNECTED_C) ? NAN : celsius;
    _converting = false;
    _nextActionAt = millis() + TEMPERATURE_INTERVAL_MS;
  }

  bool IsValid()
  {
    return !isnan(_lastCelsius);
  }

  float LastCelsius()
  {
    return _lastCelsius;
  }
}
