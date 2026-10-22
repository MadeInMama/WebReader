function updateSliderFill(percentage) {
    let progressBar = document.querySelector('.audio-progress-bar');

    progressBar.value = percentage;
    progressBar.style.background = `linear-gradient(to right, var(--success-foreground-color) 0%, var(--success-foreground-color) ${percentage}%, #ddd ${percentage}%, #ddd 100%)`;
}

updateSliderFill(0);
