const fullContentEl1 = document.getElementById("full_content");
const emptyContentEl1 = document.getElementById("empty_content");

function CheckOnEmpty() {
    document.querySelectorAll("#full_content > details").forEach(el => {
        if (el.classList.contains("ignore-empty-row-check")) return;

        if (el.querySelectorAll(lineSelector).length === 0) {
            el.style.display = "none";

            if (!document.querySelectorAll("#full_content > details").values()
                .some(f => f.open && f.style.display !== "none")) {
                document.querySelector("#full_content > details").open = true;
            }
        }
    });

    if (document.querySelectorAll(lineSelector).length === 0 &&
        document.querySelector(".ignore-empty-row-check") === undefined) {
        fullContentEl1.style.display = "none";
        emptyContentEl1.style.display = "flex";
    } else {
        fullContentEl1.style.display = "flex";
        emptyContentEl1.style.display = "none";
    }
}
