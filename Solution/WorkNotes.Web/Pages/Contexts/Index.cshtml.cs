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

// Lists contexts; ?add=true, ?edit={id}, ?delete={id} or ?members={id} opens the matching form in an overlay on the same page.
[Authorize]
public sealed class IndexModel(IWorkContextService contexts, IContextMemberService memberService, IStringLocalizer<SharedResources> localizer) : PageModel
{
    public IReadOnlyList<WorkContext> Contexts { get; private set; } = [];
    // Each form binds its own input as a handler parameter, so one form's validation never affects the other.
    public WorkContextInput Input { get; set; } = new();
    public ContextMemberInput MemberInput { get; set; } = new();
    public bool IsEditorOpen { get; private set; }
    public int? EditingId { get; private set; }
    public WorkContext? DeletingContext { get; private set; }
    public WorkContext? MembersContext { get; private set; }
    public IReadOnlyList<ContextMemberDetails> Members { get; private set; } = [];

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> OnGetAsync(bool add, int? edit, int? delete, int? members, CancellationToken cancellationToken)
    {
        if (members is { } membersId)
        {
            if (await LoadMembersAsync(membersId, cancellationToken) is { } error) return error;
        }
        else if (delete is { } deleteId)
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
    public async Task<IActionResult> OnPostAsync(int? edit, WorkContextInput input, CancellationToken cancellationToken)
    {
        Input = input;
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

    public async Task<IActionResult> OnPostAddMemberAsync(int members, ContextMemberInput memberInput, CancellationToken cancellationToken)
    {
        MemberInput = memberInput;
        if (ModelState.IsValid)
        {
            switch (await memberService.AddMemberAsync(members, UserId, memberInput.Email, cancellationToken))
            {
                case ContextMemberAddStatus.Added:
                    TempData["MembersMessage"] = "Message_MemberAdded";
                    return RedirectToPage(new { members });
                case ContextMemberAddStatus.NotFound:
                    return NotFound();
                case ContextMemberAddStatus.Forbidden:
                    return Forbidden();
                case ContextMemberAddStatus.InvalidEmail:
                    ModelState.AddModelError("MemberInput.Email", localizer["Validation_InvalidEmail"]);
                    break;
                case ContextMemberAddStatus.UserNotFound:
                    ModelState.AddModelError("MemberInput.Email", localizer["Validation_UserNotFound"]);
                    break;
                case ContextMemberAddStatus.AlreadyMember:
                    ModelState.AddModelError("MemberInput.Email", localizer["Validation_AlreadyMember"]);
                    break;
            }
        }
        if (await LoadMembersAsync(members, cancellationToken) is { } error) return error;
        Contexts = await contexts.GetForMemberAsync(UserId, cancellationToken);
        return Page();
    }

    // The add-member form posts with ?handler=AddMember; opening that address directly shows the member list.
    public IActionResult OnGetAddMember(int members) => RedirectToPage(new { members });

    private async Task<IActionResult?> LoadMembersAsync(int contextId, CancellationToken cancellationToken)
    {
        MembersContext = await contexts.GetByIdAsync(contextId, UserId, cancellationToken);
        if (MembersContext is null) return NotFound();
        if (!MembersContext.IsOwner) return Forbidden();
        Members = await memberService.GetMembersAsync(contextId, UserId, cancellationToken) ?? [];
        return null;
    }

    // A member who is not the Owner may see the context but not change it. Forbid() would redirect to the login page.
    private StatusCodeResult Forbidden() => StatusCode(StatusCodes.Status403Forbidden);

    private void OpenEditor(int? id)
    {
        IsEditorOpen = true;
        EditingId = id;
    }
}
