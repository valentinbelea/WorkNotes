using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkNotes.Business.Abstractions;

namespace WorkNotes.Web.Pages.Contexts;

// GET asks for confirmation; only the antiforgery-protected POST deletes.
[Authorize]
public sealed class DeleteModel(IWorkContextService contexts) : PageModel
{
    public string Name { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var context = await contexts.GetByIdAsync(id, cancellationToken);
        if (context is null) return NotFound();
        Name = context.Name;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        if (!await contexts.DeleteAsync(id, cancellationToken)) return NotFound();
        TempData["StatusMessage"] = "Message_ContextDeleted";
        return RedirectToPage("Index");
    }
}
