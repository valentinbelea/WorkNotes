using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Web.Environment;

public sealed class RuntimeEnvironment(IHostEnvironment environment) : IRuntimeEnvironment
{
    // Only the two explicitly supported runtime environments can select OAuth credentials.
    // Staging and custom names intentionally remain unconfigured rather than borrowing production secrets.
    public string Name => environment.IsDevelopment()
        ? GitHubEnvironments.Development
        : environment.IsProduction()
            ? GitHubEnvironments.Production
            : environment.EnvironmentName;
}
