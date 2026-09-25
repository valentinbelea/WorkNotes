using System.ComponentModel.DataAnnotations;
using WorkNotes.Business.Models;

namespace WorkNotes.Web.ViewModels;

public class WorkContextInput
{
    [Display(Name = "Field_Name"), Required(ErrorMessage = "Validation_Required")]
    [StringLength(WorkContextRules.NameMaxLength, ErrorMessage = "Validation_MaxLength")]
    public string Name { get; set; } = "";

    [Display(Name = "Field_Description")]
    [StringLength(WorkContextRules.DescriptionMaxLength, ErrorMessage = "Validation_MaxLength")]
    public string? Description { get; set; }
}
