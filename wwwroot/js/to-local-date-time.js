function convertUtcToLocal() {
    document.querySelectorAll("[utc]").forEach(el => {
        if (!new Set(el.classList).intersection(new Set(["to-local-date-time",
            "to-local-date",
            "to-local-time",
            "to-local-time-short",
            "to-local-date-text"]))) {
            el.innerHTML = utcAttr;
            return;
        }

        const utcAttr = el.getAttribute("utc");
        const date = new Date(utcAttr);

        if (isNaN(date.getTime())) {
            el.innerHTML = utcAttr;
            return;
        }

        const pad = num => String(num).padStart(2, '0');

        const dd = pad(date.getDate());
        const MM = pad(date.getMonth() + 1);
        const yyyy = date.getFullYear();
        const HH = pad(date.getHours());
        const mm = pad(date.getMinutes());
        const ss = pad(date.getSeconds());

        const formats = {
            "to-local-date-time": `${dd}.${MM}.${yyyy} ${HH}:${mm}:${ss}`,
            "to-local-date": `${dd}.${MM}.${yyyy}`,
            "to-local-time": `${HH}:${mm}:${ss}`,
            "to-local-time-short": `${HH}:${mm}`,
            "to-local-date-text": date.toLocaleDateString(undefined, {day: "numeric", month: "long", year: "numeric"}),
            "to-local-date-text-time": `${date.toLocaleDateString(undefined, {
                day: "numeric",
                month: "long",
                year: "numeric"
            })} ${HH}:${mm}:${ss}`
        };

        const activeClass = Object.keys(formats).find(className => el.classList.contains(className));

        if (activeClass) el.innerHTML = formats[activeClass];
        else el.innerHTML = utcAttr;

        el.classList.remove(activeClass);
    });
}
