window.NavigationManager = {
  currentState: null,
  checkInterval: null,

  init: function () {
    this.startChecking();
  },

  startChecking: function () {
    if (this.checkInterval) {
      clearInterval(this.checkInterval);
    }

    this.checkInterval = setInterval(() => {
      this.checkDrawerState();
    }, 50);

    setTimeout(() => this.checkDrawerState(), 100);
  },

  checkDrawerState: function () {
    const drawer = document.querySelector(".mud-drawer");
    if (!drawer) {
      return;
    }

    const drawerWidth = drawer.offsetWidth;
    const isMiniMode = drawerWidth < 100;

    if (this.currentState !== isMiniMode) {
      if (isMiniMode) {
        this.hideAllText();
      } else {
        this.showAllText();
      }

      this.currentState = isMiniMode;
    }
  },

  hideAllText: function () {
    const textElements = [
      "header-text",
      "text-dashboard",
      "text-cameras",
      "text-recordings",
      "text-analytics",
      "text-system",
      "text-logs",
      "text-status",
      "text-settings",
      "version-text",
    ];

    document.querySelector("button.mud-nav-link").style.flexDirection =
      "column";

    textElements.forEach((id) => {
      const element = document.getElementById(id);
      if (element) {
        element.style.display = "none";
      } else {
      }
    });
  },

  showAllText: function () {
    const textElements = [
      "header-text",
      "text-dashboard",
      "text-cameras",
      "text-recordings",
      "text-analytics",
      "text-system",
      "text-logs",
      "text-status",
      "text-settings",
      "version-text",
    ];

    document.querySelector("button.mud-nav-link").style.flexDirection = "row";

    textElements.forEach((id) => {
      const element = document.getElementById(id);
      if (element) {
        element.style.display = "";
      }
    });
  },

  destroy: function () {
    if (this.checkInterval) {
      clearInterval(this.checkInterval);
      this.checkInterval = null;
    }
  },
};

NavigationManager.init();

window.addEventListener("load", () => {
  setTimeout(() => {
    NavigationManager.init();
  }, 100);
});
