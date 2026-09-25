// References between notes in the editor (note-editor.js adds this extension to every note's CodeMirror).
// A reference is stored in the text as [[note:{id}|{number}]], the form NoteReferenceRules reads on the server: the id of
// the target note (it survives a new title) and the number shown. The editor shows only the number, styled as a link,
// and treats the whole reference as one unit for the cursor and for deleting. A click on it (or Ctrl+Enter next to it)
// opens its note in a tab of the editor; a reference whose note can no longer be opened keeps its number, marked.
//
// After a finished word (space, punctuation, new line or Tab) that is a number, the server is asked for the notes of the
// board with that number in their title. When there are some, a discreet suggestion below the number offers them; the
// text changes only when the user picks one, by click or from the keyboard (Tab goes into the suggestion, the arrows
// move, Enter picks, Escape closes). The suggestion also closes on a click outside it and when typing goes on elsewhere
// (another line, or before the number). Texts come from the page; appearance from note-editor.css.
import { StateField, StateEffect, RangeSet, RangeValue, Transaction, EditorView, Decoration, keymap, showTooltip, isolateHistory }
    from "../lib/codemirror/codemirror.js";

// Same form as NoteReferenceRules.Markup: an int id above 0 and the digits of the number.
const referencePattern = /\[\[note:([0-9]{1,10})\|([0-9]{1,18})\]\]/g;
const maxNoteId = 2147483647;
const wordCharacter = /[\p{L}\p{N}\p{M}]/u;

class Reference extends RangeValue {
    constructor(id, number) {
        super();
        this.id = id;
        this.number = number;
    }

    eq(other) {
        return other.id === this.id && other.number === this.number;
    }
}

// The references between from and to (whole lines: a reference never spans lines).
function readReferences(doc, from, to) {
    const found = [];
    for (const match of doc.sliceString(from, to).matchAll(referencePattern)) {
        const id = Number(match[1]);
        if (id < 1 || id > maxNoteId) continue;
        const start = from + match.index;
        found.push(new Reference(String(id), match[2]).range(start, start + match[0].length));
    }
    return found;
}

// Every reference of the document, kept up to date by reading again only the lines an edit touched.
const references = StateField.define({
    create: state => RangeSet.of(readReferences(state.doc, 0, state.doc.length)),
    update(value, transaction) {
        if (!transaction.docChanged) return value;
        const doc = transaction.state.doc;
        const lines = [];
        transaction.changes.iterChangedRanges((fromA, toA, fromB, toB) => {
            const from = doc.lineAt(fromB).from, to = doc.lineAt(toB).to;
            const last = lines[lines.length - 1];
            if (last && from <= last.to) last.to = Math.max(last.to, to);
            else lines.push({ from, to });
        });
        let next = value.map(transaction.changes);
        for (const { from, to } of lines)
            next = next.update({ filterFrom: from, filterTo: to, filter: () => false, add: readReferences(doc, from, to) });
        return next;
    }
});

const idsOf = state => {
    const ids = new Set();
    for (let cursor = state.field(references).iter(); cursor.value; cursor.next()) ids.add(cursor.value.id);
    return ids;
};

// The reference touching a position (the cursor right before or after it), or null.
function referenceAt(state, pos) {
    let found = null;
    state.field(references).between(pos, pos, (from, to, value) => { found = { from, to, id: value.id }; return false; });
    return found;
}

// The number that ends at pos, when it is a whole word of digits of the allowed length and not part of a reference.
function numberBefore(state, pos, lengths) {
    const line = state.doc.lineAt(pos);
    let from = pos;
    while (from > line.from && wordCharacter.test(line.text[from - 1 - line.from])) from--;
    const number = state.sliceDoc(from, pos);
    if (!/^[0-9]+$/.test(number) || number.length < lengths.min || number.length > lengths.max) return null;
    let inReference = false;
    state.field(references).between(from, pos, () => { inReference = true; return false; });
    return inReference ? null : { from, to: pos, number };
}

// Known targets: note id -> the tooltip of its references, or null when the note cannot be opened.
const setTargets = StateEffect.define();

// The suggestion for one number: { from, to, number, anchor, token, targets, tooltip }. targets is null while the server
// is asked, [] when no note has the number (nothing is shown, but the number counts as looked up), otherwise the notes.
// anchor is where the cursor was when the word was finished; typing may go on on that line, after the number.
const openSuggestion = StateEffect.define();
const showTargets = StateEffect.define();
const closeSuggestion = StateEffect.define();

