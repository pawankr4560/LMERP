using LMERP.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace LMERP.Infrastructure.Persistence;

/// <summary>Stores only a refresh-token hash in the existing ASP.NET Identity user-token table.</summary>
public sealed class RefreshTokenStore(LoanDbContext db) : IRefreshTokenStore
{
    private const string LoginProvider = "LMERP";
    private const string TokenName = "RefreshToken";

    public async Task SaveAsync(int userId, string tokenHash, CancellationToken cancellationToken)
    {
        var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [dbo].[AspNetUserTokens]
            SET [Value] = {tokenHash}
            WHERE [UserId] = {userId} AND [LoginProvider] = {LoginProvider} AND [Name] = {TokenName};
            """, cancellationToken);
        if (updated == 0)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [dbo].[AspNetUserTokens] ([UserId], [LoginProvider], [Name], [Value])
                VALUES ({userId}, {LoginProvider}, {TokenName}, {tokenHash});
                """, cancellationToken);
    }

    public async Task<bool> TryRotateAsync(int userId, string currentTokenHash, string nextTokenHash, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [dbo].[AspNetUserTokens]
            SET [Value] = {nextTokenHash}
            WHERE [UserId] = {userId} AND [LoginProvider] = {LoginProvider}
                AND [Name] = {TokenName} AND [Value] = {currentTokenHash};
            """, cancellationToken) == 1;

    public async Task RevokeAsync(int userId, string tokenHash, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM [dbo].[AspNetUserTokens]
            WHERE [UserId] = {userId} AND [LoginProvider] = {LoginProvider}
                AND [Name] = {TokenName} AND [Value] = {tokenHash};
            """, cancellationToken);
}
