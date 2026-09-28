namespace WorkNotes.Business.Models;

// A stored reference (a row of dbo.NoteReferences): a reference of a paragraph with the note it opens, and the text it
// is first written with in the paragraph. One per paragraph and reference, however often the paragraph writes it.
public sealed record NoteBlockReference(Guid NoteBlockId, int TargetNoteId, string ReferenceType, long ReferenceNumber, string ReferenceText)
{
    public string NormalizedReference => NoteReferenceRules.Normalize(ReferenceType, ReferenceNumber);
}
