using System.ComponentModel.DataAnnotations;

namespace WorkNotes.Business.Models;

public static class AccountRules
{
    public const int NameMaxLength = 100;
    public const int PasswordMinLength = 12;
    public const string PasswordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^a-zA-Z0-9]).{12,}$";

    public static bool ValidName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= NameMaxLength
        && !value.Any(char.IsControl);

    public static bool ValidEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 256
        && new EmailAddressAttribute().IsValid(value.Trim());
}

