#pragma once

#include "secret_store.h"
#include <Arduino.h>

namespace provision_ap
{
  enum TickResult
  {
    IDLE,
    RECEIVED
  };

  bool begin();

  TickResult tick(DeviceCredentials &credentialsOut);

  bool clientConnected();

  void end();

  String apSsid();
}
