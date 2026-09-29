namespace WorkNotes.Business.Models;

// A repository as the provider describes it. Id is the provider's stable ID (GitHub's numeric id), which survives a
// rename or a transfer; FullName is owner/name at the time it was read.
public sealed record GitRepositoryInfo(
    string Id,
    string FullName,
    string? Description,
    bool IsPrivate,
    string? DefaultBranch,
    string HtmlUrl);
