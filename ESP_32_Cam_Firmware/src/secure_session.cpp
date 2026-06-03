#include "secure_session.h"
#include "config.h"
#include <WiFi.h>
#include <esp_system.h>
#include <esp_random.h>
#include <mbedtls/base64.h>
#include <mbedtls/ctr_drbg.h>
#include <mbedtls/ecdh.h>
#include <mbedtls/ecdsa.h>
#include <mbedtls/ecp.h>
#include <mbedtls/entropy.h>
#include <mbedtls/error.h>
#include <mbedtls/gcm.h>
#include <mbedtls/md.h>
#include <mbedtls/pk.h>
#include <mbedtls/sha256.h>
#include <aes/esp_aes_gcm.h>
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>
#include <freertos/queue.h>
#include <freertos/semphr.h>

namespace secure_session
{
  static constexpr size_t DEVICE_ID_LEN = 16;
  static constexpr size_t NONCE_LEN = 32;
  static constexpr size_t EPHEMERAL_PUB_LEN = 65;
  static constexpr size_t SIGNATURE_LEN = 64;
  static constexpr size_t IV_BASE_LEN = 4;
  static constexpr size_t SESSION_ID_LEN = 16;
  static constexpr size_t GCM_TAG_LEN = 16;
  static constexpr size_t GCM_IV_LEN = 12;
  static constexpr size_t SEQ_LEN = 8;
  static constexpr size_t ENC_POOL_SIZE = 2;
  static constexpr size_t RESOLUTION_HEADER_LEN = 8;

  struct SessionState
  {
    WiFiClient client;
    uint8_t deviceIdRaw[DEVICE_ID_LEN];
    uint8_t sessionKey[32];
    uint8_t ivBase[IV_BASE_LEN];
    uint8_t sessionId[SESSION_ID_LEN];
    uint64_t seq;
    unsigned long startedAtMs;
    esp_gcm_context gcm;
    bool active;
  };

  struct EncryptedFrame
  {
    uint8_t *buf;
    size_t poolIdx;
    size_t cipherLen;
    uint64_t seq;
    uint8_t tag[GCM_TAG_LEN];
  };

  static SessionState _state = {};
  static bool _gcmReady = false;

  static uint8_t *_encPool[ENC_POOL_SIZE] = {nullptr, nullptr};
  static QueueHandle_t _freeSlots = nullptr;
  static QueueHandle_t _readyFrames = nullptr;
  static TaskHandle_t _senderTask = nullptr;
  static SemaphoreHandle_t _senderExitedSem = nullptr;
  static volatile bool _senderOk = true;
  static volatile bool _senderShouldExit = false;
  static volatile uint32_t _lastSendUs = 0;

  static uint8_t hexValue(char c)
  {
    if (c >= '0' && c <= '9') return c - '0';
    if (c >= 'a' && c <= 'f') return 10 + (c - 'a');
    if (c >= 'A' && c <= 'F') return 10 + (c - 'A');
    return 0xFF;
  }

  static bool deviceIdToRawBytes(const String &dashed, uint8_t out[DEVICE_ID_LEN])
  {
    size_t idx = 0;
    size_t i = 0;
    while (i < dashed.length() && idx < DEVICE_ID_LEN)
    {
      char c = dashed[i];
      if (c == '-')
      {
        i++;
        continue;
      }
      if (i + 1 >= dashed.length()) return false;
      uint8_t hi = hexValue(c);
      uint8_t lo = hexValue(dashed[i + 1]);
      if (hi == 0xFF || lo == 0xFF) return false;
      out[idx++] = (uint8_t)((hi << 4) | lo);
      i += 2;
    }
    return idx == DEVICE_ID_LEN;
  }

  static bool base64Decode(const String &b64, uint8_t *out, size_t outCap, size_t &outLen)
  {
    int ret = mbedtls_base64_decode(out, outCap, &outLen,
                                    (const unsigned char *)b64.c_str(), b64.length());
    return ret == 0;
  }

