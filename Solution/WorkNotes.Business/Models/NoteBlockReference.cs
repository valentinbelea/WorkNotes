namespace WorkNotes.Business.Models;

// A stored reference (a row of dbo.NoteReferences with its rows of dbo.NoteReferenceTargets): a reference of a paragraph,
// the text it is first written with in the paragraph and the notes it opens, in the order of their ids. One per
// paragraph and reference, however often the paragraph writes it; a reference that opens no note is not stored.
public sealed record NoteBlockReference(Guid NoteBlockId, string ReferenceType, long ReferenceNumber, string ReferenceText,
    IReadOnlyList<int> TargetNoteIds)
{
    public string NormalizedReference => NoteReferenceRules.Normalize(ReferenceType, ReferenceNumber);
}
