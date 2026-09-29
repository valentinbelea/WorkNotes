namespace WorkNotes.Business.Models;

// The editor's lookup of a reference just typed: the reference the text ends with (Match, where it starts in that text,
// as written), when there is one, and the notes it opens (Targets, in the order of their ids) when it is Found.
public sealed record NoteReferenceLookup(NoteReferenceLookupStatus Status, NoteReferenceMatch? Match = null,
    IReadOnlyList<NoteReferenceTarget>? Targets = null);