  static bool readExact(WiFiClient &client, uint8_t *buf, size_t len, unsigned long timeoutMs)
  {
    size_t got = 0;
    unsigned long start = millis();
    while (got < len)
    {
      if (!client.connected()) return false;
      int avail = client.available();
      if (avail <= 0)
      {
        if (millis() - start > timeoutMs) return false;
        delay(1);
        continue;
      }
      int chunk = client.read(buf + got, len - got);
      if (chunk <= 0)
      {
        if (millis() - start > timeoutMs) return false;
        delay(1);
        continue;
      }
      got += (size_t)chunk;
    }
    return true;
  }

  static bool writeAll(WiFiClient &client, const uint8_t *buf, size_t len)
  {
    size_t sent = 0;
    while (sent < len)
    {
      int n = client.write(buf + sent, len - sent);
      if (n <= 0) return false;
      sent += (size_t)n;
    }
    return true;
  }

  static void writeBE32(uint8_t *out, uint32_t v)
  {
    out[0] = (uint8_t)(v >> 24);
    out[1] = (uint8_t)(v >> 16);
    out[2] = (uint8_t)(v >> 8);
    out[3] = (uint8_t)v;
  }

  static void writeBE64(uint8_t *out, uint64_t v)
  {
    out[0] = (uint8_t)(v >> 56);
    out[1] = (uint8_t)(v >> 48);
    out[2] = (uint8_t)(v >> 40);
    out[3] = (uint8_t)(v >> 32);
    out[4] = (uint8_t)(v >> 24);
    out[5] = (uint8_t)(v >> 16);
    out[6] = (uint8_t)(v >> 8);
    out[7] = (uint8_t)v;
  }

  static int hmacSha256(const uint8_t *key, size_t keyLen,
                        const uint8_t *data, size_t dataLen,
                        uint8_t out[32])
  {
    const mbedtls_md_info_t *md = mbedtls_md_info_from_type(MBEDTLS_MD_SHA256);
    if (md == nullptr) return -1;
    return mbedtls_md_hmac(md, key, keyLen, data, dataLen, out);
  }

  static int hkdfSha256(const uint8_t *salt, size_t saltLen,
                        const uint8_t *ikm, size_t ikmLen,
                        const uint8_t *info, size_t infoLen,
                        uint8_t *okm, size_t okmLen)
  {
    uint8_t prk[32];
    if (hmacSha256(salt, saltLen, ikm, ikmLen, prk) != 0) return -1;

    if (okmLen > 255 * 32) return -1;

    const mbedtls_md_info_t *md = mbedtls_md_info_from_type(MBEDTLS_MD_SHA256);
    mbedtls_md_context_t ctx;
    mbedtls_md_init(&ctx);
    if (mbedtls_md_setup(&ctx, md, 1) != 0)
    {
      mbedtls_md_free(&ctx);
      return -1;
    }

    uint8_t t[32];
    size_t tLen = 0;
    size_t produced = 0;
    uint8_t counter = 1;

    while (produced < okmLen)
    {
      if (mbedtls_md_hmac_starts(&ctx, prk, 32) != 0) { mbedtls_md_free(&ctx); return -1; }
      if (tLen > 0)
      {
        if (mbedtls_md_hmac_update(&ctx, t, tLen) != 0) { mbedtls_md_free(&ctx); return -1; }
      }
      if (infoLen > 0)
      {
        if (mbedtls_md_hmac_update(&ctx, info, infoLen) != 0) { mbedtls_md_free(&ctx); return -1; }
      }
      if (mbedtls_md_hmac_update(&ctx, &counter, 1) != 0) { mbedtls_md_free(&ctx); return -1; }
      if (mbedtls_md_hmac_finish(&ctx, t) != 0) { mbedtls_md_free(&ctx); return -1; }
      tLen = 32;
      size_t copy = (okmLen - produced) < 32 ? (okmLen - produced) : 32;
      memcpy(okm + produced, t, copy);
      produced += copy;
      counter++;
    }

    mbedtls_md_free(&ctx);
    memset(prk, 0, sizeof(prk));
    memset(t, 0, sizeof(t));
    return 0;
  }

