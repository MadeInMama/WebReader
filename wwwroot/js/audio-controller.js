function updateSliderFill(percentage) {
    let progressBar = document.querySelector('.audio-progress-bar');

    progressBar.value = percentage;
    progressBar.style.background = `linear-gradient(to right, var(--error-foreground-color) 0%, var(--error-foreground-color) ${percentage}%, var(--sub-background-color) ${percentage}%, var(--sub-background-color) 100%)`;
}

updateSliderFill(0);
