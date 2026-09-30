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
public sealed class IndexModel(IGitRepositoryService repositories, IAccountService accounts) : PageModel
{
    [BindProperty] public List<string> Selected { get; set; } = [];
    public GitRepositorySelection Selection { get; private set; } = new(GitRepositoryStatus.NotConnected, [], false);

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    // Identity keeps email unique. Normalize the claim before using it as the functional identity of this integration;
    // persistence still uses the immutable internal user ID and its foreign key, never the display name.
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (await GetUserEmailAsync(cancellationToken) is null)
        {
            Selection = new GitRepositorySelection(GitRepositoryStatus.EmailRequired, [], false);
            return;
        }

        Selection = await repositories.GetSelectionAsync(UserId, cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (await GetUserEmailAsync(cancellationToken) is null)
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

    // Existing authentication cookies may predate the email claim. The account profile is the authoritative fallback,
    // so a valid database email works immediately without forcing the user to sign out and back in.
    private async Task<string?> GetUserEmailAsync(CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
            email = (await accounts.GetProfileAsync(UserId, cancellationToken))?.Email;
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }
}
