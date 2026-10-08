let timelines = new Map();

const DEFAULT_PLAYBACK_RATE = 1;

function formatTimeFromPercentage(startTime, durationSeconds, percentage) {
  const targetMs =
    startTime.getTime() + durationSeconds * 1000 * (percentage / 100);
  const date = new Date(targetMs);
  const hours = date.getHours().toString().padStart(2, "0");
  const minutes = date.getMinutes().toString().padStart(2, "0");
  const seconds = date.getSeconds().toString().padStart(2, "0");
  return `${hours}:${minutes}:${seconds}`;
}

function getVideoPercentage(video) {
  if (video.seekable && video.seekable.length > 0) {
    const start = video.seekable.start(0);
    const end = video.seekable.end(0);
    const duration = end - start;
    if (duration <= 0) {
      return 0;
    }
    return ((video.currentTime - start) / duration) * 100;
  }

  if (Number.isFinite(video.duration) && video.duration > 0) {
    return (video.currentTime / video.duration) * 100;
  }

  return 0;
}

function seekVideoToRelativeTime(video, relativeSeconds) {
  if (!video) {
    return;
  }

  try {
    if (video.seekable && video.seekable.length > 0) {
      const start = video.seekable.start(0);
      const end = video.seekable.end(0);
      const duration = end - start;
      video.currentTime =
        start + Math.max(0, Math.min(duration - 0.1, relativeSeconds));
      return;
    }

    if (Number.isFinite(video.duration)) {
      video.currentTime = Math.max(
        0,
        Math.min(video.duration - 0.1, relativeSeconds),
      );
    }
  } catch (error) {
    console.warn("Error seeking video:", error);
  }
}

function setSliderValue(state, percentage) {
  state.isUpdating = true;
  state.slider.value = Math.max(0, Math.min(100, percentage));
  state.isUpdating = false;
}

function updateTimeDisplay(state, percentage) {
  if (!state.timeDisplay) {
    return;
  }

  state.timeDisplay.textContent = formatTimeFromPercentage(
    state.startTime,
    state.durationSeconds,
    percentage,
  );
}

function isSyncEnabled(state) {
  return state.syncCheckbox?.checked ?? false;
}

function setScrubbing(sourceCameraId, scrubbing) {
  const source = timelines.get(sourceCameraId);
  if (!source) {
    return;
  }

  source.isDragging = scrubbing;

  if (!isSyncEnabled(source)) {
    return;
  }

  timelines.forEach((target, cameraId) => {
    if (cameraId !== sourceCameraId && isSyncEnabled(target)) {
      target.isDragging = scrubbing;
    }
  });
}

function syncCursorUi(sourceCameraId, percentage) {
  const source = timelines.get(sourceCameraId);
  if (!source) {
    return;
  }

  updateTimeDisplay(source, percentage);

  if (!isSyncEnabled(source)) {
    return;
  }

  timelines.forEach((target, cameraId) => {
    if (cameraId === sourceCameraId || !isSyncEnabled(target)) {
      return;
    }

    setSliderValue(target, percentage);
    updateTimeDisplay(target, percentage);
  });
}

function seekVideos(sourceCameraId, percentage) {
  const source = timelines.get(sourceCameraId);
  if (!source) {
    return;
  }

  const seekState = (state) => {
    seekVideoToRelativeTime(
      state.video,
      state.durationSeconds * (percentage / 100),
    );
  };

  seekState(source);

  if (!isSyncEnabled(source)) {
    return;
  }

  timelines.forEach((target, cameraId) => {
    if (cameraId !== sourceCameraId && isSyncEnabled(target)) {
      seekState(target);
    }
  });
}

function onSyncCheckboxChange(cameraId) {
  const state = timelines.get(cameraId);
  if (!state || !isSyncEnabled(state)) {
    return;
  }

  const percentage = getVideoPercentage(state.video);
  setSliderValue(state, percentage);
  syncCursorUi(cameraId, percentage);
  seekVideos(cameraId, percentage);
  syncPlaybackState(cameraId);
}

function onSliderInput(cameraId) {
  const state = timelines.get(cameraId);
  if (!state || state.isUpdating) {
    return;
  }

  syncCursorUi(cameraId, parseFloat(state.slider.value));
}

function onSliderRelease(cameraId) {
  const state = timelines.get(cameraId);
  if (!state) {
    return;
  }

  seekVideos(cameraId, parseFloat(state.slider.value));
  setScrubbing(cameraId, false);
}

function onVideoTimeUpdate(cameraId) {
  const state = timelines.get(cameraId);
  if (!state || state.isUpdating || state.isDragging) {
    return;
  }

  const percentage = getVideoPercentage(state.video);
  setSliderValue(state, percentage);
  updateTimeDisplay(state, percentage);
}

