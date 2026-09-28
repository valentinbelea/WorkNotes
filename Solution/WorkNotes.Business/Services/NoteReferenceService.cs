using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Business.Services;

public sealed class NoteReferenceService(INoteReferenceRepository references, TimeProvider time) : INoteReferenceService
{
    // A refresh whose target disappeared meanwhile is read and resolved again, once: the deletion refreshes those
    // references itself.
    private const int RefreshAttempts = 2;

    public async Task<NoteReferenceResolution> ResolveAsync(string ownerUserId, int contextId, int noteId, string? title,
        IReadOnlyList<NoteBlockInput> paragraphs, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentNullException.ThrowIfNull(paragraphs);
        cancellationToken.ThrowIfCancellationRequested();
        var found = paragraphs
            .Select(paragraph => (paragraph.Id, Matches: NoteReferenceRules.Find(paragraph.Content)))
            .Where(paragraph => paragraph.Matches.Count > 0)
            .ToList();
        if (found.Count == 0) return new([], []);

        var numbers = found.SelectMany(paragraph => paragraph.Matches).Select(match => match.ReferenceNumber).Distinct().ToList();
        var candidates = await references.GetCandidatesAsync(ownerUserId, contextId, numbers, cancellationToken);
        // The note itself counts with the title it is saved with, not the one it had.
        var titles = NoteReferenceRules.NotesByTitleReference(candidates
            .Where(candidate => candidate.Id != noteId)
            .Select(candidate => (candidate.Id, candidate.Title))
            .Append((noteId, title)));
        var stored = found.SelectMany(paragraph => ReferencesOf(paragraph.Id, noteId, paragraph.Matches, titles)).ToList();
        var targetIds = stored.Select(reference => reference.TargetNoteId).ToHashSet();
        return new(stored, candidates.Where(candidate => targetIds.Contains(candidate.Id)).ToList());
    }

    public async Task RefreshAsync(int contextId, string? previousTitle, string? title, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var before = NoteReferenceRules.Find(previousTitle);
        var after = NoteReferenceRules.Find(title);
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
                var titles = NoteReferenceRules.NotesByTitleReference(candidates.Select(candidate => (candidate.Id, candidate.Title)));
                foreach (var paragraph in owner)
                {
                    var matches = NoteReferenceRules.Find(paragraph.Content).Where(match => keys.Contains(match.NormalizedReference));
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
        var targets = new Dictionary<(Guid, string), NoteReferenceTarget>();
        foreach (var reference in stored) targets.TryAdd((reference.NoteBlockId, reference.NormalizedReference), reference.Target);
        var blocks = document.Blocks
            .Select(block => block with
            {
                Links = NoteReferenceRules.LinksIn(block.Content,
                    normalized => targets.TryGetValue((block.Id, normalized), out var target) ? target.Id : null)
            })
            .ToList();
        var shown = blocks.SelectMany(block => block.Links!).Select(link => link.TargetNoteId).ToHashSet();
        return document with
        {
            Blocks = blocks,
            References = targets.Values.Where(target => shown.Contains(target.Id)).DistinctBy(target => target.Id).ToList()
        };
    }

    // One stored reference for each reference of a paragraph that opens a note, with the text it is first written with.
    private static IEnumerable<NoteBlockReference> ReferencesOf(Guid blockId, int noteId, IEnumerable<NoteReferenceMatch> matches,
        IReadOnlyDictionary<string, IReadOnlyList<int>> titles)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var match in matches)
        {
            if (!seen.Add(match.NormalizedReference)) continue;
            if (NoteReferenceRules.TargetOf(titles, match.NormalizedReference, noteId) is { } targetNoteId)
                yield return new NoteBlockReference(blockId, targetNoteId, match.ReferenceType, match.ReferenceNumber, match.Text);
        }
    }

    // Kept to the second, the precision of the stored column (as NoteService does for the audit times).
    private DateTime SavedAtUtc()
    {
        var now = time.GetUtcNow().UtcDateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
    }
}
