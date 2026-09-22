using System.ComponentModel.DataAnnotations;
using WorkNotes.Business.Models;

namespace WorkNotes.Web.ViewModels;

public sealed class RegisterInput : ProfileInput
{
    [Display(Name = "Field_Email"), Required(ErrorMessage = "Validation_Required")]
    [EmailAddress(ErrorMessage = "Validation_InvalidEmail")]
    [StringLength(256, ErrorMessage = "Validation_MaxLength")]
    public string Email { get; set; } = "";

    [Display(Name = "Field_Password"), DataType(DataType.Password), Required(ErrorMessage = "Validation_Required")]
    [RegularExpression(AccountRules.PasswordPattern, ErrorMessage = "Validation_PasswordPolicy")]
    public string Password { get; set; } = "";

    [Display(Name = "Field_ConfirmPassword"), DataType(DataType.Password), Required(ErrorMessage = "Validation_Required")]
    [Compare(nameof(Password), ErrorMessage = "Validation_PasswordsDiffer")]
    public string ConfirmPassword { get; set; } = "";
}

