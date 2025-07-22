#include <Preferences.h>
#include <mbedtls/ctr_drbg.h>
#include <mbedtls/entropy.h>
#include <WebServer.h>
#include <Arduino.h>
#include "mbedtls/md.h"
#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <esp_camera.h>
#include "camera_pins.h"
#include "secrets.h"
#include <ArduinoJson.h>
#include <nvs_flash.h>

#define CAMERA_MODEL_AI_THINKER

#define DEBUG_ON true

#define DEBUG_PRINT(x)   \
  do                     \
  {                      \
    if (DEBUG_ON)        \
      Serial.println(x); \
  } while (0)

bool initCamera();
bool connectWiFi();
bool sendFrameToServer(camera_fb_t *fb, WiFiClient &client);
void retrieveSessionToken();

Preferences prefs;
WiFiClient client;
WiFiClientSecure httpsClient;
WebServer server(77);

void parseHostAndPath(String &host, String &path, String baseServerUrl, String url)
{
  int idx = baseServerUrl.indexOf("://");
  if (idx >= 0)
  {
    host = baseServerUrl.substring(idx + 3);
  }
  else
  {
    host = baseServerUrl;
  }

  int portIdx = host.indexOf(':');
  if (portIdx >= 0)
  {
    host = host.substring(0, portIdx);
  }

  int hostPos = url.indexOf(host);
  if (hostPos >= 0)
  {
    path = url.substring(hostPos + host.length() + String(ServerHttpsPort).length() + 1);
    if (path == "")
      path = "/";
  }
  else
  {
    path = "/";
  }
}

JsonDocument getHttpsClientBodyAsJson(WiFiClientSecure &httpsClient)
{
  int headerLineCount = 0;
  while (httpsClient.connected())
  {
    String line = httpsClient.readStringUntil('\n');
    line.trim();
    if (line.length() == 0 && headerLineCount != 0)
    {
      break;
    }
    if (line.length() != 0)
    {
      headerLineCount++;
    }
  }

  String body = "";
  while (httpsClient.available())
  {
    body += (char)httpsClient.read();
  }

  DEBUG_PRINT("=== Body ===");
  DEBUG_PRINT(body);

  int firstLineEnd = body.indexOf('{');
  int lastLineStart = body.lastIndexOf('}');

  String cleanJson = body.substring(firstLineEnd, lastLineStart + 1);
  cleanJson.trim();

  Serial.println("=== Clean JSON ===");
  Serial.println(cleanJson);

  JsonDocument doc;

  DeserializationError err = deserializeJson(doc, cleanJson);

  if (err)
  {
    DEBUG_PRINT("JSON parse error");
    return JsonDocument();
  }

  return doc;
}

String computeHmacSha256(const String &message)
{
  unsigned char hmacResult[32];

  mbedtls_md_context_t ctx;
  const mbedtls_md_info_t *info = mbedtls_md_info_from_type(MBEDTLS_MD_SHA256);

  mbedtls_md_init(&ctx);
  mbedtls_md_setup(&ctx, info, 1);

  const String &key = SharedSecretCamKey;

  mbedtls_md_hmac_starts(&ctx, (const unsigned char *)key.c_str(), key.length());
  mbedtls_md_hmac_update(&ctx, (const unsigned char *)message.c_str(), message.length());
  mbedtls_md_hmac_finish(&ctx, hmacResult);

  mbedtls_md_free(&ctx);

  String hexString = "";
  for (int i = 0; i < 32; i++)
  {
    if (hmacResult[i] < 16)
      hexString += "0";
    hexString += String(hmacResult[i], HEX);
  }

  hexString.toUpperCase();
  return hexString;
}

String generateChallengeString()
{
  uint8_t bytes[16];
  mbedtls_entropy_context entropy;
  mbedtls_ctr_drbg_context ctr_drbg;
  mbedtls_entropy_init(&entropy);
  mbedtls_ctr_drbg_init(&ctr_drbg);
  const char *pers = "gen_challenge";
  mbedtls_ctr_drbg_seed(&ctr_drbg, mbedtls_entropy_func, &entropy, (const unsigned char *)pers, strlen(pers));
  mbedtls_ctr_drbg_random(&ctr_drbg, bytes, sizeof(bytes));
  String hexString = "";
  char buf[3];
  for (int i = 0; i < 16; i++)
  {
    sprintf(buf, "%02X", bytes[i]);
    hexString += buf;
  }
  mbedtls_ctr_drbg_free(&ctr_drbg);
  mbedtls_entropy_free(&entropy);
  return hexString;
}

bool fixedTimeEquals(const uint8_t *a, const uint8_t *b, size_t len)
{
  uint8_t result = 0;
  for (size_t i = 0; i < len; i++)
    result |= a[i] ^ b[i];
  return result == 0;
}

