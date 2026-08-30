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
  String serverIdentityPubKey;
};

namespace secret_store
{
  bool begin();

  bool loadFromCompileTime(DeviceCredentials &out);

  bool loadFromNvs(DeviceCredentials &out);

  bool saveToNvs(const DeviceCredentials &creds);

  bool loadFromNvs(const char* key, String &out);

  bool saveToNvs(const char* key, String &value);

  bool isPaired();

  bool setPaired(bool paired);

  void clearAll();
}
