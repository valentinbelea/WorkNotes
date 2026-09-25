using System.Text.RegularExpressions;

namespace WorkNotes.Business.Models;

// Internal references between the notes of a board. In the text of a paragraph a reference is stored as
// [[note:{id}|{number}]]: the id of the target note, which stays the same when its title changes, and the number shown
// in its place (note-references.js reads the same form). The editor offers a reference when a typed number appears as
// a whole number in the title of another note; the text keeps the reference even when its target can no longer be
// opened, so it is shown as such instead of disappearing.
public static partial class NoteReferenceRules
{
    // Shorter numbers (days, months, counts) are too common in ordinary text to offer a reference for each of them.
    public const int MinNumberLength = 3;
    public const int MaxNumberLength = 18;
    // Ids a single request may ask about.
    public const int MaxTargetsPerRequest = 100;

    [GeneratedRegex(@"\[\[note:([0-9]{1,10})\|([0-9]{1,18})\]\]", RegexOptions.CultureInvariant)]
    private static partial Regex Markup();

    private const string MarkupStart = "[[note:";

    // A number a reference can be made from: ASCII digits only, MinNumberLength to MaxNumberLength of them.
    public static bool IsReferenceNumber(string? value) =>
        value is { Length: >= MinNumberLength and <= MaxNumberLength } && value.All(char.IsAsciiDigit);

    // The title has the number as a whole number, not next to other digits: 30080 is in "CR 30080", "CR-30080" and
    // "CR30080", not in "130080" or "300801".
    public static bool TitleContainsNumber(string? title, string number)
    {
        if (title is null || !IsReferenceNumber(number)) return false;
        for (var start = title.IndexOf(number, StringComparison.Ordinal); start >= 0;
             start = title.IndexOf(number, start + 1, StringComparison.Ordinal))
        {
            var end = start + number.Length;
            if ((start == 0 || !char.IsAsciiDigit(title[start - 1])) && (end == title.Length || !char.IsAsciiDigit(title[end])))
                return true;
        }
        return false;
    }

    public static string Format(int targetNoteId, string number) => $"[[note:{targetNoteId}|{number}]]";

    // The text in order: plain parts and references (TargetNoteId set, Text the number shown). Anything that does not
    // have the exact form, or names an impossible id, stays plain text.
    public static IReadOnlyList<NoteTextPart> Split(string? text)
    {
        var parts = new List<NoteTextPart>();
        if (string.IsNullOrEmpty(text)) return parts;
        var position = 0;
        foreach (Match match in Markup().Matches(text))
        {
            if (!TryReadId(match.Groups[1].Value, out var targetNoteId)) continue;
            if (match.Index > position) parts.Add(new NoteTextPart(text[position..match.Index], null));
            parts.Add(new NoteTextPart(match.Groups[2].Value, targetNoteId));
            position = match.Index + match.Length;
        }
        if (position < text.Length) parts.Add(new NoteTextPart(text[position..], null));
        return parts;
    }

    // The references in the paragraphs, each target and number once, in the order they first appear.
    public static IReadOnlyList<NoteReferenceInput> Find(IEnumerable<string> paragraphs) =>
        paragraphs
            .SelectMany(Split)
            .Where(part => part.TargetNoteId is not null)
            .Select(part => new NoteReferenceInput(part.TargetNoteId!.Value, part.Text))
            .Distinct()
            .ToList();

    // Text read only up to a length limit (the start of a paragraph for a card) can stop inside a reference; the
    // unfinished reference and what follows it are dropped, so no part of its form is shown.
    public static string WithoutUnfinishedReference(string text)
    {
        var start = text.LastIndexOf(MarkupStart, StringComparison.Ordinal);
        if (start < 0) return text;
        var match = Markup().Match(text, start);
        return match.Success && match.Index == start ? text : text[..start];
    }

    private static bool TryReadId(string digits, out int id) =>
        int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out id) && id > 0;
}