bool validateDeviceResponse(const String &challenge, const String &deviceResponse)
{
  String expectedHex = computeHmacSha256(challenge);
  if (expectedHex.length() != deviceResponse.length() || expectedHex.length() != 64)
    return false;
  uint8_t expectedBytes[32];
  uint8_t responseBytes[32];
  for (int i = 0; i < 32; i++)
  {
    expectedBytes[i] = (uint8_t)strtol(expectedHex.substring(i * 2, i * 2 + 2).c_str(), nullptr, 16);
    responseBytes[i] = (uint8_t)strtol(deviceResponse.substring(i * 2, i * 2 + 2).c_str(), nullptr, 16);
  }
  return fixedTimeEquals(expectedBytes, responseBytes, 32);
}

String getMacAddressString()
{
  char macStr[18];
  uint8_t mac[6];
  WiFi.macAddress(mac);
  snprintf(macStr, sizeof(macStr), "%02X:%02X:%02X:%02X:%02X:%02X", mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
  return String(macStr);
}

void setBaseServerUrl(const String &url)
{
  prefs.putString("baseServerUrl", url);
}

String getBaseServerUrl()
{
  String url = prefs.getString("baseServerUrl", "");
  return url;
}

void setSessionToken(const String &token)
{
  prefs.putString("sessionToken", token);
}

String getSessionToken()
{
  String token = prefs.getString("sessionToken", "");
  return token;
}

void handleChallenge()
{
  if (getSessionToken() != "")
  {
    DEBUG_PRINT("Already Paired with server, skipping challenge.");
    server.send(400, "text/plain", "Already Paired with server");
    return;
  }

  DEBUG_PRINT("New challenge request accepted");

  if (!server.hasArg("challenge"))
  {
    server.send(400, "text/plain", "Missing 'challenge' parameter");
    return;
  }

  String challenge = server.arg("challenge");

  DEBUG_PRINT(challenge);

  String baseServerUrl = String("https://") + server.client().remoteIP().toString() + ":" + String(ServerHttpsPort);
  setBaseServerUrl(baseServerUrl);

  String hmac = computeHmacSha256(challenge);

  String macAddress = getMacAddressString();
  JsonDocument doc;
  doc["Hmac"] = hmac;
  doc["MacAddress"] = macAddress;
  String response;
  serializeJson(doc, response);
  server.send(200, "application/json", response);

  retrieveSessionToken();
}

bool validateServer()
{
  String baseServerUrl = getBaseServerUrl();
  if (baseServerUrl == "")
  {
    DEBUG_PRINT("No baseServerUrl found in preferences. Cannot validate server.");
    return false;
  }

  String challenge = generateChallengeString();

  String macAddress = getMacAddressString();

  String url = baseServerUrl + "/api/deviceauthenticator/challenge?espCameraChallenge=" + challenge + "&mac=" + macAddress;
  DEBUG_PRINT("Request URL: " + url);

  String host = "";
  String path = "";

  parseHostAndPath(host, path, baseServerUrl, url);

  DEBUG_PRINT("Connecting to host: " + host);
  if (!httpsClient.connect(host.c_str(), ServerHttpsPort))
  {
    DEBUG_PRINT("HTTPS connection to host " + host + " failed");
    return false;
  }

  DEBUG_PRINT("Path: " + path);
  httpsClient.println("GET " + path + " HTTP/1.1");
  httpsClient.println("Host: " + host);
  httpsClient.println("Connection: Close");
  httpsClient.println();

  JsonDocument doc = getHttpsClientBodyAsJson(httpsClient);

  httpsClient.stop();

  String serverHmac = doc["hmac"] | "";
  DEBUG_PRINT("Server HMAC: " + serverHmac);

  bool result = validateDeviceResponse(challenge, serverHmac);

  if (result)
  {
    DEBUG_PRINT("Server solved the challenge.");
  }
  else
  {
    DEBUG_PRINT("Server failed to solve the challenge.");
  }

  return result;
}

void retrieveSessionToken()
{
  if (getSessionToken() != "" || !validateServer())
  {
    return;
  }

  DEBUG_PRINT("Server validated successfully.");

  String baseServerUrl = getBaseServerUrl();
  if (baseServerUrl == "")
  {
    DEBUG_PRINT("No baseServerUrl found in preferences. Cannot retrieve session token.");
    return;
  }
  String macAddress = getMacAddressString();

  // Build URL
  String url = baseServerUrl + "/api/deviceauthenticator/serverSession?mac=" + macAddress;
  DEBUG_PRINT("Requesting session token: " + url);

  // Parse host and path
  String host = "";
  String path = "";

  parseHostAndPath(host, path, baseServerUrl, url);

  DEBUG_PRINT("Connecting to host: " + host);
  if (!httpsClient.connect(host.c_str(), ServerHttpsPort))
  {
    DEBUG_PRINT("HTTPS connection to host " + host + " failed");
    return;
  }

  DEBUG_PRINT("Path: " + path);
  httpsClient.println("GET " + path + " HTTP/1.1");
  httpsClient.println("Host: " + host);
  httpsClient.println("Connection: Close");
  httpsClient.println();

  JsonDocument doc = getHttpsClientBodyAsJson(httpsClient);

  httpsClient.stop();

  String sessionToken = doc["sessionToken"] | "";
  if (sessionToken == "")
  {
    DEBUG_PRINT("No sessionToken in response");
    return;
  }
  DEBUG_PRINT("Session token received: " + sessionToken);

  setSessionToken(sessionToken);
  DEBUG_PRINT("Session token saved to storage.");
}

bool connectWiFi()
{
  DEBUG_PRINT("Connecting to WiFi...");
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASS);
  unsigned long start = millis();
  while (WiFi.status() != WL_CONNECTED)
  {
    delay(500);
    if (millis() - start > 15000)
      return false;
  }
  DEBUG_PRINT("WiFi connected. IP: " + WiFi.localIP().toString());
  return true;
}

