using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Resources.Resources;
using WorkNotes.Web.ViewModels;

namespace WorkNotes.Web.Pages.Contexts;

// Creates a context when no id is given; otherwise edits the existing one.
[Authorize]
public sealed class EditModel(IWorkContextService contexts, IStringLocalizer<SharedResources> localizer) : PageModel
{
    [BindProperty] public WorkContextInput Input { get; set; } = new();
    public bool IsNew { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken cancellationToken)
    {
        IsNew = id is null;
        if (id is not { } contextId) return Page();
        var context = await contexts.GetByIdAsync(contextId, cancellationToken);
        if (context is null) return NotFound();
        Input = new() { Name = context.Name, Description = context.Description };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken cancellationToken)
    {
        IsNew = id is null;
        if (!ModelState.IsValid) return Page();
        var status = id is { } contextId
            ? await contexts.UpdateAsync(contextId, Input.Name, Input.Description, cancellationToken)
            : await contexts.CreateAsync(Input.Name, Input.Description, cancellationToken);
        switch (status)
        {
            case WorkContextSaveStatus.Saved:
                TempData["StatusMessage"] = "Message_ContextSaved";
                return RedirectToPage("Index");
            case WorkContextSaveStatus.NotFound:
                return NotFound();
            case WorkContextSaveStatus.DuplicateName:
                ModelState.AddModelError("Input.Name", localizer["Validation_ContextNameTaken"]);
                break;
            case WorkContextSaveStatus.InvalidName:
                ModelState.AddModelError("Input.Name", localizer["Validation_InvalidContextName"]);
                break;
            case WorkContextSaveStatus.InvalidDescription:
                ModelState.AddModelError("Input.Description", localizer["Validation_InvalidDescription"]);
                break;
        }
        return Page();
    }
}
