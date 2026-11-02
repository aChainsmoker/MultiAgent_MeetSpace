using MeetSpace.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MeetSpace.Tests.Validation;

public class PasswordHashingOptionsValidatorTests
{
    private static ServiceProvider BuildProvider(Action<PasswordHashingOptions> configure)
    {
        var services = new ServiceCollection();
        services
            .AddOptions<PasswordHashingOptions>()
            .Configure(configure)
            .ValidateOnStart()
            .Validate(options =>
            {
                if (options.BcryptWorkFactor < 4 || options.BcryptWorkFactor > 31)
                    return false;
                return true;
            }, "PasswordHashing: BcryptWorkFactor must be between 4 and 31.");

        return services.BuildServiceProvider();
    }

    [Fact]
    public void ValidDefaultValue_PassesValidation()
    {
        var provider = BuildProvider(o => { o.BcryptWorkFactor = 12; });

        var options = provider.GetRequiredService<IOptions<PasswordHashingOptions>>().Value;

        Assert.Equal(12, options.BcryptWorkFactor);
    }

    [Fact]
    public void BcryptWorkFactorTooLow_ThrowsOptionsValidationException()
    {
        var provider = BuildProvider(o => { o.BcryptWorkFactor = 3; });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<PasswordHashingOptions>>().Value);
    }

    [Fact]
    public void BcryptWorkFactorTooHigh_ThrowsOptionsValidationException()
    {
        var provider = BuildProvider(o => { o.BcryptWorkFactor = 32; });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<PasswordHashingOptions>>().Value);
    }
}