  static void senderTaskFn(void *)
  {
    while (!_senderShouldExit)
    {
      EncryptedFrame f;
      if (xQueueReceive(_readyFrames, &f, portMAX_DELAY) != pdTRUE) continue;

      if (_senderShouldExit) break;
      if (f.poolIdx == SIZE_MAX) break;

      uint8_t header[4 + SEQ_LEN + GCM_TAG_LEN];
      uint32_t totalLen = (uint32_t)(SEQ_LEN + GCM_TAG_LEN + f.cipherLen);
      writeBE32(header, totalLen);
      writeBE64(header + 4, f.seq);
      memcpy(header + 4 + SEQ_LEN, f.tag, GCM_TAG_LEN);

      unsigned long t0 = micros();
      bool ok = writeAll(_state.client, header, sizeof(header))
             && writeAll(_state.client, f.buf, f.cipherLen);
      _lastSendUs = (uint32_t)(micros() - t0);

      if (!ok)
      {
        _senderOk = false;
      }

      if (_freeSlots != nullptr)
      {
        xQueueSend(_freeSlots, &f.poolIdx, 0);
      }
    }

    if (_state.client.connected())
    {
      _state.client.stop();
    }

    if (_senderExitedSem != nullptr)
    {
      xSemaphoreGive(_senderExitedSem);
    }

    vTaskDelete(nullptr);
  }

  static bool initSenderResources()
  {
    for (size_t i = 0; i < ENC_POOL_SIZE; ++i)
    {
      _encPool[i] = (uint8_t *)heap_caps_malloc(ENCRYPTION_BUFFER_SIZE, MALLOC_CAP_SPIRAM);
      if (_encPool[i] == nullptr)
      {
        DEBUG_PRINT("secure_session: failed to allocate cipher pool slot in PSRAM");
        return false;
      }
    }

    _freeSlots = xQueueCreate(ENC_POOL_SIZE, sizeof(size_t));
    _readyFrames = xQueueCreate(ENC_POOL_SIZE + 1, sizeof(EncryptedFrame));
    _senderExitedSem = xSemaphoreCreateBinary();
    if (_freeSlots == nullptr || _readyFrames == nullptr || _senderExitedSem == nullptr) return false;

    for (size_t i = 0; i < ENC_POOL_SIZE; ++i)
    {
      xQueueSend(_freeSlots, &i, 0);
    }

    _senderOk = true;
    _senderShouldExit = false;
    _lastSendUs = 0;

    BaseType_t taskRes = xTaskCreatePinnedToCore(
        senderTaskFn, "stream-sender", 4096, nullptr, 5, &_senderTask, 0);
    return taskRes == pdPASS;
  }

  static void teardownSenderResources()
  {
    if (_senderTask != nullptr)
    {
      _senderShouldExit = true;

      if (_readyFrames != nullptr)
      {
        EncryptedFrame sentinel = {};
        sentinel.poolIdx = SIZE_MAX;
        xQueueSend(_readyFrames, &sentinel, 0);
      }

      if (_senderExitedSem != nullptr)
      {
        xSemaphoreTake(_senderExitedSem, pdMS_TO_TICKS(2000));
      }

      _senderTask = nullptr;
    }

    if (_senderExitedSem != nullptr)
    {
      vSemaphoreDelete(_senderExitedSem);
      _senderExitedSem = nullptr;
    }

    if (_readyFrames != nullptr)
    {
      vQueueDelete(_readyFrames);
      _readyFrames = nullptr;
    }
    if (_freeSlots != nullptr)
    {
      vQueueDelete(_freeSlots);
      _freeSlots = nullptr;
    }
    for (size_t i = 0; i < ENC_POOL_SIZE; ++i)
    {
      if (_encPool[i] != nullptr)
      {
        heap_caps_free(_encPool[i]);
        _encPool[i] = nullptr;
      }
    }
  }

