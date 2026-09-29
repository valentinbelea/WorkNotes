// Dashboard behaviour only: all appearance comes from CSS classes rendered by the server.
// Switches the board, inserts the server-rendered new-note card, removes it on Cancel, renames titles in place
// (and shows the card's new last change), opens saved notes on double-click, swaps two cards of a month by drag
// and drop, and shows on the cards what the editor over the board saves. Positions come from the grid: cards move
// only in the DOM order, and no inline styles are written.
import { showStatusMessage } from "./status-messages.js";

// Single entry point for opening a note from the board: a card's Open and double-click. When the editor is already
// on the page (for example minimized), it opens the note in a tab (note-editor.js handles the note-editor:open event:
// it brings a minimized editor back and selects the note's tab when it is already open), keeping the other tabs.
// Otherwise the editor window with the note is fetched and put over this board, which is not read and drawn again as
// it would be for the link (/?note={id}, the editor over the board). The address still becomes the link's, as a new
// history entry, and closing the editor gives the board back (note-editor.js). When the window cannot be fetched, the
// link is followed. Without JavaScript the links work on their own.
export function openNote(id, href) {
    const handled = !document.dispatchEvent(new CustomEvent("note-editor:open", { detail: { id }, cancelable: true }));
    if (!handled) openEditorWindow(id, href);
}

// The editor window while it is being fetched: notes opened meanwhile wait for it and open in its tabs.
let editorWindow = null;

// Back or Forward to an address the page took without loading (a note opened over the board) loads it.
const reloadOnHistory = () => window.location.reload();

async function openEditorWindow(id, href) {
    if (editorWindow) {
        if (await editorWindow) openNote(id, href);
        return;
    }
    editorWindow = loadEditorWindow(id, href);
    await editorWindow;
    editorWindow = null;
}

// True once the window with the note is on the page; false when the link is followed instead.
async function loadEditorWindow(id, href) {
    const dashboard = document.querySelector("[data-notes-dashboard][data-editor-url]");
    try {
        if (!dashboard) throw new Error("board");
        const response = await fetch(`${dashboard.dataset.editorUrl}&note=${encodeURIComponent(id)}`, { headers: { Accept: "text/html" } });
        if (!response.ok) throw new Error(String(response.status));
        const fragment = document.createElement("template");
        fragment.innerHTML = await response.text();
        const dialog = fragment.content.querySelector("dialog.note-editor-dialog");
        const data = fragment.content.querySelector("#note-editor-data");
        if (!dialog || !data) throw new Error("fragment");
        window.history.pushState(null, "", href);
        // Back leaves the note for the board, as it did when the note was loaded as a page; note-editor.js asks first
        // when a tab has unsaved changes.
        window.addEventListener("popstate", reloadOnHistory);
        // Where the page puts the window it renders itself; modal.js makes it modal, then note-editor.js sets it up (the
        // module runs once: a window opened again after one closed is set up by startEditor).
        dashboard.after(dialog, data);
        dialog.dispatchEvent(new Event("modal:open", { bubbles: true }));
        const { startEditor } = await import("./note-editor.js");
        startEditor();
        return true;
    } catch {
        window.location.assign(href);
        return false;
    }
}

export function openNoteEditor(card) {
    const link = card.querySelector("[data-note-open]");
    if (link) openNote(card.dataset.noteId, link.href);
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
    });

    dashboard.addEventListener("keydown", event => {
        const card = event.target.closest("[data-new-note]");
        if (card && event.key === "Escape") cancel(card);
    });

    initializeRename(dashboard);
    followEditor(dashboard, initializeReorder(dashboard));

    // A plain click on Open goes through openNoteEditor too, so an open editor gets a new tab instead of a reload.
    dashboard.addEventListener("click", event => {
        const link = event.target.closest("[data-note-open]");
        if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        openNoteEditor(link.closest(".note-card"));
    });
    dashboard.addEventListener("dblclick", event => {
        const card = event.target.closest(".note-card[data-note-id]");
        if (card && !event.target.closest("button, a, input, select, textarea")) openNoteEditor(card);
    });
}

