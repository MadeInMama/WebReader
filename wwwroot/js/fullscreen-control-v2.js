{
    let fullscreenContainers,
        enterFullscreenBtns,
        exitFullscreenBtns,
        headerElement,
        footerElement,
        headerDisplayValue,
        footerDisplayValue,
        isFullscreen = false;

    execOnFullscreenEnter = () => {
        headerElement.style.display = "none";
        footerElement.style.display = "none";

        Array.from(fullscreenContainers).forEach(el => el.classList.add("fullscreen-on"));

        isFullscreen = true;
    }

    execOnFullscreenExit = () => {
        headerElement.style.display = headerDisplayValue;
        footerElement.style.display = footerDisplayValue;

        Array.from(fullscreenContainers).forEach(el => el.classList.remove("fullscreen-on"));

        isFullscreen = false;
    }

    function changeFullscreenModeCustom(pIsFullscreen) {
        if (pIsFullscreen) {
            execOnFullscreenExit();
        } else {
            execOnFullscreenEnter();
        }
    }

    function changeFullscreenMode() {
        changeFullscreenModeCustom(isFullscreen);
    }

    window.addEventListener("load", () => initFullscreenControl());

    function initFullscreenControl() {
        headerElement = document.querySelector("header");
        headerDisplayValue = headerElement.style.display;

        footerElement = document.querySelector("footer");
        footerDisplayValue = headerElement.style.display;

        fullscreenContainers = document.querySelectorAll(".fullscreen-container");
        enterFullscreenBtns = document.querySelectorAll(".enter-fullscreen-btn");
        exitFullscreenBtns = document.querySelectorAll(".exit-fullscreen-btn");

        Array.from(enterFullscreenBtns).forEach(el => el.onclick = () => changeFullscreenMode());
        Array.from(exitFullscreenBtns).forEach(el => el.onclick = () => changeFullscreenMode());

        execOnFullscreenExit();
    }
}
