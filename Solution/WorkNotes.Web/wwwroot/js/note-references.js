// Internal references in the editor (note-editor.js adds this extension to every note's CodeMirror). The server reads
// the references of the text (CR 30080, bug-1234...) and knows which notes each opens (INoteReferenceService); this only
// shows them: every place the server sent is marked as a link, with the title and type of each of its notes as its
// tooltip, and the text stays exactly as it is written. A click on a link (or Ctrl+Enter with the cursor on it) opens
// its notes in tabs of the editor, one, two or more. Typing inside a link or right next to it removes that link until
// the text is saved: the answer of the save brings the links of the saved text. The drawer beside the text lists the
// note's references (referenceDrawer). Appearance comes from note-editor.css.
import { StateField, StateEffect, EditorView, Decoration, keymap } from "../lib/codemirror/codemirror.js";

// The links of the text after a save, in positions of the current document: { links: [{ from, to, notes }], labels }
// (notes: the ids of the notes the link opens, in order; labels: note id -> tooltip).
export const setLinks = StateEffect.define();

function build(links, labels, length) {
    const marks = [];
    for (const link of links) {
        const notes = Array.isArray(link.notes) ? link.notes.map(String) : [];
        if (!(link.from >= 0 && link.to > link.from && link.to <= length) || notes.length === 0) continue;
        marks.push(Decoration.mark({
            class: "cm-note-reference",
            // One line per note in the tooltip; the ids, in order, for opening them.
            attributes: { title: notes.map(note => labels.get(note) ?? "").filter(Boolean).join("\n"), "data-note-reference": notes.join(" ") }
        }).range(link.from, link.to));
    }
    return Decoration.set(marks, true);
}

// links: the links of the text as loaded ({ from, to, notes } in document positions); targets: [{ id, label }];
// open(ids): opens notes in the editor's tabs.
export function noteReferences({ links, targets, open }) {
    const labels = new Map(targets.map(target => [String(target.id), target.label]));

    const shown = StateField.define({
        create: state => build(links, labels, state.doc.length),
        update(value, transaction) {
            let next = transaction.docChanged
                ? value.update({ filter: (from, to) => !transaction.changes.touchesRange(from, to) }).map(transaction.changes)
                : value;
            for (const effect of transaction.effects)
                if (effect.is(setLinks)) next = build(effect.value.links, effect.value.labels, transaction.state.doc.length);
            return next;
        },
        provide: field => EditorView.decorations.from(field)
    });

    // The notes of the link at a position (the cursor inside it, or right before or after it), or null.
    function linkAt(state, pos) {
        let notes = null;
        state.field(shown).between(pos, pos, (from, to, value) => {
            notes = value.spec.attributes["data-note-reference"].split(" ");
            return false;
        });
        return notes;
    }

    return [
        shown,
        EditorView.domEventHandlers({
            click(event, view) {
                const link = event.target instanceof Element ? event.target.closest("[data-note-reference]") : null;
                if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return false;
                // The click that ends a selection only selects.
                if (!view.state.selection.main.empty) return false;
                event.preventDefault();
                open(link.dataset.noteReference.split(" "));
                return true;
            }
        }),
        keymap.of([{
            key: "Mod-Enter",
            run(view) {
                const notes = linkAt(view.state, view.state.selection.main.head);
                if (notes === null) return false;
                open(notes);
                return true;
            }
        }])
    ];
}

// The drawer of the note's references (_NoteEditorTabPanel), rendered by the server with the note: a plain click on one
// of its links opens the link's notes in the editor's tabs (with a modifier key or another button the browser follows
// the link to the note's page). show(list, labels) replaces the list after a save: list is [{ label, notes }] in the
// order of the text (notes: the ids of the notes the reference opens), labels maps a note id to its "title" (type).
// The entries are copies of the drawer's templates, filled with textContent.
export function referenceDrawer(drawer, { open, noteUrl }) {
    const list = drawer.querySelector("[data-references-list]");
    const count = drawer.querySelector("[data-references-count]");
    const empty = drawer.querySelector("[data-references-empty]");
    const entryTemplate = drawer.querySelector("template[data-references-entry]");
    const noteTemplate = drawer.querySelector("template[data-references-note]");

    drawer.addEventListener("click", event => {
        const link = event.target instanceof Element ? event.target.closest("a[data-note-reference]") : null;
        if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        open(link.dataset.noteReference.split(" "));
    });

    function show(references, labels) {
        const entries = [];
        for (const reference of references ?? []) {
            const notes = (reference.notes ?? []).map(String).filter(id => labels.has(id));
            if (notes.length === 0) continue;
            const entry = entryTemplate.content.firstElementChild.cloneNode(true);
            const link = entry.querySelector("[data-references-reference]");
            link.textContent = reference.label;
            link.href = noteUrl(notes[0]);
            link.dataset.noteReference = notes.join(" ");
            link.title = notes.map(id => labels.get(id)).join("\n");
            const targets = entry.querySelector("[data-references-notes]");
            for (const id of notes) {
                const item = noteTemplate.content.firstElementChild.cloneNode(true);
                const target = item.querySelector("[data-references-target]");
                target.textContent = labels.get(id);
                target.href = noteUrl(id);
                target.dataset.noteReference = id;
                targets.append(item);
            }
            entries.push(entry);
        }
        list.replaceChildren(...entries);
        count.textContent = String(entries.length);
        count.classList.toggle("note-references__count--none", entries.length === 0);
        empty.hidden = entries.length > 0;
    }

    return { show };
}
