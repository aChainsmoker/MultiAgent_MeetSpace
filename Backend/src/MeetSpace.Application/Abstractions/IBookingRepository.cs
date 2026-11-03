using MeetSpace.Domain.Entities;

namespace MeetSpace.Application.Abstractions;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid bookingId, CancellationToken ct = default);

    Task<IReadOnlyCollection<Booking>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyCollection<Booking>> GetByDateRangeAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);

    Task<IReadOnlyCollection<Booking>> FindOverlappingAsync(
        Guid roomId,
        DateTime date,
        TimeSpan startTime,
        TimeSpan endTime,
        Guid? excludeBookingId,
        CancellationToken ct = default);

    Task AddAsync(Booking booking, CancellationToken ct = default);

    Task UpdateAsync(Booking booking, CancellationToken ct = default);

    Task DeleteAsync(Booking booking, CancellationToken ct = default);
}
