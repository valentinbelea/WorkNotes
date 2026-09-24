using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Resources.Resources;
using WorkNotes.Web.Notes;
using WorkNotes.Web.ViewModels;

namespace WorkNotes.Web.Pages;

// Guests see the welcome panel; signed-in users see one board per context (?context={id}, the first one by default).
// ?new=true opens the new-note card without JavaScript; ?note={id} opens the note's editor over its board.
public sealed class IndexModel(INoteService notes, IWorkContextService contexts, IStringLocalizer<SharedResources> localizer) : PageModel
{
    public IReadOnlyList<WorkContext> Contexts { get; private set; } = [];
    public WorkContext? SelectedContext { get; private set; }
    // Months newest first; the current month is always present so a new card has a group to go into.
    public IReadOnlyList<NoteMonthGroup> Months { get; private set; } = [];
    public (int Year, int Month) CurrentMonth { get; private set; }
    public NewNoteInput Input { get; set; } = new();
    public bool IsNewNoteOpen { get; private set; }
    // The note shown in the editor dialog, when ?note={id} names a note the user may see.
    public NoteDocument? OpenNote { get; private set; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool IsSignedIn => User.Identity?.IsAuthenticated == true;

    public async Task<IActionResult> OnGetAsync([FromQuery(Name = "new")] bool openNewNote, int? context, int? note, CancellationToken cancellationToken)
    {
        if (!IsSignedIn) return Page();
        if (note is { } noteId)
        {
            OpenNote = await notes.GetDocumentAsync(noteId, UserId, cancellationToken);
            if (OpenNote is null) return NotFound();
            // The editor opens over the board the note belongs to.
            context = OpenNote.ContextId;
        }
        await LoadBoardAsync(context, cancellationToken);
        IsNewNoteOpen = openNewNote && OpenNote is null && SelectedContext is not null;
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

    // Called by note-editor.js with a JSON body; the antiforgery token travels in the RequestVerificationToken header.
    public async Task<IActionResult> OnPostSaveNoteAsync(int note, [FromBody] NoteSaveRequest? request, CancellationToken cancellationToken)
    {
        if (!IsSignedIn) return EditorFailure(StatusCodes.Status401Unauthorized, "Editor_SessionExpired");
        if (request is null) return EditorFailure(StatusCodes.Status400BadRequest, "Editor_InvalidContent");
        var blocks = (request.Blocks ?? []).Select(block => new NoteBlockInput(block.Id, block.Content)).ToList();
        var result = await notes.SaveAsync(UserId, note, request.Version, request.Title, blocks, cancellationToken);
        return result.Status switch
        {
            // The updated audit texts refresh the editor's info bar without reloading the note.
            NoteSaveStatus.Saved => new JsonResult(new
            {
                version = result.Version,
                blocks = (result.Blocks ?? []).Select(block => new
                {
                    id = block.Id,
                    info = NoteDates.BlockAudit(localizer, block.CreatedAtUtc, block.ModifiedAtUtc),
                    unsavedInfo = NoteDates.BlockAudit(localizer, block.CreatedAtUtc, block.ModifiedAtUtc, unsaved: true)
                })
            }),
            NoteSaveStatus.Conflict => EditorFailure(StatusCodes.Status409Conflict, "Editor_Conflict"),
            NoteSaveStatus.Forbidden => EditorFailure(StatusCodes.Status403Forbidden, "Editor_ReadOnly"),
            NoteSaveStatus.NotFound => EditorFailure(StatusCodes.Status404NotFound, "Editor_NotFound"),
            NoteSaveStatus.InvalidTitle => EditorFailure(StatusCodes.Status400BadRequest, "Validation_InvalidNoteTitle"),
            _ => EditorFailure(StatusCodes.Status400BadRequest, "Editor_InvalidContent")
        };
    }

    // The card posts with ?handler=CreateNote; opening that address directly shows an empty new card.
    public IActionResult OnGetCreateNote(int? context) => RedirectToPage(new { @new = true, context });

    private JsonResult EditorFailure(int statusCode, string messageKey) =>
        new(new { message = localizer[messageKey].Value }) { StatusCode = statusCode };

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
