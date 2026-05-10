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
#include "mbedtls/gcm.h"
#include "base64.h"
#include "esp_system.h"
#include "mbedtls/base64.h"

#define RESET_BUTTON_PIN 14

#define MAX_FAILED_ATTEMPTS_TO_CONNECT_TO_SERVER 50
#define FAILED_ATTEMPTS_TO_PAIR_WITH_SERVER_LIMIT 20
#define FAILED_FRAMES_TO_SEND_MARK_AS_NOT_PAIRED_LIMIT 10

#define ENCRYPTION_BUFFER_SIZE (1024 * 1024)

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
bool getSessionTokenFromServer(String baseServerUrl, String macAddress);

Preferences prefs;
WiFiClient client;
WiFiClientSecure httpsClient;
WebServer server(77);

bool doesHaveToReloadSessionToken = true;
bool isPaired = false;

uint8_t aesKey[32];
size_t aesKeyLen = 0;

uint8_t *encryptionBuffer = nullptr;

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

void setServerLocalIpAddress(const String &address)
{
  prefs.putString("serverAddress", address);
}

String getServerLocalIpAddress()
{
  String address = prefs.getString("serverAddress", "");
  return address;
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
  DEBUG_PRINT("New challenge request accepted");

  if (!server.hasArg("challenge"))
  {
    server.send(400, "text/plain", "Missing 'challenge' parameter");
    return;
  }

  String challenge = server.arg("challenge");

  DEBUG_PRINT(challenge);

  String serverAddress = server.client().remoteIP().toString();

  String baseServerUrl = String("https://") + serverAddress + ":" + String(ServerHttpsPort);

  setServerLocalIpAddress(serverAddress);
  setBaseServerUrl(baseServerUrl);

  String hmac = computeHmacSha256(challenge);

  String macAddress = getMacAddressString();
  JsonDocument doc;
  doc["Hmac"] = hmac;
  doc["MacAddress"] = macAddress;
  String response;
  serializeJson(doc, response);
  server.send(200, "application/json", response);

  isPaired = getSessionTokenFromServer(baseServerUrl, macAddress);
}

bool isServerValid()
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

bool getSessionTokenFromServer(String baseServerUrl, String macAddress)
{
  if (!isServerValid())
  {
    return false;
  }

  DEBUG_PRINT("Server validated successfully.");

  if (baseServerUrl == "")
  {
    DEBUG_PRINT("No baseServerUrl found in preferences. Cannot retrieve session token.");
    return false;
  }

  String url = baseServerUrl + "/api/deviceauthenticator/serverSession?mac=" + macAddress;
  DEBUG_PRINT("Requesting session token: " + url);

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

  String sessionToken = doc["sessionToken"] | "";
  if (sessionToken == "")
  {
    DEBUG_PRINT("No sessionToken in response");
    return false;
  }
  DEBUG_PRINT("Session token received: " + sessionToken);

  setSessionToken(sessionToken);
  DEBUG_PRINT("Session token saved to storage.");

  return true;
}

bool connectWiFi()
{
  DEBUG_PRINT("Connecting to WiFi...");
  WiFi.mode(WIFI_STA);
  WiFi.setSleep(false);
  WiFi.begin(WIFI_SSID, WIFI_PASS);
  unsigned long start = millis();
  while (WiFi.status() != WL_CONNECTED)
  {
    delay(500);
    if (millis() - start > 15000)
      return false;
  }
  WiFi.setSleep(false);
  DEBUG_PRINT("WiFi connected. IP: " + WiFi.localIP().toString());
  return true;
}

bool initCamera()
{
  if (!psramFound())
  {
    DEBUG_PRINT("PSRAM not detected. Full HD streaming requires PSRAM.");
    return false;
  }

  DEBUG_PRINT("PSRAM size (bytes): " + String((uint32_t)ESP.getPsramSize()));

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
  config.frame_size = FRAMESIZE_FHD;
  config.jpeg_quality = 12;
  config.fb_count = 3;
  config.fb_location = CAMERA_FB_IN_PSRAM;
  config.grab_mode = CAMERA_GRAB_LATEST;

  DEBUG_PRINT("Initializing camera...");
  esp_err_t err = esp_camera_init(&config);
  if (err != ESP_OK)
  {
    DEBUG_PRINT("Camera init failed with error " + String(err));
    return false;
  }

  sensor_t *sensor = esp_camera_sensor_get();
  if (sensor != nullptr)
  {
    sensor->set_framesize(sensor, FRAMESIZE_FHD);
    sensor->set_quality(sensor, 12);
    sensor->set_brightness(sensor, 0);
    sensor->set_contrast(sensor, 0);
    sensor->set_saturation(sensor, 0);
    sensor->set_whitebal(sensor, 1);
    sensor->set_awb_gain(sensor, 1);
    sensor->set_wb_mode(sensor, 0);
    sensor->set_exposure_ctrl(sensor, 1);
    sensor->set_aec2(sensor, 1);
    sensor->set_ae_level(sensor, 0);
    sensor->set_gain_ctrl(sensor, 1);
    sensor->set_gainceiling(sensor, (gainceiling_t)0);
    sensor->set_bpc(sensor, 1);
    sensor->set_wpc(sensor, 1);
    sensor->set_raw_gma(sensor, 1);
    sensor->set_lenc(sensor, 1);
    sensor->set_hmirror(sensor, 0);
    sensor->set_vflip(sensor, 0);
    sensor->set_dcw(sensor, 1);
    sensor->set_colorbar(sensor, 0);
  }

  return true;
}

