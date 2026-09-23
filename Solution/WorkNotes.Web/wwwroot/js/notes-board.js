// A stable ID, not the note's index, determines its position inside the cell.
// No text, ordering, selection or persisted data is changed by this module.
export function initializeNotesBoards(root = document) {
    root.querySelectorAll(".note-cell[data-note-id]").forEach(cell => {
        const id = cell.dataset.noteId;
        const card = cell.querySelector(".note-card");
        if (!id || !card) return;
        let hash = 2166136261;
        for (const character of id) {
            hash = Math.imul(hash ^ character.charCodeAt(0), 16777619) >>> 0;
        }
        card.style.setProperty("--note-x", ((hash % 13) - 6) + "px");
        card.style.setProperty("--note-y", (((hash >>> 8) % 13) - 6) + "px");
        card.style.setProperty("--note-rotation", ((((hash >>> 16) % 17) - 8) / 10) + "deg");
    });
}
initializeNotesBoards();
