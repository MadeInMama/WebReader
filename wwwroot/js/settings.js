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

    if (settings.backgroundCover !== undefined && settings.backgroundCover === true) {
        const backgroundCoverContainer = document.createElement('div');
        const backgroundCover = document.createElement('div');

        backgroundCoverContainer.classList.add('background-cover-container');
        backgroundCover.classList.add('background-cover');

        backgroundCoverContainer.appendChild(backgroundCover);
        document.body.prepend(backgroundCoverContainer);

        const backgroundCoverBtn = document.createElement('button');
        backgroundCoverBtn.classList.add('background-cover-toggle');

        container.appendChild(backgroundCoverBtn);

        backgroundCoverBtn.onclick = () => {
            document.querySelector('.background-cover-container').classList.toggle('opacity-on');
        }
    }
}
