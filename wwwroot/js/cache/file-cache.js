const fileCacheName = 'file-cache';

const fileCacheService = {
    _getValidKey(key) {
        if (key instanceof Request) return key;
        return `/file-cache-item/${encodeURIComponent(key)}`;
    },

    async put(key, data) {
        const validKey = this._getValidKey(key);
        const cache = await caches.open(fileCacheName);
        return await cache.put(validKey, data);
    },

    async delete(key) {
        const validKey = this._getValidKey(key);
        const cache = await caches.open(fileCacheName);
        return await cache.delete(validKey);
    },

    async has(key) {
        const validKey = this._getValidKey(key);
        const cache = await caches.open(fileCacheName);
        console.log(key);
        const match = await cache.match(validKey);

        return match !== undefined;
    },

    async get(key) {
        const validKey = this._getValidKey(key);
        const cache = await caches.open(fileCacheName);
        const response = await cache.match(validKey);
        if (!response) return null;
        return await response.arrayBuffer();
    }
};
