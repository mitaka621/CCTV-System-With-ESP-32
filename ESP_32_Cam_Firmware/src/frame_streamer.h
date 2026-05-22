#pragma once

#include <Arduino.h>

namespace frame_streamer
{
  bool beginCamera();

  void setServerIp(const String &serverIp);

  void setSessionKey(const uint8_t *key, size_t keyLen);

  bool hasSessionKey();

  void tick();
}
