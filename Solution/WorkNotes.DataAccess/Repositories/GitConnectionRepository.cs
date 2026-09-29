using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.DataAccess.Context;
using GitConnectionEntity = WorkNotes.DataAccess.Entities.GitConnection;

namespace WorkNotes.DataAccess.Repositories;

// The tokens are encrypted with ASP.NET Core Data Protection before they reach SQL Server and decrypted when read:
// the database alone never gives them away. They can be decrypted only with the application's keys (see Program.cs).
public sealed class GitConnectionRepository(WorkNotesDbContext dbContext, IDataProtectionProvider protection)
    : IGitConnectionRepository
{
    private readonly IDataProtector protector = protection.CreateProtector("WorkNotes.GitConnections.Tokens");

    public async Task<GitConnection?> GetAsync(string userId, string provider, CancellationToken cancellationToken)
    {
        var row = await dbContext.GitConnections
            .AsNoTracking()
            .Where(connection => connection.UserId == userId && connection.Provider == provider)
            .Select(connection => new
            {
                connection.Provider,
                connection.AccountLogin,
                connection.ConnectedAtUtc,
                connection.ValidatedAtUtc,
                connection.AccessTokenExpiresAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : new GitConnection(row.Provider, row.AccountLogin, Utc(row.ConnectedAtUtc),
            Utc(row.ValidatedAtUtc), Utc(row.AccessTokenExpiresAtUtc));
    }

    public async Task<GitCredential?> GetCredentialAsync(string userId, string provider, CancellationToken cancellationToken)
    {
        var row = await dbContext.GitConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(connection => connection.UserId == userId && connection.Provider == provider, cancellationToken);
        return row is null ? null : new GitCredential(row.AccountId, row.AccountLogin, Tokens(row),
            Utc(row.ConnectedAtUtc), Utc(row.ValidatedAtUtc));
    }

    public async Task SaveAsync(string userId, string provider, GitCredential credential, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(userId, provider, cancellationToken);
        if (entity is not null)
        {
            Apply(entity, credential);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        entity = new GitConnectionEntity { UserId = userId, Provider = provider };
        Apply(entity, credential);
        dbContext.GitConnections.Add(entity);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // The same user connected in another request meanwhile: the latest connection replaces it.
            dbContext.Entry(entity).State = EntityState.Detached;
            if (!await UpdateAsync(userId, provider, credential, cancellationToken)) throw;
        }
    }

    public async Task<bool> UpdateAsync(string userId, string provider, GitCredential credential, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(userId, provider, cancellationToken);
        if (entity is null) return false;

        Apply(entity, credential);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Deleted by a disconnection between the read and the update.
            dbContext.Entry(entity).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<bool> DeleteAsync(string userId, string provider, CancellationToken cancellationToken) =>
        await dbContext.GitConnections
            .Where(connection => connection.UserId == userId && connection.Provider == provider)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    private Task<GitConnectionEntity?> FindAsync(string userId, string provider, CancellationToken cancellationToken) =>
        dbContext.GitConnections
            .FirstOrDefaultAsync(connection => connection.UserId == userId && connection.Provider == provider, cancellationToken);

    private void Apply(GitConnectionEntity entity, GitCredential credential)
    {
        // A credential whose tokens cannot be decrypted is never saved: it keeps the stored values.
        if (credential.Tokens is { } tokens)
        {
            entity.ProtectedAccessToken = protector.Protect(tokens.AccessToken);
            entity.ProtectedRefreshToken = tokens.RefreshToken is null ? null : protector.Protect(tokens.RefreshToken);
            entity.AccessTokenExpiresAtUtc = tokens.AccessTokenExpiresAtUtc;
            entity.RefreshTokenExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc;
            entity.Scopes = tokens.Scopes;
        }
        else if (string.IsNullOrEmpty(entity.ProtectedAccessToken))
        {
            throw new ArgumentException("A new Git connection needs its tokens.", nameof(credential));
        }

        entity.AccountId = credential.AccountId;
        entity.AccountLogin = credential.AccountLogin;
        entity.ConnectedAtUtc = credential.ConnectedAtUtc;
        entity.ValidatedAtUtc = credential.ValidatedAtUtc;
    }

    // Null when the tokens were encrypted with keys the application no longer has.
    private GitTokens? Tokens(GitConnectionEntity row)
    {
        try
        {
            return new GitTokens(
                protector.Unprotect(row.ProtectedAccessToken),
                row.ProtectedRefreshToken is null ? null : protector.Unprotect(row.ProtectedRefreshToken),
                Utc(row.AccessTokenExpiresAtUtc),
                Utc(row.RefreshTokenExpiresAtUtc),
                row.Scopes);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? Utc(DateTime? value) => value is { } date ? Utc(date) : null;
}
