namespace WorkNotes.Business.Abstractions;

public interface IAdminBootstrapService
{
    Task CreateIfMissingAsync(string userName, string password, CancellationToken cancellationToken);
}
