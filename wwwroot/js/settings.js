function setSettings(container, settings) {
    if (settings.darkView !== undefined) {
        const darkViewBtn = document.createElement('button');

        darkViewBtn.classList.add('dark-view-toggle');

        container.appendChild(darkViewBtn);

        document.querySelector(`#${settings.darkView.elementId}`).classList.add('filter-transition');

        darkViewBtn.onclick = () => {
            document.querySelector(`#${settings.darkView.elementId}`).classList.toggle(settings.darkView.filter);
        }
    }
}
