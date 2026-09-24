using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Resources.Resources;
using WorkNotes.Web.ViewModels;

namespace WorkNotes.Web.Pages.Notes;

// The text editor of a note. Everyone who may see the note can read it; only the owner saves.
[Authorize]
public sealed class EditModel(INoteService notes, IStringLocalizer<SharedResources> localizer) : PageModel
{
    public NoteDocument Note { get; private set; } = null!;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var note = await notes.GetDocumentAsync(id, UserId, cancellationToken);
        if (note is null) return NotFound();
        Note = note;
        return Page();
    }

    // Called by note-editor.js with a JSON body; the antiforgery token travels in the RequestVerificationToken header.
    public async Task<IActionResult> OnPostSaveAsync(int id, [FromBody] NoteSaveRequest? request, CancellationToken cancellationToken)
    {
        if (request is null) return Failure(StatusCodes.Status400BadRequest, "Editor_InvalidContent");
        var result = await notes.SaveAsync(UserId, id, request.Version, request.Title,
            request.Blocks.Select(block => new NoteBlockInput(block.Id, block.Content)).ToList(), cancellationToken);
        return result.Status switch
        {
            NoteSaveStatus.Saved => new JsonResult(new { version = result.Version }),
            NoteSaveStatus.Conflict => Failure(StatusCodes.Status409Conflict, "Editor_Conflict"),
            NoteSaveStatus.Forbidden => Failure(StatusCodes.Status403Forbidden, "Editor_ReadOnly"),
            NoteSaveStatus.NotFound => Failure(StatusCodes.Status404NotFound, "Editor_NotFound"),
            NoteSaveStatus.InvalidTitle => Failure(StatusCodes.Status400BadRequest, "Validation_InvalidNoteTitle"),
            _ => Failure(StatusCodes.Status400BadRequest, "Editor_InvalidContent")
        };
    }

    private JsonResult Failure(int statusCode, string messageKey) =>
        new(new { message = localizer[messageKey].Value }) { StatusCode = statusCode };
}
