{
    let fullscreenBtns,
        toolsContainer,
        setScaleContainer,
        previewPageContainer,
        headerElement,
        footerElement,
        headerDisplayValue,
        isFullscreen = false;

    execOnFullscreenEnter = () => {
        headerElement.style.display = "none";

        Array.from(fullscreenBtns).forEach(el => {
            if (el.classList.contains("fullscreen-btn-on")) {
                el.setAttribute('disabled', '');
            } else if (el.classList.contains("fullscreen-btn-off")) {
                el.removeAttribute('disabled');
            }
        });
        toolsContainer.classList.add("fullscreen-on");
        setScaleContainer.removeAttribute('disabled');
        previewPageContainer.removeAttribute('disabled');
        footerElement.setAttribute('disabled', '');

        isFullscreen = true;
    }

    execOnFullscreenExit = () => {
        headerElement.style.display = headerDisplayValue;

        Array.from(fullscreenBtns).forEach(el => {
            if (el.classList.contains("fullscreen-btn-on")) {
                el.removeAttribute('disabled');
            } else if (el.classList.contains("fullscreen-btn-off")) {
                el.setAttribute('disabled', '');
            }
        });
        toolsContainer.classList.remove("fullscreen-on");
        setScaleContainer.setAttribute('disabled', '');
        previewPageContainer.setAttribute('disabled', '');
        footerElement.removeAttribute('disabled');

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

        fullscreenBtns = document.querySelectorAll(".fullscreen-btn");
        toolsContainer = document.querySelector(".tools-container");
        setScaleContainer = document.querySelector(".tools-container .set-scale-wrapper");
        previewPageContainer = document.querySelector(".tools-container .preview-page-wrapper");

        Array.from(fullscreenBtns).forEach(el => el.onclick = () => changeFullscreenMode());
    }
}
