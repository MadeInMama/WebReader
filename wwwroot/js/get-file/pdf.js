import * as pdfjsLib from 'https://unpkg.com/pdfjs-dist@latest/build/pdf.min.mjs';

pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://unpkg.com/pdfjs-dist@latest/build/pdf.worker.min.mjs';

function prepareFile(data) {
    pdfjsLib.getDocument({data: new Uint8Array(data)}).promise.then(doc => {
        pdfDoc = doc;
        maxPage = pdfDoc.numPages;

        Array.from(document.getElementsByClassName("preview-page")).forEach(value => {
            for (let i = 0; i < maxPage; i++) {
                const option = document.createElement("option");

                option.text = `${i + 1}/${maxPage}`;
                option.value = (i + 1).toString();

                value.appendChild(option);
            }
        });

        SetPage(currentPage, false);
        SetScale(currentScale, false);

        setTimeout(() => {
            document.querySelector("header.can-be-opened").classList.remove("opened");
        }, 5000);
    });
}

let currentRenderTask = null;

function renderPage() {
    pdfDoc.getPage(currentPage).then(page => {
        if (currentRenderTask) {
            currentRenderTask.cancel();
        }

        const viewport = page.getViewport({scale: currentScale <= 0 ? 3 : currentScale / 33});
        const canvas = document.getElementById('pdf-canvas');
        const context = canvas.getContext('2d');

        canvas.height = viewport.height;
        canvas.width = viewport.width;

        const renderContext = {
            canvasContext: context,
            viewport: viewport
        };

        currentRenderTask = page.render(renderContext);

        currentRenderTask.promise.then(() => {
            currentRenderTask = null;
        }).catch(err => {
            if (err.name === 'RenderingCancelledException') {
                console.log('Предыдущий рендеринг был отменен.');
            } else {
                console.error('Ошибка рендеринга:', err);
            }
        });
    });
}

function SetScale(scale, send = true) {
    currentScale = Number.parseInt(scale);

    Array.from(document.getElementsByClassName("set-scale")).forEach(value => {
        value.value = currentScale;
        value.disabled = false;
    });

    if (currentScale === -1) {
        document.querySelector("main").style.removeProperty('grid-column');
        document.querySelector("#pdf-canvas").style.width = '100%';
    } else {
        document.querySelector("main").style.gridColumn = '1 / 4';
        document.querySelector("#pdf-canvas").style.width = currentScale + "%";
    }
    renderPage();

    if (send) SendCurrPage();
}

window.prepareFile = prepareFile;
window.renderPage = renderPage;
window.SetScale = SetScale;
