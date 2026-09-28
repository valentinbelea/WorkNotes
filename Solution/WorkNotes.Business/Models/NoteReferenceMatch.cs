namespace WorkNotes.Business.Models;

// A reference found in a text (NoteReferenceRules.Find): where it starts, the text as it is written (CR_30080), its
// type (NoteReferenceTypes) and its number. NormalizedReference (CR:30080) is what references and titles are compared by.
public sealed record NoteReferenceMatch(int Start, string Text, string ReferenceType, long ReferenceNumber)
{
    public int Length => Text.Length;
    public string NormalizedReference => NoteReferenceRules.Normalize(ReferenceType, ReferenceNumber);
}
