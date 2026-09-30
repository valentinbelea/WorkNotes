using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Business.Services;

namespace WorkNotes.Business.Tests;

public sealed class GitHubConfigurationServiceTests
{
    [Fact]
    public async Task FirstSaveRequiresSecret()
    {
        var repository = new Repository();
        var result = await new GitHubConfigurationService(repository).SaveAsync(
            new("client", null, "read:user", "https://example.test/callback"), TestContext.Current.CancellationToken);
        Assert.Equal(GitHubConfigurationSaveStatus.ClientSecretRequired, result);
        Assert.Null(repository.Saved);
    }

    [Fact]
    public async Task ExistingSecretIsKeptWhenInputIsEmpty()
    {
        var repository = new Repository { Current = new("old", true, "read:user", "https://old.test") };
        var result = await new GitHubConfigurationService(repository).SaveAsync(
            new("new", " ", "repo read:user", "https://example.test/callback"), TestContext.Current.CancellationToken);
        Assert.Equal(GitHubConfigurationSaveStatus.Succeeded, result);
        Assert.Null(repository.Saved!.ClientSecret);
    }

    [Theory]
    [InlineData("scope<script>", GitHubConfigurationSaveStatus.InvalidScopes)]
    [InlineData("not a url", GitHubConfigurationSaveStatus.InvalidCallbackUrl)]
    public async Task InvalidValuesAreRejected(string value, GitHubConfigurationSaveStatus expected)
    {
        var input = expected == GitHubConfigurationSaveStatus.InvalidScopes
            ? new GitHubConfigurationInput("client", "secret", value, "https://example.test")
            : new GitHubConfigurationInput("client", "secret", "read:user", value);
        Assert.Equal(expected, await new GitHubConfigurationService(new Repository()).SaveAsync(input, TestContext.Current.CancellationToken));
    }

    private sealed class Repository : IGitHubConfigurationRepository
    {
        public GitHubConfiguration? Current { get; init; }
        public GitHubConfigurationInput? Saved { get; private set; }
        public Task<GitHubConfiguration?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Current);
        public Task<GitHubConfigurationCredential?> GetCredentialAsync(CancellationToken cancellationToken) => Task.FromResult<GitHubConfigurationCredential?>(null);
        public Task SaveAsync(GitHubConfigurationInput input, CancellationToken cancellationToken) { Saved = input; return Task.CompletedTask; }
    }
}
