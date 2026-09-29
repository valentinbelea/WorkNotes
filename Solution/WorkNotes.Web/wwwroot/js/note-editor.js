// Note editor: CodeMirror 6 plus paragraph identity. A paragraph is the text between blank lines; it keeps a stable
// id while it is edited, so the server can keep each paragraph's creation audit and change only what was modified.
// The info bar shows that audit for the paragraph under the mouse, or at the cursor. The links of internal references
// are note-references.js (the server says where they are); opening one uses the editor's tabs.
// Appearance comes from note-editor.css; texts come from the page (resources), never from this file.
import {
    EditorState, StateField, StateEffect, Transaction, EditorView, Decoration, keymap, placeholder, highlightActiveLine,
    drawSelection, highlightSpecialChars, defaultKeymap, history, historyKeymap, invertedEffects,
    search, searchKeymap, highlightSelectionMatches
} from "../lib/codemirror/codemirror.js";
import { showStatusMessage } from "./status-messages.js";
import { noteReferences, setLinks, referenceDrawer } from "./note-references.js";

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

// A paragraph's links (from and to within its text, as the server sends them, with the notes each opens) in document
// positions, for a paragraph that starts at from; a link that does not fit the paragraph is left out.
const linksAt = (from, links, length) => (links ?? [])
    .filter(link => link.from >= 0 && link.to > link.from && link.to <= length)
    .map(link => ({ from: from + link.from, to: from + link.to, notes: link.notes, reference: link.reference }));

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
    // The owner's type switch in the footer (journal / article); a read-only editor has none.
    const typeSwitch = panel.querySelector("[data-editor-type]");
    const chosenType = () => typeSwitch?.querySelector("input:checked")?.value ?? null;
    const messages = shared.messages;
    const drawerElement = panel.querySelector("[data-editor-references]");
    // The branches linked to the references of the text now (the page's, then each answer's): a click on a reference that
    // has one asks whether to open its notes or the branch.
    let gitLinks = data.gitReferences ?? [];
    const applyGit = list => { gitLinks = list ?? []; drawer?.showGit(list); };
    // The drawer of the note's references: its links open notes in the tabs, and each save brings its list. The owner's
    // drawer also removes the branches linked to the references (removeGitReference, below).
    const drawer = drawerElement
        ? referenceDrawer(drawerElement, { open: ids => shared.openNotes(ids), noteUrl: shared.noteUrl,
            removeGit: !data.readOnly && data.gitRemoveUrl ? id => removeGitReference(id) : null })
        : null;

    // Audit texts (as stored, and with unsaved changes) and last saved content of each stored paragraph, by id.
    const auditById = new Map(data.blocks.map(block => [block.id, block]));
    const savedContentById = new Map(data.blocks.map(block => [block.id, block.content]));

    // The stored paragraphs, separated by one blank line, with their ids and their links at their positions.
    let doc = "";
    const initialLinks = [];
    const initialTracked = data.blocks.map((block, index) => {
        if (index > 0) doc += "\n\n";
        const from = doc.length;
        doc += block.content;
        initialLinks.push(...linksAt(from, block.links, block.content.length));
        return { id: block.id, from, to: doc.length };
    });

    let version = data.version;
    let savedDoc = null;
    let savedTitle = titleInput?.value ?? "";
    let savedType = chosenType();
    let saving = false;
    let problem = null;     // message of the last failed save
    let conflict = false;   // the note changed elsewhere: saving stays blocked until the page is reloaded

    const isDirty = () => !data.readOnly && (!view.state.doc.eq(savedDoc) || (titleInput !== null && titleInput.value !== savedTitle)
        || chosenType() !== savedType);

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
        const noteType = chosenType();
        const blocks = paragraphs.map(paragraph => ({ id: paragraph.id, content: state.sliceDoc(paragraph.from, paragraph.to) }));
        saving = true;
        updateStatus();
        try {
            const response = await fetch(data.saveUrl, {
                method: "POST",
                headers: { "Content-Type": "application/json", "RequestVerificationToken": shared.token },
                body: JSON.stringify({ version, title, noteType, blocks })
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
            savedType = noteType;
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
            const labels = new Map((body.references ?? []).map(target => [String(target.id), target.label]));
            showSavedLinks(body, labels);
            drawer?.show(body.referenceList, labels);
            applyGit(body.gitReferences);
            // The note's card on the board behind the window shows the save.
            shared.showOnBoard(body.card);
        } catch {
            problem = texts.failed;
            showStatusMessage(messages, "error", problem);
        } finally {
            saving = false;
            updateStatus();
            updateInfo(view.state);
        }
    }

    // The server's answer about the text before a word just ended (INoteReferenceService.LookUpAsync), or null.
    const findReference = (text, signal) => fetch(data.lookupUrl, {
        method: "POST",
        headers: { "Content-Type": "application/json", "Accept": "application/json", "RequestVerificationToken": shared.token },
        body: JSON.stringify({ text }),
        signal
    }).then(response => response.ok ? response.json() : null);

    const gitHeaders = { "Content-Type": "application/json", "Accept": "application/json", "RequestVerificationToken": shared.token };

    // The branches of a repository whose name contains a reference (INoteGitReferenceService.SearchBranchesAsync), for the
    // Git option of the lookup's popup: { message, note, branches }; a failure is only a message. An aborted search throws.
    async function searchBranches({ repository, reference }, signal) {
        try {
            const response = await fetch(data.gitBranchesUrl, { method: "POST", headers: gitHeaders, body: JSON.stringify({ repository, reference }), signal });
            const body = await response.json().catch(() => ({}));
            if (!response.ok) return { message: body.message ?? texts.gitFailed, branches: [] };
            return { message: body.message, note: body.note, branches: body.branches ?? [] };
        } catch (error) {
            if (error?.name === "AbortError") throw error;
            return { message: texts.gitFailed, branches: [] };
        }
    }

    // Links a branch to the reference of the paragraph where the popup is: { ok, message }. The paragraph must be stored
    // for the link, so an unsaved note is saved first; the answer brings the note's branches for the drawer.
    async function addGitReference({ repository, reference, branch, position }) {
        if (saving) return { ok: false, message: texts.gitSaveFirst };
        if (isDirty()) await save();
        if (problem !== null || conflict) return { ok: false, message: problem ?? texts.failed };
        const at = position();
        if (at === null) return { ok: false, message: texts.gitChanged };
        const place = view.state.field(trackedParagraphs).find(item => at >= item.from && at <= item.to);
        if (!place || !auditById.has(place.id)) return { ok: false, message: texts.gitSaveFirst };
        try {
            const response = await fetch(data.gitAddUrl, {
                method: "POST", headers: gitHeaders, body: JSON.stringify({ blockId: place.id, repository, reference, branch })
            });
            const body = await response.json().catch(() => ({}));
            if (!response.ok) return { ok: false, message: body.message ?? texts.gitFailed };
            applyGit(body.gitReferences);
            showStatusMessage(messages, "success", body.message);
            return { ok: true };
        } catch {
            return { ok: false, message: texts.gitFailed };
        }
    }

    // Removes a branch from the drawer (its button); the answer brings the note's branches now.
    async function removeGitReference(id) {
        try {
            const response = await fetch(data.gitRemoveUrl, { method: "POST", headers: gitHeaders, body: JSON.stringify({ id }) });
            const body = await response.json().catch(() => ({}));
            if (!response.ok) {
                showStatusMessage(messages, "error", body.message ?? texts.gitFailed);
                return;
            }
            applyGit(body.gitReferences);
            showStatusMessage(messages, "success", body.message);
        } catch {
            showStatusMessage(messages, "error", texts.gitFailed);
        }
    }

    // The links of the saved text, drawn where its paragraphs are now. A paragraph edited while the save was on its way
    // gets its links at the next save.
    function showSavedLinks(body, labels) {
        const places = new Map(view.state.field(trackedParagraphs).map(item => [item.id, item]));
        const links = [];
        for (const block of body.blocks ?? []) {
            const place = places.get(block.id);
            const content = savedContentById.get(block.id);
            if (!place || content === undefined || view.state.sliceDoc(place.from, place.to) !== content) continue;
            links.push(...linksAt(place.from, block.links, content.length));
        }
        view.dispatch({ effects: setLinks.of({ links, labels }), annotations: Transaction.addToHistory.of(false) });
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
        // Before the default keys: Ctrl+Enter on a link opens its notes; Tab and Escape serve the lookup's popup first.
        noteReferences({
            links: initialLinks, targets: data.references ?? [], open: ids => shared.openNotes(ids),
            // Where a reference goes besides its notes: the branches linked to it, if any.
            choice: shared.choice ? { ...shared.choice, branches: reference => gitLinks.filter(link => link.normalized === reference) } : null,
            // The owner's editor asks about the references it types.
            lookup: data.readOnly || !data.lookupUrl || !shared.lookup ? null : {
                ...shared.lookup, find: findReference,
                // The Git option of the popup, when this note can link branches.
                git: data.gitBranchesUrl && data.gitAddUrl
                    ? { repositories: shared.gitRepositories, choice: shared.gitChoice, search: searchBranches, add: addGitReference }
                    : null
            }
        }),
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
    typeSwitch?.addEventListener("change", updateStatus);

    // Without JavaScript the page shows the text read-only; these parts only make sense with the editor running.
    if (saveButton) saveButton.hidden = false;
    if (typeSwitch) {
        // The switch takes the place of the type's name.
        typeSwitch.hidden = false;
        const typeName = panel.querySelector("[data-editor-type-name]");
        if (typeName) typeName.hidden = true;
    }
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
// The board behind the window follows each save (notes-board.js, note-editor:saved), so closing the window only takes
// it away, without loading the page again, while nothing is unsaved; otherwise the page loads the board again.
function initializeEditorWindow(dialog, settings, dataElement) {
    const noteUrl = id => `${settings.noteUrl}?note=${id}`;
    const lookupTemplate = dialog.querySelector("template[data-reference-lookup]");
    const lookupNoteTemplate = dialog.querySelector("template[data-reference-lookup-note]");
    const shared = {
        texts: settings.texts,
        phrases: settings.phrases,
        messages: dialog.querySelector("[data-editor-messages]"),
        token: dialog.querySelector("input[name='__RequestVerificationToken']")?.value ?? "",
        // A link opens its notes in tabs of this window, like notes opened from the board.
        openNotes: ids => openNotes(ids.map(String)),
        noteUrl,
        // The popup of a reference just typed (note-references.js), common to all tabs.
        lookup: lookupTemplate && lookupNoteTemplate
            ? { length: settings.lookupLength, template: lookupTemplate, noteTemplate: lookupNoteTemplate,
                branchTemplate: dialog.querySelector("template[data-reference-lookup-branch]") }
            : null,
        // The repositories the Git option of that popup offers, and the one chosen last, kept for all tabs.
        gitRepositories: settings.gitRepositories ?? [],
        gitChoice: { repository: null },
        // The popup of a reference that leads to its notes and to a branch, common to all tabs.
        choice: dialog.querySelector("template[data-reference-choice]") && dialog.querySelector("template[data-reference-choice-branch]")
            ? { template: dialog.querySelector("template[data-reference-choice]"), branchTemplate: dialog.querySelector("template[data-reference-choice-branch]") }
            : null,
        // A saved note's card (the save's answer) goes to the board, which marks the event handled once the card shows
        // it; a save the board could not show makes closing the window load the board again.
        showOnBoard: card => {
            if (!card || document.dispatchEvent(new CustomEvent("note-editor:saved", { detail: card, cancelable: true }))) boardStale = true;
        }
    };
    const windowPanel = dialog.querySelector("[data-editor-window]");
    const tabList = dialog.querySelector("[data-editor-tabs]");
    const panels = dialog.querySelector("[data-editor-panels]");
    const minimized = dialog.querySelector("[data-editor-minimized]");
    const tabs = new Map();         // note id -> { item, panel, editor }, in the order of the tab bar
    let activeId = null;
    let leaving = false;            // unsaved changes were already confirmed away
    let boardStale = false;         // a save the board behind could not show
    let removed = false;            // the window closed in place
    const listeners = new AbortController(); // the document's and the window's listeners, removed with the window

    const isMinimized = () => dialog.classList.contains("note-editor-dialog--minimized");
    let referencesOpen = false;     // the references drawer, open or closed in every tab

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

    // The references drawer is open or closed for the whole window: a tab opened later shows it the same way, and
    // opening or closing it in one tab does the same in the others.
    function followReferencesDrawer(panel) {
        const drawer = panel.querySelector("[data-editor-references]");
        if (!drawer) return;
        drawer.open = referencesOpen;
        drawer.addEventListener("toggle", () => {
            if (drawer.open === referencesOpen) return;
            referencesOpen = drawer.open;
            for (const tab of tabs.values()) {
                const other = tab.panel.querySelector("[data-editor-references]");
                if (other && other !== drawer) other.open = referencesOpen;
            }
        });
    }

    // A note's type as chosen in its footer (the radio has the classes and the name of its type): its tab and, while it is
    // the active one, the window show it at once; the minimized form takes it from the panel. It is saved with the note.
    function showType(id, option) {
        const tab = tabs.get(id);
        if (!tab || !option.checked) return;
        tab.panel.dataset.typeClass = option.dataset.sheetClass;
        tab.panel.dataset.typeName = option.dataset.typeName;
        tab.item.classList.remove("note-editor-tab--journal", "note-editor-tab--article");
        tab.item.classList.add(option.dataset.tabClass);
        const name = tab.item.querySelector("[data-editor-tab-type]");
        if (name) name.textContent = option.dataset.typeName;
        if (id === activeId) setSheetType(option.dataset.sheetClass);
    }

    function addTab(item, panel) {
        const id = item.dataset.editorTab;
        const data = JSON.parse(panel.querySelector("[data-editor-note]").textContent);
        const editor = createNoteEditor(panel, data, shared);
        tabs.set(id, { item, panel, editor });
        followReferencesDrawer(panel);
        const label = item.querySelector("[data-editor-tab-title]");
        panel.querySelector("[data-editor-title]")?.addEventListener("input", () => {
            label.textContent = editor.title();
            label.title = label.textContent;
        });
        panel.querySelector("[data-editor-type]")?.addEventListener("change", event => showType(id, event.target));
        item.querySelector("[data-editor-tab-select]").addEventListener("click", () => activate(id));
        const close = item.querySelector("[data-editor-tab-close]");
        close.hidden = false;
        close.addEventListener("click", () => closeTab(id));
        return id;
    }

    // Nothing to lose and nothing to show: every tab is saved and the board shows every save.
    const closesInPlace = () => !boardStale && ![...tabs.values()].some(tab => tab.editor.isDirty());

    // The window goes and the board behind it is used again as it is, without loading the page: the address becomes the
    // board's (the close URL), as a new history entry, like the page it replaces (Back loads the note again), and the
    // board puts the focus on the card of the active note (note-editor:closed).
    function removeWindow() {
        if (removed) return;
        removed = true;
        listeners.abort();
        for (const tab of tabs.values()) tab.editor.view.destroy();
        dialog.close();
        dialog.remove();
        dataElement.remove();
        window.history.pushState(null, "", settings.closeUrl);
        window.addEventListener("popstate", reloadOnHistory);
        document.dispatchEvent(new CustomEvent("note-editor:closed", { detail: { id: activeId } }));
    }

    // The last tab was closed, its changes (if any) confirmed away: the window goes, in place unless the board missed a save.
    function closeWindow() {
        if (!boardStale) { removeWindow(); return; }
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

    // A note opened from the board or from a reference: its tab if it is already open, otherwise a new tab fetched from
    // the server. The other tabs keep everything, unsaved changes included. With show false the tab is only added (or
    // left as it is), hidden behind the active one. False when the note could not be opened.
    async function openNote(id, { show = true } = {}) {
        if (isMinimized()) restore({ focus: false });
        if (tabs.has(id)) { if (show) activate(id); return true; }
        try {
            const response = await fetch(`${settings.tabUrl}&note=${encodeURIComponent(id)}`, { headers: { Accept: "text/html" } });
            if (!response.ok) throw new Error(String(response.status));
            const fragment = document.createElement("template");
            fragment.innerHTML = await response.text();
            if (removed) return false; // the window closed meanwhile
            const item = fragment.content.querySelector("[data-editor-tab]");
            const panel = fragment.content.querySelector("[data-editor-panel]");
            if (!item || !panel) throw new Error("fragment");
            if (tabs.has(id)) { if (show) activate(id); return true; } // opened twice while loading
            if (!show) {
                // Not the selected tab: its panel stays hidden until the tab is chosen.
                item.querySelector("[data-editor-tab-select]").setAttribute("aria-selected", "false");
                item.querySelector("[data-editor-tab-select]").tabIndex = -1;
                panel.hidden = true;
            }
            tabList.append(item);
            panels.append(panel);
            const added = addTab(item, panel);
            if (show) activate(added);
            return true;
        } catch {
            showStatusMessage(shared.messages, "error", shared.texts.openFailed);
            return false;
        }
    }

    // A reference's notes, in order: each gets its tab (an open note keeps its own), then the first one that opened is
    // shown. One note is the same as opening it from the board.
    async function openNotes(ids) {
        if (ids.length === 1) return openNote(ids[0]);
        let first = null;
        for (const id of ids) {
            if (await openNote(id, { show: false }) && first === null) first = id;
        }
        if (first !== null) activate(first);
        return first !== null;
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

    // Close (in the header and on the minimized form), Escape and a click beside the window (modal.js, modal:close) take
    // the window away in place when they can; otherwise Close is a link to the board and modal.js goes there, and leaving
    // the page asks first about unsaved changes.
    for (const link of dialog.querySelectorAll("[data-editor-close]")) {
        link.addEventListener("click", event => {
            if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || !closesInPlace()) return;
            event.preventDefault();
            removeWindow();
        });
    }
    dialog.addEventListener("modal:close", event => {
        if (!closesInPlace()) return;
        event.preventDefault();
        removeWindow();
    });

    const { signal } = listeners;
    // Ctrl+S / Cmd+S saves the active note, not while the editor is minimized.
    document.addEventListener("keydown", event => {
        if (!isMinimized() && (event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
            event.preventDefault();
            tabs.get(activeId)?.editor.save();
        }
    }, { signal });
    // Leaving the page (Close or Escape with unsaved changes, the board) asks first when any tab has unsaved changes.
    window.addEventListener("beforeunload", event => {
        if (!leaving && [...tabs.values()].some(tab => tab.editor.isDirty())) event.preventDefault();
    }, { signal });
    // Open and double-click on a board card (notes-board.js) come here while the editor is on the page.
    document.addEventListener("note-editor:open", event => {
        event.preventDefault();
        openNote(String(event.detail.id));
    }, { signal });
    // Two cards swapped on the board: the tabs of those notes take the notes' new versions.
    document.addEventListener("note-board:versions", event => {
        for (const change of event.detail) tabs.get(String(change.id))?.editor.followVersion(change.previous, change.version);
    }, { signal });

    const first = dialog.querySelector("[data-editor-tab]");
    activate(addTab(first, dialog.querySelector("[data-editor-panel]")));
}

// Back or Forward to an address the page took without loading (the board once the window closed in place) loads it.
const reloadOnHistory = () => window.location.reload();

// Sets up the editor window on the page, once: the one the page loads with (/?note={id}), or the one notes-board.js has
// just put there. notes-board.js calls it after importing this module, which runs only once, so a window opened again
// after one closed in place is set up too.
const startedWindows = new WeakSet();
export function startEditor() {
    const dialog = document.querySelector("dialog.note-editor-dialog");
    const dataElement = document.getElementById("note-editor-data");
    if (!dialog || !dataElement || startedWindows.has(dialog)) return;
    startedWindows.add(dialog);
    initializeEditorWindow(dialog, JSON.parse(dataElement.textContent), dataElement);
}

startEditor();
