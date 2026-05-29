function convertUtcToLocal() {
    document.querySelectorAll("[utc]").forEach(el => {
        const utcAttr = el.getAttribute("utc").replace(/['"]/g, '');
        const date = new Date(utcAttr);

        if (isNaN(date.getTime())) return;

        const timeOptions = {hourCycle: 'h23', hour: "2-digit", minute: "2-digit"};

        const formats = {
            "to-local-date-time": {
                day: "2-digit",
                month: "2-digit",
                year: "numeric", ...timeOptions,
                second: "2-digit"
            },
            "to-local-date": {day: "2-digit", month: "2-digit", year: "numeric"},
            "to-local-time": {...timeOptions, second: "2-digit"},
            "to-local-date-text": {day: "numeric", month: "long", year: "numeric"},
            "to-local-time-short": timeOptions
        };

        const activeClass = Object.keys(formats).find(className => el.classList.contains(className));

        if (activeClass) {
            el.innerHTML = new Intl.DateTimeFormat(navigator.language, formats[activeClass]).format(date);
            el.classList.remove(activeClass);
        }
    });
}
