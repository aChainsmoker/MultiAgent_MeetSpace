using MeetSpace.Api.Extensions;
using MeetSpace.Application.Options;
using MeetSpace.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MeetSpace.Tests.Validation;

public class OptionsRegistrationExtensionTests
{
    // Значения повторяют appsettings.json; тесты изменяют отдельные ключи.
    private static readonly Dictionary<string, string?> DefaultConfigValues = new()
    {
        ["Booking:WorkingHoursStart"] = "09:00:00",
        ["Booking:WorkingHoursEnd"] = "18:00:00",
        ["Booking:BookingTimeStepMinutes"] = "15",
        ["Jwt:AccessTokenLifetimeMinutes"] = "10",
        ["Jwt:RefreshTokenLifetimeDays"] = "14",
        ["PasswordHashing:BcryptWorkFactor"] = "12",
        ["S3:BucketName"] = "meet-space",
        ["S3:PresignedUrlLifetimeMinutes"] = "60",
        ["Validation:UserNameMaxLength"] = "100",
        ["Validation:EmailMaxLength"] = "255",
        ["Validation:EquipmentNameMaxLength"] = "50",
        ["Validation:RoomNameMaxLength"] = "200",
        ["Validation:RoomDescriptionMaxLength"] = "2000",
        ["Validation:BookingTitleMaxLength"] = "200",
        ["Validation:BookingDescriptionMaxLength"] = "2000"
    };

    private static IConfiguration BuildConfiguration(IDictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>(DefaultConfigValues);
        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
            {
                values[key] = value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static ServiceProvider BuildProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddMeetSpaceOptions(configuration);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddMeetSpaceOptions_RegistersAllOptions()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Booking:BookingTimeStepMinutes"] = "30"
        });
        var provider = BuildProvider(configuration);

        var booking = provider.GetRequiredService<IOptions<BookingOptions>>().Value;
        Assert.Equal(TimeSpan.FromHours(9), booking.WorkingHoursStart);
        Assert.Equal(TimeSpan.FromHours(18), booking.WorkingHoursEnd);
        Assert.Equal(30, booking.BookingTimeStepMinutes);

        var jwt = provider.GetRequiredService<IOptions<JwtOptions>>().Value;
        Assert.Equal(10, jwt.AccessTokenLifetimeMinutes);
        Assert.Equal(14, jwt.RefreshTokenLifetimeDays);

        var passwordHashing = provider.GetRequiredService<IOptions<PasswordHashingOptions>>().Value;
        Assert.Equal(12, passwordHashing.BcryptWorkFactor);

        var s3 = provider.GetRequiredService<IOptions<S3Options>>().Value;
        Assert.Equal("meet-space", s3.BucketName);
        Assert.Equal(60, s3.PresignedUrlLifetimeMinutes);

        var validation = provider.GetRequiredService<IOptions<ValidationOptions>>().Value;
        Assert.Equal(100, validation.UserNameMaxLength);
        Assert.Equal(255, validation.EmailMaxLength);
        Assert.Equal(50, validation.EquipmentNameMaxLength);
        Assert.Equal(200, validation.RoomNameMaxLength);
        Assert.Equal(2000, validation.RoomDescriptionMaxLength);
        Assert.Equal(200, validation.BookingTitleMaxLength);
        Assert.Equal(2000, validation.BookingDescriptionMaxLength);
    }

    [Fact]
    public void AddMeetSpaceOptions_WithInvalidConfig_ThrowsOnResolve()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Booking:WorkingHoursStart"] = "18:00:00",
            ["Booking:WorkingHoursEnd"] = "09:00:00"
        });
        var provider = BuildProvider(configuration);

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<BookingOptions>>().Value);
        Assert.Equal(typeof(BookingOptions), exception.OptionsType);
    }

    [Fact]
    public void AddMeetSpaceOptions_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration();

        var result = services.AddMeetSpaceOptions(configuration);

        Assert.Same(services, result);
    }
}
