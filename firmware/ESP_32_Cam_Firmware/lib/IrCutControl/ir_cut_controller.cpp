#include "ir_cut_controller.h"
#include "config.h"
#include <Arduino.h>
#include <esp_timer.h>
#include <esp_camera.h>

namespace ir_cut_controller
{
  static bool _isNight = false;
  static bool _stateKnown = false;
  static bool _sensorPresent = false;
  static bool _forceBlackAndWhite = IR_CUT_FORCE_BLACK_AND_WHITE;
  static int _lastValue = 0;

  static unsigned long _lastSampleMs = 0;

  static volatile bool _pulseActive = false;
  static esp_timer_handle_t _pulseTimer = nullptr;

  static void driveIdle()
  {
    digitalWrite(IR_CUT_IN1_PIN, LOW);
    digitalWrite(IR_CUT_IN2_PIN, LOW);
  }

  static void endPulse(void *arg)
  {
    driveIdle();
    _pulseActive = false;
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

  static void startPulse(bool night)
  {
    esp_timer_stop(_pulseTimer);
    driveIdle();

    if (night)
    {
      digitalWrite(IR_CUT_IN2_PIN, HIGH);
    }
    else
    {
      digitalWrite(IR_CUT_IN1_PIN, HIGH);
    }

    _pulseActive = true;
    esp_timer_start_once(_pulseTimer, (uint64_t)IR_CUT_PULSE_MS * 1000ULL);

    if (DEBUG_ON)
    {
      Serial.printf("IR-cut: pulsing MX1508 %s (GPIO%d) for %dms\n",
                    night ? "NIGHT/IN2" : "DAY/IN1",
                    night ? IR_CUT_IN2_PIN : IR_CUT_IN1_PIN,
                    IR_CUT_PULSE_MS);
    }
  }

  void applyColorMode()
  {
    sensor_t *sensor = esp_camera_sensor_get();
    if (sensor == nullptr)
    {
      return;
    }
    bool blackAndWhite = _forceBlackAndWhite || _isNight;
    sensor->set_special_effect(sensor, blackAndWhite ? 2 : 0);
  }

  static void applyState(bool night)
  {
    _isNight = night;
    _stateKnown = true;
    startPulse(night);
    applyColorMode();
  }

  static void relayTestTask(void *param)
  {
    bool night = false;
    for (;;)
    {
      startPulse(night);
      Serial.printf("IR-cut TEST: pulsing %s\n", night ? "NIGHT/IN2" : "DAY/IN1");
      night = !night;
      vTaskDelay(pdMS_TO_TICKS(IR_CUT_TEST_INTERVAL_MS));
    }
  }

  void begin()
  {
    pinMode(IR_CUT_IN1_PIN, OUTPUT);
    pinMode(IR_CUT_IN2_PIN, OUTPUT);
    driveIdle();

    esp_timer_create_args_t timerArgs = {};
    timerArgs.callback = &endPulse;
    timerArgs.name = "ircut_pulse";
    esp_timer_create(&timerArgs, &_pulseTimer);

    if (IR_CUT_TEST_MODE)
    {
      DEBUG_PRINT("IR-cut: TEST MODE - background task pulsing MX1508 directions");
      xTaskCreate(relayTestTask, "relayTest", 3072, NULL, 1, NULL);
      return;
    }

    if (_forceBlackAndWhite)
    {
      DEBUG_PRINT("IR-cut: forced black & white mode - sensor and relays disabled");
      _isNight = true;
      _stateKnown = true;
      applyColorMode();
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
    if (_forceBlackAndWhite || !_sensorPresent)
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
    return _forceBlackAndWhite || _isNight;
  }

  bool isForceBlackAndWhite()
  {
    return _forceBlackAndWhite;
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
