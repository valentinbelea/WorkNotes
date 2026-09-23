using System.ComponentModel.DataAnnotations;

namespace WorkNotes.Web.ViewModels;

public class ContextMemberInput
{
    [Display(Name = "Field_Email"), Required(ErrorMessage = "Validation_Required")]
    [EmailAddress(ErrorMessage = "Validation_InvalidEmail")]
    [StringLength(256, ErrorMessage = "Validation_MaxLength")]
    public string Email { get; set; } = "";
}
