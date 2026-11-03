namespace MeetSpace.Application.Models;

public class BookingRequestDto
{
    public Guid RoomId { get; init; }
    public DateTime Date { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
}
