namespace WorkNotes.Business.Models;

// One note as creating the references of the existing notes left it: the numbers of its text that other notes have in
// their title, in the order they first appear, and whether its references were made.
public sealed record NoteReferenceBackfillNote(
    int NoteId,
    string? Title,
    NoteReferenceBackfillStatus Status,
    IReadOnlyList<NoteReferenceBackfillMatch> Matches);
