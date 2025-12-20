const targetElement = document.body;

const EventTypes = {INFINITE: 1, FROM_MILLIS: 2};

const customEvent = new CustomEvent('SendCurrPageEvent', {
    detail: {
        message: 'Message Sent',
        type: EventTypes.FROM_MILLIS,
        millis: 3000,
    },
    bubbles: true,
    cancelable: true
});

let eventTimeout;

targetElement.addEventListener('SendCurrPageEvent', (event) => {
    Array.from(document.getElementsByClassName("event-output")).forEach(el => {
        el.style.opacity = '1';
        el.innerHTML = event.detail.message;
        clearTimeout(eventTimeout);
        if (event.detail.type === EventTypes.FROM_MILLIS) {
            eventTimeout = setTimeout(() => el.style.opacity = '0', event.detail.millis);
        }
    });
});
