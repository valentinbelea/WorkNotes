using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Resources.Resources;
using WorkNotes.Web.Messages;
using WorkNotes.Web.Notes;
using WorkNotes.Web.ViewModels;

namespace WorkNotes.Web.Pages;

// Guests see the welcome panel; signed-in users see one board per context (?context={id}, the first one by default).
// ?new=true opens the new-note card without JavaScript; ?note={id} opens the note's editor over its board;
// ?delete={id} asks, over the board, to confirm deleting a note.
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
    // The note whose deletion is being confirmed (?delete={id}); only its owner gets there.
    public NoteSummary? DeletingNote { get; private set; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool IsSignedIn => User.Identity?.IsAuthenticated == true;

    public async Task<IActionResult> OnGetAsync([FromQuery(Name = "new")] bool openNewNote, int? context, int? note, int? delete,
        CancellationToken cancellationToken)
    {
        if (!IsSignedIn) return Page();
        if (note is { } noteId)
        {
            OpenNote = await notes.GetDocumentAsync(noteId, UserId, cancellationToken);
            if (OpenNote is null) return NotFound();
            // The editor opens over the board the note belongs to.
            context = OpenNote.ContextId;
        }
        else if (delete is { } deleteId)
        {
            DeletingNote = await notes.GetSummaryAsync(deleteId, UserId, cancellationToken);
            if (DeletingNote is null) return NotFound();
            if (!DeletingNote.IsOwner) return StatusCode(StatusCodes.Status403Forbidden);
            context = DeletingNote.ContextId;
        }
        await LoadBoardAsync(context, cancellationToken);
        IsNewNoteOpen = openNewNote && OpenNote is null && DeletingNote is null && SelectedContext is not null;
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
                    TempData.SetStatusMessage("Message_NoteSaved");
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
                message = localizer["Message_NoteSaved"].Value,
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

    // The title field of a card. With JavaScript it posts in the background and gets JSON back (Accept: application/json);
    // without it, Enter submits the form and the board is shown again with a message.
    public async Task<IActionResult> OnPostRenameNoteAsync(int note, int? context, string? title, CancellationToken cancellationToken)
    {
        var wantsJson = Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
        if (!IsSignedIn) return wantsJson ? EditorFailure(StatusCodes.Status401Unauthorized, "Editor_SessionExpired") : Challenge();
        var result = await notes.RenameAsync(UserId, note, title, cancellationToken);
        var messageKey = result.Status switch
        {
            NoteSaveStatus.Saved => "Message_NoteRenamed",
            NoteSaveStatus.InvalidTitle => "Validation_InvalidNoteTitle",
            NoteSaveStatus.Forbidden => "Editor_ReadOnly",
            _ => "Editor_NotFound"
        };
        if (wantsJson)
        {
            // The card keeps its place until the board is loaded again; it shows the stored title and the new last change.
            return result is { Status: NoteSaveStatus.Saved, Note: { } renamed }
                ? new JsonResult(new
                {
                    title = renamed.Title,
                    modified = new
                    {
                        text = NoteDates.Card(renamed.LastChangedAtUtc),
                        iso = NoteDates.Iso(renamed.LastChangedAtUtc),
                        shown = NoteDates.ShowsModified(renamed)
                    },
                    message = localizer[messageKey].Value
                })
                : EditorFailure(result.Status switch
                {
                    NoteSaveStatus.InvalidTitle => StatusCodes.Status400BadRequest,
                    NoteSaveStatus.Forbidden => StatusCodes.Status403Forbidden,
                    _ => StatusCodes.Status404NotFound
                }, messageKey);
        }
        TempData.SetStatusMessage(messageKey, result.Status == NoteSaveStatus.Saved ? StatusMessageKind.Success : StatusMessageKind.Error);
        return RedirectToPage(new { context });
    }

    public async Task<IActionResult> OnPostDeleteNoteAsync(int note, int? context, CancellationToken cancellationToken)
    {
        if (!IsSignedIn) return Challenge();
        switch (await notes.DeleteAsync(UserId, note, cancellationToken))
        {
            case NoteDeleteStatus.NotFound:
                return NotFound();
            case NoteDeleteStatus.Forbidden:
                return StatusCode(StatusCodes.Status403Forbidden);
        }
        TempData.SetStatusMessage("Message_NoteDeleted");
        return RedirectToPage(new { context });
    }

    // The forms post with ?handler=CreateNote, RenameNote or DeleteNote; opening those addresses directly shows the board.
    public IActionResult OnGetCreateNote(int? context) => RedirectToPage(new { @new = true, context });
    public IActionResult OnGetRenameNote(int? context) => RedirectToPage(new { context });
    public IActionResult OnGetDeleteNote(int? context) => RedirectToPage(new { context });

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
