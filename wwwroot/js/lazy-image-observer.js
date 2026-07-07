document.addEventListener("DOMContentLoaded", function () {
    const lazyImages = document.querySelectorAll(".lazy-image");

    const imageObserver = new IntersectionObserver((entries, _) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                const img = entry.target;

                img.src = img.dataset.src;

                img.onload = function () {
                    img.classList.add("lazy-image-loaded");
                };

                imageObserver.unobserve(img);
            }
        });
    }, {
        rootMargin: "0px 0px -100px 0px"
    });

    lazyImages.forEach(image => imageObserver.observe(image));
});
