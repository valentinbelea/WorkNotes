using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Business.Services;

namespace WorkNotes.Business.Tests;

public sealed class ContextMemberServiceTests
{
    private const string Owner = "owner-1";

    [Fact]
    public async Task OwnerAddsMemberByTrimmedEmailWithMemberRole()
    {
        var members = new StubMembers();
        var service = new ContextMemberService(new StubContexts(isOwner: true), members);

        var status = await service.AddMemberAsync(1, Owner, "  ana@example.com ", CancellationToken.None);

        Assert.Equal(ContextMemberAddStatus.Added, status);
        Assert.Equal(("ana@example.com", ContextRoles.Member), members.Added);
    }

    [Fact]
    public async Task MemberWhoIsNotOwnerCannotAddOrListMembers()
    {
        var members = new StubMembers();
        var service = new ContextMemberService(new StubContexts(isOwner: false), members);

        Assert.Equal(ContextMemberAddStatus.Forbidden, await service.AddMemberAsync(1, Owner, "ana@example.com", CancellationToken.None));
        Assert.Null(await service.GetMembersAsync(1, Owner, CancellationToken.None));
        Assert.False(members.WasCalled);
    }

    [Fact]
    public async Task ContextOutsideMembershipIsNotFound()
    {
        var members = new StubMembers();
        var service = new ContextMemberService(new StubContexts(exists: false), members);

        Assert.Equal(ContextMemberAddStatus.NotFound, await service.AddMemberAsync(1, Owner, "ana@example.com", CancellationToken.None));
        Assert.Null(await service.GetMembersAsync(1, Owner, CancellationToken.None));
        Assert.False(members.WasCalled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task InvalidEmailIsRejectedBeforeDataAccess(string email)
    {
        var contexts = new StubContexts(isOwner: true);
        var members = new StubMembers();
        var service = new ContextMemberService(contexts, members);

        Assert.Equal(ContextMemberAddStatus.InvalidEmail, await service.AddMemberAsync(1, Owner, email, CancellationToken.None));
        Assert.False(contexts.WasCalled);
        Assert.False(members.WasCalled);
    }

    [Theory]
    [InlineData(ContextMemberAddStatus.UserNotFound)]
    [InlineData(ContextMemberAddStatus.AlreadyMember)]
    public async Task RepositoryOutcomeIsReturnedUnchanged(ContextMemberAddStatus outcome)
    {
        var service = new ContextMemberService(new StubContexts(isOwner: true), new StubMembers(outcome));

        Assert.Equal(outcome, await service.AddMemberAsync(1, Owner, "ana@example.com", CancellationToken.None));
    }

    [Fact]
    public async Task OwnerSeesMemberList()
    {
        var service = new ContextMemberService(new StubContexts(isOwner: true), new StubMembers());

        var list = await service.GetMembersAsync(1, Owner, CancellationToken.None);

        Assert.Equal(Owner, Assert.Single(list!).UserId);
    }

    [Fact]
    public async Task OwnerRemovesMember()
    {
        var members = new StubMembers();
        var service = new ContextMemberService(new StubContexts(isOwner: true), members);

        Assert.Equal(ContextMemberRemoveStatus.Removed, await service.RemoveMemberAsync(1, Owner, "user-2", CancellationToken.None));
        Assert.Equal("user-2", members.Removed);
    }

    [Fact]
    public async Task OwnerCannotRemoveThemselves()
    {
        var members = new StubMembers();
        var service = new ContextMemberService(new StubContexts(isOwner: true), members);

        Assert.Equal(ContextMemberRemoveStatus.Forbidden, await service.RemoveMemberAsync(1, Owner, Owner, CancellationToken.None));
        Assert.False(members.WasCalled);
    }

    [Fact]
    public async Task MemberWhoIsNotOwnerCannotRemove()
    {
        var members = new StubMembers();
        var service = new ContextMemberService(new StubContexts(isOwner: false), members);

        Assert.Equal(ContextMemberRemoveStatus.Forbidden, await service.RemoveMemberAsync(1, Owner, "user-2", CancellationToken.None));
        Assert.False(members.WasCalled);
    }

    [Fact]
    public async Task RemovingFromContextOutsideMembershipIsNotFound()
    {
        var members = new StubMembers();
        var service = new ContextMemberService(new StubContexts(exists: false), members);

        Assert.Equal(ContextMemberRemoveStatus.NotFound, await service.RemoveMemberAsync(1, Owner, "user-2", CancellationToken.None));
        Assert.False(members.WasCalled);
    }

    [Theory]
    [InlineData(true, "")]
    [InlineData(false, "user-2")]
    public async Task MissingMembershipIsReported(bool removes, string memberUserId)
    {
        var service = new ContextMemberService(new StubContexts(isOwner: true), new StubMembers(removes: removes));

        Assert.Equal(ContextMemberRemoveStatus.MemberNotFound, await service.RemoveMemberAsync(1, Owner, memberUserId, CancellationToken.None));
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

    private sealed class StubMembers(ContextMemberAddStatus outcome = ContextMemberAddStatus.Added, bool removes = true) : IContextMemberRepository
    {
        public bool WasCalled { get; private set; }
        public string? Removed { get; private set; }

        public Task<bool> RemoveMemberAsync(int contextId, string memberUserId, CancellationToken cancellationToken)
        {
            WasCalled = true;
            Removed = memberUserId;
            return Task.FromResult(removes);
        }
        public (string Email, string Role)? Added { get; private set; }

        public Task<IReadOnlyList<ContextMemberDetails>> GetMembersAsync(int contextId, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult<IReadOnlyList<ContextMemberDetails>>([new(Owner, "Ana", "Pop", "ana@example.com", ContextRoles.Owner)]);
        }

        public Task<ContextMemberAddStatus> AddByEmailAsync(int contextId, string email, string role, CancellationToken cancellationToken)
        {
            WasCalled = true;
            Added = (email, role);
            return Task.FromResult(outcome);
        }
    }
}
