const modal = document.getElementById('custom-modal');
const modalContent = document.getElementById('custom-modal-content');

function openModalClicked(html) {
    document.body.style.marginRight = `${getBrowserScrollbarWidth()}px`;
    modal.style.left = `calc(50% - ${getBrowserScrollbarWidth() / 2}px)`;
    document.querySelector('footer').style.left = `calc(50% - ${getBrowserScrollbarWidth() / 2}px)`;
    document.querySelector('html').classList.add('no-scroll');
    document.onscroll = function (e) {
        e.preventDefault();
    }
    document.ontouchmove = function (e) {
        e.preventDefault();
    }
    window.onscroll = function (e) {
        e.preventDefault();
    }
    window.ontouchmove = function (e) {
        e.preventDefault();
    }

    modalContent.innerHTML = '<button id="close-custom-modal-btn" class="close"></button>';

    modalContent.innerHTML += html;

    modal.showModal();

    document.getElementById('close-custom-modal-btn').onclick = () => closeModal();
}

modal.onclick = (event) => {
    if (event.target === modal) closeModal();
};

function closeModal() {
    modal.close();
    document.body.style.marginRight = '';
    document.querySelector('footer').style.left = '';
    document.querySelector('html').classList.remove('no-scroll');
    document.onscroll = function (e) {
        return true;
    }
    document.ontouchmove = function (e) {
        return true;
    }
    window.onscroll = function (e) {
        return true;
    }
    window.ontouchmove = function (e) {
        return true;
    }
}

function getBrowserScrollbarWidth() {
    return window.innerWidth - document.documentElement.clientWidth;
}
