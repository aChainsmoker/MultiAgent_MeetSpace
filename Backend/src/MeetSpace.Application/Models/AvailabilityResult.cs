namespace MeetSpace.Application.Models;

public class AvailabilityResult
{
    public bool IsAvailable { get; init; }
    public IReadOnlyCollection<ConflictingBookingInfo>? Conflicts { get; init; }

    public static AvailabilityResult Available() =>
        new() { IsAvailable = true, Conflicts = null };

    public static AvailabilityResult Conflicted(IReadOnlyCollection<ConflictingBookingInfo> conflicts) =>
        new() { IsAvailable = false, Conflicts = conflicts };
}

public class ConflictingBookingInfo
{
    public Guid BookingId { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
}
