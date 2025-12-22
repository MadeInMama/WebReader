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
                        await resolve(await axios.get(config.url, config.data, config.options));
                        break;
                    case 'POST':
                        await resolve(await axios.post(config.url, config.data, config.options));
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

const requestQueue = new RequestQueue();
