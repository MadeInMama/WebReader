class CacheService {
    constructor(fileCacheName) {
        this.fileCacheName = fileCacheName;
    }

    _getValidKey(key) {
        return key;
    }

    async put(key, data) {
        const validKey = this._getValidKey(key);
        const cache = await caches.open(this.fileCacheName);
        return await cache.put(validKey, data);
    }

    async delete(key) {
        const validKey = this._getValidKey(key);
        const cache = await caches.open(this.fileCacheName);
        return await cache.delete(validKey);
    }

    async has(key) {
        const validKey = this._getValidKey(key);
        const cache = await caches.open(this.fileCacheName);
        const match = await cache.match(validKey);

        return match !== undefined;
    }

    async get(key) {
        const validKey = this._getValidKey(key);
        const cache = await caches.open(this.fileCacheName);
        const response = await cache.match(validKey);
        if (!response) return null;
        return await response.arrayBuffer();
    }

    async getAll() {
        const cache = await caches.open(this.fileCacheName);
        const requests = await cache.keys();

        const promises = requests.map(async (request) => {
            const response = await cache.match(request);
            const data = response ? await response.arrayBuffer() : null;
            return {
                key: request.url,
                data: data
            };
        });

        return Promise.all(promises);
    }

    async deleteAll() {
        return await caches.delete(this.fileCacheName);
    }
}

class FileCacheService extends CacheService {
    constructor() {
        super('file-cache');
    }

    _getValidKey(key) {
        if (key instanceof Request) return key;
        return `/file-cache-item/${encodeURIComponent(key)}`;
    }
}

class CoreCacheService extends CacheService {
    #CORE_ASSETS = [
        '/Home/Offline',

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

        '/js/db.js',
        '/js/custom-axios.js',
        '/js/custom-event.js',
        '/js/delete-row.js',
        '/js/empty-row-control.js',
        '/js/fullscreen-control.js',
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

        '/js/cache/file-cache.js',
        '/js/cache/file-download.js',

        '/favicon.ico',
        '/manifest.json',
        '/sw.js'
    ];

    constructor() {
        super('pwa-core-v1');
    }

    async putStatic() {
        await caches.open(this.fileCacheName)
            .then((cache) => {
                return Promise.allSettled(
                    this.#CORE_ASSETS.map(url => cache.add(url))
                );
            })
    }
}

class UserCacheService extends CacheService {
    constructor() {
        super('pwa-user-selected-v1');
    }
}

const fileCacheService = new FileCacheService();
const coreCacheService = new CoreCacheService();
const userCacheService = new UserCacheService();
