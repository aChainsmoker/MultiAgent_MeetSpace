namespace MeetSpace.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public User User { get; private set; } = null!;

    private RefreshToken() { }

    public RefreshToken(string token, Guid userId)
    {
        Id = Guid.NewGuid();
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        Token = token;
        UserId = userId;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = CreatedAt.AddDays(Constants.DomainConstants.RefreshTokenLifetimeDays);
    }
}
