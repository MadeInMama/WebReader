// ==========================================
// НАСТРОЙКА: Вставьте сюда ваш Публичный VAPID ключ
// ==========================================
const APPLICATION_SERVER_KEY = "BM4cx4uxndGMF0b01rOaztwWfoeU7TjTQh1SgHR7uq-VrQsRZ8dYTuFYryPIk9mG5puf7UWL6uu75hyGwW3N6Ec";

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
            }
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

function setupNotificationButton() {
    const btn = document.getElementById('btn-enable-push');
    if (!btn) return;

    // Показываем кнопку только если подписка еще не оформлена
    if (Notification.permission !== 'granted') {
        btn.style.display = 'block';
        btn.addEventListener('click', async () => {
            // Важно: на iOS вызов должен происходить строго внутри этого click-события!
            await initPushNotification();
            btn.style.display = 'none';
        });
    }
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
