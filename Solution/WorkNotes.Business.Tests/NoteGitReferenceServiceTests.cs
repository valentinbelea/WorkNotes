using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Business.Services;

namespace WorkNotes.Business.Tests;

public sealed class NoteGitReferenceServiceTests
{
    private const string UserId = "user-1";
    private const int NoteId = 7;
    private const string RepositoryUrl = "https://github.com/octocat/hello";
    private static readonly Guid BlockId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 29, 12, 0, 0, 700, TimeSpan.Zero);
    private static readonly DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly GitCredential Credential = new("583231", "octocat", new GitTokens("access-1", null, null, null, null), Now, Now);

    // ---- Searching the branches -------------------------------------------------------------------------------

    [Fact]
    public async Task OnlyTheBranchesThatContainTheReferenceAreFoundByName()
    {
        var gitHub = new StubGitHub { Branches = ["main", "feature/CR_30080_export", "fix/cr-30080", "feature/CR_30081", "Feature/CR_30080", "feature/xCR_30080"] };

        var search = await Service(gitHub: gitHub).SearchBranchesAsync(UserId, NoteId, "42", "CR:30080", CancellationToken.None);

        Assert.Equal(GitReferenceStatus.Succeeded, search.Status);
        Assert.Equal(["Feature/CR_30080", "feature/CR_30080_export", "fix/cr-30080"], search.Branches.Select(branch => branch.Name));
        Assert.Equal($"{RepositoryUrl}/tree/feature/CR_30080_export", search.Branches[1].Url);
        Assert.Equal("access-1", gitHub.ReceivedAccessToken);
        Assert.Equal("octocat/hello", gitHub.ReceivedRepository);
        Assert.False(search.Truncated);
    }

    [Fact]
    public async Task OnlyTheFirstMatchesAreListedAndATruncatedRepositoryIsReported()
    {
        var gitHub = new StubGitHub
        {
            Branches = Enumerable.Range(0, GitReferenceRules.MaxBranchesShown + 10).Select(index => $"feature/CR_30080_{index:000}").ToList(),
            Truncated = true
        };

        var search = await Service(gitHub: gitHub).SearchBranchesAsync(UserId, NoteId, "42", "CR:30080", CancellationToken.None);

        Assert.Equal(GitReferenceRules.MaxBranchesShown, search.Branches.Count);
        Assert.True(search.Truncated);
    }

    [Fact]
    public async Task ARepositoryWithoutMatchesGivesAnEmptyList()
    {
        var search = await Service().SearchBranchesAsync(UserId, NoteId, "42", "CR:999", CancellationToken.None);

        Assert.Equal(GitReferenceStatus.Succeeded, search.Status);
        Assert.Empty(search.Branches);
    }

    [Theory]
    [InlineData(null, GitReferenceStatus.NotFound)]
    [InlineData(false, GitReferenceStatus.Forbidden)]
    public async Task OnlyTheOwnerOfAVisibleNoteSearches(bool? owner, GitReferenceStatus expected)
    {
        var gitHub = new StubGitHub();

        var search = await Service(links: new StubLinks { Owner = owner }, gitHub: gitHub).SearchBranchesAsync(UserId, NoteId, "42", "CR:30080", CancellationToken.None);

        Assert.Equal(expected, search.Status);
        Assert.Null(gitHub.ReceivedAccessToken);
    }

    [Theory]
    [InlineData("")]
    [InlineData("TASK:1")]
    [InlineData("cr:30080")]
    public async Task ATextThatIsNotANormalizedReferenceIsRefusedBeforeGitHub(string reference)
    {
        var gitHub = new StubGitHub();

        var search = await Service(gitHub: gitHub).SearchBranchesAsync(UserId, NoteId, "42", reference, CancellationToken.None);

        Assert.Equal(GitReferenceStatus.InvalidReference, search.Status);
        Assert.Null(gitHub.ReceivedAccessToken);
    }

    [Theory]
    [InlineData("43")]
    [InlineData("abc")]
    [InlineData("")]
    public async Task OnlyAnImportedRepositoryIsSearched(string repositoryId)
    {
        var gitHub = new StubGitHub();

        var search = await Service(gitHub: gitHub).SearchBranchesAsync(UserId, NoteId, repositoryId, "CR:30080", CancellationToken.None);

        Assert.Equal(GitReferenceStatus.RepositoryNotFound, search.Status);
        Assert.Null(gitHub.ReceivedAccessToken);
    }

    [Theory]
    [InlineData(GitVerifyStatus.NotConfigured, GitReferenceStatus.NotConfigured)]
    [InlineData(GitVerifyStatus.NotConnected, GitReferenceStatus.NotConnected)]
    [InlineData(GitVerifyStatus.ReconnectRequired, GitReferenceStatus.ReconnectRequired)]
    [InlineData(GitVerifyStatus.Unavailable, GitReferenceStatus.Unavailable)]
    public async Task WithoutAUsableTokenNothingIsSearched(GitVerifyStatus token, GitReferenceStatus expected)
    {
        var gitHub = new StubGitHub();

        var search = await Service(gitHub: gitHub, token: GitAccessToken.Failed(token)).SearchBranchesAsync(UserId, NoteId, "42", "CR:30080", CancellationToken.None);

        Assert.Equal(expected, search.Status);
        Assert.Null(gitHub.ReceivedAccessToken);
    }

    [Theory]
    [InlineData(GitProviderStatus.NotFound, GitReferenceStatus.RepositoryNotFound)]
    [InlineData(GitProviderStatus.Rejected, GitReferenceStatus.ReconnectRequired)]
    [InlineData(GitProviderStatus.Unavailable, GitReferenceStatus.Unavailable)]
    public async Task AListGitHubDoesNotGiveIsReportedAsWhatItIs(GitProviderStatus listing, GitReferenceStatus expected)
    {
        var search = await Service(gitHub: new StubGitHub { Listing = listing }).SearchBranchesAsync(UserId, NoteId, "42", "CR:30080", CancellationToken.None);

        Assert.Equal(expected, search.Status);
        Assert.Empty(search.Branches);
    }

    // ---- Linking a branch -------------------------------------------------------------------------------------

    [Fact]
    public async Task ABranchIsLinkedToTheReferenceOfTheParagraph()
    {
        var links = new StubLinks { BlockContent = "Working on cr 30080 today" };
        var gitHub = new StubGitHub { Existing = "feature/CR_30080_export" };

        var result = await Service(links, gitHub).AddAsync(UserId, NoteId, BlockId, "42", "CR:30080", "feature/CR_30080_export", CancellationToken.None);

        Assert.Equal(GitReferenceStatus.Succeeded, result.Status);
        var (noteId, added) = Assert.Single(links.Added);
        Assert.Equal(NoteId, noteId);
        Assert.Equal(new NewNoteGitReference(BlockId, "CR", 30080, "GitHub", "42", "octocat/hello", RepositoryUrl, "feature/CR_30080_export", UserId, Now), added);
        Assert.Equal("CR:30080", added.NormalizedReference);
        Assert.Equal("feature/CR_30080_export", gitHub.ReceivedBranch);
        Assert.Equal((BlockId, UserId), links.ReceivedBlock);
    }

    [Fact]
    public async Task TheBranchIsStoredWithTheNameGitHubGivesIt()
    {
        var links = new StubLinks { BlockContent = "CR 30080" };
        var gitHub = new StubGitHub { Existing = "feature/CR_30080" };

        await Service(links, gitHub).AddAsync(UserId, NoteId, BlockId, "42", "CR:30080", "feature/CR_30080", CancellationToken.None);

        Assert.Equal("feature/CR_30080", Assert.Single(links.Added).Reference.BranchName);
    }

    [Fact]
    public async Task TheResultHasTheLinksOfTheNoteNow()
    {
        var links = new StubLinks
        {
            BlockContent = "CR 30080",
            Stored = [Stored(1, "CR:30080", "feature/CR_30080", "CR 30080 is here")]
        };

        var result = await Service(links, new StubGitHub { Existing = "feature/CR_30080" })
            .AddAsync(UserId, NoteId, BlockId, "42", "CR:30080", "feature/CR_30080", CancellationToken.None);

        var reference = Assert.Single(result.References!);
        Assert.Equal(new NoteGitReference(1, BlockId, "CR:30080", "octocat/hello", "feature/CR_30080", $"{RepositoryUrl}/tree/feature/CR_30080"), reference);
    }

    [Theory]
    [InlineData("The paragraph writes CR 30081 only")]
    [InlineData("")]
    public async Task AParagraphThatDoesNotWriteTheReferenceCannotBeLinked(string content)
    {
        var links = new StubLinks { BlockContent = content };
        var gitHub = new StubGitHub { Existing = "feature/CR_30080" };

        var result = await Service(links, gitHub).AddAsync(UserId, NoteId, BlockId, "42", "CR:30080", "feature/CR_30080", CancellationToken.None);

        Assert.Equal(GitReferenceStatus.InvalidReference, result.Status);
        Assert.Empty(links.Added);
        Assert.Null(gitHub.ReceivedAccessToken);
    }

    [Fact]
    public async Task AParagraphThatIsNotTheOwnersNoteIsRefused()
    {
        var links = new StubLinks { BlockContent = null };

        var result = await Service(links).AddAsync(UserId, NoteId, BlockId, "42", "CR:30080", "feature/CR_30080", CancellationToken.None);

        Assert.Equal(GitReferenceStatus.InvalidReference, result.Status);
        Assert.Empty(links.Added);
    }

    [Theory]
    [InlineData(null, GitReferenceStatus.NotFound)]
    [InlineData(false, GitReferenceStatus.Forbidden)]
    public async Task OnlyTheOwnerOfAVisibleNoteLinks(bool? owner, GitReferenceStatus expected)
    {
        var links = new StubLinks { Owner = owner, BlockContent = "CR 30080" };

        var result = await Service(links).AddAsync(UserId, NoteId, BlockId, "42", "CR:30080", "feature/CR_30080", CancellationToken.None);

        Assert.Equal(expected, result.Status);
        Assert.Empty(links.Added);
    }

    [Fact]
    public async Task AnUnknownRepositoryIsRefused()
    {
        var result = await Service(new StubLinks { BlockContent = "CR 30080" })
            .AddAsync(UserId, NoteId, BlockId, "43", "CR:30080", "feature/CR_30080", CancellationToken.None);

        Assert.Equal(GitReferenceStatus.RepositoryNotFound, result.Status);
    }

    [Theory]
    [InlineData("feature/CR_30081")]
    [InlineData("main")]
    public async Task ABranchWhoseNameDoesNotContainTheReferenceIsRefused(string branch)
    {
        var links = new StubLinks { BlockContent = "CR 30080" };

        var result = await Service(links, new StubGitHub { Existing = branch }).AddAsync(UserId, NoteId, BlockId, "42", "CR:30080", branch, CancellationToken.None);

        Assert.Equal(GitReferenceStatus.BranchNotFound, result.Status);
        Assert.Empty(links.Added);
    }

    [Fact]
    public async Task ABranchGitHubDoesNotHaveIsRefused()
    {
        var links = new StubLinks { BlockContent = "CR 30080" };

        var result = await Service(links, new StubGitHub { Existing = null }).AddAsync(UserId, NoteId, BlockId, "42", "CR:30080", "feature/CR_30080", CancellationToken.None);

        Assert.Equal(GitReferenceStatus.BranchNotFound, result.Status);
        Assert.Empty(links.Added);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ABranchNameThatCannotBeStoredIsRefusedBeforeGitHub(string branch)
    {
        var gitHub = new StubGitHub();

        var result = await Service(new StubLinks { BlockContent = "CR 30080" }, gitHub).AddAsync(UserId, NoteId, BlockId, "42", "CR:30080", branch, CancellationToken.None);

        Assert.Equal(GitReferenceStatus.BranchNotFound, result.Status);
        Assert.Null(gitHub.ReceivedBranch);
    }

    [Theory]
    [InlineData(GitProviderStatus.Rejected, GitReferenceStatus.ReconnectRequired)]
    [InlineData(GitProviderStatus.Unavailable, GitReferenceStatus.Unavailable)]
    public async Task AFailedBranchCheckIsReportedAsWhatItIs(GitProviderStatus checkStatus, GitReferenceStatus expected)
    {
        var links = new StubLinks { BlockContent = "CR 30080" };

        var result = await Service(links, new StubGitHub { BranchStatus = checkStatus }).AddAsync(UserId, NoteId, BlockId, "42", "CR:30080", "feature/CR_30080", CancellationToken.None);

        Assert.Equal(expected, result.Status);
        Assert.Empty(links.Added);
    }

    [Fact]
    public async Task AParagraphDeletedMeanwhileIsAnInvalidReference()
    {
        var links = new StubLinks { BlockContent = "CR 30080", AddSucceeds = false };

        var result = await Service(links, new StubGitHub { Existing = "feature/CR_30080" })
            .AddAsync(UserId, NoteId, BlockId, "42", "CR:30080", "feature/CR_30080", CancellationToken.None);

        Assert.Equal(GitReferenceStatus.InvalidReference, result.Status);
    }

    // ---- Removing a link --------------------------------------------------------------------------------------

    [Fact]
    public async Task TheOwnerRemovesALinkAndGetsTheOthers()
    {
        var links = new StubLinks { Stored = [Stored(2, "CR:30080", "feature/CR_30080_b", "CR 30080")] };

        var result = await Service(links).RemoveAsync(UserId, NoteId, 1, CancellationToken.None);

        Assert.Equal(GitReferenceStatus.Succeeded, result.Status);
        Assert.Equal([2], result.References!.Select(reference => reference.Id));
        Assert.Equal((NoteId, UserId, 1), links.Removed);
    }

    [Fact]
    public async Task ALinkThatDoesNotExistCannotBeRemoved()
    {
        var result = await Service(new StubLinks { RemoveSucceeds = false }).RemoveAsync(UserId, NoteId, 99, CancellationToken.None);

        Assert.Equal(GitReferenceStatus.InvalidReference, result.Status);
    }

    [Theory]
    [InlineData(null, GitReferenceStatus.NotFound)]
    [InlineData(false, GitReferenceStatus.Forbidden)]
    public async Task OnlyTheOwnerOfAVisibleNoteRemoves(bool? owner, GitReferenceStatus expected)
    {
        var links = new StubLinks { Owner = owner };

        var result = await Service(links).RemoveAsync(UserId, NoteId, 1, CancellationToken.None);

        Assert.Equal(expected, result.Status);
        Assert.Null(links.Removed);
    }

    // ---- Reading the links ------------------------------------------------------------------------------------

    [Fact]
    public async Task ALinkShowsWhileItsParagraphStillWritesItsReference()
    {
        var links = new StubLinks
        {
            Stored =
            [
                Stored(1, "CR:30080", "feature/CR_30080", "Written: cr-30080"),
                Stored(2, "CR:30080", "feature/CR_30080_old", "Now it says CR 30081"),
                Stored(3, "BUG:5", "fix/BUG_5", "bug 5 too", position: 0)
            ]
        };

        var shown = await Service(links).GetAsync(UserId, NoteId, CancellationToken.None);

        Assert.Equal([3, 1], shown.Select(reference => reference.Id));
    }

    [Fact]
    public async Task LinksAreInDocumentOrderThenByReferenceAndBranch()
    {
        var links = new StubLinks
        {
            Stored =
            [
                Stored(1, "CR:2", "feature/CR_2_b", "CR 2 and CR 1", position: 1),
                Stored(2, "CR:1", "feature/CR_1", "CR 2 and CR 1", position: 1),
                Stored(3, "CR:2", "feature/CR_2_a", "CR 2 and CR 1", position: 1),
                Stored(4, "CR:9", "feature/CR_9", "CR 9", position: 0)
            ]
        };

        var shown = await Service(links).GetAsync(UserId, NoteId, CancellationToken.None);

        Assert.Equal([4, 2, 3, 1], shown.Select(reference => reference.Id));
    }

    [Fact]
    public async Task ALinkWhoseRepositoryAddressIsNotHttpsIsNotShown()
    {
        var links = new StubLinks
        {
            Stored =
            [
                Stored(1, "CR:1", "feature/CR_1", "CR 1") with { RepositoryUrl = "javascript:alert(1)" },
                Stored(2, "CR:1", "feature/CR_1", "CR 1") with { RepositoryUrl = "http://github.com/o/r" },
                Stored(3, "CR:1", "feature/CR_1", "CR 1")
            ]
        };

        var shown = await Service(links).GetAsync(UserId, NoteId, CancellationToken.None);

        Assert.Equal([3], shown.Select(reference => reference.Id));
    }

    [Fact]
    public async Task ANoteWithoutLinksReadsNothingElse()
    {
        var types = new StubTypes();

        Assert.Empty(await Service(types: types).GetAsync(UserId, NoteId, CancellationToken.None));
        Assert.Equal(0, types.Reads);
    }

    // ---- Cancellation and failures ----------------------------------------------------------------------------

    [Fact]
    public async Task CancelledRequestsStopBeforeDataAccess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var links = new StubLinks { BlockContent = "CR 30080" };
        var gitHub = new StubGitHub();
        var service = Service(links, gitHub);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SearchBranchesAsync(UserId, NoteId, "42", "CR:30080", cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.AddAsync(UserId, NoteId, BlockId, "42", "CR:30080", "b", cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RemoveAsync(UserId, NoteId, 1, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync(UserId, NoteId, cancellation.Token));
        Assert.Equal(0, links.Calls);
        Assert.Null(gitHub.ReceivedAccessToken);
    }

    [Fact]
    public async Task TheRequestTokenReachesEveryDependency()
    {
        using var cancellation = new CancellationTokenSource();
        var links = new StubLinks { BlockContent = "CR 30080" };
        var gitHub = new StubGitHub { Existing = "feature/CR_30080" };

        await Service(links, gitHub).AddAsync(UserId, NoteId, BlockId, "42", "CR:30080", "feature/CR_30080", cancellation.Token);

        Assert.NotEmpty(links.ReceivedTokens);
        Assert.All(links.ReceivedTokens.Concat(gitHub.ReceivedTokens), token => Assert.Equal(cancellation.Token, token));
        Assert.NotEmpty(gitHub.ReceivedTokens);
    }

    [Fact]
    public async Task ADatabaseFailureIsNotAMissingNote()
    {
        var expected = new InvalidOperationException("Database unavailable.");
        var service = Service(new StubLinks { Error = expected });

        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SearchBranchesAsync(UserId, NoteId, "42", "CR:30080", CancellationToken.None)));
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAsync(UserId, NoteId, CancellationToken.None)));
    }

    // ---- Stubs ------------------------------------------------------------------------------------------------

    private static NoteGitReferenceService Service(StubLinks? links = null, StubGitHub? gitHub = null, GitAccessToken? token = null, StubTypes? types = null) =>
        new(links ?? new StubLinks(), new StubRepositories(), new StubTokens(token ?? new GitAccessToken(GitVerifyStatus.Valid, Credential)),
            gitHub ?? new StubGitHub(), types ?? new StubTypes(), new FixedTime(UtcNow));

    private static StoredNoteGitReference Stored(int id, string reference, string branch, string content, int position = 0) =>
        new(id, BlockId, position, content, reference, "octocat/hello", RepositoryUrl, branch);

    private sealed class FixedTime(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubTypes : IReferenceTypeService
    {
        private readonly NoteReferenceParser parser = new(["CR", "BUG"]);
        public int Reads { get; private set; }

        public Task<NoteReferenceParser> GetParserAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reads++;
            return Task.FromResult(parser);
        }
    }

    private sealed class StubTokens(GitAccessToken token) : IGitHubTokenService
    {
        public Task<GitAccessToken> GetAsync(string userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(token);
        }
    }

    // One imported repository: 42, octocat/hello.
    private sealed class StubRepositories : IGitRepositoryRepository
    {
        public Task<IReadOnlyList<GitRepository>> GetImportedAsync(string userId, string provider, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GitRepository>>(
                [new GitRepository(new GitRepositoryInfo("42", "octocat/hello", null, false, "main", RepositoryUrl), Now, Now)]);

        public Task<bool> ReplaceAsync(string userId, string provider, IReadOnlyList<GitRepositoryInfo> repositories, DateTime nowUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubGitHub : IGitHubOAuthClient
    {
        public IReadOnlyList<string> Branches { get; init; } = ["main", "feature/CR_30080"];
        public bool Truncated { get; init; }
        public GitProviderStatus Listing { get; init; } = GitProviderStatus.Succeeded;
        // The branch GitHub has for the name asked (null: none).
        public string? Existing { get; init; }
        public GitProviderStatus BranchStatus { get; init; } = GitProviderStatus.Succeeded;
        public string? ReceivedAccessToken { get; private set; }
        public string? ReceivedRepository { get; private set; }
        public string? ReceivedBranch { get; private set; }
        public List<CancellationToken> ReceivedTokens { get; } = [];
        public bool IsConfigured => true;

        public Task<GitProviderResult<GitBranchCatalog>> GetBranchesAsync(string accessToken, string repositoryFullName, CancellationToken cancellationToken)
        {
            (ReceivedAccessToken, ReceivedRepository) = (accessToken, repositoryFullName);
            ReceivedTokens.Add(cancellationToken);
            return Task.FromResult(Listing switch
            {
                GitProviderStatus.Succeeded => GitProviderResult<GitBranchCatalog>.Succeeded(new GitBranchCatalog(Branches, Truncated)),
                GitProviderStatus.NotFound => GitProviderResult<GitBranchCatalog>.NotFound,
                GitProviderStatus.Rejected => GitProviderResult<GitBranchCatalog>.Rejected,
                _ => GitProviderResult<GitBranchCatalog>.Unavailable
            });
        }

        public Task<GitProviderResult<string>> GetBranchAsync(string accessToken, string repositoryFullName, string branchName, CancellationToken cancellationToken)
        {
            (ReceivedAccessToken, ReceivedRepository, ReceivedBranch) = (accessToken, repositoryFullName, branchName);
            ReceivedTokens.Add(cancellationToken);
            return Task.FromResult(BranchStatus switch
            {
                GitProviderStatus.Succeeded => Existing is null ? GitProviderResult<string>.NotFound : GitProviderResult<string>.Succeeded(Existing),
                GitProviderStatus.NotFound => GitProviderResult<string>.NotFound,
                GitProviderStatus.Rejected => GitProviderResult<string>.Rejected,
                _ => GitProviderResult<string>.Unavailable
            });
        }

        public string GetAuthorizationUrl(string state, string codeChallenge, string redirectUri) => throw new NotSupportedException();
        public Task<GitProviderResult<GitTokens>> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<GitProviderResult<GitTokens>> RefreshAsync(string refreshToken, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<GitProviderResult<GitAccount>> GetAccountAsync(string accessToken, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<GitProviderResult<GitRepositoryCatalog>> GetRepositoriesAsync(string accessToken, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<GitProviderStatus> RevokeAsync(string accessToken, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubLinks : INoteGitReferenceRepository
    {
        public bool? Owner { get; init; } = true;
        public string? BlockContent { get; init; }
        public IReadOnlyList<StoredNoteGitReference> Stored { get; init; } = [];
        public bool AddSucceeds { get; init; } = true;
        public bool RemoveSucceeds { get; init; } = true;
        public Exception? Error { get; init; }
        public int Calls { get; private set; }
        public (Guid BlockId, string OwnerUserId)? ReceivedBlock { get; private set; }
        public (int NoteId, string OwnerUserId, int LinkId)? Removed { get; private set; }
        public List<(int NoteId, NewNoteGitReference Reference)> Added { get; } = [];
        public List<CancellationToken> ReceivedTokens { get; } = [];

        public Task<bool?> IsOwnerAsync(int noteId, string userId, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            return Task.FromResult(Owner);
        }

        public Task<IReadOnlyList<StoredNoteGitReference>> GetAsync(int noteId, string userId, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            return Task.FromResult(Stored);
        }

        public Task<string?> GetBlockContentAsync(int noteId, string ownerUserId, Guid blockId, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            ReceivedBlock = (blockId, ownerUserId);
            return Task.FromResult(BlockContent);
        }

        public Task<bool> AddAsync(int noteId, NewNoteGitReference reference, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            if (AddSucceeds) Added.Add((noteId, reference));
            return Task.FromResult(AddSucceeds);
        }

        public Task<bool> RemoveAsync(int noteId, string ownerUserId, int linkId, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            if (RemoveSucceeds) Removed = (noteId, ownerUserId, linkId);
            return Task.FromResult(RemoveSucceeds);
        }

        private void Record(CancellationToken cancellationToken)
        {
            Calls++;
            ReceivedTokens.Add(cancellationToken);
            if (Error is not null) throw Error;
        }
    }
}
