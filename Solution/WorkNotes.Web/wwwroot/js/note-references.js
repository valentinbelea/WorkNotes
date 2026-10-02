// Internal references in the editor (note-editor.js adds this extension to every note's CodeMirror). The server reads
// the references of the text (CR 30080, bug-1234...) and knows which notes each opens (INoteReferenceService); this only
// shows them: every place the server sent is marked as a link, with the title and type of each of its notes as its
// tooltip, and the text stays exactly as it is written. A click on a link (or Ctrl+Enter with the cursor on it) opens
// its notes in tabs of the editor, one, two or more. Typing inside a link, or a letter or digit right next to it, removes
// that link until the text is saved (the answer of the save brings the links of the saved text); a word end typed right
// next to it (a space, punctuation) leaves it, since the reference stays the same.
// A word ended right after a number (by one of wordEnds, or by Tab) asks the server whether the text before it ends with
// a reference, unless a link ends there; a popup above it shows the answer (referenceLookup) and offers two ways to add
// the reference: the application reference (a link to the notes that name it) and the Git reference (a branch of one of
// the imported repositories whose name contains it). The drawer beside the text lists the note's references and the
// branches linked to them (referenceDrawer). Appearance comes from note-editor.css; texts come from the page.
import { StateField, StateEffect, EditorView, Decoration, keymap, showTooltip, tooltips, Transaction } from "../lib/codemirror/codemirror.js";

// The links of the text after a save, in positions of the current document: { links: [{ from, to, notes }], labels }
// (notes: the ids of the notes the link opens, in order; labels: note id -> tooltip).
export const setLinks = StateEffect.define();

// A link made from the lookup's popup before the save, in positions of the current document: { from, to, notes, title }.
const addLink = StateEffect.define({
    map: (link, changes) => ({ ...link, from: changes.mapPos(link.from, 1), to: changes.mapPos(link.to, -1) })
});

// The popup of a reference that leads to two places (its notes and a branch linked to it), at { pos, notes, branches } or null.
const openChoice = StateEffect.define();
const closeChoice = StateEffect.define();

// The characters that end a word. Typed right after a digit, one of them asks the server whether the text before it ends
// with a reference; Tab does the same (it inserts nothing). Typed right next to a link, one of them leaves it a link.
// Only the server reads references (a date or a quantity ends with no reference, so nothing is shown for it).
const wordEnds = [" ", "\u00a0", "\t", "\n", ".", ",", ";", ":", "-", "_", "!", "?", "(", ")", "[", "]", "{", "}", "/", "\\", "'", "\"",
    "„", "”", "«", "»", "…"];

// Whether an edit leaves the link from-to as it is: nothing changed inside it, and text typed right against it begins
// (after it) or ends (before it) with a word end, so the link is still the same whole word.
function keepsLink(changes, from, to) {
    let kept = true;
    changes.iterChanges((fromA, toA, fromB, toB, inserted) => {
        if (!kept || toA < from || fromA > to) return;
        const text = inserted.toString();
        if (fromA === toA && fromA === to && wordEnds.includes(text[0])) return;
        if (fromA === toA && fromA === from && wordEnds.includes(text[text.length - 1])) return;
        kept = false;
    });
    return kept;
}

// A link's mark: its class, its tooltip (one line per note) and the ids of its notes, in order, for opening them.
const linkMark = (notes, title, reference) => Decoration.mark({
    class: "cm-note-reference", attributes: { title, "data-note-reference": notes.join(" "), "data-reference": reference ?? "" }
});

function build(links, labels, length) {
    const marks = [];
    for (const link of links) {
        const notes = Array.isArray(link.notes) ? link.notes.map(String) : [];
        if (!(link.from >= 0 && link.to > link.from && link.to <= length) || notes.length === 0) continue;
        marks.push(linkMark(notes, notes.map(note => labels.get(note) ?? "").filter(Boolean).join("\n"), link.reference).range(link.from, link.to));
    }
    return Decoration.set(marks, true);
}

