window.initHlsStream = (videoId, playlistContent) => {
  const video = document.getElementById(videoId);
  if (!video) {
    return;
  }

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
    const hls = new Hls({ lowLatencyMode: true, debug: true });
    console.log("initHlsStream: attaching media", { videoId, manifestUrl });
    hls.loadSource(manifestUrl);
    hls.attachMedia(video);

    hls.on(Hls.Events.MANIFEST_PARSED, (_evt, data) => {
      console.log("HLS MANIFEST_PARSED", data);
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
    // Safari native HLS support
    video.src = manifestUrl;
  }
};
