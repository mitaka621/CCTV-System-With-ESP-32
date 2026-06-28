#include "ir_cut_controller.h"
#include "config.h"
#include <Arduino.h>
#include <esp_timer.h>
#include <esp_camera.h>

namespace ir_cut_controller
{
  static const int RELAY_ACTIVE = IR_CUT_RELAY_ACTIVE_HIGH ? HIGH : LOW;
  static const int RELAY_IDLE = IR_CUT_RELAY_ACTIVE_HIGH ? LOW : HIGH;

  static bool _isNight = false;
  static bool _stateKnown = false;
  static bool _sensorPresent = false;
  static int _lastValue = 0;

  static unsigned long _lastSampleMs = 0;

  static volatile bool _pulseActive = false;
  static volatile int _pulsePin = -1;
  static esp_timer_handle_t _pulseTimer = nullptr;

  static void endPulse(void *arg)
  {
    if (_pulsePin >= 0)
    {
      digitalWrite(_pulsePin, RELAY_IDLE);
    }
    _pulseActive = false;
    _pulsePin = -1;
  }

  static bool detectSensor()
  {
    pinMode(LIGHT_SENSOR_PIN, INPUT_PULLDOWN);
    delay(10);
    int pulledLow = analogRead(LIGHT_SENSOR_PIN);

    pinMode(LIGHT_SENSOR_PIN, INPUT_PULLUP);
    delay(10);
    int pulledHigh = analogRead(LIGHT_SENSOR_PIN);

    pinMode(LIGHT_SENSOR_PIN, INPUT);
    analogSetPinAttenuation(LIGHT_SENSOR_PIN, ADC_11db);

    int delta = pulledHigh - pulledLow;
    if (DEBUG_ON)
    {
      Serial.printf("IR-cut: sensor probe low=%d high=%d delta=%d (threshold=%d)\n",
                    pulledLow, pulledHigh, delta, LIGHT_SENSOR_PRESENCE_DELTA);
    }
    return delta < LIGHT_SENSOR_PRESENCE_DELTA;
  }

  static int readSensor()
  {
    long sum = 0;
    for (int i = 0; i < 8; i++)
    {
      sum += analogRead(LIGHT_SENSOR_PIN);
    }
    return (int)(sum / 8);
  }

  static void startPulse(int pin)
  {
    esp_timer_stop(_pulseTimer);

    if (_pulseActive && _pulsePin != pin && _pulsePin >= 0)
    {
      digitalWrite(_pulsePin, RELAY_IDLE);
    }

    _pulsePin = pin;
    _pulseActive = true;
    digitalWrite(pin, RELAY_ACTIVE);
    esp_timer_start_once(_pulseTimer, (uint64_t)IR_CUT_RELAY_PULSE_MS * 1000ULL);

    if (DEBUG_ON)
    {
      Serial.printf("IR-cut: pulsing relay GPIO%d -> level %d for %dms\n", pin, RELAY_ACTIVE, IR_CUT_RELAY_PULSE_MS);
    }
  }

  void applyColorMode()
  {
    sensor_t *sensor = esp_camera_sensor_get();
    if (sensor == nullptr)
    {
      return;
    }
    sensor->set_special_effect(sensor, _isNight ? 2 : 0);
  }

  static void applyState(bool night)
  {
    _isNight = night;
    _stateKnown = true;
    startPulse(night ? IR_CUT_NIGHT_RELAY_PIN : IR_CUT_DAY_RELAY_PIN);
    applyColorMode();
  }

  static void relayTestTask(void *param)
  {
    bool level = false;
    for (;;)
    {
      int out = level ? HIGH : LOW;
      digitalWrite(IR_CUT_NIGHT_RELAY_PIN, out);
      digitalWrite(IR_CUT_DAY_RELAY_PIN, out);
      Serial.printf("IR-cut TEST: GPIO%d & GPIO%d -> %s\n",
                    IR_CUT_NIGHT_RELAY_PIN, IR_CUT_DAY_RELAY_PIN, out == HIGH ? "HIGH (3.3V)" : "LOW (0V)");
      level = !level;
      vTaskDelay(pdMS_TO_TICKS(IR_CUT_RELAY_TEST_INTERVAL_MS));
    }
  }

  void begin()
  {
    pinMode(IR_CUT_NIGHT_RELAY_PIN, OUTPUT);
    pinMode(IR_CUT_DAY_RELAY_PIN, OUTPUT);
    digitalWrite(IR_CUT_NIGHT_RELAY_PIN, RELAY_IDLE);
    digitalWrite(IR_CUT_DAY_RELAY_PIN, RELAY_IDLE);

    esp_timer_create_args_t timerArgs = {};
    timerArgs.callback = &endPulse;
    timerArgs.name = "ircut_pulse";
    esp_timer_create(&timerArgs, &_pulseTimer);

    if (IR_CUT_RELAY_TEST_MODE)
    {
      DEBUG_PRINT("IR-cut: TEST MODE - background task toggling both relays every 3s");
      xTaskCreate(relayTestTask, "relayTest", 3072, NULL, 1, NULL);
      return;
    }

    _sensorPresent = detectSensor();
    if (!_sensorPresent)
    {
      DEBUG_PRINT("IR-cut: no light sensor detected - relays idle, color mode forced");
      _isNight = false;
      _stateKnown = true;
      applyColorMode();
      return;
    }

    int value = readSensor();
    _lastValue = value;
    bool night = value < LIGHT_SENSOR_NIGHT_THRESHOLD;

    applyState(night);
    _lastSampleMs = millis();
  }

  void tick()
  {
    if (!_sensorPresent)
    {
      return;
    }

    if ((millis() - _lastSampleMs) < LIGHT_SENSOR_SAMPLE_INTERVAL_MS)
    {
      return;
    }
    _lastSampleMs = millis();

    int value = readSensor();
    _lastValue = value;

    bool night = _isNight;
    if (value < LIGHT_SENSOR_NIGHT_THRESHOLD)
    {
      night = true;
    }
    else if (value > LIGHT_SENSOR_DAY_THRESHOLD)
    {
      night = false;
    }

    if (!_stateKnown || night != _isNight)
    {
      applyState(night);
    }
  }

  bool isNight()
  {
    return _isNight;
  }

  bool sensorPresent()
  {
    return _sensorPresent;
  }

  int lastValue()
  {
    return _lastValue;
  }
}