// links: the links of the text as loaded ({ from, to, notes } in document positions); targets: [{ id, label }];
// open(ids): opens notes in the editor's tabs; choice: { template, branchTemplate, branches(reference) } for the popup of a reference
// that also has branches linked to it, or null; lookup: what referenceLookup needs, or null in a read-only editor.
export function noteReferences({ links, targets, open, lookup, choice }) {
    const labels = new Map(targets.map(target => [String(target.id), target.label]));

    const shown = StateField.define({
        create: state => build(links, labels, state.doc.length),
        update(value, transaction) {
            let next = transaction.docChanged
                ? value.update({ filter: (from, to) => keepsLink(transaction.changes, from, to) }).map(transaction.changes)
                : value;
            for (const effect of transaction.effects) {
                if (effect.is(setLinks)) next = build(effect.value.links, effect.value.labels, transaction.state.doc.length);
                else if (effect.is(addLink) && effect.value.to > effect.value.from)
                    next = next.update({ add: [linkMark(effect.value.notes, effect.value.title, effect.value.reference).range(effect.value.from, effect.value.to)] });
            }
            return next;
        },
        provide: field => EditorView.decorations.from(field)
    });

    // The link at a position (the cursor inside it, or right before or after it): its notes, its reference and its end.
    function linkAt(state, pos) {
        let found = null;
        state.field(shown).between(pos, pos, (from, to, value) => {
            found = { notes: value.spec.attributes["data-note-reference"].split(" "), reference: value.spec.attributes["data-reference"], to };
            return false;
        });
        return found;
    }

    // A link with no branch linked to its reference opens its notes at once; one with branches asks where to go.
    function follow(view, notes, reference, to) {
        const branches = choice && reference ? choice.branches(reference) : [];
        if (branches.length === 0) { open(notes); return; }
        view.dispatch({ effects: openChoice.of({ pos: to, notes, branches }) });
    }

    const choiceField = choice ? referenceChoice(choice, open) : null;

    const extensions = [
        shown,
        ...(choiceField ?? []),
        EditorView.domEventHandlers({
            click(event, view) {
                const link = event.target instanceof Element ? event.target.closest("[data-note-reference]") : null;
                if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return false;
                // The click that ends a selection only selects.
                if (!view.state.selection.main.empty) return false;
                event.preventDefault();
                follow(view, link.dataset.noteReference.split(" "), link.dataset.reference, view.posAtDOM(link, link.childNodes.length));
                return true;
            }
        }),
        keymap.of([{
            key: "Mod-Enter",
            run(view) {
                const found = linkAt(view.state, view.state.selection.main.head);
                if (found === null) return false;
                follow(view, found.notes, found.reference, found.to);
                return true;
            }
        }])
    ];
    return lookup ? [...extensions, ...referenceLookup(lookup, (state, to) => {
        let linked = false;
        state.field(shown).between(to, to, (start, end) => { if (end === to) linked = true; });
        return linked;
    })] : extensions;
}

// ---- The choice of a reference with a branch ------------------------------------------------------------------------

