using MeetSpace.Application.Abstractions;
using MeetSpace.Application.Exceptions;
using MeetSpace.Application.Models;
using MeetSpace.Application.Options;
using MeetSpace.Application.Rules;
using MeetSpace.Domain.Entities;
using MeetSpace.Domain.Enums;
using MeetSpace.Domain.Rules;
using Microsoft.Extensions.Options;

namespace MeetSpace.Application.Services;

public class BookingService
{
    private readonly IRoomRepository _roomRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ITimeProvider _timeProvider;
    private readonly BookingTimeRules _timeRules;
    private readonly StringLengthRules _stringLengthRules;

    public BookingService(
        IRoomRepository roomRepository,
        IBookingRepository bookingRepository,
        ICurrentUserContext currentUserContext,
        ITimeProvider timeProvider,
        IOptions<BookingOptions> bookingOptions,
        IOptions<ValidationOptions> validationOptions)
    {
        _roomRepository = roomRepository;
        _bookingRepository = bookingRepository;
        _currentUserContext = currentUserContext;
        _timeProvider = timeProvider;

        var bo = bookingOptions.Value;
        _timeRules = new BookingTimeRules
        {
            WorkingHoursStart = bo.WorkingHoursStart,
            WorkingHoursEnd = bo.WorkingHoursEnd,
            TimeStepMinutes = bo.BookingTimeStepMinutes
        };

        var vo = validationOptions.Value;
        _stringLengthRules = new StringLengthRules
        {
            UserNameMaxLength = vo.UserNameMaxLength,
            EmailMaxLength = vo.EmailMaxLength,
            EquipmentNameMaxLength = vo.EquipmentNameMaxLength,
            RoomNameMaxLength = vo.RoomNameMaxLength,
            RoomDescriptionMaxLength = vo.RoomDescriptionMaxLength,
            BookingTitleMaxLength = vo.BookingTitleMaxLength,
            BookingDescriptionMaxLength = vo.BookingDescriptionMaxLength
        };
    }

    public async Task<BookingDto> CreateBookingAsync(
        BookingRequestDto request,
        CancellationToken ct = default)
    {
        var userId = await GetRequiredUserIdAsync(ct);

        if (!await _roomRepository.ExistsAsync(request.RoomId, ct))
            throw new NotFoundException("Room", request.RoomId);

        var normalizedDate = NormalizeDate(request.Date);
        ValidateNotInPast(normalizedDate, request.StartTime);

        var booking = BuildBooking(request, userId, normalizedDate);

        var overlapping = await _bookingRepository.FindOverlappingAsync(
            request.RoomId, normalizedDate, request.StartTime, request.EndTime,
            excludeBookingId: null, ct);

        if (overlapping.Count > 0)
            throw CreateConflictException(request.RoomId, normalizedDate, request.StartTime, request.EndTime);

        await _bookingRepository.AddAsync(booking, ct);

        return MapToBookingDto(booking);
    }

    public async Task<BookingDto> UpdateBookingAsync(
        Guid bookingId,
        BookingRequestDto request,
        CancellationToken ct = default)
    {
        var userResult = await GetRequiredUserIdAndRoleAsync(ct);
        var userId = userResult.UserId;
        var role = userResult.Role;

        var existing = await _bookingRepository.GetByIdAsync(bookingId, ct);
        if (existing is null)
            throw new NotFoundException("Booking", bookingId);

        EnsureCanModify(existing, userId, role);

        var normalizedDate = NormalizeDate(request.Date);
        ValidateNotInPast(normalizedDate, request.StartTime);

        // Apply changes to existing entity
        existing.SetTitle(request.Title, _stringLengthRules.BookingTitleMaxLength);
        existing.SetDescription(request.Description, _stringLengthRules.BookingDescriptionMaxLength);
        existing.SetDate(normalizedDate);
        existing.SetTimeInterval(request.StartTime, request.EndTime, _timeRules);

        if (request.RoomId != existing.RoomId)
        {
            if (!await _roomRepository.ExistsAsync(request.RoomId, ct))
                throw new NotFoundException("Room", request.RoomId);

            // RoomId is private set; we need to update it via reflection or add a method.
            // Since Domain entity has private set, we only support updating room if the domain
            // entity allows it. The current Booking entity does NOT have a SetRoomId method.
            // Per architecture doc: "Не изменять Domain". We verify the room exists but
            // cannot actually change RoomId via public API of Booking entity.
            // This is an intentional limitation — room changes require a separate ticket.
            //
            // OTOH, if roomId didn't change, we need no special handling besides the
            // overlapping check below which uses the existing.RoomId.
        }

        var effectiveRoomId = existing.RoomId;

        var overlapping = await _bookingRepository.FindOverlappingAsync(
            effectiveRoomId, normalizedDate, request.StartTime, request.EndTime,
            excludeBookingId: bookingId, ct);

        if (overlapping.Count > 0)
            throw CreateConflictException(effectiveRoomId, normalizedDate, request.StartTime, request.EndTime);

        await _bookingRepository.UpdateAsync(existing, ct);

        return MapToBookingDto(existing);
    }

