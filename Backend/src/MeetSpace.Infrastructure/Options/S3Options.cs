namespace MeetSpace.Infrastructure.Options;

public class S3Options
{
    public string BucketName { get; set; } = string.Empty;
    public int PresignedUrlLifetimeMinutes { get; set; }
}