  static void resetState()
  {
    bool senderWasRunning = (_senderTask != nullptr);

    teardownSenderResources();

    if (!senderWasRunning && _state.client.connected())
    {
      _state.client.stop();
    }

    if (_gcmReady)
    {
      esp_aes_gcm_free(&_state.gcm);
      _gcmReady = false;
    }
    memset(_state.sessionKey, 0, sizeof(_state.sessionKey));
    memset(_state.ivBase, 0, sizeof(_state.ivBase));
    memset(_state.sessionId, 0, sizeof(_state.sessionId));
    _state.seq = 0;
    _state.active = false;
    _senderOk = true;
    _senderShouldExit = false;
    _lastSendUs = 0;
  }

  static bool performHandshake(const DeviceCredentials &creds)
  {
    if (!deviceIdToRawBytes(creds.deviceId, _state.deviceIdRaw))
    {
      DEBUG_PRINT("secure_session: deviceId parse failed");
      return false;
    }

    if (!writeAll(_state.client, _state.deviceIdRaw, DEVICE_ID_LEN))
    {
      DEBUG_PRINT("secure_session: failed to send deviceId");
      return false;
    }

    uint8_t serverHello[NONCE_LEN + EPHEMERAL_PUB_LEN + SIGNATURE_LEN];
    if (!readExact(_state.client, serverHello, sizeof(serverHello), STREAM_HANDSHAKE_TIMEOUT_MS))
    {
      DEBUG_PRINT("secure_session: failed to read server hello");
      return false;
    }

    const uint8_t *nonceServer = serverHello;
    const uint8_t *ephemeralServerPub = serverHello + NONCE_LEN;
    const uint8_t *serverSignature = serverHello + NONCE_LEN + EPHEMERAL_PUB_LEN;

    if (ephemeralServerPub[0] != 0x04)
    {
      DEBUG_PRINT("secure_session: server ephemeral pub format byte invalid");
      return false;
    }

    uint8_t serverSignatureHash[32];
    {
      const char domain[] = STREAM_DOMAIN_TAG;
      const size_t domainLen = sizeof(domain);
      mbedtls_sha256_context sha;
      mbedtls_sha256_init(&sha);
      mbedtls_sha256_starts(&sha, 0);
      mbedtls_sha256_update(&sha, (const unsigned char *)domain, domainLen);
      mbedtls_sha256_update(&sha, _state.deviceIdRaw, DEVICE_ID_LEN);
      mbedtls_sha256_update(&sha, nonceServer, NONCE_LEN);
      mbedtls_sha256_update(&sha, ephemeralServerPub, EPHEMERAL_PUB_LEN);
      mbedtls_sha256_finish(&sha, serverSignatureHash);
      mbedtls_sha256_free(&sha);
    }

    bool serverSignatureOk = false;
    {
      size_t spkiCap = creds.serverIdentityPubKey.length();
      uint8_t *spkiDer = (uint8_t *)malloc(spkiCap);
      if (spkiDer == nullptr) return false;
      size_t spkiLen = 0;
      if (!base64Decode(creds.serverIdentityPubKey, spkiDer, spkiCap, spkiLen))
      {
        DEBUG_PRINT("secure_session: server identity pub key base64 decode failed");
        free(spkiDer);
        return false;
      }

      mbedtls_pk_context serverPk;
      mbedtls_pk_init(&serverPk);
      int ret = mbedtls_pk_parse_public_key(&serverPk, spkiDer, spkiLen);
      free(spkiDer);
      if (ret != 0)
      {
        DEBUG_PRINT(String("secure_session: parse server pub key failed: ") + ret);
        mbedtls_pk_free(&serverPk);
        return false;
      }

      mbedtls_ecp_keypair *serverKp = mbedtls_pk_ec(serverPk);

      mbedtls_mpi r, s;
      mbedtls_mpi_init(&r);
      mbedtls_mpi_init(&s);
      mbedtls_mpi_read_binary(&r, serverSignature, 32);
      mbedtls_mpi_read_binary(&s, serverSignature + 32, 32);

      ret = mbedtls_ecdsa_verify(&serverKp->grp,
                                 serverSignatureHash, sizeof(serverSignatureHash),
                                 &serverKp->Q,
                                 &r, &s);

      mbedtls_mpi_free(&r);
      mbedtls_mpi_free(&s);
      mbedtls_pk_free(&serverPk);

      if (ret != 0)
      {
        DEBUG_PRINT(String("secure_session: server signature INVALID: ") + ret);
        return false;
      }
      serverSignatureOk = true;
    }
    if (!serverSignatureOk) return false;

    mbedtls_entropy_context entropy;
    mbedtls_ctr_drbg_context ctrDrbg;
    mbedtls_entropy_init(&entropy);
    mbedtls_ctr_drbg_init(&ctrDrbg);

    const char pers[] = "campr-stream";
    if (mbedtls_ctr_drbg_seed(&ctrDrbg, mbedtls_entropy_func, &entropy,
                              (const unsigned char *)pers, sizeof(pers) - 1) != 0)
    {
      DEBUG_PRINT("secure_session: drbg seed failed");
      mbedtls_ctr_drbg_free(&ctrDrbg);
      mbedtls_entropy_free(&entropy);
      return false;
    }

    mbedtls_ecp_group grp;
    mbedtls_mpi ephemeralDevicePriv;
    mbedtls_ecp_point ephemeralDevicePub;
    mbedtls_ecp_point peerEphemeralServerPub;
    mbedtls_mpi sharedSecretMpi;
    mbedtls_ecp_group_init(&grp);
    mbedtls_mpi_init(&ephemeralDevicePriv);
    mbedtls_ecp_point_init(&ephemeralDevicePub);
    mbedtls_ecp_point_init(&peerEphemeralServerPub);
    mbedtls_mpi_init(&sharedSecretMpi);

    bool ecdhOk = false;
    uint8_t sharedSecretBytes[32];
    uint8_t ephemeralDevicePubBytes[EPHEMERAL_PUB_LEN];

    do
    {
      if (mbedtls_ecp_group_load(&grp, MBEDTLS_ECP_DP_SECP256R1) != 0) break;

      if (mbedtls_ecp_gen_keypair(&grp, &ephemeralDevicePriv, &ephemeralDevicePub,
                                  mbedtls_ctr_drbg_random, &ctrDrbg) != 0) break;

      size_t outLen = 0;
      if (mbedtls_ecp_point_write_binary(&grp, &ephemeralDevicePub,
                                         MBEDTLS_ECP_PF_UNCOMPRESSED,
                                         &outLen, ephemeralDevicePubBytes, sizeof(ephemeralDevicePubBytes)) != 0) break;
      if (outLen != EPHEMERAL_PUB_LEN) break;

      if (mbedtls_ecp_point_read_binary(&grp, &peerEphemeralServerPub, ephemeralServerPub, EPHEMERAL_PUB_LEN) != 0) break;
      if (mbedtls_ecp_check_pubkey(&grp, &peerEphemeralServerPub) != 0) break;

      if (mbedtls_ecdh_compute_shared(&grp, &sharedSecretMpi, &peerEphemeralServerPub, &ephemeralDevicePriv,
                                      mbedtls_ctr_drbg_random, &ctrDrbg) != 0) break;

      if (mbedtls_mpi_write_binary(&sharedSecretMpi, sharedSecretBytes, sizeof(sharedSecretBytes)) != 0) break;

      ecdhOk = true;
    } while (false);

    mbedtls_ecp_group_free(&grp);
    mbedtls_mpi_free(&ephemeralDevicePriv);
    mbedtls_ecp_point_free(&ephemeralDevicePub);
    mbedtls_ecp_point_free(&peerEphemeralServerPub);
    mbedtls_mpi_free(&sharedSecretMpi);

    if (!ecdhOk)
    {
      DEBUG_PRINT("secure_session: ECDH failed");
      mbedtls_ctr_drbg_free(&ctrDrbg);
      mbedtls_entropy_free(&entropy);
      return false;
    }

    uint8_t nonceDevice[NONCE_LEN];
    esp_fill_random(nonceDevice, sizeof(nonceDevice));

    uint8_t deviceSignatureHash[32];
    {
      const char domain[] = STREAM_DOMAIN_TAG;
      const size_t domainLen = sizeof(domain);
      mbedtls_sha256_context sha;
      mbedtls_sha256_init(&sha);
      mbedtls_sha256_starts(&sha, 0);
      mbedtls_sha256_update(&sha, (const unsigned char *)domain, domainLen);
      mbedtls_sha256_update(&sha, _state.deviceIdRaw, DEVICE_ID_LEN);
      mbedtls_sha256_update(&sha, nonceServer, NONCE_LEN);
      mbedtls_sha256_update(&sha, nonceDevice, NONCE_LEN);
      mbedtls_sha256_update(&sha, ephemeralServerPub, EPHEMERAL_PUB_LEN);
      mbedtls_sha256_update(&sha, ephemeralDevicePubBytes, EPHEMERAL_PUB_LEN);
      mbedtls_sha256_finish(&sha, deviceSignatureHash);
      mbedtls_sha256_free(&sha);
    }

    uint8_t deviceSignature[SIGNATURE_LEN];
    bool signOk = false;
    {
      size_t derCap = creds.privateKey.length();
      uint8_t *derBuf = (uint8_t *)malloc(derCap);
      if (derBuf == nullptr)
      {
        mbedtls_ctr_drbg_free(&ctrDrbg);
        mbedtls_entropy_free(&entropy);
        memset(sharedSecretBytes, 0, sizeof(sharedSecretBytes));
        return false;
      }
      size_t derLen = 0;
      if (!base64Decode(creds.privateKey, derBuf, derCap, derLen))
      {
        DEBUG_PRINT("secure_session: device private key base64 decode failed");
        free(derBuf);
        mbedtls_ctr_drbg_free(&ctrDrbg);
        mbedtls_entropy_free(&entropy);
        memset(sharedSecretBytes, 0, sizeof(sharedSecretBytes));
        return false;
      }

      mbedtls_pk_context devicePk;
      mbedtls_pk_init(&devicePk);
      int ret = mbedtls_pk_parse_key(&devicePk, derBuf, derLen, nullptr, 0);
      free(derBuf);
      if (ret != 0)
      {
        DEBUG_PRINT(String("secure_session: device private key parse failed: ") + ret);
        mbedtls_pk_free(&devicePk);
        mbedtls_ctr_drbg_free(&ctrDrbg);
        mbedtls_entropy_free(&entropy);
        memset(sharedSecretBytes, 0, sizeof(sharedSecretBytes));
        return false;
      }

      mbedtls_ecp_keypair *kp = mbedtls_pk_ec(devicePk);
      mbedtls_mpi r, s;
      mbedtls_mpi_init(&r);
      mbedtls_mpi_init(&s);
      ret = mbedtls_ecdsa_sign(&kp->grp, &r, &s,
                               &kp->d,
                               deviceSignatureHash, sizeof(deviceSignatureHash),
                               mbedtls_ctr_drbg_random, &ctrDrbg);
      if (ret == 0)
      {
        if (mbedtls_mpi_write_binary(&r, deviceSignature, 32) == 0 &&
            mbedtls_mpi_write_binary(&s, deviceSignature + 32, 32) == 0)
        {
          signOk = true;
        }
      }
      mbedtls_mpi_free(&r);
      mbedtls_mpi_free(&s);
      mbedtls_pk_free(&devicePk);
    }

    mbedtls_ctr_drbg_free(&ctrDrbg);
    mbedtls_entropy_free(&entropy);

    if (!signOk)
    {
      DEBUG_PRINT("secure_session: device sign failed");
      memset(sharedSecretBytes, 0, sizeof(sharedSecretBytes));
      return false;
    }

    uint8_t deviceHello[NONCE_LEN + EPHEMERAL_PUB_LEN + SIGNATURE_LEN];
    memcpy(deviceHello, nonceDevice, NONCE_LEN);
    memcpy(deviceHello + NONCE_LEN, ephemeralDevicePubBytes, EPHEMERAL_PUB_LEN);
    memcpy(deviceHello + NONCE_LEN + EPHEMERAL_PUB_LEN, deviceSignature, SIGNATURE_LEN);

    if (!writeAll(_state.client, deviceHello, sizeof(deviceHello)))
    {
      DEBUG_PRINT("secure_session: failed to send device hello");
      memset(sharedSecretBytes, 0, sizeof(sharedSecretBytes));
      return false;
    }

    uint8_t hkdfSalt[NONCE_LEN * 2];
    memcpy(hkdfSalt, nonceServer, NONCE_LEN);
    memcpy(hkdfSalt + NONCE_LEN, nonceDevice, NONCE_LEN);

    const size_t derivedKeyMaterialLen = 32 + IV_BASE_LEN + SESSION_ID_LEN;
    uint8_t derivedKeyMaterial[derivedKeyMaterialLen];
    const char info[] = STREAM_HKDF_INFO;
    int hkdfRet = hkdfSha256(hkdfSalt, sizeof(hkdfSalt),
                              sharedSecretBytes, sizeof(sharedSecretBytes),
                              (const unsigned char *)info, sizeof(info) - 1,
                              derivedKeyMaterial, derivedKeyMaterialLen);
    memset(sharedSecretBytes, 0, sizeof(sharedSecretBytes));
    if (hkdfRet != 0)
    {
      DEBUG_PRINT(String("secure_session: HKDF failed: ") + hkdfRet);
      memset(derivedKeyMaterial, 0, sizeof(derivedKeyMaterial));
      return false;
    }

    memcpy(_state.sessionKey, derivedKeyMaterial, 32);
    memcpy(_state.ivBase, derivedKeyMaterial + 32, IV_BASE_LEN);
    memcpy(_state.sessionId, derivedKeyMaterial + 32 + IV_BASE_LEN, SESSION_ID_LEN);
    memset(derivedKeyMaterial, 0, sizeof(derivedKeyMaterial));

    esp_aes_gcm_init(&_state.gcm);
    if (esp_aes_gcm_setkey(&_state.gcm, MBEDTLS_CIPHER_ID_AES, _state.sessionKey, 256) != 0)
    {
      DEBUG_PRINT("secure_session: gcm_setkey failed");
      esp_aes_gcm_free(&_state.gcm);
      return false;
    }
    _gcmReady = true;

    _state.seq = 0;
    _state.startedAtMs = millis();
    _state.active = true;

    DEBUG_PRINT("secure_session: handshake complete");
    return true;
  }

