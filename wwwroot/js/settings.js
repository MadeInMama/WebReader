function setSettings(container, settings) {
    if (settings.darkView !== undefined) {
        let darkViewBtn = document.createElement('button');
        darkViewBtn.classList.add('dark-view-toggle');
        container.appendChild(darkViewBtn);
        darkViewBtn.onclick = () => {
            document.querySelector(`#${settings.darkView.elementId}`).classList.toggle(settings.darkView.filter);
        }
    }
}
