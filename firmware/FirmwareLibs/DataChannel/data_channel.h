#pragma once

#include "secret_store.h"
#include <Arduino.h>

namespace data_channel
{
  struct Segment
  {
    const uint8_t *data;
    size_t len;
  };

  struct SendTiming
  {
    uint32_t encryptUs;
    uint32_t sendUs;
  };

  typedef void (*MessageHandler)(const uint8_t *data, size_t len);

  bool Begin(const DeviceCredentials &creds, uint16_t serverPort);

  bool IsActive();

  void End();

  bool Send(const uint8_t *data, size_t len, SendTiming *timing = nullptr);

  bool SendSegments(const Segment *segments, size_t segmentCount, SendTiming *timing = nullptr);

  void SetMessageHandler(MessageHandler handler);

  bool Receive(uint8_t *out, size_t outCap, size_t &outLen);
}
