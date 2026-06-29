function prepareFile(data) {
    try {
        const fb2Text = new TextDecoder('utf-8').decode(data);

        parseFB2(fb2Text);
        maxPage = pages.length;

        Array.from(document.getElementsByClassName("preview-page")).forEach(value => {
            for (let i = 0; i < pages.length; i++) {
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
    } catch (err) {
        console.error('Failed to load FB2:', err);
    }
}

function renderPage() {
    document.querySelector('.fb2-canvas-container').innerHTML = pages[currentPage - 1];
}

function SetScale(scale, send = true) {
    currentScale = scale;

    Array.from(document.getElementsByClassName("set-scale")).forEach(value => {
        value.value = currentScale;
        value.disabled = false;
    });

    const fb2Section = document.querySelector(".fb2-canvas-container > section");
    if (fb2Section) fb2Section.style.fontSize = (currentScale / 100) + "em";
    document.querySelector("#custom-modal").style.fontSize = currentScale / 100 + "em";

    if (send) SendCurrPage();
}

function parseFB2(xmlString) {
    pages = [];
    imageMap.clear();
    noteMap.clear();

    const parser = new DOMParser();
    const doc = parser.parseFromString(xmlString, 'application/xml');
    if (doc.querySelector('parsererror')) {
        console.error('Invalid XML');
        return;
    }

    const sections = Array.from(doc.querySelectorAll("section"));

    const maxPCount = 6;

    sections.forEach(section => {
        const parts = Array.from(section.querySelectorAll(":scope > *"));
        if (parts.length <= maxPCount) return;

        const wordCountToSlice = 500;
        let currWordCount = 0;

        const fragment = doc.createDocumentFragment();

        let newSection = doc.createElement("section");
        newSection.setAttribute("data-split", "true");
        for (const attr of section.attributes) {
            newSection.setAttribute(attr.name, attr.value);
        }

        parts.forEach(part => {
            let wordCount = part.textContent.trim().split('').length;
            currWordCount += wordCount;

            switch (part.localName) {
                case "title":
                    break;
                case "image":
                    currWordCount += 50 - wordCount;
                    break;
                case "cite":
                    break;
                case "p":
                    break;
                default:
                    break;
            }

            newSection.appendChild(part.cloneNode(true));

            if (currWordCount > wordCountToSlice) {
                fragment.appendChild(newSection);

                newSection = doc.createElement("section");
                newSection.setAttribute("data-split", "true");
                for (const attr of section.attributes) {
                    newSection.setAttribute(attr.name, attr.value);
                }
                currWordCount = 0;
            }
        });

        section.parentNode.replaceChild(fragment, section);
    });

    doc.querySelectorAll('binary').forEach(bin => {
        const id = bin.getAttribute('id');
        const type = bin.getAttribute('content-type') || 'image/png';
        const data = bin.textContent.trim();
        if (id && data) imageMap.set(id, `data:${type};base64,${data}`);
    });

    doc.querySelectorAll('body[name="notes"] > section').forEach(note => {
        const id = note.getAttribute('id');
        if (id && note) noteMap.set(id, new DOMParser().parseFromString(convertNode(note, imageMap, false), 'text/html').querySelector('section').innerHTML);
    });

    const coverImg = doc.querySelector('coverpage image');
    if (coverImg) {
        const href = coverImg.getAttribute('l:href') || coverImg.getAttribute('href');
        if (href?.startsWith('#')) {
            const src = imageMap.get(href.slice(1));
            if (src) {
                pages.push(`<div class="fb2-cover"><img src="${escapeHtml(src)}" class="fb2-cover-img" alt="Cover" /></div>`);
            }
        }
    }

    const body = doc.querySelector('body');

    convertNode(body, imageMap);

    pages = pages.filter(page => page !== '')
    //.map(f => f.replaceAll("…", "<span style='font-family: monospace'>…</span>"));
}

function convertNode(node, imageMap, isPage = true) {
    if (node.nodeType === Node.TEXT_NODE) {
        return escapeHtml(node.nodeValue);
    }
    if (node.nodeType !== Node.ELEMENT_NODE) return '';

    const tag = node.localName;
    const children = () => Array.from(node.childNodes).map(n => convertNode(n, imageMap, isPage)).join('');

    switch (tag) {
        case 'p':
            return `<p>${children()}</p>`;
        case 'cite':
            return `<p class="cite-author">${children()}</p>`;
        case 'section':
            if (isPage) pages.push(`<section>${children()}</section>`);
            return `<section>${children()}</section>`;
        case 'title': {
            const level = node.closest('section') ? 2 : 1;
            return `<h${level}>${children()}</h${level}>`;
        }
        case 'image':
        case 'img': {
            const href = node.getAttribute('l:href') || node.getAttribute('href') || '';
            if (href.startsWith('#')) {
                const src = imageMap.get(href.slice(1));
                if (!isPage) return `<div class="fb2-image-container"><img src="${escapeHtml(src)}" class="fb2-image" /></div>`;
                else {
                    return `<div class="fb2-image-container">
                                        <details>
                                            <summary><span>Image (click to toggle)</span></summary>
                                            <img onclick='this.parentNode.removeAttribute("open")' src="${escapeHtml(src)}" class="fb2-image" alt="Image not found"/>
                                        </details>
                                    </div>`;
                }
            }
            return `<div>[Image missing: ${href}]</div>`;
        }
        case 'poem':
            return `<div class="poem">${children()}</div>`;
        case 'v':
            return `<div class="verse">${children()}</div>`;
        case 'empty-line':
            return '';
        case 'emphasis':
            return ` <em>${children()}</em> `;
        case 'strong':
            return `<strong>${children()}</strong>`;
        case 'a':
            if (node.getAttribute('type') === 'note') {
                const onClickHtml = noteMap.get(node.getAttribute('l:href').replace('#', ''));
                return `<button type="button" class="note-btn" onclick='openModalClicked(${JSON.stringify(onClickHtml)})'></button>`;
            } else return '';
        default:
            return children();
    }
}

function escapeHtml(str) {
    return str?.trim().replaceAll('\n', '').replaceAll(/[&<>"']/g, m =>
        ({'&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;'}[m])
    ) || '';
}
