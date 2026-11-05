const APPLICATION_SERVER_KEY = "BM4cx4uxndGMF0b01rOaztwWfoeU7TjTQh1SgHR7uq-VrQsRZ8dYTuFYryPIk9mG5puf7UWL6uu75hyGwW3N6Ec";

function getDeviceName() {
    const ua = navigator.userAgent;
    const width = window.screen.width;
    const height = window.screen.height;
    const ratio = window.devicePixelRatio;

    // КРИТИЧЕСКИЙ ШАГ: Проверяем, запущено ли приложение как установленное PWA
    const isPWA = window.matchMedia('(display-mode: standalone)').matches || navigator.standalone;

    // 1. ОПРЕДЕЛЯЕМ БРАУЗЕР (ИЛИ СТАВИМ PWA)
    let browser = "Browser";

    if (isPWA) {
        browser = "PWA"; // Если запущено как приложение, имя браузера нам больше не важно
    } else if (/OPR\/|Opera\//.test(ua)) {
        browser = "Opera";
    } else if (/Edg\//.test(ua)) {
        browser = "Edge";
    } else if (/YaBrowser\//.test(ua)) {
        browser = "Яндекс.Браузер";
    } else if (/Chrome\//.test(ua)) {
        // Проверяем, что это не пустой WebView внутри приложений
        browser = /wv/.test(ua) ? "WebView" : "Chrome";
    } else if (/Firefox\//.test(ua)) {
        browser = "Firefox";
    } else if (/Safari\//.test(ua) && !/Chrome\//.test(ua)) {
        browser = "Safari";
    } else if (/MSIE\s|Trident\//.test(ua)) {
        browser = "Internet Explorer";
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

                    // Детальное сопоставление по экрану и видеочипу
                    if (width === 440 && height === 956) model = "iPhone 16 Pro Max";
                    else if (width === 402 && height === 874) model = "iPhone 16 Pro";
                    else if (width === 430 && height === 932) model = gpu.includes("Apple GPU") ? "iPhone 16 Plus / 15 Pro Max" : "iPhone 15 Plus / 14 Pro Max";
                    else if (width === 393 && height === 852) model = gpu.includes("Apple GPU") ? "iPhone 16 / 15 Pro" : "iPhone 15 / 14 Pro";
                    else if (width === 428 && height === 926) model = "iPhone 14 Plus / 13 Pro Max";
                    else if (width === 390 && height === 844) model = "iPhone 14 / 13 / 13 Pro";
                    else if (width === 414 && height === 896) model = ratio === 3 ? "iPhone 11 Pro Max / XS Max" : "iPhone 11 / XR";
                    else if (width === 375 && height === 812) model = "iPhone 11 Pro / X / XS / 13 mini";
                    else if (width === 414 && height === 736) model = "iPhone 8 Plus / 7 Plus";
                    else if (width === 375 && height === 667) model = "iPhone SE / 8";
                }
            }
        } catch (e) {
        }

        // Для iOS: если это PWA, то пишем просто "PWA на iPhone...", иначе "Safari на iPhone..."
        const displayBrowser = isPWA ? "PWA" : (browser === "Safari" ? "Safari" : `${browser} for iOS`);
        return `${displayBrowser} on ${model}`;
    }

    // --- APPLE IPAD ---
    const isiPad = /iPad/.test(ua) || (navigator.maxTouchPoints && navigator.maxTouchPoints > 2 && /Macintosh/.test(ua));
    if (isiPad) {
        let model = "iPad";
        if (width >= 1024 && height >= 1366 || width >= 1366 && height >= 1024) model = "iPad Pro 13\"";
        else if (width >= 834 && height >= 1194 || width >= 1194 && height >= 834) model = "iPad Pro 11\" / Air";
        else if (width === 768 && height === 1024) model = "iPad Classic / mini";

        const displayBrowser = isPWA ? "PWA" : (browser === "Safari" ? "Safari" : `${browser} for iOS`);
        return `${displayBrowser} on ${model}`;
    }

    // --- ANDROID ---
    if (/Android/.test(ua)) {
        let model = "Android";
        const match = ua.match(/Android\s+[^;]+;\s+([^;)]+)/);

        // ПРОВЕРКА: Проверяем, что регулярное выражение нашло совпадение (match)
        // и в строке нет лишнего служебного слова "Build"
        if (match && match[1] && !match[1].includes("Build")) {
            let m = match[1].trim(); // Берем именно первую подгруппу (название модели)
            if (m.startsWith("SM-")) m = `Samsung ${m}`;
            if (m.startsWith("Pixel")) m = `Google ${m}`;
            model = m;
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
            DeviceName: getDeviceName()
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
