function prepareFile(zip) {
    try {
        parseImages(zip).then(() => {
            maxPage = pages.length;

            Array.from(document.getElementsByClassName("preview-page")).forEach(value => {
                for (let i = 0; i < maxPage; i++) {
                    const option = document.createElement("option");

                    option.text = `${i + 1}/${maxPage}`;
                    option.value = (i + 1).toString();

                    value.appendChild(option);
                }
            });

            SetPage(currentPage, false);

            setTimeout(() => {
                document.querySelector("header.can-be-opened").classList.remove("opened");
            }, 1000);
        });
    } catch (err) {
        console.error('Failed to load ZIP:', err);
    }
}

function renderPage() {
    document.querySelector('#img-canvas').src = pages[currentPage - 1];
    document.querySelector('#img-canvas').style.opacity = '1';
}

function SetScale(scale, send = true) {
    currentScale = Number.parseInt(scale);

    Array.from(document.getElementsByClassName("set-scale")).forEach(value => {
        value.value = currentScale
    });

    if (currentScale === -1) {
        document.querySelector("main").style.removeProperty('grid-column');
        document.querySelector("main").style.removeProperty('overflow-y');
        document.querySelector("main").style.removeProperty('display');
        document.querySelector("#img-canvas").style.removeProperty('height');
        document.querySelector("#img-canvas").style.removeProperty('width');
    } else if (currentScale === 0) {
        document.querySelector("main").style.removeProperty('grid-column');
        document.querySelector("main").style.overflowY = 'hidden';
        document.querySelector("main").style.display = 'block';
        document.querySelector("#img-canvas").style.height = '100%';
        document.querySelector("#img-canvas").style.removeProperty('width');
    } else {
        document.querySelector("main").style.removeProperty('overflow-y');
        document.querySelector("main").style.removeProperty('display');
        document.querySelector("main").style.gridColumn = '1 / 4';
        document.querySelector("#img-canvas").style.removeProperty('height');
        document.querySelector("#img-canvas").style.width = currentScale + "%";
    }

    if (send) SendCurrPage();
}

async function parseImages(zip) {
    const zipFile = await new JSZip().loadAsync(zip);

    for (const img in zipFile.files) {
        const blob = await zipFile.files[img].async('blob');
        const imageUrl = URL.createObjectURL(blob);
        pages.push(imageUrl);
    }
}
