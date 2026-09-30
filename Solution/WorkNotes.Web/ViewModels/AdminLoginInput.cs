using System.ComponentModel.DataAnnotations;

namespace WorkNotes.Web.ViewModels;

public sealed class AdminLoginInput
{
    [Required(ErrorMessage = "Validation_UserNameRequired")]
    [Display(Name = "Field_UserName")]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "Validation_PasswordRequired")]
    [DataType(DataType.Password)]
    [Display(Name = "Field_Password")]
    public string Password { get; set; } = "";
}
