class FixedRequestQueue {
    constructor(maxSize, intervalMs) {
        this.queue = [];
        this.maxSize = maxSize;
        this.intervalMs = intervalMs;
        this.isProcessing = false;
    }

    push(taskFunction) {
        if (this.queue.length >= this.maxSize) {
            this.queue.shift();
        }

        this.queue.push(taskFunction);

        if (!this.isProcessing) {
            this.process();
        }
    }

    async process() {
        if (this.queue.length === 0) {
            this.isProcessing = false;
            return;
        }

        this.isProcessing = true;

        const currentTask = this.queue.shift();

        try {
            await currentTask();
        } catch (err) {
            console.error("Ошибка при выполнении задачи из очереди:", err);
        }

        setTimeout(() => this.process(), this.intervalMs);
    }
}

const fixedRequestQueue = new FixedRequestQueue(2, 500);
