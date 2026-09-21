using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Services;
using WorkNotes.DataAccess;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<IApplicationVersionService, ApplicationVersionService>();
builder.Services.AddDataAccess(builder.Configuration.GetConnectionString("WorkNotes")
    ?? throw new InvalidOperationException("ConnectionStrings:WorkNotes is required."));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
