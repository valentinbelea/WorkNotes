// Dashboard behaviour only: all appearance comes from CSS classes rendered by the server.
// Inserts the server-rendered new-note card, removes it on Cancel, and opens saved notes.
// Positions come from the grid; this module never moves cards or writes inline styles.

// Single entry point for opening a note, used by the Open button and by double-click.
export function openNoteEditor(noteId) {
    // TODO: open the note's text editor when it exists (for example navigate to its editor page).
    document.dispatchEvent(new CustomEvent("worknotes:open-note", { detail: { noteId } }));
}

export function initializeNotesBoard(board) {
    const template = document.getElementById("new-note-template");
    const newNoteButton = document.querySelector("[data-note-new]");

    const focusTitle = card => card.querySelector("[data-new-note-title]")?.focus();

    newNoteButton?.addEventListener("click", event => {
        const open = board.querySelector("[data-new-note]");
        if (open) { event.preventDefault(); focusTitle(open); return; }
        if (!template) return; // no template: the link opens ?new=true
        event.preventDefault();
        board.prepend(template.content.firstElementChild.cloneNode(true));
        focusTitle(board.firstElementChild);
    });

    const cancel = card => {
        card.remove();
        newNoteButton?.focus();
    };

    board.addEventListener("click", event => {
        const cancelLink = event.target.closest("[data-new-note-cancel]");
        if (cancelLink) {
            event.preventDefault();
            cancel(cancelLink.closest("[data-new-note]"));
            return;
        }
        const openButton = event.target.closest("[data-note-open]");
        if (openButton) openNoteEditor(openButton.closest("[data-note-id]").dataset.noteId);
    });

    board.addEventListener("keydown", event => {
        const card = event.target.closest("[data-new-note]");
        if (card && event.key === "Escape") cancel(card);
    });

    board.addEventListener("dblclick", event => {
        const card = event.target.closest(".note-card[data-note-id]");
        if (card && !event.target.closest("button, a, input, select, textarea")) openNoteEditor(card.dataset.noteId);
    });
}

document.querySelectorAll("[data-notes-board]").forEach(initializeNotesBoard);
