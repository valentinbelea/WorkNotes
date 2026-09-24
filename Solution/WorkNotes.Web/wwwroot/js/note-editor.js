// Note editor: CodeMirror 6 plus paragraph identity. A paragraph is the text between blank lines; it keeps a stable
// id while it is edited, so the server can keep each paragraph's creation audit and change only what was modified.
// Appearance comes from note-editor.css; texts come from the page (resources), never from this file.
import {
    EditorState, StateField, StateEffect, Transaction, EditorView, keymap, placeholder, highlightActiveLine,
    drawSelection, highlightSpecialChars, defaultKeymap, history, historyKeymap, invertedEffects,
    search, searchKeymap, highlightSelectionMatches
} from "../lib/codemirror/codemirror.js";

// ---- Paragraphs of a document ------------------------------------------------------------------------------

export function paragraphsOf(doc) {
    const result = [];
    let from = -1, to = -1;
    for (let number = 1; number <= doc.lines; number++) {
        const line = doc.line(number);
        if (/\S/.test(line.text)) {
            if (from < 0) from = line.from;
            to = line.to;
        } else if (from >= 0) {
            result.push({ from, to });
            from = -1;
        }
    }
    if (from >= 0) result.push({ from, to });
    return result;
}

// ---- Tracked paragraphs: the ids known from the last load or save, mapped through every edit -----------------

// Text typed inside a paragraph stays in it; text typed right before or after it does not (from maps forward,
// to maps backward). A paragraph whose text is deleted completely collapses and is dropped.
const mapTracked = (list, changes) =>
    list.map(item => ({ id: item.id, from: changes.mapPos(item.from, 1), to: changes.mapPos(item.to, -1) }));

const resetTracked = StateEffect.define();
const restoreTracked = StateEffect.define({ map: (list, mapping) => mapTracked(list, mapping) });

const trackedParagraphs = StateField.define({
    create: () => [],
    update(value, transaction) {
        let next = transaction.docChanged ? mapTracked(value, transaction.changes).filter(item => item.to > item.from) : value;
        for (const effect of transaction.effects) {
            if (effect.is(resetTracked)) next = effect.value;
            else if (effect.is(restoreTracked)) {
                const ids = new Set(effect.value.map(item => item.id));
                next = [...next.filter(item => !ids.has(item.id)), ...effect.value].sort((a, b) => a.from - b.from);
            }
        }
        return next;
    }
});

// Undo brings deleted text back; this brings the paragraph's id back with it.
const undoableParagraphs = invertedEffects.of(transaction => {
    if (!transaction.docChanged) return [];
    const lost = transaction.startState.field(trackedParagraphs).filter(item =>
        transaction.changes.mapPos(item.to, -1) <= transaction.changes.mapPos(item.from, 1));
    return lost.length ? [restoreTracked.of(lost)] : [];
});

const newId = () => crypto.randomUUID?.() ?? "10000000-1000-4000-8000-100000000000".replace(/[018]/g, digit =>
    (digit ^ crypto.getRandomValues(new Uint8Array(1))[0] & 15 >> digit / 4).toString(16));

// Each tracked paragraph, in document order, gives its id to the first current paragraph it overlaps that has none:
// a split paragraph keeps its id in the first fragment, merged paragraphs keep the first id, copies get new ids.
export function identifyParagraphs(state) {
    const paragraphs = paragraphsOf(state.doc).map(item => ({ ...item, id: null }));
    const tracked = [...state.field(trackedParagraphs)].sort((a, b) => a.from - b.from);
    let start = 0;
    for (const item of tracked) {
        while (start < paragraphs.length && paragraphs[start].to <= item.from) start++;
        for (let index = start; index < paragraphs.length && paragraphs[index].from < item.to; index++) {
            if (paragraphs[index].id === null) { paragraphs[index].id = item.id; break; }
        }
    }
    for (const paragraph of paragraphs) paragraph.id ??= newId();
    return paragraphs;
}

// ---- Page wiring -------------------------------------------------------------------------------------------

