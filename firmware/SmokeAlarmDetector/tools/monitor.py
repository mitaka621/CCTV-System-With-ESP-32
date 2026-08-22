import sys
import time
import serial
from serial.tools import list_ports

ESPRESSIF_VENDOR_ID = "303A"
BAUD = 115200

#used to monitor the smoke alarm even after deep sleep and then wakeup, since the default platform io monitor loses the device after deep sleep is activated

def Monitor():
    port = None
    while True:
        if port is None:
            candidates = [p.device for p in list_ports.comports()
                          if ESPRESSIF_VENDOR_ID in (p.hwid or "").upper()]
            if not candidates:
                time.sleep(0.2)
                continue
            port = candidates[0]
            try:
                link = serial.Serial(port, BAUD, timeout=0.2)
                link.dtr = True
                link.rts = False
                sys.stdout.write("\n--- attached to %s ---\n" % port)
                sys.stdout.flush()
            except Exception:
                port = None
                time.sleep(0.2)
                continue

        try:
            data = link.read(4096)
            if data:
                sys.stdout.write(data.decode("utf-8", "replace"))
                sys.stdout.flush()
        except Exception:
            sys.stdout.write("\n--- %s went away, waiting ---\n" % port)
            sys.stdout.flush()
            try:
                link.close()
            except Exception:
                pass
            port = None
            time.sleep(0.2)


if __name__ == "__main__":
    try:
        Monitor()
    except KeyboardInterrupt:
        pass
