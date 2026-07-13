const fullContentEl = document.getElementById("full_content");
const emptyContentEl = document.getElementById("empty_content");

window.addEventListener("load", () => {
    Array.from(document.querySelectorAll(".delete-row-btn")).forEach(f => f.onclick = async () => await Delete(f.dataset.id, f.dataset.name));
});

async function Delete(id, name) {
    if (!confirm(`Delete ${name}?`.trim())) return;

    document.querySelectorAll(".delete-row-btn").forEach(el => el.disabled = true);
    document.querySelector(`#RL_${id} > .event-output > .delete-row-btn`).classList.remove("remove");
    document.querySelector(`#RL_${id} > .event-output > .delete-row-btn`).classList.add("waiting");

    requestQueue.enqueue({
        method: 'POST',
        url: url,
        data: {
            Id: id
        },
        options: {
            headers: {
                'RequestVerificationToken': GetAntiForgeryToken()
            },
            withCredentials: true
        },
        beforeSend: () => {
        }
    })
        .then(_ => {
            document.querySelector(`#RL_${id} > .event-output > .delete-row-btn`).classList.remove("waiting");
            document.querySelector(`#RL_${id} > .event-output > .delete-row-btn`).classList.add("success");

            fileCacheService.delete(id);

            setTimeout(() => {
                document.getElementById(`RL_${id}`).remove();
                CheckOnEmpty();
                document.querySelectorAll(".delete-row-btn").forEach(el => el.disabled = false);
            }, 1000);
        })
        .catch(_ => {
            document.querySelector(`#RL_${id} > .event-output > .delete-row-btn`).classList.remove("waiting");
            document.querySelector(`#RL_${id} > .event-output > .delete-row-btn`).classList.add("error");

            setTimeout(() => {
                document.querySelector(`#RL_${id} > .event-output > .delete-row-btn`).classList.remove("error");
                document.querySelector(`#RL_${id} > .event-output > .delete-row-btn`).classList.add("remove");
                document.querySelectorAll(".delete-row-btn").forEach(el => el.disabled = false);
            }, 1000);
        });
}

function CheckOnEmpty() {
    document.querySelectorAll("#full_content > details").forEach(el => {
        if (el.querySelectorAll(lineSelector).length === 0) {
            el.style.display = "none";

            if (!document.querySelectorAll("#full_content > details").values()
                .some(f => f.open && f.style.display !== "none")) {
                document.querySelector("#full_content > details").open = true;
            }
        }
    });

    if (document.querySelectorAll(lineSelector).length === 0) {
        fullContentEl.style.display = "none";
        emptyContentEl.style.display = "flex";
    } else {
        fullContentEl.style.display = "flex";
        emptyContentEl.style.display = "none";
    }
}
