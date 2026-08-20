#pragma once

namespace led_indicator
{
  enum State
  {
    OFF,
    BOOTING,
    WIFI_CONNECTING,
    AP_PROVISIONING,
    PROVISION_RECEIVED,
    PAIRING,
    PAIRED,
    STREAMING,
    ERROR_NETWORK,
    ERROR_FATAL
  };

  void begin();
  void setState(State state);
  State getState();
  void tick();
}
