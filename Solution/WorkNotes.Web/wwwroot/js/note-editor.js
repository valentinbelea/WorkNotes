// Note editor: CodeMirror 6 plus paragraph identity. A paragraph is the text between blank lines; it keeps a stable
// id while it is edited, so the server can keep each paragraph's creation audit and change only what was modified.
// The info bar shows that audit for the paragraph under the mouse, or at the cursor.
// Appearance comes from note-editor.css; texts come from the page (resources), never from this file.
import {
    EditorState, StateField, StateEffect, Transaction, EditorView, Decoration, keymap, placeholder, highlightActiveLine,
    drawSelection, highlightSpecialChars, defaultKeymap, history, historyKeymap, invertedEffects,
    search, searchKeymap, highlightSelectionMatches
} from "../lib/codemirror/codemirror.js";
import { showStatusMessage } from "./status-messages.js";

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

// The paragraph containing a position, or null on a blank line (paragraphs are sorted).
function paragraphAt(paragraphs, pos) {
    let low = 0, high = paragraphs.length - 1;
    while (low <= high) {
        const middle = (low + high) >> 1;
        const paragraph = paragraphs[middle];
        if (pos < paragraph.from) high = middle - 1;
        else if (pos > paragraph.to) low = middle + 1;
        else return paragraph;
    }
    return null;
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
// a split paragraph keeps its id in the first fragment, merged paragraphs keep the first id, copies get none.
// Computed once per editor state and shared by the info bar and the save.
const matchedByState = new WeakMap();
function matchParagraphs(state) {
    let paragraphs = matchedByState.get(state);
    if (paragraphs) return paragraphs;
    paragraphs = paragraphsOf(state.doc).map(item => ({ ...item, id: null }));
    const tracked = [...state.field(trackedParagraphs)].sort((a, b) => a.from - b.from);
    let start = 0;
    for (const item of tracked) {
        while (start < paragraphs.length && paragraphs[start].to <= item.from) start++;
        for (let index = start; index < paragraphs.length && paragraphs[index].from < item.to; index++) {
            if (paragraphs[index].id === null) { paragraphs[index].id = item.id; break; }
        }
    }
    matchedByState.set(state, paragraphs);
    return paragraphs;
}

// Paragraphs without a known id are new and get one now.
export const identifyParagraphs = state => matchParagraphs(state).map(item => ({ ...item, id: item.id ?? newId() }));

// ---- Paragraph under the mouse -----------------------------------------------------------------------------

const setHovered = StateEffect.define();

// The hovered paragraph ({ from, to } or null); typing clears it, the next mouse move sets it again.
const hoveredParagraph = StateField.define({
    create: () => null,
    update(value, transaction) {
        let next = transaction.docChanged ? null : value;
        for (const effect of transaction.effects) if (effect.is(setHovered)) next = effect.value;
        return next;
    },
    // Each line of the hovered paragraph gets the cm-hoveredParagraph class (styled in note-editor.css).
    provide: field => EditorView.decorations.compute([field], state => {
        const paragraph = state.field(field);
        if (!paragraph) return Decoration.none;
        const lines = [];
        const last = state.doc.lineAt(paragraph.to).number;
        for (let number = state.doc.lineAt(paragraph.from).number; number <= last; number++)
            lines.push(Decoration.line({ class: "cm-hoveredParagraph" }).range(state.doc.line(number).from));
        return Decoration.set(lines);
    })
});

const sameParagraph = (a, b) => a === b || (a !== null && b !== null && a.from === b.from && a.to === b.to);

const hoverTracking = EditorView.domEventHandlers({
    mousemove(event, view) {
        const pos = view.posAtCoords({ x: event.clientX, y: event.clientY });
        const found = pos === null ? null : paragraphAt(matchParagraphs(view.state), pos);
        const paragraph = found && { from: found.from, to: found.to };
        if (!sameParagraph(paragraph, view.state.field(hoveredParagraph))) view.dispatch({ effects: setHovered.of(paragraph) });
    },
    mouseleave(event, view) {
        if (view.state.field(hoveredParagraph) !== null) view.dispatch({ effects: setHovered.of(null) });
    }
});

// ---- One note: its editor in a tab panel -------------------------------------------------------------------

// Everything a note needs lives in its panel (_NoteEditorTabPanel), so several notes are edited side by side, each with
// its own content, title, unsaved changes, undo history, version and save status. `shared` holds the texts common to
// all tabs, the dialog's message region and the antiforgery token.
function createNoteEditor(panel, data, shared) {
    const texts = shared.texts;
    const host = panel.querySelector("[data-note-editor]");
    const statusElement = panel.querySelector("[data-editor-status]");
    const infoElement = panel.querySelector("[data-editor-info]");
    const saveButton = panel.querySelector("[data-editor-save]");
    const titleInput = panel.querySelector("[data-editor-title]");
    const messages = shared.messages;

    // Audit texts (as stored, and with unsaved changes) and last saved content of each stored paragraph, by id.
    const auditById = new Map(data.blocks.map(block => [block.id, block]));
    const savedContentById = new Map(data.blocks.map(block => [block.id, block.content]));

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

    const isDirty = () => !data.readOnly && (!view.state.doc.eq(savedDoc) || (titleInput !== null && titleInput.value !== savedTitle));

    function updateStatus() {
        if (data.readOnly) return; // nothing is saved from a read-only editor
        const dirty = isDirty();
        statusElement.textContent = problem ?? (saving ? texts.saving : dirty ? texts.unsaved : texts.saved);
        statusElement.classList.toggle("note-editor-toolbar__status--error", problem !== null);
        if (saveButton) saveButton.disabled = saving || conflict || !dirty;
    }

    // The hovered paragraph, otherwise the one at the cursor: its stored audit, or why there is none yet.
    function updateInfo(state) {
        if (!infoElement) return;
        const paragraphs = matchParagraphs(state);
        const hovered = state.field(hoveredParagraph);
        const paragraph = hovered
            ? paragraphs.find(item => item.from === hovered.from && item.to === hovered.to) ?? null
            : paragraphAt(paragraphs, state.selection.main.head);
        let text = texts.infoHint;
        if (paragraph) {
            const audit = paragraph.id === null ? undefined : auditById.get(paragraph.id);
            if (audit === undefined) text = texts.blockNew;
            else text = state.sliceDoc(paragraph.from, paragraph.to) === savedContentById.get(paragraph.id) ? audit.info : audit.unsavedInfo;
        }
        infoElement.textContent = text;
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
        const blocks = paragraphs.map(paragraph => ({ id: paragraph.id, content: state.sliceDoc(paragraph.from, paragraph.to) }));
        saving = true;
        updateStatus();
        try {
            const response = await fetch(data.saveUrl, {
                method: "POST",
                headers: { "Content-Type": "application/json", "RequestVerificationToken": shared.token },
                body: JSON.stringify({ version, title, blocks })
            });
            const body = await response.json().catch(() => ({}));
            if (!response.ok) {
                problem = body.message ?? texts.failed;
                conflict = [401, 403, 404, 409].includes(response.status);
                showStatusMessage(messages, "error", problem);
                return;
            }
            version = body.version;
            savedDoc = state.doc;
            savedTitle = title;
            problem = null;
            showStatusMessage(messages, "success", body.message);
            // The note's last change, shown by the minimized form when this tab is active.
            if (body.modified) {
                panel.dataset.modifiedText = body.modified.text;
                panel.dataset.modifiedIso = body.modified.iso;
            }
            // The saved paragraphs are now the reference for the info bar.
            auditById.clear();
            savedContentById.clear();
            for (const block of body.blocks ?? []) auditById.set(block.id, block);
            for (const block of blocks) savedContentById.set(block.id, block.content);
        } catch {
            problem = texts.failed;
            showStatusMessage(messages, "error", problem);
        } finally {
            saving = false;
            updateStatus();
            updateInfo(view.state);
        }
    }

    const extensions = [
        trackedParagraphs.init(() => initialTracked),
        undoableParagraphs,
        hoveredParagraph,
        hoverTracking,
        history(),
        drawSelection(),
        highlightSpecialChars(),
        highlightActiveLine(),
        highlightSelectionMatches(),
        search({ top: true }),
        EditorView.lineWrapping,
        placeholder(texts.placeholder),
        EditorState.phrases.of(shared.phrases),
        EditorView.contentAttributes.of({ "aria-label": texts.content }),
        keymap.of([...searchKeymap, ...historyKeymap, ...defaultKeymap]),
        EditorView.updateListener.of(update => {
            if (update.docChanged) updateStatus();
            if (update.docChanged || update.selectionSet
                || update.transactions.some(transaction => transaction.effects.some(effect => effect.is(setHovered))))
                updateInfo(update.state);
        })
    ];
    if (data.readOnly) extensions.push(EditorState.readOnly.of(true), EditorView.editable.of(false));

    const view = new EditorView({ parent: host, state: EditorState.create({ doc, extensions }) });
    savedDoc = view.state.doc;

    saveButton?.addEventListener("click", save);
    titleInput?.addEventListener("input", updateStatus);

    // Without JavaScript the page shows the text read-only; these parts only make sense with the editor running.
    if (saveButton) saveButton.hidden = false;
    const infoBar = panel.querySelector("[data-editor-info-bar]");
    if (infoBar) infoBar.hidden = false;
    updateStatus();
    updateInfo(view.state);

    return {
        view, save, isDirty,
        // The note changed places on the board (notes-board.js): its row has a new version, its content is the same.
        // An editor that was up to date goes on with the new version; a stale one still gets the conflict.
        followVersion: (previous, current) => { if (version === previous) version = current; },
        title: () => (titleInput ? titleInput.value.trim() : panel.querySelector(".note-sheet__title")?.textContent.trim()) || texts.untitled,
        focus: () => { view.requestMeasure(); if (!data.readOnly) view.focus(); }
    };
}

// ---- The editor window: tabs, minimize and close ------------------------------------------------------------

// The notes are tabs in one window. Opening a note that already has a tab selects it; closing a tab closes only that
// note (after a confirmation when it has unsaved changes); the header's Close closes the window with all its tabs,
// and leaving the page with unsaved changes in any tab asks first. Minimize and Maximize only change how the window
// is shown: the tabs, their order, the active tab and every note's state stay as they are.
function initializeEditorWindow(dialog, settings) {
    const shared = {
        texts: settings.texts,
        phrases: settings.phrases,
        messages: dialog.querySelector("[data-editor-messages]"),
        token: dialog.querySelector("input[name='__RequestVerificationToken']")?.value ?? ""
    };
    const windowPanel = dialog.querySelector("[data-editor-window]");
    const tabList = dialog.querySelector("[data-editor-tabs]");
    const panels = dialog.querySelector("[data-editor-panels]");
    const minimized = dialog.querySelector("[data-editor-minimized]");
    const tabs = new Map();         // note id -> { item, panel, editor }, in the order of the tab bar
    let activeId = null;
    let leaving = false;            // unsaved changes were already confirmed away

    const isMinimized = () => dialog.classList.contains("note-editor-dialog--minimized");
    const noteUrl = id => `${settings.noteUrl}?note=${id}`;

    // The window's paper follows the active note's type, like its card on the board.
    function setSheetType(typeClass) {
        for (const element of [windowPanel, minimized]) {
            element.classList.remove("note-sheet--journal", "note-sheet--article");
            element.classList.add(typeClass);
        }
    }

    function activate(id, { focus = true } = {}) {
        const tab = tabs.get(id);
        if (!tab) return;
        activeId = id;
        for (const [otherId, other] of tabs) {
            const selected = otherId === id;
            const button = other.item.querySelector("[data-editor-tab-select]");
            button.setAttribute("aria-selected", String(selected));
            button.tabIndex = selected ? 0 : -1;
            other.item.classList.toggle("note-editor-tab--active", selected);
            other.panel.hidden = !selected;
        }
        setSheetType(tab.panel.dataset.typeClass);
        tab.item.scrollIntoView({ block: "nearest", inline: "nearest" });
        // The address names the active note, so a reload opens it again.
        // (window.history: `history` here is CodeMirror's undo history extension.)
        window.history.replaceState(window.history.state, "", noteUrl(id));
        if (focus) tab.editor.focus();
        else tab.editor.view.requestMeasure();
    }

    function addTab(item, panel) {
        const id = item.dataset.editorTab;
        const data = JSON.parse(panel.querySelector("[data-editor-note]").textContent);
        const editor = createNoteEditor(panel, data, shared);
        tabs.set(id, { item, panel, editor });
        const label = item.querySelector("[data-editor-tab-title]");
        panel.querySelector("[data-editor-title]")?.addEventListener("input", () => {
            label.textContent = editor.title();
            label.title = label.textContent;
        });
        item.querySelector("[data-editor-tab-select]").addEventListener("click", () => activate(id));
        const close = item.querySelector("[data-editor-tab-close]");
        close.hidden = false;
        close.addEventListener("click", () => closeTab(id));
        return id;
    }

    function closeWindow() {
        leaving = true;
        window.location.assign(settings.closeUrl);
    }

    function closeTab(id) {
        const tab = tabs.get(id);
        if (!tab) return;
        if (tab.editor.isDirty() && !window.confirm(shared.texts.confirmCloseTab.replace("{0}", tab.editor.title()))) return;
        if (tabs.size === 1) {
            // The last note closes the window; its changes were just confirmed away.
            closeWindow();
            return;
        }
        const order = [...tabs.keys()];
        const index = order.indexOf(id);
        tab.editor.view.destroy();
        tab.item.remove();
        tab.panel.remove();
        tabs.delete(id);
        if (activeId === id) activate(order[index + 1] ?? order[index - 1]);
    }

    // A note opened from the board: its tab if it is already open, otherwise a new tab fetched from the server.
    async function openNote(id) {
        if (isMinimized()) restore({ focus: false });
        if (tabs.has(id)) { activate(id); return; }
        try {
            const response = await fetch(`${settings.tabUrl}&note=${encodeURIComponent(id)}`, { headers: { Accept: "text/html" } });
            if (!response.ok) throw new Error(String(response.status));
            const fragment = document.createElement("template");
            fragment.innerHTML = await response.text();
            const item = fragment.content.querySelector("[data-editor-tab]");
            const panel = fragment.content.querySelector("[data-editor-panel]");
            if (!item || !panel) throw new Error("fragment");
            if (tabs.has(id)) { activate(id); return; } // opened twice while loading
            tabList.append(item);
            panels.append(panel);
            activate(addTab(item, panel));
        } catch {
            showStatusMessage(shared.messages, "error", shared.texts.openFailed);
        }
    }

    function minimize() {
        if (isMinimized()) return;
        const tab = tabs.get(activeId);
        const panel = tab.panel;
        // The compact form shows the tab that is active now.
        minimized.querySelector("[data-editor-minimized-type]").textContent = panel.dataset.typeName;
        minimized.querySelector("[data-editor-minimized-title]").textContent = tab.editor.title();
        for (const [selector, prefix] of [["[data-editor-created]", "created"], ["[data-editor-modified]", "modified"]]) {
            const time = minimized.querySelector(selector);
            time.textContent = panel.dataset[`${prefix}Text`];
            time.dateTime = panel.dataset[`${prefix}Iso`];
        }
        dialog.close();
        dialog.classList.add("note-editor-dialog--minimized");
        dialog.show();
        minimized.querySelector("[data-editor-maximize]")?.focus();
    }

    function restore({ focus = true } = {}) {
        if (!isMinimized()) return;
        dialog.close();
        dialog.classList.remove("note-editor-dialog--minimized");
        dialog.showModal();
        const tab = tabs.get(activeId);
        if (focus) tab.editor.focus();
        else tab.editor.view.requestMeasure();
    }

    // The tab bar: arrows, Home and End move between the tabs (the selected tab is the one Tab reaches).
    tabList.addEventListener("keydown", event => {
        if (!event.target.matches("[data-editor-tab-select]")) return;
        const order = [...tabs.keys()];
        const index = order.indexOf(activeId);
        const next = { ArrowRight: index + 1, ArrowLeft: index - 1, Home: 0, End: order.length - 1 }[event.key];
        if (next === undefined) return;
        event.preventDefault();
        const id = order[(next + order.length) % order.length];
        activate(id, { focus: false });
        tabs.get(id).item.querySelector("[data-editor-tab-select]").focus();
    });

    const minimizeButton = dialog.querySelector("[data-editor-minimize]");
    minimizeButton.hidden = false;
    minimizeButton.addEventListener("click", minimize);
    minimized.querySelector("[data-editor-maximize]").addEventListener("click", () => restore());
    minimized.addEventListener("dblclick", event => { if (!event.target.closest("a, button")) restore(); });

    // Ctrl+S / Cmd+S saves the active note, not while the editor is minimized.
    document.addEventListener("keydown", event => {
        if (!isMinimized() && (event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
            event.preventDefault();
            tabs.get(activeId)?.editor.save();
        }
    });
    // Leaving the page (the header's Close, Escape, the board) asks first when any tab has unsaved changes.
    window.addEventListener("beforeunload", event => {
        if (!leaving && [...tabs.values()].some(tab => tab.editor.isDirty())) event.preventDefault();
    });
    // Open and double-click on a board card (notes-board.js) come here while the editor is on the page.
    document.addEventListener("note-editor:open", event => {
        event.preventDefault();
        openNote(String(event.detail.id));
    });
    // Two cards swapped on the board: the tabs of those notes take the notes' new versions.
    document.addEventListener("note-board:versions", event => {
        for (const change of event.detail) tabs.get(String(change.id))?.editor.followVersion(change.previous, change.version);
    });

    const first = dialog.querySelector("[data-editor-tab]");
    activate(addTab(first, dialog.querySelector("[data-editor-panel]")));
}

const editorDialog = document.querySelector("dialog.note-editor-dialog");
const dataElement = document.getElementById("note-editor-data");
if (editorDialog && dataElement) initializeEditorWindow(editorDialog, JSON.parse(dataElement.textContent));
