using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Services;

namespace WorkNotes.Business.Tests;

public sealed class ApplicationVersionServiceTests
{
    [Fact]
    public async Task EmptyTableReturnsNull()
    {
        var service = new ApplicationVersionService(new StubRepository([]));

        Assert.Null(await service.GetCurrentVersionAsync());
    }

    [Theory]
    [InlineData("v.0.9", "v.0.10", "v.0.10")]
    [InlineData("v.0.01", "v.0.0", "v.0.01")]
    [InlineData("v.2.2.13.3", "v.2.2.13.4", "v.2.2.13.4")]
    [InlineData("legacy-label", "v.0.01", "v.0.01")]
    public async Task SelectsHighestNumericVersionAndPreservesLabel(string first, string second, string expected)
    {
        var service = new ApplicationVersionService(new StubRepository([first, second]));

        Assert.Equal(expected, await service.GetCurrentVersionAsync());
    }

    [Fact]
    public async Task PassesCancellationTokenToDataAccess()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new StubRepository(["v.0.01"]);
        var service = new ApplicationVersionService(repository);

        await service.GetCurrentVersionAsync(cancellation.Token);

        Assert.Equal(cancellation.Token, repository.ReceivedToken);
    }

    [Fact]
    public async Task CancelledRequestStopsBeforeDataAccess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var repository = new StubRepository(["v.0.01"]);
        var service = new ApplicationVersionService(repository);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetCurrentVersionAsync(cancellation.Token));
        Assert.False(repository.WasCalled);
    }

    [Fact]
    public async Task DatabaseFailureIsNotReportedAsAnEmptyTable()
    {
        var expected = new InvalidOperationException("Database unavailable.");
        var service = new ApplicationVersionService(new StubRepository([], expected));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetCurrentVersionAsync());

        Assert.Same(expected, actual);
    }

    private sealed class StubRepository(IReadOnlyList<string> versions, Exception? error = null)
        : IApplicationVersionRepository
    {
        public CancellationToken ReceivedToken { get; private set; }
        public bool WasCalled { get; private set; }

        public Task<IReadOnlyList<string>> GetVersionsAsync(CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            WasCalled = true;
            return error is null
                ? Task.FromResult(versions)
                : Task.FromException<IReadOnlyList<string>>(error);
        }
    }
}
