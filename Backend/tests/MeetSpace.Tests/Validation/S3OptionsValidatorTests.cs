using MeetSpace.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MeetSpace.Tests.Validation;

public class S3OptionsValidatorTests
{
    private static ServiceProvider BuildProvider(Action<S3Options> configure)
    {
        var services = new ServiceCollection();
        services
            .AddOptions<S3Options>()
            .Configure(configure)
            .ValidateOnStart()
            .Validate(options =>
            {
                if (string.IsNullOrWhiteSpace(options.BucketName))
                    return false;
                if (options.PresignedUrlLifetimeMinutes <= 0)
                    return false;
                return true;
            }, "S3 configuration: BucketName is required and PresignedUrlLifetimeMinutes must be positive.");

        return services.BuildServiceProvider();
    }

    [Fact]
    public void ValidDefaultValues_PassesValidation()
    {
        var provider = BuildProvider(o =>
        {
            o.BucketName = "meet-space";
            o.PresignedUrlLifetimeMinutes = 60;
        });

        var options = provider.GetRequiredService<IOptions<S3Options>>().Value;

        Assert.Equal("meet-space", options.BucketName);
        Assert.Equal(60, options.PresignedUrlLifetimeMinutes);
    }

    [Fact]
    public void BucketNameEmpty_ThrowsOptionsValidationException()
    {
        var provider = BuildProvider(o =>
        {
            o.BucketName = "";
            o.PresignedUrlLifetimeMinutes = 60;
        });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<S3Options>>().Value);
    }

    [Fact]
    public void PresignedUrlLifetimeMinutesZero_ThrowsOptionsValidationException()
    {
        var provider = BuildProvider(o =>
        {
            o.BucketName = "meet-space";
            o.PresignedUrlLifetimeMinutes = 0;
        });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<S3Options>>().Value);
    }
}
