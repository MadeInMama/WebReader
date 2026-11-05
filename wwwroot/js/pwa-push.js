const APPLICATION_SERVER_KEY = "BM4cx4uxndGMF0b01rOaztwWfoeU7TjTQh1SgHR7uq-VrQsRZ8dYTuFYryPIk9mG5puf7UWL6uu75hyGwW3N6Ec";

async function getDeviceName() {
    const ua = navigator.userAgent;

    // Используем min и max, чтобы код не зависел от поворота экрана (портрет/альбом)
    const screenW = window.screen.width;
    const screenH = window.screen.height;
    const minDim = Math.min(screenW, screenH);
    const maxDim = Math.max(screenW, screenH);
    const ratio = window.devicePixelRatio;

    // Проверяем, запущено ли приложение как PWA
    const isPWA = window.matchMedia('(display-mode: standalone)').matches ||
        ('standalone' in navigator && navigator.standalone);

    // Определяем браузер с учетом особенностей iOS (CriOS, FxiOS и т.д.)
    let browser = isPWA ? "PWA" : "Browser";
    if (!isPWA) {
        if (/OPR\/|Opera\/|OPT\//.test(ua)) browser = "Opera";
        else if (/Edg\/|EdgiOS/.test(ua)) browser = "Edge";
        else if (/YaBrowser\//.test(ua)) browser = "Yandex Browser";
        else if (/Chrome\/|CriOS/.test(ua)) browser = /wv/.test(ua) ? "WebView" : "Chrome";
        else if (/Firefox\/|FxiOS/.test(ua)) browser = "Firefox";
        else if (/Safari\//.test(ua) && !/Chrome\/|CriOS\/|FxiOS\/|EdgiOS\//.test(ua)) browser = "Safari";
    }

    // 1. High-Entropy Hints для Android (Chromium)
    if (navigator.userAgentData && navigator.userAgentData.mobile) {
        try {
            const hints = await navigator.userAgentData.getHighEntropyValues(['model']);
            if (hints && hints.model) {
                return `${browser} on ${hints.model}`;
            }
        } catch (e) {
        }
    }

    // 2. ОПРЕДЕЛЯЕМ ОПЕРАЦИОННУЮ СИСТЕМУ И МОДЕЛЬ

    // --- APPLE IPHONE ---
    if (/iPhone/.test(ua)) {
        let model = "iPhone";
        try {
            const canvas = document.createElement('canvas');
            const gl = canvas.getContext('webgl') || canvas.getContext('experimental-webgl');
            if (gl) {
                const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
                if (debugInfo) {
                    const gpu = gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL) || "";

                    // Сравнение по minDim и maxDim решает проблему поворота экрана
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
                }
            }
        } catch (e) {
            // Игнорируем ошибки canvas
        }

        const displayBrowser = isPWA ? "PWA" : (browser === "Safari" ? "Safari" : `${browser} for iOS`);
        return `${displayBrowser} on ${model}`;
    }

    // --- APPLE IPAD ---
    const isiPad = /iPad/.test(ua) || (navigator.maxTouchPoints && navigator.maxTouchPoints > 2 && /Macintosh/.test(ua));
    if (isiPad) {
        let model = "iPad";
        if (maxDim >= 1366) model = "iPad Pro 13\"";
        else if (maxDim >= 1194) model = "iPad Pro 11\" / Air";
        else if (minDim === 768 && maxDim === 1024) model = "iPad Classic / mini";

        const displayBrowser = isPWA ? "PWA" : (browser === "Safari" ? "Safari" : `${browser} for iOS`);
        return `${displayBrowser} on ${model}`;
    }

    // --- ANDROID ---
    if (/Android/.test(ua)) {
        let model = "Android Device";
        // Более надежный поиск модели во всей строке после слова Android
        const match = ua.match(/Android\s+\d+[^;]*;\s*([^;)]+)/) || ua.match(/\bLinux;\s*Android.+?;\s*([^;)]+)/);
        if (match && match[1]) {
            let m = match[1].trim();
            // Отсекаем служебную информацию вроде "Build/..."
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

    // --- ДЕСКТОПНЫЕ ОС ---
    if (/Windows/.test(ua)) return `${browser} on Windows`;
    if (/Macintosh/.test(ua)) return `${browser} on macOS`;
    if (/Linux/.test(ua)) return `${browser} on Linux`;

    return `${browser} (Unknown OS)`;
}

// 1. Главная функция инициализации и подписки
async function initPushNotification() {
    console.log("PWA-Push: Инициализация подписки...");

    if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
        console.warn("PWA-Push: Браузер не поддерживает Service Worker или Push-уведомления.");
        return;
    }

    try {
        // Ждем, пока Service Worker будет полностью готов к работе
        const registration = await navigator.serviceWorker.ready;
        console.log("PWA-Push: Service Worker готов:", registration);

        // Проверяем, разрешил ли уже пользователь уведомления
        if (Notification.permission === 'denied') {
            console.error("PWA-Push: Пользователь заблокировал уведомления в настройках браузера!");
            return;
        }

        // Проверяем, есть ли уже активная подписка в браузере
        let subscription = await registration.pushManager.getSubscription();
        console.log("PWA-Push: Текущая подписка в браузере:", subscription);

        if (!subscription) {
            console.log("PWA-Push: Подписка отсутствует. Запрашиваем разрешение у пользователя...");

            if (!APPLICATION_SERVER_KEY || APPLICATION_SERVER_KEY === "ВАШ_ОТКРЫТЫЙ_VAPID_КЛЮЧ") {
                console.error("PWA-Push: Ошибка! Вы забыли указать APPLICATION_SERVER_KEY в начале файла pwa-push.js");
                return;
            }

            const convertedKey = urlBase64ToUint8Array(APPLICATION_SERVER_KEY);

            // Запрашиваем у браузера создание новой подписки
            subscription = await registration.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: convertedKey
            });

            console.log("PWA-Push: Пользователь разрешил уведомления. Подписка создана:", subscription);
        } else {
            console.log("PWA-Push: Подписка уже существует, отправляем её на сервер для проверки/обновления.");
        }

        // Отправляем данные подписки на бэкенд ASP.NET Core MVC
        await sendSubscriptionToServer(subscription);

    } catch (error) {
        console.error("PWA-Push: ❌ Произошла ошибка в процессе подписки:", error);
    }
}

