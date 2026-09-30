using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkNotes.Web.Authentication;

namespace WorkNotes.Web.Pages.Admin;

[Authorize(AuthenticationSchemes = AdminAuthenticationDefaults.Scheme)]
public sealed class IndexModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Admin/Configuration");
}
