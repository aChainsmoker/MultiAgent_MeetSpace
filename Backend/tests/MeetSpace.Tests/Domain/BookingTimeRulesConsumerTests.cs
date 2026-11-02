using MeetSpace.Domain.Entities;
using MeetSpace.Domain.Rules;

namespace MeetSpace.Tests.Domain;

public class BookingTimeRulesConsumerTests
{
    private static readonly BookingTimeRules DefaultRules = new()
    {
        WorkingHoursStart = TimeSpan.FromHours(9),
        WorkingHoursEnd = TimeSpan.FromHours(18),
        TimeStepMinutes = 15
    };

    private static Booking CreateBooking(BookingTimeRules? rules = null, int titleMaxLength = 200)
    {
        return new Booking(
            title: "Test",
            userId: Guid.NewGuid(),
            roomId: Guid.NewGuid(),
            date: new DateTime(2026, 11, 4),
            startTime: TimeSpan.FromHours(10),
            endTime: TimeSpan.FromHours(11),
            rules: rules ?? DefaultRules,
            titleMaxLength: titleMaxLength);
    }

    [Fact]
    public void SetTimeInterval_WithCustomStep30_AcceptsStep30()
    {
        var rules = new BookingTimeRules
        {
            WorkingHoursStart = TimeSpan.FromHours(9),
            WorkingHoursEnd = TimeSpan.FromHours(18),
            TimeStepMinutes = 30
        };

        var booking = CreateBooking(rules);

        // Act — set time with 30-min step
        booking.SetTimeInterval(
            TimeSpan.FromHours(10),
            TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(30)),
            rules);

        Assert.Equal(TimeSpan.FromHours(10), booking.StartTime);
        Assert.Equal(TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(30)), booking.EndTime);
    }

    [Fact]
    public void SetTimeInterval_WithCustomStep30_RejectsStep15()
    {
        var rules = new BookingTimeRules
        {
            WorkingHoursStart = TimeSpan.FromHours(9),
            WorkingHoursEnd = TimeSpan.FromHours(18),
            TimeStepMinutes = 30
        };

        var booking = CreateBooking(rules);

        var ex = Assert.Throws<ArgumentException>(() =>
            booking.SetTimeInterval(
                TimeSpan.FromHours(10),
                TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(15)),
                rules));

        Assert.Contains("30", ex.Message);
    }

    [Fact]
    public void SetTitle_WithMaxLength10_Accepts10Chars()
    {
        var booking = CreateBooking(titleMaxLength: 10);

        booking.SetTitle("1234567890", 10);

        Assert.Equal("1234567890", booking.Title);
    }

    [Fact]
    public void SetTitle_WithMaxLength10_Rejects11Chars()
    {
        var booking = CreateBooking(titleMaxLength: 10);

        var ex = Assert.Throws<ArgumentException>(() =>
            booking.SetTitle("12345678901", 10));

        Assert.Contains("10", ex.Message);
    }
}
