using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WorkNotes.Business.Abstractions;
using WorkNotes.Integrations.GitHub;

namespace WorkNotes.Integrations;

public static class DependencyInjection
{
    // gitHub is the "GitHub" configuration section (GitHubOptions).
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration gitHub)
    {
        ArgumentNullException.ThrowIfNull(gitHub);

        services.Configure<GitHubOptions>(gitHub);
        // A typed client: transient, over the handlers pooled by IHttpClientFactory.
        services.AddHttpClient<IGitHubOAuthClient, GitHubOAuthClient>((provider, client) =>
        {
            client.Timeout = provider.GetRequiredService<IOptions<GitHubOptions>>().Value.Timeout;
            // GitHub refuses API requests without a User-Agent.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WorkNotes");
        });

        return services;
    }
}
