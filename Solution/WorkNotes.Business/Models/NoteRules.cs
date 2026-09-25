using System.Text;

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

    // The card preview is read from the first paragraphs, at most PreviewSourceLength characters of each.
    public const int PreviewParagraphs = 3;
    public const int PreviewSourceLength = 300;
    public const int PreviewMaxLength = 280;

    // The start of the note for its card: the first paragraphs, one per line, shortened at a word boundary. References
    // stay in their stored form (the card shows them as links); each counts as the number it shows and is never cut in
    // half. A paragraph read only up to PreviewSourceLength characters may stop inside a reference, which is dropped.
    public static string? BuildPreview(IEnumerable<string>? paragraphs)
    {
        var text = string.Join("\n", (paragraphs ?? [])
            .Select(paragraph => (paragraph.Length >= PreviewSourceLength ? NoteReferenceRules.WithoutUnfinishedReference(paragraph) : paragraph).Trim())
            .Where(paragraph => paragraph.Length > 0));
        if (text.Length == 0) return null;
        var parts = NoteReferenceRules.Split(text);
        var visible = string.Concat(parts.Select(part => part.Text));
        if (visible.Length <= PreviewMaxLength) return text;
        var cut = visible.LastIndexOfAny([' ', '\n', '\t'], PreviewMaxLength);
        // A very long word is cut where the limit falls.
        if (cut < PreviewMaxLength / 2) cut = PreviewMaxLength;
        var preview = new StringBuilder();
        var shown = 0;
        foreach (var part in parts)
        {
            var room = cut - shown;
            if (part.Text.Length > room)
            {
                // Plain text is cut at the limit; a reference that does not fit whole is left out.
                if (part.TargetNoteId is null) preview.Append(part.Text, 0, room);
                break;
            }
            preview.Append(part.TargetNoteId is { } targetNoteId ? NoteReferenceRules.Format(targetNoteId, part.Text) : part.Text);
            shown += part.Text.Length;
        }
        return preview.ToString().TrimEnd() + "…";
    }
}
