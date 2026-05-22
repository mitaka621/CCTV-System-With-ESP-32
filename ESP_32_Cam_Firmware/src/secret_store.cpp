#include "secret_store.h"
#include "config.h"
#include "secrets.h"
#include <Preferences.h>

namespace secret_store
{
  static Preferences _prefs;
  static bool _ready = false;

  bool begin()
  {
    if (_ready)
    {
      return true;
    }
    _ready = _prefs.begin(NVS_NAMESPACE, false);
    return _ready;
  }

  bool loadFromCompileTime(DeviceCredentials &out)
  {
#ifdef HAS_PROVISIONED_SECRETS
    out.wifiSsid = WIFI_SSID;
    out.wifiPass = WIFI_PASS;
    out.deviceId = DEVICE_ID;
    out.privateKey = PRIVATE_KEY;
    out.nonce = NONCE;
    out.serverIp = SERVER_IP;
    return out.wifiSsid.length() > 0 &&
           out.deviceId.length() > 0 &&
           out.privateKey.length() > 0 &&
           out.serverIp.length() > 0;
#else
    (void)out;
    return false;
#endif
  }

  bool loadFromNvs(DeviceCredentials &out)
  {
    if (!_ready)
    {
      return false;
    }

    out.wifiSsid = _prefs.getString("wifiSsid", "");
    out.wifiPass = _prefs.getString("wifiPass", "");
    out.deviceId = _prefs.getString("deviceId", "");
    out.privateKey = _prefs.getString("privateKey", "");
    out.nonce = _prefs.getString("nonce", "");
    out.serverIp = _prefs.getString("serverIp", "");

    return out.wifiSsid.length() > 0 &&
           out.deviceId.length() > 0 &&
           out.privateKey.length() > 0 &&
           out.serverIp.length() > 0;
  }

  bool saveToNvs(const DeviceCredentials &creds)
  {
    if (!_ready)
    {
      return false;
    }
    _prefs.putString("wifiSsid", creds.wifiSsid);
    _prefs.putString("wifiPass", creds.wifiPass);
    _prefs.putString("deviceId", creds.deviceId);
    _prefs.putString("privateKey", creds.privateKey);
    _prefs.putString("nonce", creds.nonce);
    _prefs.putString("serverIp", creds.serverIp);
    return true;
  }

  bool isPaired()
  {
    if (!_ready)
    {
      return false;
    }
    return _prefs.getBool("paired", false);
  }

  bool setPaired(bool paired)
  {
    if (!_ready)
    {
      return false;
    }
    return _prefs.putBool("paired", paired) > 0;
  }

  void clearAll()
  {
    if (!_ready)
    {
      return;
    }
    _prefs.clear();
  }
}
