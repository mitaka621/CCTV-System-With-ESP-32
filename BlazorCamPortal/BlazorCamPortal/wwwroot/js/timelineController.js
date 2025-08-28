// Timeline Controller for Video Replay System
// Handles interactive timeline functionality with drag/drop, sync, and video integration

class TimelineController {
  constructor() {
    this.isDragging = false;
    this.currentDraggingTimeline = null;
    this.timelines = new Map();
    this.syncEnabled = true;

    // Bind methods to maintain context
    this.handleMouseMove = this.handleMouseMove.bind(this);
    this.handleMouseUp = this.handleMouseUp.bind(this);
  }

  // Initialize a timeline for a specific camera
  initTimeline(cameraId) {
    const timelineContainer = document.getElementById(`timeline-${cameraId}`);
    const cursor = document.getElementById(`cursor-${cameraId}`);
    const video = document.getElementById(`video-${cameraId}`);

    if (!timelineContainer || !cursor) {
      console.warn(`Timeline elements not found for camera ${cameraId}`);
      return;
    }

    // Store timeline reference
    this.timelines.set(cameraId, {
      container: timelineContainer,
      cursor: cursor,
      track: timelineContainer.querySelector(".timeline-track"),
      video: video,
    });

    // Add click handler to timeline track for direct positioning
    const track = timelineContainer.querySelector(".timeline-track");
    if (track) {
      track.addEventListener("click", (e) =>
        this.handleTimelineClick(cameraId, e)
      );
    }

    // Add video timeupdate listener for automatic cursor movement
    if (video) {
      video.addEventListener("timeupdate", () => {
        this.updateCursorFromVideo(cameraId);
      });

      // Also listen for play/pause events
      video.addEventListener("play", () => {
        console.log(`Video ${cameraId} started playing`);
      });

      video.addEventListener("pause", () => {
        console.log(`Video ${cameraId} paused`);
      });
    }
  }

  // Update cursor position based on video current time
  updateCursorFromVideo(cameraId) {
    if (this.isDragging && this.currentDraggingTimeline === cameraId) {
      return; // Don't auto-update if user is dragging
    }

    const timeline = this.timelines.get(cameraId);
    if (!timeline || !timeline.video) return;

    const video = timeline.video;

    if (video.seekable && video.seekable.length > 0) {
      const startTime = video.seekable.start(0);
      const endTime = video.seekable.end(0);
      const videoDuration = endTime - startTime;
      const currentRelativeTime = video.currentTime - startTime;

      // Calculate percentage based on video progress
      const percentage = (currentRelativeTime / videoDuration) * 100;
      const clampedPercentage = Math.max(0, Math.min(100, percentage));

      // Update cursor position
      this.updateCursorPosition(cameraId, clampedPercentage);

      // Update current time display with proper datetime
      if (window.blazorComponentInstance) {
        window.blazorComponentInstance
          .invokeMethodAsync(
            "GetFormattedTimeForPercentage",
            cameraId,
            clampedPercentage
          )
          .then((formattedTime) => {
            this.updateCurrentTimeDisplay(cameraId, formattedTime);
          });
      }

      // Sync other timelines if enabled
      if (this.syncEnabled) {
        this.syncOtherTimelines(cameraId, clampedPercentage);
      }
    }
  }

  // Handle timeline track click for direct positioning
  handleTimelineClick(cameraId, event) {
    if (this.isDragging) return;

    const track = event.currentTarget;
    const rect = track.getBoundingClientRect();
    const percentage = ((event.clientX - rect.left) / rect.width) * 100;

    this.updateCursorPosition(cameraId, percentage);
    this.notifyPositionChange(cameraId, percentage);
  }

  // Start dragging operation
  startDrag(cameraId) {
    this.isDragging = true;
    this.currentDraggingTimeline = cameraId;

    // Add global mouse event listeners
    document.addEventListener("mousemove", this.handleMouseMove);
    document.addEventListener("mouseup", this.handleMouseUp);
    document.addEventListener("mouseleave", this.handleMouseUp);

    // Show bubble
    this.showBubble(cameraId);

    // Add dragging class for visual feedback
    const cursor = document.getElementById(`cursor-${cameraId}`);
    if (cursor) {
      cursor.classList.add("dragging");
    }
  }

