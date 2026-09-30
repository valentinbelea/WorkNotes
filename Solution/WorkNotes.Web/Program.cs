using Microsoft.Extensions.Localization;
using WorkNotes.Resources.Resources;
using WorkNotes.Web.Localization;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Services;
using WorkNotes.DataAccess;
using WorkNotes.Integrations;
using WorkNotes.Web.Git;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using WorkNotes.Web.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(LocalizationConfiguration.Configure);
builder.Services.AddSingleton<IStringLocalizer>(provider => provider.GetRequiredService<IStringLocalizer<SharedResources>>());
builder.Services.ConfigureOptions<LocalizedMvcOptions>();
builder.Services.AddRazorPages().AddDataAnnotationsLocalization(options =>
    options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResources)));
builder.Services.AddProblemDetails();
var authenticationBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});
authenticationBuilder.AddIdentityCookies();
authenticationBuilder.AddCookie(AdminAuthenticationDefaults.Scheme, options =>
{
    options.Cookie.Name = "WorkNotes.Admin.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.LoginPath = "/admin/login";
    options.AccessDeniedPath = "/admin/login";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});
builder.Services.AddAuthorization();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "WorkNotes.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});
builder.Services.AddScoped<IApplicationVersionService, ApplicationVersionService>();
builder.Services.AddScoped<IWorkContextService, WorkContextService>();
builder.Services.AddScoped<IContextMemberService, ContextMemberService>();
builder.Services.AddScoped<INoteService, NoteService>();
builder.Services.AddScoped<INoteReferenceService, NoteReferenceService>();
builder.Services.AddSingleton<ReferenceTypeCache>();
builder.Services.AddScoped<IReferenceTypeService, ReferenceTypeService>();
builder.Services.AddScoped<IGitHubTokenService, GitHubTokenService>();
builder.Services.AddScoped<IGitHubConnectionService, GitHubConnectionService>();
builder.Services.AddScoped<IGitRepositoryService, GitRepositoryService>();
builder.Services.AddScoped<INoteGitReferenceService, NoteGitReferenceService>();
builder.Services.AddScoped<IGitHubConfigurationService, GitHubConfigurationService>();
builder.Services.AddSingleton<GitHubAuthorizationCookie>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDataAccess(builder.Configuration.GetConnectionString("WorkNotes")
    ?? throw new InvalidOperationException("ConnectionStrings:WorkNotes is required."));

builder.Services.AddIntegrations(builder.Configuration.GetSection("GitHub"));

// The keys encrypt the cookies and the stored Git tokens: they must survive restarts and be the same on every instance.
// DataProtection:KeysPath sets their folder on a hosted environment; without it the per-user default folder is used.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("WorkNotes");
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
    if (OperatingSystem.IsWindows()) dataProtection.ProtectKeysWithDpapi();
}

builder.Services.AddScoped<IdentityErrorDescriber, LocalizedIdentityErrorDescriber>();

var app = builder.Build();

if (builder.Configuration["AdminBootstrap:Password"] is { Length: > 0 } bootstrapPassword)
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<IAdminBootstrapService>().CreateIfMissingAsync(
        builder.Configuration["AdminBootstrap:UserName"] ?? "admin", bootstrapPassword, CancellationToken.None);
}
app.UseRequestLocalization();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
