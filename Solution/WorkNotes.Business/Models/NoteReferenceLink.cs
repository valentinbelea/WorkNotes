namespace WorkNotes.Business.Models;

// Where a paragraph shows a link: its text from Start, as it is written there (CR_30080), is the reference ReferenceType
// and ReferenceNumber and opens the notes TargetNoteIds (one or more, in the order of their ids).
public sealed record NoteReferenceLink(int Start, string Text, string ReferenceType, long ReferenceNumber, IReadOnlyList<int> TargetNoteIds)
{
    public int Length => Text.Length;
    public string NormalizedReference => NoteReferenceRules.Normalize(ReferenceType, ReferenceNumber);
}
