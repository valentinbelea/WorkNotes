using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Business.Services;

namespace WorkNotes.Business.Tests;

public sealed class NoteReferenceServiceTests
{
    private const string Owner = "user-1";
    private const string Colleague = "user-2";
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 9, 28, 8, 0, 0, 700, TimeSpan.Zero));
    private static readonly DateTime SavedAtUtc = new(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);
    private static readonly NoteReferenceTarget Cr30080 = new(12, "CR 30080 Export facturi", NoteTypes.Article);
    private static readonly NoteReferenceTarget Bug1234 = new(13, "Rezolvare Bug-1234", NoteTypes.Article);
    private static readonly NoteReferenceTarget Cr1234 = new(14, "CR_1234 raport", NoteTypes.Journal);
    private static readonly NoteReferenceTarget Test30080 = new(15, "Testare cr-30080", NoteTypes.Journal);

    [Fact]
    public async Task AReferenceWithASingleTargetIsStoredOnceWithTheTextItIsFirstWrittenWith()
    {
        var paragraph = Guid.NewGuid();
        var repository = new StubRepository { Candidates = { [Owner] = [Cr30080] } };

        var resolution = await Resolve(repository, (paragraph, "CR_30080 întâi, apoi CR 30080 și cr-30080."));

        Assert.Equal([new NoteBlockReference(paragraph, 12, NoteReferenceTypes.Cr, 30080, "CR_30080")], resolution.References);
        Assert.Equal([Cr30080], resolution.Targets);
        var read = Assert.Single(repository.CandidatesRead);
        Assert.Equal((Owner, 5), (read.UserId, read.ContextId));
        Assert.Equal([30080L], read.Numbers);
    }

    [Fact]
    public async Task EachReferenceOfAParagraphIsStored()
    {
        var paragraph = Guid.NewGuid();
        var repository = new StubRepository { Candidates = { [Owner] = [Cr30080, Bug1234, Cr1234] } };

        var resolution = await Resolve(repository, (paragraph, "CR 30080, bug1234 și cr-1234"));

        Assert.Equal([("CR:30080", 12, "CR 30080"), ("BUG:1234", 13, "bug1234"), ("CR:1234", 14, "cr-1234")],
            resolution.References.Select(reference => (reference.NormalizedReference, reference.TargetNoteId, reference.ReferenceText)));
    }

    [Fact]
    public async Task ACrAndABugWithTheSameNumberOpenTheirOwnNotes()
    {
        var paragraph = Guid.NewGuid();
        var repository = new StubRepository { Candidates = { [Owner] = [Bug1234, Cr1234] } };

        var resolution = await Resolve(repository, (paragraph, "Bug 1234 vine din CR 1234"));

        Assert.Equal([(NoteReferenceTypes.Bug, 13), (NoteReferenceTypes.Cr, 14)],
            resolution.References.Select(reference => (reference.ReferenceType, reference.TargetNoteId)));
    }

    [Fact]
    public async Task AReferenceNoNoteHasInItsTitleIsNotStored()
    {
        // "Release 30080" and "CR 130080" have the digits, not the reference.
        var repository = new StubRepository { Candidates = { [Owner] = [new(20, "Release 30080", NoteTypes.Article), new(21, "CR 130080", NoteTypes.Article)] } };

        var resolution = await Resolve(repository, (Guid.NewGuid(), "CR 30080"));

        Assert.Empty(resolution.References);
        Assert.Empty(resolution.Targets);
    }

    [Fact]
    public async Task AReferenceSeveralNotesHaveInTheirTitleIsNotStored()
    {
        var repository = new StubRepository { Candidates = { [Owner] = [Cr30080, Test30080] } };

        Assert.Empty((await Resolve(repository, (Guid.NewGuid(), "CR 30080"))).References);
    }

    [Fact]
    public async Task TheNoteItselfIsNeverATargetButCountsAmongTheNotesWithTheReference()
    {
        var paragraph = Guid.NewGuid();
        var repository = new StubRepository { Candidates = { [Owner] = [Cr30080] } };

        // Note 12 writes its own CR: nothing to open.
        Assert.Empty((await Resolve(repository, (paragraph, "CR 30080"), noteId: 12, title: Cr30080.Title)).References);
        // Note 7 is being saved with the reference in its title too: two notes have it.
        Assert.Empty((await Resolve(repository, (paragraph, "CR 30080"), noteId: 7, title: "Analiză CR 30080")).References);
    }

    [Fact]
    public async Task TheNoteIsReadWithTheTitleItIsSavedWith()
    {
        var paragraph = Guid.NewGuid();
        // As stored, note 7 still has the reference in its title; it is being saved without it.
        var repository = new StubRepository { Candidates = { [Owner] = [Cr30080, new(7, "CR 30080 vechi", NoteTypes.Journal)] } };

        var resolution = await Resolve(repository, (paragraph, "CR 30080"), noteId: 7, title: "Jurnal");

        Assert.Equal(12, Assert.Single(resolution.References).TargetNoteId);
    }

    [Fact]
    public async Task TextWithoutReferencesReadsNoCandidates()
    {
        var repository = new StubRepository { Candidates = { [Owner] = [Cr30080] } };

        var resolution = await Resolve(repository, (Guid.NewGuid(), "Doar 30080, XCR30080A și bug-uri"));

        Assert.Empty(resolution.References);
        Assert.Empty(repository.CandidatesRead);
    }

    [Fact]
    public async Task TheCrsJournalLinksEachCrOrBugWithASingleNote()
    {
        // The CRs journal (note 99) lists the work of the board; each CR or bug has its note, except CR 30082 (two notes)
        // and CR 30083 (none yet).
        Guid first = Guid.NewGuid(), second = Guid.NewGuid(), third = Guid.NewGuid();
        var repository = new StubRepository
        {
            Candidates =
            {
                [Owner] =
                [
                    Cr30080, new(16, "CR_30081", NoteTypes.Article), new(17, "Rezolvare Bug-512", NoteTypes.Article),
                    new(18, "CR 30082 analiză", NoteTypes.Article), new(19, "CR-30082 testare", NoteTypes.Journal)
                ]
            }
        };

        var resolution = await Resolve(repository,
        [
            (first, "CR 30080 - export facturi (în lucru)\nCR-30081 raport lunar"),
            (second, "bug_512 rezolvat; cr30082 amânat; CR 30083 nou"),
            (third, "Fără CR-uri aici.")
        ], noteId: 99, title: "CRs");

        Assert.Equal([(first, "CR:30080", 12), (first, "CR:30081", 16), (second, "BUG:512", 17)],
            resolution.References.Select(reference => (reference.NoteBlockId, reference.NormalizedReference, reference.TargetNoteId)));
        Assert.Equal([30080L, 30081, 512, 30082, 30083], repository.CandidatesRead.Single().Numbers);
    }

    [Fact]
    public async Task ResolvingStopsBeforeTheDataAccessWhenCancelled()
    {
        var repository = new StubRepository();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new NoteReferenceService(repository, Time)
            .ResolveAsync(Owner, 5, 7, null, [new(Guid.NewGuid(), "CR 30080")], cancellation.Token));
        Assert.Empty(repository.CandidatesRead);
    }

    [Fact]
    public async Task ARefreshResolvesOnlyTheReferencesOneTitleHasAndTheOtherHasNot()
    {
        var paragraph = Guid.NewGuid();
        var repository = new StubRepository
        {
            Sources = [Source(paragraph, 7, "CR 30080, bug 1 și CR 30081")],
            Candidates = { [Owner] = [new(12, "CR 30080 și CR 30081", NoteTypes.Article)] }
        };

        await Refresh(repository, "CR 30080 și bug 1", "CR 30080 și CR 30081");

        // CR 30080 is in both titles and stays as it is; bug 1 went away, CR 30081 came.
        var replaced = Assert.Single(repository.Replaced);
        Assert.Equal(["BUG:1", "CR:30081"], replaced.NormalizedReferences.Order());
        Assert.Equal([new NoteBlockReference(paragraph, 12, NoteReferenceTypes.Cr, 30081, "CR 30081")], replaced.References);
        Assert.Equal(SavedAtUtc, replaced.SavedAtUtc);
        Assert.Equal([1L, 30081], repository.SourcesRead.Single().Numbers.Order());
    }

    [Theory]
    [InlineData("CR 30080", "cr-30080 export")]
    [InlineData("Jurnal", "Jurnal de lucru")]
    [InlineData(null, "Fără referințe")]
    [InlineData("CRs", null)]
    public async Task TitlesNamingTheSameReferencesRefreshNothing(string? previousTitle, string? title)
    {
        var repository = new StubRepository { Sources = [Source(Guid.NewGuid(), 7, "CR 30080")] };

        await Refresh(repository, previousTitle, title);

        Assert.Empty(repository.SourcesRead);
        Assert.Empty(repository.Replaced);
    }

    [Fact]
    public async Task ACreatedNoteWithTheReferenceMakesItAmbiguous()
    {
        var paragraph = Guid.NewGuid();
        // Note 15 was just created: now two notes have CR 30080 in their title.
        var repository = new StubRepository { Sources = [Source(paragraph, 7, "Vezi CR 30080")], Candidates = { [Owner] = [Cr30080, Test30080] } };

        await Refresh(repository, null, Test30080.Title);

        var replaced = Assert.Single(repository.Replaced);
        Assert.Equal(["CR:30080"], replaced.NormalizedReferences);
        Assert.Equal([paragraph], replaced.Paragraphs.Select(source => source.NoteBlockId));
        Assert.Empty(replaced.References);
    }

    [Fact]
    public async Task ADeletedNoteLeavesTheOtherNoteWithTheReferenceAsItsTarget()
    {
        var paragraph = Guid.NewGuid();
        // Note 15 was just deleted: only note 12 has CR 30080 in its title now.
        var repository = new StubRepository { Sources = [Source(paragraph, 7, "Vezi cr_30080")], Candidates = { [Owner] = [Cr30080] } };

        await Refresh(repository, Test30080.Title, null);

        Assert.Equal([new NoteBlockReference(paragraph, 12, NoteReferenceTypes.Cr, 30080, "cr_30080")], Assert.Single(repository.Replaced).References);
    }

    [Fact]
    public async Task ARenamedTargetNoLongerOpensTheReferenceItsTitleLost()
    {
        var paragraph = Guid.NewGuid();
        // Note 12 was "CR 30080" and is now "CR 30081": no note has CR 30080 any more.
        var repository = new StubRepository { Sources = [Source(paragraph, 7, "CR 30080")], Candidates = { [Owner] = [new(12, "CR 30081", NoteTypes.Article)] } };

        await Refresh(repository, "CR 30080", "CR 30081");

        var replaced = Assert.Single(repository.Replaced);
        Assert.Equal(["CR:30080", "CR:30081"], replaced.NormalizedReferences.Order());
        Assert.Empty(replaced.References);
    }

    [Fact]
    public async Task EachParagraphFollowsWhatItsOwnerMaySee()
    {
        Guid own = Guid.NewGuid(), colleagues = Guid.NewGuid();
        var repository = new StubRepository
        {
            Sources = [Source(own, 7, "CR 30080"), Source(colleagues, 8, "CR 30080", Colleague)],
            Candidates =
            {
                // The owner sees note 12 and their private note 15; the colleague sees only note 12.
                [Owner] = [Cr30080, Test30080],
                [Colleague] = [Cr30080]
            }
        };

        await Refresh(repository, null, Cr30080.Title);

        Assert.Equal([Owner, Colleague], repository.CandidatesRead.Select(read => read.UserId));
        Assert.Equal([new NoteBlockReference(colleagues, 12, NoteReferenceTypes.Cr, 30080, "CR 30080")], Assert.Single(repository.Replaced).References);
    }

    [Fact]
    public async Task ARefreshWritesOnlyTheRefreshedReferencesOfAParagraph()
    {
        var paragraph = Guid.NewGuid();
        var repository = new StubRepository { Sources = [Source(paragraph, 7, "CR 30080 și bug 1234")], Candidates = { [Owner] = [Cr30080, Bug1234] } };

        await Refresh(repository, null, Bug1234.Title);

        var replaced = Assert.Single(repository.Replaced);
        Assert.Equal(["BUG:1234"], replaced.NormalizedReferences);
        Assert.Equal([13], replaced.References.Select(reference => reference.TargetNoteId));
    }

    [Fact]
    public async Task NoParagraphWritingTheNumberMeansNothingToWrite()
    {
        var repository = new StubRepository { Candidates = { [Owner] = [Cr30080] } };

        await Refresh(repository, null, Cr30080.Title);

        Assert.Single(repository.SourcesRead);
        Assert.Empty(repository.CandidatesRead);
        Assert.Empty(repository.Replaced);
    }

    [Fact]
    public async Task ARefreshWhoseTargetDisappearedIsReadAgainOnce()
    {
        var repository = new StubRepository
        {
            Sources = [Source(Guid.NewGuid(), 7, "CR 30080")],
            Candidates = { [Owner] = [Cr30080] },
            ReplaceResults = new Queue<bool>([false, false, true])
        };

        await Refresh(repository, null, Cr30080.Title);

        // The second attempt fails as well: the deletion that caused it refreshes the same references.
        Assert.Equal(2, repository.SourcesRead.Count);
        Assert.Equal(2, repository.Replaced.Count);
    }

    [Fact]
    public async Task ARefreshStopsBeforeTheDataAccessWhenCancelled()
    {
        var repository = new StubRepository { Sources = [Source(Guid.NewGuid(), 7, "CR 30080")] };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new NoteReferenceService(repository, Time).RefreshAsync(5, null, "CR 30080", cancellation.Token));
        Assert.Empty(repository.SourcesRead);
    }

    [Fact]
    public async Task EveryPlaceAParagraphWritesAStoredReferenceIsALink()
    {
        Guid first = Guid.NewGuid(), second = Guid.NewGuid();
        var repository = new StubRepository { Targets = [new(first, "CR:30080", Cr30080), new(second, "BUG:1234", Bug1234)] };
        var document = Document(("CR 30080; cr-30080 și CR_30080, dar nu bug 30080", first), ("bug1234 și CR 30080", second), ("Fără", Guid.NewGuid()));

        var opened = await new NoteReferenceService(repository, Time).WithLinksAsync(document, Colleague, CancellationToken.None);

        Assert.Equal([new NoteReferenceLink(0, 8, 12), new NoteReferenceLink(10, 8, 12), new NoteReferenceLink(22, 8, 12)], opened.Blocks[0].Links);
        // In the second paragraph only bug 1234 is stored; its CR 30080 has no link there.
        Assert.Equal([new NoteReferenceLink(0, 7, 13)], opened.Blocks[1].Links);
        Assert.Empty(opened.Blocks[2].Links!);
        Assert.Equal([Cr30080, Bug1234], opened.References);
        Assert.Equal((7, Colleague), repository.TargetsRead);
    }

    [Fact]
    public async Task AStoredReferenceTheTextNoLongerWritesShowsNothing()
    {
        var paragraph = Guid.NewGuid();
        var repository = new StubRepository { Targets = [new(paragraph, "CR:30080", Cr30080)] };

        var opened = await new NoteReferenceService(repository, Time).WithLinksAsync(Document(("CR 30081", paragraph)), Owner, CancellationToken.None);

        Assert.Empty(opened.Blocks[0].Links!);
        Assert.Empty(opened.References!);
    }

    [Fact]
    public async Task ADocumentWithoutStoredReferencesIsReturnedAsItIs()
    {
        var document = Document(("CR 30080", Guid.NewGuid()));

        Assert.Same(document, await new NoteReferenceService(new StubRepository(), Time).WithLinksAsync(document, Owner, CancellationToken.None));
    }

    private static Task<NoteReferenceResolution> Resolve(StubRepository repository, (Guid Id, string Content) paragraph,
        int noteId = 7, string? title = "Jurnal") =>
        Resolve(repository, [paragraph], noteId, title);

    private static Task<NoteReferenceResolution> Resolve(StubRepository repository, (Guid Id, string Content)[] paragraphs,
        int noteId = 7, string? title = "Jurnal") =>
        new NoteReferenceService(repository, Time).ResolveAsync(Owner, 5, noteId, title,
            paragraphs.Select(paragraph => new NoteBlockInput(paragraph.Id, paragraph.Content)).ToList(), CancellationToken.None);

    private static Task Refresh(StubRepository repository, string? previousTitle, string? title) =>
        new NoteReferenceService(repository, Time).RefreshAsync(5, previousTitle, title, CancellationToken.None);

    private static NoteReferenceSource Source(Guid id, int noteId, string content, string owner = Owner) =>
        new(id, noteId, owner, content, $"version-{id}");

    private static NoteDocument Document(params (string Content, Guid Id)[] blocks) =>
        new(7, 5, "TopDev", NoteTypes.Journal, "CRs", NoteVisibilities.Context, false, SavedAtUtc, SavedAtUtc, "v1",
            blocks.Select(block => new NoteBlockDetails(block.Id, block.Content, SavedAtUtc, SavedAtUtc)).ToList());

    private sealed record Replacement(IReadOnlyCollection<string> NormalizedReferences, IReadOnlyList<NoteReferenceSource> Paragraphs,
        IReadOnlyList<NoteBlockReference> References, DateTime SavedAtUtc);

    private sealed class FixedTime(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    // Board 5. Like the data access, candidates and paragraphs are those whose text has one of the numbers' digits.
    private sealed class StubRepository : INoteReferenceRepository
    {
        public Dictionary<string, IReadOnlyList<NoteReferenceTarget>> Candidates { get; } = [];
        public IReadOnlyList<NoteReferenceSource> Sources { get; init; } = [];
        public IReadOnlyList<NoteBlockTarget> Targets { get; init; } = [];
        public Queue<bool> ReplaceResults { get; init; } = new();

        public List<(string UserId, int ContextId, long[] Numbers)> CandidatesRead { get; } = [];
        public List<(int ContextId, long[] Numbers)> SourcesRead { get; } = [];
        public List<Replacement> Replaced { get; } = [];
        public (int NoteId, string UserId)? TargetsRead { get; private set; }

        private static bool HasDigits(string? text, IReadOnlyCollection<long> numbers) =>
            text is not null && numbers.Any(number => text.Contains(number.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        public Task<IReadOnlyList<NoteReferenceTarget>> GetCandidatesAsync(string userId, int contextId, IReadOnlyCollection<long> numbers,
            CancellationToken cancellationToken)
        {
            CandidatesRead.Add((userId, contextId, [.. numbers]));
            return Task.FromResult<IReadOnlyList<NoteReferenceTarget>>(
                (Candidates.TryGetValue(userId, out var visible) ? visible : []).Where(note => HasDigits(note.Title, numbers)).ToList());
        }

        public Task<IReadOnlyList<NoteReferenceSource>> GetSourcesAsync(int contextId, IReadOnlyCollection<long> numbers, CancellationToken cancellationToken)
        {
            SourcesRead.Add((contextId, [.. numbers]));
            return Task.FromResult<IReadOnlyList<NoteReferenceSource>>(Sources.Where(source => HasDigits(source.Content, numbers)).ToList());
        }

        public Task<bool> ReplaceReferencesAsync(IReadOnlyCollection<string> normalizedReferences, IReadOnlyList<NoteReferenceSource> paragraphs,
            IReadOnlyList<NoteBlockReference> references, DateTime savedAtUtc, CancellationToken cancellationToken)
        {
            Replaced.Add(new Replacement(normalizedReferences, paragraphs, references, savedAtUtc));
            return Task.FromResult(ReplaceResults.Count == 0 || ReplaceResults.Dequeue());
        }

        public Task<IReadOnlyList<NoteBlockTarget>> GetTargetsAsync(int noteId, string userId, CancellationToken cancellationToken)
        {
            TargetsRead = (noteId, userId);
            return Task.FromResult(Targets);
        }
    }
}
