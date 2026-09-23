using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Resources.Resources;
using WorkNotes.Web.ViewModels;

namespace WorkNotes.Web.Pages.Contexts;

// Lists contexts; ?add=true, ?edit={id} or ?delete={id} opens the form or the delete confirmation in an overlay on the same page.
[Authorize]
public sealed class IndexModel(IWorkContextService contexts, IStringLocalizer<SharedResources> localizer) : PageModel
{
    public IReadOnlyList<WorkContext> Contexts { get; private set; } = [];
    [BindProperty] public WorkContextInput Input { get; set; } = new();
    public bool IsEditorOpen { get; private set; }
    public int? EditingId { get; private set; }
    public WorkContext? DeletingContext { get; private set; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> OnGetAsync(bool add, int? edit, int? delete, CancellationToken cancellationToken)
    {
        if (delete is { } deleteId)
        {
            DeletingContext = await contexts.GetByIdAsync(deleteId, UserId, cancellationToken);
            if (DeletingContext is null) return NotFound();
            if (!DeletingContext.IsOwner) return Forbidden();
        }
        else if (edit is { } contextId)
        {
            var context = await contexts.GetByIdAsync(contextId, UserId, cancellationToken);
            if (context is null) return NotFound();
            if (!context.IsOwner) return Forbidden();
            Input = new() { Name = context.Name, Description = context.Description };
            OpenEditor(contextId);
        }
        else if (add)
        {
            OpenEditor(null);
        }
        Contexts = await contexts.GetForMemberAsync(UserId, cancellationToken);
        return Page();
    }

    // The form posts back to the URL that opened it, so a refresh after a validation error reopens the same form.
    public async Task<IActionResult> OnPostAsync(int? edit, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var status = edit is { } contextId
                ? await contexts.UpdateAsync(contextId, UserId, Input.Name, Input.Description, cancellationToken)
                : await contexts.CreateAsync(Input.Name, Input.Description, UserId, cancellationToken);
            switch (status)
            {
                case WorkContextSaveStatus.Saved:
                    TempData["StatusMessage"] = "Message_ContextSaved";
                    return RedirectToPage();
                case WorkContextSaveStatus.NotFound:
                    return NotFound();
                case WorkContextSaveStatus.Forbidden:
                    return Forbidden();
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
        Contexts = await contexts.GetForMemberAsync(UserId, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int delete, CancellationToken cancellationToken)
    {
        switch (await contexts.DeleteAsync(delete, UserId, cancellationToken))
        {
            case WorkContextDeleteStatus.NotFound:
                return NotFound();
            case WorkContextDeleteStatus.Forbidden:
                return Forbidden();
        }
        TempData["StatusMessage"] = "Message_ContextDeleted";
        return RedirectToPage();
    }

    // A member who is not the Owner may see the context but not change it. Forbid() would redirect to the login page.
    private StatusCodeResult Forbidden() => StatusCode(StatusCodes.Status403Forbidden);

    private void OpenEditor(int? id)
    {
        IsEditorOpen = true;
        EditingId = id;
    }
}
