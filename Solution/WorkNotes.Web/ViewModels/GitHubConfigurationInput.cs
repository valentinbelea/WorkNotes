using System.ComponentModel.DataAnnotations;

namespace WorkNotes.Web.ViewModels;

public sealed class GitHubConfigurationInput
{
    [Required(ErrorMessage = "Validation_ClientIdRequired")]
    [Display(Name = "Field_ClientId")]
    public string ClientId { get; set; } = "";

    [DataType(DataType.Password)]
    [Display(Name = "Field_ClientSecret")]
    public string? ClientSecret { get; set; }

    [Display(Name = "Field_Scopes")]
    public string Scopes { get; set; } = "";

    [Required(ErrorMessage = "Validation_CallbackUrlRequired")]
    [Url(ErrorMessage = "Validation_CallbackUrlInvalid")]
    [Display(Name = "Field_CallbackUrl")]
    public string CallbackUrl { get; set; } = "";
}