  // Handle mouse move during drag
  handleMouseMove(event) {
    if (!this.isDragging || !this.currentDraggingTimeline) return;

    const timeline = this.timelines.get(this.currentDraggingTimeline);
    if (!timeline) return;

    const rect = timeline.track.getBoundingClientRect();
    let percentage = ((event.clientX - rect.left) / rect.width) * 100;

    // Clamp percentage to valid range
    percentage = Math.max(0, Math.min(100, percentage));

    this.updateCursorPosition(this.currentDraggingTimeline, percentage);
    this.updateBubblePosition(this.currentDraggingTimeline, event.clientX);
    this.notifyPositionChange(this.currentDraggingTimeline, percentage);

    // Sync other timelines if enabled
    if (this.syncEnabled) {
      this.syncOtherTimelines(this.currentDraggingTimeline, percentage);
    }

    event.preventDefault();
  }

  // Handle mouse up to end drag
  handleMouseUp() {
    if (!this.isDragging) return;

    const cameraId = this.currentDraggingTimeline;

    // Remove global listeners
    document.removeEventListener("mousemove", this.handleMouseMove);
    document.removeEventListener("mouseup", this.handleMouseUp);
    document.removeEventListener("mouseleave", this.handleMouseUp);

    // Hide bubble with delay
    setTimeout(() => this.hideBubble(cameraId), 1000);

    // Remove dragging class
    const cursor = document.getElementById(`cursor-${cameraId}`);
    if (cursor) {
      cursor.classList.remove("dragging");
    }

    // Notify drag end
    if (window.blazorComponentInstance) {
      window.blazorComponentInstance.invokeMethodAsync(
        "OnTimelineDragEnd",
        cameraId
      );
    }

    this.isDragging = false;
    this.currentDraggingTimeline = null;
  }

  // Update cursor position
  updateCursorPosition(cameraId, percentage) {
    const cursor = document.getElementById(`cursor-${cameraId}`);
    if (cursor) {
      cursor.style.left = `${percentage}%`;
    }
  }

  // Sync other timelines when sync is enabled
  syncOtherTimelines(sourceCameraId, percentage) {
    this.timelines.forEach((timeline, cameraId) => {
      if (cameraId !== sourceCameraId) {
        this.updateCursorPosition(cameraId, percentage);
      }
    });
  }

  // Show datetime bubble
  showBubble(cameraId) {
    const bubble = document.getElementById(`bubble-${cameraId}`);
    if (bubble) {
      bubble.style.display = "block";
    }
  }

  // Hide datetime bubble
  hideBubble(cameraId) {
    const bubble = document.getElementById(`bubble-${cameraId}`);
    if (bubble) {
      bubble.style.display = "none";
    }
  }

  // Update bubble position during drag
  updateBubblePosition(cameraId, clientX) {
    const bubble = document.getElementById(`bubble-${cameraId}`);
    const timeline = this.timelines.get(cameraId);

    if (bubble && timeline) {
      const rect = timeline.container.getBoundingClientRect();
      const relativeX = clientX - rect.left;
      const percentage = (relativeX / rect.width) * 100;

      // Keep bubble within timeline bounds
      const clampedPercentage = Math.max(5, Math.min(95, percentage));
      bubble.style.left = `${clampedPercentage}%`;
    }
  }

  // Update bubble text content
  updateBubbleText(cameraId, text) {
    const bubbleText = document.getElementById(`bubble-text-${cameraId}`);
    if (bubbleText) {
      bubbleText.textContent = text;
    }
  }

  // Update current time display
  updateCurrentTimeDisplay(cameraId, timeText) {
    const currentTimeDisplay = document.getElementById(
      `current-time-${cameraId}`
    );
    if (currentTimeDisplay) {
      currentTimeDisplay.textContent = timeText;
    }
  }

  // Notify Blazor component of position change
  notifyPositionChange(cameraId, percentage) {
    if (window.blazorComponentInstance) {
      window.blazorComponentInstance.invokeMethodAsync(
        "OnTimelinePositionChanged",
        cameraId,
        percentage
      );
    }
  }

  // Set sync mode
  setSyncMode(enabled) {
    this.syncEnabled = enabled;
  }
}

// Global timeline controller instance
window.timelineController = new TimelineController();

// Global functions for Blazor interop
window.addGlobalTimelineEvents = function (cameraId, syncEnabled) {
  window.timelineController.setSyncMode(syncEnabled);
  window.timelineController.startDrag(cameraId);
};

window.showTimelineBubble = function (cameraId) {
  window.timelineController.showBubble(cameraId);
};

window.hideTimelineBubble = function (cameraId) {
  window.timelineController.hideBubble(cameraId);
};

window.updateTimelineBubble = function (cameraId, text) {
  window.timelineController.updateBubbleText(cameraId, text);
};

window.updateTimelineCursor = function (cameraId, percentage) {
  window.timelineController.updateCursorPosition(cameraId, percentage);
};

window.updateCurrentTimeDisplay = function (cameraId, timeText) {
  window.timelineController.updateCurrentTimeDisplay(cameraId, timeText);
};

