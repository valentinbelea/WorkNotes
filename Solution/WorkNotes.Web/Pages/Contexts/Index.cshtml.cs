using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Web.Pages.Contexts;

[Authorize]
public sealed class IndexModel(IWorkContextService contexts) : PageModel
{
    public IReadOnlyList<WorkContext> Contexts { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Contexts = await contexts.GetAllAsync(cancellationToken);
}
