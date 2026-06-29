const CORE_CACHE = 'pwa-core-v1';
const USER_CACHE = 'pwa-user-selected-v1';

const CORE_ASSETS = [
    '/',
    '/manifest.json'
];

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CORE_CACHE).then((cache) => {
            return cache.addAll(CORE_ASSETS);
        })
    );
    self.skipWaiting();
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys().then((cacheNames) => {
            return Promise.all(
                cacheNames.map((cache) => {
                    if (cache !== CORE_CACHE && cache !== USER_CACHE) {
                        return caches.delete(cache);
                    }
                })
            );
        })
    );
    self.clients.claim();
});

// Перехват запросов (Сеть / Кэш пользовательских файлов)
self.addEventListener('fetch', (event) => {
    if (event.request.method !== 'GET') return;

    event.respondWith(
        caches.match(event.request).then((cachedResponse) => {
            if (cachedResponse) {
                return cachedResponse;
            }
            return fetch(event.request).catch(() => {
                return new Response('Офлайн режим.', {
                    status: 503,
                    headers: new Headers({'Content-Type': 'text/plain; charset=utf-8'})
                });
            });
        })
    );
});
