class RequestQueue {
    constructor() {
        this.queue = [];
        this.isProcessing = false;
    }

    enqueue(config) {
        return new Promise((resolve, reject) => {
            this.queue.push({config, resolve, reject});
            this.process();
        });
    }

    async process() {
        if (this.isProcessing || this.queue.length === 0) return;

        this.isProcessing = true;

        while (this.queue.length > 0) {
            const {config, resolve, reject} = this.queue.shift();

            try {
                config.beforeSend();
                switch (config.method) {
                    case 'GET':
                        if (config.options.responseType !== 'arraybuffer') {
                            await resolve(await axios.get(config.url, config.data, config.options));
                        } else {
                            let respP = await fetch(config.url, {
                                method: 'GET',
                                body: config.data,
                                headers: config.options.headers
                            });
                            resolve(await updateProgress(respP, config.setProgress));
                        }
                        break;
                    case 'POST':
                        if (config.options.responseType !== 'arraybuffer') {
                            await resolve(await axios.post(config.url, config.data, config.options));
                        } else {
                            let respP = await fetch(config.url, {
                                method: 'POST',
                                body: config.data,
                                headers: config.options.headers
                            });
                            resolve(await updateProgress(respP, config.setProgress));
                        }
                        break;
                    default:
                }
            } catch (error) {
                await reject(error);
            }
        }

        this.isProcessing = false;
    }
}

async function updateProgress(response, progressSetter) {
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }

    const contentLength = response.headers.get('Content-Length');
    const total = contentLength ? parseInt(contentLength, 10) : 0;
    let loaded = 0;

    const reader = response.body.getReader();
    const chunks = [];

    while (true) {
        const {done, value} = await reader.read();
        if (done) break;

        chunks.push(value);
        loaded += value.length;

        if (total) {
            const percent = Math.min(99, Math.round((loaded / total) * 100));
            if (progressSetter !== undefined) progressSetter(percent);
        }
    }

    if (progressSetter !== undefined) progressSetter(100);

    const totalLength = chunks.reduce((acc, chunk) => acc + chunk.length, 0);
    const result = new Uint8Array(totalLength);

    let position = 0;
    for (const chunk of chunks) {
        result.set(chunk, position);
        position += chunk.length;
    }

    return result.buffer;
}

const requestQueue = new RequestQueue();
