const heightByRunningType = Object.freeze({
    Other: '100svh',
    Android: '100svh',
    AndroidPWA: '100svh',
    Ios: '100dvh',
    IosPWA: '100vh',
});

const isAndroid = /android/i.test(navigator.userAgent || navigator.vendor || window.opera);

const isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent || navigator.vendor || window.opera)
    || (navigator.maxTouchPoints && navigator.maxTouchPoints > 2 && /MacIntel/.test(navigator.platform));

const isPWA = window.matchMedia('(display-mode: standalone)').matches
    || window.navigator.standalone
    || document.referrer.includes('android-app://');

function getHeight() {
    if (isPWA) {
        if (isIOS) return heightByRunningType.IosPWA;
        if (isAndroid) return heightByRunningType.AndroidPWA;
    } else {
        if (isIOS) return heightByRunningType.Ios;
        if (isAndroid) return heightByRunningType.Android;
    }
    return heightByRunningType.Other;
}

function apply() {
    document.documentElement.style.setProperty('--pwa-height', getHeight());
}

apply();

window.addEventListener('pageshow', apply);
window.addEventListener('orientationchange', apply);
window.addEventListener('resize', apply);

if (window.visualViewport) {
    window.visualViewport.addEventListener('resize', apply);
}
