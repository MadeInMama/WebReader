const fileDownloadStateType = Object.freeze({
    Download: 'Download',
    Saved: 'Saved',
    Loading: 'Loading',
    Deleting: 'Deleting'
});

function downloadFile(bucketId, fileId, clazz) {
    const pageUrl = `/File/GetFile?bucketId=${bucketId}&fileId=${fileId}`;
    fetch(pageUrl).then(response => {
        userCacheService.put(pageUrl, response);
    }).catch(error => {
        console.error("Error in downloadFile chain:", error);
        clazz.updateState(fileDownloadStateType.Download);
    });

    requestQueue.enqueue({
        method: 'POST',
        url: `/api/S3/GetFile?bucketId=${bucketId}&fileId=${fileId}`,
        data: {},
        options: {
            responseType: 'arraybuffer',
            headers: {
                'RequestVerificationToken': GetAntiForgeryToken()
            },
            withCredentials: true
        },
        beforeSend: () => {
        },
        setProgress: (val) => {
            clazz.updateState(fileDownloadStateType.Loading, val);
        }
    })
        .then(response => {
            if (!response) {
                throw new Error("Empty response received from requestQueue");
            }

            const responseToCache = new Response(response, {
                headers: {'Content-Type': 'application/octet-stream'}
            });

            return fileCacheService.put(fileId, responseToCache);
        })
        .then(() => {
            clazz.updateState(fileDownloadStateType.Saved);
        })
        .catch(error => {
            console.error("Error in downloadFile chain:", error);
            clazz.updateState(fileDownloadStateType.Download);
        });
}

function deleteFile(bucketId, fileId, clazz) {
    fileCacheService.delete(fileId).then((res) => {
        clazz.updateState(fileDownloadStateType.Download);
        return res;
    })
        .catch(error => {
            console.error(error);
            clazz.updateState(fileDownloadStateType.Saved);
        });
}

class FileDownloadButton {
    constructor(element, bucketId, fileId) {
        this.btn = element;
        this.bucketId = bucketId;
        this.fileId = fileId;
        this.state = fileDownloadStateType.Download;
        this.init();
    }

    init() {
        this.btn.addEventListener('click', () => this.handleClick());

        fileCacheService.has(this.fileId).then(isDownloaded => {
            this.updateState(isDownloaded ? fileDownloadStateType.Saved : fileDownloadStateType.Download);
        }).catch(error => {
            console.error(error);
        });
    }

    updateState(newState, progress = 0) {
        this.state = newState;
        this.btn.disabled = false;

        this.btn.classList.remove('unload-file');
        this.btn.classList.remove('download-file');

        switch (newState) {
            case fileDownloadStateType.Download:
                this.btn.classList.add('download-file');
                break;
            case fileDownloadStateType.Saved:
                this.btn.classList.add('unload-file');
                break;
            case fileDownloadStateType.Loading:
            case fileDownloadStateType.Deleting:
                this.btn.disabled = true;
                this.btn.innerHTML = `${progress}%`;
                break;
        }
    }

    handleClick() {
        if (this.state === fileDownloadStateType.Download) {
            this.startDownload();
        } else if (this.state === fileDownloadStateType.Saved) {
            this.startDelete();
        }
    }

    startDownload() {
        this.updateState(fileDownloadStateType.Loading, 0);
        try {
            downloadFile(this.bucketId, this.fileId, this);
        } catch (err) {
            console.error(err);
            alert('Ошибка при скачивании');
            this.updateState(fileDownloadStateType.Download);
        }
    }

    startDelete() {
        this.updateState(fileDownloadStateType.Deleting);
        try {
            deleteFile(this.bucketId, this.fileId, this);
        } catch (err) {
            console.error(err);
            this.updateState(fileDownloadStateType.Saved);
        }
    }
}
