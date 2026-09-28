using System.Globalization;
using System.Text.RegularExpressions;

namespace WorkNotes.Business.Models;

// Internal references between notes: a CR or a bug written with its number, as CR 30080, CR-30080, CR_30080, CR30080,
// bug 1234, bug-1234, bug_1234 or bug1234. The type is read in any case; between the type and the number there are
// spaces, one - or _, or nothing. The type and the number are whole words: a letter, digit or combining mark right
// before the type or right after the number makes them part of a longer word (XCR30080A is no reference). A reference
// is compared by its type and number only (CR:30080), so CR-30080 and cr 30080 name the same CR, and CR 1234 is not
// bug 1234. A note's title is read with the same rules to know which references name it.
// The text itself never changes: the links are stored apart (NoteReferences) and drawn over the text.
// Scripts/version_0.02/004_ReplaceNoteReferences.sql reads the stored text with the same rules.
public static partial class NoteReferenceRules
{
    // At most this many spaces between the type and the number, and this many digits (the number is a bigint).
    public const int MaxSeparatorLength = 50;
    public const int MaxNumberLength = 18;
    // The longest text read as one reference: BUG, the spaces and the digits (NoteReferences.ReferenceText).
    public const int MaxTextLength = 3 + MaxSeparatorLength + MaxNumberLength;

    // The limits above, written out: a generated expression takes only a constant string. The spaces are the usual one
    // and the no-break space, which text copied from documents and pages often has.
    [GeneratedRegex(@"(?<![\p{L}\p{N}\p{M}])(?<type>[Cc][Rr]|[Bb][Uu][Gg])(?:[  ]{1,50}|[-_])?(?<number>[0-9]{1,18})(?![\p{L}\p{N}\p{M}])",
        RegexOptions.CultureInvariant)]
    private static partial Regex Reference();

    // The references in a text, in text order, each as it is written there.
    public static IReadOnlyList<NoteReferenceMatch> Find(string? text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var found = new List<NoteReferenceMatch>();
        foreach (Match match in Reference().Matches(text))
        {
            var type = match.Groups["type"].Value.ToUpperInvariant();
            var number = long.Parse(match.Groups["number"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture);
            found.Add(new NoteReferenceMatch(match.Index, match.Value, type, number));
        }
        return found;
    }

    // The form references are compared and stored by: CR:30080, BUG:1234 (the number without leading zeros).
    public static string Normalize(string referenceType, long referenceNumber) =>
        $"{referenceType}:{referenceNumber.ToString(CultureInfo.InvariantCulture)}";

    // For each reference, the notes whose title has it, each note once.
    public static IReadOnlyDictionary<string, IReadOnlyList<int>> NotesByTitleReference(IEnumerable<(int NoteId, string? Title)> notes) =>
        notes
            .SelectMany(note => Find(note.Title).Select(match => (match.NormalizedReference, note.NoteId)))
            .Distinct()
            .GroupBy(item => item.NormalizedReference, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<int>)group.Select(item => item.NoteId).ToList(), StringComparer.Ordinal);

    // The note a reference written in noteId opens: the only note whose title has the reference, when it is another
    // note. None when no note has it, when several have it (the choice would be a guess) or when only noteId has it.
    public static int? TargetOf(IReadOnlyDictionary<string, IReadOnlyList<int>> notesByTitleReference, string normalizedReference, int noteId) =>
        notesByTitleReference.TryGetValue(normalizedReference, out var notes) && notes.Count == 1 && notes[0] != noteId ? notes[0] : null;

    // Where a paragraph shows links: every reference of its text that targetOf gives a note for, whatever its separator or
    // case, so all the times a paragraph writes a reference open the same note.
    public static IReadOnlyList<NoteReferenceLink> LinksIn(string? text, Func<string, int?> targetOf)
    {
        ArgumentNullException.ThrowIfNull(targetOf);
        var links = new List<NoteReferenceLink>();
        foreach (var match in Find(text))
        {
            if (targetOf(match.NormalizedReference) is { } targetNoteId) links.Add(new NoteReferenceLink(match.Start, match.Length, targetNoteId));
        }
        return links;
    }
}
