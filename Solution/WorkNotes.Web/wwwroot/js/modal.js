// Upgrades server-rendered overlays (dialog[data-modal][open]) to modal dialogs: focus stays inside,
// the page behind is inert, and Escape or a click on the overlay returns to the close URL.
function upgrade(dialog) {
    if (typeof dialog.showModal !== "function") return;
    dialog.close();
    dialog.showModal();
    const closeUrl = dialog.dataset.closeUrl;
    dialog.addEventListener("cancel", event => {
        event.preventDefault();
        if (closeUrl) window.location.assign(closeUrl);
    });
    dialog.addEventListener("click", event => {
        // A minimized editor (note-editor.js) is no overlay: a click on it never closes it.
        if (event.target === dialog && closeUrl && dialog.matches(":modal")) window.location.assign(closeUrl);
    });
}

document.querySelectorAll("dialog[data-modal][open]").forEach(upgrade);
// An overlay added to the page later (the editor window opened from the board, notes-board.js) asks for the same.
document.addEventListener("modal:open", event => {
    if (event.target.matches?.("dialog[data-modal][open]")) upgrade(event.target);
});
