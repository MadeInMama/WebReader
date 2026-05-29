const htmlElement = document.querySelector('html');
const mainElement = document.querySelector('main');
const header = document.querySelector('header');
const headerToggleBtn = document.querySelector('.header-toggle');
const footer = document.querySelector('footer');

if (footer.childNodes.length > 0 &&
    footer.innerHTML.replace(/\s+/g, '').length > 0) {
    footer.style.visibility = "visible";
} else {
    footer.style.visibility = "hidden";
}

function GetAntiForgeryToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]').value;
}

function getBrowserScrollbarWidth() {
    return window.innerWidth - document.documentElement.clientWidth;
}

document.querySelectorAll(".always-open-details").forEach((details) => {
    details.querySelector('summary').onclick = (e) => {
        if (details.open) {
            e.preventDefault();
            details.open = true;
        }
    };
});

function onNoScrollApplied() {
    const defWidth = getBrowserScrollbarWidth();
    const footerStyles = window.getComputedStyle(footer);
    footer.style.width = `${parseInt(footerStyles.width)}px`;
    footer.style.left = `${parseInt(footerStyles.left)}px`;
    htmlElement.style.paddingRight = `${defWidth + parseInt(window.getComputedStyle(htmlElement).paddingRight)}px`;
    htmlElement.classList.add('no-scroll');
    mainElement.classList.add('no-scroll');
    if (window.getComputedStyle(header).position === 'fixed') {
        header.style.paddingRight = `${defWidth + parseInt(window.getComputedStyle(header).paddingRight)}px`;
    }
    document.onscroll = function (e) {
        e.preventDefault();
    }
    document.ontouchmove = function (e) {
        e.preventDefault();
    }
    window.onscroll = function (e) {
        e.preventDefault();
    }
    window.ontouchmove = function (e) {
        e.preventDefault();
    }
}

function onNoScrollRemoved() {
    htmlElement.classList.remove('no-scroll');
    mainElement.classList.remove('no-scroll');
    htmlElement.style.removeProperty('padding-right');
    header.style.removeProperty('padding-right');
    footer.style.removeProperty('left');
    footer.style.removeProperty('width');
    document.onscroll = function (e) {
        return true;
    }
    document.ontouchmove = function (e) {
        return true;
    }
    window.onscroll = function (e) {
        return true;
    }
    window.ontouchmove = function (e) {
        return true;
    }
}

headerToggleBtn.onclick = (e) => {
    header.classList.toggle('opened');

    if (header.classList.contains('opened')) {
        onNoScrollApplied();

        header.onscroll = function (e) {
            e.preventDefault();
        }
        header.ontouchmove = function (e) {
            e.preventDefault();
        }
    } else {
        onNoScrollRemoved();

        header.onscroll = function (e) {
            return true;
        }
        header.ontouchmove = function (e) {
            return true;
        }
    }
};
