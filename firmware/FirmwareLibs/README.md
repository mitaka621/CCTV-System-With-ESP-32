# FirmwareLibs

Shared PlatformIO libraries used by every device firmware under `firmware/`.
Each folder is a standalone library with its own `library.json`. The library
dependency finder only compiles the ones a project actually includes.

| Library | Purpose |
| --- | --- |
| `SecretStore` | `DeviceCredentials`, NVS storage, compile-time secrets fallback |
| `Crypto` | Ed25519 signing (TweetNaCl) used during pairing |
| `DataChannel` | Encrypted, mutually authenticated TCP channel to the portal server |
| `Provisioning` | Soft-AP setup wizard web server and server-side pairing verification |
| `WiFiManager` | Station-mode connect and reconnect |
| `LedIndicators` | RGB state indicator and single-colour status LED |
| `Buzzer` | Pulsed alarm buzzer |

## Adding a new device project

Start from `../NewDeviceExample`. It is a buildable template that uses every
library here and shows both the send and the receive side of `DataChannel`. Copy
the folder, rename it, then work through the steps below.

1. Create `firmware/<DeviceName>/` with the standard PlatformIO layout
   (`platformio.ini`, `include/`, `src/`).
2. Point it at this folder:

   ```ini
   lib_extra_dirs = ../FirmwareLibs
   build_flags =
   	-iquote include
   ```

3. Copy `include/config.h` from the template and delete the macros the new
   device does not need. Every macro the shared libraries reference must be
   defined, otherwise the project will not compile.
4. Declare the external dependencies of whatever shared libraries you include:
   `bblanchon/ArduinoJson` for `Provisioning`, `adafruit/Adafruit NeoPixel` for
   `LedIndicators`.
5. If the device uses `Provisioning`, copy `data/jsqr.min.js.gz` and
   `data/nacl.min.js.gz` into the new project and embed them. The linker symbol
   names are derived from the path, so they must stay at `data/` inside the
   project:

   ```ini
   board_build.embed_files =
   	data/jsqr.min.js.gz
   	data/nacl.min.js.gz
   ```

6. Run CertGenerator once. It discovers every `platformio.ini` under the
   repository root and writes `ROOT_CA_CERT` into each project's
   `include/secrets.h`, creating the file when it is missing.

## Board requirement

`SecretStore` derives its NVS encryption key from the HMAC peripheral and eFuse
key block 4. Those exist on the ESP32-S2, S3 and C3, but not on the original
ESP32. A `SOC_HMAC_SUPPORTED` guard compiles the encryption out on a classic
`esp32dev` board, which then stores credentials in plain text: anyone who can
read the flash over the serial port recovers the Wi-Fi password and the device
private key. Prefer an S3 unless you are reusing existing hardware.

## Macros the shared libraries expect from `include/config.h`

`DEBUG_ON`, `DEBUG_PRINT`, `NVS_NAMESPACE`, `DOMAIN_TAG`, `ServerHttpsPort`, the
`PROVISION_AP_*` group, the `DATA_CHANNEL_*` group, `RGB_LED_*`,
`STATUS_LED_*`, and `BUZZER_*`.

`DATA_CHANNEL_DOMAIN_TAG` and `DATA_CHANNEL_HKDF_INFO` are part of the handshake
and must match the server. Do not change their values per device.