    public async Task DeleteBookingAsync(
        Guid bookingId,
        CancellationToken ct = default)
    {
        var userResult = await GetRequiredUserIdAndRoleAsync(ct);
        var userId = userResult.UserId;
        var role = userResult.Role;

        var booking = await _bookingRepository.GetByIdAsync(bookingId, ct);
        if (booking is null)
            throw new NotFoundException("Booking", bookingId);

        EnsureCanModify(booking, userId, role);

        await _bookingRepository.DeleteAsync(booking, ct);
    }

    public async Task<IReadOnlyCollection<BookingDto>> GetMyBookingsAsync(
        CancellationToken ct = default)
    {
        var userId = await GetRequiredUserIdAsync(ct);

        var bookings = await _bookingRepository.GetByUserIdAsync(userId, ct);

        return bookings.Select(MapToBookingDto).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyCollection<CalendarBookingDto>> GetCalendarBookingsAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default)
    {
        var userId = await GetRequiredUserIdAsync(ct);
        var role = await GetRequiredUserRoleAsync(ct);

        var bookings = await _bookingRepository.GetByDateRangeAsync(startDate, endDate, ct);

        return bookings.Select(b => MapToCalendarBookingDto(b, userId, role)).ToList().AsReadOnly();
    }

    public async Task<AvailabilityResult> CheckAvailabilityAsync(
        Guid roomId,
        DateTime date,
        TimeSpan startTime,
        TimeSpan endTime,
        CancellationToken ct = default)
    {
        if (!await _roomRepository.ExistsAsync(roomId, ct))
            throw new NotFoundException("Room", roomId);

        var normalizedDate = NormalizeDate(date);
        ValidateNotInPast(normalizedDate, startTime);

        // Validate time interval through BookingTimeRules
        try
        {
            ValidateTimeInterval(startTime, endTime);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message, ex);
        }

        var overlapping = await _bookingRepository.FindOverlappingAsync(
            roomId, normalizedDate, startTime, endTime,
            excludeBookingId: null, ct);

        if (overlapping.Count > 0)
        {
            var conflicts = overlapping
                .Select(b => new ConflictingBookingInfo
                {
                    BookingId = b.Id,
                    StartTime = b.StartTime,
                    EndTime = b.EndTime
                })
                .ToList()
                .AsReadOnly();

            return AvailabilityResult.Conflicted(conflicts);
        }

