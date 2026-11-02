namespace MeetSpace.Domain.Rules;

public class BookingTimeRules
{
    public TimeSpan WorkingHoursStart { get; init; }
    public TimeSpan WorkingHoursEnd { get; init; }
    public int TimeStepMinutes { get; init; }
}
