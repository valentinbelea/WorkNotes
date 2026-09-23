using System.ComponentModel.DataAnnotations;
using WorkNotes.Business.Models;
namespace WorkNotes.Web.ViewModels;

public sealed class ChangePasswordInput
{
    [Display(Name = "Field_CurrentPassword"), DataType(DataType.Password), Required(ErrorMessage = "Validation_Required")]
    public string CurrentPassword { get; set; } = "";

    [Display(Name = "Field_NewPassword"), DataType(DataType.Password), Required(ErrorMessage = "Validation_Required")]
    [RegularExpression(AccountRules.PasswordPattern, ErrorMessage = "Validation_PasswordPolicy")]
    public string NewPassword { get; set; } = "";

    [Display(Name = "Field_ConfirmNewPassword"), DataType(DataType.Password), Required(ErrorMessage = "Validation_Required")]
    [Compare(nameof(NewPassword), ErrorMessage = "Validation_PasswordsDiffer")]
    public string ConfirmPassword { get; set; } = "";
}

