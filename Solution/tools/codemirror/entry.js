// The CodeMirror 6 APIs WorkNotes uses; bundled into wwwroot/lib/codemirror/codemirror.js by build.mjs.
export { EditorState, StateField, StateEffect, RangeSet, RangeValue, MapMode, Transaction } from "@codemirror/state";
export { EditorView, Decoration, keymap, placeholder, highlightActiveLine, drawSelection, highlightSpecialChars } from "@codemirror/view";
export { defaultKeymap, history, historyKeymap, invertedEffects, indentWithTab } from "@codemirror/commands";
export { search, searchKeymap, highlightSelectionMatches } from "@codemirror/search";
