(() => {
    const menu = document.getElementById("main-menu");
    const openButton = document.querySelector("[data-menu-open]");
    const closeButton = menu?.querySelector("[data-menu-close]");
    if (!menu || !openButton || !closeButton || typeof menu.showModal !== "function") return;

    // Without JavaScript the open dialog remains an ordinary navigation section.
    menu.removeAttribute("open");
    menu.dataset.drawer = "";
    openButton.hidden = false;
    closeButton.hidden = false;

    openButton.addEventListener("click", () => {
        menu.showModal();
        openButton.setAttribute("aria-expanded", "true");
    });
    closeButton.addEventListener("click", () => menu.close());
    menu.addEventListener("close", () => {
        openButton.setAttribute("aria-expanded", "false");
        openButton.focus();
    });
    menu.addEventListener("click", event => {
        if (event.target !== menu) return;
        const bounds = menu.getBoundingClientRect();
        if (event.clientX < bounds.left || event.clientX > bounds.right ||
            event.clientY < bounds.top || event.clientY > bounds.bottom) menu.close();
    });
})();

