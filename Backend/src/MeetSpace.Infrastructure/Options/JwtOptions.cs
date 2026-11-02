namespace MeetSpace.Infrastructure.Options;

public class JwtOptions
{
    public int AccessTokenLifetimeMinutes { get; set; }
    public int RefreshTokenLifetimeDays { get; set; }
}
