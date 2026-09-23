using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Resources.Resources;
using WorkNotes.Web.ViewModels;

namespace WorkNotes.Web.Pages.Contexts;

// Lists contexts; ?add=true or ?edit={id} opens the add/edit form in an overlay on the same page.
[Authorize]
public sealed class IndexModel(IWorkContextService contexts, IStringLocalizer<SharedResources> localizer) : PageModel
{
    public IReadOnlyList<WorkContext> Contexts { get; private set; } = [];
    [BindProperty] public WorkContextInput Input { get; set; } = new();
    public bool IsEditorOpen { get; private set; }
    public int? EditingId { get; private set; }

    public async Task<IActionResult> OnGetAsync(bool add, int? edit, CancellationToken cancellationToken)
    {
        if (edit is { } contextId)
        {
            var context = await contexts.GetByIdAsync(contextId, cancellationToken);
            if (context is null) return NotFound();
            Input = new() { Name = context.Name, Description = context.Description };
            OpenEditor(contextId);
        }
        else if (add)
        {
            OpenEditor(null);
        }
        Contexts = await contexts.GetAllAsync(cancellationToken);
        return Page();
    }

    // The form posts back to the URL that opened it, so a refresh after a validation error reopens the same form.
    public async Task<IActionResult> OnPostAsync(int? edit, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var status = edit is { } contextId
                ? await contexts.UpdateAsync(contextId, Input.Name, Input.Description, cancellationToken)
                : await contexts.CreateAsync(Input.Name, Input.Description, cancellationToken);
            switch (status)
            {
                case WorkContextSaveStatus.Saved:
                    TempData["StatusMessage"] = "Message_ContextSaved";
                    return RedirectToPage();
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
        }
        // Validation errors keep the overlay open over the refreshed list.
        OpenEditor(edit);
        Contexts = await contexts.GetAllAsync(cancellationToken);
        return Page();
    }

    private void OpenEditor(int? id)
    {
        IsEditorOpen = true;
        EditingId = id;
    }
}
