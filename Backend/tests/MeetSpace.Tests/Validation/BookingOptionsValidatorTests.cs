using MeetSpace.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MeetSpace.Tests.Validation;

public class BookingOptionsValidatorTests
{
    private static ServiceProvider BuildProvider(Action<BookingOptions> configure)
    {
        var services = new ServiceCollection();
        var optionsBuilder = services
            .AddOptions<BookingOptions>()
            .Configure(configure)
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
            }, "Invalid Booking configuration.");

        return services.BuildServiceProvider();
    }

    [Fact]
    public void ValidDefaultValues_PassesValidation()
    {
        var provider = BuildProvider(o =>
        {
            o.WorkingHoursStart = TimeSpan.FromHours(9);
            o.WorkingHoursEnd = TimeSpan.FromHours(18);
            o.BookingTimeStepMinutes = 15;
        });

        var options = provider.GetRequiredService<IOptions<BookingOptions>>().Value;

        Assert.Equal(TimeSpan.FromHours(9), options.WorkingHoursStart);
        Assert.Equal(TimeSpan.FromHours(18), options.WorkingHoursEnd);
        Assert.Equal(15, options.BookingTimeStepMinutes);
    }

    [Fact]
    public void WorkingHoursStartAfterEnd_ThrowsOptionsValidationException()
    {
        var provider = BuildProvider(o =>
        {
            o.WorkingHoursStart = TimeSpan.FromHours(18);
            o.WorkingHoursEnd = TimeSpan.FromHours(9);
            o.BookingTimeStepMinutes = 15;
        });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<BookingOptions>>().Value);
    }

    [Fact]
    public void WorkingHoursStartEqualsEnd_ThrowsOptionsValidationException()
    {
        var provider = BuildProvider(o =>
        {
            o.WorkingHoursStart = TimeSpan.FromHours(12);
            o.WorkingHoursEnd = TimeSpan.FromHours(12);
            o.BookingTimeStepMinutes = 15;
        });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<BookingOptions>>().Value);
    }

    [Fact]
    public void BookingTimeStepMinutesZero_ThrowsOptionsValidationException()
    {
        var provider = BuildProvider(o =>
        {
            o.WorkingHoursStart = TimeSpan.FromHours(9);
            o.WorkingHoursEnd = TimeSpan.FromHours(18);
            o.BookingTimeStepMinutes = 0;
        });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<BookingOptions>>().Value);
    }

    [Fact]
    public void BookingTimeStepMinutesExceedsWorkingInterval_ThrowsOptionsValidationException()
    {
        var provider = BuildProvider(o =>
        {
            o.WorkingHoursStart = TimeSpan.FromHours(9);
            o.WorkingHoursEnd = TimeSpan.FromHours(18);
            o.BookingTimeStepMinutes = 9999;
        });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<BookingOptions>>().Value);
    }
}
