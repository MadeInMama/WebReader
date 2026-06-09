const modal = document.getElementById('custom-modal');
const modalContent = document.getElementById('custom-modal-content');

function openModalClicked(html) {
    onNoScrollApplied();

    modal.style.left = '50%';

    modalContent.innerHTML = '<button id="close-custom-modal-btn" class="close"></button>';

    modalContent.innerHTML += html;

    modal.showModal();

    document.getElementById('close-custom-modal-btn').onclick = () => closeModal();
}

modal.onclick = (e) => {
    if (e.target === modal) closeModal();
};

function closeModal() {
    modal.close();

    onNoScrollRemoved();
}
