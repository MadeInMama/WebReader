const CORE_CACHE = 'pwa-core-v1';
const USER_CACHE = 'pwa-user-selected-v1';

const CORE_ASSETS = [
    '/',
    '/manifest.json'
];

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CORE_CACHE).then((cache) => {
            return Promise.all(
                CORE_ASSETS.map(url => {
                    return cache.add(url).catch(err => {
                        console.error(`Failed to pre-cache ${url}:`, err);
                    });
                })
            );
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

self.addEventListener('fetch', (event) => {
    if (event.request.method !== 'GET') return;

    const url = new URL(event.request.url);

    const isNavigation = event.request.mode === 'navigate';
    const isStaticAsset =
        url.pathname.startsWith('/css/') ||
        url.pathname.startsWith('/js/') ||
        url.pathname.endsWith('.min.mjs') ||
        url.pathname.endsWith('.min.js') ||
        url.pathname.startsWith('/fonts/') ||
        url.pathname.startsWith('/images/') ||
        url.pathname.startsWith('/icons/') ||
        url.pathname === '/manifest.json' ||
        url.pathname === '/favicon.ico';

    if (isNavigation) {
        event.respondWith(
            caches.match(event.request, {ignoreSearch: true}).then((cachedResponse) => {
                const fetchPromise = fetch(event.request).then((networkResponse) => {
                    const responseClone = networkResponse.clone();
                    if (networkResponse && networkResponse.ok) {
                        caches.open(USER_CACHE).then((cache) => {
                            cache.put(event.request, responseClone);
                        });
                    }
                    return networkResponse;
                }).catch(() => cachedResponse);

                return cachedResponse || fetchPromise;
            })
        );
        return;
    }

    if (isStaticAsset) {
        event.respondWith(
            caches.match(event.request).then((cachedResponse) => {
                if (cachedResponse) return cachedResponse;

                return fetch(event.request).then((networkResponse) => {
                    if (networkResponse && networkResponse.ok &&
                        !url.pathname.includes('favicon.ico') &&
                        !url.pathname.includes('icon-512.png')) {
                        const responseClone = networkResponse.clone();
                        caches.open(CORE_CACHE).then((cache) => {
                            cache.put(event.request, responseClone);
                        });
                    }
                    return networkResponse;
                });
            })
        );
        return;
    }

    event.respondWith(
        fetch(event.request)
            .then((networkResponse) => {
                if (networkResponse && networkResponse.ok) {
                    const responseClone = networkResponse.clone();
                    caches.open(USER_CACHE).then((cache) => {
                        cache.put(event.request, responseClone);
                    });
                }
                return networkResponse;
            })
            .catch(() => {
                return caches.match(event.request).then((cachedResponse) => {
                    if (cachedResponse) return cachedResponse;

                    return new Response('Офлайн режим.', {
                        status: 503,
                        headers: new Headers({'Content-Type': 'text/plain; charset=utf-8'})
                    });
                });
            })
    );
});
