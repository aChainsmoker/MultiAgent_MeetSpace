namespace MeetSpace.Application.Options;

public class BookingOptions
{
    public TimeSpan WorkingHoursStart { get; set; }
    public TimeSpan WorkingHoursEnd { get; set; }
    public int BookingTimeStepMinutes { get; set; }
}
