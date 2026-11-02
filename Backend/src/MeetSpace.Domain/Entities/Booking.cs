using MeetSpace.Domain.Rules;

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

    public Booking(
        string title,
        Guid userId,
        Guid roomId,
        DateTime date,
        TimeSpan startTime,
        TimeSpan endTime,
        BookingTimeRules rules,
        int titleMaxLength,
        string? description = null,
        int? descriptionMaxLength = null)
    {
        Id = Guid.NewGuid();
        SetTitle(title, titleMaxLength);
        UserId = userId;
        RoomId = roomId;
        SetDate(date);
        SetTimeInterval(startTime, endTime, rules);
        if (description != null)
            SetDescription(description, descriptionMaxLength ?? 0);
    }

    public void SetTitle(string title, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (title.Length > maxLength)
            throw new ArgumentException($"Title must not exceed {maxLength} characters.");
        Title = title;
    }

    public void SetDescription(string? description, int maxLength)
    {
        if (description != null && description.Length > maxLength)
            throw new ArgumentException($"Description must not exceed {maxLength} characters.");
        Description = description;
    }

    public void SetDate(DateTime date)
    {
        if (date.Kind == DateTimeKind.Local)
            date = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
        Date = date.Date;
    }

    public void SetTimeInterval(TimeSpan startTime, TimeSpan endTime, BookingTimeRules rules)
    {
        if (endTime <= startTime)
            throw new ArgumentException("End time must be later than start time.");

        if (startTime < rules.WorkingHoursStart)
            throw new ArgumentException($"Start time must not be earlier than {rules.WorkingHoursStart:hh\\:mm}.");

        if (endTime > rules.WorkingHoursEnd)
            throw new ArgumentException($"End time must not be later than {rules.WorkingHoursEnd:hh\\:mm}.");

        if (startTime.Minutes % rules.TimeStepMinutes != 0)
            throw new ArgumentException($"Start time minutes must be a multiple of {rules.TimeStepMinutes}.");

        if (endTime.Minutes % rules.TimeStepMinutes != 0)
            throw new ArgumentException($"End time minutes must be a multiple of {rules.TimeStepMinutes}.");

        StartTime = startTime;
        EndTime = endTime;
    }
}
