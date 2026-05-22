#pragma once

#include <Arduino.h>

namespace wifi_manager
{
  bool connect(const String &ssid, const String &password, unsigned long timeoutMs);

  bool isConnected();

  String macAddress();

  String macAddressColons();
}
