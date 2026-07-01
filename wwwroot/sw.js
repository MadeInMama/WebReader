const CORE_CACHE = 'pwa-core-v1';
const USER_CACHE = 'pwa-user-selected-v1';

const CORE_ASSETS = [
    '/',
    '/manifest.json',
];

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CORE_CACHE).then((cache) => {
            return Promise.all(
                CORE_ASSETS.map(url => {
                    return cache.add(url).catch(err => {
                        console.warn(`Could not pre-cache ${url}. It will be cached dynamically later.`, err);
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
            .then(() => {
                return self.clients.claim();
            })
    );
});
/*
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
        const isRootUrl = url.pathname === '/' || url.pathname === '';
        const matchOptions = isRootUrl ? {ignoreSearch: true} : {};

        event.respondWith(
            caches.match(event.request, matchOptions).then((cachedResponse) => {
                if (cachedResponse) {
                    if (navigator.onLine) {
                        const fetchPromise = fetch(event.request).then((networkResponse) => {
                            if (networkResponse && networkResponse.ok) {
                                const cloned = networkResponse.clone();
                                caches.open(USER_CACHE).then((cache) => {
                                    cache.put(event.request, cloned);
                                });
                            }
                        }).catch(() => {
                        });
                        event.waitUntil(fetchPromise);
                    }

                    return cachedResponse;
                }

                return fetch(event.request).then((networkResponse) => {
                    if (networkResponse && networkResponse.ok) {
                        const cloned = networkResponse.clone();
                        caches.open(USER_CACHE).then((cache) => {
                            cache.put(event.request, cloned);
                        });
                    }
                    return networkResponse;
                }).catch(() => {
                    return new Response('Офлайн режим. Подключитесь к интернету для первой загрузки.', {
                        status: 503,
                        headers: new Headers({'Content-Type': 'text/plain; charset=utf-8'})
                    });
                });
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
                        !url.pathname.endsWith('favicon.ico') &&
                        !url.pathname.endsWith('icon-512.png')) {
                        const cloned = networkResponse.clone();
                        caches.open(CORE_CACHE).then((cache) => {
                            cache.put(event.request, cloned);
                        });
                    }
                    return networkResponse;
                }).catch(() => {
                    return new Response('', {status: 404, statusText: 'Not Found'});
                });
            })
        );
        return;
    }

    event.respondWith(
        fetch(event.request)
            .then((networkResponse) => {
                if (networkResponse && networkResponse.ok) {
                    const cloned = networkResponse.clone();
                    caches.open(USER_CACHE).then((cache) => {
                        cache.put(event.request, cloned);
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
*/
