window.onload = () => {
    const header = document.querySelector('header');
    const main = document.querySelector('main');
    const footer = document.querySelector('footer');

    let reservedSize = header.clientHeight;
    
    if (footer.childNodes.length > 0 &&
        footer.innerHTML.replace(/\s+/g, '').length > 0) {
        reservedSize += footer.clientHeight;
        footer.style.visibility = "visible";
    } else {
        footer.style.visibility = "hidden";
        main.style.marginBottom = "0";
    }

    main.style.minHeight = `calc(100vh - ${reservedSize}px)`;
};