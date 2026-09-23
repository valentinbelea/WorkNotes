using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface IAccountService
{
    Task<AccountResult> RegisterAsync(string firstName, string lastName, string email, string password, CancellationToken cancellationToken);
    Task<AccountProfile?> GetProfileAsync(string userId, CancellationToken cancellationToken);
    Task<AccountResult> UpdateProfileAsync(string userId, string firstName, string lastName, CancellationToken cancellationToken);
    Task<AccountResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken);
}

