using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Business.Services;

namespace WorkNotes.Business.Tests;

public sealed class NoteReferenceBackfillServiceTests
{
    private const string Owner = "user-1";
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 9, 28, 8, 0, 0, 700, TimeSpan.Zero));
    private static readonly DateTime SavedAtUtc = new(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);
    private static readonly NoteReferenceTarget Cr30080 = new(12, "CR 30080 Export facturi", NoteTypes.Article);
    private static readonly NoteReferenceTarget Test30080 = new(13, "Testare CR-30080", NoteTypes.Journal);
    private static readonly NoteReferenceTarget Release512 = new(14, "Release 512", NoteTypes.Article);

    [Fact]
    public async Task ANumberInTheTitleOfExactlyOneOtherNoteBecomesAReference()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var notes = new StubNotes([Cr30080, Release512], Document(7, "Jurnal", ("Am lucrat la 30080, apoi la 30080.", first), ("Fără numere", second)));

        var results = await RunAsync(notes, save: true);

        var saved = Assert.Single(notes.Saves);
        Assert.Equal(7, saved.NoteId);
        Assert.Equal("version-7", saved.ExpectedVersion);
        // Only the changed paragraph is saved, under its id.
        Assert.Equal([new NoteBlockInput(first, "Am lucrat la [[note:12|30080]], apoi la [[note:12|30080]].")], saved.Paragraphs);
        Assert.Equal([new NoteReferenceInput(12, "30080")], saved.References);
        Assert.Equal(SavedAtUtc, saved.SavedAtUtc);
        var result = Assert.Single(results);
        Assert.Equal((7, "Jurnal", NoteReferenceBackfillStatus.Linked), (result.NoteId, result.Title, result.Status));
        var match = Assert.Single(result.Matches);
        Assert.Equal(("30080", 2), (match.Number, match.Count));
        Assert.Equal([Cr30080], match.Targets);
    }

    [Fact]
    public async Task ANumberSeveralNotesHaveIsLeftForTheOwnerToChoose()
    {
        var notes = new StubNotes([Cr30080, Test30080], Document(7, null, ("Vezi 30080", Guid.NewGuid())));

        var result = Assert.Single(await RunAsync(notes, save: true));

        Assert.Empty(notes.Saves);
        Assert.Equal(NoteReferenceBackfillStatus.Unchanged, result.Status);
        var match = Assert.Single(result.Matches);
        Assert.Equal([Cr30080, Test30080], match.Targets);
    }

    [Fact]
    public async Task TheNoteItselfIsNeverATarget()
    {
        // Note 12 is the one being read: its own title does not make the number ambiguous, nor a target.
        var notes = new StubNotes([Cr30080, Test30080], Document(12, Cr30080.Title, ("Continuare în 30080", Guid.NewGuid())));

        var result = Assert.Single(await RunAsync(notes, save: true));

        Assert.Equal(NoteReferenceBackfillStatus.Linked, result.Status);
        Assert.Equal([new NoteReferenceInput(13, "30080")], Assert.Single(notes.Saves).References);
    }

    [Fact]
    public async Task ANoteWithoutMatchingNumbersIsNotSaved()
    {
        var notes = new StubNotes([Cr30080], Document(7, "Jurnal", ("Doar 12 și 130080, și [[note:12|30080]]", Guid.NewGuid())));

        var result = Assert.Single(await RunAsync(notes, save: true));

        Assert.Equal((7, "Jurnal", NoteReferenceBackfillStatus.Unchanged), (result.NoteId, result.Title, result.Status));
        Assert.Empty(result.Matches);
        Assert.Empty(notes.Saves);
    }

    [Fact]
    public async Task APreviewSavesNothing()
    {
        var notes = new StubNotes([Cr30080], Document(7, "Jurnal", ("Vezi 30080", Guid.NewGuid())));

        var result = Assert.Single(await RunAsync(notes, save: false));

        Assert.Equal(NoteReferenceBackfillStatus.Linked, result.Status);
        Assert.Equal("30080", Assert.Single(result.Matches).Number);
        Assert.Empty(notes.Saves);
    }

    [Fact]
    public async Task TheStoredReferencesAreThoseOfTheWholeTextThatCanBeOpened()
    {
        // The second paragraph already refers to 14, which the owner may open, and to 99, which the owner may not.
        var notes = new StubNotes([Cr30080, Release512],
            Document(7, "Jurnal", ("Vezi 30080", Guid.NewGuid()), ("Deja: [[note:14|512]] și [[note:99|700]]", Guid.NewGuid())));

        await RunAsync(notes, save: true);

        var saved = Assert.Single(notes.Saves);
        Assert.Single(saved.Paragraphs);
        Assert.Equal([new NoteReferenceInput(12, "30080"), new NoteReferenceInput(14, "512")], saved.References);
    }

    [Fact]
    public async Task ANoteChangedWhileItWasReadIsReported()
    {
        var notes = new StubNotes([Cr30080], Document(7, "Jurnal", ("Vezi 30080", Guid.NewGuid()))) { SaveSucceeds = false };

        var result = Assert.Single(await RunAsync(notes, save: true));

        Assert.Equal(NoteReferenceBackfillStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task ATextThatWouldBecomeTooLongIsNotSaved()
    {
        var content = "30080 " + new string('a', NoteRules.MaxContentLength - 6);
        var notes = new StubNotes([Cr30080], Document(7, "Jurnal", (content, Guid.NewGuid())));

        var result = Assert.Single(await RunAsync(notes, save: true));

        Assert.Equal(NoteReferenceBackfillStatus.TooLong, result.Status);
        Assert.Empty(notes.Saves);
    }

    [Fact]
    public async Task EachOwnerAndBoardIsReadWithItsOwnTargets()
    {
        // Two notes of one owner on board 5, one of another member on the same board, one on board 6.
        var notes = new StubNotes([],
            Document(7, "a", ("30080", Guid.NewGuid())), Document(8, "b", ("30080", Guid.NewGuid())),
            Document(9, "c", ("30080", Guid.NewGuid())), Document(10, "d", ("30080", Guid.NewGuid())))
        {
            Sources = [new(7, 5, Owner), new(8, 5, Owner), new(9, 5, "user-2"), new(10, 6, Owner)],
            TargetsByBoard =
            {
                [(Owner, 5)] = [Cr30080],
                // user-2 may see two notes with the number on the same board.
                [("user-2", 5)] = [Cr30080, Test30080],
                [(Owner, 6)] = []
            }
        };

        var results = await RunAsync(notes, save: true);

        Assert.Equal([(Owner, 5), ("user-2", 5), (Owner, 6)], notes.TargetsRead);
        Assert.Equal([(7, Owner), (8, Owner), (9, "user-2"), (10, Owner)], notes.DocumentsRead);
        Assert.Equal([NoteReferenceBackfillStatus.Linked, NoteReferenceBackfillStatus.Linked, NoteReferenceBackfillStatus.Unchanged,
            NoteReferenceBackfillStatus.Unchanged], results.Select(result => result.Status));
        Assert.Equal([7, 8], notes.Saves.Select(save => save.NoteId));
    }

    [Fact]
    public async Task ANoteNoLongerAvailableIsSkipped()
    {
        var notes = new StubNotes([Cr30080]) { Sources = [new(7, 5, Owner)] };

        Assert.Empty(await RunAsync(notes, save: true));
        Assert.Empty(notes.Saves);
    }

    [Fact]
    public async Task StoppingEndsTheRunBetweenNotes()
    {
        var notes = new StubNotes([Cr30080], Document(7, "a", ("30080", Guid.NewGuid())), Document(8, "b", ("30080", Guid.NewGuid())));
        using var stop = new CancellationTokenSource();
        var results = new List<NoteReferenceBackfillNote>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var result in new NoteReferenceBackfillService(notes, Time).CreateReferencesAsync(true, stop.Token))
            {
                results.Add(result);
                stop.Cancel();
            }
        });

        // The first note was saved whole; the second was not read.
        Assert.Equal([7], results.Select(result => result.NoteId));
        Assert.Equal([7], notes.Saves.Select(save => save.NoteId));
        Assert.Equal([(7, Owner)], notes.DocumentsRead);
    }

    private static async Task<List<NoteReferenceBackfillNote>> RunAsync(StubNotes notes, bool save)
    {
        var results = new List<NoteReferenceBackfillNote>();
        await foreach (var result in new NoteReferenceBackfillService(notes, Time).CreateReferencesAsync(save, CancellationToken.None))
            results.Add(result);
        return results;
    }

    private static NoteDocument Document(int id, string? title, params (string Content, Guid Id)[] blocks) =>
        new(id, 5, "TopDev", NoteTypes.Journal, title, NoteVisibilities.Private, true, SavedAtUtc, SavedAtUtc, $"version-{id}",
            blocks.Select(block => new NoteBlockDetails(block.Id, block.Content, SavedAtUtc, SavedAtUtc)).ToList());

    private sealed record Save(int NoteId, string ExpectedVersion, IReadOnlyList<NoteBlockInput> Paragraphs,
        IReadOnlyList<NoteReferenceInput> References, DateTime SavedAtUtc);

    private sealed class FixedTime(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    // Unless told otherwise, every document is a note of Owner on board 5, where Owner may see targets.
    private sealed class StubNotes(IReadOnlyList<NoteReferenceTarget> targets, params NoteDocument[] documents) : INoteReferenceBackfillRepository
    {
        public IReadOnlyList<NoteReferenceSource>? Sources { get; init; }
        public Dictionary<(string UserId, int ContextId), IReadOnlyList<NoteReferenceTarget>> TargetsByBoard { get; } = [];
        public bool SaveSucceeds { get; init; } = true;

        public List<(string UserId, int ContextId)> TargetsRead { get; } = [];
        public List<(int NoteId, string UserId)> DocumentsRead { get; } = [];
        public List<Save> Saves { get; } = [];

        public Task<IReadOnlyList<NoteReferenceSource>> GetReferenceSourcesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Sources ?? documents.Select(document => new NoteReferenceSource(document.Id, 5, Owner)).ToList());

        public Task<IReadOnlyList<NoteReferenceTarget>> GetAllReferenceTargetsAsync(string userId, int contextId, CancellationToken cancellationToken)
        {
            TargetsRead.Add((userId, contextId));
            // Like the data access: the notes the user may see include the note being read.
            var visible = TargetsByBoard.TryGetValue((userId, contextId), out var found) ? found : targets;
            return Task.FromResult<IReadOnlyList<NoteReferenceTarget>>([.. visible, .. documents
                .Where(document => visible.All(target => target.Id != document.Id))
                .Select(document => new NoteReferenceTarget(document.Id, document.Title, document.NoteType))]);
        }

        public Task<NoteDocument?> GetDocumentAsync(int noteId, string userId, CancellationToken cancellationToken)
        {
            DocumentsRead.Add((noteId, userId));
            return Task.FromResult(documents.FirstOrDefault(document => document.Id == noteId));
        }

        public Task<bool> SaveReferencesAsync(int noteId, string expectedVersion, IReadOnlyList<NoteBlockInput> paragraphs,
            IReadOnlyList<NoteReferenceInput> references, DateTime savedAtUtc, CancellationToken cancellationToken)
        {
            Saves.Add(new Save(noteId, expectedVersion, paragraphs, references, savedAtUtc));
            return Task.FromResult(SaveSucceeds);
        }
    }
}
