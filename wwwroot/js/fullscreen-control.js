isFullscreenNow = () => !!document.fullscreenElement;
isTouchSupported = () => ("ontouchstart" in document.documentElement);

let enterFullscreenBtn, exitFullscreenBtn, fullscreenContainer, isFullscreen = isFullscreenNow();

checkFullscreenSupport = () => {
    if (isTouchSupported()) {
        fullscreenContainer.style.display = "none";
        return false;
    }
    return true;
}

execOnFullscreenEnter = () => {
    enterFullscreenBtn.style.display = "none";
    exitFullscreenBtn.style.display = "block";
}

execOnFullscreenExit = () => {
    exitFullscreenBtn.style.display = "none";
    enterFullscreenBtn.style.display = "block";
}

enterFullscreenMode = () => {
    document.documentElement.requestFullscreen().then(_ => {
        execOnFullscreenEnter();
        isFullscreen = true;
    });
}

exitFullscreenMode = () => {
    document.exitFullscreen().then(_ => {
        execOnFullscreenExit();
        isFullscreen = false;
    });
}

changeFullscreenMode = () => {
    if (isFullscreen) {
        exitFullscreenMode();
    } else {
        enterFullscreenMode();
    }
}

window.addEventListener("load", () => initFullscreenControl());

function initFullscreenControl() {
    fullscreenContainer = document.querySelector("#fullscreen-container");
    enterFullscreenBtn = document.querySelector("#enter-fullscreen-btn");
    exitFullscreenBtn = document.querySelector("#exit-fullscreen-btn");

    enterFullscreenBtn.onclick = () => changeFullscreenMode();
    exitFullscreenBtn.onclick = () => changeFullscreenMode();

    if (checkFullscreenSupport()) {
        isFullscreen ? execOnFullscreenEnter() : execOnFullscreenExit();
    }
}