// The popup above a link whose reference also has branches linked to it: open its notes (the application reference) or a
// branch on GitHub, in a new tab. Tab does the first, Shift+Tab the second (the list gets the focus when there are
// several branches), Escape closes it; typing, moving the cursor or leaving the editor closes it too.
function referenceChoice({ template, branchTemplate }, open) {
    const field = StateField.define({
        create: () => null,
        update(value, transaction) {
            for (const effect of transaction.effects) {
                if (effect.is(openChoice)) return effect.value;
                if (effect.is(closeChoice)) return null;
            }
            return value && (transaction.docChanged || transaction.selectionSet) ? null : value;
        },
        provide: f => showTooltip.from(f, value => value && { pos: value.pos, above: true, strictSide: false, create: view => popup(view, value) })
    });

    function popup(view, value) {
        const dom = template.content.firstElementChild.cloneNode(true);
        const close = () => view.dispatch({ effects: closeChoice.of(null) });
        dom.querySelector("[data-choice-notes]").addEventListener("click", () => { close(); open(value.notes); view.focus(); });
        dom.querySelector("[data-choice-branches]").replaceChildren(...value.branches
            .filter(branch => typeof branch.url === "string" && branch.url.startsWith("https://"))
            .map(branch => {
                const item = branchTemplate.content.firstElementChild.cloneNode(true);
                const link = item.querySelector("[data-choice-branch]");
                link.href = branch.url;
                link.title = `${branch.repository}: ${branch.branch}`;
                link.querySelector("[data-choice-repository]").textContent = branch.repository;
                link.querySelector("[data-choice-name]").textContent = branch.branch;
                link.addEventListener("click", () => setTimeout(close, 0));
                return item;
            }));
        // A click keeps the focus in the text; the links still open.
        dom.addEventListener("mousedown", event => event.preventDefault());
        dom.addEventListener("keydown", event => {
            if (event.key !== "Escape") return;
            event.preventDefault();
            close();
            view.focus();
        });
        return { dom, offset: { x: 0, y: 4 } };
    }

    const keys = keymap.of([
        { key: "Escape", run(view) { if (!view.state.field(field, false)) return false; view.dispatch({ effects: closeChoice.of(null) }); return true; } },
        { key: "Tab", run(view) {
            const value = view.state.field(field, false);
            if (!value) return false;
            view.dispatch({ effects: closeChoice.of(null) });
            open(value.notes);
            return true;
        } },
        { key: "Shift-Tab", run(view) {
            const value = view.state.field(field, false);
            if (!value) return false;
            const links = [...view.dom.querySelectorAll("[data-choice-branch]")];
            if (links.length === 1) {
                links[0].click();
                return true;
            }
            links[0]?.focus();
            return links.length > 0;
        } }
    ]);
    const closeOnBlur = EditorView.domEventHandlers({ blur(event, view) { if (view.state.field(field, false)) view.dispatch({ effects: closeChoice.of(null) }); return false; } });
    return [field, keys, closeOnBlur];
}

// ---- The lookup of a reference just typed --------------------------------------------------------------------------

const isDigit = character => character >= "0" && character <= "9";
// How long the popup says a reference was not found before it closes by itself.
const missingShownFor = 4000;
// The popup keeps this far from the edges of the window (on a phone it would touch them otherwise).
const windowGutter = 16;

const openLookup = StateEffect.define();
const answerLookup = StateEffect.define();
const closeLookup = StateEffect.define();

