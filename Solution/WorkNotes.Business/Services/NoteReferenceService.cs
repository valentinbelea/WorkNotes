using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Business.Services;

public sealed class NoteReferenceService(INoteReferenceRepository references, IReferenceTypeService types, TimeProvider time)
    : INoteReferenceService
{
    // A refresh one of whose notes was deleted meanwhile is read and resolved again, once: the deletion itself takes away
    // the rows that open the deleted note.
    private const int RefreshAttempts = 2;

    public async Task<NoteReferenceResolution> ResolveAsync(string ownerUserId, int contextId, int noteId,
        IReadOnlyList<NoteBlockInput> paragraphs, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentNullException.ThrowIfNull(paragraphs);
        cancellationToken.ThrowIfCancellationRequested();
        var parser = await types.GetParserAsync(cancellationToken);
        var found = paragraphs
            .Select(paragraph => (paragraph.Id, Matches: parser.Find(paragraph.Content)))
            .Where(paragraph => paragraph.Matches.Count > 0)
            .ToList();
        if (found.Count == 0) return new([], [], new Dictionary<Guid, IReadOnlyList<NoteReferenceLink>>());

        var numbers = found.SelectMany(paragraph => paragraph.Matches).Select(match => match.ReferenceNumber).Distinct().ToList();
        var candidates = await references.GetCandidatesAsync(ownerUserId, contextId, numbers, cancellationToken);
        // The note itself is never a target, whatever its title.
        var titles = NoteReferenceRules.NotesByTitleReference(parser, candidates
            .Where(candidate => candidate.Id != noteId)
            .Select(candidate => (candidate.Id, candidate.Title)));
        var stored = found.SelectMany(paragraph => ReferencesOf(paragraph.Id, noteId, paragraph.Matches, titles)).ToList();
        var targetIds = stored.SelectMany(reference => reference.TargetNoteIds).ToHashSet();
        // Where the saved text shows them: every place a paragraph writes one of its stored references.
        var targets = stored.ToDictionary(reference => (reference.NoteBlockId, reference.NormalizedReference), reference => reference.TargetNoteIds);
        var links = found
            .Select(paragraph => (paragraph.Id, Links: NoteReferenceRules.LinksIn(paragraph.Matches,
                normalized => targets.TryGetValue((paragraph.Id, normalized), out var targetNoteIds) ? targetNoteIds : [])))
            .Where(paragraph => paragraph.Links.Count > 0)
            .ToDictionary(paragraph => paragraph.Id, paragraph => paragraph.Links);
        return new(stored, candidates.Where(candidate => targetIds.Contains(candidate.Id)).ToList(), links);
    }

    public async Task RefreshAsync(int contextId, string? previousTitle, string? title, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var parser = await types.GetParserAsync(cancellationToken);
        var before = parser.Find(previousTitle);
        var after = parser.Find(title);
        // A reference both titles have keeps its notes: only the others can change where they lead.
        var changed = before.ExceptBy(after.Select(match => match.NormalizedReference), match => match.NormalizedReference)
            .Concat(after.ExceptBy(before.Select(match => match.NormalizedReference), match => match.NormalizedReference))
            .DistinctBy(match => match.NormalizedReference)
            .ToList();
        if (changed.Count == 0) return;
        var keys = changed.Select(match => match.NormalizedReference).ToHashSet(StringComparer.Ordinal);
        var numbers = changed.Select(match => match.ReferenceNumber).Distinct().ToList();

        for (var attempt = 1; attempt <= RefreshAttempts; attempt++)
        {
            var paragraphs = await references.GetSourcesAsync(contextId, numbers, cancellationToken);
            if (paragraphs.Count == 0) return;
            var stored = new List<NoteBlockReference>();
            // A paragraph's references open only notes its owner may see.
            foreach (var owner in paragraphs.GroupBy(paragraph => paragraph.OwnerUserId, StringComparer.Ordinal))
            {
                var candidates = await references.GetCandidatesAsync(owner.Key, contextId, numbers, cancellationToken);
                var titles = NoteReferenceRules.NotesByTitleReference(parser, candidates.Select(candidate => (candidate.Id, candidate.Title)));
                foreach (var paragraph in owner)
                {
                    var matches = parser.Find(paragraph.Content).Where(match => keys.Contains(match.NormalizedReference));
                    stored.AddRange(ReferencesOf(paragraph.NoteBlockId, paragraph.NoteId, matches, titles));
                }
            }
            if (await references.ReplaceReferencesAsync(keys, paragraphs, stored, SavedAtUtc(), cancellationToken)) return;
        }
    }

    public async Task<NoteDocument> WithLinksAsync(NoteDocument document, string userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        var stored = await references.GetTargetsAsync(document.Id, userId, cancellationToken);
        if (stored.Count == 0) return document;
        var parser = await types.GetParserAsync(cancellationToken);
        // Each stored reference of a paragraph with the notes it opens for the user, in the order of their ids.
        var targets = stored
            .GroupBy(reference => (reference.NoteBlockId, reference.NormalizedReference))
            .ToDictionary(group => group.Key, group => (IReadOnlyList<int>)group.Select(reference => reference.Target.Id).Distinct().Order().ToList());
        var blocks = document.Blocks
            .Select(block => block with
            {
                Links = NoteReferenceRules.LinksIn(parser.Find(block.Content),
                    normalized => targets.TryGetValue((block.Id, normalized), out var targetNoteIds) ? targetNoteIds : [])
            })
            .ToList();
        var shown = blocks.SelectMany(block => block.Links!).SelectMany(link => link.TargetNoteIds).ToHashSet();
        return document with
        {
            Blocks = blocks,
            References = stored.Select(reference => reference.Target).DistinctBy(target => target.Id)
                .Where(target => shown.Contains(target.Id)).OrderBy(target => target.Id).ToList()
        };
    }

    // One stored reference for each reference of a paragraph that opens at least one note, with the text it is first
    // written with and all the notes it opens.
    private static IEnumerable<NoteBlockReference> ReferencesOf(Guid blockId, int noteId, IEnumerable<NoteReferenceMatch> matches,
        IReadOnlyDictionary<string, IReadOnlyList<int>> titles)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var match in matches)
        {
            if (!seen.Add(match.NormalizedReference)) continue;
            var targetNoteIds = NoteReferenceRules.TargetsOf(titles, match.NormalizedReference, noteId);
            if (targetNoteIds.Count > 0)
                yield return new NoteBlockReference(blockId, match.ReferenceType, match.ReferenceNumber, match.Text, targetNoteIds);
        }
    }

    // Kept to the second, the precision of the stored column (as NoteService does for the audit times).
    private DateTime SavedAtUtc()
    {
        var now = time.GetUtcNow().UtcDateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
    }
}