bool encryptAESGCM(uint8_t *input, size_t inputLen,
                   uint8_t *output, uint8_t *iv, uint8_t *tag,
                   const uint8_t *key)
{
  mbedtls_gcm_context gcm;
  mbedtls_gcm_init(&gcm);
  mbedtls_gcm_setkey(&gcm, MBEDTLS_CIPHER_ID_AES, key, 256);

  int ret = mbedtls_gcm_crypt_and_tag(&gcm, MBEDTLS_GCM_ENCRYPT,
                                      inputLen,
                                      iv, 12,  // IV
                                      NULL, 0, // no additional data (AAD)
                                      input, output,
                                      16, tag); // 16-byte tag
  mbedtls_gcm_free(&gcm);
  return (ret == 0);
}

bool getAesKeyFromPrefs(uint8_t *keyBuf, size_t keyBufSize, size_t &keyLen)
{
  String tokenBase64 = getSessionToken();

  if (tokenBase64.length() == 0)
    return false;

  size_t olen = 0;
  int ret = mbedtls_base64_decode(
      keyBuf, keyBufSize, &olen,
      (const unsigned char *)tokenBase64.c_str(),
      tokenBase64.length());

  if (ret != 0)
  {
    DEBUG_PRINT("Base64 decode failed");
    return false;
  }

  keyLen = olen;
  return true;
}

bool sendFrameToServer(camera_fb_t *fb, WiFiClient &client)
{
  if (encryptionBuffer == nullptr)
    return false;

  if (fb->len > ENCRYPTION_BUFFER_SIZE)
  {
    DEBUG_PRINT("Frame too large for encryption buffer: " + String(fb->len));
    return false;
  }

  if (doesHaveToReloadSessionToken)
  {
    if (!getAesKeyFromPrefs(aesKey, sizeof(aesKey), aesKeyLen))
    {
      return false;
    }
    doesHaveToReloadSessionToken = false;
  }

  uint8_t mac[6];
  WiFi.macAddress(mac);

  uint8_t iv[12];
  esp_fill_random(iv, sizeof(iv));

  uint8_t tag[16];

  if (!encryptAESGCM(fb->buf, fb->len, encryptionBuffer, iv, tag, aesKey))
  {
    return false;
  }

  uint32_t totalLen = 6 + sizeof(iv) + sizeof(tag) + fb->len;
  uint8_t header[4 + sizeof(mac) + sizeof(iv) + sizeof(tag)];
  header[0] = (uint8_t)(totalLen >> 24);
  header[1] = (uint8_t)(totalLen >> 16);
  header[2] = (uint8_t)(totalLen >> 8);
  header[3] = (uint8_t)(totalLen);
  memcpy(header + 4, mac, sizeof(mac));
  memcpy(header + 4 + sizeof(mac), iv, sizeof(iv));
  memcpy(header + 4 + sizeof(mac) + sizeof(iv), tag, sizeof(tag));

  if (client.write(header, sizeof(header)) != sizeof(header))
    return false;

  if (client.write(encryptionBuffer, fb->len) != fb->len)
    return false;

  return true;
}

// removes all server related data from persistent storage
void forgetServer()
{
  // Clear all server related data from persistent storage
  prefs.clear();

  doesHaveToReloadSessionToken = true;
}

unsigned long resetButtonPressStart = 0;
bool resetButtonWasPressed = false;

void handleResetButtonPress()
{
  int buttonState = digitalRead(RESET_BUTTON_PIN);

  if (buttonState == LOW && !resetButtonWasPressed)
  {
    resetButtonWasPressed = true;
    resetButtonPressStart = millis();
  }
  else if (buttonState == HIGH && resetButtonWasPressed)
  {
    resetButtonWasPressed = false;
  }
  else if (buttonState == LOW && resetButtonWasPressed)
  {
    if (millis() - resetButtonPressStart >= 5000)
    {
      DEBUG_PRINT("Camera reset initiated");
      resetButtonWasPressed = false;

      forgetServer();
      ESP.restart();
    }
  }
}

int failedWiFiReconnectCounter = 0;

void wifiConnectivityCheck()
{
  if (WiFi.status() != WL_CONNECTED)
  {
    DEBUG_PRINT("WiFi disconnected. Attempting to reconnect...");
    while (!connectWiFi())
    {
      failedWiFiReconnectCounter++;
      DEBUG_PRINT("WiFi reconnection failed. Will retry...");
      if (failedWiFiReconnectCounter >= 10)
      {
        DEBUG_PRINT("WiFi reconnection failed too many times. Restarting ESP...");
        delay(1000);
        ESP.restart();
        return;
      }
      delay(2000);
    }
    DEBUG_PRINT("WiFi reconnected.");
    failedWiFiReconnectCounter = 0;
  }
}

