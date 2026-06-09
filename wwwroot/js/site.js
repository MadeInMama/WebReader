const mainElement = document.querySelector('main');
const footer = document.querySelector('footer');
const scrollProgressBar = document.querySelector('#scroll-progress-bar');

if (footer.childNodes.length > 0 &&
    footer.innerHTML.replace(/\s+/g, '').length > 0) {
    footer.style.display = "grid";
} else {
    footer.style.display = "none";
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

function onNoScrollApplied() {
}

function onNoScrollRemoved() {
}

document.querySelectorAll(".opened-trigger")
    .forEach(el => {
        const className = 'opened';
        const attrTarget = 'target';

        el.onclick = (e) => {
            e.preventDefault();

            const target = document.querySelector(el.getAttribute(attrTarget));

            target.classList.toggle(className);

            if (target.classList.contains(className)) {
                onNoScrollApplied();

                Array.from(document.querySelectorAll(".opened"))
                    .filter(f => f !== target)
                    .forEach(toClose => {
                        toClose.classList.remove(className);
                    });
            } else {
                onNoScrollRemoved();
            }
        }
    });

function updateProgress() {
    const scrollTop = mainElement.scrollTop;
    const scrollHeight = mainElement.scrollHeight;
    const clientHeight = mainElement.clientHeight;

    const totalScroll = scrollHeight - clientHeight;
    const scrolledPercent = (scrollTop / totalScroll) * 100;

    if (scrolledPercent > 0) {
        scrollProgressBar.style.width = scrolledPercent + "%";
    } else {
        scrollProgressBar.style.width = "0%";
    }

    scrollProgressBar.style.backgroundColor =
        scrolledPercent > 99 ? 'var(--success-foreground-color)' : 'var(--error-foreground-color)';
}

mainElement.addEventListener("scroll", updateProgress);
window.addEventListener("load", updateProgress);
window.addEventListener("resize", updateProgress);
