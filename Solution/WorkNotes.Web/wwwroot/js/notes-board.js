// Dashboard behaviour only: all appearance comes from CSS classes rendered by the server.
// Switches the board, inserts the server-rendered new-note card, removes it on Cancel, and opens saved notes.
// Positions come from the grid; this module never moves cards or writes inline styles.

// Single entry point for opening a note, used by the Open button and by double-click.
export function openNoteEditor(noteId) {
    // TODO: open the note's text editor when it exists (for example navigate to its editor page).
    document.dispatchEvent(new CustomEvent("worknotes:open-note", { detail: { noteId } }));
}

export function initializeDashboard(dashboard) {
    const template = document.getElementById("new-note-template");
    const target = dashboard.querySelector("[data-new-note-target]");
    const newNoteButton = dashboard.querySelector("[data-note-new]");
    const focusTitle = card => card.querySelector("[data-new-note-title]")?.focus();

    dashboard.querySelector("[data-board-switch]")?.addEventListener("change", event => event.target.form.requestSubmit());

    newNoteButton?.addEventListener("click", event => {
        const open = dashboard.querySelector("[data-new-note]");
        if (open) { event.preventDefault(); focusTitle(open); return; }
        if (!template || !target) return; // the link opens ?new=true instead
        event.preventDefault();
        target.prepend(template.content.firstElementChild.cloneNode(true));
        focusTitle(target.firstElementChild);
    });

    const cancel = card => {
        card.remove();
        newNoteButton?.focus();
    };

    dashboard.addEventListener("click", event => {
        const cancelLink = event.target.closest("[data-new-note-cancel]");
        if (cancelLink) {
            event.preventDefault();
            cancel(cancelLink.closest("[data-new-note]"));
            return;
        }
        const openButton = event.target.closest("[data-note-open]");
        if (openButton) openNoteEditor(openButton.closest("[data-note-id]").dataset.noteId);
    });

    dashboard.addEventListener("keydown", event => {
        const card = event.target.closest("[data-new-note]");
        if (card && event.key === "Escape") cancel(card);
    });

    dashboard.addEventListener("dblclick", event => {
        const card = event.target.closest(".note-card[data-note-id]");
        if (card && !event.target.closest("button, a, input, select, textarea")) openNoteEditor(card.dataset.noteId);
    });
}

document.querySelectorAll("[data-notes-dashboard]").forEach(initializeDashboard);
