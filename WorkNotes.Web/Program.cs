using Microsoft.Extensions.Localization;
using WorkNotes.Resources.Resources;
using WorkNotes.Web.Localization;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Services;
using WorkNotes.DataAccess;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(LocalizationConfiguration.Configure);
builder.Services.AddSingleton<IStringLocalizer>(provider => provider.GetRequiredService<IStringLocalizer<SharedResources>>());
builder.Services.ConfigureOptions<LocalizedMvcOptions>();
builder.Services.AddRazorPages().AddDataAnnotationsLocalization(options =>
    options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResources)));
builder.Services.AddProblemDetails();
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
}).AddIdentityCookies();
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
builder.Services.AddDataAccess(builder.Configuration.GetConnectionString("WorkNotes")
    ?? throw new InvalidOperationException("ConnectionStrings:WorkNotes is required."));

builder.Services.AddScoped<IdentityErrorDescriber, LocalizedIdentityErrorDescriber>();

var app = builder.Build();
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
