using System.ComponentModel.DataAnnotations;
using WorkNotes.Business.Models;

namespace WorkNotes.Web.ViewModels;

public class NewNoteInput
{
    [Display(Name = "Field_Context"), Range(1, int.MaxValue, ErrorMessage = "Validation_Required")]
    public int ContextId { get; set; }

    [Display(Name = "Field_NoteType"), Required(ErrorMessage = "Validation_Required")]
    public string NoteType { get; set; } = NoteTypes.Journal;

    [Display(Name = "Field_Title")]
    [StringLength(NoteRules.TitleMaxLength, ErrorMessage = "Validation_MaxLength")]
    public string? Title { get; set; }
}
