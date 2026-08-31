# NewDeviceExample

A minimal, buildable device firmware that uses every shared library in
`../FirmwareLibs`. Copy this folder to start a new device.

It does the same pairing and connection dance as the camera, then sends an
8-byte reading once per second instead of JPEG frames:

```
soft-AP wizard -> save credentials to NVS -> Wi-Fi -> server pairing -> data channel
```

## Reading payload

| Offset | Size | Field |
| --- | --- | --- |
| 0 | 1 | payload version (`READING_PAYLOAD_VERSION`) |
| 1 | 4 | uptime in seconds, big endian |
| 5 | 2 | free heap in KB, big endian |
| 7 | 1 | flags, bit 0 = buzzer active |

Replace `buildReading` in [main.cpp](src/main.cpp) with whatever your device
measures. The channel does not care what the bytes mean.

## Sending

```cpp
data_channel::Send(_readingBuf, READING_PAYLOAD_LEN);
```

For a large payload with a header in front of it, use `SendSegments` instead so
the payload is not copied twice:

```cpp
data_channel::Segment segments[2] = {{header, headerLen}, {body, bodyLen}};
data_channel::SendSegments(segments, 2);
```

## Receiving

This example polls, which keeps everything on the main loop:

```cpp
while (data_channel::Receive(message, sizeof(message), messageLen)) { ... }
```

The alternative is `data_channel::SetMessageHandler(handler)`, which the camera
firmware uses. The handler runs on the channel's receiver task, so anything it
touches must be safe to touch from another task. Set a handler *or* poll, not
both: once a handler is installed nothing is queued for `Receive`.

Inbound messages carry `[version][code][payload]`, parsed in `receiveCommands`.
The command codes live in [device_command.h](include/device_command.h) and must
match what the server sends.

## Setup

1. Set `board` in [platformio.ini](platformio.ini). Prefer an ESP32-S3, S2 or C3:
   `SecretStore` derives its NVS encryption key from the HMAC peripheral and
   eFuse key block 4, which the original ESP32 does not have, so a classic
   `esp32dev` board falls back to storing credentials in plain text.
2. Fix the pins in [include/config.h](include/config.h) for your hardware.
3. Create `include/secrets.h` by copying `include/secrets.example.h`, then run
   CertGenerator to write the real `ROOT_CA_CERT` into it. CertGenerator finds
   this project on its own, by scanning for `platformio.ini`.
4. `pio run -t upload -t monitor`

`DATA_CHANNEL_BUFFER_SIZE` is 4 KB here because the readings are tiny. The
channel allocates two buffers of that size, preferring PSRAM and falling back to
internal heap, so raise it only if your payloads actually need the room.
