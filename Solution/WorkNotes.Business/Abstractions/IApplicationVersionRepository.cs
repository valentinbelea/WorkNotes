namespace WorkNotes.Business.Abstractions;

public interface IApplicationVersionRepository
{
    Task<IReadOnlyList<string>> GetVersionsAsync(CancellationToken cancellationToken = default);
}
