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

#define ROOT_CA_CERT \
"-----BEGIN CERTIFICATE-----\n" \
"MIIDCTCCAfGgAwIBAgIUHZEgJLikjh1uxTRsaj6cL1WmFl0wDQYJKoZIhvcNAQEL\n" \
"BQAwFDESMBAGA1UEAwwJTXlMb2NhbENBMB4XDTI2MDYyODIzMDQ1MVoXDTM2MDYy\n" \
"NTIzMDQ1MVowFDESMBAGA1UEAwwJTXlMb2NhbENBMIIBIjANBgkqhkiG9w0BAQEF\n" \
"AAOCAQ8AMIIBCgKCAQEAqqbJWGpDhUSR5IMv3EAY0UPRU9x/B8T2eSOK8w+FHEZ7\n" \
"OpzpafQBte5JWspHKOyei9YRbFFMIvwqMnRUfe3iVlxG1ByGLcD7tsLEtnn2UyOu\n" \
"RCMh9K3/ansdtxCCUe1BxMzo1MIhfUQE2gobdAQ7Vbfh3/BYbnXvg+bHRZ8jomAH\n" \
"H8v2h/eoq1Qj8zoMIzWoolvrl6n5JJYQNvThouNTDGxS7tkCxCrx5W20x+GYADXr\n" \
"suKigOH8bhjY07DMeNZM/HP5qz/CbGJdGvHUvfNXPjXWhOm6QzAcXZBg6H/VwP5e\n" \
"x8V5QsJ42yMEctrnl+sUi1eGDIbS8EFJoSkFp3F6bQIDAQABo1MwUTAdBgNVHQ4E\n" \
"FgQUtWcQDuEj2Wh0Pe/BlqOdXeD+uJMwHwYDVR0jBBgwFoAUtWcQDuEj2Wh0Pe/B\n" \
"lqOdXeD+uJMwDwYDVR0TAQH/BAUwAwEB/zANBgkqhkiG9w0BAQsFAAOCAQEABUWq\n" \
"GsuXPZo5QzyTfb8xcrUgD5Gucg8CLxiD7cpbHW8PmO5AjRoMr2Emm+lB0p84leg2\n" \
"lNQD2JJR3FDW4vrNoi2SCYFQA0qIdgbtY7gUZuKmp99TBHaq2K5ysKyyrs5ilU2Y\n" \
"y7Cjq4ytWjn8YqmQJcWqIaGiXlxPFXorHXwKdIpweXZsCU2ccRH3VWATHnQcYVjF\n" \
"VP1RPsRmC7JznFNFRy/Hn3r8WmWGPudK50QXahjFdeRrMnLLaF0gRuwN5FckdzSG\n" \
"UFVivkniQGhSDL0WtzOiAQEuHqmtVc4CgfwACB00DAROuYvCUMqn71M9Fa4v/+gj\n" \
"4QKkUELxybsxQ1B5Mg==\n" \
"-----END CERTIFICATE-----\n" \