// 2. Функция отправки подписки на контроллер MVC
async function sendSubscriptionToServer(subscription) {
    console.log("PWA-Push: Отправка запроса через requestQueue...");

    const rawSubscription = subscription.toJSON();

    console.log("PWA-Push: Извлеченные ключи:", rawSubscription.keys);
    console.log(subscription);
    console.log(subscription.keys);

    const deviceName = await getDeviceName();

    // subscription имеет структуру { endpoint: "...", keys: { auth: "...", p256dh: "..." } }
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
        .then(response => {
            console.log("PWA-Push: 🎉 Отлично! Подписка успешно сохранена на сервере.");
        })
        .catch(error => {
            console.error("PWA-Push: ❌ Ошибка при отправке подписки через очередь:", error);
        });
}

async function unsubscribeUserFromPush() {
    console.log("PWA-Push: Начало процесса отписки...");

    if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
        return;
    }

    try {
        const registration = await navigator.serviceWorker.ready;

        // 1. Получаем текущую активную подписку в браузере
        const subscription = await registration.pushManager.getSubscription();

        if (subscription) {
            const endpoint = subscription.endpoint;

            // 2. Отменяем подписку в push-службе браузера
            const successful = await subscription.unsubscribe();

            if (successful) {
                console.log("PWA-Push: Браузер успешно аннулировал подписку.");

                // 3. Отправляем запрос на удаление из базы данных ASP.NET Core
                await removeSubscriptionFromServer(endpoint);
            } else {
                console.error("PWA-Push: Не удалось отменить подписку в браузере.");
            }
        } else {
            console.log("PWA-Push: Активной подписки в браузере не обнаружено.");
        }
    } catch (error) {
        console.error("PWA-Push: ❌ Ошибка при отписке:", error);
    }
}

// Функция отправки запроса на удаление через вашу очередь
async function removeSubscriptionFromServer(endpoint) {
    console.log("PWA-Push: Отправка запроса на удаление подписки с сервера...");

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
        .then(() => {
            console.log("PWA-Push: 🎉 Подписка успешно удалена из базы данных сервера.");
        })
        .catch(error => {
            console.error("PWA-Push: ❌ Ошибка при удалении подписки с сервера:", error);
        });
}

// 3. Вспомогательная функция для конвертации VAPID ключа
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
