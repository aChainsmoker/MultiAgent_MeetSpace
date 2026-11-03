namespace MeetSpace.Domain.Rules;

public class BookingTimeRules
{
    public TimeSpan WorkingHoursStart { get; init; }
    public TimeSpan WorkingHoursEnd { get; init; }
    public int TimeStepMinutes { get; init; }

    public void Validate(TimeSpan startTime, TimeSpan endTime)
    {
        if (endTime <= startTime)
            throw new ArgumentException("End time must be later than start time.");

        if (startTime < WorkingHoursStart)
            throw new ArgumentException($"Start time must not be earlier than {WorkingHoursStart:hh\\:mm}.");

        if (endTime > WorkingHoursEnd)
            throw new ArgumentException($"End time must not be later than {WorkingHoursEnd:hh\\:mm}.");

        if ((int)(startTime - WorkingHoursStart).TotalMinutes % TimeStepMinutes != 0)
            throw new ArgumentException($"Start time must be aligned to the {TimeStepMinutes}-minute grid.");

        if ((int)(endTime - WorkingHoursStart).TotalMinutes % TimeStepMinutes != 0)
            throw new ArgumentException($"End time must be aligned to the {TimeStepMinutes}-minute grid.");
    }
}