function getLinkedStates(sourceCameraId) {
  const source = timelines.get(sourceCameraId);
  if (!source) {
    return [];
  }

  const linkedStates = [source];

  if (!isSyncEnabled(source)) {
    return linkedStates;
  }

  timelines.forEach((target, cameraId) => {
    if (cameraId !== sourceCameraId && isSyncEnabled(target)) {
      linkedStates.push(target);
    }
  });

  return linkedStates;
}

function getSliderPlaybackRate(slider) {
  return Math.pow(2, parseFloat(slider.value));
}

function setSpeedSliderValue(slider, rate) {
  if (slider) {
    slider.value = Math.log2(rate);
  }
}

function updatePlayPauseUi(state) {
  if (!state.playPauseButton) {
    return;
  }

  const isPlaying = state.isRewinding || !state.video.paused;
  state.playPauseButton.classList.toggle("is-playing", isPlaying);
  state.playPauseButton.setAttribute("aria-label", isPlaying ? "Pause" : "Play");
}

function playVideo(video) {
  if (video.paused && !video.ended) {
    video.play().catch((error) => console.warn("Play failed:", error));
  }
}

function getSeekableStart(video) {
  return video.seekable && video.seekable.length > 0 ? video.seekable.start(0) : 0;
}

function stepRewind(state, timestamp) {
  if (!state.isRewinding) {
    return;
  }

  const elapsedSeconds = Math.max(0, (timestamp - state.rewindLastTimestamp) / 1000);
  state.rewindLastTimestamp = timestamp;
  state.rewindTargetTime = Math.max(
    getSeekableStart(state.video),
    state.rewindTargetTime - elapsedSeconds * state.rewindRate,
  );

  // Seeking again before the previous seek finishes only restarts decoding, so the target keeps accumulating instead.
  if (!state.video.seeking) {
    state.video.currentTime = state.rewindTargetTime;
  }

  state.rewindFrameId = requestAnimationFrame((nextTimestamp) => stepRewind(state, nextTimestamp));
}

function startRewind(state, rate) {
  state.rewindRate = rate;

  if (state.isRewinding) {
    return;
  }

  state.isRewinding = true;
  state.video.playbackRate = DEFAULT_PLAYBACK_RATE;
  state.video.pause();
  state.rewindTargetTime = state.video.currentTime;
  state.rewindLastTimestamp = performance.now();
  state.rewindFrameId = requestAnimationFrame((timestamp) => stepRewind(state, timestamp));
  updatePlayPauseUi(state);
}

function stopRewind(state) {
  if (!state.isRewinding) {
    return;
  }

  state.isRewinding = false;
  cancelAnimationFrame(state.rewindFrameId);
  state.rewindFrameId = null;
  updatePlayPauseUi(state);
}

function applyForwardRate(sourceCameraId, rate) {
  getLinkedStates(sourceCameraId).forEach((state) => {
    stopRewind(state);
    state.video.playbackRate = rate;
    playVideo(state.video);
    setSpeedSliderValue(state.forwardSlider, rate);
  });
}

function applyRewindRate(sourceCameraId, rate) {
  getLinkedStates(sourceCameraId).forEach((state) => {
    startRewind(state, rate);
    setSpeedSliderValue(state.rewindSlider, rate);
  });
}

function resetPlaybackSpeed(sourceCameraId) {
  getLinkedStates(sourceCameraId).forEach((state) => {
    stopRewind(state);
    state.video.playbackRate = DEFAULT_PLAYBACK_RATE;
    playVideo(state.video);
    setSpeedSliderValue(state.forwardSlider, DEFAULT_PLAYBACK_RATE);
    setSpeedSliderValue(state.rewindSlider, DEFAULT_PLAYBACK_RATE);
  });
}

function setPlaying(state, shouldPlay) {
  stopRewind(state);

  if (shouldPlay) {
    playVideo(state.video);
  } else {
    state.video.pause();
  }

  updatePlayPauseUi(state);
}

function togglePlayback(sourceCameraId) {
  const source = timelines.get(sourceCameraId);
  if (!source) {
    return;
  }

  const shouldPlay = source.video.paused && !source.isRewinding;

  getLinkedStates(sourceCameraId).forEach((state) => setPlaying(state, shouldPlay));
}

function syncPlaybackState(sourceCameraId) {
  const source = timelines.get(sourceCameraId);
  if (!source) {
    return;
  }

  const shouldPlay = !source.video.paused;

  getLinkedStates(sourceCameraId).forEach((state) => {
    if (state === source) {
      return;
    }

    state.video.playbackRate = source.video.playbackRate;
    setPlaying(state, shouldPlay);
  });
}

