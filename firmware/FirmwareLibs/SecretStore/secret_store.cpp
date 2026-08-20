#include "secret_store.h"
#include "config.h"
#include "secrets.h"
#include <Preferences.h>
#include <esp_efuse.h>
#include <esp_hmac.h>
#include <esp_random.h>
#include <mbedtls/gcm.h>
#include <mbedtls/base64.h>
#include <string.h>

namespace secret_store
{
  static Preferences _prefs;
  static bool _ready = false;
  static bool _keyReady = false;
  static uint8_t _aesKey[32];

  static constexpr esp_efuse_block_t DEVICE_KEY_BLOCK = EFUSE_BLK_KEY4;
  static constexpr hmac_key_id_t DEVICE_KEY_HMAC_ID = HMAC_KEY4;
  static const char DEVICE_KEY_LABEL[] = "campr-secret-store-v1";

  static constexpr size_t GCM_IV_LEN = 12;
  static constexpr size_t GCM_TAG_LEN = 16;

  static bool ensureDeviceKey()
  {
    if (_keyReady)
    {
      return true;
    }

    // Only ever burn a fully unused block, and never touch any other eFuse.
    if (esp_efuse_key_block_unused(DEVICE_KEY_BLOCK))
    {
      uint8_t newKey[32];
      esp_fill_random(newKey, sizeof(newKey));
      esp_err_t burnErr = esp_efuse_write_key(DEVICE_KEY_BLOCK,
                                              ESP_EFUSE_KEY_PURPOSE_HMAC_UP,
                                              newKey, sizeof(newKey));
      memset(newKey, 0, sizeof(newKey));
      if (burnErr != ESP_OK)
      {
        DEBUG_PRINT("secret_store: failed to provision device HMAC key in eFuse");
        return false;
      }
      DEBUG_PRINT("secret_store: provisioned new device HMAC key in eFuse");
    }
    else if (esp_efuse_get_key_purpose(DEVICE_KEY_BLOCK) != ESP_EFUSE_KEY_PURPOSE_HMAC_UP)
    {
      DEBUG_PRINT("secret_store: eFuse key block already used for another purpose");
      return false;
    }

    esp_err_t hmacErr = esp_hmac_calculate(DEVICE_KEY_HMAC_ID,
                                           DEVICE_KEY_LABEL, strlen(DEVICE_KEY_LABEL),
                                           _aesKey);
    if (hmacErr != ESP_OK)
    {
      memset(_aesKey, 0, sizeof(_aesKey));
      DEBUG_PRINT("secret_store: failed to derive device key via HMAC peripheral");
      return false;
    }

    _keyReady = true;
    return true;
  }

  static bool encryptValue(const String &plain, String &out)
  {
    if (!ensureDeviceKey())
    {
      return false;
    }

    size_t plainLen = plain.length();
    size_t blobLen = GCM_IV_LEN + GCM_TAG_LEN + plainLen;
    uint8_t *blob = (uint8_t *)malloc(blobLen);
    if (blob == nullptr)
    {
      return false;
    }

    uint8_t *iv = blob;
    uint8_t *tag = blob + GCM_IV_LEN;
    uint8_t *cipher = blob + GCM_IV_LEN + GCM_TAG_LEN;
    esp_fill_random(iv, GCM_IV_LEN);

    mbedtls_gcm_context gcm;
    mbedtls_gcm_init(&gcm);
    bool ok = mbedtls_gcm_setkey(&gcm, MBEDTLS_CIPHER_ID_AES, _aesKey, 256) == 0 &&
              mbedtls_gcm_crypt_and_tag(&gcm, MBEDTLS_GCM_ENCRYPT, plainLen,
                                        iv, GCM_IV_LEN, nullptr, 0,
                                        (const uint8_t *)plain.c_str(), cipher,
                                        GCM_TAG_LEN, tag) == 0;
    mbedtls_gcm_free(&gcm);
    if (!ok)
    {
      free(blob);
      return false;
    }

    size_t b64Cap = ((blobLen + 2) / 3) * 4 + 1;
    uint8_t *b64 = (uint8_t *)malloc(b64Cap);
    if (b64 == nullptr)
    {
      free(blob);
      return false;
    }

    size_t b64Len = 0;
    int b64Err = mbedtls_base64_encode(b64, b64Cap, &b64Len, blob, blobLen);
    free(blob);
    if (b64Err != 0)
    {
      free(b64);
      return false;
    }

    b64[b64Len] = '\0';
    out = String((const char *)b64);
    free(b64);
    return true;
  }

