function prepareFile(data) {
    try {
        const fb2Text = new TextDecoder('utf-8').decode(data);

        parseFB2(fb2Text);
        maxPage = pages.length;

        const headersMap = pages.map(pageHtml => {
            const doc = new DOMParser().parseFromString(pageHtml, 'text/html');
            return doc.querySelector('h2 > p')?.textContent.trim() || null;
        });

        Array.from(document.getElementsByClassName("preview-page")).forEach(selectElement => {
            let currentOptgroup = null;
            let lastHeaderName = null;

            for (let i = 0; i < pages.length; i++) {
                const optgroupHeader = headersMap[i];

                if (optgroupHeader && optgroupHeader !== lastHeaderName) {
                    lastHeaderName = optgroupHeader;

                    currentOptgroup = document.createElement("optgroup");
                    currentOptgroup.label = optgroupHeader;

                    selectElement.appendChild(currentOptgroup);
                }

                const option = document.createElement("option");
                option.text = `${i + 1}/${maxPage}`;
                option.value = (i + 1).toString();

                if (currentOptgroup) {
                    currentOptgroup.appendChild(option);
                } else {
                    selectElement.appendChild(option);
                }
            }
        });

        SetPage(currentPage, false);
        document.querySelector('.fullscreen-btn-on').removeAttribute("disabled");
    } catch (err) {
        console.error('Failed to load FB2:', err);
    }
}

const apiTts = axios.create({
    timeout: 1000 * 60 * 5
});