function bindSpeedSlider(state, slider, applyRate, signal) {
  if (!slider) {
    return;
  }

  const cameraId = state.cameraId;
  const release = () => resetPlaybackSpeed(cameraId);

  slider.addEventListener(
    "pointerdown",
    () => {
      applyRate(cameraId, getSliderPlaybackRate(slider));
      window.addEventListener("pointerup", release, { once: true, signal });
      window.addEventListener("pointercancel", release, { once: true, signal });
    },
    { signal },
  );
  slider.addEventListener("input", () => applyRate(cameraId, getSliderPlaybackRate(slider)), { signal });
  slider.addEventListener("change", release, { signal });
}

window.resetTimelines = function () {
  timelines.forEach((state) => {
    stopRewind(state);
    state.listenersController.abort();
  });

  timelines.clear();
};

window.getReplayVideoPercentage = function (cameraId) {
  const state = timelines.get(cameraId);
  if (state?.video) {
    return getVideoPercentage(state.video);
  }

  const video = document.getElementById(`video-${cameraId}`);
  return video ? getVideoPercentage(video) : 0;
};

window.initTimelineForCamera = function (
  cameraId,
  startTimeIso,
  durationSeconds,
) {
  if (timelines.has(cameraId)) {
    return;
  }

  const slider = document.getElementById(`timeline-cursor-${cameraId}`);
  const video = document.getElementById(`video-${cameraId}`);
  const syncCheckbox = document.getElementById(
    `camera-sync-enabled-${cameraId}`,
  );
  const timeDisplay = document.getElementById(`current-time-${cameraId}`);

  if (!slider || !video) {
    console.warn(`Timeline elements not found for camera ${cameraId}`);
    return;
  }

  const state = {
    cameraId,
    slider,
    video,
    syncCheckbox,
    timeDisplay,
    playPauseButton: document.getElementById(`play-pause-${cameraId}`),
    forwardSlider: document.getElementById(`forward-slider-${cameraId}`),
    rewindSlider: document.getElementById(`rewind-slider-${cameraId}`),
    listenersController: new AbortController(),
    startTime: new Date(startTimeIso),
    durationSeconds,
    isDragging: false,
    isUpdating: false,
    isRewinding: false,
    rewindRate: DEFAULT_PLAYBACK_RATE,
    rewindTargetTime: 0,
    rewindLastTimestamp: 0,
    rewindFrameId: null,
  };

  timelines.set(cameraId, state);

  const signal = state.listenersController.signal;

  slider.addEventListener("pointerdown", () => setScrubbing(cameraId, true), { signal });
  slider.addEventListener("input", () => onSliderInput(cameraId), { signal });
  slider.addEventListener("change", () => onSliderRelease(cameraId), { signal });
  slider.addEventListener("pointercancel", () => setScrubbing(cameraId, false), { signal });
  syncCheckbox?.addEventListener("change", () => onSyncCheckboxChange(cameraId), { signal });
  video.addEventListener("timeupdate", () => onVideoTimeUpdate(cameraId), { signal });

  state.playPauseButton?.addEventListener("click", () => togglePlayback(cameraId), { signal });
  video.addEventListener("play", () => updatePlayPauseUi(state), { signal });
  video.addEventListener("pause", () => updatePlayPauseUi(state), { signal });
  bindSpeedSlider(state, state.forwardSlider, applyForwardRate, signal);
  bindSpeedSlider(state, state.rewindSlider, applyRewindRate, signal);
  setSpeedSliderValue(state.forwardSlider, DEFAULT_PLAYBACK_RATE);
  setSpeedSliderValue(state.rewindSlider, DEFAULT_PLAYBACK_RATE);
  updatePlayPauseUi(state);

  const container = video.closest(".video-container");
  let loaderTimeout = null;

  const setLoading = (isLoading) => {
    if (!container) {
      return;
    }

    if (isLoading) {
      if (loaderTimeout === null) {
        loaderTimeout = setTimeout(() => {
          container.classList.add("is-loading");
          loaderTimeout = null;
        }, 120);
      }
      return;
    }

    if (loaderTimeout !== null) {
      clearTimeout(loaderTimeout);
      loaderTimeout = null;
    }
    container.classList.remove("is-loading");
  };

  video.addEventListener(
    "seeking",
    () => {
      if (!state.isRewinding) {
        setLoading(true);
      }
    },
    { signal },
  );
  video.addEventListener("waiting", () => setLoading(true), { signal });
  video.addEventListener("seeked", () => setLoading(false), { signal });
  video.addEventListener("playing", () => setLoading(false), { signal });
  video.addEventListener("canplay", () => setLoading(false), { signal });
};
