using Microsoft.Extensions.Localization;
using WorkNotes.Resources.Resources;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkNotes.Business.Abstractions;
using WorkNotes.Web.ViewModels;
namespace WorkNotes.Web.Pages.Account;

[Authorize]
public sealed class IndexModel(IAccountService accounts, IStringLocalizer<SharedResources> localizer) : PageModel
{
    [BindProperty] public ProfileInput Input { get; set; } = new();
    public string Email { get; private set; } = "";
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var profile = await accounts.GetProfileAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken);
        if (profile is null) return Challenge();
        Input = new() { FirstName = profile.FirstName, LastName = profile.LastName };
        Email = profile.Email;
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var profile = await accounts.GetProfileAsync(userId, cancellationToken);
        if (profile is null) return Challenge();
        Email = profile.Email;
        if (!ModelState.IsValid) return Page();
        var result = await accounts.UpdateProfileAsync(userId, Input.FirstName, Input.LastName, cancellationToken);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("", localizer[error]);
            return Page();
        }
        TempData["StatusMessage"] = "Message_SaveSucceeded";
        return RedirectToPage();
    }
}

