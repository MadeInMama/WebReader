const modal = document.getElementById('custom-modal');
const modalContent = document.getElementById('custom-modal-content');

function openModalClicked(html) {
    onNoScrollApplied();

    modal.onscroll = function (e) {
        e.preventDefault();
    }
    modal.ontouchmove = function (e) {
        e.preventDefault();
    }

    modal.style.left = `calc(50% - ${getBrowserScrollbarWidth() / 2}px)`;

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

    onNoScrollRemoved();

    modal.onscroll = function (e) {
        return true;
    }
    modal.ontouchmove = function (e) {
        return true;
    }
}
