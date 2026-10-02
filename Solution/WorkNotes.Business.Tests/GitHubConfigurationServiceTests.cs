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
        var result = await new GitHubConfigurationService(repository, new Environment()).SaveAsync(
            new(GitHubEnvironments.Development, "client", null, "read:user", "https://example.test/callback"), CancellationToken.None);
        Assert.Equal(GitHubConfigurationSaveStatus.ClientSecretRequired, result);
        Assert.Null(repository.Saved);
    }

    [Fact]
    public async Task ExistingSecretIsKeptWhenInputIsEmpty()
    {
        var repository = new Repository { Current = new(GitHubEnvironments.Development, "old", true, "read:user", "https://old.test") };
        var result = await new GitHubConfigurationService(repository, new Environment()).SaveAsync(
            new(GitHubEnvironments.Development, "new", " ", "repo read:user", "https://example.test/callback"), CancellationToken.None);
        Assert.Equal(GitHubConfigurationSaveStatus.Succeeded, result);
        Assert.Null(repository.Saved!.ClientSecret);
    }

    [Theory]
    [InlineData("scope<script>", GitHubConfigurationSaveStatus.InvalidScopes)]
    [InlineData("not a url", GitHubConfigurationSaveStatus.InvalidCallbackUrl)]
    public async Task InvalidValuesAreRejected(string value, GitHubConfigurationSaveStatus expected)
    {
        var input = expected == GitHubConfigurationSaveStatus.InvalidScopes
            ? new GitHubConfigurationInput(GitHubEnvironments.Development, "client", "secret", value, "https://example.test")
            : new GitHubConfigurationInput(GitHubEnvironments.Development, "client", "secret", "read:user", value);
        Assert.Equal(expected, await new GitHubConfigurationService(new Repository(), new Environment()).SaveAsync(input, CancellationToken.None));
    }

    [Fact]
    public async Task ArbitraryEnvironmentCannotBeSaved()
    {
        var repository = new Repository();
        var status = await new GitHubConfigurationService(repository, new Environment()).SaveAsync(
            new("Staging", "client", "secret", "read:user", "https://example.test/callback"),
            CancellationToken.None);

        Assert.Equal(GitHubConfigurationSaveStatus.InvalidEnvironment, status);
        Assert.Null(repository.Saved);
    }

    [Theory]
    [InlineData(GitHubEnvironments.Development)]
    [InlineData(GitHubEnvironments.Production)]
    public async Task SaveTargetsOnlySelectedEnvironment(string environmentName)
    {
        var repository = new Repository();
        await new GitHubConfigurationService(repository, new Environment()).SaveAsync(
            new(environmentName, "client", "secret", "read:user", "https://example.test/callback"),
            CancellationToken.None);

        Assert.Equal(environmentName, repository.Saved!.EnvironmentName);
    }

    [Theory]
    [InlineData(GitHubEnvironments.Development)]
    [InlineData(GitHubEnvironments.Production)]
    public async Task CredentialReadUsesCurrentRuntimeEnvironment(string environmentName)
    {
        var repository = new Repository();
        var service = new GitHubConfigurationService(repository, new Environment(environmentName));

        await service.GetCredentialAsync(CancellationToken.None);

        Assert.Equal(environmentName, repository.CredentialEnvironmentName);
    }

    [Fact]
    public async Task CredentialReadPropagatesCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new Repository();

        await new GitHubConfigurationService(repository, new Environment()).GetCredentialAsync(cancellation.Token);

        Assert.Equal(cancellation.Token, repository.CredentialToken);
    }

    [Fact]
    public async Task CredentialRepositoryFailuresAreNotConvertedToMissingConfiguration()
    {
        var expected = new InvalidOperationException("database unavailable");
        var service = new GitHubConfigurationService(new Repository { CredentialFailure = expected }, new Environment());

        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetCredentialAsync(CancellationToken.None)));
    }

    private sealed class Environment(string name = GitHubEnvironments.Development) : IRuntimeEnvironment
    {
        public string Name { get; } = name;
    }

    private sealed class Repository : IGitHubConfigurationRepository
    {
        public GitHubConfiguration? Current { get; init; }
        public GitHubConfigurationInput? Saved { get; private set; }
        public CancellationToken CredentialToken { get; private set; }
        public string? CredentialEnvironmentName { get; private set; }
        public Exception? CredentialFailure { get; init; }
        public Task<GitHubConfiguration?> GetAsync(string environmentName, CancellationToken cancellationToken) => Task.FromResult(Current);
        public Task<GitHubConfigurationCredential?> GetCredentialAsync(string environmentName, CancellationToken cancellationToken)
        {
            CredentialEnvironmentName = environmentName;
            CredentialToken = cancellationToken;
            return CredentialFailure is null
                ? Task.FromResult<GitHubConfigurationCredential?>(null)
                : Task.FromException<GitHubConfigurationCredential?>(CredentialFailure);
        }
        public Task SaveAsync(GitHubConfigurationInput input, CancellationToken cancellationToken) { Saved = input; return Task.CompletedTask; }
    }
}
