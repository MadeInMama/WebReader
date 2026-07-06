const fileDownloadStateType = Object.freeze({
    Download: 'Download',
    Saved: 'Saved',
    Loading: 'Loading',
    Deleting: 'Deleting'
});

function downloadFile(bucketId, fileId, updateState, fileName) {
    const pageUrl = `/File/GetFile?bucketId=${bucketId}&fileId=${fileId}`;
    fetch(pageUrl)
        .then(response => {
            const newHeaders = new Headers(response.headers);
            newHeaders.append('x-file-name', encodeURIComponent(fileName));

            const responseToCache = new Response(response.body, {
                status: response.status,
                statusText: response.statusText,
                headers: newHeaders
            });

            return userCacheService.put(pageUrl, responseToCache);
        })
        .then(_ => {
            return coreCacheService.putStatic().then(_ => {
                return requestQueue.enqueue({
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
                        updateState(fileDownloadStateType.Loading, val);
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
                        updateState(fileDownloadStateType.Saved);
                    });
            });
        })
        .catch(error => {
            console.error("Error in downloadFile chain:", error);
            updateState(fileDownloadStateType.Download);
        });
}

function deleteFile(bucketId, fileId, updateState) {
    fileCacheService.delete(fileId).then((res) => {
        updateState(fileDownloadStateType.Download);
        return res;
    })
        .then(_ => {
            const pageUrl = `/File/GetFile?bucketId=${bucketId}&fileId=${fileId}`;
            return userCacheService.delete(pageUrl);
        })
        .catch(error => {
            console.error(error);
            updateState(fileDownloadStateType.Saved);
        });
}

class FileDownloadButton {
    constructor(element, bucketId, fileId, fileName) {
        this.btn = element;
        this.bucketId = bucketId;
        this.fileId = fileId;
        this.fileName = fileName;
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

        switch (newState) {
            case fileDownloadStateType.Download:
                this.btn.disabled = false;
                this.btn.classList.remove('unload-file');
                this.btn.classList.add('download-file');
                this.btn.innerHTML = '';
                break;
            case fileDownloadStateType.Saved:
                this.btn.disabled = false;
                this.btn.classList.remove('download-file');
                this.btn.classList.add('unload-file');
                this.btn.innerHTML = '';
                break;
            case fileDownloadStateType.Loading:
            case fileDownloadStateType.Deleting:
                this.btn.classList.remove('unload-file');
                this.btn.classList.remove('download-file');
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
            downloadFile(this.bucketId, this.fileId, (newState, progress = 0) => this.updateState(newState, progress), this.fileName);
        } catch (err) {
            console.error(err);
            alert('Ошибка при скачивании');
            this.updateState(fileDownloadStateType.Download);
        }
    }

    startDelete() {
        this.updateState(fileDownloadStateType.Deleting);
        try {
            deleteFile(this.bucketId, this.fileId, (newState) => this.updateState(newState));
        } catch (err) {
            console.error(err);
            this.updateState(fileDownloadStateType.Saved);
        }
    }
}
