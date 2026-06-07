window.__cameraStreams = window.__cameraStreams || {};
window.__streamStopState = window.__streamStopState || {};
window.__STREAM_STOP_DEBOUNCE_MS = 500;

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

window.__stopCameraStreamNow = (elementId) => {
  // A transient dispose (drag re-render) leaves the <img> in the live DOM, so
  // the stream is still on screen — never abort it. Only a real teardown
  // (navigation away) detaches the element from the document.
  const liveImg = document.getElementById(elementId);
  if (liveImg && liveImg.isConnected) {
    console.log(`[camera-stream] stop skipped for ${elementId} (still in DOM)`);
    delete window.__streamStopState[elementId];
    return;
  }

  const img = window.__cameraStreams[elementId];
  if (img) {
    img.src = "";
    img.removeAttribute("src");
  }
  delete window.__cameraStreams[elementId];
  delete window.__streamStopState[elementId];
};

window.__cancelPendingStreamStop = (elementId) => {
  const state = window.__streamStopState[elementId];
  if (state && state.timer) {
    clearTimeout(state.timer);
    state.timer = null;
  }
};

// Debounced stop: a stop only fires when this request is isolated from the
// previous one (>= __STREAM_STOP_DEBOUNCE_MS apart) AND no new request arrives
// within __STREAM_STOP_DEBOUNCE_MS afterwards. Tight bursts of dispose/recreate
// (drag re-renders) never reach the activation, so the stream is left alone.
window.scheduleCameraStreamStop = (elementId) => {
  const now = Date.now();
  const state = window.__streamStopState[elementId] || { lastRequest: 0, timer: null };

  if (state.timer) {
    clearTimeout(state.timer);
    state.timer = null;
  }

  const isolatedFromPrevious = now - state.lastRequest >= window.__STREAM_STOP_DEBOUNCE_MS;

  console.log(
    `[camera-stream] stop request for ${elementId} — ${isolatedFromPrevious ? "armed (waiting for silence)" : "suppressed (part of a burst)"}`
  );

  if (isolatedFromPrevious) {
    state.timer = setTimeout(() => {
      console.log(`[camera-stream] stop activated for ${elementId}`);
      window.__stopCameraStreamNow(elementId);
    }, window.__STREAM_STOP_DEBOUNCE_MS);
  }

  state.lastRequest = now;
  window.__streamStopState[elementId] = state;
};

window.startCameraStream = (elementId, url) => {
  window.__cancelPendingStreamStop(elementId);

  const apply = () => {
    const img = document.getElementById(elementId);
    if (!img) {
      return;
    }

    window.__cameraStreams[elementId] = img;

    if (img.src && img.src.endsWith(url)) {
      return;
    }

    const container = img.parentElement;
    const loading = container
      ? container.querySelector(".camera-loading, .fullscreen-loading")
      : null;

    const hideLoading = () => {
      if (loading) {
        loading.style.display = "none";
      }
    };

    img.addEventListener("load", hideLoading, { once: true });
    img.addEventListener("error", hideLoading, { once: true });

    img.src = url;
  };

  if (document.readyState === "complete") {
    setTimeout(apply, 0);
  } else {
    window.addEventListener("load", () => setTimeout(apply, 0), { once: true });
  }
};

(() => {
  const stopAll = () => window.stopCameraStreams(".camera-stream, .fullscreen-stream");
  window.addEventListener("pagehide", stopAll);
  window.addEventListener("beforeunload", stopAll);
})();

window.applyCameraBoardSizing = () => {
  const boards = document.querySelectorAll(".camera-board");
  boards.forEach((board) => {
    const cols = parseInt(getComputedStyle(board).getPropertyValue("--cols"), 10) || 4;
    const gap = 16;
    const width = board.clientWidth;
    const cellWidth = (width - gap * (cols - 1)) / cols;
    if (cellWidth <= 0) {
      return;
    }

    let chrome = 88;
    const sampleCard = board.querySelector(".camera-card");
    if (sampleCard) {
      const header = sampleCard.querySelector(".mud-card-header");
      const actions = sampleCard.querySelector(".mud-card-actions");
      const measuredChrome = (header?.offsetHeight || 0) + (actions?.offsetHeight || 0);
      if (measuredChrome > 0) {
        chrome = measuredChrome;
      }
    }

    const feedHeight = (cellWidth * 3) / 4;
    const cellHeight = feedHeight + chrome;

    board.style.setProperty("--grid-gap", `${gap}px`);
    board.style.setProperty("--cell-w", `${Math.round(cellWidth)}px`);
    board.style.setProperty("--feed-h", `${Math.round(feedHeight)}px`);
    board.style.setProperty("--card-chrome", `${Math.round(chrome)}px`);
    board.style.setProperty("--cell-h", `${Math.round(cellHeight)}px`);
  });
};

window.initCameraBoardSizing = () => {
  const board = document.querySelector(".camera-board");
  if (!board) {
    return;
  }

  window.applyCameraBoardSizing();

  requestAnimationFrame(() => window.applyCameraBoardSizing());

  if (window.__cameraBoardResizeObserver) {
    window.__cameraBoardResizeObserver.disconnect();
  }

  const observer = new ResizeObserver(() => window.applyCameraBoardSizing());
  observer.observe(board);
  window.__cameraBoardResizeObserver = observer;
};