  bool begin(const DeviceCredentials &creds, uint16_t serverPort)
  {
    end();

    if (creds.serverIp.length() == 0 || creds.deviceId.length() == 0
        || creds.privateKey.length() == 0 || creds.serverIdentityPubKey.length() == 0)
    {
      DEBUG_PRINT("secure_session: incomplete credentials");
      return false;
    }

    DEBUG_PRINT("secure_session: connecting to " + creds.serverIp + ":" + String(serverPort));
    _state.client.setNoDelay(true);
    if (!_state.client.connect(creds.serverIp.c_str(), serverPort))
    {
      DEBUG_PRINT("secure_session: TCP connect failed");
      return false;
    }
    _state.client.setNoDelay(true);

    if (!performHandshake(creds))
    {
      resetState();
      return false;
    }

    if (!initSenderResources())
    {
      DEBUG_PRINT("secure_session: failed to init sender resources");
      resetState();
      return false;
    }

    return true;
  }

  bool isActive()
  {
    if (!_state.active) return false;
    if (!_senderOk)
    {
      DEBUG_PRINT("secure_session: sender task reported failure");
      resetState();
      return false;
    }
    if (!_state.client.connected())
    {
      resetState();
      return false;
    }
    if (millis() - _state.startedAtMs > STREAM_MAX_SESSION_DURATION_MS)
    {
      DEBUG_PRINT("secure_session: session expired by time");
      resetState();
      return false;
    }
    if (_state.seq >= STREAM_MAX_SESSION_FRAMES)
    {
      DEBUG_PRINT("secure_session: session expired by frame count");
      resetState();
      return false;
    }
    return true;
  }