// The card's last change as the server formats it, hidden while it reads like the creation date. After a rename the card
// keeps its place; the board orders it by the new date on the next load.
function showLastChange(card, modified) {
    const date = card?.querySelector("[data-note-modified]");
    const time = date?.querySelector("time");
    if (!time || !modified) return;
    time.dateTime = modified.iso;
    time.textContent = modified.text;
    date.hidden = !modified.shown;
}

// The editor over the board (note-editor.js). After each save the note's card shows what was saved, as the page would
// draw it again (_NoteCard): the title, the type and the start of the text where they changed, the last change always.
// A save that moved the note to another month (the current one) moves the card to that month's list, in the order the
// server returned. The event is marked handled once the card shows the save, so the editor closes without loading the
// page again; a card the page does not have, or a month it does not show (a page from an earlier month), leaves it
// unhandled and closing the editor loads the board. When the editor closes, the focus goes to the card of its active note.
function followEditor(dashboard, reorder) {
    const cardOf = id => [...dashboard.querySelectorAll(".note-card[data-note-id]")].find(card => card.dataset.noteId === String(id));
    document.addEventListener("note-editor:saved", event => {
        const saved = event.detail;
        const card = saved ? cardOf(saved.id) : undefined;
        if (!card) return;
        showSaved(card, saved);
        if (saved.month && !reorder?.place(card.closest(".note-cell"), saved.month)) return;
        event.preventDefault();
    });
    document.addEventListener("note-editor:closed", event => {
        cardOf(event.detail?.id)?.querySelector("[data-note-open]")?.focus();
    });
}

// A saved note's title (in the rename field, the heading read out and the labels of Open and Delete), its type (the
// card's colour and the name read out) and the start of its text, each only where it changed, and its last change.
function showSaved(card, saved) {
    const title = saved.title ?? "";
    const field = card.querySelector("[data-note-title]");
    if (field && field.defaultValue !== title) {
        field.value = field.defaultValue = title;
        field.title = saved.name;
    }
    const name = card.querySelector("[data-note-name]");
    if (name && name.textContent !== saved.name) name.textContent = saved.name;
    for (const [selector, label] of [["[data-note-open]", saved.open], ["[data-note-delete]", saved.delete]]) {
        const link = card.querySelector(selector);
        if (!link || link.getAttribute("aria-label") === label) continue;
        link.setAttribute("aria-label", label);
        link.title = label;
    }
    if (saved.typeClass && !card.classList.contains(saved.typeClass)) {
        card.classList.remove("note-card--journal", "note-card--article");
        card.classList.add(saved.typeClass);
        const type = card.querySelector("[data-note-type]");
        if (type) type.textContent = saved.typeName;
    }
    const preview = card.querySelector("[data-note-preview]");
    const text = saved.preview ?? "";
    if (preview && preview.textContent !== text) {
        preview.textContent = text;
        preview.hidden = text === "";
    }
    showLastChange(card, saved.modified);
}

// Titles are renamed in place. Enter (or leaving the field with a changed title) saves in the background;
// Escape restores the saved title. Without JavaScript, Enter submits the same form and the board reloads.
function initializeRename(dashboard) {
    // The result of each rename goes to the layout's status region and stays until the user closes it.
    const messages = document.querySelector("[data-status-region]");

    async function rename(form) {
        const input = form.querySelector("[data-note-title]");
        if (!input || input.value === input.defaultValue || form.dataset.saving) return;
        form.dataset.saving = "true";
        try {
            const response = await fetch(form.action, { method: "POST", body: new FormData(form), headers: { Accept: "application/json" } });
            const body = await response.json().catch(() => ({}));
            if (response.ok) {
                // The stored title is normalized (trimmed); an empty title means "untitled".
                input.value = body.title ?? "";
                input.defaultValue = input.value;
                input.title = input.value || input.placeholder;
                showLastChange(form.closest(".note-card"), body.modified);
                showStatusMessage(messages, "success", body.message);
            } else {
                input.value = input.defaultValue;
                showStatusMessage(messages, "error", body.message ?? dashboard.dataset.renameFailed);
            }
        } catch {
            input.value = input.defaultValue;
            showStatusMessage(messages, "error", dashboard.dataset.renameFailed);
        } finally {
            delete form.dataset.saving;
        }
    }

    dashboard.addEventListener("submit", event => {
        const form = event.target.closest("[data-note-rename]");
        if (!form) return;
        event.preventDefault();
        rename(form);
    });
    dashboard.addEventListener("keydown", event => {
        const input = event.target.closest("[data-note-title]");
        if (input && event.key === "Escape") {
            input.value = input.defaultValue;
            input.blur();
        }
    });
    dashboard.addEventListener("focusout", event => {
        const input = event.target.closest("[data-note-title]");
        if (input) rename(input.form);
    });
}

