namespace MeetSpace.Domain.Entities;

public class Booking
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoomId { get; private set; }
    public DateTime Date { get; private set; }
    public TimeSpan StartTime { get; private set; }
    public TimeSpan EndTime { get; private set; }

    public User User { get; private set; } = null!;
    public Room Room { get; private set; } = null!;

    private Booking() { }

    public Booking(string title, Guid userId, Guid roomId, DateTime date, TimeSpan startTime, TimeSpan endTime)
    {
        Id = Guid.NewGuid();
        SetTitle(title);
        UserId = userId;
        RoomId = roomId;
        SetDate(date);
        SetTimeInterval(startTime, endTime);
    }

    public void SetTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (title.Length > Constants.DomainConstants.BookingTitleMaxLength)
            throw new ArgumentException($"Title must not exceed {Constants.DomainConstants.BookingTitleMaxLength} characters.");
        Title = title;
    }

    public void SetDescription(string? description)
    {
        if (description != null && description.Length > Constants.DomainConstants.BookingDescriptionMaxLength)
            throw new ArgumentException($"Description must not exceed {Constants.DomainConstants.BookingDescriptionMaxLength} characters.");
        Description = description;
    }

    public void SetDate(DateTime date)
    {
        if (date.Kind == DateTimeKind.Local)
            date = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
        Date = date.Date;
    }

    public void SetTimeInterval(TimeSpan startTime, TimeSpan endTime)
    {
        if (endTime <= startTime)
            throw new ArgumentException("End time must be later than start time.");

        if (startTime < Constants.DomainConstants.WorkingHoursStart)
            throw new ArgumentException($"Start time must not be earlier than {Constants.DomainConstants.WorkingHoursStart:hh\\:mm}.");

        if (endTime > Constants.DomainConstants.WorkingHoursEnd)
            throw new ArgumentException($"End time must not be later than {Constants.DomainConstants.WorkingHoursEnd:hh\\:mm}.");

        if (startTime.Minutes % Constants.DomainConstants.BookingTimeStepMinutes != 0)
            throw new ArgumentException($"Start time minutes must be a multiple of {Constants.DomainConstants.BookingTimeStepMinutes}.");

        if (endTime.Minutes % Constants.DomainConstants.BookingTimeStepMinutes != 0)
            throw new ArgumentException($"End time minutes must be a multiple of {Constants.DomainConstants.BookingTimeStepMinutes}.");

        StartTime = startTime;
        EndTime = endTime;
    }
}
