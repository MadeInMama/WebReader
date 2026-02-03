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

document.querySelectorAll(".always-open-details").forEach((details) => {
    details.querySelector('summary').onclick = (e) => {
        if (details.open) {
            e.preventDefault();
            details.open = true;
        }
    };
});

const header = document.querySelector('header');
const headerToggleBtn = document.querySelector('.header-toggle');
const htmlElement = document.querySelector('html');

headerToggleBtn.onclick = (e) => {
    header.classList.toggle('opened');

    if (header.classList.contains('opened')) {
        htmlElement.classList.add('no-scroll');
    } else {
        htmlElement.classList.remove('no-scroll');
    }
};
