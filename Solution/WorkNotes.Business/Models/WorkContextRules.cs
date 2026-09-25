namespace WorkNotes.Business.Models;

public static class WorkContextRules
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 1000;

    public static bool ValidName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= NameMaxLength
        && !value.Any(char.IsControl);

    // Descriptions may span several lines; other control characters are rejected.
    public static bool ValidDescription(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || (value.Trim().Length <= DescriptionMaxLength
            && !value.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')));

    public static string? NormalizeDescription(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
