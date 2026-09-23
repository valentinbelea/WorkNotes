namespace WorkNotes.Business.Models;

public static class NoteRules
{
    public const int TitleMaxLength = 200;

    // The title is optional so a quick capture is never blocked.
    public static bool ValidTitle(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || (value.Trim().Length <= TitleMaxLength && !value.Any(char.IsControl));

    public static string? NormalizeTitle(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
