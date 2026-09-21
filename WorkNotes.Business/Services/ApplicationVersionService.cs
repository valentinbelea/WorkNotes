using WorkNotes.Business.Abstractions;

namespace WorkNotes.Business.Services;

public sealed class ApplicationVersionService(IApplicationVersionRepository repository)
    : IApplicationVersionService
{
    public async Task<string?> GetCurrentVersionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var versions = await repository.GetVersionsAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // Compare numeric versions while preserving the original label for display.
        return versions
            .OrderByDescending(value => Version.TryParse(
                value.StartsWith("v.", StringComparison.OrdinalIgnoreCase) ? value[2..] : value,
                out var parsed) ? parsed : new Version(0, 0))
            .ThenByDescending(value => value, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
