function toLocalDateTime() {
    document.querySelectorAll(".to-local-date-time").forEach(el => {
        el.classList.remove("to-local-date-time");

        const date = new Date(el.getAttribute("utc"));

        const dd = String(date.getDate()).padStart(2, '0');
        const MM = String(date.getMonth() + 1).padStart(2, '0');
        const yyyy = date.getFullYear();
        const HH = String(date.getHours()).padStart(2, '0');
        const mm = String(date.getMinutes()).padStart(2, '0');
        const ss = String(date.getSeconds()).padStart(2, '0');

        el.innerHTML = `${dd}.${MM}.${yyyy} ${HH}:${mm}:${ss}`;
    });
}

function toLocalDate() {
    document.querySelectorAll(".to-local-date").forEach(el => {
        el.classList.remove("to-local-date");

        const date = new Date(el.getAttribute("utc"));

        const dd = String(date.getDate()).padStart(2, '0');
        const MM = String(date.getMonth() + 1).padStart(2, '0');
        const yyyy = date.getFullYear();

        el.innerHTML = `${dd}.${MM}.${yyyy}`;
    });
}

function toLocalTime() {
    document.querySelectorAll(".to-local-time").forEach(el => {
        el.classList.remove("to-local-time");

        const date = new Date(el.getAttribute("utc"));

        const HH = String(date.getHours()).padStart(2, '0');
        const mm = String(date.getMinutes()).padStart(2, '0');
        const ss = String(date.getSeconds()).padStart(2, '0');

        el.innerHTML = `${HH}:${mm}:${ss}`;
    });
}
