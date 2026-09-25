using Microsoft.Extensions.Localization;
using WorkNotes.Resources.Resources;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkNotes.Business.Abstractions;
using WorkNotes.Web.Messages;
using WorkNotes.Web.ViewModels;
namespace WorkNotes.Web.Pages.Account;

[Authorize]
public sealed class ChangePasswordModel(IAccountService accounts, IStringLocalizer<SharedResources> localizer) : PageModel
{
    [BindProperty] public ChangePasswordInput Input { get; set; } = new();
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        var result = await accounts.ChangePasswordAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            Input.CurrentPassword, Input.NewPassword, cancellationToken);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("", localizer[error]);
            return Page();
        }
        TempData.SetStatusMessage("Message_PasswordChanged");
        return RedirectToPage("/Account/Index");
    }
}

