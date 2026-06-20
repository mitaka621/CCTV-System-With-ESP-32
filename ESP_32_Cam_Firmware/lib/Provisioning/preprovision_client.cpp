#include "preprovision_client.h"
#include "config.h"
#include "crypto_signer.h"
#include "secrets.h"
#include <ArduinoJson.h>
#include <WiFiClientSecure.h>

namespace preprovision_client
{
  static Result sendVerifyRequest(const String &host, const String &body, int &statusOut)
  {
    WiFiClientSecure https;
    https.setCACert(ROOT_CA_CERT);
    https.setTimeout(15000);

    DEBUG_PRINT("Connecting HTTPS to " + host + ":" + String(ServerHttpsPort));

    if (!https.connect(host.c_str(), ServerHttpsPort))
    {
      DEBUG_PRINT("HTTPS connect failed");
      return NETWORK_ERROR;
    }

    String path = "/api/Preprovision";
    https.print(String("POST ") + path + " HTTP/1.1\r\n");
    https.print(String("Host: ") + host + "\r\n");
    https.print("Content-Type: application/json\r\n");
    https.print("Content-Length: " + String(body.length()) + "\r\n");
    https.print("Connection: close\r\n");
    https.print("\r\n");
    https.print(body);

    String statusLine = https.readStringUntil('\n');
    statusLine.trim();
    DEBUG_PRINT("Server status: " + statusLine);

    int firstSpace = statusLine.indexOf(' ');
    int secondSpace = statusLine.indexOf(' ', firstSpace + 1);
    if (firstSpace < 0 || secondSpace < 0)
    {
      https.stop();
      return SERVER_ERROR;
    }
    statusOut = statusLine.substring(firstSpace + 1, secondSpace).toInt();

    while (https.connected())
    {
      String line = https.readStringUntil('\n');
      if (line == "\r" || line.length() == 0)
      {
        break;
      }
    }

    https.stop();

    if (statusOut == 200)
    {
      return SUCCESS;
    }
    if (statusOut == 400 || statusOut == 401)
    {
      return SIGNATURE_REJECTED;
    }
    return SERVER_ERROR;
  }

  Result verify(const DeviceCredentials &creds)
  {
    String fingerprintHex;
    if (!crypto_signer::computeFingerprintHex(creds.privateKey, fingerprintHex))
    {
      DEBUG_PRINT("Failed to compute fingerprint");
      return LOCAL_ERROR;
    }
    DEBUG_PRINT("Fingerprint: " + fingerprintHex);

    uint8_t hash[32];
    if (!crypto_signer::computeBindingHash(creds.deviceId, fingerprintHex, creds.nonce, hash))
    {
      DEBUG_PRINT("Failed to compute binding hash");
      return LOCAL_ERROR;
    }

    String signatureBase64;
    if (!crypto_signer::signHash(creds.privateKey, hash, signatureBase64))
    {
      DEBUG_PRINT("Failed to sign hash");
      return LOCAL_ERROR;
    }
    DEBUG_PRINT("Signature: " + signatureBase64);

    JsonDocument doc;
    doc["DeviceId"] = creds.deviceId;
    doc["DeviceSignature"] = signatureBase64;
    String body;
    serializeJson(doc, body);

    int status = 0;
    Result result = sendVerifyRequest(creds.serverIp, body, status);
    DEBUG_PRINT("Preprovision verify result: " + String((int)result) + " (HTTP " + String(status) + ")");
    return result;
  }
}
