namespace WorkNotes.Business.Models;

// Where a paragraph shows a link: its text from Start, Length characters, is the reference ReferenceType and
// ReferenceNumber (CR 30080) and opens the notes TargetNoteIds (one or more, in the order of their ids).
public sealed record NoteReferenceLink(int Start, int Length, string ReferenceType, long ReferenceNumber, IReadOnlyList<int> TargetNoteIds)
{
    public string NormalizedReference => NoteReferenceRules.Normalize(ReferenceType, ReferenceNumber);
}