  bool sendFrame(const uint8_t *data, size_t len, uint32_t width, uint32_t height, FrameTiming *timing)
  {
    if (timing != nullptr)
    {
      timing->encryptUs = 0;
      timing->sendUs = 0;
    }

    if (!isActive()) return false;
    if (data == nullptr || len == 0) return false;
    if (RESOLUTION_HEADER_LEN + len > ENCRYPTION_BUFFER_SIZE) return false;
    if (_freeSlots == nullptr || _readyFrames == nullptr) return false;

    size_t slot;
    if (xQueueReceive(_freeSlots, &slot, pdMS_TO_TICKS(STREAM_FRAME_TCP_TIMEOUT_MS)) != pdTRUE)
    {
      DEBUG_PRINT("secure_session: timed out waiting for free encryption slot");
      return false;
    }

    EncryptedFrame f;
    f.buf = _encPool[slot];
    f.poolIdx = slot;

    size_t plainLen = RESOLUTION_HEADER_LEN + len;
    f.cipherLen = plainLen;

    writeBE32(f.buf, width);
    writeBE32(f.buf + 4, height);
    memcpy(f.buf + RESOLUTION_HEADER_LEN, data, len);

    _state.seq++;
    f.seq = _state.seq;

    uint8_t iv[GCM_IV_LEN];
    memcpy(iv, _state.ivBase, IV_BASE_LEN);
    writeBE64(iv + IV_BASE_LEN, f.seq);

    uint8_t aad[SESSION_ID_LEN + DEVICE_ID_LEN + SEQ_LEN];
    memcpy(aad, _state.sessionId, SESSION_ID_LEN);
    memcpy(aad + SESSION_ID_LEN, _state.deviceIdRaw, DEVICE_ID_LEN);
    writeBE64(aad + SESSION_ID_LEN + DEVICE_ID_LEN, f.seq);

    unsigned long encryptStartUs = micros();
    int ret = esp_aes_gcm_crypt_and_tag(&_state.gcm, MBEDTLS_GCM_ENCRYPT,
                                        plainLen,
                                        iv, GCM_IV_LEN,
                                        aad, sizeof(aad),
                                        f.buf, f.buf,
                                        GCM_TAG_LEN, f.tag);
    if (timing != nullptr)
    {
      timing->encryptUs = (uint32_t)(micros() - encryptStartUs);
    }
    if (ret != 0)
    {
      xQueueSend(_freeSlots, &slot, 0);
      DEBUG_PRINT(String("secure_session: gcm encrypt failed: ") + ret);
      resetState();
      return false;
    }

    if (xQueueSend(_readyFrames, &f, pdMS_TO_TICKS(STREAM_FRAME_TCP_TIMEOUT_MS)) != pdTRUE)
    {
      xQueueSend(_freeSlots, &slot, 0);
      DEBUG_PRINT("secure_session: failed to enqueue encrypted frame");
      return false;
    }

    if (timing != nullptr)
    {
      timing->sendUs = _lastSendUs;
    }
    return true;
  }

  void end()
  {
    resetState();
  }
}
