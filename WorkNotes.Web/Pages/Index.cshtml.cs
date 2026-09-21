using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkNotes.Business.Abstractions;

namespace WorkNotes.Web.Pages;

public sealed class IndexModel(IApplicationVersionService versionService) : PageModel
{
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var currentVersion = await versionService.GetCurrentVersionAsync(cancellationToken);
        ViewData["ApplicationVersion"] = currentVersion ?? "Versiune neconfigurată";
    }
}
