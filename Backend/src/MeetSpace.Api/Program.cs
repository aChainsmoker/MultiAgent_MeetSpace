using MeetSpace.Application.Options;
using MeetSpace.Infrastructure.Options;

var builder = WebApplication.CreateBuilder(args);

// Booking
builder.Services
    .AddOptions<BookingOptions>()
    .Bind(builder.Configuration.GetSection("Booking"))
    .ValidateOnStart()
    .Validate(options =>
    {
        if (options.WorkingHoursStart >= options.WorkingHoursEnd)
            return false;
        if (options.BookingTimeStepMinutes <= 0 || options.BookingTimeStepMinutes > 60)
            return false;
        if (options.BookingTimeStepMinutes > (int)(options.WorkingHoursEnd - options.WorkingHoursStart).TotalMinutes)
            return false;
        return true;
    }, "Invalid Booking configuration: working hours must be valid and time step must be positive and fit within the working interval.");

// Jwt
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateOnStart()
    .Validate(options =>
    {
        if (options.AccessTokenLifetimeMinutes <= 0)
            return false;
        if (options.RefreshTokenLifetimeDays <= 0)
            return false;
        return true;
    }, "Jwt configuration: lifetimes must be positive.");

// PasswordHashing
builder.Services
    .AddOptions<PasswordHashingOptions>()
    .Bind(builder.Configuration.GetSection("PasswordHashing"))
    .ValidateOnStart()
    .Validate(options =>
    {
        if (options.BcryptWorkFactor < 4 || options.BcryptWorkFactor > 31)
            return false;
        return true;
    }, "PasswordHashing: BcryptWorkFactor must be between 4 and 31.");

// S3
builder.Services
    .AddOptions<S3Options>()
    .Bind(builder.Configuration.GetSection("S3"))
    .ValidateOnStart()
    .Validate(options =>
    {
        if (string.IsNullOrWhiteSpace(options.BucketName))
            return false;
        if (options.PresignedUrlLifetimeMinutes <= 0)
            return false;
        return true;
    }, "S3 configuration: BucketName is required and PresignedUrlLifetimeMinutes must be positive.");

// Validation
builder.Services
    .AddOptions<ValidationOptions>()
    .Bind(builder.Configuration.GetSection("Validation"))
    .ValidateOnStart()
    .Validate(options =>
    {
        if (options.UserNameMaxLength <= 0) return false;
        if (options.EmailMaxLength <= 0) return false;
        if (options.EquipmentNameMaxLength <= 0) return false;
        if (options.RoomNameMaxLength <= 0) return false;
        if (options.RoomDescriptionMaxLength <= 0) return false;
        if (options.BookingTitleMaxLength <= 0) return false;
        if (options.BookingDescriptionMaxLength <= 0) return false;
        return true;
    }, "Validation: all MaxLength values must be positive.");

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
