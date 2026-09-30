using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Web.Messages;

namespace WorkNotes.Web.Pages.Repositories;

// The import of GitHub repositories: every repository GitHub lists for the connected account, with a check box each;
// saving makes the checked ones the user's imported repositories.
[Authorize]
public sealed class IndexModel(IGitRepositoryService repositories) : PageModel
{
    [BindProperty] public List<string> Selected { get; set; } = [];
    public GitRepositorySelection Selection { get; private set; } = new(GitRepositoryStatus.NotConnected, [], false);

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    // Identity keeps email unique. Normalize the claim before using it as the functional identity of this integration;
    // persistence still uses the immutable internal user ID and its foreign key, never the display name.
    private string? UserEmail => User.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant() is { Length: > 0 } email
        ? email
        : null;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (UserEmail is null)
        {
            Selection = new GitRepositorySelection(GitRepositoryStatus.EmailRequired, [], false);
            return;
        }

        Selection = await repositories.GetSelectionAsync(UserId, cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (UserEmail is null)
        {
            TempData.SetStatusMessage("GitHub_EmailRequired", StatusMessageKind.Error);
            return RedirectToPage();
        }

        var (key, kind) = await repositories.SaveSelectionAsync(UserId, Selected, cancellationToken) switch
        {
            GitRepositoryStatus.Succeeded => ("GitHub_RepositoriesSaved", StatusMessageKind.Success),
            GitRepositoryStatus.Conflict => ("GitHub_RepositoriesConflict", StatusMessageKind.Warning),
            GitRepositoryStatus.EmailRequired => ("GitHub_EmailRequired", StatusMessageKind.Error),
            GitRepositoryStatus.NotConfigured => ("GitHub_NotConfigured", StatusMessageKind.Error),
            GitRepositoryStatus.NotConnected => ("GitHub_NotConnected", StatusMessageKind.Warning),
            GitRepositoryStatus.ReconnectRequired => ("GitHub_ReconnectRequired", StatusMessageKind.Warning),
            _ => ("GitHub_Unavailable", StatusMessageKind.Error)
        };
        TempData.SetStatusMessage(key, kind);
        return RedirectToPage();
    }
}
