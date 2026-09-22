using Microsoft.Extensions.Localization;
using WorkNotes.Resources.Resources;
using Microsoft.AspNetCore.Mvc;
using WorkNotes.Business.Abstractions;
namespace WorkNotes.Web.ViewComponents;

public sealed class ApplicationVersionViewComponent(IApplicationVersionService versions, IStringLocalizer<SharedResources> localizer) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync() =>
        View("Default", await versions.GetCurrentVersionAsync(HttpContext.RequestAborted) ?? localizer["Footer_NoVersion"].Value);
}

