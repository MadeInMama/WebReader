{
    let enterFullscreenBtn,
        exitFullscreenBtn,
        fullscreenContainer,
        headerElement,
        mainElement,
        parentElement,
        eventOutputElement,
        footerElement,
        headerDisplayValue,
        footerDisplayValue,
        isFullscreen = false;

    execOnFullscreenEnter = () => {
        headerElement.style.display = "none";
        footerElement.style.display = "none";

        enterFullscreenBtn.style.display = "none";
        exitFullscreenBtn.style.display = "block";

        mainElement.appendChild(fullscreenContainer);

        fullscreenContainer.classList.add("fullscreen-container-on");
        eventOutputElement.classList.add("event-output-fullscreen-on");

        isFullscreen = true;
    }

    execOnFullscreenExit = () => {
        headerElement.style.display = headerDisplayValue;
        footerElement.style.display = footerDisplayValue;

        exitFullscreenBtn.style.display = "none";
        enterFullscreenBtn.style.display = "block";

        parentElement.appendChild(fullscreenContainer);

        fullscreenContainer.classList.remove("fullscreen-container-on");
        eventOutputElement.classList.remove("event-output-fullscreen-on");

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

        mainElement = document.querySelector("main");

        fullscreenContainer = document.querySelector("#fullscreen-container");
        enterFullscreenBtn = document.querySelector("#enter-fullscreen-btn");
        exitFullscreenBtn = document.querySelector("#exit-fullscreen-btn");

        parentElement = fullscreenContainer.parentElement;

        eventOutputElement = document.querySelector(".event-output")

        enterFullscreenBtn.onclick = () => changeFullscreenMode();
        exitFullscreenBtn.onclick = () => changeFullscreenMode();

        execOnFullscreenExit();
    }
}
