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
    constructor() {
        super('pwa-core-v1');
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
