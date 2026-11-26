namespace LMERP.Application.Abstractions;

public interface IRefreshTokenStore
{
    Task SaveAsync(int userId, string tokenHash, CancellationToken cancellationToken);
    Task<bool> TryRotateAsync(int userId, string currentTokenHash, string nextTokenHash, CancellationToken cancellationToken);
    Task RevokeAsync(int userId, string tokenHash, CancellationToken cancellationToken);
}
