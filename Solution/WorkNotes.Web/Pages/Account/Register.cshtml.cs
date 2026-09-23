using Microsoft.Extensions.Localization;
using WorkNotes.Resources.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkNotes.Business.Abstractions;
using WorkNotes.Web.ViewModels;
namespace WorkNotes.Web.Pages.Account;

[AllowAnonymous]
public sealed class RegisterModel(IAccountService accounts, IStringLocalizer<SharedResources> localizer) : PageModel
{
    [BindProperty] public RegisterInput Input { get; set; } = new();
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        var result = await accounts.RegisterAsync(Input.FirstName, Input.LastName, Input.Email, Input.Password, cancellationToken);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("", localizer[error]);
            return Page();
        }
        TempData["StatusMessage"] = "Message_RegistrationSucceeded";
        return RedirectToPage("/Account/Login");
    }
}

