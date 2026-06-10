function fixDynamicHeight() {
    setTimeout(() => {
        const realHeight = window.innerHeight;
        document.documentElement.style.setProperty('--pwa-height', `${realHeight}px`);
    }, 100);
}

window.addEventListener('load', fixDynamicHeight);

window.addEventListener('pageshow', fixDynamicHeight);

window.addEventListener('resize', fixDynamicHeight);
window.addEventListener('orientationchange', fixDynamicHeight);
