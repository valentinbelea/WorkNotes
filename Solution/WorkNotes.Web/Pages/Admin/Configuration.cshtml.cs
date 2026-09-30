using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Resources.Resources;
using WorkNotes.Web.Authentication;
using WorkNotes.Web.ViewModels;

namespace WorkNotes.Web.Pages.Admin;

[Authorize(AuthenticationSchemes = AdminAuthenticationDefaults.Scheme)]
public sealed class ConfigurationModel(IGitHubConfigurationService service, IStringLocalizer<SharedResources> localizer) : PageModel
{
    [BindProperty] public ViewModels.GitHubConfigurationInput Input { get; set; } = new();
    public bool HasClientSecret { get; private set; }
    public string? StatusMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var current = await service.GetAsync(cancellationToken);
        if (current is null) return;
        Input = new() { ClientId = current.ClientId, Scopes = current.Scopes, CallbackUrl = current.CallbackUrl };
        HasClientSecret = current.HasClientSecret;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        var status = await service.SaveAsync(new(Input.ClientId, Input.ClientSecret, Input.Scopes, Input.CallbackUrl), cancellationToken);
        if (status != GitHubConfigurationSaveStatus.Succeeded)
        {
            ModelState.AddModelError("", localizer[$"Message_GitHubConfiguration_{status}"]);
            HasClientSecret = (await service.GetAsync(cancellationToken))?.HasClientSecret == true;
            return Page();
        }
        StatusMessage = localizer["Message_ConfigurationSaved"];
        await OnGetAsync(cancellationToken);
        return Page();
    }
}
