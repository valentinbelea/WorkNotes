using System.Globalization;

namespace WorkNotes.Business.Models;

// Internal references between notes: a reference type configured in dbo.ReferenceTypes (CR, BUG...) written with its
// number, as CR 30080, CR-30080, CR_30080, CR30080, bug 1234, bug-1234, bug_1234 or bug1234. The type is read in any
// case; between the type and the number there are spaces, one - or _, or nothing. The type and the number are whole
// words: a letter, digit or combining mark right before the type or right after the number makes them part of a longer
// word (XCR30080A is no reference). A reference is compared by its type and number only (CR:30080), so CR-30080 and
// cr 30080 name the same CR, and CR 1234 is not bug 1234. A note's title is read with the same rules to know which
// references name it. NoteReferenceParser reads the texts with the types configured.
// The text itself never changes: the links are stored apart (NoteReferences) and drawn over the text.
// Scripts/version_0.02/012_RefreshNoteReferences.sql reads the stored text with the same rules.
public static class NoteReferenceRules
{
    // A type has at most this many letters (CK_ReferenceTypes_Code): with the number it fits NormalizedReference.
    public const int MaxTypeLength = 10;
    // At most this many spaces between the type and the number, and this many digits (the number is a bigint).
    public const int MaxSeparatorLength = 50;
    public const int MaxNumberLength = 18;
    // The longest text read as one reference: the longest type, the spaces and the digits (NoteReferences.ReferenceText).
    public const int MaxTextLength = MaxTypeLength + MaxSeparatorLength + MaxNumberLength;
    // The lookup of a reference just typed reads only the end of the text: the longest reference and the character before
    // it, which says whether the reference starts a word.
    public const int LookupLength = MaxTextLength + 1;

    // A type as dbo.ReferenceTypes stores it: 1 to 10 capital ASCII letters (CR, BUG).
    public static bool ValidType(string? type) =>
        type is { Length: > 0 and <= MaxTypeLength } && type.All(letter => letter is >= 'A' and <= 'Z');

    // The form references are compared and stored by: CR:30080, BUG:1234 (the number without leading zeros).
    public static string Normalize(string referenceType, long referenceNumber) =>
        $"{referenceType}:{referenceNumber.ToString(CultureInfo.InvariantCulture)}";

    // The reference a text ends with (the one just typed, before the separator that ends it), with its start in the text
    // given; null when no reference ends the text. Only the last LookupLength characters are read.
    public static NoteReferenceMatch? EndingReference(NoteReferenceParser parser, string? text)
    {
        ArgumentNullException.ThrowIfNull(parser);
        if (string.IsNullOrEmpty(text)) return null;
        var from = Math.Max(0, text.Length - LookupLength);
        var end = parser.Find(text[from..]).LastOrDefault(match => from + match.Start + match.Length == text.Length);
        return end is null ? null : end with { Start = from + end.Start };
    }

    // For each reference, the notes whose title has it, each note once.
    public static IReadOnlyDictionary<string, IReadOnlyList<int>> NotesByTitleReference(NoteReferenceParser parser,
        IEnumerable<(int NoteId, string? Title)> notes)
    {
        ArgumentNullException.ThrowIfNull(parser);
        return notes
            .SelectMany(note => parser.Find(note.Title).Select(match => (match.NormalizedReference, note.NoteId)))
            .Distinct()
            .GroupBy(item => item.NormalizedReference, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<int>)group.Select(item => item.NoteId).ToList(), StringComparer.Ordinal);
    }

    // The notes a reference written in noteId opens: every note whose title has the reference, except noteId itself, in
    // the order of their ids. None when no other note has it.
    public static IReadOnlyList<int> TargetsOf(IReadOnlyDictionary<string, IReadOnlyList<int>> notesByTitleReference, string normalizedReference, int noteId) =>
        notesByTitleReference.TryGetValue(normalizedReference, out var notes) ? notes.Where(id => id != noteId).Order().ToList() : [];

    // Where a paragraph shows links: every reference found in its text (matches) that targetsOf gives notes for, whatever
    // its separator or case, so all the times a paragraph writes a reference open the same notes.
    public static IReadOnlyList<NoteReferenceLink> LinksIn(IEnumerable<NoteReferenceMatch> matches, Func<string, IReadOnlyList<int>> targetsOf)
    {
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(targetsOf);
        var links = new List<NoteReferenceLink>();
        foreach (var match in matches)
        {
            var targetNoteIds = targetsOf(match.NormalizedReference);
            if (targetNoteIds.Count > 0)
                links.Add(new NoteReferenceLink(match.Start, match.Text, match.ReferenceType, match.ReferenceNumber, targetNoteIds));
        }
        return links;
    }
}
