using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using WorkNotes.Resources.Resources;

namespace WorkNotes.Web.Localization;

// Identity's descriptions are localized here; service contracts carry only stable codes.
public sealed class LocalizedIdentityErrorDescriber(IStringLocalizer<SharedResources> localizer) : IdentityErrorDescriber
{
    private IdentityError Error(string key) => new() { Code = key, Description = localizer[key] };
    public override IdentityError DefaultError() => Error("Identity_DefaultError");
    public override IdentityError ConcurrencyFailure() => Error("Identity_ConcurrencyFailure");
    public override IdentityError DuplicateEmail(string email) => Error("Identity_DuplicateEmail");
    public override IdentityError DuplicateUserName(string userName) => DuplicateEmail(userName);
    public override IdentityError InvalidEmail(string? email) => Error("Validation_InvalidEmail");
    public override IdentityError InvalidUserName(string? userName) => InvalidEmail(userName);
    public override IdentityError PasswordMismatch() => Error("Identity_PasswordMismatch");
    public override IdentityError PasswordTooShort(int length) => Error("Validation_PasswordPolicy");
    public override IdentityError PasswordRequiresDigit() => Error("Identity_PasswordRequiresDigit");
    public override IdentityError PasswordRequiresLower() => Error("Identity_PasswordRequiresLower");
    public override IdentityError PasswordRequiresUpper() => Error("Identity_PasswordRequiresUpper");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error("Identity_PasswordRequiresNonAlphanumeric");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error("Validation_PasswordPolicy");
    public override IdentityError InvalidToken() => DefaultError();
    public override IdentityError LoginAlreadyAssociated() => DefaultError();
    public override IdentityError RecoveryCodeRedemptionFailed() => DefaultError();
    public override IdentityError UserAlreadyHasPassword() => DefaultError();
    public override IdentityError UserLockoutNotEnabled() => DefaultError();
    public override IdentityError InvalidRoleName(string? role) => DefaultError();
    public override IdentityError DuplicateRoleName(string role) => DefaultError();
    public override IdentityError UserAlreadyInRole(string role) => DefaultError();
    public override IdentityError UserNotInRole(string role) => DefaultError();
}

