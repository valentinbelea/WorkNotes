using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Resources.Resources;
using WorkNotes.Web.ViewModels;

namespace WorkNotes.Web.Pages;

// Guests see the welcome panel; signed-in users see one board per context (?context={id}, the first one by default).
// ?new=true opens the new-note card without JavaScript.
public sealed class IndexModel(INoteService notes, IWorkContextService contexts, IStringLocalizer<SharedResources> localizer) : PageModel
{
    public IReadOnlyList<WorkContext> Contexts { get; private set; } = [];
    public WorkContext? SelectedContext { get; private set; }
    // Months newest first; the current month is always present so a new card has a group to go into.
    public IReadOnlyList<NoteMonthGroup> Months { get; private set; } = [];
    public (int Year, int Month) CurrentMonth { get; private set; }
    public NewNoteInput Input { get; set; } = new();
    public bool IsNewNoteOpen { get; private set; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool IsSignedIn => User.Identity?.IsAuthenticated == true;

    public async Task<IActionResult> OnGetAsync([FromQuery(Name = "new")] bool openNewNote, int? context, CancellationToken cancellationToken)
    {
        if (!IsSignedIn) return Page();
        await LoadBoardAsync(context, cancellationToken);
        IsNewNoteOpen = openNewNote && SelectedContext is not null;
        return Page();
    }

    public async Task<IActionResult> OnPostCreateNoteAsync(NewNoteInput input, CancellationToken cancellationToken)
    {
        if (!IsSignedIn) return Challenge();
        Input = input;
        if (ModelState.IsValid)
        {
            switch (await notes.CreateAsync(UserId, input.ContextId, input.NoteType, input.Title, cancellationToken))
            {
                case NoteCreateStatus.Created:
                    TempData["StatusMessage"] = "Message_NoteSaved";
                    return RedirectToPage(new { context = input.ContextId });
                case NoteCreateStatus.InvalidType:
                    ModelState.AddModelError("Input.NoteType", localizer["Validation_InvalidValue"]);
                    break;
                case NoteCreateStatus.InvalidTitle:
                    ModelState.AddModelError("Input.Title", localizer["Validation_InvalidNoteTitle"]);
                    break;
                case NoteCreateStatus.ContextNotFound:
                    ModelState.AddModelError("Input.Title", localizer["Validation_ContextUnavailable"]);
                    break;
            }
        }
        // Errors keep the unsaved card first on the board, with the values the user typed.
        await LoadBoardAsync(input.ContextId, cancellationToken);
        IsNewNoteOpen = SelectedContext is not null;
        return Page();
    }

    // The card posts with ?handler=CreateNote; opening that address directly shows an empty new card.
    public IActionResult OnGetCreateNote(int? context) => RedirectToPage(new { @new = true, context });

    private async Task LoadBoardAsync(int? contextId, CancellationToken cancellationToken)
    {
        Contexts = await contexts.GetForMemberAsync(UserId, cancellationToken);
        // An unknown or foreign id falls back to the first board without revealing anything about it.
        SelectedContext = Contexts.FirstOrDefault(item => item.Id == contextId) ?? Contexts.FirstOrDefault();
        if (SelectedContext is null) return;
        Input.ContextId = SelectedContext.Id;
        CurrentMonth = notes.GetCurrentMonth();
        var months = await notes.GetBoardAsync(UserId, SelectedContext.Id, cancellationToken);
        Months = months.Any(month => (month.Year, month.Month) == CurrentMonth)
            ? months
            : [new NoteMonthGroup(CurrentMonth.Year, CurrentMonth.Month, []), .. months];
    }
}