window.initTimelineForCamera = function (cameraId) {
  window.timelineController.initTimeline(cameraId);

  // Debug: Check if markers are present
  setTimeout(() => {
    const markers = document.querySelectorAll(
      `#timeline-${cameraId} .timeline-marker`
    );
    const labels = document.querySelectorAll(
      `#timeline-${cameraId} .marker-label`
    );
    const track = document.querySelector(
      `#timeline-${cameraId} .timeline-track`
    );
    const container = document.querySelector(`#timeline-${cameraId}`);

    console.log(`Timeline ${cameraId} initialized:`);
    console.log(`- Found ${markers.length} markers`);
    console.log(`- Found ${labels.length} labels`);
    console.log(
      `- Track dimensions:`,
      track ? track.getBoundingClientRect() : "not found"
    );
    console.log(
      `- Container dimensions:`,
      container ? container.getBoundingClientRect() : "not found"
    );

    if (markers.length > 0) {
      console.log(
        "First marker:",
        markers[0].style.cssText,
        markers[0].className,
        markers[0].dataset.minute
      );
    }

    if (labels.length > 0) {
      console.log(
        "First label:",
        labels[0].textContent,
        labels[0].style.cssText
      );
      console.log("Label computed style:", {
        position: window.getComputedStyle(labels[0]).position,
        bottom: window.getComputedStyle(labels[0]).bottom,
        left: window.getComputedStyle(labels[0]).left,
        visibility: window.getComputedStyle(labels[0]).visibility,
        opacity: window.getComputedStyle(labels[0]).opacity,
        zIndex: window.getComputedStyle(labels[0]).zIndex,
      });
    }
  }, 100);
};

window.setBlazorComponentInstance = function (objRef) {
  window.blazorComponentInstance = objRef;
};

window.seekVideoToTime = function (videoId, timeString) {
  const video = document.getElementById(videoId);
  if (video && video.currentTime !== undefined) {
    try {
      // Parse time string and convert to seconds
      const date = new Date(timeString);
      const seconds =
        date.getSeconds() + date.getMinutes() * 60 + date.getHours() * 3600;

      // For HLS videos, we might need to use a different approach
      if (video.seekable && video.seekable.length > 0) {
        const targetTime = Math.max(
          video.seekable.start(0),
          Math.min(video.seekable.end(0) - 1, seconds)
        );
        video.currentTime = targetTime;
      } else {
        video.currentTime = seconds;
      }
    } catch (error) {
      console.warn("Error seeking video:", error);
    }
  }
};

window.seekVideoToRelativeTime = function (videoId, relativeSeconds) {
  const video = document.getElementById(videoId);
  if (video && video.currentTime !== undefined) {
    try {
      // For HLS videos, seek relative to the start of the video
      if (video.seekable && video.seekable.length > 0) {
        const startTime = video.seekable.start(0);
        const endTime = video.seekable.end(0);
        const videoDuration = endTime - startTime;

        // Calculate target time as a proportion of the video duration
        const targetTime =
          startTime + Math.max(0, Math.min(videoDuration - 1, relativeSeconds));

        console.log(
          `Seeking video ${videoId} to: ${targetTime}s (relative: ${relativeSeconds}s, range: ${startTime}-${endTime})`
        );
        video.currentTime = targetTime;
      } else {
        // Fallback for non-HLS videos
        video.currentTime = Math.max(
          0,
          Math.min(video.duration - 1, relativeSeconds)
        );
      }
    } catch (error) {
      console.warn("Error seeking video:", error);
    }
  }
};

// Initialize timelines when DOM is ready
document.addEventListener("DOMContentLoaded", function () {
  // Auto-detect and initialize timelines
  const timelineContainers = document.querySelectorAll('[id^="timeline-"]');
  timelineContainers.forEach((container) => {
    const cameraId = container.id.replace("timeline-", "");
    window.timelineController.initTimeline(cameraId);
  });
});

// Enhanced CSS for dragging state
const style = document.createElement("style");
style.textContent = `
    .timeline-cursor.dragging {
        z-index: 25;
    }
    
    .timeline-cursor.dragging .cursor-handle {
        transform: translate(-50%, -50%) scale(1.3);
        box-shadow: 0 0 25px rgba(255, 255, 255, 1);
        border-color: rgba(119, 107, 231, 1);
    }
    
    .timeline-cursor.dragging .cursor-line {
        box-shadow: 
            0 0 12px rgba(255, 255, 255, 0.8),
            0 0 24px rgba(255, 255, 255, 0.4);
    }
    
    body.timeline-dragging {
        cursor: grabbing !important;
        user-select: none;
    }
`;
document.head.appendChild(style);
