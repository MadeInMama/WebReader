const targetElement = document.body;

const EventTypes = {INFINITE: 1, FROM_MILLIS: 2};

const customSendCurrPageEvent = new CustomEvent('SendCurrPageEvent', {
    detail: {
        message: 'Message Sent',
        type: EventTypes.FROM_MILLIS,
        millis: 3000,
    },
    bubbles: true,
    cancelable: true
});

let sendCurrPageEventTimeout;

targetElement.addEventListener('SendCurrPageEvent', (event) => {
    Array.from(document.getElementsByClassName("event-output")).forEach(el => {
        el.style.opacity = '1';
        el.innerHTML = event.detail.message;
        clearTimeout(sendCurrPageEventTimeout);
        if (event.detail.type === EventTypes.FROM_MILLIS) {
            sendCurrPageEventTimeout = setTimeout(() => el.style.opacity = '0', event.detail.millis);
        }
    });
});

setInterval(() => {
    Array.from(document.getElementsByClassName("event-output")).forEach(value => {
        value.childNodes.forEach(node => {
            if (node.classList.contains('progress-bar-inner-linear')) {
                node.style.width = `${60 * node.getAttribute("progress") / 100}%`;
            } else if (node.classList.contains('progress-bar-inner-text')) {
                node.style.width = '100%';
                node.innerHTML = node.getAttribute("progress");
            }
        });
    });
}, 50);

Array.from(document.getElementsByClassName("event-output")).forEach(value => {
    value.onclick = (e) => {
        e.currentTarget.childNodes.forEach(node => {
            if (node.classList.contains('progress-bar-inner-linear')) {
                node.classList.remove('progress-bar-inner-linear');
                node.classList.add('progress-bar-inner-text');
                node.style.width = `${60 * node.getAttribute("progress") / 100}%`;
            } else if (node.classList.contains('progress-bar-inner-text')) {
                node.classList.remove('progress-bar-inner-text');
                node.classList.add('progress-bar-inner-linear');
                node.style.width = '100%';
                node.innerHTML = node.getAttribute("progress");
            }
        });
    };
});
