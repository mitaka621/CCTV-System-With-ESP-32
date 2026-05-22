#pragma once

#include <Arduino.h>

namespace crypto_signer
{
  bool computeFingerprintHex(const String &privateKeyBase64, String &fingerprintHexOut);

  bool computeBindingHash(const String &deviceIdDashed,
                          const String &fingerprintHex,
                          const String &nonceBase64,
                          uint8_t hashOut[32]);

  bool signHash(const String &privateKeyBase64,
                const uint8_t hash[32],
                String &signatureBase64Out);
}
