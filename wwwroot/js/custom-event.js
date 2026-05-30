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

targetElement.addEventListener('SendCurrPageEvent', (e) => {
    Array.from(document.getElementsByClassName("event-output")).forEach(el => {
        el.style.opacity = '1';
        el.innerHTML = e.detail.message;
        clearTimeout(sendCurrPageEventTimeout);
        if (e.detail.type === EventTypes.FROM_MILLIS) {
            sendCurrPageEventTimeout = setTimeout(() => el.style.opacity = '0', e.detail.millis);
        }
    });
});

Array.from(document.getElementsByClassName("event-output")).forEach(value => {
    value.onclick = (e) => {
        Array.from(e.currentTarget.children).forEach(node => {
            if (node.classList.contains('progress-bar-inner-linear')) {
                node.style.visibility = node.style.visibility === 'hidden' ? 'visible' : 'hidden';
            }

            if (node.classList.contains('progress-bar-inner-text')) {
                node.style.visibility = node.style.visibility === 'hidden' ? 'visible' : 'hidden';
            }
        });
    };
});
