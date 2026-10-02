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

    public IReadOnlyList<string> Environments { get; } = [GitHubEnvironments.Development, GitHubEnvironments.Production];

    public async Task OnGetAsync(string? environmentName, CancellationToken cancellationToken)
    {
        var selectedEnvironment = GitHubEnvironments.IsSupported(environmentName ?? "")
            ? environmentName!
            : GitHubEnvironments.Development;
        Input.EnvironmentName = selectedEnvironment;
        var current = await service.GetAsync(selectedEnvironment, cancellationToken);
        if (current is null) return;
        Input = new() { EnvironmentName = current.EnvironmentName, ClientId = current.ClientId, Scopes = current.Scopes, CallbackUrl = current.CallbackUrl };
        HasClientSecret = current.HasClientSecret;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        var status = await service.SaveAsync(new(Input.EnvironmentName, Input.ClientId, Input.ClientSecret, Input.Scopes, Input.CallbackUrl), cancellationToken);
        if (status != GitHubConfigurationSaveStatus.Succeeded)
        {
            ModelState.AddModelError("", localizer[$"Message_GitHubConfiguration_{status}"]);
            HasClientSecret = (await service.GetAsync(Input.EnvironmentName, cancellationToken))?.HasClientSecret == true;
            return Page();
        }
        StatusMessage = localizer["Message_ConfigurationSaved"];
        await OnGetAsync(Input.EnvironmentName, cancellationToken);
        return Page();
    }
}
