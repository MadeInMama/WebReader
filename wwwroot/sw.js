const CORE_CACHE = 'pwa-core-v1';
const USER_CACHE = 'pwa-user-selected-v1';
const OFFLINE_URL = '/Home/Offline';

const CORE_ASSETS = [
    'https://unpkg.com/pdfjs-dist@latest/build/pdf.min.mjs',
    'https://cdnjs.cloudflare.com/ajax/libs/jszip/3.10.1/jszip.min.js',

    OFFLINE_URL,

    '/css/click-effects.css',
    '/css/file-common-preview.css',
    '/css/file-fb2-preview.css',
    '/css/file-img-preview.css',
    '/css/file-list.css',
    '/css/file-pdf-preview.css',
    '/css/fullscreen-control.css',
    '/css/header.css',
    '/css/login-register.css',
    '/css/modal-info-with-custom-html.css',
    '/css/scheduled-tasks.css',
    '/css/site.css',
    '/css/site-items.css',

    '/fonts/Comfortaa-VariableFont_wght.ttf',

    '/icons/arrow-circle-up-svgrepo-com.svg',
    '/icons/background-svgrepo-com.svg',
    '/icons/circular-cake-graphic-with-quarter-part-cutted-svgrepo-com.svg',
    '/icons/dark-mode-svgrepo-com.svg',
    '/icons/download-svgrepo-com.svg',
    '/icons/error-cross-svgrepo-com.svg',
    '/icons/fullscreen-exit-svgrepo-com.svg',
    '/icons/fullscreen-svgrepo-com.svg',
    '/icons/info-svgrepo-com.svg',
    '/icons/log-in-3-svgrepo-com.svg',
    '/icons/menu-01-svgrepo-com.svg',
    '/icons/progress-0-svgrepo-com.svg',
    '/icons/progress-33-svgrepo-com.svg',
    '/icons/round-done-button-svgrepo-com.svg',
    '/icons/stopwatch-wait-svgrepo-com.svg',
    '/icons/success-tick-svgrepo-com.svg',
    '/icons/upload-svgrepo-com.svg',
    '/icons/user-add-svgrepo-com.svg',

    '/js/custom-axios.js',
    '/js/custom-event.js',
    '/js/delete-row.js',
    '/js/empty-row-control.js',
    '/js/fullscreen-control-v2.js',
    '/js/modal-info-with-custom-html.js',
    '/js/nav-btns-pass-scroll-to-main.js',
    '/js/settings.js',
    '/js/site.js',
    '/js/to-local-date-time.js',
    '/js/window-height-setter.js',

    '/js/scheduled-task/scheduled-task.js',
    '/js/scheduled-task/scheduled-task-filters.js',
    '/js/scheduled-task/scheduled-task-form.js',
    '/js/scheduled-task/scheduled-task-websocket.js',

    '/js/get-file/common.js',
    '/js/get-file/fb2.js',
    '/js/get-file/img.js',
    '/js/get-file/pdf.js',

    '/js/cache/cache.js',
    '/js/cache/file-download.js',

    '/favicon.ico',
    '/manifest.json',
    '/sw.js'
];

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CORE_CACHE)
            .then((cache) => {
                return Promise.allSettled(
                    CORE_ASSETS.map(url => cache.add(url))
                );
            })
            .then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys()
            .then((cacheNames) => {
                return Promise.all(
                    cacheNames.filter((name) => name !== CORE_CACHE && name !== USER_CACHE)
                        .map((name) => caches.delete(name))
                );
            })
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') return;
    event.respondWith(
        fetch(event.request)
            .then(response => response)
            .catch(_ => {
                const url = new URL(event.request.url);

                const isVersionedFile = /\.(js|css)$/i.test(url.pathname);

                const matchOptions = isVersionedFile ? {ignoreSearch: true} : {};

                return caches.match(event.request, matchOptions).then((cachedResponse) => {
                    if (cachedResponse) {
                        return cachedResponse;
                    }
                    if (event.request.mode === 'navigate') {
                        return caches.match(OFFLINE_URL);
                    }
                });
            })
    );
});
