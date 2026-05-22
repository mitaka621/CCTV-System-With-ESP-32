#include "crypto_signer.h"
#include "config.h"
#include <mbedtls/pk.h>
#include <mbedtls/sha256.h>
#include <mbedtls/base64.h>
#include <mbedtls/ctr_drbg.h>
#include <mbedtls/entropy.h>

namespace crypto_signer
{
  static String deviceIdToHex32Lower(const String &dashed)
  {
    String out;
    out.reserve(32);
    for (size_t i = 0; i < dashed.length(); i++)
    {
      char c = dashed[i];
      if (c == '-')
      {
        continue;
      }
      if (c >= 'A' && c <= 'Z')
      {
        c = c + ('a' - 'A');
      }
      out += c;
    }
    return out;
  }

  static String bytesToHexLower(const uint8_t *data, size_t len)
  {
    static const char hex[] = "0123456789abcdef";
    String out;
    out.reserve(len * 2);
    for (size_t i = 0; i < len; i++)
    {
      out += hex[(data[i] >> 4) & 0x0F];
      out += hex[data[i] & 0x0F];
    }
    return out;
  }

  static bool base64Decode(const String &b64, uint8_t *out, size_t outCap, size_t &outLen)
  {
    int ret = mbedtls_base64_decode(out, outCap, &outLen,
                                    (const unsigned char *)b64.c_str(), b64.length());
    return ret == 0;
  }

  static bool base64Encode(const uint8_t *data, size_t len, String &out)
  {
    size_t outLen = 0;
    mbedtls_base64_encode(nullptr, 0, &outLen, data, len);
    if (outLen == 0)
    {
      outLen = ((len + 2) / 3) * 4 + 1;
    }
    uint8_t *buf = (uint8_t *)malloc(outLen + 1);
    if (buf == nullptr)
    {
      return false;
    }
    size_t written = 0;
    int ret = mbedtls_base64_encode(buf, outLen + 1, &written, data, len);
    if (ret != 0)
    {
      free(buf);
      return false;
    }
    buf[written] = 0;
    out = String((const char *)buf);
    free(buf);
    return true;
  }

  static bool parsePrivateKey(const String &privateKeyBase64,
                              mbedtls_pk_context *pk)
  {
    uint8_t *der = (uint8_t *)malloc(privateKeyBase64.length());
    if (der == nullptr)
    {
      return false;
    }
    size_t derLen = 0;
    if (!base64Decode(privateKeyBase64, der, privateKeyBase64.length(), derLen))
    {
      DEBUG_PRINT("Private key base64 decode failed");
      free(der);
      return false;
    }

    int ret = mbedtls_pk_parse_key(pk, der, derLen, nullptr, 0);
    free(der);
    if (ret != 0)
    {
      DEBUG_PRINT("mbedtls_pk_parse_key failed: " + String(ret));
      return false;
    }
    return true;
  }

  bool computeFingerprintHex(const String &privateKeyBase64, String &fingerprintHexOut)
  {
    mbedtls_entropy_context entropy;
    mbedtls_ctr_drbg_context ctr_drbg;
    mbedtls_pk_context pk;
    mbedtls_entropy_init(&entropy);
    mbedtls_ctr_drbg_init(&ctr_drbg);
    mbedtls_pk_init(&pk);

    const char pers[] = "campr-fp";
    int ret = mbedtls_ctr_drbg_seed(&ctr_drbg, mbedtls_entropy_func, &entropy,
                                    (const unsigned char *)pers, sizeof(pers) - 1);
    if (ret != 0)
    {
      mbedtls_pk_free(&pk);
      mbedtls_ctr_drbg_free(&ctr_drbg);
      mbedtls_entropy_free(&entropy);
      return false;
    }

    if (!parsePrivateKey(privateKeyBase64, &pk))
    {
      mbedtls_pk_free(&pk);
      mbedtls_ctr_drbg_free(&ctr_drbg);
      mbedtls_entropy_free(&entropy);
      return false;
    }

    uint8_t spkiBuf[256];
    int written = mbedtls_pk_write_pubkey_der(&pk, spkiBuf, sizeof(spkiBuf));
    if (written <= 0)
    {
      DEBUG_PRINT("mbedtls_pk_write_pubkey_der failed: " + String(written));
      mbedtls_pk_free(&pk);
      mbedtls_ctr_drbg_free(&ctr_drbg);
      mbedtls_entropy_free(&entropy);
      return false;
    }

    const uint8_t *spki = spkiBuf + sizeof(spkiBuf) - written;

    uint8_t fingerprint[32];
    mbedtls_sha256(spki, (size_t)written, fingerprint, 0);

    fingerprintHexOut = bytesToHexLower(fingerprint, sizeof(fingerprint));

    mbedtls_pk_free(&pk);
    mbedtls_ctr_drbg_free(&ctr_drbg);
    mbedtls_entropy_free(&entropy);
    return true;
  }

