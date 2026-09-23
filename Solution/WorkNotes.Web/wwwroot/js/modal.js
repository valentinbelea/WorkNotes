// Upgrades server-rendered overlays (dialog[data-modal][open]) to modal dialogs: focus stays inside,
// the page behind is inert, and Escape or a click on the overlay returns to the close URL.
document.querySelectorAll("dialog[data-modal][open]").forEach(dialog => {
    if (typeof dialog.showModal !== "function") return;
    dialog.close();
    dialog.showModal();
    const closeUrl = dialog.dataset.closeUrl;
    dialog.addEventListener("cancel", event => {
        event.preventDefault();
        if (closeUrl) window.location.assign(closeUrl);
    });
    dialog.addEventListener("click", event => {
        if (event.target === dialog && closeUrl) window.location.assign(closeUrl);
    });
});
