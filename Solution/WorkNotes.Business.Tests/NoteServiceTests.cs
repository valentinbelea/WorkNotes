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
    public async Task ExistingDailyJournalIsReported()
    {
        var service = new NoteService(new StubNotes(NoteCreateStatus.JournalExists), new StubContexts(), TimeProvider.System);

        Assert.Equal(NoteCreateStatus.JournalExists, await service.CreateAsync(User, 5, NoteTypes.Journal, null, CancellationToken.None));
    }

    [Fact]
    public async Task BoardIsReadForTheCurrentUser()
    {
        var notes = new StubNotes();
        var service = new NoteService(notes, new StubContexts(), TimeProvider.System);

        await service.GetBoardAsync(User, CancellationToken.None);

        Assert.Equal(User, notes.BoardUser);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetBoardAsync(" ", CancellationToken.None));
    }

    private sealed class FixedTime(DateTimeOffset utcNow, TimeSpan offset) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone("Test", offset, "Test", "Test");
    }

    private sealed class StubNotes(NoteCreateStatus outcome = NoteCreateStatus.Created) : INoteRepository
    {
        public NewNote? Added { get; private set; }
        public string? BoardUser { get; private set; }

        public Task<IReadOnlyList<NoteSummary>> GetBoardAsync(string userId, CancellationToken cancellationToken)
        {
            BoardUser = userId;
            return Task.FromResult<IReadOnlyList<NoteSummary>>([]);
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