apiTts.interceptors.response.use(
    (response) => response,
    async (error) => {
        const {config} = error;

        if (!config) return Promise.reject(error);

        config.__retryCount = config.__retryCount || 1;

        if (config.__retryCount >= 3 || error.response.status === 404) {
            return Promise.reject(error);
        }

        config.__retryCount += 1;
        console.warn(`[Axios Retry] Ошибка при загрузке файла. Попытка #${config.__retryCount}...`);

        const delay = Math.pow(2, config.__retryCount - 1) * 1000;
        await new Promise(resolve => setTimeout(resolve, delay));

        return apiTts(config);
    }
);

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
    updateSliderFill(0);
    document.querySelector('.audio-progress-bar').disabled = true;
    document.querySelector(".audio-controller > button").disabled = true;
    document.querySelector(".audio-controller").setAttribute('disabled', '');

    document.querySelector(".audio-controller > button").classList.remove('audio-play-btn');
    document.querySelector(".audio-controller > button").classList.remove('audio-pause-btn');
    document.querySelector(".audio-controller > button").classList.add('audio-loading-btn');
    document.querySelector(".audio-controller > button").classList.remove('audio-error-btn');

    document.querySelector('.fb2-canvas-container').innerHTML = pages[currentPage - 1];

    document.querySelector('.audio-speed-select').onchange = (e) => {
        if (currentAudio) {
            currentAudio.playbackRate = parseFloat(e.target.value);
        }
    };

    document.querySelector('.audio-progress-bar').oninput = (e) => {
        if (!currentAudio.duration) {
            e.preventDefault();
            return;
        }
        currentAudio.currentTime = (e.target.value / 100) * currentAudio.duration;
        updateSliderFill(e.target.value);
        document.querySelector(`p[tts-id='${currentAudioTtsId}']`).style.backgroundSize = e.target.value + '%';
    };

    Array.from(document.getElementsByClassName("tts-btn")).forEach(f => {
        f.onclick = async (event) => {
            if (isTtsLogicPlay) return;
            isTtsLogicPlay = true;

            try {
                if (event.target.classList.contains('tts-loading') || event.target.classList.contains('tts-error')) {
                    cancelAnimationFrame(animationFrameId);
                    updateSliderFill(0);
                    document.querySelector('.audio-progress-bar').disabled = true;
                    document.querySelector(".audio-controller > button").disabled = true;
                    document.querySelector(".audio-controller").removeAttribute('disabled');
                    return;
                }

                if (event.target.classList.contains('tts-pause') && currentAudio != null) {
                    cancelAnimationFrame(animationFrameId);
                    currentAudio.pause();
                    currentAudio.currentTime = Math.max(0, currentAudio.currentTime - 2);
                    setTtsControlBtnActiveClass(event.target, 'tts-play');
                    document.querySelector('.audio-progress-bar').disabled = false;
                    document.querySelector(".audio-controller > button").disabled = false;
                    document.querySelector(".audio-controller").removeAttribute('disabled');
                    trackTtsProgress();
                    return;
                }

                let ttsId = event.target.getAttribute('tts-id');

                if (event.target.classList.contains('tts-play') && currentAudio != null && currentAudioTtsId === ttsId) {
                    currentAudio.play();
                    setTtsControlBtnActiveClass(event.target, 'tts-pause');
                    document.querySelector('.audio-progress-bar').disabled = false;
                    document.querySelector(".audio-controller > button").disabled = false;
                    document.querySelector(".audio-controller").removeAttribute('disabled');
                    return;
                }

                cancelAnimationFrame(animationFrameId);
                setTtsControlBtnActiveClass(event.target, 'tts-loading');
                document.querySelector('.audio-progress-bar').disabled = true;
                document.querySelector(".audio-controller > button").disabled = true;
                document.querySelector(".audio-controller").removeAttribute('disabled');
                if (currentAudioTtsId != null) {
                    setTtsControlBtnActiveClass(document.querySelector(`.tts-btn[tts-id='${currentAudioTtsId}']`), 'tts-play');
                    document.querySelector(`p[tts-id='${currentAudioTtsId}']`).style.backgroundSize = 0 + '%';
                    currentAudio.pause();
                    currentAudioTtsId = null;
                    if (currentAudioUrl) URL.revokeObjectURL(currentAudioUrl);
                    currentAudioUrl = null;
                }

                currentAudio = new Audio();

                currentAudio.play().catch(() => {
                });

                currentAudioTtsId = ttsId;

                const response = await apiTts({
                    method: 'POST',
                    url: `/api/Tts/GetFile?bucketId=${bucketId}&fileId=${fileId}&ttsId=${ttsId}`,
                    data: {},
                    responseType: 'blob',
                    headers: {
                        'RequestVerificationToken': GetAntiForgeryToken()
                    },
                    withCredentials: true
                });

                currentAudioUrl = URL.createObjectURL(response.data);
                currentAudio.src = currentAudioUrl;
                currentAudio.load();

                currentAudio.playbackRate = parseFloat(document.querySelector('.audio-speed-select').value);

                currentAudio.onplay = () => {
                    animationFrameId = requestAnimationFrame(trackTtsProgress);
                };

                currentAudio.onended = () => {
                    currentAudioTtsId = null;
                    URL.revokeObjectURL(currentAudioUrl);
                    currentAudioUrl = null;
                    document.querySelector('.audio-progress-bar').disabled = true;
                    document.querySelector(".audio-controller > button").disabled = true;
                    document.querySelector(".audio-controller").setAttribute('disabled', '');
                    cancelAnimationFrame(animationFrameId);
                    document.querySelector(`p[tts-id='${ttsId}']`).style.backgroundSize = 0 + '%';
                    setTtsControlBtnActiveClass(event.target, 'tts-play');

                    if (document.querySelector('.audio-auto-next').checked) {
                        let nextBtn;

                        if (document.querySelector(`p[tts-id='${ttsId}']`).parentElement.tagName.toLocaleLowerCase() === 'section') {
                            nextBtn = document.querySelector(`p[tts-id='${ttsId}'] ~ p[tts-id] > button.tts-btn`);
                        } else {
                            const currentP = document.querySelector(`p[tts-id='${ttsId}']`);

                            if (currentP) {
                                const currentTopElement = currentP.closest('section > *');
                                const nextElement = currentTopElement ? currentTopElement.nextElementSibling : null;
                                nextBtn = nextElement ? nextElement.querySelector('button.tts-btn') || nextElement.closest('button.tts-btn') : null;
                            }
                        }
                        if (!nextBtn) {
                            document.querySelector(".next-page-controller")?.click();
                            document.querySelector("button.tts-btn")?.click();
                        } else {
                            const target = nextBtn.parentElement;
                            const main = document.querySelector('main');

                            if (target && main) {
                                const targetTop = target.getBoundingClientRect().top;
                                const mainTop = main.getBoundingClientRect().top;

                                const desiredDelta = targetTop - mainTop;

                                const maxRemainingScroll = main.scrollHeight - main.scrollTop - main.clientHeight;

                                const safeDelta = Math.min(desiredDelta, maxRemainingScroll);

                                main.scrollBy({
                                    top: Math.max(safeDelta, 0),
                                    behavior: 'smooth'
                                });
                            }

                            nextBtn?.click();
                        }
                    }
                };

                await currentAudio.play();

                document.querySelector('.audio-progress-bar').disabled = false;
                document.querySelector(".audio-controller > button").disabled = false;
                document.querySelector(".audio-controller").removeAttribute('disabled');

                document.querySelector(".audio-controller > button").onclick = () => {
                    event.target.click();
                };

                setTtsControlBtnActiveClass(event.target, 'tts-pause');
            } catch (e) {
                console.error(e);
                setTtsControlBtnActiveClass(event.target, 'tts-error');
                cancelAnimationFrame(animationFrameId);
                if (currentAudioTtsId) {
                    document.querySelector(`p[tts-id='${currentAudioTtsId}']`).style.backgroundSize = 0 + '%';
                }
                document.querySelector('.audio-progress-bar').disabled = true;
                document.querySelector(".audio-controller > button").disabled = true;
                document.querySelector(".audio-controller").setAttribute('disabled', '');

                throw e;
            } finally {
                isTtsLogicPlay = false;
            }
        };
    });
}

