using MeetSpace.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MeetSpace.Tests.Validation;

public class JwtOptionsValidatorTests
{
    private static ServiceProvider BuildProvider(Action<JwtOptions> configure)
    {
        var services = new ServiceCollection();
        services
            .AddOptions<JwtOptions>()
            .Configure(configure)
            .ValidateOnStart()
            .Validate(options =>
            {
                if (options.AccessTokenLifetimeMinutes <= 0)
                    return false;
                if (options.RefreshTokenLifetimeDays <= 0)
                    return false;
                return true;
            }, "Jwt configuration: lifetimes must be positive.");

        return services.BuildServiceProvider();
    }

    [Fact]
    public void ValidDefaultValues_PassesValidation()
    {
        var provider = BuildProvider(o =>
        {
            o.AccessTokenLifetimeMinutes = 10;
            o.RefreshTokenLifetimeDays = 14;
        });

        var options = provider.GetRequiredService<IOptions<JwtOptions>>().Value;

        Assert.Equal(10, options.AccessTokenLifetimeMinutes);
        Assert.Equal(14, options.RefreshTokenLifetimeDays);
    }

    [Fact]
    public void AccessTokenLifetimeMinutesZero_ThrowsOptionsValidationException()
    {
        var provider = BuildProvider(o =>
        {
            o.AccessTokenLifetimeMinutes = 0;
            o.RefreshTokenLifetimeDays = 14;
        });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<JwtOptions>>().Value);
    }

    [Fact]
    public void RefreshTokenLifetimeDaysZero_ThrowsOptionsValidationException()
    {
        var provider = BuildProvider(o =>
        {
            o.AccessTokenLifetimeMinutes = 10;
            o.RefreshTokenLifetimeDays = 0;
        });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<JwtOptions>>().Value);
    }
}
