using System.Globalization;
using System.Text.RegularExpressions;

namespace WorkNotes.Business.Models;

// Reads the internal references of texts with a set of reference types, the prefixes configured in dbo.ReferenceTypes
// (CR, BUG...), by the rules of NoteReferenceRules. It does not change once built, and finding is safe from several
// threads, so one parser serves every request (ReferenceTypeCache). A type that is not 1 to 10 capital ASCII letters is
// left out; with no type, a text has no reference.
public sealed class NoteReferenceParser
{
    private readonly Regex? reference;

    public NoteReferenceParser(IEnumerable<string?> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        Types = types.Where(NoteReferenceRules.ValidType).Select(type => type!).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToList();
        if (Types.Count > 0) reference = new Regex(Pattern(Types), RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    // The types read, each once, in ordinal order.
    public IReadOnlyList<string> Types { get; }

    // The references in a text, in text order, each as it is written there; the type in capitals, as configured.
    public IReadOnlyList<NoteReferenceMatch> Find(string? text)
    {
        if (reference is null || string.IsNullOrEmpty(text)) return [];
        var found = new List<NoteReferenceMatch>();
        foreach (Match match in reference.Matches(text))
        {
            var type = match.Groups["type"].Value.ToUpperInvariant();
            var number = long.Parse(match.Groups["number"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture);
            found.Add(new NoteReferenceMatch(match.Index, match.Value, type, number));
        }
        return found;
    }

    // Each type letter by letter, in its two ASCII forms ([Cc][Rr]: IgnoreCase would also take the Kelvin sign for K),
    // the longest first; then 1 to 50 spaces, one - or _, or nothing, and 1 to 18 ASCII digits. The spaces are the usual
    // one and the no-break space, which text copied from documents and pages often has.
    private static string Pattern(IReadOnlyList<string> types)
    {
        var alternatives = types
            .OrderByDescending(type => type.Length).ThenBy(type => type, StringComparer.Ordinal)
            .Select(type => string.Concat(type.Select(letter => $"[{letter}{char.ToLowerInvariant(letter)}]")));
        return @"(?<![\p{L}\p{N}\p{M}])(?<type>" + string.Join("|", alternatives) + ")"
            + @"(?:[  ]{1," + NoteReferenceRules.MaxSeparatorLength.ToString(CultureInfo.InvariantCulture) + "}|[-_])?"
            + @"(?<number>[0-9]{1," + NoteReferenceRules.MaxNumberLength.ToString(CultureInfo.InvariantCulture) + "})"
            + @"(?![\p{L}\p{N}\p{M}])";
    }
}
