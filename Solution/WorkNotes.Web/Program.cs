using Microsoft.Extensions.Localization;
using WorkNotes.Resources.Resources;
using WorkNotes.Web.Localization;
using WorkNotes.Web.Notes;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Services;
using WorkNotes.DataAccess;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);
// With create-note-references the application runs that maintenance command instead of the site.
var createNoteReferences = NoteReferenceBackfillCommand.IsRequested(args);
if (createNoteReferences)
    // The command's report is its output: the framework's information logs (the SQL of every query) are left out.
    builder.Logging.AddFilter("Microsoft", LogLevel.Warning);

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
builder.Services.AddScoped<IWorkContextService, WorkContextService>();
builder.Services.AddScoped<IContextMemberService, ContextMemberService>();
builder.Services.AddScoped<INoteService, NoteService>();
builder.Services.AddScoped<INoteReferenceBackfillService, NoteReferenceBackfillService>();
builder.Services.AddScoped<NoteReferenceBackfillCommand>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDataAccess(builder.Configuration.GetConnectionString("WorkNotes")
    ?? throw new InvalidOperationException("ConnectionStrings:WorkNotes is required."));

builder.Services.AddScoped<IdentityErrorDescriber, LocalizedIdentityErrorDescriber>();

var app = builder.Build();

if (createNoteReferences)
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
    // Ctrl+C stops the command (each note is saved whole or not at all); a second Ctrl+C ends the process.
    var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = !stop.IsCancellationRequested;
        stop.Cancel();
    };
    await using var scope = app.Services.CreateAsyncScope();
    Environment.ExitCode = await scope.ServiceProvider.GetRequiredService<NoteReferenceBackfillCommand>()
        .RunAsync(args.Contains(NoteReferenceBackfillCommand.SaveOption), Console.Out, stop.Token);
    return;
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
