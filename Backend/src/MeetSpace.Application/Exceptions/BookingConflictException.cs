namespace MeetSpace.Application.Exceptions;

public class BookingConflictException : ApplicationException
{
    public Guid RoomId { get; }
    public DateTime Date { get; }
    public TimeSpan StartTime { get; }
    public TimeSpan EndTime { get; }

    public BookingConflictException(Guid roomId, DateTime date, TimeSpan startTime, TimeSpan endTime)
        : base($"Room '{roomId}' is already booked on {date:yyyy-MM-dd} from {startTime:hh\\:mm} to {endTime:hh\\:mm}.")
    {
        RoomId = roomId;
        Date = date;
        StartTime = startTime;
        EndTime = endTime;
    }
}
