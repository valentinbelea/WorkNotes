namespace WorkNotes.Business.Models;

// The limits of an imported repository (the columns of dbo.GitRepositories) and of the list read from the provider.
public static class GitRepositoryRules
{
    public const int IdMaxLength = 50;
    public const int FullNameMaxLength = 200;
    public const int DescriptionMaxLength = 400;
    public const int DefaultBranchMaxLength = 255;
    public const int HtmlUrlMaxLength = 300;

    // At most 10 pages of 100: the list stays one page, read in a few seconds.
    public const int PageSize = 100;
    public const int MaxListed = 1000;

    // A provider ID is digits only (GitHub's numeric ids); anything else from a form is ignored.
    public static bool ValidId(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= IdMaxLength && id.All(char.IsAsciiDigit);

    // Null when the repository cannot be stored: no ID, no name, a name or a URL that does not fit, or a URL that is not
    // an absolute https address. The description is trimmed, its control characters become spaces and it is cut to
    // the column; a branch that does not fit is left out.
    public static GitRepositoryInfo? Normalize(GitRepositoryInfo repository)
    {
        if (!ValidId(repository.Id) || !ValidText(repository.FullName, FullNameMaxLength)
            || repository.HtmlUrl.Length > HtmlUrlMaxLength
            || !Uri.TryCreate(repository.HtmlUrl, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
            return null;

        var description = repository.Description is null ? null
            : new string(repository.Description.Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
        if (description?.Length > DescriptionMaxLength) description = description[..DescriptionMaxLength].TrimEnd();
        var branch = ValidText(repository.DefaultBranch, DefaultBranchMaxLength) ? repository.DefaultBranch : null;

        return repository with
        {
            FullName = repository.FullName.Trim(),
            Description = string.IsNullOrEmpty(description) ? null : description,
            DefaultBranch = branch
        };
    }

    private static bool ValidText(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength && !value.Any(char.IsControl);
}
