using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Resources.Resources;
using WorkNotes.Web.ViewModels;

namespace WorkNotes.Web.Pages;

// Guests see the welcome panel; signed-in users see their board. ?new=true opens the new-note card without JavaScript.
public sealed class IndexModel(INoteService notes, IWorkContextService contexts, IStringLocalizer<SharedResources> localizer) : PageModel
{
    public IReadOnlyList<NoteSummary> Notes { get; private set; } = [];
    public IReadOnlyList<WorkContext> Contexts { get; private set; } = [];
    public NewNoteInput Input { get; set; } = new();
    public bool IsNewNoteOpen { get; private set; }
    public NewNoteCardViewModel NewNoteCard => new(Input, Contexts);

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool IsSignedIn => User.Identity?.IsAuthenticated == true;

    public async Task<IActionResult> OnGetAsync([FromQuery(Name = "new")] bool openNewNote, CancellationToken cancellationToken)
    {
        if (!IsSignedIn) return Page();
        await LoadBoardAsync(cancellationToken);
        IsNewNoteOpen = openNewNote && Contexts.Count > 0;
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
                    return RedirectToPage();
                case NoteCreateStatus.InvalidType:
                    ModelState.AddModelError("Input.NoteType", localizer["Validation_InvalidValue"]);
                    break;
                case NoteCreateStatus.InvalidTitle:
                    ModelState.AddModelError("Input.Title", localizer["Validation_InvalidNoteTitle"]);
                    break;
                case NoteCreateStatus.ContextNotFound:
                    ModelState.AddModelError("Input.ContextId", localizer["Validation_ContextUnavailable"]);
                    break;
                case NoteCreateStatus.JournalExists:
                    ModelState.AddModelError("Input.NoteType", localizer["Validation_JournalExists"]);
                    break;
            }
        }
        // Errors keep the unsaved card first on the board, with the values the user typed.
        await LoadBoardAsync(cancellationToken);
        IsNewNoteOpen = true;
        return Page();
    }

    // The card posts with ?handler=CreateNote; opening that address directly shows an empty new card.
    public IActionResult OnGetCreateNote() => RedirectToPage(new { @new = true });

    private async Task LoadBoardAsync(CancellationToken cancellationToken)
    {
        Contexts = await contexts.GetForMemberAsync(UserId, cancellationToken);
        Notes = await notes.GetBoardAsync(UserId, cancellationToken);
        if (Input.ContextId == 0 && Contexts.Count > 0) Input.ContextId = Contexts[0].Id;
    }
}