function trackTtsProgress() {
    if (currentAudioTtsId) {
        const percentage = (currentAudio.currentTime / currentAudio.duration) * 100;

        document.querySelector(`p[tts-id='${currentAudioTtsId}'`).style.backgroundSize = percentage + '%';

        updateSliderFill(percentage);

        if (!currentAudio.paused) {
            animationFrameId = requestAnimationFrame(trackTtsProgress);
        }
    }
}

function setTtsControlBtnActiveClass(el, className) {
    if (className === 'tts-play') {
        el.classList.add('tts-play');
        el.classList.remove('tts-pause');
        el.classList.remove('tts-loading');
        el.classList.remove('tts-error');

        document.querySelector(".audio-controller > button").classList.add('audio-play-btn');
        document.querySelector(".audio-controller > button").classList.remove('audio-pause-btn');
        document.querySelector(".audio-controller > button").classList.remove('audio-loading-btn');
        document.querySelector(".audio-controller > button").classList.remove('audio-error-btn');
    } else if (className === 'tts-pause') {
        el.classList.remove('tts-play');
        el.classList.add('tts-pause');
        el.classList.remove('tts-loading');
        el.classList.remove('tts-error');

        document.querySelector(".audio-controller > button").classList.remove('audio-play-btn');
        document.querySelector(".audio-controller > button").classList.add('audio-pause-btn');
        document.querySelector(".audio-controller > button").classList.remove('audio-loading-btn');
        document.querySelector(".audio-controller > button").classList.remove('audio-error-btn');
    } else if (className === 'tts-loading') {
        el.classList.remove('tts-play');
        el.classList.remove('tts-pause');
        el.classList.add('tts-loading');
        el.classList.remove('tts-error');

        document.querySelector(".audio-controller > button").classList.remove('audio-play-btn');
        document.querySelector(".audio-controller > button").classList.remove('audio-pause-btn');
        document.querySelector(".audio-controller > button").classList.add('audio-loading-btn');
        document.querySelector(".audio-controller > button").classList.remove('audio-error-btn');
    } else if (className === 'tts-error') {
        el.classList.remove('tts-play');
        el.classList.remove('tts-pause');
        el.classList.remove('tts-loading');
        el.classList.add('tts-error');

        document.querySelector(".audio-controller > button").classList.remove('audio-play-btn');
        document.querySelector(".audio-controller > button").classList.remove('audio-pause-btn');
        document.querySelector(".audio-controller > button").classList.remove('audio-loading-btn');
        document.querySelector(".audio-controller > button").classList.add('audio-error-btn');
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

    convertTitlesToHtmlHeaders(doc);

    const body = doc.querySelector('body');

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

    convertNode(body, imageMap);

    pages = pages.filter(page => page !== '');
}

function convertTitlesToHtmlHeaders(doc) {
    const ns = doc.documentElement.namespaceURI;

    const bodies = doc.getElementsByTagNameNS(ns, 'body');
    if (bodies.length > 0) {
        const body = bodies[0];
        const bodyTitles = Array.from(body.childNodes).filter(node => node.localName === 'title');

        bodyTitles.forEach(title => {
            const h1 = doc.createElementNS(ns, 'h1');

            while (title.firstChild) {
                h1.appendChild(title.firstChild);
            }

            body.replaceChild(h1, title);
        });
    }

    const sections = Array.from(doc.getElementsByTagNameNS(ns, 'section'));

    sections.forEach(section => {
        const sectionTitle = Array.from(section.childNodes).find(node => node.localName === 'title');

        if (sectionTitle) {
            const h2 = doc.createElementNS(ns, 'h2');

            while (sectionTitle.firstChild) {
                h2.appendChild(sectionTitle.firstChild);
            }

            section.replaceChild(h2, sectionTitle);
        }
    });
}

function convertNode(node, imageMap, isPage = true) {
    if (node.nodeType === Node.TEXT_NODE) {
        return escapeHtml(node.nodeValue);
    }
    if (node.nodeType !== Node.ELEMENT_NODE) return '';

    const tag = node.localName;

    const getChildrenHtml = () => Array.from(node.childNodes).map(n => convertNode(n, imageMap, isPage)).join('');

    switch (tag) {
        case 'p': {
            const hasTtsId = node.hasAttribute('tts-id');
            let ttsBtn = '';
            let ttsAttr = '';

            if (hasTtsId) {
                const ttsId = node.getAttribute('tts-id');
                ttsAttr = ` tts-id="${escapeHtml(ttsId)}"`;
                ttsBtn = `<button class="tts-btn tts-play" tts-id="${escapeHtml(ttsId)}"></button>`;
            }

            return `<p${ttsAttr}>${ttsBtn}${getChildrenHtml()}</p>`;
        }

        case 'poem':
            const isLast = !node.nextElementSibling || node.nextElementSibling.localName !== 'poem';
            return `<i class="poem">${getChildrenHtml()}</i> ${isLast ? '<br />' : '<br /><br />'} `;

        case 'stanza':
            return getChildrenHtml();

        case 'v': {
            const content = getChildrenHtml();
            const isLast = !node.nextElementSibling || node.nextElementSibling.localName !== 'v';
            return `<span>${content}</span>${isLast ? '' : '<br />'}`;
        }

        case 'cite':
            return ` <span class="cite">— ${getChildrenHtml()}</span>`;

        case 'section': {
            const content = getChildrenHtml();
            if (isPage) {
                pages.push(`<section>${content}</section>`);
            }
            return `<section>${content}</section>`;
        }

        case 'title': {
            const level = node.closest && node.closest('section') ? 2 : 1;
            return `<h${level}>${getChildrenHtml()}</h${level}>`;
        }

        case 'h1':
            return `<h1>${getChildrenHtml()}</h1>`;
        case 'h2':
            return `<h2>${getChildrenHtml()}</h2>`;

        case 'image':
        case 'img': {
            const href = node.getAttribute('l:href') || node.getAttribute('href') || '';
            if (href.startsWith('#')) {
                const src = imageMap.get(href.slice(1));
                if (!isPage) {
                    return `<span class="fb2-image"><img src="${escapeHtml(src)}" class="fb2-image" /></span>`;
                } else {
                    return `<span class="fb2-image">
                                <details style="display:inline-block;">
                                    <summary><span>Image (click)</span></summary>
                                    <img onclick='this.parentNode.removeAttribute("open")' src="${escapeHtml(src)}" class="fb2-image" alt="Image missing"/>
                                </details>
                            </span>`;
                }
            }
            return `<span>[Image missing: ${escapeHtml(href)}]</span>`;
        }

        case 'empty-line':
            return '<br />';

        case 'emphasis':
            return ` <em>${getChildrenHtml()}</em> `;
        case 'strong':
            return `<strong>${getChildrenHtml()}</strong>`;

        case 'a':
            if (node.getAttribute('type') === 'note') {
                const rawHref = node.getAttribute('l:href') || node.getAttribute('href') || '';
                const noteKey = rawHref.replace('#', '');
                const onClickHtml = noteMap.get(noteKey) || '';
                const safeJson = escapeHtml(JSON.stringify(onClickHtml));
                return `<button type="button" class="note-btn" onclick="openModalClicked(JSON.parse(this.getAttribute('data-note')))" data-note="${safeJson}"></button>`;
            }
            return '';

        default:
            return getChildrenHtml();
    }
}

function escapeHtml(str) {
    return str?.trim().replaceAll('\n', '').replaceAll(/[&<>"']/g, m =>
        ({'&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;'}[m])
    ) || '';
}
