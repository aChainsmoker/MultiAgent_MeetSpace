using MeetSpace.Application.Abstractions;
using MeetSpace.Application.Exceptions;
using MeetSpace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeetSpace.DataAccess.Repositories;

public class BookingRepository : IBookingRepository
{
    private readonly MeetSpaceDbContext _context;

    public BookingRepository(MeetSpaceDbContext context)
    {
        _context = context;
    }

    public async Task<Booking?> GetByIdAsync(Guid bookingId, CancellationToken ct = default)
    {
        // Tracked — BookingService.UpdateBookingAsync/DeleteBookingAsync mutates the entity
        return await _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Room)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);
    }

    public async Task<IReadOnlyCollection<Booking>> GetByUserIdAsync(
        Guid userId, CancellationToken ct = default)
    {
        return await _context.Bookings
            .AsNoTracking()
            .Include(b => b.User)
            .Include(b => b.Room)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.Date)
            .ThenBy(b => b.StartTime)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyCollection<Booking>> GetByDateRangeAsync(
        DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
    {
        var start = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var end = endDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        return await _context.Bookings
            .AsNoTracking()
            .Include(b => b.User)
            .Include(b => b.Room)
            .Where(b => b.Date >= start && b.Date <= end)
            .OrderBy(b => b.Date)
            .ThenBy(b => b.StartTime)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyCollection<Booking>> FindOverlappingAsync(
        Guid roomId, DateTime date, TimeSpan startTime, TimeSpan endTime,
        Guid? excludeBookingId, CancellationToken ct = default)
    {
        var query = _context.Bookings
            .AsNoTracking()
            .Where(b => b.RoomId == roomId)
            .Where(b => b.Date == date)
            .Where(b => b.StartTime < endTime && b.EndTime > startTime);

        if (excludeBookingId.HasValue)
            query = query.Where(b => b.Id != excludeBookingId.Value);

        return await query.ToListAsync(ct);
    }

    public async Task AddAsync(Booking booking, CancellationToken ct = default)
    {
        _context.Bookings.Add(booking);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsOverlapViolation(ex))
        {
            throw new BookingConflictException(
                booking.RoomId, booking.Date, booking.StartTime, booking.EndTime);
        }
    }

    public async Task UpdateAsync(Booking booking, CancellationToken ct = default)
    {
        var entry = _context.Entry(booking);

        if (entry.State == EntityState.Detached)
        {
            _context.Bookings.Attach(booking);
            entry.State = EntityState.Modified;
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BookingConflictException(
                booking.RoomId, booking.Date, booking.StartTime, booking.EndTime);
        }
        catch (DbUpdateException ex) when (IsOverlapViolation(ex))
        {
            throw new BookingConflictException(
                booking.RoomId, booking.Date, booking.StartTime, booking.EndTime);
        }
    }

    public async Task DeleteAsync(Booking booking, CancellationToken ct = default)
    {
        var entry = _context.Entry(booking);

        if (entry.State == EntityState.Detached)
            _context.Bookings.Attach(booking);

        _context.Bookings.Remove(booking);
        await _context.SaveChangesAsync(ct);
    }

    private static bool IsOverlapViolation(DbUpdateException ex)
    {
        // PostgreSQL exclusion constraint violation: SQLSTATE 23P01
        var sqlState = ex.InnerException?.Data["SqlState"] as string;
        return sqlState == "23P01";
    }
}
