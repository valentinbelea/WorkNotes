namespace WorkNotes.Business.Models;

// Values stored in NoteReferences.ReferenceType (CK_NoteReferences_ReferenceType): what an internal reference names.
public static class NoteReferenceTypes
{
    public const string Cr = "CR";
    public const string Bug = "BUG";

    public static bool IsValid(string? value) => value is Cr or Bug;
}
