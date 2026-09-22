namespace WorkNotes.Business.Abstractions;

public interface IApplicationVersionService
{
    Task<string?> GetCurrentVersionAsync(CancellationToken cancellationToken = default);
}
