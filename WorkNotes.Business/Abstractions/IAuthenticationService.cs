namespace WorkNotes.Business.Abstractions;

public interface IAuthenticationService
{
    Task<bool> SignInAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken);
    Task SignOutAsync(CancellationToken cancellationToken);
}

