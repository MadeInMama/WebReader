const APPLICATION_SERVER_KEY = "BM4cx4uxndGMF0b01rOaztwWfoeU7TjTQh1SgHR7uq-VrQsRZ8dYTuFYryPIk9mG5puf7UWL6uu75hyGwW3N6Ec";

async function getDeviceName() {
    const ua = navigator.userAgent;

    const screenW = window.screen.width;
    const screenH = window.screen.height;
    const minDim = Math.min(screenW, screenH);
    const maxDim = Math.max(screenW, screenH);
    const ratio = window.devicePixelRatio;

    const isPWA = window.matchMedia('(display-mode: standalone)').matches ||
        ('standalone' in navigator && navigator.standalone);

    let browser = isPWA ? "PWA" : "Browser";
    if (!isPWA) {
        if (/OPR\/|Opera\/|OPT\//.test(ua)) browser = "Opera";
        else if (/Edg\/|EdgiOS/.test(ua)) browser = "Edge";
        else if (/YaBrowser\//.test(ua)) browser = "Yandex Browser";
        else if (/Chrome\/|CriOS/.test(ua)) browser = /wv/.test(ua) ? "WebView" : "Chrome";
        else if (/Firefox\/|FxiOS/.test(ua)) browser = "Firefox";
        else if (/Safari\//.test(ua) && !/Chrome\/|CriOS\/|FxiOS\/|EdgiOS\//.test(ua)) browser = "Safari";
    }

    if (navigator.userAgentData && navigator.userAgentData.mobile) {
        try {
            const hints = await navigator.userAgentData.getHighEntropyValues(['model']);
            if (hints && hints.model) {
                return `${browser} on ${hints.model}`;
            }
        } catch (e) {
        }
    }

    let gpu = "";
    try {
        const canvas = document.createElement('canvas');
        const gl = canvas.getContext('webgl') || canvas.getContext('experimental-webgl');
        if (gl) {
            const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
            if (debugInfo) {
                gpu = gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL) || "";
            }
        }
    } catch (e) {
    }

    if (/iPhone/.test(ua)) {
        let model = "iPhone";
        if (minDim === 440 && maxDim === 956) model = "iPhone 16 Pro Max";
        else if (minDim === 402 && maxDim === 874) model = "iPhone 16 Pro";
        else if (minDim === 430 && maxDim === 932) model = gpu.includes("Apple GPU") ? "iPhone 16 Plus / 15 Pro Max" : "iPhone 15 Plus / 14 Pro Max";
        else if (minDim === 393 && maxDim === 852) model = gpu.includes("Apple GPU") ? "iPhone 16 / 15 Pro" : "iPhone 15 / 14 Pro";
        else if (minDim === 428 && maxDim === 926) model = "iPhone 14 Plus / 13 Pro Max";
        else if (minDim === 390 && maxDim === 844) model = "iPhone 14 / 13 / 13 Pro";
        else if (minDim === 414 && maxDim === 896) model = ratio === 3 ? "iPhone 11 Pro Max / XS Max" : "iPhone 11 / XR";
        else if (minDim === 375 && maxDim === 812) model = ratio === 3 ? "iPhone 11 Pro / X / XS" : "iPhone 13 mini / 12 mini";
        else if (minDim === 414 && maxDim === 736) model = "iPhone 8 Plus / 7 Plus";
        else if (minDim === 375 && maxDim === 667) model = "iPhone SE / 8 / 7";

        const displayBrowser = isPWA ? "PWA" : (browser === "Safari" ? "Safari" : `${browser} for iOS`);
        return `${displayBrowser} on ${model}`;
    }

    const isiPad = /iPad/.test(ua) || (navigator.maxTouchPoints && navigator.maxTouchPoints > 2 && /Macintosh/.test(ua));
    if (isiPad) {
        let model = "iPad";

        if (minDim === 1032 && maxDim === 1376) {
            model = "iPad Pro 13\" (M4, 2024)";
        } else if (minDim === 1024 && maxDim === 1366) {
            model = "iPad Pro 12.9\"";
        } else if (minDim === 834 && maxDim === 1210) {
            model = "iPad Pro 11\" (M4, 2024)";
        } else if (minDim === 834 && maxDim === 1194) {
            model = gpu.includes("Apple GPU") ? "iPad Pro 11\" / Air 11\"" : "iPad Pro 11\"";
        } else if (minDim === 820 && maxDim === 1180) {
            model = "iPad Air (Gen 4/5) / iPad 10.9\"";
        } else if (minDim === 834 && maxDim === 1112) {
            model = "iPad Air 10.5\" (Gen 3) / iPad Pro 10.5\"";
        } else if (minDim === 768 && maxDim === 1024) {
            model = ratio === 2 ? "iPad (Gen 5-9) / mini (Gen 2-5)" : "iPad (Gen 1-4) / mini 1";
        } else if (minDim === 744 && maxDim === 1133) {
            model = "iPad mini (Gen 6/7)";
        }

        const displayBrowser = isPWA ? "PWA" : (browser === "Safari" ? "Safari" : `${browser} for iOS`);
        return `${displayBrowser} on ${model}`;
    }

    if (/Android/.test(ua)) {
        let model = "Android Device";
        const match = ua.match(/Android\s+\d+[^;]*;\s*([^;)]+)/) || ua.match(/\bLinux;\s*Android.+?;\s*([^;)]+)/);
        if (match && match[1]) {
            let m = match[1].trim();
            if (m.includes("Build/")) {
                m = m.split("Build/")[0].trim();
            }
            if (m.length > 2) {
                if (m.startsWith("SM-")) m = `Samsung ${m}`;
                if (m.startsWith("Pixel")) m = `Google ${m}`;
                model = m;
            }
        }
        return `${browser} on ${model}`;
    }

    if (/Windows/.test(ua)) return `${browser} on Windows`;
    if (/Macintosh/.test(ua)) return `${browser} on macOS`;
    if (/Linux/.test(ua)) return `${browser} on Linux`;

    return `${browser} (Unknown OS)`;
}

