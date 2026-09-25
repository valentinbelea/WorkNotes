using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.DataAccess.Entities;

namespace WorkNotes.DataAccess.Identity;

public sealed class IdentityAccountService(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
    : IAccountService, IAuthenticationService
{
    public async Task<AccountResult> RegisterAsync(string firstName, string lastName, string email, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!AccountRules.ValidName(firstName) || !AccountRules.ValidName(lastName))
            return AccountResult.Failure("Validation_InvalidName");
        if (!AccountRules.ValidEmail(email))
            return AccountResult.Failure("Validation_InvalidEmail");
        var user = new ApplicationUser { FirstName = firstName.Trim(), LastName = lastName.Trim(), Email = email.Trim(), UserName = email.Trim() };
        try
        {
            return Result(await users.CreateAsync(user, password));
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // The unique database indexes also protect concurrent registrations.
            return AccountResult.Failure("Identity_DuplicateEmail");
        }
    }

    public async Task<bool> SignInAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!AccountRules.ValidEmail(email)) return false;
        var result = await signIn.PasswordSignInAsync(email.Trim(), password, rememberMe, lockoutOnFailure: true);
        return result.Succeeded;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await signIn.SignOutAsync();
    }

    public async Task<AccountProfile?> GetProfileAsync(string userId, CancellationToken cancellationToken) =>
        await users.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new AccountProfile(u.Id, u.FirstName, u.LastName, u.Email!))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AccountResult> UpdateProfileAsync(string userId, string firstName, string lastName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!AccountRules.ValidName(firstName) || !AccountRules.ValidName(lastName))
            return AccountResult.Failure("Validation_InvalidName");
        var user = await users.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return AccountResult.Failure("Message_AccountUnavailable");
        user.FirstName = firstName.Trim();
        user.LastName = lastName.Trim();
        var result = await users.UpdateAsync(user);
        if (result.Succeeded) await signIn.RefreshSignInAsync(user);
        return Result(result);
    }

    public async Task<AccountResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await users.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return AccountResult.Failure("Message_AccountUnavailable");
        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Succeeded) await signIn.RefreshSignInAsync(user);
        return Result(result);
    }

    private static AccountResult Result(IdentityResult result) =>
        result.Succeeded ? AccountResult.Success() : AccountResult.Failure(result.Errors.Select(e => e.Code).ToArray());
}

