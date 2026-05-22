#pragma once

#include <Arduino.h>

struct DeviceCredentials
{
  String wifiSsid;
  String wifiPass;
  String deviceId;
  String privateKey;
  String nonce;
  String serverIp;
};

namespace secret_store
{
  bool begin();

  bool loadFromCompileTime(DeviceCredentials &out);

  bool loadFromNvs(DeviceCredentials &out);

  bool saveToNvs(const DeviceCredentials &creds);

  bool isPaired();

  bool setPaired(bool paired);

  void clearAll();
}
