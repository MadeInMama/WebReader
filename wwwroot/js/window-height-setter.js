function fixDynamicHeight() {
    const realHeight = window.innerHeight;
    document.documentElement.style.setProperty('--pwa-height', `${realHeight}px`);
}

window.addEventListener('DOMContentLoaded', fixDynamicHeight);
window.addEventListener('resize', fixDynamicHeight);
window.addEventListener('orientationchange', fixDynamicHeight);