// The owner changes the order of their notes by drag and drop. A card is picked up only by its tape and dropped on
// another of the owner's cards in the same month (the same list); the two swap places. The cells change places as soon
// as the drag is over and the swap is saved in the background, so the next drag can start at once, even while earlier
// swaps are still being saved: the saves go to the server one at a time, in the order of the swaps. Once every save
// has its answer, each list follows the order the server stored; a failed save shows an error message and its swap is
// undone. While dragging, CSS classes mark the picked-up card, the cells it can be dropped on and the cell under the
// pointer; a drop anywhere else is ignored and the card stays where it was. Every class is removed when the drag ends
// or is cancelled. No cell moves during a drag: the browser follows the dragged tape until dragend, and a tape moved
// on drop can lose its dragend (Firefox), so the swap waits for dragend.
// The result's place() moves the card of a note that a save in the editor moved to another month.
function initializeReorder(dashboard) {
    const form = dashboard.querySelector("[data-note-order]");
    if (!form) return;
    const messages = document.querySelector("[data-status-region]");
    const noteId = cell => cell.querySelector("[data-note-id]")?.dataset.noteId;
    const noteIds = board => [...board.children].map(noteId).filter(Boolean);
    const cellAt = target => (target instanceof Element ? target.closest(".note-cell") : null);
    const stored = new Map();       // list -> its note ids in the order the server last stored
    let drag = null;                // { cell, targets, target, dropped } while a card is dragged
    let saves = Promise.resolve();  // the swaps, saved one after another
    let unsaved = 0;                // swaps still waiting for the server's answer

    // Without JavaScript the tape is only decoration.
    for (const handle of dashboard.querySelectorAll("[data-note-drag-handle]")) {
        handle.draggable = true;
        handle.title = dashboard.dataset.dragHint ?? "";
    }

    function setTarget(cell) {
        if (drag.target === cell) return;
        drag.target?.classList.remove("note-cell--drop-target");
        drag.target = cell;
        cell?.classList.add("note-cell--drop-target");
    }

    // The drag is over: the marks go, and a card dropped on another one changes places with it.
    function endDrag() {
        if (!drag) return;
        const { cell, targets, dropped } = drag;
        cell.classList.remove("note-cell--dragging");
        for (const other of targets) other.classList.remove("note-cell--drop-eligible", "note-cell--drop-target");
        drag = null;
        if (dropped) swap(cell, dropped);
        settle();
    }

    dashboard.addEventListener("dragstart", event => {
        const handle = event.target instanceof Element ? event.target.closest("[data-note-drag-handle]") : null;
        if (!handle) return; // links and selected text keep the browser's own dragging
        endDrag(); // a previous drag whose dragend never came
        const cell = handle.closest(".note-cell");
        const card = cell.querySelector(".note-card");
        // The owner's other cards of the same month can take its place.
        const targets = [...cell.parentElement.children].filter(other => other !== cell && other.querySelector("[data-note-drag-handle]"));
        const current = drag = { cell, targets: new Set(targets), target: null, dropped: null };
        const box = card.getBoundingClientRect();
        event.dataTransfer.effectAllowed = "move";
        // A type of its own, so the note is never dropped as text into a field.
        event.dataTransfer.setData("application/x-worknotes-note", noteId(cell));
        event.dataTransfer.setDragImage(card, event.clientX - box.left, event.clientY - box.top);
        // Marked once the browser has taken the drag image, which then shows the card as it is.
        requestAnimationFrame(() => {
            if (drag !== current) return;
            cell.classList.add("note-cell--dragging");
            for (const target of targets) target.classList.add("note-cell--drop-eligible");
        });
    });

    // Over one of the cells: the drop is allowed there. Anywhere else the browser shows that it is not.
    const over = event => {
        if (!drag) return;
        const cell = cellAt(event.target);
        if (!drag.targets.has(cell)) { setTarget(null); return; }
        event.preventDefault();
        event.dataTransfer.dropEffect = "move";
        setTarget(cell);
    };
    dashboard.addEventListener("dragenter", over);
    dashboard.addEventListener("dragover", over);
    dashboard.addEventListener("dragleave", event => {
        if (drag && !dashboard.contains(event.relatedTarget)) setTarget(null);
    });

    // The drop is only noted: the cells change places on the dragend that follows it. Should a browser never send that
    // dragend, the swap still happens a moment later.
    dashboard.addEventListener("drop", event => {
        if (!drag) return;
        const target = cellAt(event.target);
        if (!drag.targets.has(target)) return;
        event.preventDefault();
        const current = drag;
        current.dropped = target;
        setTimeout(() => { if (drag === current) endDrag(); }, 500);
    });
    dashboard.addEventListener("dragend", endDrag);

    // The two cells change places at once; the swap is saved after the ones made before it.
    function swap(cell, target) {
        const board = cell.parentElement;
        if (!stored.has(board)) stored.set(board, noteIds(board));
        const note = noteId(cell), other = noteId(target);
        exchange(cell, target);
        unsaved++;
        board.setAttribute("aria-busy", "true");
        saves = saves.then(() => save(board, note, other));
    }

    async function save(board, note, target) {
        let failure = dashboard.dataset.reorderFailed;
        try {
            const data = new FormData(form);
            data.set("note", note);
            data.set("target", target);
            const response = await fetch(form.action, { method: "POST", body: data, headers: { Accept: "application/json" } });
            const body = await response.json().catch(() => ({}));
            if (response.ok && Array.isArray(body.order)) {
                stored.set(board, body.order.map(String));
                // An editor tab of one of the two notes (note-editor.js) goes on saving with the note's new version.
                document.dispatchEvent(new CustomEvent("note-board:versions", { detail: body.versions ?? [] }));
                failure = null;
            } else if (body.message) {
                failure = body.message;
            }
        } catch {
            // The request did not get an answer: the generic message.
        }
        unsaved--;
        settle();
        if (failure !== null) showStatusMessage(messages, "error", failure);
    }

    // Once every swap has its answer and no card is being dragged, each list takes the order the server stored (after
    // a failed save, the order from before that swap); an unsaved new card stays first.
    function settle() {
        if (unsaved > 0 || drag) return;
        for (const [board, ids] of stored) {
            board.removeAttribute("aria-busy");
            if (noteIds(board).join() === ids.join()) continue;
            const byId = new Map([...board.children].map(item => [noteId(item), item]));
            for (const id of ids) {
                const item = byId.get(id);
                if (item) board.append(item);
            }
        }
    }

    // A note saved in the editor moved to another month (month: its key and notes, in the order the server returned):
    // its cell goes to that month's list, which takes that order as it takes a swap's. The month it left is hidden by CSS
    // once no card is left in it. False when the page has no list for that month.
    function place(cell, month) {
        const board = [...dashboard.querySelectorAll("[data-note-month]")].find(list => list.dataset.noteMonth === month.key);
        if (!board || !cell) return false;
        board.append(cell);
        stored.set(board, (month.notes ?? []).map(String));
        settle();
        return true;
    }

    return { place };
}

// Two cells of the same list change places.
function exchange(first, second) {
    const marker = document.createComment("");
    first.replaceWith(marker);
    second.replaceWith(first);
    marker.replaceWith(second);
}

document.querySelectorAll("[data-notes-dashboard]").forEach(initializeDashboard);
