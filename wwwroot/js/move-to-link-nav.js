const navElements = Object.freeze({
    Home: "#nav-home",
    Files: "#nav-files",
    Reading: "#nav-reading",
    ScheduledTasks: "#nav-scheduled-tasks",
    Account: "#nav-account",
    PreviousPart: "#nav-previous-part",
    NextPart: "#nav-next-part",
    Parts: "#nav-parts"
});

function moveToLinkNav(target) {
    const el = document.querySelector(target);


    if (el) {
        el.style.fontWeight = '700';

        el.scrollIntoView({
            behavior: 'auto',
            block: 'nearest',
            inline: 'center'
        });
    }
}
