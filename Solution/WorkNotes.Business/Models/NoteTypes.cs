namespace WorkNotes.Business.Models;

// Values stored in Notes.NoteType; the database CHECK constraint accepts only these.
public static class NoteTypes
{
    public const string Journal = "Journal";
    public const string Article = "Article";

    public static bool IsValid(string? value) => value is Journal or Article;
}
