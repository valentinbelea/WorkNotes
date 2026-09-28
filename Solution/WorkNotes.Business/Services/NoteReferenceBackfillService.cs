using System.Runtime.CompilerServices;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Business.Services;

public sealed class NoteReferenceBackfillService(INoteReferenceBackfillRepository notes, TimeProvider time) : INoteReferenceBackfillService
{
    public async IAsyncEnumerable<NoteReferenceBackfillNote> CreateReferencesAsync(bool save,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sources = await notes.GetReferenceSourcesAsync(cancellationToken);
        // The notes of one owner on one board can point to the same notes: those are read once, and each number's
        // candidates are looked for once.
        foreach (var board in sources.GroupBy(source => (source.ContextId, source.OwnerUserId)))
        {
            var targets = await notes.GetAllReferenceTargetsAsync(board.Key.OwnerUserId, board.Key.ContextId, cancellationToken);
            var candidates = new Dictionary<string, IReadOnlyList<NoteReferenceTarget>>(StringComparer.Ordinal);
            foreach (var source in board)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = await notes.GetDocumentAsync(source.NoteId, source.OwnerUserId, cancellationToken);
                // Deleted, archived or no longer the owner's since the list was read.
                if (document is null || !document.IsOwner) continue;
                yield return await CreateReferencesAsync(document, targets, candidates, save, cancellationToken);
            }
        }
    }

    private async Task<NoteReferenceBackfillNote> CreateReferencesAsync(NoteDocument document, IReadOnlyList<NoteReferenceTarget> targets,
        Dictionary<string, IReadOnlyList<NoteReferenceTarget>> candidates, bool save, CancellationToken cancellationToken)
    {
        var matches = new List<NoteReferenceBackfillMatch>();
        // The other notes of the board with the number whole in their title, as the editor would offer them. Only a
        // single one is certain; between several, the choice is the owner's, in the editor.
        int? TargetOf(string number)
        {
            if (!candidates.TryGetValue(number, out var found))
                candidates[number] = found = targets.Where(target => NoteReferenceRules.TitleContainsNumber(target.Title, number)).ToList();
            var others = found.Where(target => target.Id != document.Id).ToList();
            if (others.Count == 0) return null;
            var index = matches.FindIndex(match => match.Number == number);
            if (index < 0) matches.Add(new NoteReferenceBackfillMatch(number, others, 1));
            else matches[index] = matches[index] with { Count = matches[index].Count + 1 };
            return others.Count == 1 ? others[0].Id : null;
        }

        var paragraphs = document.Blocks
            .Select(block => new NoteBlockInput(block.Id, NoteReferenceRules.LinkNumbers(block.Content, TargetOf)))
            .ToList();
        var changed = paragraphs.Where((paragraph, index) => paragraph.Content != document.Blocks[index].Content).ToList();
        var status = changed.Count == 0 ? NoteReferenceBackfillStatus.Unchanged
            // The editor could no longer save a longer note.
            : paragraphs.Sum(paragraph => paragraph.Content.Length) > NoteRules.MaxContentLength ? NoteReferenceBackfillStatus.TooLong
            : NoteReferenceBackfillStatus.Linked;
        if (status == NoteReferenceBackfillStatus.Linked && save)
        {
            // As at a save: the note's stored references are those of its whole text whose target the owner may open.
            var valid = targets.Where(target => target.Id != document.Id).Select(target => target.Id).ToHashSet();
            var references = NoteReferenceRules.Find(paragraphs.Select(paragraph => paragraph.Content))
                .Where(reference => valid.Contains(reference.TargetNoteId))
                .ToList();
            if (!await notes.SaveReferencesAsync(document.Id, document.Version, changed, references, SavedAtUtc(), cancellationToken))
                status = NoteReferenceBackfillStatus.Conflict;
        }
        return new NoteReferenceBackfillNote(document.Id, document.Title, status, matches);
    }

    // Kept to the second, the precision of the stored column (as NoteService does for the audit times).
    private DateTime SavedAtUtc()
    {
        var now = time.GetUtcNow().UtcDateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
    }
}
