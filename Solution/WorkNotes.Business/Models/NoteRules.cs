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

    // Limits for one save, so a note stays a readable document and a request stays small.
    public const int MaxBlocks = 5000;
    public const int MaxContentLength = 1_000_000;

    // A paragraph keeps its inner line breaks and tabs; other control characters are rejected.
    public static bool ValidBlockContent(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && !value.Any(character => char.IsControl(character) && character is not ('\n' or '\t'));

    // Line endings are stored as \n; blank lines around a paragraph are not part of it.
    public static string NormalizeBlockContent(string value) =>
        value.Replace("\r\n", "\n").Replace('\r', '\n').Trim('\n');
}
