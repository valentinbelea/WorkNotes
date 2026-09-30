using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface IAdminAuthenticationService
{
    Task<AdminAuthenticationResult?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken);
}
