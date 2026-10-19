# CCTV-System-With-ESP-32

## Linux Setup

1. Download the FFmpeg binary (it is stored in Git LFS):

```bash
git lfs pull
```

2. Install the VA-API libraries and the AMD GPU driver (FFmpeg loads them from the system):

```bash
sudo apt install libva2 libva-drm2 mesa-va-drivers vainfo
```

3. Give the user that runs the app access to the GPU, then log out and back in:

```bash
sudo usermod -aG render,video <app-user>
```

4. Check that the GPU can encode H.264. The output must list `VAProfileH264...` with `VAEntrypointEncSlice`:

```bash
vainfo --display drm --device /dev/dri/renderD128
```

5. Restrict access to the encryption keys of the saved SMB passwords (they are stored unencrypted on Linux):

```bash
chmod 700 <storage-root>/data/data-protection-keys
```
