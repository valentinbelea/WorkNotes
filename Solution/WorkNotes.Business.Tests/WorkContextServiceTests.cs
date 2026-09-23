using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Business.Services;

namespace WorkNotes.Business.Tests;

public sealed class WorkContextServiceTests
{
    private const string Creator = "user-1";

    [Fact]
    public async Task CreateTrimsNameAndDescription()
    {
        var repository = new StubRepository();
        var service = new WorkContextService(repository);

        var status = await service.CreateAsync("  SD Worx ", "  Proiecte client  ", Creator, CancellationToken.None);

        Assert.Equal(WorkContextSaveStatus.Saved, status);
        Assert.Equal("SD Worx", repository.Saved?.Name);
        Assert.Equal("Proiecte client", repository.Saved?.Description);
    }

    [Fact]
    public async Task CreatorBecomesOwner()
    {
        var repository = new StubRepository();
        var service = new WorkContextService(repository);

        await service.CreateAsync("TopDev", null, Creator, CancellationToken.None);

        Assert.Equal(Creator, repository.Owner);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateRequiresCreator(string creator)
    {
        var repository = new StubRepository();
        var service = new WorkContextService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync("TopDev", null, creator, CancellationToken.None));
        Assert.False(repository.WasCalled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankDescriptionIsStoredAsNull(string? description)
    {
        var repository = new StubRepository();
        var service = new WorkContextService(repository);

        await service.CreateAsync("TopDev", description, Creator, CancellationToken.None);

        Assert.Null(repository.Saved?.Description);
    }

    [Fact]
    public async Task DescriptionMayContainLineBreaks()
    {
        var repository = new StubRepository();
        var service = new WorkContextService(repository);

        var status = await service.CreateAsync("TopDev", "Rând 1\r\nRând 2", Creator, CancellationToken.None);

        Assert.Equal(WorkContextSaveStatus.Saved, status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("SD\nWorx")]
    public async Task InvalidNameIsRejectedBeforeDataAccess(string name)
    {
        var repository = new StubRepository();
        var service = new WorkContextService(repository);

        Assert.Equal(WorkContextSaveStatus.InvalidName, await service.CreateAsync(name, null, Creator, CancellationToken.None));
        Assert.Equal(WorkContextSaveStatus.InvalidName, await service.UpdateAsync(1, name, null, CancellationToken.None));
        Assert.False(repository.WasCalled);
    }

    [Fact]
    public async Task NameAndDescriptionMustFitDatabaseColumns()
    {
        var repository = new StubRepository();
        var service = new WorkContextService(repository);

        Assert.Equal(WorkContextSaveStatus.InvalidName,
            await service.CreateAsync(new string('a', WorkContextRules.NameMaxLength + 1), null, Creator, CancellationToken.None));
        Assert.Equal(WorkContextSaveStatus.InvalidDescription,
            await service.CreateAsync("TopDev", new string('a', WorkContextRules.DescriptionMaxLength + 1), Creator, CancellationToken.None));
        Assert.Equal(WorkContextSaveStatus.Saved,
            await service.CreateAsync(new string('a', WorkContextRules.NameMaxLength), null, Creator, CancellationToken.None));
    }

    [Theory]
    [InlineData(WorkContextSaveStatus.DuplicateName)]
    [InlineData(WorkContextSaveStatus.NotFound)]
    public async Task RepositoryOutcomeIsReturnedUnchanged(WorkContextSaveStatus outcome)
    {
        var service = new WorkContextService(new StubRepository(outcome));

        Assert.Equal(outcome, await service.UpdateAsync(7, "TopDev", null, CancellationToken.None));
    }

    [Fact]
    public async Task PassesCancellationTokenToDataAccess()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new StubRepository();
        var service = new WorkContextService(repository);

        await service.GetAllAsync(cancellation.Token);

        Assert.Equal(cancellation.Token, repository.ReceivedToken);
    }

    [Fact]
    public async Task CancelledRequestStopsBeforeDataAccess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var repository = new StubRepository();
        var service = new WorkContextService(repository);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CreateAsync("TopDev", null, Creator, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.DeleteAsync(1, cancellation.Token));
        Assert.False(repository.WasCalled);
    }

    private sealed class StubRepository(WorkContextSaveStatus outcome = WorkContextSaveStatus.Saved) : IWorkContextRepository
    {
        public (string Name, string? Description)? Saved { get; private set; }
        public bool WasCalled { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public Task<IReadOnlyList<WorkContext>> GetAllAsync(CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            return Task.FromResult<IReadOnlyList<WorkContext>>([]);
        }

        public Task<WorkContext?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            return Task.FromResult<WorkContext?>(null);
        }

        public string? Owner { get; private set; }

        public Task<WorkContextSaveStatus> AddAsync(string name, string? description, string ownerUserId, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            Saved = (name, description);
            Owner = ownerUserId;
            return Task.FromResult(outcome);
        }

        public Task<WorkContextSaveStatus> UpdateAsync(int id, string name, string? description, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            Saved = (name, description);
            return Task.FromResult(outcome);
        }

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            return Task.FromResult(true);
        }

        private void Record(CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedToken = cancellationToken;
        }
    }
}
