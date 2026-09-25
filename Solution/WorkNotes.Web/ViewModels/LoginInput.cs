using System.ComponentModel.DataAnnotations;
namespace WorkNotes.Web.ViewModels;

public sealed class LoginInput
{
    [Display(Name = "Field_Email"), Required(ErrorMessage = "Validation_Required")]
    [EmailAddress(ErrorMessage = "Validation_InvalidEmail")]
    [StringLength(256, ErrorMessage = "Validation_MaxLength")]
    public string Email { get; set; } = "";

    [Display(Name = "Field_Password"), DataType(DataType.Password), Required(ErrorMessage = "Validation_Required")]
    public string Password { get; set; } = "";

    [Display(Name = "Field_RememberMe")]
    public bool RememberMe { get; set; }
}

