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
    } catch (err) {
        console.error('Failed to load FB2:', err);
    }
}

let currentAudio = null;
let currentAudioTtsId = null;
let currentAudioUrl = null;
let animationFrameId = null;
let isTtsLogicPlay = false;

function renderPage() {
    currentAudio?.pause();
    URL.revokeObjectURL(currentAudioUrl);
    currentAudio = null;
    currentAudioTtsId = null;
    currentAudioUrl = null;

    document.querySelector('.fb2-canvas-container').innerHTML = pages[currentPage - 1];

    Array.from(document.getElementsByClassName("tts-btn")).forEach(f => {
        f.onclick = async (event) => {
            if (isTtsLogicPlay) return;
            isTtsLogicPlay = true;

            try {
                if (event.target.classList.contains('tts-loading') || event.target.classList.contains('tts-error')) {
                    cancelAnimationFrame(animationFrameId);
                    isTtsLogicPlay = false;
                    return;
                }

                if (event.target.classList.contains('tts-pause') && currentAudio != null) {
                    currentAudio.pause();
                    cancelAnimationFrame(animationFrameId);
                    setTtsControlBtnActiveClass(event.target, 'tts-play');
                    isTtsLogicPlay = false;
                    return;
                }

                let ttsId = event.target.getAttribute('tts-id');

                if (event.target.classList.contains('tts-play') && currentAudio != null && currentAudioTtsId === ttsId) {
                    currentAudio.play();
                    setTtsControlBtnActiveClass(event.target, 'tts-pause');
                    isTtsLogicPlay = false;
                    return;
                }

                cancelAnimationFrame(animationFrameId);
                setTtsControlBtnActiveClass(event.target, 'tts-loading');
                if (currentAudioTtsId != null) {
                    setTtsControlBtnActiveClass(document.querySelector(`.tts-btn[tts-id=${currentAudioTtsId}]`), 'tts-play');
                    document.querySelector(`p[tts-id=${currentAudioTtsId}]`).style.backgroundSize = 0 + '%';
                    currentAudio.pause();
                    currentAudio = null;
                    currentAudioTtsId = null;
                    currentAudioUrl = null;
                }

                URL.revokeObjectURL(currentAudioUrl);

                currentAudioTtsId = ttsId;
                let ttsLang = event.target.getAttribute('tts-lang');
                let ttsText = event.target.getAttribute('tts-text');

                const requestConfig = {
                    method: 'POST',
                    url: `/api/Tts/GetFile?ttsId=${ttsId}&ttsLang=${ttsLang}&ttsText=${ttsText}`,
                    data: {},
                    responseType: 'blob',
                    headers: {
                        'RequestVerificationToken': GetAntiForgeryToken()
                    },
                    withCredentials: true
                };

                const response = await axios(requestConfig);
                const audioBlob = new Blob([response.data], {type: 'audio/ogg'});

                currentAudioUrl = URL.createObjectURL(audioBlob);

                currentAudio = new Audio(currentAudioUrl);

                //TODO: speed selector (single setting btn for scale and etc)
                currentAudio.playbackRate = 1.5;

                currentAudio.addEventListener('play', () => {
                    animationFrameId = requestAnimationFrame(trackTtsProgress);
                });

                currentAudio.addEventListener('ended', () => {
                    URL.revokeObjectURL(currentAudioUrl);
                    currentAudio = null;
                    currentAudioTtsId = null;
                    currentAudioUrl = null;
                    cancelAnimationFrame(animationFrameId);
                    document.querySelector(`p[tts-id=${ttsId}]`).style.backgroundSize = 100 + '%';
                    setTtsControlBtnActiveClass(event.target, 'tts-play');
                });

                await currentAudio.play();

                setTtsControlBtnActiveClass(event.target, 'tts-pause');
            } catch (e) {
                setTtsControlBtnActiveClass(event.target, 'tts-error');
                cancelAnimationFrame(animationFrameId);
                document.querySelector(`p[tts-id=${currentAudioTtsId}]`).style.backgroundSize = 0 + '%';
                throw e;
            }

            isTtsLogicPlay = false;
        };
    });
}

function trackTtsProgress() {
    if (currentAudio && !currentAudio.paused) {
        const percentage = (currentAudio.currentTime / currentAudio.duration) * 100;

        document.querySelector(`p[tts-id=${currentAudioTtsId}`).style.backgroundSize = percentage + '%';

        animationFrameId = requestAnimationFrame(trackTtsProgress);
    }
}

function setTtsControlBtnActiveClass(el, className) {
    if (className === 'tts-play') {
        el.classList.add('tts-play');
        el.classList.remove('tts-pause');
        el.classList.remove('tts-loading');
        el.classList.remove('tts-error');
    } else if (className === 'tts-pause') {
        el.classList.remove('tts-play');
        el.classList.add('tts-pause');
        el.classList.remove('tts-loading');
        el.classList.remove('tts-error');
    } else if (className === 'tts-loading') {
        el.classList.remove('tts-play');
        el.classList.remove('tts-pause');
        el.classList.add('tts-loading');
        el.classList.remove('tts-error');
    } else if (className === 'tts-error') {
        el.classList.remove('tts-play');
        el.classList.remove('tts-pause');
        el.classList.remove('tts-loading');
        el.classList.add('tts-error');
    }
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

        parts.forEach((part, index, array) => {
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

            if (currWordCount > wordCountToSlice || index === array.length - 1) {
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

    pages = pages.filter(page => page !== '');
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
            let ttsBtn = null;
            if (node.hasAttribute('tts-id')) {
                //TODO: some text duplicates in log
                // console.log(node.textContent);
                // debugger;
                ttsBtn = `<button class="tts-btn tts-play"
                            tts-id="${node.getAttribute('tts-id')}"
                            tts-lang="${node.getAttribute('tts-lang')}"
                            tts-text="${Array.from(node.childNodes)
                    .filter(node => node.nodeType === Node.TEXT_NODE)
                    .map(node => node.textContent)
                    .join('')
                    .trim()}"
                            class="tts tts-play"></button>`;
            }

            return `<p tts-id="${node.getAttribute('tts-id')}">${ttsBtn ? ttsBtn : ''}${children()}</p>`;
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
