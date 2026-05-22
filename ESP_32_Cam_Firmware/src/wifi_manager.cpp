#include "wifi_manager.h"
#include "config.h"
#include <WiFi.h>

namespace wifi_manager
{
  bool connect(const String &ssid, const String &password, unsigned long timeoutMs)
  {
    DEBUG_PRINT("Connecting to WiFi SSID: " + ssid);
    WiFi.mode(WIFI_STA);
    WiFi.setSleep(false);
    WiFi.disconnect(true, false);
    WiFi.begin(ssid.c_str(), password.c_str());

    unsigned long start = millis();
    while (WiFi.status() != WL_CONNECTED)
    {
      if (millis() - start > timeoutMs)
      {
        DEBUG_PRINT("WiFi connect timeout");
        return false;
      }
      delay(250);
    }
    WiFi.setSleep(false);
    DEBUG_PRINT("WiFi connected. IP: " + WiFi.localIP().toString());
    return true;
  }

  bool isConnected()
  {
    return WiFi.status() == WL_CONNECTED;
  }

  String macAddress()
  {
    uint8_t mac[6];
    WiFi.macAddress(mac);
    char buf[13];
    snprintf(buf, sizeof(buf), "%02X%02X%02X%02X%02X%02X",
             mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
    return String(buf);
  }

  String macAddressColons()
  {
    uint8_t mac[6];
    WiFi.macAddress(mac);
    char buf[18];
    snprintf(buf, sizeof(buf), "%02X:%02X:%02X:%02X:%02X:%02X",
             mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
    return String(buf);
  }
}
