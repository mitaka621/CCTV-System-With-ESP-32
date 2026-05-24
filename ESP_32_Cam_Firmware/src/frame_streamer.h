#pragma once

#include "secret_store.h"
#include <Arduino.h>

namespace frame_streamer
{
  bool beginCamera();

  bool startSession(const DeviceCredentials &creds);

  bool isSessionActive();

  void endSession();

  void tick();
}