bool initCamera()
{
  camera_config_t config;
  config.ledc_channel = LEDC_CHANNEL_0;
  config.ledc_timer = LEDC_TIMER_0;
  config.pin_d0 = Y2_GPIO_NUM;
  config.pin_d1 = Y3_GPIO_NUM;
  config.pin_d2 = Y4_GPIO_NUM;
  config.pin_d3 = Y5_GPIO_NUM;
  config.pin_d4 = Y6_GPIO_NUM;
  config.pin_d5 = Y7_GPIO_NUM;
  config.pin_d6 = Y8_GPIO_NUM;
  config.pin_d7 = Y9_GPIO_NUM;
  config.pin_xclk = XCLK_GPIO_NUM;
  config.pin_pclk = PCLK_GPIO_NUM;
  config.pin_vsync = VSYNC_GPIO_NUM;
  config.pin_href = HREF_GPIO_NUM;
  config.pin_sccb_sda = SIOD_GPIO_NUM;
  config.pin_sccb_scl = SIOC_GPIO_NUM;
  config.pin_pwdn = PWDN_GPIO_NUM;
  config.pin_reset = RESET_GPIO_NUM;
  config.xclk_freq_hz = 20000000;
  config.pixel_format = PIXFORMAT_JPEG;
  config.frame_size = FRAMESIZE_HD;
  config.jpeg_quality = 12;
  config.fb_count = 1;
  DEBUG_PRINT("Initializing camera...");
  esp_err_t err = esp_camera_init(&config);
  if (err != ESP_OK)
  {
    DEBUG_PRINT("Camera init failed with error " + String(err));
    return false;
  }
  return true;
}

bool sendFrameToServer(camera_fb_t *fb, WiFiClient &client)
{
  uint32_t len = fb->len;
  uint8_t sizeBuf[4] =
      {
          (uint8_t)(len >> 24),
          (uint8_t)(len >> 16),
          (uint8_t)(len >> 8),
          (uint8_t)(len)};
  if (client.write(sizeBuf, 4) != 4)
    return false;
  if (client.write(fb->buf, fb->len) != fb->len)
    return false;
  return true;
}

void setup()
{
  Serial.begin(115200);
  delay(1000);
  DEBUG_PRINT("Booting...");
  if (!prefs.begin("settings"))
  {
    DEBUG_PRINT("Preferences begin failed!");
  }
  if (!connectWiFi())
  {
    DEBUG_PRINT("WiFi connection failed. Restarting...");
    delay(5000);
    ESP.restart();
  }
  if (!initCamera())
  {
    DEBUG_PRINT("Camera init failed. Restarting...");
    delay(5000);
    ESP.restart();
  }
  server.on("/challenge", HTTP_GET, handleChallenge);
  server.begin();
  DEBUG_PRINT("HTTP server started.");

  httpsClient.setCACert(ROOT_CA_CERT);

  retrieveSessionToken();

  DEBUG_PRINT("Setup complete.");
}

void loop()
{
  server.handleClient();
  // if (!client.connected())
  // {
  //   DEBUG_PRINT("Connecting to TCP server...");
  //   if (!client.connect(TCP_SERVER_IP, TCP_SERVER_PORT))
  //   {
  //     DEBUG_PRINT("TCP connection failed.");
  //     delay(2000);
  //     return;
  //   }
  //   DEBUG_PRINT("TCP connected.");
  // }
  // camera_fb_t *fb = esp_camera_fb_get();
  // if (!fb)
  // {
  //   DEBUG_PRINT("Camera capture failed");
  //   delay(100);
  //   return;
  // }
  // if (!sendFrameToServer(fb, client))
  // {
  //   DEBUG_PRINT("Failed to send frame");
  //   client.stop();
  // }
  // else
  // {
  //   DEBUG_PRINT("Frame sent");
  // }
  // esp_camera_fb_return(fb);
  delay(100);
}
