using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Business.Services;

namespace WorkNotes.Business.Tests;

public sealed class NoteServiceTests
{
    private const string User = "user-1";
    // Months are read in the application's local time; these tests use UTC for it.
    private static readonly TimeProvider UtcTime = new FixedTime(new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero), TimeSpan.Zero);

    [Fact]
    public async Task JournalIsPrivateAndDatedWithTheLocalDay()
    {
        var notes = new StubNotes();
        // 22:30 UTC is already the next day at UTC+3.
        var time = new FixedTime(new DateTimeOffset(2026, 9, 23, 22, 30, 0, TimeSpan.Zero), TimeSpan.FromHours(3));
        var service = new NoteService(notes, new StubContexts(), time);

        var status = await service.CreateAsync(User, 5, NoteTypes.Journal, "  Analiză  ", CancellationToken.None);

        Assert.Equal(NoteCreateStatus.Created, status);
        Assert.Equal(new NewNote(5, User, NoteTypes.Journal, "Analiză", new DateOnly(2026, 9, 24), NoteVisibilities.Private), notes.Added);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task ArticleHasNoJournalDateAndTitleIsOptional(string? title)
    {
        var notes = new StubNotes();
        var service = new NoteService(notes, new StubContexts(), TimeProvider.System);

        await service.CreateAsync(User, 5, NoteTypes.Article, title, CancellationToken.None);

        Assert.Null(notes.Added!.JournalDate);
        Assert.Null(notes.Added.Title);
        Assert.Equal(NoteVisibilities.Private, notes.Added.Visibility);
    }

    [Theory]
    [InlineData("Document")]
    [InlineData("")]
    public async Task UnknownTypeIsRejectedBeforeDataAccess(string noteType)
    {
        var notes = new StubNotes();
        var contexts = new StubContexts();
        var service = new NoteService(notes, contexts, TimeProvider.System);

        Assert.Equal(NoteCreateStatus.InvalidType, await service.CreateAsync(User, 5, noteType, null, CancellationToken.None));
        Assert.False(contexts.WasCalled);
        Assert.Null(notes.Added);
    }

    [Fact]
    public async Task TitleMustFitTheColumnAndHaveNoControlCharacters()
    {
        var notes = new StubNotes();
        var service = new NoteService(notes, new StubContexts(), TimeProvider.System);

        Assert.Equal(NoteCreateStatus.InvalidTitle,
            await service.CreateAsync(User, 5, NoteTypes.Article, new string('a', NoteRules.TitleMaxLength + 1), CancellationToken.None));
        Assert.Equal(NoteCreateStatus.InvalidTitle, await service.CreateAsync(User, 5, NoteTypes.Article, "a\nb", CancellationToken.None));
        Assert.Null(notes.Added);
    }

    [Fact]
    public async Task ContextOutsideMembershipIsRejected()
    {
        var notes = new StubNotes();
        var service = new NoteService(notes, new StubContexts(exists: false), TimeProvider.System);

        Assert.Equal(NoteCreateStatus.ContextNotFound, await service.CreateAsync(User, 5, NoteTypes.Journal, null, CancellationToken.None));
        Assert.Null(notes.Added);
    }

    [Fact]
    public async Task MemberWhoIsNotOwnerOfTheContextMayWriteNotes()
    {
        var notes = new StubNotes();
        var service = new NoteService(notes, new StubContexts(isOwner: false), TimeProvider.System);

        Assert.Equal(NoteCreateStatus.Created, await service.CreateAsync(User, 5, NoteTypes.Article, null, CancellationToken.None));
    }

    [Fact]
    public async Task SeveralJournalsPerDayAreAllowed()
    {
        var notes = new StubNotes();
        var service = new NoteService(notes, new StubContexts(), TimeProvider.System);

        Assert.Equal(NoteCreateStatus.Created, await service.CreateAsync(User, 5, NoteTypes.Journal, "Dimineața", CancellationToken.None));
        Assert.Equal(NoteCreateStatus.Created, await service.CreateAsync(User, 5, NoteTypes.Journal, "După-amiaza", CancellationToken.None));
    }

    [Fact]
    public async Task BoardIsGroupedByLocalMonthAndEachMonthFollowsTheOrder()
    {
        var utc = (int month, int day, int hour) => new DateTime(2026, month, day, hour, 0, 0, DateTimeKind.Utc);
        var notes = new StubNotes(board:
        [
            Note(1, NoteTypes.Journal, utc(8, 20, 9), order: 1),
            Note(2, NoteTypes.Article, utc(9, 2, 9), order: 4),
            Note(3, NoteTypes.Journal, utc(9, 1, 9), order: 1),
            Note(4, NoteTypes.Article, utc(9, 5, 9), order: 3),
            // 22:30 UTC on 31 August is already September at UTC+3.
            Note(5, NoteTypes.Journal, new DateTime(2026, 8, 31, 22, 30, 0, DateTimeKind.Utc), order: 2),
        ]);
        var time = new FixedTime(new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(3));
        var service = new NoteService(notes, new StubContexts(), time);

        var months = await service.GetBoardAsync(User, 5, CancellationToken.None);

        Assert.Equal([(2026, 9), (2026, 8)], months.Select(month => (month.Year, month.Month)));
        Assert.Equal([3, 5, 4, 2], months[0].Notes.Select(note => note.Id));
        Assert.Equal([1], months[1].Notes.Select(note => note.Id));
        Assert.Equal(5, notes.BoardContext);
        Assert.Equal((2026, 9), service.GetCurrentMonth());
    }

    [Fact]
    public async Task OrderComesBeforeTheTypeAndTheDates()
    {
        var utc = (int day) => new DateTime(2026, 9, day, 9, 0, 0, DateTimeKind.Utc);
        var notes = new StubNotes(board:
        [
            Note(1, NoteTypes.Journal, utc(8), order: 3),
            Note(2, NoteTypes.Article, utc(1), order: 1),
            Note(3, NoteTypes.Journal, utc(2), modifiedAtUtc: utc(9), order: 2),
        ]);
        var service = new NoteService(notes, new StubContexts(), UtcTime);

        var months = await service.GetBoardAsync(User, 5, CancellationToken.None);

        Assert.Equal([2, 3, 1], months.Single().Notes.Select(note => note.Id));
    }

    [Fact]
    public async Task NotesWithTheSameOrderShowTheLatestChangeFirst()
    {
        var utc = (int month, int day) => new DateTime(2026, month, day, 9, 0, 0, DateTimeKind.Utc);
        var notes = new StubNotes(board:
        [
            // Created in August and changed at 22:30 UTC on 31 August, already September at UTC+3.
            Note(1, NoteTypes.Article, utc(8, 10), modifiedAtUtc: new DateTime(2026, 8, 31, 22, 30, 0, DateTimeKind.Utc)),
            Note(2, NoteTypes.Article, utc(9, 5)),
            // Created before note 2, changed after it.
            Note(3, NoteTypes.Article, utc(9, 1), modifiedAtUtc: utc(9, 8)),
            Note(4, NoteTypes.Journal, utc(9, 2)),
            Note(5, NoteTypes.Journal, utc(8, 20)),
        ]);
        var time = new FixedTime(new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(3));
        var service = new NoteService(notes, new StubContexts(), time);

        var months = await service.GetBoardAsync(User, 5, CancellationToken.None);

        Assert.Equal([(2026, 9), (2026, 8)], months.Select(month => (month.Year, month.Month)));
        // Journals no longer come first: with the same order, the latest change leads.
        Assert.Equal([3, 2, 4, 1], months[0].Notes.Select(note => note.Id));
        Assert.Equal([5], months[1].Notes.Select(note => note.Id));
    }

    [Fact]
    public async Task SameOrderAndLastChangeShowTheLatestCreatedThenTheHighestId()
    {
        var utc = (int day) => new DateTime(2026, 9, day, 9, 0, 0, DateTimeKind.Utc);
        var notes = new StubNotes(board:
        [
            Note(1, NoteTypes.Article, utc(1), modifiedAtUtc: utc(10)),
            Note(2, NoteTypes.Article, utc(5), modifiedAtUtc: utc(10)),
            Note(3, NoteTypes.Article, utc(5), modifiedAtUtc: utc(10)),
        ]);
        var service = new NoteService(notes, new StubContexts(), UtcTime);

        var months = await service.GetBoardAsync(User, 5, CancellationToken.None);

        Assert.Equal([3, 2, 1], months.Single().Notes.Select(note => note.Id));
    }

    private static NoteDocument Document(bool isOwner = true) =>
        new(7, 5, "TopDev", NoteTypes.Article, "Titlu", NoteVisibilities.Private, isOwner, DateTime.UtcNow, DateTime.UtcNow, "v1", []);

    [Fact]
    public async Task OwnerSavesNormalizedParagraphsInOrder()
    {
        var notes = new StubNotes(document: Document());
        // Sub-second parts are dropped: audit times have the precision of the stored columns.
        var time = new FixedTime(new DateTimeOffset(2026, 9, 24, 8, 0, 0, 700, TimeSpan.Zero), TimeSpan.FromHours(3));
        var service = new NoteService(notes, new StubContexts(), time);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var result = await service.SaveAsync(User, 7, "v1", "  Analiză  ",
            [new(first, "\nRând 1\r\nRând 2\n"), new(second, "Al doilea\tparagraf")], CancellationToken.None);

        Assert.Equal(new NoteSaveResult(NoteSaveStatus.Saved, "v2"), result);
        Assert.Equal("Analiză", notes.Saved!.Title);
        Assert.Equal([new NoteBlockInput(first, "Rând 1\nRând 2"), new NoteBlockInput(second, "Al doilea\tparagraf")], notes.Saved.Blocks);
        Assert.Equal(new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc), notes.Saved.SavedAtUtc);
        Assert.Equal(("v1", User), (notes.Saved.ExpectedVersion, notes.Saved.OwnerUserId));
    }

    [Fact]
    public async Task OnlyTheOwnerSaves()
    {
        var notes = new StubNotes(document: Document(isOwner: false));
        var service = new NoteService(notes, new StubContexts(), TimeProvider.System);

        Assert.Equal(NoteSaveStatus.Forbidden, (await service.SaveAsync(User, 7, "v1", null, [], CancellationToken.None)).Status);
        Assert.Null(notes.Saved);
    }

    [Fact]
    public async Task InvisibleNoteIsNotFound()
    {
        var notes = new StubNotes(document: null);
        var service = new NoteService(notes, new StubContexts(), TimeProvider.System);

        Assert.Equal(NoteSaveStatus.NotFound, (await service.SaveAsync(User, 7, "v1", null, [], CancellationToken.None)).Status);
        Assert.Null(notes.Saved);
    }

    public static TheoryData<NoteBlockInput[]> InvalidParagraphs()
    {
        var id = Guid.NewGuid();
        return new()
        {
            new[] { new NoteBlockInput(Guid.Empty, "Text") },
            new[] { new NoteBlockInput(id, "Unu"), new NoteBlockInput(id, "Doi") },
            new[] { new NoteBlockInput(Guid.NewGuid(), " \n ") },
            new[] { new NoteBlockInput(Guid.NewGuid(), "a\u0007b") },
            new[] { new NoteBlockInput(Guid.NewGuid(), new string('a', NoteRules.MaxContentLength + 1)) },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidParagraphs))]
    public async Task InvalidParagraphsAreRejectedBeforeDataAccess(NoteBlockInput[] blocks)
    {
        var notes = new StubNotes(document: Document());
        var service = new NoteService(notes, new StubContexts(), TimeProvider.System);

        Assert.Equal(NoteSaveStatus.InvalidContent, (await service.SaveAsync(User, 7, "v1", null, blocks, CancellationToken.None)).Status);
        Assert.Null(notes.Saved);
    }

    [Fact]
    public async Task TooManyParagraphsAreRejected()
    {
        var notes = new StubNotes(document: Document());
        var service = new NoteService(notes, new StubContexts(), TimeProvider.System);
        var blocks = Enumerable.Range(0, NoteRules.MaxBlocks + 1).Select(_ => new NoteBlockInput(Guid.NewGuid(), "x")).ToList();

        Assert.Equal(NoteSaveStatus.InvalidContent, (await service.SaveAsync(User, 7, "v1", null, blocks, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task InvalidTitleAndMissingVersionAreRejected()
    {
        var notes = new StubNotes(document: Document());
        var service = new NoteService(notes, new StubContexts(), TimeProvider.System);

        Assert.Equal(NoteSaveStatus.InvalidTitle, (await service.SaveAsync(User, 7, "v1", "a\nb", [], CancellationToken.None)).Status);
        Assert.Equal(NoteSaveStatus.Conflict, (await service.SaveAsync(User, 7, " ", null, [], CancellationToken.None)).Status);
        Assert.Null(notes.Saved);
    }

    [Fact]
    public async Task ConflictFromDataAccessIsReturned()
    {
        var notes = new StubNotes(document: Document(), saveResult: new NoteSaveResult(NoteSaveStatus.Conflict));
        var service = new NoteService(notes, new StubContexts(), TimeProvider.System);

        Assert.Equal(NoteSaveStatus.Conflict, (await service.SaveAsync(User, 7, "v1", null, [new(Guid.NewGuid(), "Text")], CancellationToken.None)).Status);
    }

    private static NoteSummary Note(int id, string type, DateTime createdAtUtc, bool isOwner = true, DateTime? modifiedAtUtc = null,
        int order = 0, int contextId = 5) =>
        new(id, contextId, type, null, null, null, NoteVisibilities.Private, createdAtUtc, modifiedAtUtc, isOwner, order, $"v{id}");

    [Fact]
    public async Task OwnerRenamesWithANormalizedTitleAndAudit()
    {
        var summary = Note(7, NoteTypes.Journal, new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc));
        var notes = new StubNotes(summary: summary);
        var time = new FixedTime(new DateTimeOffset(2026, 9, 24, 8, 0, 0, 900, TimeSpan.Zero), TimeSpan.FromHours(3));
        var service = new NoteService(notes, new StubContexts(), time);
        var savedAtUtc = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc);

        var result = await service.RenameAsync(User, 7, "  CR 30042  ", CancellationToken.None);

        // The card is refreshed with the stored title and the new modification time.
        Assert.Equal(new NoteRenameResult(NoteSaveStatus.Saved, summary with { Title = "CR 30042", ModifiedAtUtc = savedAtUtc }), result);
        Assert.Equal(savedAtUtc, result.Note!.LastChangedAtUtc);
        Assert.Equal((7, User, "CR 30042", savedAtUtc), notes.Renamed);
    }

    [Fact]
    public async Task EmptyTitleMakesTheNoteUntitled()
    {
        var notes = new StubNotes(summary: Note(7, NoteTypes.Journal, DateTime.UtcNow) with { Title = "Analiză" });
        var service = new NoteService(notes, new StubContexts(), TimeProvider.System);

        var result = await service.RenameAsync(User, 7, "   ", CancellationToken.None);

        Assert.Equal(NoteSaveStatus.Saved, result.Status);
        Assert.Null(result.Note!.Title);
        Assert.Null(notes.Renamed!.Value.Title);
    }

    [Fact]
    public async Task OnlyTheOwnerRenamesOrDeletes()
    {
        var notes = new StubNotes(summary: Note(7, NoteTypes.Journal, DateTime.UtcNow, isOwner: false));
        var service = new NoteService(notes, new StubContexts(), TimeProvider.System);

        Assert.Equal(NoteSaveStatus.Forbidden, (await service.RenameAsync(User, 7, "Titlu", CancellationToken.None)).Status);
        Assert.Equal(NoteDeleteStatus.Forbidden, await service.DeleteAsync(User, 7, CancellationToken.None));
        Assert.Null(notes.Renamed);
        Assert.Null(notes.Deleted);
    }

    [Fact]
    public async Task InvisibleNoteCannotBeRenamedOrDeleted()
    {
        var notes = new StubNotes(summary: null);
        var service = new NoteService(notes, new StubContexts(), TimeProvider.System);

        Assert.Equal(NoteSaveStatus.NotFound, (await service.RenameAsync(User, 7, "Titlu", CancellationToken.None)).Status);
        Assert.Equal(NoteDeleteStatus.NotFound, await service.DeleteAsync(User, 7, CancellationToken.None));
    }

    [Fact]
    public async Task InvalidTitleIsNotRenamed()
    {
        var notes = new StubNotes(summary: Note(7, NoteTypes.Journal, DateTime.UtcNow));
        var service = new NoteService(notes, new StubContexts(), TimeProvider.System);

        Assert.Equal(NoteSaveStatus.InvalidTitle, (await service.RenameAsync(User, 7, "a\tb", CancellationToken.None)).Status);
        Assert.Equal(NoteSaveStatus.InvalidTitle, (await service.RenameAsync(User, 7, new string('a', NoteRules.TitleMaxLength + 1), CancellationToken.None)).Status);
        Assert.Null(notes.Renamed);
    }

    [Fact]
    public async Task OwnerDeletes()
    {
        var notes = new StubNotes(summary: Note(7, NoteTypes.Article, DateTime.UtcNow));
        var service = new NoteService(notes, new StubContexts(), TimeProvider.System);

        Assert.Equal(NoteDeleteStatus.Deleted, await service.DeleteAsync(User, 7, CancellationToken.None));
        Assert.Equal((7, User), notes.Deleted);
    }

    [Fact]
    public async Task BoardIsReadForTheCurrentUser()
    {
        var notes = new StubNotes();
        var service = new NoteService(notes, new StubContexts(), TimeProvider.System);

        await service.GetBoardAsync(User, 5, CancellationToken.None);

        Assert.Equal(User, notes.BoardUser);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetBoardAsync(" ", 5, CancellationToken.None));
    }

    private static readonly DateTime September = new(2026, 9, 5, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task OwnerSwapsTwoNotesOfTheSameMonth()
    {
        var notes = new StubNotes(board:
        [
            Note(1, NoteTypes.Journal, September, order: 1),
            Note(2, NoteTypes.Article, September, order: 2),
            Note(3, NoteTypes.Article, September, order: 3),
        ]);
        var service = new NoteService(notes, new StubContexts(), UtcTime);

        var result = await service.SwapOrderAsync(User, 1, 3, CancellationToken.None);

        // Each takes the other's place; the one in between stays where it is.
        Assert.Equal(NoteOrderStatus.Saved, result.Status);
        Assert.Equal([3, 2, 1], result.NoteIds);
        Assert.Equal([new NoteVersionChange(1, "v1", "v1+"), new NoteVersionChange(3, "v3", "v3+")], result.Versions);
        Assert.Equal((User, 1, 3), (notes.Swapped!.Value.Owner, notes.Swapped.Value.First.Id, notes.Swapped.Value.Second.Id));
    }

    [Fact]
    public async Task NotesSwapOnlyWithinTheSameLocalMonth()
    {
        var notes = new StubNotes(board:
        [
            // 22:30 UTC on 31 August is already September at UTC+3; 20:00 UTC is still August.
            Note(1, NoteTypes.Journal, new DateTime(2026, 8, 31, 22, 30, 0, DateTimeKind.Utc), order: 1),
            Note(2, NoteTypes.Journal, new DateTime(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc), order: 2),
            Note(3, NoteTypes.Journal, new DateTime(2026, 8, 31, 20, 0, 0, DateTimeKind.Utc), order: 3),
        ]);
        var time = new FixedTime(new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(3));
        var service = new NoteService(notes, new StubContexts(), time);

        Assert.Equal(NoteOrderStatus.InvalidTarget, (await service.SwapOrderAsync(User, 3, 2, CancellationToken.None)).Status);
        Assert.Null(notes.Swapped);
        Assert.Equal(NoteOrderStatus.Saved, (await service.SwapOrderAsync(User, 1, 2, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task NotesOfAnotherBoardOrTheSameNoteCannotBeSwapped()
    {
        var notes = new StubNotes(board: [Note(1, NoteTypes.Journal, September), Note(2, NoteTypes.Journal, September, contextId: 6)]);
        var service = new NoteService(notes, new StubContexts(), UtcTime);

        Assert.Equal(NoteOrderStatus.InvalidTarget, (await service.SwapOrderAsync(User, 1, 2, CancellationToken.None)).Status);
        Assert.Equal(NoteOrderStatus.InvalidTarget, (await service.SwapOrderAsync(User, 1, 1, CancellationToken.None)).Status);
        Assert.Null(notes.Swapped);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task OnlyTheOwnerOfBothNotesSwapsThem(bool ownsNote, bool ownsTarget)
    {
        var notes = new StubNotes(board: [Note(1, NoteTypes.Journal, September, isOwner: ownsNote), Note(2, NoteTypes.Article, September, isOwner: ownsTarget)]);
        var service = new NoteService(notes, new StubContexts(), UtcTime);

        Assert.Equal(NoteOrderStatus.Forbidden, (await service.SwapOrderAsync(User, 1, 2, CancellationToken.None)).Status);
        Assert.Null(notes.Swapped);
    }

    [Fact]
    public async Task InvisibleNoteCannotBeSwapped()
    {
        var notes = new StubNotes(board: [Note(1, NoteTypes.Journal, September)]);
        var service = new NoteService(notes, new StubContexts(), UtcTime);

        Assert.Equal(NoteOrderStatus.NotFound, (await service.SwapOrderAsync(User, 1, 2, CancellationToken.None)).Status);
        Assert.Equal(NoteOrderStatus.NotFound, (await service.SwapOrderAsync(User, 2, 1, CancellationToken.None)).Status);
        Assert.Null(notes.Swapped);
    }

    [Fact]
    public async Task SwapOfANoteChangedInTheMeantimeIsAConflict()
    {
        var notes = new StubNotes(board: [Note(1, NoteTypes.Journal, September, order: 1), Note(2, NoteTypes.Article, September, order: 2)],
            swapConflict: true);
        var service = new NoteService(notes, new StubContexts(), UtcTime);

        var result = await service.SwapOrderAsync(User, 1, 2, CancellationToken.None);

        Assert.Equal(new NoteOrderResult(NoteOrderStatus.Conflict), result);
    }

    [Fact]
    public async Task CancelledSwapStopsBeforeDataAccess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var notes = new StubNotes(board: [Note(1, NoteTypes.Journal, September), Note(2, NoteTypes.Article, September)]);
        var service = new NoteService(notes, new StubContexts(), UtcTime);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SwapOrderAsync(User, 1, 2, cancellation.Token));
        Assert.Null(notes.Swapped);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SwapOrderAsync(" ", 1, 2, CancellationToken.None));
    }

    private sealed class FixedTime(DateTimeOffset utcNow, TimeSpan offset) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone("Test", offset, "Test", "Test");
    }

    private sealed class StubNotes(NoteCreateStatus outcome = NoteCreateStatus.Created, IReadOnlyList<NoteSummary>? board = null,
        NoteDocument? document = null, NoteSaveResult? saveResult = null, NoteSummary? summary = null, bool swapConflict = false) : INoteRepository
    {
        // The stored notes: a swap changes their orders, like the database.
        private readonly List<NoteSummary> stored = [.. board ?? []];

        public NoteChanges? Saved { get; private set; }
        public (int NoteId, string Owner, string? Title, DateTime SavedAtUtc)? Renamed { get; private set; }
        public (int NoteId, string Owner)? Deleted { get; private set; }
        public (string Owner, NoteSummary First, NoteSummary Second)? Swapped { get; private set; }

        public Task<NoteSummary?> GetSummaryAsync(int noteId, string userId, CancellationToken cancellationToken) =>
            Task.FromResult(summary ?? stored.FirstOrDefault(note => note.Id == noteId));

        public Task<IReadOnlyList<NoteVersionChange>?> SwapOrderAsync(string ownerUserId, NoteSummary first, NoteSummary second,
            CancellationToken cancellationToken)
        {
            Swapped = (ownerUserId, first, second);
            if (swapConflict) return Task.FromResult<IReadOnlyList<NoteVersionChange>?>(null);
            stored[stored.FindIndex(note => note.Id == first.Id)] = first with { Order = second.Order, Version = first.Version + "+" };
            stored[stored.FindIndex(note => note.Id == second.Id)] = second with { Order = first.Order, Version = second.Version + "+" };
            return Task.FromResult<IReadOnlyList<NoteVersionChange>?>(
                [new(first.Id, first.Version, first.Version + "+"), new(second.Id, second.Version, second.Version + "+")]);
        }

        public Task<bool> RenameAsync(int noteId, string ownerUserId, string? title, DateTime savedAtUtc, CancellationToken cancellationToken)
        {
            Renamed = (noteId, ownerUserId, title, savedAtUtc);
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(int noteId, string ownerUserId, CancellationToken cancellationToken)
        {
            Deleted = (noteId, ownerUserId);
            return Task.FromResult(true);
        }

        public Task<NoteDocument?> GetDocumentAsync(int noteId, string userId, CancellationToken cancellationToken) =>
            Task.FromResult(document);

        public Task<NoteSaveResult> SaveAsync(NoteChanges changes, CancellationToken cancellationToken)
        {
            Saved = changes;
            return Task.FromResult(saveResult ?? new NoteSaveResult(NoteSaveStatus.Saved, "v2"));
        }

        public NewNote? Added { get; private set; }
        public string? BoardUser { get; private set; }
        public int? BoardContext { get; private set; }

        public Task<IReadOnlyList<NoteSummary>> GetBoardAsync(string userId, int contextId, CancellationToken cancellationToken)
        {
            BoardUser = userId;
            BoardContext = contextId;
            return Task.FromResult<IReadOnlyList<NoteSummary>>([.. stored]);
        }

        public Task<NoteCreateStatus> AddAsync(NewNote note, CancellationToken cancellationToken)
        {
            Added = note;
            return Task.FromResult(outcome);
        }
    }

    private sealed class StubContexts(bool exists = true, bool isOwner = true) : IWorkContextRepository
    {
        public bool WasCalled { get; private set; }

        public Task<WorkContext?> GetByIdAsync(int id, string userId, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(exists ? new WorkContext(id, "TopDev", null, isOwner) : null);
        }

        public Task<IReadOnlyList<WorkContext>> GetForMemberAsync(string userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WorkContextSaveStatus> AddAsync(string name, string? description, string ownerUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WorkContextSaveStatus> UpdateAsync(int id, string userId, string name, string? description, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WorkContextDeleteStatus> DeleteAsync(int id, string userId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