  static bool decryptValue(const String &stored, String &out)
  {
    if (stored.length() == 0)
    {
      out = "";
      return true;
    }

    if (!ensureDeviceKey())
    {
      return false;
    }

    size_t b64Len = stored.length();
    size_t blobCap = (b64Len / 4) * 3 + 4;
    uint8_t *blob = (uint8_t *)malloc(blobCap);
    if (blob == nullptr)
    {
      return false;
    }

    size_t blobLen = 0;
    int b64Err = mbedtls_base64_decode(blob, blobCap, &blobLen,
                                       (const uint8_t *)stored.c_str(), b64Len);
    if (b64Err != 0 || blobLen < GCM_IV_LEN + GCM_TAG_LEN)
    {
      free(blob);
      return false;
    }

    const uint8_t *iv = blob;
    const uint8_t *tag = blob + GCM_IV_LEN;
    const uint8_t *cipher = blob + GCM_IV_LEN + GCM_TAG_LEN;
    size_t cipherLen = blobLen - GCM_IV_LEN - GCM_TAG_LEN;

    uint8_t *plain = (uint8_t *)malloc(cipherLen + 1);
    if (plain == nullptr)
    {
      free(blob);
      return false;
    }

    mbedtls_gcm_context gcm;
    mbedtls_gcm_init(&gcm);
    bool ok = mbedtls_gcm_setkey(&gcm, MBEDTLS_CIPHER_ID_AES, _aesKey, 256) == 0 &&
              mbedtls_gcm_auth_decrypt(&gcm, cipherLen, iv, GCM_IV_LEN, nullptr, 0,
                                       tag, GCM_TAG_LEN, cipher, plain) == 0;
    mbedtls_gcm_free(&gcm);
    free(blob);
    if (!ok)
    {
      free(plain);
      return false;
    }

    plain[cipherLen] = '\0';
    out = String((const char *)plain);
    free(plain);
    return true;
  }

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
    out.serverIdentityPubKey = SERVER_IDENTITY_PUBKEY;
    return out.wifiSsid.length() > 0 &&
           out.deviceId.length() > 0 &&
           out.privateKey.length() > 0 &&
           out.serverIp.length() > 0 &&
           out.serverIdentityPubKey.length() > 0;
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

    if (!decryptValue(_prefs.getString("wifiSsid", ""), out.wifiSsid) ||
        !decryptValue(_prefs.getString("wifiPass", ""), out.wifiPass) ||
        !decryptValue(_prefs.getString("deviceId", ""), out.deviceId) ||
        !decryptValue(_prefs.getString("privateKey", ""), out.privateKey) ||
        !decryptValue(_prefs.getString("nonce", ""), out.nonce) ||
        !decryptValue(_prefs.getString("serverIp", ""), out.serverIp) ||
        !decryptValue(_prefs.getString("srvIdPubKey", ""), out.serverIdentityPubKey))
    {
      return false;
    }

    return out.wifiSsid.length() > 0 &&
           out.deviceId.length() > 0 &&
           out.privateKey.length() > 0 &&
           out.serverIp.length() > 0 &&
           out.serverIdentityPubKey.length() > 0;
  }

  bool saveToNvs(const DeviceCredentials &creds)
  {
    if (!_ready)
    {
      return false;
    }

    String encWifiSsid, encWifiPass, encDeviceId, encPrivateKey, encNonce, encServerIp, encSrvIdPubKey;
    if (!encryptValue(creds.wifiSsid, encWifiSsid) ||
        !encryptValue(creds.wifiPass, encWifiPass) ||
        !encryptValue(creds.deviceId, encDeviceId) ||
        !encryptValue(creds.privateKey, encPrivateKey) ||
        !encryptValue(creds.nonce, encNonce) ||
        !encryptValue(creds.serverIp, encServerIp) ||
        !encryptValue(creds.serverIdentityPubKey, encSrvIdPubKey))
    {
      DEBUG_PRINT("secret_store: failed to encrypt credentials; nothing saved");
      return false;
    }

    _prefs.putString("wifiSsid", encWifiSsid);
    _prefs.putString("wifiPass", encWifiPass);
    _prefs.putString("deviceId", encDeviceId);
    _prefs.putString("privateKey", encPrivateKey);
    _prefs.putString("nonce", encNonce);
    _prefs.putString("serverIp", encServerIp);
    _prefs.putString("srvIdPubKey", encSrvIdPubKey);
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
