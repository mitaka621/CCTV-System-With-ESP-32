#pragma once

#include "secret_store.h"
#include <Arduino.h>

namespace secure_session
{
  struct FrameTiming
  {
    uint32_t encryptUs;
    uint32_t sendUs;
  };

  bool begin(const DeviceCredentials &creds, uint16_t serverPort);

  bool isActive();

  bool sendFrame(const uint8_t *data, size_t len, FrameTiming *timing = nullptr);

  void end();
}