window.initCameraDragHandles = () => {
  if (window.__cameraDragHandlesInitialized) {
    return;
  }
  window.__cameraDragHandlesInitialized = true;

  const edgeZone = 220;
  const maxScrollStep = 32;

  let dragAllowed = false;
  let isCameraBoardDragging = false;
  let autoScrollDelta = 0;
  let autoScrollRaf = null;
  let dragScrollBase = 0;
  let dragScrollVirtual = 0;
  let dragScrollContent = null;

  const log = (message, detail) => {
    if (detail !== undefined) {
      console.log(`[camera-drag-scroll] ${message}`, detail);
    } else {
      console.log(`[camera-drag-scroll] ${message}`);
    }
  };

  const maxPageScroll = () =>
    Math.max(0, document.documentElement.scrollHeight - window.innerHeight);

  const getDragScrollContent = () =>
    document.querySelector(".mud-main-content.main-content") ||
    document.querySelector(".mud-main-content") ||
    document.querySelector(".main-content");

  const clearVirtualScrollTransform = () => {
    if (!dragScrollContent) {
      return;
    }

    dragScrollContent.style.transform = "";
    dragScrollContent.style.willChange = "";
  };

  const renderVirtualScroll = () => {
    if (!dragScrollContent || dragScrollVirtual === 0) {
      clearVirtualScrollTransform();
      return;
    }

    dragScrollContent.style.transform = `translateY(${-dragScrollVirtual}px)`;
    dragScrollContent.style.willChange = "transform";
  };

  const beginDragScroll = () => {
    dragScrollBase = window.scrollY;
    dragScrollVirtual = 0;
    dragScrollContent = getDragScrollContent();
    clearVirtualScrollTransform();
  };

  const commitDragScroll = () => {
    const targetScroll = dragScrollBase + dragScrollVirtual;

    if (dragScrollVirtual !== 0) {
      window.scrollTo({
        top: targetScroll,
        left: window.scrollX,
        behavior: "instant"
      });
    }

    clearVirtualScrollTransform();

    dragScrollBase = 0;
    dragScrollVirtual = 0;
    dragScrollContent = null;
  };

  const applyScrollDelta = (deltaY) => {
    if (!deltaY) {
      return;
    }

    const beforeWindow = window.scrollY;
    window.scrollTo({
      top: beforeWindow + deltaY,
      left: window.scrollX,
      behavior: "instant"
    });

    if (window.scrollY !== beforeWindow) {
      dragScrollBase = window.scrollY;
      dragScrollVirtual = 0;
      clearVirtualScrollTransform();
      return;
    }

    const maxScroll = maxPageScroll();
    let nextVirtual = dragScrollVirtual + deltaY;
    let nextAbsolute = dragScrollBase + nextVirtual;

    if (nextAbsolute < 0) {
      nextVirtual = -dragScrollBase;
    } else if (nextAbsolute > maxScroll) {
      nextVirtual = maxScroll - dragScrollBase;
    }

    dragScrollVirtual = nextVirtual;
    renderVirtualScroll();
  };

  const stopAutoScroll = () => {
    autoScrollDelta = 0;
    if (autoScrollRaf) {
      cancelAnimationFrame(autoScrollRaf);
      autoScrollRaf = null;
    }
  };

  const autoScrollTick = () => {
    autoScrollRaf = null;

    if (!isCameraBoardDragging || autoScrollDelta === 0) {
      return;
    }

    applyScrollDelta(autoScrollDelta);
    autoScrollRaf = requestAnimationFrame(autoScrollTick);
  };

  const queueAutoScroll = () => {
    if (!autoScrollRaf) {
      autoScrollRaf = requestAnimationFrame(autoScrollTick);
    }
  };

  const updateAutoScrollFromY = (clientY) => {
    if (clientY < edgeZone) {
      autoScrollDelta = -Math.min(maxScrollStep, Math.max(8, Math.ceil((edgeZone - clientY) / 4)));
    } else if (clientY > window.innerHeight - edgeZone) {
      autoScrollDelta = Math.min(maxScrollStep, Math.max(8, Math.ceil((clientY - (window.innerHeight - edgeZone)) / 4)));
    } else {
      autoScrollDelta = 0;
    }

    if (autoScrollDelta !== 0) {
      queueAutoScroll();
    }
  };

  const endDrag = (source) => {
    if (!isCameraBoardDragging) {
      return;
    }

    isCameraBoardDragging = false;
    stopAutoScroll();
    commitDragScroll();
    log("drag ended", { source });
  };

  const updateDragAllowed = (target) => {
    dragAllowed = !!(target && target.closest && target.closest(".camera-drag-handle"));
  };

  document.addEventListener("mousedown", (e) => updateDragAllowed(e.target), true);
  document.addEventListener("touchstart", (e) => updateDragAllowed(e.target), true);

  document.addEventListener(
    "dragstart",
    (e) => {
      const item = e.target.closest && e.target.closest(".mud-drop-item");
      const onBoard = !!(item && item.closest(".camera-board"));

      if (item && !dragAllowed) {
        e.preventDefault();
        return;
      }

      if (onBoard && dragAllowed) {
        isCameraBoardDragging = true;
        beginDragScroll();
        log("drag started", {
          windowScrollY: window.scrollY,
          maxScroll: maxPageScroll(),
          virtualScroll: true
        });
      }
    },
    true
  );

  document.addEventListener("dragend", () => endDrag("dragend"), true);
  document.addEventListener("drop", () => endDrag("drop"), true);
  document.addEventListener("mouseup", () => endDrag("mouseup"), true);

  document.addEventListener("dragover", (e) => {
    if (!isCameraBoardDragging) {
      return;
    }

    e.preventDefault();
    updateAutoScrollFromY(e.clientY);
  }, true);
};

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
