namespace WorkNotes.Business.Models;

// A provider answer: Value is set exactly when Status is Succeeded.
public sealed record GitProviderResult<T>(GitProviderStatus Status, T? Value) where T : class
{
    public static GitProviderResult<T> Succeeded(T value) => new(GitProviderStatus.Succeeded, value);
    public static GitProviderResult<T> Rejected { get; } = new(GitProviderStatus.Rejected, null);
    public static GitProviderResult<T> Unavailable { get; } = new(GitProviderStatus.Unavailable, null);
}
