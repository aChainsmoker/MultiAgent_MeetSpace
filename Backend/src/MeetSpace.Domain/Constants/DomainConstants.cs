namespace MeetSpace.Domain.Constants;

public static class DomainConstants
{
    public static readonly TimeSpan WorkingHoursStart = new(9, 0, 0);
    public static readonly TimeSpan WorkingHoursEnd = new(18, 0, 0);

    public const int BookingTimeStepMinutes = 15;

    public const int AccessTokenLifetimeMinutes = 10;
    public const int RefreshTokenLifetimeDays = 14;

    public const int BcryptWorkFactor = 12;

    public const string S3BucketName = "meet-space";
    public const int PresignedUrlLifetimeMinutes = 60;

    public const int UserNameMaxLength = 100;
    public const int EmailMaxLength = 255;
    public const int EquipmentNameMaxLength = 50;
    public const int RoomNameMaxLength = 200;
    public const int RoomDescriptionMaxLength = 2000;
    public const int BookingTitleMaxLength = 200;
    public const int BookingDescriptionMaxLength = 2000;
}