  bool computeBindingHash(const String &deviceIdDashed,
                          const String &fingerprintHex,
                          const String &nonceBase64,
                          uint8_t hashOut[32])
  {
    String deviceIdHex = deviceIdToHex32Lower(deviceIdDashed);
    if (deviceIdHex.length() != 32)
    {
      DEBUG_PRINT("Device id must reduce to 32 hex chars, got " + String(deviceIdHex.length()));
      return false;
    }
    if (fingerprintHex.length() != 64)
    {
      DEBUG_PRINT("Fingerprint must be 64 hex chars, got " + String(fingerprintHex.length()));
      return false;
    }

    uint8_t nonceBytes[64];
    size_t nonceLen = 0;
    if (!base64Decode(nonceBase64, nonceBytes, sizeof(nonceBytes), nonceLen))
    {
      DEBUG_PRINT("Nonce base64 decode failed");
      return false;
    }

    const char domain[] = DOMAIN_TAG;
    const size_t domainLen = sizeof(domain) - 1;

    mbedtls_sha256_context ctx;
    mbedtls_sha256_init(&ctx);
    mbedtls_sha256_starts(&ctx, 0);
    mbedtls_sha256_update(&ctx, (const unsigned char *)domain, domainLen);
    mbedtls_sha256_update(&ctx, (const unsigned char *)"|", 1);
    mbedtls_sha256_update(&ctx, (const unsigned char *)deviceIdHex.c_str(), 32);
    mbedtls_sha256_update(&ctx, (const unsigned char *)"|", 1);
    mbedtls_sha256_update(&ctx, (const unsigned char *)fingerprintHex.c_str(), 64);
    mbedtls_sha256_update(&ctx, (const unsigned char *)"|", 1);
    mbedtls_sha256_update(&ctx, nonceBytes, nonceLen);
    mbedtls_sha256_finish(&ctx, hashOut);
    mbedtls_sha256_free(&ctx);

    return true;
  }

  bool signHash(const String &privateKeyBase64,
                const uint8_t hash[32],
                String &signatureBase64Out)
  {
    mbedtls_entropy_context entropy;
    mbedtls_ctr_drbg_context ctr_drbg;
    mbedtls_pk_context pk;
    mbedtls_entropy_init(&entropy);
    mbedtls_ctr_drbg_init(&ctr_drbg);
    mbedtls_pk_init(&pk);

    const char pers[] = "campr-sign";
    int ret = mbedtls_ctr_drbg_seed(&ctr_drbg, mbedtls_entropy_func, &entropy,
                                    (const unsigned char *)pers, sizeof(pers) - 1);
    if (ret != 0)
    {
      mbedtls_pk_free(&pk);
      mbedtls_ctr_drbg_free(&ctr_drbg);
      mbedtls_entropy_free(&entropy);
      return false;
    }

    if (!parsePrivateKey(privateKeyBase64, &pk))
    {
      mbedtls_pk_free(&pk);
      mbedtls_ctr_drbg_free(&ctr_drbg);
      mbedtls_entropy_free(&entropy);
      return false;
    }

    uint8_t sig[MBEDTLS_PK_SIGNATURE_MAX_SIZE];
    size_t sigLen = 0;
    ret = mbedtls_pk_sign(&pk, MBEDTLS_MD_SHA256, hash, 32,
                          sig, &sigLen,
                          mbedtls_ctr_drbg_random, &ctr_drbg);
    if (ret != 0)
    {
      DEBUG_PRINT("mbedtls_pk_sign failed: " + String(ret));
      mbedtls_pk_free(&pk);
      mbedtls_ctr_drbg_free(&ctr_drbg);
      mbedtls_entropy_free(&entropy);
      return false;
    }

    bool ok = base64Encode(sig, sigLen, signatureBase64Out);

    mbedtls_pk_free(&pk);
    mbedtls_ctr_drbg_free(&ctr_drbg);
    mbedtls_entropy_free(&entropy);
    return ok;
  }
}
