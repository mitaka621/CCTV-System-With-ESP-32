window.hlsStreams = window.hlsStreams || new Map();

window.initHlsStream = (videoId, playlistContent, startPercentage = 0) => {
  const video = document.getElementById(videoId);
  if (!video) {
    return;
  }

  window.destroyHlsStream(videoId);

  const origin = window.location.origin;
  const normalizedPlaylist = playlistContent
    .split("\n")
    .map((line) => {
      const trimmed = line.trim();
      if (
        trimmed.length > 0 &&
        !trimmed.startsWith("#") &&
        !/^https?:\/\//i.test(trimmed)
      ) {
        return `${origin}${trimmed.startsWith("/") ? "" : "/"}${trimmed}`;
      }
      return line;
    })
    .join("\n");

  const manifestBlob = new Blob([normalizedPlaylist], {
    type: "application/vnd.apple.mpegurl",
  });
  const manifestUrl = URL.createObjectURL(manifestBlob);
  const stream = { video, hls: null, manifestUrl };
  window.hlsStreams.set(videoId, stream);

  const seekToStartPercentage = () => {
    if (startPercentage <= 0) {
      return;
    }

    try {
      if (video.seekable && video.seekable.length > 0) {
        const start = video.seekable.start(0);
        const end = video.seekable.end(0);
        const duration = end - start;
        video.currentTime =
          start +
          Math.max(0, Math.min(duration - 0.1, duration * (startPercentage / 100)));
        return;
      }

      if (Number.isFinite(video.duration) && video.duration > 0) {
        video.currentTime = Math.max(
          0,
          Math.min(
            video.duration - 0.1,
            video.duration * (startPercentage / 100),
          ),
        );
      }
    } catch (error) {
      console.warn("Error seeking HLS stream:", error);
    }
  };

  try {
    console.log("HLS normalized playlist:\n", normalizedPlaylist);
    const firstSegment = normalizedPlaylist
      .split("\n")
      .map((l) => l.trim())
      .find((l) => l && !l.startsWith("#"));
    if (firstSegment) {
      console.log("Probing first segment:", firstSegment);
      fetch(firstSegment, { method: "HEAD" })
        .then((r) => console.log("Probe status:", r.status))
        .catch((e) => console.warn("Probe error:", e));
    } else {
      console.warn("No segment URL found in playlist");
    }
  } catch (e) {
    console.warn("Playlist diagnostics failed:", e);
  }

  if (Hls.isSupported()) {
    const hls = new Hls({ lowLatencyMode: true, debug: true, maxBufferLength: 120 });
    stream.hls = hls;
    console.log("initHlsStream: attaching media", { videoId, manifestUrl });
    hls.loadSource(manifestUrl);
    hls.attachMedia(video);

    hls.on(Hls.Events.MANIFEST_PARSED, (_evt, data) => {
      console.log("HLS MANIFEST_PARSED", data);
      seekToStartPercentage();
      if (video.paused) {
        video.play().catch((e) => console.warn("Autoplay failed:", e));
      }
    });

    hls.on(Hls.Events.LEVEL_LOADED, (_evt, data) => {
      console.log("HLS LEVEL_LOADED", {
        details: data.details?.fragments?.length,
        targetduration: data.details?.targetduration,
      });
    });

    hls.on(Hls.Events.FRAG_LOADING, (_evt, data) => {
      console.log("HLS FRAG_LOADING", { url: data.frag?.url });
    });

    hls.on(Hls.Events.ERROR, (_event, data) => {
      console.error("HLS error:", data);
    });
  } else if (video.canPlayType("application/vnd.apple.mpegurl")) {
    video.src = manifestUrl;
    video.addEventListener(
      "loadedmetadata",
      () => {
        seekToStartPercentage();
        video.play().catch((e) => console.warn("Autoplay failed:", e));
      },
      { once: true },
    );
  }
};

window.destroyHlsStream = (videoId) => {
  const stream = window.hlsStreams.get(videoId);
  window.hlsStreams.delete(videoId);

  // the stored element is used because Blazor may already have removed it from the page
  const video = stream?.video ?? document.getElementById(videoId);

  if (stream?.hls) {
    stream.hls.destroy();
  }

  if (stream?.manifestUrl) {
    URL.revokeObjectURL(stream.manifestUrl);
  }

  if (!video) {
    return;
  }

  video.pause();
  video.removeAttribute("src");
  video.load();
};

window.destroyAllHlsStreams = () => {
  [...window.hlsStreams.keys()].forEach((videoId) => window.destroyHlsStream(videoId));
};

window.openFullScreen = async (videoId) => {
    const video = document.getElementById(videoId);
    video.controls = true;
    await video.requestFullscreen();   
};

document.addEventListener("fullscreenchange", () => {
    if (!document.fullscreenElement) {
        document.querySelectorAll("video").forEach(video => {
            video.controls = false;
        });
    }
});