function initializeNoteEditor(host, data) {
    const texts = data.texts;
    const statusElement = document.querySelector("[data-editor-status]");
    const saveButton = document.querySelector("[data-editor-save]");
    const titleInput = document.querySelector("[data-editor-title]");
    const token = document.querySelector("input[name='__RequestVerificationToken']")?.value ?? "";

    // The stored paragraphs, separated by one blank line, with their ids at their positions.
    let doc = "";
    const initialTracked = data.blocks.map((block, index) => {
        if (index > 0) doc += "\n\n";
        const from = doc.length;
        doc += block.content;
        return { id: block.id, from, to: doc.length };
    });

    let version = data.version;
    let savedDoc = null;
    let savedTitle = titleInput?.value ?? "";
    let saving = false;
    let problem = null;     // message of the last failed save
    let conflict = false;   // the note changed elsewhere: saving stays blocked until the page is reloaded

    const isDirty = () => !view.state.doc.eq(savedDoc) || (titleInput !== null && titleInput.value !== savedTitle);

    function updateStatus() {
        if (data.readOnly) return; // nothing is saved from a read-only editor
        const dirty = isDirty();
        statusElement.textContent = problem ?? (saving ? texts.saving : dirty ? texts.unsaved : texts.saved);
        statusElement.classList.toggle("note-editor-toolbar__status--error", problem !== null);
        if (saveButton) saveButton.disabled = data.readOnly || saving || conflict || !dirty;
    }

    async function save() {
        if (data.readOnly || saving || conflict || !isDirty()) return;
        const state = view.state;
        const paragraphs = identifyParagraphs(state);
        // Keep the ids from now on, so edits typed while saving are mapped onto them.
        view.dispatch({
            effects: resetTracked.of(paragraphs.map(({ id, from, to }) => ({ id, from, to }))),
            annotations: Transaction.addToHistory.of(false)
        });
        const title = titleInput?.value ?? "";
        saving = true;
        updateStatus();
        try {
            const response = await fetch(data.saveUrl, {
                method: "POST",
                headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
                body: JSON.stringify({
                    version,
                    title,
                    blocks: paragraphs.map(paragraph => ({ id: paragraph.id, content: state.sliceDoc(paragraph.from, paragraph.to) }))
                })
            });
            const body = await response.json().catch(() => ({}));
            if (!response.ok) {
                problem = body.message ?? texts.failed;
                conflict = response.status === 409 || response.status === 403 || response.status === 404;
                return;
            }
            version = body.version;
            savedDoc = state.doc;
            savedTitle = title;
            problem = null;
        } catch {
            problem = texts.failed;
        } finally {
            saving = false;
            updateStatus();
        }
    }

    const extensions = [
        trackedParagraphs.init(() => initialTracked),
        undoableParagraphs,
        history(),
        drawSelection(),
        highlightSpecialChars(),
        highlightActiveLine(),
        highlightSelectionMatches(),
        search({ top: true }),
        EditorView.lineWrapping,
        placeholder(texts.placeholder),
        EditorState.phrases.of(data.phrases),
        EditorView.contentAttributes.of({ "aria-label": texts.content }),
        keymap.of([...searchKeymap, ...historyKeymap, ...defaultKeymap]),
        EditorView.updateListener.of(update => { if (update.docChanged) updateStatus(); })
    ];
    if (data.readOnly) extensions.push(EditorState.readOnly.of(true), EditorView.editable.of(false));

    const view = new EditorView({ parent: host, state: EditorState.create({ doc, extensions }) });
    savedDoc = view.state.doc;

    // Ctrl+S / Cmd+S saves from the editor and from the title.
    document.addEventListener("keydown", event => {
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
            event.preventDefault();
            save();
        }
    });
    saveButton?.addEventListener("click", save);
    titleInput?.addEventListener("input", updateStatus);
    window.addEventListener("beforeunload", event => {
        if (!data.readOnly && isDirty()) event.preventDefault();
    });

    updateStatus();
    if (!data.readOnly) view.focus();
    return view;
}

const host = document.querySelector("[data-note-editor]");
const dataElement = document.getElementById("note-editor-data");
if (host && dataElement) initializeNoteEditor(host, JSON.parse(dataElement.textContent));
