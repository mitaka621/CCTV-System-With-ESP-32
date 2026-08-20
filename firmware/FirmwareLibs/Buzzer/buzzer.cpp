#include "buzzer.h"
#include "config.h"
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>

namespace buzzer
{
  static volatile bool _active = false;

  static void buzzerTask(void *)
  {
    bool level = false;
    for (;;)
    {
      if (_active)
      {
        level = !level;
        digitalWrite(BUZZER_PIN, level ? HIGH : LOW);
        vTaskDelay(pdMS_TO_TICKS(level ? BUZZER_ON_MS : BUZZER_OFF_MS));
      }
      else
      {
        if (level)
        {
          level = false;
          digitalWrite(BUZZER_PIN, LOW);
        }
        vTaskDelay(pdMS_TO_TICKS(50));
      }
    }
  }

  void begin()
  {
    pinMode(BUZZER_PIN, OUTPUT);
    digitalWrite(BUZZER_PIN, LOW);
    xTaskCreate(buzzerTask, "buzzer", 2048, nullptr, 1, nullptr);
  }

  void setActive(bool active)
  {
    _active = active;
  }

  bool isActive()
  {
    return _active;
  }
}