// The popup of a reference just typed, above it (a CodeMirror tooltip, a copy of the page's template). While the server
// looks the text up it shows the search (note-editor.css shows it only when the answer takes a moment); then either the
// notes the reference opens, with the button that makes it a link now (Tab does the same; the save links it anyway), or
// that no note has it in its title, for a few seconds. No reference: nothing. It closes with Escape, when the text asked
// about changes, when the cursor leaves the line where the word ended (the next one, after Enter) or goes back before the
// reference's end, when the text is clicked and when the editor loses the focus.
// length: how much of the line before the end of the word is sent; find(text, signal): the server's answer, or null;
// template, noteTemplate: the popup and one of its notes; linkEndsAt(state, to): whether a link ends at to (the text
// before is a link already: nothing to ask).
// git: the Git reference option, or null (no option): { repositories: [{ id, name }], choice: { repository }, search, add }.
// search({ repository, reference }, signal) gives { message, note, branches: [{ name, url }] } (it throws only when
// aborted); add({ repository, reference, branch, position }) gives { ok, message }, where position() is where the
// reference starts now (null once the popup is gone). branchTemplate: one branch of the popup's list. The option opens
// with its button or Shift+Tab; picking a branch links it to the paragraph and closes the popup.
function referenceLookup({ length, find, create, template, noteTemplate, branchTemplate, git }, linkEndsAt) {
    let tokens = 0;
    let request = null;     // the lookup on its way: { token, controller }
    let closing = 0;        // the timer that closes a popup that found no note
    let tabbed = null;      // where Tab last asked ({ doc, head }): the next Tab there moves the focus on
    let shown = null;       // the open popup's Git option ({ open }), for Shift+Tab

    // { token, from, to, anchor, state, message, notes, reference, tooltip }: from and to are the text asked about, then
    // the reference found in it (reference: as CR:30080); anchor is where the cursor was when the word ended; state is
    // "searching", "found" or "missing".
    const lookup = StateField.define({
        create: () => null,
        update(value, transaction) {
            for (const effect of transaction.effects) {
                if (effect.is(openLookup)) value = effect.value;
                else if (effect.is(closeLookup) && value?.token === effect.value) value = null;
                else if (effect.is(answerLookup) && value?.token === effect.value.token)
                    value = { ...value, ...effect.value, tooltip: { ...value.tooltip, pos: effect.value.from } };
            }
            if (!value) return null;
            if (transaction.docChanged) {
                if (transaction.changes.touchesRange(value.from, value.to)) return null;
                const from = transaction.changes.mapPos(value.from, 1), to = transaction.changes.mapPos(value.to, -1);
                value = { ...value, from, to, anchor: transaction.changes.mapPos(value.anchor, -1),
                    tooltip: { ...value.tooltip, pos: value.state === "searching" ? to : from } };
            }
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

    const alive = view => view.dom.isConnected;

    function close(view, token) {
        if (alive(view) && view.state.field(lookup, false)?.token === token) view.dispatch({ effects: closeLookup.of(token) });
    }

    // The reference found becomes a link now, as the save would draw it, and the typing goes on.
    function accept(view) {
        const value = view.state.field(lookup, false);
        if (value?.state !== "found") return false;
        const effects = [closeLookup.of(value.token)];
        if (!linkEndsAt(view.state, value.to))
            effects.push(addLink.of({ from: value.from, to: value.to, notes: value.notes.map(note => String(note.id)),
                title: value.notes.map(note => note.label).join("\n"), reference: value.reference }));
        view.dispatch({ effects });
        view.focus();
        return true;
    }

    function popup(view) {
        const dom = template.content.firstElementChild.cloneNode(true);
        const message = dom.querySelector("[data-lookup-message]");
        const list = dom.querySelector("[data-lookup-notes]");
        const quickCreate = dom.querySelector("[data-lookup-quick-create]");
        const gitButton = dom.querySelector("[data-lookup-git-open]");
        const gitPanel = dom.querySelector("[data-lookup-git]");
        const gitOption = git && gitButton && gitPanel ? gitOptionOf(view, dom, gitButton, gitPanel) : null;
        if (!gitOption) { gitButton?.remove(); gitPanel?.remove(); }
        let state = "searching";
        let token = null;
        const api = { open: () => gitOption?.open(true) ?? false };
        const render = value => {
            if (!value) return;
            // Another lookup in the same popup: the Git option starts again for its reference.
            const changedToken = value.token !== token;
            if (changedToken) {
                token = value.token;
                gitOption?.reset();
                if (quickCreate) {
                    quickCreate.disabled = false;
                    quickCreate.textContent = quickCreate.dataset.textIdle;
                }
            }
            if (!changedToken && value.state === state) return;
            dom.classList.replace(`note-reference-lookup--${state}`, `note-reference-lookup--${value.state}`);
            dom.classList.toggle("note-reference-lookup--can-create", value.state === "missing" && value.canCreate === true);
            state = value.state;
            message.textContent = value.message;
            list.replaceChildren(...(value.notes ?? []).map(note => {
                const item = noteTemplate.content.firstElementChild.cloneNode(true);
                item.classList.toggle("note-reference-lookup__note--article", note.article === true);
                item.querySelector("[data-lookup-type]").textContent = note.type;
                item.querySelector("[data-lookup-name]").textContent = note.title;
                return item;
            }));
        };
        // A click keeps the focus in the text, so the typing goes on; only the repository list needs the focus to open.
        dom.addEventListener("mousedown", event => {
            if (!(event.target instanceof Element && event.target.closest("select"))) event.preventDefault();
        });
        dom.querySelector("[data-lookup-create]").addEventListener("click", () => accept(view));
        quickCreate?.addEventListener("click", async () => {
            const value = view.state.field(lookup, false);
            if (!create || value?.state !== "missing" || value.canCreate !== true || quickCreate.disabled) return;
            clearTimeout(closing);
            quickCreate.disabled = true;
            quickCreate.textContent = quickCreate.dataset.textBusy;
            const result = await create({ reference: value.reference, title: value.title });
            if (!alive(view) || view.state.field(lookup, false)?.token !== value.token) return;
            if (result.ok) { close(view, value.token); return; }
            message.textContent = result.message;
            quickCreate.disabled = false;
            quickCreate.textContent = quickCreate.dataset.textIdle;
        });
        dom.addEventListener("keydown", event => {
            if (event.key !== "Escape") return;
            // Used here: the dialog must not take it as a request to close the editor.
            event.preventDefault();
            close(view, view.state.field(lookup, false)?.token);
            view.focus();
        });
        shown = api;
        render(view.state.field(lookup, false));
        return {
            dom, offset: { x: 0, y: 4 },
            update: update => render(update.state.field(lookup, false)),
            destroy() {
                if (shown === api) shown = null;
                gitOption?.reset();
            }
        };
    }

    // The Git reference option of one popup: its button opens a panel with the imported repositories to choose from and
    // the branches of the chosen one whose name contains the reference, read from GitHub when it opens and when the
    // repository changes. A branch button links it (git.add). reset() closes the panel and forgets what it showed.
    function gitOptionOf(view, dom, button, panel) {
        const select = panel.querySelector("[data-lookup-git-repository]");
        const none = panel.querySelector("[data-lookup-git-none]");
        const messageElement = panel.querySelector("[data-lookup-git-message]");
        const branches = panel.querySelector("[data-lookup-git-branches]");
        const texts = panel.dataset;
        let searching = null;   // the AbortController of the search on its way
        let adding = false;

        select.replaceChildren(...git.repositories.map(repository => {
            const option = document.createElement("option");
            option.value = repository.id;
            option.textContent = repository.name;
            return option;
        }));
        const noRepositories = git.repositories.length === 0;
        select.closest("label").hidden = noRepositories;
        none.hidden = !noRepositories;
        if (!noRepositories && git.repositories.some(repository => repository.id === git.choice.repository))
            select.value = git.choice.repository;
        button.hidden = false;

        const setBusy = busy => {
            select.disabled = busy;
            for (const branch of branches.querySelectorAll("button")) branch.disabled = busy;
        };

        function search() {
            const value = view.state.field(lookup, false);
            searching?.abort();
            branches.replaceChildren();
            if (noRepositories || !value?.reference || !select.value) { messageElement.textContent = ""; return; }
            git.choice.repository = select.value;
            const controller = new AbortController();
            searching = controller;
            messageElement.textContent = texts.textSearching;
            git.search({ repository: select.value, reference: value.reference }, controller.signal).then(result => {
                if (controller.signal.aborted) return;
                messageElement.textContent = [result.message, result.note].filter(Boolean).join(" ");
                branches.replaceChildren(...(result.branches ?? []).map(branch => {
                    const item = branchTemplate.content.firstElementChild.cloneNode(true);
                    item.querySelector("[data-lookup-git-name]").textContent = branch.name;
                    const choose = item.querySelector("[data-lookup-git-branch]");
                    choose.title = branch.name;
                    choose.addEventListener("click", () => link(branch));
                    return item;
                }));
            }, () => { if (!controller.signal.aborted) messageElement.textContent = texts.textFailed; });
        }

        // Links the branch to the reference of the paragraph; on success the popup closes, otherwise it says why.
        async function link(branch) {
            const value = view.state.field(lookup, false);
            if (adding || !value?.reference) return;
            adding = true;
            setBusy(true);
            messageElement.textContent = texts.textAdding;
            let result;
            try {
                result = await git.add({ repository: select.value, reference: value.reference, branch: branch.name,
                    position: () => view.state.field(lookup, false)?.from ?? null });
            } catch {
                result = { ok: false, message: texts.textFailed };
            }
            adding = false;
            if (!alive(view)) return;
            if (result.ok) {
                close(view, value.token);
                view.focus();
                return;
            }
            // Still the same popup: it stays open with the reason; otherwise the status message has shown it.
            if (view.state.field(lookup, false)?.token === value.token) {
                setBusy(false);
                messageElement.textContent = result.message ?? texts.textFailed;
            }
        }

        select.addEventListener("change", search);
        button.addEventListener("click", () => (panel.hidden ? open(false) : reset()));

        // Opens the panel (focus: moves the focus to the repository list, for the keyboard) and searches.
        function open(focus) {
            if (adding) return true;
            clearTimeout(closing);
            if (panel.hidden) {
                panel.hidden = false;
                button.setAttribute("aria-expanded", "true");
                // The popup is no longer one status message: its parts announce themselves.
                dom.setAttribute("role", "group");
                dom.removeAttribute("aria-atomic");
                search();
            }
            if (focus) (noRepositories ? panel.querySelector("a") : select)?.focus();
            return true;
        }

        function reset() {
            searching?.abort();
            searching = null;
            panel.hidden = true;
            button.setAttribute("aria-expanded", "false");
            branches.replaceChildren();
            messageElement.textContent = "";
            setBusy(false);
        }

        return { open, reset };
    }

    // Asks about the text of the line before to (at most length characters), where a word has just ended.
    function start(view, to) {
        const doc = view.state.doc;
        const from = Math.max(doc.lineAt(to).from, to - length);
        const token = ++tokens;
        request?.controller.abort();
        clearTimeout(closing);
        const controller = new AbortController();
        request = { token, controller };
        view.dispatch({ effects: openLookup.of({ token, from, to, anchor: view.state.selection.main.head, state: "searching",
            tooltip: { pos: to, above: true, create: popup } }) });
        find(doc.sliceString(from, to), controller.signal).then(answer => {
            const value = alive(view) ? view.state.field(lookup, false) : null;
            if (value?.token !== token) return;
            const status = answer?.status;
            const referenceFrom = value.from + answer?.start;
            // The reference ends where the word ended; anything else is no answer to this text.
            if ((status !== "found" && status !== "missing") || !(answer.start >= 0) || referenceFrom + answer.length !== value.to) {
                close(view, token);
                return;
            }
            const notes = status === "found" ? (answer.notes ?? []).filter(note => Number.isInteger(note.id)) : [];
            if (status === "found" && notes.length === 0) { close(view, token); return; }
            const reference = typeof answer.reference === "string" ? answer.reference : null;
            view.dispatch({ effects: answerLookup.of({ token, from: referenceFrom, to: value.to, state: status, message: answer.message,
                notes, reference, title: answer.title, canCreate: answer.canCreate === true }) });
            // A reference no note has stays for a few seconds, unless the Git option can be used (it has repositories to look in).
            if (status === "missing" && answer.canCreate !== true && !(git && git.repositories.length > 0 && reference))
                closing = setTimeout(() => close(view, token), missingShownFor);
        }, () => close(view, token));
    }

    return [
        lookup,
        tooltips({ tooltipSpace: () => ({ left: windowGutter, top: windowGutter, right: window.innerWidth - windowGutter,
            bottom: window.innerHeight - windowGutter }) }),
        keymap.of([
            {
                // Tab makes the reference found a link. After a number it also ends the word and asks, once: the next Tab
                // there moves the focus on, as usual.
                key: "Tab",
                run(view) {
                    const value = view.state.field(lookup, false);
                    if (value?.state === "found") return accept(view);
                    const { main } = view.state.selection;
                    if (value || !main.empty || !isDigit(view.state.sliceDoc(main.head - 1, main.head))) return false;
                    if (linkEndsAt(view.state, main.head)) return false;
                    if (tabbed?.doc === view.state.doc && tabbed.head === main.head) return false;
                    tabbed = { doc: view.state.doc, head: main.head };
                    start(view, main.head);
                    return true;
                }
            },
            {
                // Opens the Git reference option of the popup once it has its answer, with the focus on the repositories.
                key: "Shift-Tab",
                run(view) {
                    const value = view.state.field(lookup, false);
                    if (!git || !shown || !value?.reference || value.state === "searching") return false;
                    return shown.open();
                }
            },
            {
                // Closes the popup; while it only searches (not shown yet) Escape does what it would do anyway.
                key: "Escape",
                run(view) {
                    const value = view.state.field(lookup, false);
                    if (!value) return false;
                    close(view, value.token);
                    return value.state !== "searching";
                }
            }
        ]),
        EditorView.domEventHandlers({
            blur(event, view) {
                const value = view.state.field(lookup, false);
                const inPopup = event.relatedTarget instanceof Element && event.relatedTarget.closest(".note-reference-lookup");
                if (value && !inPopup) close(view, value.token);
            }
        }),
        EditorView.updateListener.of(update => {
            if (!update.docChanged) return;
            // A word ended by typing (a character, or Enter), not a paste or a deletion.
            const last = update.transactions[update.transactions.length - 1];
            if (!last.isUserEvent("input.type") && last.annotation(Transaction.userEvent) !== "input") return;
            let change = null, count = 0;
            last.changes.iterChanges((fromA, toA, fromB, toB, inserted) => { count++; change = { at: fromB, text: inserted.toString() }; });
            if (count !== 1 || !change.text || !wordEnds.includes(change.text[0])) return;
            if (!isDigit(update.state.sliceDoc(change.at - 1, change.at)) || linkEndsAt(update.state, change.at)) return;
            const doc = update.state.doc;
            // Not while the update is being applied.
            queueMicrotask(() => { if (alive(update.view) && update.view.state.doc === doc) start(update.view, change.at); });
        })
    ];
}

// The drawer of the note's references (_NoteEditorTabPanel), rendered by the server with the note: a plain click on one
// of its links opens the link's notes in the editor's tabs (with a modifier key or another button the browser follows
// the link to the page of its first note). show(list, labels) replaces the list after a save: list is [{ label, notes }]
// in the order of the text (label: the reference as its first link writes it; notes: the ids of the notes it opens),
// labels maps a note id to its "title" (type), one line of the tooltip. The entries are copies of the drawer's template,
// filled with textContent.
// Below it the branches linked to the references of the text (the Git references): showGit(list) replaces them, list is
// [{ id, reference, repository, branch, url, remove }] as the server sends it after adding or removing one (remove: the
// name of the button that removes the link, which the owner has: removeGit(id) is given only to the owner's editor).
// The number on the drawer's strip counts both lists.
export function referenceDrawer(drawer, { open, noteUrl, removeGit }) {
    const list = drawer.querySelector("[data-references-list]");
    const count = drawer.querySelector("[data-references-count]");
    const empty = drawer.querySelector("[data-references-empty]");
    const entryTemplate = drawer.querySelector("template[data-references-entry]");
    const gitSection = drawer.querySelector("[data-references-git]");
    const gitList = drawer.querySelector("[data-references-git-list]");
    const gitTemplate = drawer.querySelector("template[data-references-git-entry]");
    let applicationCount = list.children.length;
    let gitCount = gitList?.children.length ?? 0;

    function refresh() {
        const total = applicationCount + gitCount;
        count.textContent = String(total);
        count.classList.toggle("note-references__count--none", total === 0);
        empty.hidden = total > 0;
    }

    // The remove buttons work with JavaScript only: they show once it runs, and only in the owner's editor.
    const showRemoveButtons = () => {
        for (const button of gitList?.querySelectorAll("[data-git-remove]") ?? []) button.hidden = !removeGit;
    };
    showRemoveButtons();

    drawer.addEventListener("click", event => {
        if (!(event.target instanceof Element)) return;
        const remove = removeGit ? event.target.closest("button[data-git-remove]") : null;
        if (remove) {
            removeGit(Number(remove.dataset.gitRemove), remove);
            return;
        }
        const link = event.target.closest("a[data-note-reference]");
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
            entries.push(entry);
        }
        list.replaceChildren(...entries);
        applicationCount = entries.length;
        refresh();
    }

    function showGit(references) {
        if (!gitList || !gitTemplate) return;
        const entries = [];
        for (const reference of references ?? []) {
            // A branch is only ever linked to a https address.
            if (typeof reference.url !== "string" || !reference.url.startsWith("https://")) continue;
            const entry = gitTemplate.content.firstElementChild.cloneNode(true);
            entry.querySelector("[data-git-reference]").textContent = reference.reference;
            entry.querySelector("[data-git-repository]").textContent = reference.repository;
            const branch = entry.querySelector("[data-git-branch]");
            branch.textContent = reference.branch;
            branch.href = reference.url;
            branch.title = `${reference.repository}: ${reference.branch}`;
            const remove = entry.querySelector("[data-git-remove]");
            remove.dataset.gitRemove = String(reference.id);
            remove.setAttribute("aria-label", reference.remove);
            remove.title = reference.remove;
            remove.hidden = !removeGit;
            entries.push(entry);
        }
        gitList.replaceChildren(...entries);
        gitSection.hidden = entries.length === 0;
        gitCount = entries.length;
        refresh();
    }

    return { show, showGit };
}