void setup()
{
  pinMode(RESET_BUTTON_PIN, INPUT_PULLUP);

  Serial.begin(115200);
  unsigned long serialReadyStart = millis();
  while (!Serial && (millis() - serialReadyStart) < 3000)
  {
    delay(10);
  }
  delay(500);
  DEBUG_PRINT("Booting...");
  if (!prefs.begin("settings"))
  {
    DEBUG_PRINT("Preferences begin failed!");
  }
  encryptionBuffer = (uint8_t *)heap_caps_malloc(ENCRYPTION_BUFFER_SIZE, MALLOC_CAP_SPIRAM);
  if (encryptionBuffer == nullptr)
  {
    DEBUG_PRINT("Failed to allocate encryption buffer in PSRAM. Restarting...");
    delay(5000);
    ESP.restart();
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

  // we check local storage to see if we were connected to the server previously
  isPaired = getSessionToken().length() > 0;

  if (!isPaired)
  {
    DEBUG_PRINT("Not paired with server. Awaiting server to initiate handshake...");
  }

  while (!isPaired)
  {
    // if we were not connected, then we wait for the server's callback function which will set isPaired to true if the server sends challenge request and session token
    server.handleClient();

    delay(100);
  }

  server.stop();

  DEBUG_PRINT("Setup complete.");
}

int failedAttemptsToConnectToserver = 0;
int failedAttemptsToPairWithServer = 0;
int failedFramesToSendCounter = 0;

// if we are in the main loop it means we are successfully paired to the server and have all its info including ip, port etc.
void loop()
{
  wifiConnectivityCheck();

  handleResetButtonPress();

  // if somehow the connection to the server is lost we need to try to get a new session token
  if (!isPaired)
  {
    String baseServerUrl = getBaseServerUrl();
    String macAddress = getMacAddressString();

    if (baseServerUrl == "" || macAddress == "")
    {
      DEBUG_PRINT("No baseServerUrl or macAddress found in preferences. Cannot retrieve session token.");

      forgetServer();

      ESP.restart();

      return;
    }

    isPaired = getSessionTokenFromServer(baseServerUrl, macAddress);

    if (isPaired)
    {
      DEBUG_PRINT("Successfully paired with server.");

      doesHaveToReloadSessionToken = true;
      failedFramesToSendCounter = 0;
    }
    else
    {
      DEBUG_PRINT("Failed to pair with server.");

      failedAttemptsToPairWithServer++;
    }
  }

  // if we fail to connect to the server entierly multiple times then we reset the persistant storage and restart the device
  //(the same will happen if the reset button is pressed this is just a failsafe)
  if (failedAttemptsToConnectToserver >= MAX_FAILED_ATTEMPTS_TO_CONNECT_TO_SERVER) // 30 min failsafe time before reset
  {
    DEBUG_PRINT("Max failed attempts to connect to server reached. Restarting...");
    forgetServer();
    ESP.restart();
  }

  // if we fail to pair with the server multiple times then we reset the persistant storage and restart the device
  //(the same will happen if the reset button is pressed this is just a failsafe)
  if (failedAttemptsToPairWithServer >= FAILED_ATTEMPTS_TO_PAIR_WITH_SERVER_LIMIT)
  {
    DEBUG_PRINT("Failed attempts to pair with server limit reached. Restarting...");
    forgetServer();
    ESP.restart();
    return;
  }

  if (failedFramesToSendCounter >= FAILED_FRAMES_TO_SEND_MARK_AS_NOT_PAIRED_LIMIT)
  {
    isPaired = false;
    return;
  }

  if (!client.connected())
  {
    String serverAddressStr = getServerLocalIpAddress();
    if (serverAddressStr == "")
    {
      delay(100);
      return;
    }

    DEBUG_PRINT("Connecting to TCP server...");
    if (!client.connect(serverAddressStr.c_str(), ServerTcpPort))
    {
      DEBUG_PRINT("TCP connection failed.");
      delay(20000);
      failedAttemptsToConnectToserver++;
      return;
    }
    client.setNoDelay(true);
    DEBUG_PRINT("TCP connected.");
  }

  camera_fb_t *fb = esp_camera_fb_get();

  if (!fb)
  {
    DEBUG_PRINT("Camera capture failed");
    delay(100);
    return;
  }
  if (!sendFrameToServer(fb, client))
  {
    DEBUG_PRINT("Failed to send frame");
    client.stop();

    failedFramesToSendCounter++;
  }
  else
  {
    DEBUG_PRINT(String(millis()) + " -> Frame sent");

    failedFramesToSendCounter = 0;
  }
  esp_camera_fb_return(fb);
  delay(1);
}
