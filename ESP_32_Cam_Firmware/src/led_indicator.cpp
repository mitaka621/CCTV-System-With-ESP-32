#include "led_indicator.h"
#include "config.h"
#include <Adafruit_NeoPixel.h>
#include <Arduino.h>

namespace led_indicator
{
  static Adafruit_NeoPixel _pixels(RGB_LED_COUNT, RGB_LED_PIN, NEO_GRB + NEO_KHZ800);
  static State _currentState = OFF;
  static unsigned long _animationStartMs = 0;

  static uint32_t color(uint8_t r, uint8_t g, uint8_t b)
  {
    return _pixels.Color(r, g, b);
  }

  static uint32_t scaleColor(uint32_t base, uint8_t brightness255)
  {
    uint8_t r = (uint8_t)(((base >> 16) & 0xFF) * brightness255 / 255);
    uint8_t g = (uint8_t)(((base >> 8) & 0xFF) * brightness255 / 255);
    uint8_t b = (uint8_t)((base & 0xFF) * brightness255 / 255);
    return color(r, g, b);
  }

  static uint8_t breathing(unsigned long elapsedMs, unsigned long periodMs)
  {
    float t = (float)(elapsedMs % periodMs) / (float)periodMs;
    float wave = (sinf(t * 2.0f * (float)PI) + 1.0f) * 0.5f;
    return (uint8_t)(wave * 200.0f + 30.0f);
  }

  static bool blink(unsigned long elapsedMs, unsigned long periodMs)
  {
    return ((elapsedMs / (periodMs / 2)) % 2) == 0;
  }

  void begin()
  {
    _pixels.begin();
    _pixels.setBrightness(100);
    _pixels.clear();
    _pixels.show();
    _currentState = BOOTING;
    _animationStartMs = millis();
  }

  void setState(State state)
  {
    if (_currentState == state)
    {
      return;
    }
    _currentState = state;
    _animationStartMs = millis();
  }

  State getState()
  {
    return _currentState;
  }

  void tick()
  {
    unsigned long elapsed = millis() - _animationStartMs;
    uint32_t pixelColor = 0;

    switch (_currentState)
    {
    case OFF:
      pixelColor = color(0, 0, 0);
      break;
    case BOOTING:
      pixelColor = scaleColor(color(255, 255, 255), breathing(elapsed, 1500));
      break;
    case WIFI_CONNECTING:
      pixelColor = blink(elapsed, 600) ? color(0, 0, 200) : color(0, 0, 0);
      break;
    case AP_PROVISIONING:
      pixelColor = scaleColor(color(0, 200, 200), breathing(elapsed, 1800));
      break;
    case PROVISION_RECEIVED:
      pixelColor = blink(elapsed, 150) ? color(0, 255, 0) : color(0, 0, 0);
      break;
    case PAIRING:
      pixelColor = scaleColor(color(180, 0, 220), breathing(elapsed, 900));
      break;
    case PAIRED:
      pixelColor = color(0, 220, 0);
      break;
    case STREAMING:
      pixelColor = scaleColor(color(0, 60, 0), breathing(elapsed, 4000));
      break;
    case ERROR_NETWORK:
      pixelColor = blink(elapsed, 1200) ? color(255, 80, 0) : color(0, 0, 0);
      break;
    case ERROR_FATAL:
      pixelColor = blink(elapsed, 300) ? color(255, 0, 0) : color(0, 0, 0);
      break;
    }

    _pixels.setPixelColor(0, pixelColor);
    _pixels.show();
  }
}
