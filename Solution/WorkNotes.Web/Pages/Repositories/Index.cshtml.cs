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

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Selection = await repositories.GetSelectionAsync(UserId, cancellationToken);

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var (key, kind) = await repositories.SaveSelectionAsync(UserId, Selected, cancellationToken) switch
        {
            GitRepositoryStatus.Succeeded => ("GitHub_RepositoriesSaved", StatusMessageKind.Success),
            GitRepositoryStatus.Conflict => ("GitHub_RepositoriesConflict", StatusMessageKind.Warning),
            GitRepositoryStatus.NotConfigured => ("GitHub_NotConfigured", StatusMessageKind.Error),
            GitRepositoryStatus.NotConnected => ("GitHub_NotConnected", StatusMessageKind.Warning),
            GitRepositoryStatus.ReconnectRequired => ("GitHub_ReconnectRequired", StatusMessageKind.Warning),
            _ => ("GitHub_Unavailable", StatusMessageKind.Error)
        };
        TempData.SetStatusMessage(key, kind);
        return RedirectToPage();
    }
}