export function noteReferences(options) {
    const { texts, lengths } = options;
    const hidden = Decoration.replace({});
    let tokens = 0;

    const targets = StateField.define({
        create(state) {
            const known = new Map(options.targets.map(target => [String(target.id), target.label]));
            // The server checked every reference of the stored text: the others cannot be opened.
            for (const range of readReferences(state.doc, 0, state.doc.length)) if (!known.has(range.value.id)) known.set(range.value.id, null);
            return known;
        },
        update(value, transaction) {
            let next = value;
            for (const effect of transaction.effects) {
                if (!effect.is(setTargets)) continue;
                next = new Map(next);
                for (const id of effect.value.unavailable ?? []) next.set(String(id), null);
                for (const target of effect.value.found ?? []) next.set(String(target.id), target.label);
            }
            return next;
        }
    });

    // Only the number is shown: the rest of the form is hidden, the number is marked as a link (or as one that cannot
    // be opened) with the target's title and type as its tooltip.
    const decorations = EditorView.decorations.compute([references, targets], state => {
        const known = state.field(targets);
        const marks = [];
        for (let cursor = state.field(references).iter(); cursor.value; cursor.next()) {
            const { from, to, value } = cursor;
            const numberFrom = to - 2 - value.number.length;
            const label = known.get(value.id);
            marks.push(hidden.range(from, numberFrom));
            marks.push(Decoration.mark({
                class: typeof label === "string" ? "cm-note-reference" : "cm-note-reference cm-note-reference--broken",
                attributes: { title: typeof label === "string" ? label : texts.referenceBroken, "data-note-reference": value.id }
            }).range(numberFrom, to - 2));
            marks.push(hidden.range(to - 2, to));
        }
        return Decoration.set(marks);
    });

    function close(view) {
        if (view.state.field(suggestion, false)) view.dispatch({ effects: closeSuggestion.of(null) });
    }

    // Turns the number into a reference to the chosen note; the cursor stays where it was, after the number.
    function choose(view, target) {
        const value = view.state.field(suggestion, false);
        if (!value || !target || view.state.sliceDoc(value.from, value.to) !== value.number) { close(view); view.focus(); return; }
        const insert = `[[note:${target.id}|${value.number}]]`;
        const head = view.state.selection.main.head;
        view.dispatch({
            changes: { from: value.from, to: value.to, insert },
            selection: { anchor: head + insert.length - (value.to - value.from) },
            effects: [closeSuggestion.of(null), setTargets.of({ found: [target] })],
            // Its own undo step: Ctrl+Z first takes back what was typed after it, then the reference.
            annotations: isolateHistory.of("full"),
            userEvent: "input.reference",
            scrollIntoView: true
        });
        view.focus();
    }

    function suggestionView(view, found) {
        const element = (tag, className, text) => {
            const node = document.createElement(tag);
            node.className = className;
            if (text !== undefined) node.textContent = text;
            return node;
        };
        const dom = element("div", "note-reference-suggestion");
        dom.setAttribute("role", "group");
        dom.setAttribute("aria-label", texts.referenceSuggestion);
        const list = element("div", "note-reference-suggestion__options");
        for (const target of found) {
            const option = element("button", "note-reference-suggestion__option"
                + (target.type === "Article" ? " note-reference-suggestion__option--article" : " note-reference-suggestion__option--journal"));
            option.type = "button";
            option.dataset.referenceTarget = String(target.id);
            option.setAttribute("aria-label", target.option);
            option.append(element("span", "note-reference-suggestion__type", target.typeName), element("span", "note-reference-suggestion__name", target.title));
            list.append(option);
        }
        dom.append(element("p", "note-reference-suggestion__title", texts.referenceSuggestion), list,
            element("p", "note-reference-suggestion__hint", texts.referenceHint));
        const buttons = () => [...list.querySelectorAll("button")];
        // A click keeps the focus in the text, so the reference is made and typing goes on.
        dom.addEventListener("mousedown", event => event.preventDefault());
        dom.addEventListener("click", event => {
            const option = event.target.closest("[data-reference-target]");
            if (option) choose(view, found.find(target => String(target.id) === option.dataset.referenceTarget));
        });
        dom.addEventListener("keydown", event => {
            const all = buttons();
            const index = all.indexOf(document.activeElement);
            const move = { ArrowDown: index + 1, ArrowUp: index - 1, Home: 0, End: all.length - 1,
                Tab: event.shiftKey ? index - 1 : index + 1 }[event.key];
            if (event.key === "Escape") {
                // Stops here: the dialog must not take it as a request to close the editor.
                event.preventDefault();
                event.stopPropagation();
                close(view);
                view.focus();
            } else if (move !== undefined) {
                event.preventDefault();
                all[(move + all.length) % all.length].focus();
            }
        });
        dom.addEventListener("focusout", event => {
            if (!dom.contains(event.relatedTarget) && event.relatedTarget !== view.contentDOM) close(view);
        });
        options.announce?.(texts.referenceAvailable);
        return { dom };
    }

    const suggestion = StateField.define({
        create: () => null,
        update(value, transaction) {
            for (const effect of transaction.effects) {
                if (effect.is(openSuggestion)) value = effect.value;
                else if (effect.is(closeSuggestion)) value = null;
                else if (effect.is(showTargets) && value?.token === effect.value.token) {
                    const found = effect.value.targets;
                    value = { ...value, targets: found, tooltip: found.length === 0 ? null
                        : { pos: value.from, above: false, create: view => suggestionView(view, found) } };
                }
            }
            if (!value) return null;
            if (transaction.docChanged) {
                // The number itself changed: nothing to suggest any more.
                if (transaction.changes.touchesRange(value.from, value.to)) return null;
                const changes = transaction.changes;
                const from = changes.mapPos(value.from, 1);
                value = { ...value, from, to: changes.mapPos(value.to, -1), anchor: changes.mapPos(value.anchor, -1),
                    tooltip: value.tooltip && { ...value.tooltip, pos: from } };
            }
            // A click in the text, or the cursor leaving the line where typing went on (or going before the number).
            if (transaction.isUserEvent("select.pointer")) return null;
            if (transaction.docChanged || transaction.selection) {
                const { head } = transaction.state.selection.main;
                const doc = transaction.state.doc;
                if (head < value.to || doc.lineAt(head).number !== doc.lineAt(value.anchor).number) return null;
            }
            return value;
        },
        provide: field => showTooltip.from(field, value => value?.tooltip ?? null)
    });

    // An answer that arrives after its tab was closed is dropped.
    const alive = view => view.dom.isConnected;

    // Asks the server for the notes with the number in their title, and shows them if the number is still there.
    function lookUp(view, found, anchor) {
        const token = ++tokens;
        view.dispatch({ effects: openSuggestion.of({ ...found, anchor, token, targets: null, tooltip: null }) });
        const show = list => {
            if (alive(view) && view.state.field(suggestion, false)?.token === token) view.dispatch({ effects: showTargets.of({ token, targets: list }) });
        };
        options.findTargets(found.number).then(show, () => show([]));
    }

    function openReference(view, id) {
        if (typeof view.state.field(targets).get(id) !== "string") return false;
        // A note deleted since the text was loaded cannot be opened: from then on its references are marked.
        Promise.resolve(options.open(id)).then(opened => {
            if (opened === false && alive(view)) view.dispatch({ effects: setTargets.of({ unavailable: [id] }) });
        });
        return true;
    }

    // Pasted references (or ones brought back by undo) that were never checked: the server says which it can open.
    let pending = null;
    function checkUnknown(view) {
        clearTimeout(pending);
        pending = setTimeout(() => {
            if (!alive(view)) return;
            const known = view.state.field(targets);
            const unknown = [...idsOf(view.state)].filter(id => !known.has(id));
            if (unknown.length === 0) return;
            const settle = found => { if (alive(view)) view.dispatch({ effects: setTargets.of({ unavailable: unknown, found }) }); };
            options.resolveTargets(unknown).then(settle, () => settle([]));
        }, 300);
    }

    const extensions = [
        references,
        targets,
        decorations,
        EditorView.atomicRanges.of(view => view.state.field(references)),
        EditorView.domEventHandlers({
            click(event, view) {
                const mark = event.target instanceof Element ? event.target.closest(".cm-note-reference") : null;
                if (!mark || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return false;
                // The click that ends a selection only selects.
                if (!view.state.selection.main.empty) return false;
                if (!openReference(view, mark.dataset.noteReference)) return false;
                event.preventDefault();
                return true;
            }
        })
    ];
    if (options.readOnly) return extensions;

    return [
        ...extensions,
        suggestion,
        keymap.of([
            {
                // Into the suggestion when it is shown. Tab also finishes a word: once that number was looked up, Tab
                // moves the focus on as usual.
                key: "Tab",
                run(view) {
                    const value = view.state.field(suggestion);
                    if (value?.tooltip) {
                        view.dom.querySelector(".note-reference-suggestion__option")?.focus();
                        return true;
                    }
                    const { main } = view.state.selection;
                    if (!main.empty) return false;
                    const found = numberBefore(view.state, main.head, lengths);
                    if (!found || (value && value.from === found.from && value.to === found.to)) return false;
                    lookUp(view, found, main.head);
                    return true;
                }
            },
            { key: "Escape", run: view => { if (!view.state.field(suggestion)?.tooltip) return false; close(view); return true; } },
            {
                key: "Mod-Enter",
                run(view) {
                    const reference = referenceAt(view.state, view.state.selection.main.head);
                    return reference !== null && openReference(view, reference.id);
                }
            }
        ]),
        EditorView.domEventHandlers({
            blur(event, view) {
                if (!(event.relatedTarget instanceof Element && event.relatedTarget.closest(".note-reference-suggestion"))) close(view);
            }
        }),
        EditorView.updateListener.of(update => {
            if (!update.docChanged) return;
            checkUnknown(update.view);
            // A word finished by typing a space, punctuation or a new line; not a paste, a deletion or a reference.
            const last = update.transactions[update.transactions.length - 1];
            const event = last.annotation(Transaction.userEvent);
            if (event !== "input.type" && event !== "input") return;
            let change = null, count = 0;
            last.changes.iterChanges((fromA, toA, fromB, toB, inserted) => { count++; change = { at: fromB, text: inserted.toString() }; });
            if (count !== 1 || !change.text || wordCharacter.test(change.text[0])) return;
            const found = numberBefore(update.state, change.at, lengths);
            if (!found) return;
            const anchor = update.state.selection.main.head;
            const doc = update.state.doc;
            // Not while the update is being applied.
            queueMicrotask(() => { if (update.view.state.doc === doc) lookUp(update.view, found, anchor); });
        })
    ];
}
