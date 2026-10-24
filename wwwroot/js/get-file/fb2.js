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
                    currentAudio = null;
                    currentAudioTtsId = null;
                    currentAudioUrl = null;
                }

                URL.revokeObjectURL(currentAudioUrl);

                currentAudioTtsId = ttsId;

                const response = await axios({
                    method: 'POST',
                    url: `/api/Tts/GetFile?bucketId=${bucketId}&fileId=${fileId}&ttsId=${ttsId}`,
                    data: {},
                    responseType: 'blob',
                    headers: {
                        'RequestVerificationToken': GetAntiForgeryToken()
                    },
                    withCredentials: true
                });

                const audioBlob = new Blob([response.data], {type: 'audio/ogg'});

                currentAudioUrl = URL.createObjectURL(audioBlob);

                currentAudio = new Audio(currentAudioUrl);

                currentAudio.playbackRate = parseFloat(document.querySelector('.audio-speed-select').value);

                currentAudio.addEventListener('play', () => {
                    animationFrameId = requestAnimationFrame(trackTtsProgress);
                });

                currentAudio.addEventListener('ended', () => {
                    URL.revokeObjectURL(currentAudioUrl);
                    currentAudio = null;
                    currentAudioTtsId = null;
                    currentAudioUrl = null;
                    document.querySelector('.audio-progress-bar').disabled = true;
                    document.querySelector(".audio-controller > button").disabled = true;
                    document.querySelector(".audio-controller").setAttribute('disabled', '');
                    cancelAnimationFrame(animationFrameId);
                    document.querySelector(`p[tts-id='${ttsId}']`).style.backgroundSize = 0 + '%';
                    document.querySelector('.audio-progress-bar').oninput = () => {
                    };
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

                                main.scrollBy({
                                    top: targetTop - mainTop,
                                    behavior: 'smooth'
                                });
                            }

                            nextBtn?.click();
                        }
                    }
                });

                document.querySelector('.audio-progress-bar').oninput = (e) => {
                    if (!currentAudio.duration) return;
                    currentAudio.currentTime = (e.target.value / 100) * currentAudio.duration;
                    updateSliderFill(e.target.value);
                    document.querySelector(`p[tts-id='${currentAudioTtsId}']`).style.backgroundSize = e.target.value + '%';
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
                setTtsControlBtnActiveClass(event.target, 'tts-error');
                cancelAnimationFrame(animationFrameId);
                document.querySelector('.audio-progress-bar').oninput = () => {
                };
                document.querySelector(`p[tts-id='${currentAudioTtsId}]'`).style.backgroundSize = 0 + '%';
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
    if (currentAudio) {
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
    const children = () => Array.from(node.childNodes).map(n => convertNode(n, imageMap, isPage)).join('');

    switch (tag) {
        case 'p':
            let ttsBtn = null;
            const hasTtsId = node.hasAttribute('tts-id');
            if (hasTtsId) {
                //TODO: some text duplicates in log
                // console.log(node.textContent);
                ttsBtn = `<button class="tts-btn tts-play"
                            tts-id="${node.getAttribute('tts-id')}"
                            class="tts tts-play"></button>`;
            }

            return `<p ${hasTtsId ? "tts-id='" + node.getAttribute('tts-id') + "'" : ''}>
                        ${ttsBtn ? ttsBtn : ''}${children()}
                    </p>`;
        case 'cite':
            return `<p class="cite-author">${children()}</p>`;
        case 'section':
            if (isPage) pages.push(`<section>${children()}</section>`);
            return `<section>${children()}</section>`;
        case 'title': {
            const level = node.closest('section') ? 2 : 1;
            return `<h${level}>${children()}</h${level}>`;
        }
        case 'h1':
            return `<h1>${children()}</h1>`;
        case 'h2':
            return `<h2>${children()}</h2>`;
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
