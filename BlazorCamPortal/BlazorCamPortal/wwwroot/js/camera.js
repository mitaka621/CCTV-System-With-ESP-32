window.stopCameraStreams = (selector) => {
  const images = document.querySelectorAll(selector);
  images.forEach((img) => {
    img.removeAttribute("src");
  });
};

window.stopCameraStreamById = (elementId) => {
  const img = document.getElementById(elementId);
  if (img) {
    img.removeAttribute("src");
  }
};

(() => {
  const stopAll = () => window.stopCameraStreams(".camera-stream, .fullscreen-stream");
  window.addEventListener("pagehide", stopAll);
  window.addEventListener("beforeunload", stopAll);
})();

window.forceDialogFullscreen = () => {
  setTimeout(() => {
    const dialogs = document.querySelectorAll(".mud-dialog-container");
    const dialog = document.querySelector(".mud-dialog");
    const overlay = document.querySelector(".mud-overlay");

    dialogs.forEach((dialogContainer) => {
      if (dialogContainer.querySelector(".fullscreen-camera")) {
        dialogContainer.style.cssText = `
                    padding: 0 !important;
                    margin: 0 !important;
                    width: 100vw !important;
                    height: 100vh !important;
                    max-width: 100vw !important;
                    max-height: 100vh !important;
                    position: fixed !important;
                    top: 0 !important;
                    left: 0 !important;
                    z-index: 9999 !important;
                `;
      }
    });

    if (dialog && dialog.querySelector(".fullscreen-camera")) {
      dialog.style.cssText = `
                padding: 0 !important;
                margin: 0 !important;
                width: 100vw !important;
                height: 100vh !important;
                max-width: 100vw !important;
                max-height: 100vh !important;
                border-radius: 0 !important;
                box-shadow: none !important;
            `;
    }

    if (overlay) {
      overlay.style.cssText = `
                background: rgba(0,0,0,1) !important;
                padding: 0 !important;
                margin: 0 !important;
            `;
    }

    const dialogContent = document.querySelector(".mud-dialog-content");
    if (dialogContent && dialogContent.querySelector(".fullscreen-camera")) {
      dialogContent.style.cssText = `
                padding: 0 !important;
                margin: 0 !important;
                width: 100vw !important;
                height: 100vh !important;
                overflow: hidden !important;
            `;
    }
  }, 50);
};
