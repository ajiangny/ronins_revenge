// The browser takes Esc to leave fullscreen before the game sees it. Where the Keyboard Lock API exists (Chromium),
// locking Escape while fullscreen hands a tap of it to the game; holding it still leaves fullscreen.
mergeInto(LibraryManager.library, {
  LockEscapeInFullscreen: function () {
    if (window.roninEscapeLock) return;
    window.roninEscapeLock = true;
    var update = function () {
      var keyboard = navigator.keyboard;
      if (!keyboard || !keyboard.lock) return;
      if (document.fullscreenElement) keyboard.lock(["Escape"]).catch(function () {});
      else keyboard.unlock();
    };
    document.addEventListener("fullscreenchange", update);
    update();
  }
});
