using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Business.Services;

namespace WorkNotes.Business.Tests;

public sealed class NoteServiceTests
{
    private const string User = "user-1";

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
    public async Task BoardIsGroupedByLocalMonthWithJournalsBeforeArticles()
    {
        var utc = (int month, int day, int hour) => new DateTime(2026, month, day, hour, 0, 0, DateTimeKind.Utc);
        var notes = new StubNotes(board:
        [
            Note(1, NoteTypes.Journal, utc(8, 20, 9)),
            Note(2, NoteTypes.Article, utc(9, 2, 9)),
            Note(3, NoteTypes.Journal, utc(9, 1, 9)),
            Note(4, NoteTypes.Article, utc(9, 5, 9)),
            // 22:30 UTC on 31 August is already September at UTC+3.
            Note(5, NoteTypes.Journal, new DateTime(2026, 8, 31, 22, 30, 0, DateTimeKind.Utc)),
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
    public async Task BoardFollowsTheLastChangeAndTheCreationForNotesNeverChanged()
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
        Assert.Equal([4, 3, 2, 1], months[0].Notes.Select(note => note.Id));
        Assert.Equal([5], months[1].Notes.Select(note => note.Id));
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

    private static NoteSummary Note(int id, string type, DateTime createdAtUtc, bool isOwner = true, DateTime? modifiedAtUtc = null) =>
        new(id, 5, type, null, null, null, NoteVisibilities.Private, createdAtUtc, modifiedAtUtc, isOwner);

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

    private sealed class FixedTime(DateTimeOffset utcNow, TimeSpan offset) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone("Test", offset, "Test", "Test");
    }

    private sealed class StubNotes(NoteCreateStatus outcome = NoteCreateStatus.Created, IReadOnlyList<NoteSummary>? board = null,
        NoteDocument? document = null, NoteSaveResult? saveResult = null, NoteSummary? summary = null) : INoteRepository
    {
        public NoteChanges? Saved { get; private set; }
        public (int NoteId, string Owner, string? Title, DateTime SavedAtUtc)? Renamed { get; private set; }
        public (int NoteId, string Owner)? Deleted { get; private set; }

        public Task<NoteSummary?> GetSummaryAsync(int noteId, string userId, CancellationToken cancellationToken) => Task.FromResult(summary);

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
            return Task.FromResult(board ?? []);
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
