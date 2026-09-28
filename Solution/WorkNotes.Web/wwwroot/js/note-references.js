// Internal references in the editor (note-editor.js adds this extension to every note's CodeMirror). The server reads
// the references of the text (CR 30080, bug-1234...) and knows which note each opens (INoteReferenceService); this only
// shows them: every place the server sent is marked as a link, with the title and type of its note as its tooltip, and
// the text stays exactly as it is written. A click on a link (or Ctrl+Enter with the cursor on it) opens its note in a
// tab of the editor. Typing inside a link or right next to it removes that link until the text is saved: the answer
// of the save brings the links of the saved text. Appearance comes from note-editor.css.
import { StateField, StateEffect, EditorView, Decoration, keymap } from "../lib/codemirror/codemirror.js";

// The links of the text after a save, in positions of the current document: { links: [{ from, to, note }], labels }
// (labels: note id -> tooltip).
export const setLinks = StateEffect.define();

function build(links, labels, length) {
    const marks = [];
    for (const link of links) {
        if (!(link.from >= 0 && link.to > link.from && link.to <= length)) continue;
        const note = String(link.note);
        marks.push(Decoration.mark({
            class: "cm-note-reference",
            attributes: { title: labels.get(note) ?? "", "data-note-reference": note }
        }).range(link.from, link.to));
    }
    return Decoration.set(marks, true);
}

// links: the links of the text as loaded ({ from, to, note } in document positions); targets: [{ id, label }];
// open(id): opens a note in the editor's tabs.
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

    // The note of the link at a position (the cursor inside it, or right before or after it), or null.
    function linkAt(state, pos) {
        let note = null;
        state.field(shown).between(pos, pos, (from, to, value) => {
            note = value.spec.attributes["data-note-reference"];
            return false;
        });
        return note;
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
                open(link.dataset.noteReference);
                return true;
            }
        }),
        keymap.of([{
            key: "Mod-Enter",
            run(view) {
                const note = linkAt(view.state, view.state.selection.main.head);
                if (note === null) return false;
                open(note);
                return true;
            }
        }])
    ];
}
