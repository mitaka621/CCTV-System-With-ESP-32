let timelines = new Map();

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

window.resetTimelines = function () {
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
    startTime: new Date(startTimeIso),
    durationSeconds,
    isDragging: false,
    isUpdating: false,
  };

  timelines.set(cameraId, state);

  slider.addEventListener("pointerdown", () => setScrubbing(cameraId, true));
  slider.addEventListener("input", () => onSliderInput(cameraId));
  slider.addEventListener("change", () => onSliderRelease(cameraId));
  slider.addEventListener("pointercancel", () => setScrubbing(cameraId, false));
  syncCheckbox?.addEventListener("change", () => onSyncCheckboxChange(cameraId));
  video.addEventListener("timeupdate", () => onVideoTimeUpdate(cameraId));
};
