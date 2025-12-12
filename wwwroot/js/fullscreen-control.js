isFullscreenNow = () => !!document.fullscreenElement;

let enterFullscreenBtn,
    exitFullscreenBtn,
    fullscreenContainer,
    headerElement,
    headerDisplayValue,
    isFullscreen = isFullscreenNow();

checkFullscreenSupport = () => {
    if (("ontouchstart" in document.documentElement)) {
        fullscreenContainer.style.display = "none";
        return false;
    }
    return true;
}

execOnFullscreenEnter = () => {
    headerElement.style.display = "none";
    enterFullscreenBtn.style.display = "none";
    exitFullscreenBtn.style.display = "block";
    isFullscreen = true;
}

execOnFullscreenExit = () => {
    exitFullscreenBtn.style.display = "none";
    enterFullscreenBtn.style.display = "block";
    headerElement.style.display = headerDisplayValue;
    isFullscreen = false;
}

enterFullscreenMode = () => {
    document.documentElement.requestFullscreen().then(_ => {
        execOnFullscreenEnter();
    });
}

exitFullscreenMode = () => {
    document.exitFullscreen().then(_ => {
        execOnFullscreenExit();
    });
}

function changeFullscreenModeCustom(pIsFullscreen) {
    if (pIsFullscreen) {
        exitFullscreenMode();
    } else {
        enterFullscreenMode();
    }
}

function changeFullscreenMode() {
    changeFullscreenModeCustom(isFullscreen);
}

window.addEventListener("load", () => initFullscreenControl());
document.addEventListener("fullscreenchange", () => {
    if (isFullscreenNow() !== isFullscreen) {
        isFullscreenNow() ? execOnFullscreenEnter() : execOnFullscreenExit();
    }
});

function initFullscreenControl() {
    headerElement = document.querySelector("header");
    headerDisplayValue = headerElement.style.display;
    fullscreenContainer = document.querySelector("#fullscreen-container");
    enterFullscreenBtn = document.querySelector("#enter-fullscreen-btn");
    exitFullscreenBtn = document.querySelector("#exit-fullscreen-btn");

    enterFullscreenBtn.onclick = () => changeFullscreenMode();
    exitFullscreenBtn.onclick = () => changeFullscreenMode();

    if (checkFullscreenSupport()) {
        isFullscreen ? execOnFullscreenEnter() : execOnFullscreenExit();
    }
}