async function subscribeUserToPWANotifications(afterSuccessFunc, onErrorFunc) {
    if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
        if (onErrorFunc) onErrorFunc();
        return;
    }

    try {
        const registration = await navigator.serviceWorker.ready;

        if (Notification.permission === 'denied') {
            if (onErrorFunc) onErrorFunc();
            return;
        }

        let subscription = await registration.pushManager.getSubscription();

        if (!subscription) {
            const convertedKey = urlBase64ToUint8Array(APPLICATION_SERVER_KEY);

            subscription = await registration.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: convertedKey
            });
        }

        const rawSubscription = subscription.toJSON();
        const deviceName = await getDeviceName();

        requestQueue.enqueue({
            method: 'POST',
            url: '/api/PwaNotification/Subscribe',
            data: {
                Endpoint: subscription.endpoint,
                Keys: {
                    p256dh: rawSubscription.keys?.p256dh || "",
                    auth: rawSubscription.keys?.auth || ""
                },
                DeviceName: deviceName
            },
            options: {
                headers: {
                    'RequestVerificationToken': GetAntiForgeryToken()
                },
                withCredentials: true
            },
            beforeSend: () => {
            }
        })
            .then(_ => {
                if (afterSuccessFunc) afterSuccessFunc();
            })
            .catch(_ => {
                if (onErrorFunc) onErrorFunc();
            });

    } catch (_) {
    }
}

async function unsubscribeUserFromPWANotifications(afterSuccessFunc, onErrorFunc) {
    if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
        if (onErrorFunc) onErrorFunc();
        return;
    }

    try {
        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();

        if (subscription) {
            const endpoint = subscription.endpoint;
            const successful = await subscription.unsubscribe();

            if (successful) {
                requestQueue.enqueue({
                    method: 'POST',
                    url: '/api/PwaNotification/Unsubscribe',
                    data: {
                        Endpoint: endpoint
                    },
                    options: {
                        headers: {
                            'RequestVerificationToken': GetAntiForgeryToken()
                        },
                        withCredentials: true
                    },
                    beforeSend: () => {
                    }
                })
                    .then(_ => {
                        if (afterSuccessFunc) afterSuccessFunc();
                    })
                    .catch(_ => {
                        if (onErrorFunc) onErrorFunc();
                    });
            }
        } else {
            if (onErrorFunc) onErrorFunc();
        }
    } catch (_) {
        if (onErrorFunc) onErrorFunc();
    }
}

async function isSubscriptionExistsOnLocal() {
    const registration = await navigator.serviceWorker.ready;
    const subscription = await registration.pushManager.getSubscription();

    return subscription ? subscription.endpoint : "";
}

async function isSubscriptionExistsOnServer(endpoint) {
    if (!endpoint) return false;

    const res = await axios.post('/api/PwaNotification/IsSubscribed', {
        Endpoint: endpoint
    }, {
        headers: {
            'RequestVerificationToken': GetAntiForgeryToken()
        },
        withCredentials: true
    });

    return res.status === 200 && res.data;
}

function urlBase64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - base64String.length % 4) % 4);
    const base64 = (base64String + padding).replace(/\-/g, '+').replace(/_/g, '/');
    const rawData = window.atob(base64);
    const outputArray = new Uint8Array(rawData.length);
    for (let i = 0; i < rawData.length; ++i) {
        outputArray[i] = rawData.charCodeAt(i);
    }
    return outputArray;
}
