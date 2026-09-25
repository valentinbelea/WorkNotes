using System.ComponentModel.DataAnnotations;
using WorkNotes.Business.Models;

namespace WorkNotes.Web.ViewModels;

public class ProfileInput
{
    [Display(Name = "Field_FirstName"), Required(ErrorMessage = "Validation_Required")]
    [StringLength(AccountRules.NameMaxLength, ErrorMessage = "Validation_MaxLength")]
    public string FirstName { get; set; } = "";

    [Display(Name = "Field_LastName"), Required(ErrorMessage = "Validation_Required")]
    [StringLength(AccountRules.NameMaxLength, ErrorMessage = "Validation_MaxLength")]
    public string LastName { get; set; } = "";
}

