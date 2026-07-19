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

  enum class CameraCommand : uint8_t
  {
    None = 0,
    ResetSecurityAlarm = 1,
    ActivateBuzzerAlarm = 2,
    SaveNewConfig = 3
  };

  typedef void (*CommandHandler)(CameraCommand command, const uint8_t *payload, size_t payloadLen);

  void setCommandHandler(CommandHandler handler);

  bool begin(const DeviceCredentials &creds, uint16_t serverPort);

  bool isActive();

  bool sendFrame(const uint8_t *data, size_t len, uint32_t width, uint32_t height,
                 const uint8_t *telemetry, uint16_t telemetryLen, FrameTiming *timing = nullptr);

  void end();
}