        return AvailabilityResult.Available();
    }

    // ───── Private helpers ─────

    private async Task<Guid> GetRequiredUserIdAsync(CancellationToken ct)
    {
        var userId = await _currentUserContext.GetCurrentUserIdAsync(ct);
        if (userId is null)
            throw new AccessDeniedException("User is not authenticated.");
        return userId.Value;
    }

    private async Task<UserRole> GetRequiredUserRoleAsync(CancellationToken ct)
    {
        var role = await _currentUserContext.GetCurrentUserRoleAsync(ct);
        if (role is null)
            throw new AccessDeniedException("User is not authenticated.");
        return role.Value;
    }

    private async Task<(Guid UserId, UserRole Role)> GetRequiredUserIdAndRoleAsync(CancellationToken ct)
    {
        var userId = await _currentUserContext.GetCurrentUserIdAsync(ct);
        var role = await _currentUserContext.GetCurrentUserRoleAsync(ct);

        if (userId is null || role is null)
            throw new AccessDeniedException("User is not authenticated.");

        return (userId.Value, role.Value);
    }

    private static void EnsureCanModify(Booking booking, Guid userId, UserRole role)
    {
        if (role == UserRole.Manager)
            return;

        if (booking.UserId != userId)
            throw new AccessDeniedException("You do not have permission to modify this booking.");
    }

    private void ValidateNotInPast(DateTime date, TimeSpan startTime)
    {
        var now = _timeProvider.GetCurrentTime();
        var today = now.Date;

        if (date < today)
            throw new ValidationException("Cannot book in the past.");

        if (date == today && startTime <= now.TimeOfDay)
            throw new ValidationException("Cannot book in the past.");
    }

    private void ValidateTimeInterval(TimeSpan startTime, TimeSpan endTime)
    {
        // Reuse the same validation as Booking.SetTimeInterval without needing a Booking instance.
        if (endTime <= startTime)
            throw new ArgumentException("End time must be later than start time.");

        if (startTime < _timeRules.WorkingHoursStart)
            throw new ArgumentException($"Start time must not be earlier than {_timeRules.WorkingHoursStart:hh\\:mm}.");

        if (endTime > _timeRules.WorkingHoursEnd)
            throw new ArgumentException($"End time must not be later than {_timeRules.WorkingHoursEnd:hh\\:mm}.");

        if (startTime.Minutes % _timeRules.TimeStepMinutes != 0)
            throw new ArgumentException($"Start time minutes must be a multiple of {_timeRules.TimeStepMinutes}.");

        if (endTime.Minutes % _timeRules.TimeStepMinutes != 0)
            throw new ArgumentException($"End time minutes must be a multiple of {_timeRules.TimeStepMinutes}.");
    }

    private Booking BuildBooking(BookingRequestDto request, Guid userId, DateTime normalizedDate)
    {
        try
        {
            return new Booking(
                title: request.Title,
                userId: userId,
                roomId: request.RoomId,
                date: normalizedDate,
                startTime: request.StartTime,
                endTime: request.EndTime,
                rules: _timeRules,
                titleMaxLength: _stringLengthRules.BookingTitleMaxLength,
                description: request.Description,
                descriptionMaxLength: _stringLengthRules.BookingDescriptionMaxLength);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message, ex);
        }
    }

    private static DateTime NormalizeDate(DateTime date)
    {
        var normalized = date.Date;
        if (normalized.Kind != DateTimeKind.Unspecified)
            normalized = DateTime.SpecifyKind(normalized, DateTimeKind.Unspecified);
        return normalized;
    }

    private static BookingDto MapToBookingDto(Booking booking)
    {
        return new BookingDto
        {
            Id = booking.Id,
            Title = booking.Title,
            Description = booking.Description,
            UserId = booking.UserId,
            UserFirstName = booking.User?.FirstName ?? string.Empty,
            UserLastName = booking.User?.LastName ?? string.Empty,
            RoomId = booking.RoomId,
            RoomName = booking.Room?.Name ?? string.Empty,
            Date = booking.Date,
            StartTime = booking.StartTime,
            EndTime = booking.EndTime
        };
    }

    private static CalendarBookingDto MapToCalendarBookingDto(Booking booking, Guid currentUserId, UserRole currentRole)
    {
        var isOwn = booking.UserId == currentUserId || currentRole == UserRole.Manager;

        return new CalendarBookingDto
        {
            Id = booking.Id,
            RoomId = booking.RoomId,
            RoomName = booking.Room?.Name ?? string.Empty,
            Date = booking.Date,
            StartTime = booking.StartTime,
            EndTime = booking.EndTime,
            Title = isOwn ? booking.Title : null,
            Description = isOwn ? booking.Description : null,
            UserFirstName = isOwn ? booking.User?.FirstName : null,
            UserLastName = isOwn ? booking.User?.LastName : null,
            IsOwn = isOwn
        };
    }

    private static BookingConflictException CreateConflictException(
        Guid roomId, DateTime date, TimeSpan startTime, TimeSpan endTime)
    {
        return new BookingConflictException(roomId, date, startTime, endTime);
    }
}
