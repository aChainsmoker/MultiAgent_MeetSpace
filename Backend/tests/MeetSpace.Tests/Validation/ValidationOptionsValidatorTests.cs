using MeetSpace.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MeetSpace.Tests.Validation;

public class ValidationOptionsValidatorTests
{
    private static ServiceProvider BuildProvider(Action<ValidationOptions> configure)
    {
        var services = new ServiceCollection();
        services
            .AddOptions<ValidationOptions>()
            .Configure(configure)
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

        return services.BuildServiceProvider();
    }

    [Fact]
    public void ValidDefaultValues_PassesValidation()
    {
        var provider = BuildProvider(o =>
        {
            o.UserNameMaxLength = 100;
            o.EmailMaxLength = 255;
            o.EquipmentNameMaxLength = 50;
            o.RoomNameMaxLength = 200;
            o.RoomDescriptionMaxLength = 2000;
            o.BookingTitleMaxLength = 200;
            o.BookingDescriptionMaxLength = 2000;
        });

        var options = provider.GetRequiredService<IOptions<ValidationOptions>>().Value;

        Assert.Equal(100, options.UserNameMaxLength);
        Assert.Equal(255, options.EmailMaxLength);
    }

    [Fact]
    public void UserNameMaxLengthZero_ThrowsOptionsValidationException()
    {
        var provider = BuildProvider(o =>
        {
            o.UserNameMaxLength = 0;
            o.EmailMaxLength = 255;
            o.EquipmentNameMaxLength = 50;
            o.RoomNameMaxLength = 200;
            o.RoomDescriptionMaxLength = 2000;
            o.BookingTitleMaxLength = 200;
            o.BookingDescriptionMaxLength = 2000;
        });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ValidationOptions>>().Value);
    }
}
