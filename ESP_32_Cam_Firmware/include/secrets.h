#pragma once

// =============================================================
// Optional manually-provisioned secrets.
//
// If you obtained credentials from the Blazor wizard "manual"
// path (no QR), uncomment HAS_PROVISIONED_SECRETS and paste the
// values below. The firmware will boot directly with them and
// skip QR scanning.
//
// If HAS_PROVISIONED_SECRETS is left commented out, the firmware
// will first try NVS (persistent storage) and fall back to QR
// scanning when nothing is stored.
// =============================================================

// Place Raw Secrets Here
//| | | | | | | | | | |
//| | | | | | | | | | |
// V V V V V V V V V V V

// #define HAS_PROVISIONED_SECRETS

#ifdef HAS_PROVISIONED_SECRETS
#define WIFI_SSID "your-ssid"
#define WIFI_PASS "your-password"
#define DEVICE_ID "00000000-0000-0000-0000-000000000000"
#define PRIVATE_KEY "base64-pkcs8-der-of-p256-private-key"
#define NONCE "base64-of-32-byte-nonce"
#define SERVER_IP "192.168.1.42"
#define SERVER_IDENTITY_PUBKEY "base64-spki-der-of-server-p256-public-key"
#endif

// =============================================================
// Self-signed CA the server uses for HTTPS.
// Replace by running CertGenerator.
// =============================================================

#define ROOT_CA_CERT                \
    "-----BEGIN CERTIFICATE-----\n" \
    "-----END CERTIFICATE-----\n"
