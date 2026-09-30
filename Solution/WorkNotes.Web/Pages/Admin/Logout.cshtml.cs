using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkNotes.Web.Authentication;

namespace WorkNotes.Web.Pages.Admin;

[Authorize(AuthenticationSchemes = AdminAuthenticationDefaults.Scheme)]
public sealed class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(AdminAuthenticationDefaults.Scheme);
        return RedirectToPage("/Admin/Login");
    }
}
