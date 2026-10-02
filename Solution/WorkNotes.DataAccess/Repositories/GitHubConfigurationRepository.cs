using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.DataAccess.Context;
using Entity = WorkNotes.DataAccess.Entities.GitHubConfiguration;

namespace WorkNotes.DataAccess.Repositories;

public sealed class GitHubConfigurationRepository(WorkNotesDbContext dbContext, IDataProtectionProvider protection)
    : IGitHubConfigurationRepository
{
    private readonly IDataProtector protector = protection.CreateProtector("WorkNotes.GitHubConfiguration.v1");

    public async Task<GitHubConfiguration?> GetAsync(string environmentName, CancellationToken cancellationToken)
    {
        var row = await dbContext.GitHubConfigurations.AsNoTracking()
            .SingleOrDefaultAsync(value => value.EnvironmentName == environmentName, cancellationToken);
        if (row is null) return null;
        try
        {
            return new(row.EnvironmentName, protector.Unprotect(row.ProtectedClientId), true, row.Scopes, row.CallbackUrl);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public async Task<GitHubConfigurationCredential?> GetCredentialAsync(string environmentName, CancellationToken cancellationToken)
    {
        var row = await dbContext.GitHubConfigurations.AsNoTracking()
            .SingleOrDefaultAsync(value => value.EnvironmentName == environmentName, cancellationToken);
        if (row is null) return null;
        try
        {
            return new(row.EnvironmentName, protector.Unprotect(row.ProtectedClientId), protector.Unprotect(row.ProtectedClientSecret),
                row.Scopes, row.CallbackUrl);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public async Task SaveAsync(GitHubConfigurationInput input, CancellationToken cancellationToken)
    {
        var row = await dbContext.GitHubConfigurations
            .SingleOrDefaultAsync(value => value.EnvironmentName == input.EnvironmentName, cancellationToken);
        if (row is null)
        {
            if (input.ClientSecret is null) throw new InvalidOperationException("The initial secret is required.");
            row = new Entity
            {
                Id = input.EnvironmentName == GitHubEnvironments.Production ? 1 : 2,
                EnvironmentName = input.EnvironmentName,
                CreatedAtUtc = DateTime.UtcNow,
                ProtectedClientSecret = protector.Protect(input.ClientSecret)
            };
            dbContext.GitHubConfigurations.Add(row);
        }
        else if (input.ClientSecret is not null)
        {
            row.ProtectedClientSecret = protector.Protect(input.ClientSecret);
        }

        row.ProtectedClientId = protector.Protect(input.ClientId);
        row.Scopes = input.Scopes;
        row.CallbackUrl = input.CallbackUrl;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
