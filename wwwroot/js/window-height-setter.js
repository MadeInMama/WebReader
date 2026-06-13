const heightByRunningType = Object.freeze({
    Other: '100svh',
    Android: '100svh',
    AndroidPWA: '100svh',
    Ios: '100dvh',
    IosPWA: '100vh',
});

const ua = navigator.userAgent || navigator.vendor;

const isAndroid = /android/i.test(ua);

const isIOS = /iPad|iPhone|iPod/.test(ua) || 
    (navigator.maxTouchPoints > 2 && /Mac/.test(ua));

const isPWA = window.matchMedia('(display-mode: standalone)').matches || 
    navigator.standalone || 
    document.referrer.includes('android-app://');

function getHeight() {
    if (isIOS) return isPWA ? heightByRunningType.IosPWA : heightByRunningType.Ios;
    if (isAndroid) return isPWA ? heightByRunningType.AndroidPWA : heightByRunningType.Android;
    
    return heightByRunningType.Other;
}

function apply() {
    document.documentElement.style.setProperty('--pwa-height', getHeight());
}

function debounce(fn, delay = 100) {
    let timeout;
    return (...args) => {
        clearTimeout(timeout);
        timeout = setTimeout(() => fn(...args), delay);
    };
}

const optimizedApply = debounce(apply, 50);

apply();

window.addEventListener('pageshow', apply);
window.addEventListener('orientationchange', apply);
window.addEventListener('resize', optimizedApply);

if (window.visualViewport) {
    window.visualViewport.addEventListener('resize', optimizedApply);
}
