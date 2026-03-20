const fullContentEl = document.getElementById("full_content");
const emptyContentEl = document.getElementById("empty_content");

async function Delete(id, name) {
    let confirmRes = confirm(`Delete ${name}?`.trim());

    if (!confirmRes) return;

    document.querySelectorAll(".remove").forEach(el => el.disabled = true);
    document.querySelector(`#RL_${id} > .event-output > button`).classList = 'waiting';

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
            document.querySelector(`#RL_${id} > .event-output > button`).classList = 'success';

            setTimeout(() => {
                document.getElementById(`RL_${id}`).remove();
                CheckOnEmpty();
                document.querySelectorAll(".remove").forEach(el => el.disabled = false);
            }, 1000);
        })
        .catch(_ => {
            document.querySelector(`#RL_${id} > .event-output > button`).classList = 'error';

            setTimeout(() => {
                document.querySelector(`#RL_${id} > .event-output > button`).classList = 'remove';
                document.querySelectorAll(".remove").forEach(el => el.disabled = false);
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
