namespace MeetSpace.Application.Models;

public class CalendarBookingDto
{
    public Guid Id { get; init; }
    public Guid RoomId { get; init; }
    public string RoomName { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }

    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? UserFirstName { get; init; }
    public string? UserLastName { get; init; }
    public bool IsOwn { get; init; }
}
