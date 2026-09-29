using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Services;

namespace WorkNotes.Business.Tests;

public sealed class ReferenceTypeServiceTests
{
    [Fact]
    public async Task TheActiveTypesAreThePrefixesOfTheParser()
    {
        var repository = new StubRepository(["BUG", "CR", "TASK"]);

        var parser = await new ReferenceTypeService(repository, new ReferenceTypeCache(), new ManualTime()).GetParserAsync(CancellationToken.None);

        Assert.Equal(["BUG", "CR", "TASK"], parser.Types);
        Assert.Equal(["TASK:12"], parser.Find("Vezi task-12").Select(match => match.NormalizedReference));
    }

    [Fact]
    public async Task EveryRequestUsesTheTypesReadOnceForTheApplication()
    {
        var repository = new StubRepository(["CR"]);
        var cache = new ReferenceTypeCache();
        var time = new ManualTime();

        var first = await new ReferenceTypeService(repository, cache, time).GetParserAsync(CancellationToken.None);
        time.Advance(ReferenceTypeCache.Duration - TimeSpan.FromSeconds(1));
        var second = await new ReferenceTypeService(repository, cache, time).GetParserAsync(CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal(1, repository.Reads);
    }

    [Fact]
    public async Task TheTypesAreReadAgainOnceTheirTimeHasPassed()
    {
        var repository = new StubRepository(["CR"]);
        var cache = new ReferenceTypeCache();
        var time = new ManualTime();
        await new ReferenceTypeService(repository, cache, time).GetParserAsync(CancellationToken.None);

        // BUG was activated meanwhile: the next request after the time has passed reads it.
        repository.Types = ["BUG", "CR"];
        time.Advance(ReferenceTypeCache.Duration);
        var parser = await new ReferenceTypeService(repository, cache, time).GetParserAsync(CancellationToken.None);

        Assert.Equal(["BUG", "CR"], parser.Types);
        Assert.Equal(2, repository.Reads);
        Assert.Same(parser, cache.Current!.Parser);
    }

    [Fact]
    public async Task OneRequestKeepsTheSameTypesToTheEnd()
    {
        var repository = new StubRepository(["CR"]);
        var cache = new ReferenceTypeCache();
        var time = new ManualTime();
        var service = new ReferenceTypeService(repository, cache, time);

        var first = await service.GetParserAsync(CancellationToken.None);
        repository.Types = ["BUG", "CR"];
        time.Advance(ReferenceTypeCache.Duration * 2);

        Assert.Same(first, await service.GetParserAsync(CancellationToken.None));
        Assert.Equal(1, repository.Reads);
    }

    [Fact]
    public async Task NoActiveTypeMeansNoReference()
    {
        var parser = await new ReferenceTypeService(new StubRepository([]), new ReferenceTypeCache(), new ManualTime())
            .GetParserAsync(CancellationToken.None);

        Assert.Empty(parser.Find("CR 30080 și bug 1234"));
    }

    [Fact]
    public async Task AFailedReadIsNoParserAndIsReadAgainByTheNextRequest()
    {
        var repository = new StubRepository(["CR"]) { Failure = new InvalidOperationException("database unavailable") };
        var cache = new ReferenceTypeCache();
        var time = new ManualTime();

        // A database error is not an empty set of types.
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ReferenceTypeService(repository, cache, time).GetParserAsync(CancellationToken.None));
        Assert.Null(cache.Current);

        repository.Failure = null;
        Assert.Equal(["CR"], (await new ReferenceTypeService(repository, cache, time).GetParserAsync(CancellationToken.None)).Types);
        Assert.Equal(2, repository.Reads);
    }

    [Fact]
    public async Task ReadingStopsBeforeTheDataAccessWhenCancelledAndPassesTheToken()
    {
        var repository = new StubRepository(["CR"]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ReferenceTypeService(repository, new ReferenceTypeCache(), new ManualTime()).GetParserAsync(cancellation.Token));
        Assert.Equal(0, repository.Reads);

        using var active = new CancellationTokenSource();
        await new ReferenceTypeService(repository, new ReferenceTypeCache(), new ManualTime()).GetParserAsync(active.Token);
        Assert.Equal(active.Token, repository.ReceivedToken);
    }

    // A clock that moves only when told; timestamps are ticks.
    private sealed class ManualTime : TimeProvider
    {
        private long timestamp = 1_000;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public void Advance(TimeSpan time) => timestamp += time.Ticks;
    }

    private sealed class StubRepository(IReadOnlyList<string> types) : IReferenceTypeRepository
    {
        public IReadOnlyList<string> Types { get; set; } = types;
        public Exception? Failure { get; set; }
        public int Reads { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public Task<IReadOnlyList<string>> GetActiveTypesAsync(CancellationToken cancellationToken)
        {
            Reads++;
            ReceivedToken = cancellationToken;
            return Failure is null ? Task.FromResult(Types) : Task.FromException<IReadOnlyList<string>>(Failure);
        }
    }
}
