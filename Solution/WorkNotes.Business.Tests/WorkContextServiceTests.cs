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

    [Fact]
    public async Task ReadsAndChangesAreScopedToTheCurrentMember()
    {
        var repository = new StubRepository();
        var service = new WorkContextService(repository);

        await service.GetForMemberAsync(Creator, CancellationToken.None);
        Assert.Equal(Creator, repository.Member);
        await service.GetByIdAsync(3, "user-2", CancellationToken.None);
        Assert.Equal("user-2", repository.Member);
        await service.UpdateAsync(3, "user-3", "TopDev", null, CancellationToken.None);
        Assert.Equal("user-3", repository.Member);
        await service.DeleteAsync(3, "user-4", CancellationToken.None);
        Assert.Equal("user-4", repository.Member);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MemberIsRequiredBeforeDataAccess(string userId)
    {
        var repository = new StubRepository();
        var service = new WorkContextService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetForMemberAsync(userId, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetByIdAsync(1, userId, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(1, userId, "TopDev", null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteAsync(1, userId, CancellationToken.None));
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
        Assert.Equal(WorkContextSaveStatus.InvalidName, await service.UpdateAsync(1, Creator, name, null, CancellationToken.None));
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

        Assert.Equal(outcome, await service.UpdateAsync(7, Creator, "TopDev", null, CancellationToken.None));
    }

    [Fact]
    public async Task OwnerCanEditAndDelete()
    {
        var repository = new StubRepository();
        var service = new WorkContextService(repository);

        Assert.Equal(WorkContextSaveStatus.Saved, await service.UpdateAsync(1, Creator, "TopDev", null, CancellationToken.None));
        Assert.Equal(WorkContextDeleteStatus.Deleted, await service.DeleteAsync(1, Creator, CancellationToken.None));
    }

    [Fact]
    public async Task MemberWhoIsNotOwnerCannotEditOrDelete()
    {
        var repository = new StubRepository(isOwner: false);
        var service = new WorkContextService(repository);

        Assert.Equal(WorkContextSaveStatus.Forbidden, await service.UpdateAsync(1, Creator, "TopDev", null, CancellationToken.None));
        Assert.Equal(WorkContextDeleteStatus.Forbidden, await service.DeleteAsync(1, Creator, CancellationToken.None));
        Assert.False(repository.Changed);
    }

    [Fact]
    public async Task EditOrDeleteOfContextOutsideMembershipIsNotFound()
    {
        var repository = new StubRepository(exists: false);
        var service = new WorkContextService(repository);

        Assert.Equal(WorkContextSaveStatus.NotFound, await service.UpdateAsync(1, Creator, "TopDev", null, CancellationToken.None));
        Assert.Equal(WorkContextDeleteStatus.NotFound, await service.DeleteAsync(1, Creator, CancellationToken.None));
        Assert.False(repository.Changed);
    }

    [Fact]
    public async Task PassesCancellationTokenToDataAccess()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new StubRepository();
        var service = new WorkContextService(repository);

        await service.GetForMemberAsync(Creator, cancellation.Token);

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
            () => service.DeleteAsync(1, Creator, cancellation.Token));
        Assert.False(repository.WasCalled);
    }

    private sealed class StubRepository(WorkContextSaveStatus outcome = WorkContextSaveStatus.Saved, bool exists = true, bool isOwner = true) : IWorkContextRepository
    {
        // By default the requested context exists and the current user owns it.
        private readonly WorkContext? existing = exists ? new WorkContext(1, "TopDev", null, isOwner) : null;
        public bool Changed { get; private set; }

        public (string Name, string? Description)? Saved { get; private set; }
        public bool WasCalled { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public string? Member { get; private set; }

        public Task<IReadOnlyList<WorkContext>> GetForMemberAsync(string userId, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            Member = userId;
            return Task.FromResult<IReadOnlyList<WorkContext>>([]);
        }

        public Task<WorkContext?> GetByIdAsync(int id, string userId, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            Member = userId;
            return Task.FromResult(existing);
        }

        public string? Owner { get; private set; }

        public Task<WorkContextSaveStatus> AddAsync(string name, string? description, string ownerUserId, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            Saved = (name, description);
            Owner = ownerUserId;
            return Task.FromResult(outcome);
        }

        public Task<WorkContextSaveStatus> UpdateAsync(int id, string userId, string name, string? description, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            Member = userId;
            Saved = (name, description);
            Changed = true;
            return Task.FromResult(outcome);
        }

        public Task<WorkContextDeleteStatus> DeleteAsync(int id, string userId, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            Member = userId;
            Changed = true;
            return Task.FromResult(WorkContextDeleteStatus.Deleted);
        }

        private void Record(CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedToken = cancellationToken;
        }
    }
}
