window.addEventListener("load", async () => {
    if (await fileCacheService.has(fileId)) {
        prepareFile(await fileCacheService.get(fileId));
    } else {
        requestQueue.enqueue({
            method: 'POST',
            url: decodedString,
            data: {},
            options: {
                responseType: 'arraybuffer',
                headers: {
                    'RequestVerificationToken': GetAntiForgeryToken()
                },
                withCredentials: true
            },
            beforeSend: () => {
                customSendCurrPageEvent.detail.message = `<div class="progress-bar"></div>
                                                  <div class="progress-bar-inner progress-bar-inner-linear" style="--progress-multiplier: 46;--progress: 0"></div>
                                                  <div class="progress-bar-inner progress-bar-inner-text" style="visibility: hidden;">0</div>`;
                customSendCurrPageEvent.detail.type = EventTypes.INFINITE;
                targetElement.dispatchEvent(customSendCurrPageEvent);
            },
            setProgress: (val) => {
                Array.from(document.getElementsByClassName("progress-bar-inner")).forEach(el => {
                    if (el.classList.contains('progress-bar-inner-text')) {
                        el.innerHTML = val;
                    } else if (el.classList.contains('progress-bar-inner-linear')) {
                        el.style.setProperty('--progress', val);
                    }
                });
            }
        })
            .then(response => {
                prepareFile(response);
                customSendCurrPageEvent.detail.message = `<div class="success"></div>`;
                customSendCurrPageEvent.detail.type = EventTypes.FROM_MILLIS;
                customSendCurrPageEvent.detail.millis = 3000;
                targetElement.dispatchEvent(customSendCurrPageEvent);
            })
            .catch(error => {
                customSendCurrPageEvent.detail.message = `<div class="error"></div>`;
                customSendCurrPageEvent.detail.type = EventTypes.INFINITE;
                targetElement.dispatchEvent(customSendCurrPageEvent);
                console.error(error);
            });
    }
});

Array.from(document.getElementsByClassName("prev-page-controller")).forEach(value => {
    value.onclick = () => {
        if (passScrollState.wasDrag) return;
        SetPage(currentPage - 1);

        Array.from(document.getElementsByClassName("next-page-controller")).forEach(value => {
            value.disabled = false;
        });
    };
});

Array.from(document.getElementsByClassName("next-page-controller")).forEach(value => {
    value.onclick = () => {
        if (passScrollState.wasDrag) return;
        SetPage(currentPage + 1);

        Array.from(document.getElementsByClassName("prev-page-controller")).forEach(value => {
            value.disabled = false;
        });
    };
});

Array.from(document.getElementsByClassName("set-scale")).forEach(value => {
    value.value = currentScale;

    value.onchange = (e) => {
        SetScale(e.target.value);
    };
});

Array.from(document.getElementsByClassName("preview-page")).forEach(value => {
    value.onchange = (e) => {
        SetPage(e.target.value);
    };
});

let hideHeaderTimeout;

function SetPage(page, send = true) {
    const scrollToTop = currentPage < page;

    currentPage = clamp(page, 1, maxPage);

    if (currentPage === maxPage) {
        Array.from(document.getElementsByClassName("prev-page-controller")).forEach(value => {
            value.disabled = false;
        });
        Array.from(document.getElementsByClassName("next-page-controller")).forEach(value => {
            value.disabled = true;
        });
    } else if (currentPage === 1) {
        Array.from(document.getElementsByClassName("prev-page-controller")).forEach(value => {
            value.disabled = true;
        });
        Array.from(document.getElementsByClassName("next-page-controller")).forEach(value => {
            value.disabled = false;
        });
    } else {
        Array.from(document.getElementsByClassName("prev-page-controller")).forEach(value => {
            value.disabled = false;
        });
        Array.from(document.getElementsByClassName("next-page-controller")).forEach(value => {
            value.disabled = false;
        });
    }

    Array.from(document.getElementsByClassName("preview-page")).forEach(value => {
        value.value = currentPage.toString();
        value.disabled = false;
    });

    renderPage(currentPage);

    SetScale(currentScale, false);

    mainElement.scrollTo({
        top: scrollToTop ? 0 : mainElement.scrollHeight,
        behavior: 'smooth'
    });

    if (send) SendCurrPage();
    else {
        hideHeaderTimeout = setTimeout(() => {
            document.querySelector("header.can-be-opened").classList.remove("opened");
        }, 5000);
    }

    if (currentPage === maxPage) {
        clearTimeout(hideHeaderTimeout);
        hideHeaderTimeout = null;
        document.querySelector("header.can-be-opened").classList.add("opened");
    }
}

function SendCurrPage() {
    requestQueue.enqueue({
        method: 'POST',
        url: '/api/FileApi/UpdateReading',
        data: {
            FileId: fileId,
            Page: currentPage,
            Scale: currentScale,
            IsLastPage: currentPage === maxPage,
        },
        options: {
            headers: {
                'RequestVerificationToken': GetAntiForgeryToken()
            },
            withCredentials: true
        },
        beforeSend: () => {
            customSendCurrPageEvent.detail.message = `<div class="waiting"></div>`;
            customSendCurrPageEvent.detail.type = EventTypes.INFINITE;
            targetElement.dispatchEvent(customSendCurrPageEvent);
        }
    })
        .then(_ => {
            customSendCurrPageEvent.detail.message = `<div class="success"></div>`;
            customSendCurrPageEvent.detail.type = EventTypes.FROM_MILLIS;
            customSendCurrPageEvent.detail.millis = 3000;
            targetElement.dispatchEvent(customSendCurrPageEvent);
        })
        .catch(_ => {
            customSendCurrPageEvent.detail.message = `<div class="error"></div>`;
            customSendCurrPageEvent.detail.type = EventTypes.INFINITE;
            targetElement.dispatchEvent(customSendCurrPageEvent);
        });
}

function clamp(value, min, max) {
    return Math.min(Math.max(value, min), max);
}
