using MeetSpace.Application.Abstractions;
using MeetSpace.Application.Exceptions;
using MeetSpace.Application.Models;
using MeetSpace.Application.Options;
using MeetSpace.Application.Services;
using MeetSpace.Domain.Entities;
using MeetSpace.Domain.Enums;
using MeetSpace.Domain.Rules;
using Microsoft.Extensions.Options;
using Moq;

namespace MeetSpace.Tests.Application.Services;

public class BookingServiceTests
{
    // ─── Defaults ───
    private static readonly DateTime FixedNow = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime FutureDate = new(2026, 11, 15, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime PastDate = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RoomId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherRoomId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid BookingId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly TimeSpan StartTime = TimeSpan.FromHours(10);
    private static readonly TimeSpan EndTime = TimeSpan.FromHours(11);

    // ─── Helper: Create BookingService with mocks ───
    private (BookingService Service, Mock<IRoomRepository> RoomRepo, Mock<IBookingRepository> BookingRepo,
            Mock<ICurrentUserContext> UserCtx, Mock<ITimeProvider> Time)
        CreateService(
            BookingOptions? bookingOpts = null,
            ValidationOptions? validationOpts = null,
            DateTime? fixedTime = null)
    {
        var roomRepo = new Mock<IRoomRepository>();
        var bookingRepo = new Mock<IBookingRepository>();
        var userCtx = new Mock<ICurrentUserContext>();
        var timeProvider = new Mock<ITimeProvider>();

        var bo = Options.Create(bookingOpts ?? new BookingOptions
        {
            WorkingHoursStart = TimeSpan.FromHours(9),
            WorkingHoursEnd = TimeSpan.FromHours(18),
            BookingTimeStepMinutes = 15
        });

        var vo = Options.Create(validationOpts ?? new ValidationOptions
        {
            UserNameMaxLength = 100,
            EmailMaxLength = 200,
            EquipmentNameMaxLength = 100,
            RoomNameMaxLength = 100,
            RoomDescriptionMaxLength = 500,
            BookingTitleMaxLength = 200,
            BookingDescriptionMaxLength = 2000
        });

        timeProvider.Setup(t => t.GetCurrentTime()).Returns(fixedTime ?? FixedNow);

        var service = new BookingService(roomRepo.Object, bookingRepo.Object, userCtx.Object, timeProvider.Object, bo, vo);
        return (service, roomRepo, bookingRepo, userCtx, timeProvider);
    }

    private static BookingRequestDto CreateValidRequest(
        Guid? roomId = null, DateTime? date = null, TimeSpan? start = null, TimeSpan? end = null,
        string title = "Test Meeting", string? description = null)
    {
        return new BookingRequestDto
        {
            RoomId = roomId ?? RoomId,
            Date = date ?? FutureDate,
            StartTime = start ?? StartTime,
            EndTime = end ?? EndTime,
            Title = title,
            Description = description
        };
    }

    private static Booking CreateBookingEntity(
        Guid? bookingId = null, Guid? userId = null, Guid? roomId = null,
        DateTime? date = null, TimeSpan? start = null, TimeSpan? end = null,
        string title = "Test Meeting", string? description = null,
        User? user = null, Room? room = null,
        BookingTimeRules? rules = null, int titleMaxLength = 200)
    {
        var b = new Booking(
            title: title,
            userId: userId ?? UserId,
            roomId: roomId ?? RoomId,
            date: date ?? FutureDate,
            startTime: start ?? StartTime,
            endTime: end ?? EndTime,
            rules: rules ?? new BookingTimeRules { WorkingHoursStart = TimeSpan.FromHours(9), WorkingHoursEnd = TimeSpan.FromHours(18), TimeStepMinutes = 15 },
            titleMaxLength: titleMaxLength,
            description: description,
            descriptionMaxLength: 2000);

        if (bookingId.HasValue)
        {
            typeof(Booking).GetProperty(nameof(Booking.Id))!.SetValue(b, bookingId.Value);
        }

        if (user != null)
        {
            typeof(Booking).GetProperty(nameof(Booking.User))!.SetValue(b, user);
        }

        if (room != null)
        {
            typeof(Booking).GetProperty(nameof(Booking.Room))!.SetValue(b, room);
        }

        return b;
    }

    private static User CreateUser(Guid? id = null, string firstName = "John", string lastName = "Doe",
        UserRole role = UserRole.User)
    {
        var user = new User(firstName, lastName, "john@test.com", "hash", 100, 200, role);
        if (id.HasValue)
        {
            typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id.Value);
        }
        return user;
    }

    private static Room CreateRoom(Guid? id = null, string name = "Room A")
    {
        var room = new Room(name, 10, 1, 100);
        if (id.HasValue)
        {
            typeof(Room).GetProperty(nameof(Room.Id))!.SetValue(room, id.Value);
        }
        return room;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 1–8: SUCCESS SCENARIOS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateBooking_ValidRequest_CreatesBooking()
    {
        // Arrange
        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, StartTime, EndTime, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        // Act
        var result = await svc.CreateBookingAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Title, result.Title);
        Assert.Equal(UserId, result.UserId);
        Assert.Equal(RoomId, result.RoomId);
        Assert.Equal(FutureDate, result.Date);
        Assert.Equal(StartTime, result.StartTime);
        Assert.Equal(EndTime, result.EndTime);

        bookingRepo.Verify(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateBooking_UserUpdatesOwnBooking_Succeeds()
    {
        // Arrange
        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest(start: TimeSpan.FromHours(11), end: TimeSpan.FromHours(12), title: "Updated");

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var user = CreateUser(id: UserId);
        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId, user: user);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, TimeSpan.FromHours(11), TimeSpan.FromHours(12), BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        // Act
        var result = await svc.UpdateBookingAsync(BookingId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated", result.Title);
        Assert.Equal(TimeSpan.FromHours(11), result.StartTime);
        Assert.Equal(TimeSpan.FromHours(12), result.EndTime);

        bookingRepo.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateBooking_ManagerUpdatesOtherBooking_Succeeds()
    {
        // Arrange
        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest(title: "Manager Edit");

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.Manager);

        var owner = CreateUser(id: OtherUserId);
        var existing = CreateBookingEntity(bookingId: BookingId, userId: OtherUserId, user: owner);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, StartTime, EndTime, BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        // Act
        var result = await svc.UpdateBookingAsync(BookingId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Manager Edit", result.Title);
        bookingRepo.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteBooking_UserDeletesOwnBooking_Succeeds()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Act
        await svc.DeleteBookingAsync(BookingId);

        // Assert
        bookingRepo.Verify(r => r.DeleteAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteBooking_ManagerDeletesOtherBooking_Succeeds()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.Manager);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: OtherUserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Act
        await svc.DeleteBookingAsync(BookingId);

        // Assert
        bookingRepo.Verify(r => r.DeleteAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMyBookings_ReturnsList()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var user = CreateUser(id: UserId, firstName: "Alice", lastName: "Smith");
        var room = CreateRoom(id: RoomId, name: "Room A");
        var booking1 = CreateBookingEntity(bookingId: Guid.NewGuid(), userId: UserId, user: user, room: room);
        var booking2 = CreateBookingEntity(bookingId: Guid.NewGuid(), userId: UserId, user: user, room: room,
            start: TimeSpan.FromHours(14), end: TimeSpan.FromHours(15), title: "Meeting 2");

        bookingRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { booking1, booking2 });

        // Act
        var result = await svc.GetMyBookingsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, b => b.Title == "Test Meeting");
        Assert.Contains(result, b => b.Title == "Meeting 2");
        Assert.All(result, b => Assert.Equal(UserId, b.UserId));
        Assert.All(result, b => Assert.Equal("Alice", b.UserFirstName));
        Assert.All(result, b => Assert.Equal("Smith", b.UserLastName));
        Assert.All(result, b => Assert.Equal("Room A", b.RoomName));
    }

    [Fact]
    public async Task GetCalendarBookings_OwnAndOthers_DifferentVisibility()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var ownUser = CreateUser(id: UserId, firstName: "Me", lastName: "Myself");
        var otherUser = CreateUser(id: OtherUserId, firstName: "Other", lastName: "Person");
        var room = CreateRoom(id: RoomId, name: "Room A");

        var ownBooking = CreateBookingEntity(userId: UserId, user: ownUser, room: room,
            title: "My Meeting", description: "Private");
        var otherBooking = CreateBookingEntity(userId: OtherUserId, user: otherUser, room: room,
            start: TimeSpan.FromHours(14), end: TimeSpan.FromHours(15),
            title: "Their Meeting", description: "Secret");

        bookingRepo.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { ownBooking, otherBooking });

        // Act
        var result = await svc.GetCalendarBookingsAsync(DateOnly.FromDateTime(FutureDate), DateOnly.FromDateTime(FutureDate));

        // Assert
        Assert.Equal(2, result.Count);

        var ownResult = result.Single(b => b.IsOwn);
        Assert.Equal("My Meeting", ownResult.Title);
        Assert.Equal("Private", ownResult.Description);
        Assert.Equal("Me", ownResult.UserFirstName);

        var otherResult = result.Single(b => !b.IsOwn);
        Assert.Null(otherResult.Title);
        Assert.Null(otherResult.Description);
        Assert.Null(otherResult.UserFirstName);
        Assert.Null(otherResult.UserLastName);
        Assert.False(otherResult.IsOwn);
    }

    [Fact]
    public async Task CheckAvailability_RoomFree_ReturnsAvailable()
    {
        // Arrange
        var (svc, roomRepo, bookingRepo, _, _) = CreateService();

        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, StartTime, EndTime, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        // Act
        var result = await svc.CheckAvailabilityAsync(RoomId, FutureDate, StartTime, EndTime);

        // Assert
        Assert.True(result.IsAvailable);
        Assert.Null(result.Conflicts);
    }

    [Fact]
    public async Task CheckAvailability_RoomOccupied_ReturnsConflicted()
    {
        // Arrange
        var (svc, roomRepo, bookingRepo, _, _) = CreateService();

        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var conflicting = CreateBookingEntity(bookingId: BookingId);
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, StartTime, EndTime, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { conflicting });

        // Act
        var result = await svc.CheckAvailabilityAsync(RoomId, FutureDate, StartTime, EndTime);

        // Assert
        Assert.False(result.IsAvailable);
        Assert.NotNull(result.Conflicts);
        Assert.Single(result.Conflicts);
        Assert.Equal(BookingId, result.Conflicts!.First().BookingId);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 9–13: VALIDATION SCENARIOS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateBooking_EndTimeEqualsStartTime_ThrowsValidationException()
    {
        // Arrange
        var (svc, roomRepo, _, userCtx, _) = CreateService();
        var request = CreateValidRequest(start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(10));

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.CreateBookingAsync(request));
        Assert.Contains("later", ex.Message.ToLower());
    }

    [Fact]
    public async Task CreateBooking_EndTimeBeforeStartTime_ThrowsValidationException()
    {
        // Arrange
        var (svc, roomRepo, _, userCtx, _) = CreateService();
        var request = CreateValidRequest(start: TimeSpan.FromHours(12), end: TimeSpan.FromHours(10));

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.CreateBookingAsync(request));
        Assert.Contains("later", ex.Message.ToLower());
    }

    [Fact]
    public async Task CreateBooking_BeforeWorkingHours_ThrowsValidationException()
    {
        // Arrange
        var (svc, roomRepo, _, userCtx, _) = CreateService();
        var request = CreateValidRequest(start: TimeSpan.FromHours(8), end: TimeSpan.FromHours(10));

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.CreateBookingAsync(request));
        Assert.Contains("earlier", ex.Message.ToLower());
    }

    [Fact]
    public async Task CreateBooking_AfterWorkingHours_ThrowsValidationException()
    {
        // Arrange
        var (svc, roomRepo, _, userCtx, _) = CreateService();
        var request = CreateValidRequest(start: TimeSpan.FromHours(17), end: TimeSpan.FromHours(19));

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.CreateBookingAsync(request));
        Assert.Contains("later", ex.Message.ToLower());
    }

    [Fact]
    public async Task CreateBooking_InvalidTimeStepMinutes_ThrowsValidationException()
    {
        // Arrange
        var (svc, roomRepo, _, userCtx, _) = CreateService();
        // Step is 15 minutes, so 10:07 is invalid
        var request = CreateValidRequest(start: TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(7)),
                                         end: TimeSpan.FromHours(11));

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.CreateBookingAsync(request));
        Assert.Contains("15", ex.Message);
    }

    [Fact]
    public async Task CreateBooking_PastDate_ThrowsValidationException()
    {
        // Arrange
        var (svc, roomRepo, _, userCtx, _) = CreateService();
        var request = CreateValidRequest(date: PastDate);

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.CreateBookingAsync(request));
        Assert.Contains("past", ex.Message.ToLower());
    }

    [Fact]
    public async Task CreateBooking_PastTimeOnToday_ThrowsValidationException()
    {
        // Arrange
        var now = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Unspecified);
        var (svc, roomRepo, _, userCtx, _) = CreateService(fixedTime: now);
        // Request for today but start time is before "now"
        var request = CreateValidRequest(date: now.Date, start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(11));

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.CreateBookingAsync(request));
        Assert.Contains("past", ex.Message.ToLower());
    }

    [Fact]
    public async Task CreateBooking_EmptyTitle_ThrowsValidationException()
    {
        // Arrange
        var (svc, roomRepo, _, userCtx, _) = CreateService();
        var request = CreateValidRequest(title: "");

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.CreateBookingAsync(request));
        // Domain throws ArgumentException for null/whitespace which gets wrapped
    }

    [Fact]
    public async Task CreateBooking_WhitespaceTitle_ThrowsValidationException()
    {
        // Arrange
        var (svc, roomRepo, _, userCtx, _) = CreateService();
        var request = CreateValidRequest(title: "   ");

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => svc.CreateBookingAsync(request));
    }

    // ═══════════════════════════════════════════════════════════════════
    // 14–19: OVERLAP / CONFLICT SCENARIOS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateBooking_ExactOverlap_ThrowsConflict()
    {
        // Arrange
        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest(start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(12));

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var existingBooking = CreateBookingEntity(start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(12));
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, TimeSpan.FromHours(10), TimeSpan.FromHours(12), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { existingBooking });

        // Act & Assert
        await Assert.ThrowsAsync<BookingConflictException>(() => svc.CreateBookingAsync(request));
    }

    [Fact]
    public async Task CreateBooking_PartialOverlap_ThrowsConflict()
    {
        // Arrange
        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService();
        // Requested: [10:00-12:00), Existing: [11:00-13:00) — partial overlap
        var request = CreateValidRequest(start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(12));

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var existingBooking = CreateBookingEntity(start: TimeSpan.FromHours(11), end: TimeSpan.FromHours(13));
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, TimeSpan.FromHours(10), TimeSpan.FromHours(12), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { existingBooking });

        // Act & Assert
        await Assert.ThrowsAsync<BookingConflictException>(() => svc.CreateBookingAsync(request));
    }

    [Fact]
    public async Task CreateBooking_NestedOverlap_ThrowsConflict()
    {
        // Arrange
        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService();
        // Requested: [10:00-13:00), Existing: [11:00-12:00) — nested inside
        var request = CreateValidRequest(start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(13));

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var existingBooking = CreateBookingEntity(start: TimeSpan.FromHours(11), end: TimeSpan.FromHours(12));
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, TimeSpan.FromHours(10), TimeSpan.FromHours(13), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { existingBooking });

        // Act & Assert
        await Assert.ThrowsAsync<BookingConflictException>(() => svc.CreateBookingAsync(request));
    }

    [Fact]
    public async Task CreateBooking_AdjacentBookings_NoConflict()
    {
        // Arrange
        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService();
        // Requested: [09:00-10:00), Existing: [10:00-11:00) — adjacent, no conflict
        var request = CreateValidRequest(start: TimeSpan.FromHours(9), end: TimeSpan.FromHours(10));

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, TimeSpan.FromHours(9), TimeSpan.FromHours(10), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        // Act
        var result = await svc.CreateBookingAsync(request);

        // Assert
        Assert.NotNull(result);
        bookingRepo.Verify(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateBooking_OtherRoom_NoConflict()
    {
        // Arrange
        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest(roomId: OtherRoomId);

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(OtherRoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        bookingRepo.Setup(r => r.FindOverlappingAsync(OtherRoomId, FutureDate, StartTime, EndTime, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        // Act
        var result = await svc.CreateBookingAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(OtherRoomId, result.RoomId);
        bookingRepo.Verify(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateBooking_OtherDate_NoConflict()
    {
        // Arrange
        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService();
        var otherDate = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Unspecified);
        var request = CreateValidRequest(date: otherDate);

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, otherDate, StartTime, EndTime, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        // Act
        var result = await svc.CreateBookingAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(otherDate, result.Date);
        bookingRepo.Verify(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 20: UPDATE EXCLUDES CURRENT BOOKING FROM OVERLAP CHECK
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateBooking_SameTime_NoSelfConflict()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        // User updates their booking but keeps the same time — should not conflict with itself
        var request = CreateValidRequest(); // Same StartTime/EndTime as existing

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        // No overlapping bookings (self is excluded via excludeBookingId)
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, StartTime, EndTime, BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        // Act
        var result = await svc.UpdateBookingAsync(BookingId, request);

        // Assert
        Assert.NotNull(result);
        bookingRepo.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 21–23: ACCESS CONTROL
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateBooking_UserModifiesOtherBooking_ThrowsAccessDenied()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        // Booking owned by OtherUserId
        var existing = CreateBookingEntity(bookingId: BookingId, userId: OtherUserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AccessDeniedException>(() => svc.UpdateBookingAsync(BookingId, request));
        Assert.Contains("permission", ex.Message.ToLower());
    }

    [Fact]
    public async Task DeleteBooking_UserDeletesOtherBooking_ThrowsAccessDenied()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: OtherUserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AccessDeniedException>(() => svc.DeleteBookingAsync(BookingId));
        Assert.Contains("permission", ex.Message.ToLower());
    }

    [Fact]
    public async Task CreateBooking_UnauthenticatedUser_ThrowsAccessDenied()
    {
        // Arrange
        var (svc, roomRepo, _, userCtx, _) = CreateService();
        var request = CreateValidRequest();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync((UserRole?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AccessDeniedException>(() => svc.CreateBookingAsync(request));
        Assert.Contains("authenticated", ex.Message.ToLower());
    }

    [Fact]
    public async Task DeleteBooking_UnauthenticatedUser_ThrowsAccessDenied()
    {
        // Arrange
        var (svc, _, _, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync((UserRole?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AccessDeniedException>(() => svc.DeleteBookingAsync(BookingId));
        Assert.Contains("authenticated", ex.Message.ToLower());
    }

    [Fact]
    public async Task UpdateBooking_UnauthenticatedUser_ThrowsAccessDenied()
    {
        // Arrange
        var (svc, _, _, userCtx, _) = CreateService();
        var request = CreateValidRequest();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync((UserRole?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AccessDeniedException>(() => svc.UpdateBookingAsync(BookingId, request));
        Assert.Contains("authenticated", ex.Message.ToLower());
    }

    // ═══════════════════════════════════════════════════════════════════
    // 24–26: NOT FOUND
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateBooking_RoomNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var (svc, roomRepo, _, userCtx, _) = CreateService();
        var request = CreateValidRequest();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => svc.CreateBookingAsync(request));
        Assert.Equal("Room", ex.EntityName);
        Assert.Equal(RoomId, ex.EntityId);
    }

    [Fact]
    public async Task UpdateBooking_BookingNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync((Booking?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => svc.UpdateBookingAsync(BookingId, request));
        Assert.Equal("Booking", ex.EntityName);
        Assert.Equal(BookingId, ex.EntityId);
    }

    [Fact]
    public async Task DeleteBooking_BookingNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync((Booking?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => svc.DeleteBookingAsync(BookingId));
        Assert.Equal("Booking", ex.EntityName);
        Assert.Equal(BookingId, ex.EntityId);
    }

    [Fact]
    public async Task CheckAvailability_RoomNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var (svc, roomRepo, _, _, _) = CreateService();

        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.CheckAvailabilityAsync(RoomId, FutureDate, StartTime, EndTime));
        Assert.Equal("Room", ex.EntityName);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 27–29: STATE PROTECTION — no write calls on error
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateBooking_ValidationError_DoesNotCallAddAsync()
    {
        // Arrange
        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest(start: TimeSpan.FromHours(12), end: TimeSpan.FromHours(10)); // invalid

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        await Assert.ThrowsAsync<ValidationException>(() => svc.CreateBookingAsync(request));

        // Assert
        bookingRepo.Verify(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateBooking_ConflictError_DoesNotCallAddAsync()
    {
        // Arrange
        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var overlapping = CreateBookingEntity();
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, StartTime, EndTime, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { overlapping });

        // Act
        await Assert.ThrowsAsync<BookingConflictException>(() => svc.CreateBookingAsync(request));

        // Assert
        bookingRepo.Verify(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteBooking_AccessDenied_DoesNotCallDeleteAsync()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: OtherUserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Act
        await Assert.ThrowsAsync<AccessDeniedException>(() => svc.DeleteBookingAsync(BookingId));

        // Assert
        bookingRepo.Verify(r => r.DeleteAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateBooking_AccessDenied_DoesNotCallUpdateAsync()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: OtherUserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Act
        await Assert.ThrowsAsync<AccessDeniedException>(() => svc.UpdateBookingAsync(BookingId, request));

        // Assert
        bookingRepo.Verify(r => r.UpdateAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 30: CUSTOM OPTIONS (step 30 minutes, working time 10:00-20:00)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateBooking_CustomBookingOptions_Step30Working10to20()
    {
        // Arrange
        var customOpts = new BookingOptions
        {
            WorkingHoursStart = TimeSpan.FromHours(10),
            WorkingHoursEnd = TimeSpan.FromHours(20),
            BookingTimeStepMinutes = 30
        };

        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService(bookingOpts: customOpts);

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Time within custom working hours, 30-min step
        var request = CreateValidRequest(start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(30)));

        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, TimeSpan.FromHours(10), TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(30)), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        // Act
        var result = await svc.CreateBookingAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TimeSpan.FromHours(10), result.StartTime);
        Assert.Equal(TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(30)), result.EndTime);
    }

    [Fact]
    public async Task CreateBooking_CustomStep30_Rejects15MinStep()
    {
        // Arrange
        var customOpts = new BookingOptions
        {
            WorkingHoursStart = TimeSpan.FromHours(10),
            WorkingHoursEnd = TimeSpan.FromHours(20),
            BookingTimeStepMinutes = 30
        };

        var (svc, roomRepo, _, userCtx, _) = CreateService(bookingOpts: customOpts);

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // 15-minute step is invalid with 30-min step setting
        var request = CreateValidRequest(start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(15)));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.CreateBookingAsync(request));
        Assert.Contains("30", ex.Message);
    }

    [Fact]
    public async Task CreateBooking_CustomWorkingHours_RejectsBeforeStart()
    {
        // Arrange
        var customOpts = new BookingOptions
        {
            WorkingHoursStart = TimeSpan.FromHours(10),
            WorkingHoursEnd = TimeSpan.FromHours(20),
            BookingTimeStepMinutes = 30
        };

        var (svc, roomRepo, _, userCtx, _) = CreateService(bookingOpts: customOpts);

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // 09:00 is before 10:00 working start
        var request = CreateValidRequest(start: TimeSpan.FromHours(9), end: TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(30)));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.CreateBookingAsync(request));
        Assert.Contains("10:00", ex.Message);
    }

    [Fact]
    public async Task CreateBooking_CustomWorkingHours_RejectsAfterEnd()
    {
        // Arrange
        var customOpts = new BookingOptions
        {
            WorkingHoursStart = TimeSpan.FromHours(10),
            WorkingHoursEnd = TimeSpan.FromHours(20),
            BookingTimeStepMinutes = 30
        };

        var (svc, roomRepo, _, userCtx, _) = CreateService(bookingOpts: customOpts);

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // 19:30-21:00 goes past 20:00
        var request = CreateValidRequest(start: TimeSpan.FromHours(19).Add(TimeSpan.FromMinutes(30)), end: TimeSpan.FromHours(21));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.CreateBookingAsync(request));
        Assert.Contains("20:00", ex.Message);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ADDITIONAL: DTO mapping tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateBooking_DtoHasCorrectUserData()
    {
        // Arrange
        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest(title: "Scrum", description: "Daily standup");

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, StartTime, EndTime, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        // Note: the booking returned is newly created, so User/Room nav properties are null
        // Mapping uses booking.User?.FirstName ?? string.Empty

        // Act
        var result = await svc.CreateBookingAsync(request);

        // Assert
        Assert.Equal("Scrum", result.Title);
        Assert.Equal("Daily standup", result.Description);
        Assert.Equal(UserId, result.UserId);
        Assert.Equal(RoomId, result.RoomId);
    }

    [Fact]
    public async Task GetMyBookings_WithUserAndRoomData_MapsCorrectly()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var user = CreateUser(id: UserId, firstName: "Bob", lastName: "Johnson");
        var room = CreateRoom(id: RoomId, name: "Conference B");
        var booking = CreateBookingEntity(bookingId: BookingId, userId: UserId, user: user, room: room,
            title: "Sprint Review", description: "Biweekly review");

        bookingRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { booking });

        // Act
        var result = await svc.GetMyBookingsAsync();

        // Assert
        var dto = Assert.Single(result);
        Assert.Equal(BookingId, dto.Id);
        Assert.Equal("Sprint Review", dto.Title);
        Assert.Equal("Biweekly review", dto.Description);
        Assert.Equal("Bob", dto.UserFirstName);
        Assert.Equal("Johnson", dto.UserLastName);
        Assert.Equal("Conference B", dto.RoomName);
    }

    [Fact]
    public async Task GetCalendarBookings_ManagerSeesAllDetails()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.Manager);

        var otherUser = CreateUser(id: OtherUserId, firstName: "Alice", lastName: "Wonder");
        var room = CreateRoom(id: RoomId, name: "Boardroom");
        var otherBooking = CreateBookingEntity(userId: OtherUserId, user: otherUser, room: room,
            title: "Board Meeting", description: "Q4 planning");

        bookingRepo.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { otherBooking });

        // Act
        var result = await svc.GetCalendarBookingsAsync(DateOnly.FromDateTime(FutureDate), DateOnly.FromDateTime(FutureDate));

        // Assert
        var dto = Assert.Single(result);
        Assert.False(dto.IsOwn); // FIX-04: Manager does not own other booking, but sees details
        Assert.Equal("Board Meeting", dto.Title);
        Assert.Equal("Q4 planning", dto.Description);
        Assert.Equal("Alice", dto.UserFirstName);
        Assert.Equal("Wonder", dto.UserLastName);
    }

    [Fact]
    public async Task GetCalendarBookings_IsOwnTrueForSelf()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var ownUser = CreateUser(id: UserId);
        var booking = CreateBookingEntity(userId: UserId, user: ownUser);

        bookingRepo.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { booking });

        // Act
        var result = await svc.GetCalendarBookingsAsync(DateOnly.FromDateTime(FutureDate), DateOnly.FromDateTime(FutureDate));

        // Assert
        var dto = Assert.Single(result);
        Assert.True(dto.IsOwn);
    }

    [Fact]
    public async Task CheckAvailability_ValidatesTimeInterval()
    {
        // Arrange
        var (svc, roomRepo, _, _, _) = CreateService();

        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act & Assert — invalid: end <= start
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            svc.CheckAvailabilityAsync(RoomId, FutureDate, TimeSpan.FromHours(12), TimeSpan.FromHours(10)));
        Assert.Contains("later", ex.Message.ToLower());
    }

    [Fact]
    public async Task CheckAvailability_PastDate_ThrowsValidationException()
    {
        // Arrange
        var (svc, roomRepo, _, _, _) = CreateService();

        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            svc.CheckAvailabilityAsync(RoomId, PastDate, StartTime, EndTime));
        Assert.Contains("past", ex.Message.ToLower());
    }

    [Fact]
    public async Task UpdateBooking_ChangesAllFields_Succeeds()
    {
        // Arrange
        var newDate = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Unspecified);
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest(
            date: newDate,
            start: TimeSpan.FromHours(14),
            end: TimeSpan.FromHours(15),
            title: "Updated Title",
            description: "Updated Description");

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var user = CreateUser(id: UserId);
        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId, user: user);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, newDate, TimeSpan.FromHours(14), TimeSpan.FromHours(15), BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        // Act
        var result = await svc.UpdateBookingAsync(BookingId, request);

        // Assert
        Assert.Equal("Updated Title", result.Title);
        Assert.Equal("Updated Description", result.Description);
        Assert.Equal(newDate, result.Date);
        Assert.Equal(TimeSpan.FromHours(14), result.StartTime);
        Assert.Equal(TimeSpan.FromHours(15), result.EndTime);

        bookingRepo.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateBooking_EndTimeMinuteNotMultipleOfStep_ThrowsValidationException()
    {
        // Arrange
        var (svc, roomRepo, _, userCtx, _) = CreateService();
        // Step=15, end at 11:07 is invalid
        var request = CreateValidRequest(
            start: TimeSpan.FromHours(10),
            end: TimeSpan.FromHours(11).Add(TimeSpan.FromMinutes(7)));

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.CreateBookingAsync(request));
        Assert.Contains("15", ex.Message);
    }

    [Fact]
    public async Task CreateBooking_DescriptionExceedsMaxLength_ThrowsValidationException()
    {
        // Arrange
        var (svc, roomRepo, _, userCtx, _) = CreateService(
            validationOpts: new ValidationOptions
            {
                UserNameMaxLength = 100,
                EmailMaxLength = 200,
                EquipmentNameMaxLength = 100,
                RoomNameMaxLength = 100,
                RoomDescriptionMaxLength = 500,
                BookingTitleMaxLength = 200,
                BookingDescriptionMaxLength = 10
            });

        var request = CreateValidRequest(description: "This description is way too long");

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.CreateBookingAsync(request));
        Assert.Contains("10", ex.Message);
    }

    [Fact]
    public async Task GetMyBookings_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        bookingRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        // Act
        var result = await svc.GetMyBookingsAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateBooking_NormalizesDateKind()
    {
        // Arrange
        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService();
        // Date with Utc kind should be normalized
        var utcDate = new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);
        var request = CreateValidRequest(date: utcDate);

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, It.IsAny<DateTime>(), StartTime, EndTime, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        // Act
        var result = await svc.CreateBookingAsync(request);

        // Assert
        Assert.Equal(DateTimeKind.Unspecified, result.Date.Kind);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REGRESSION: FIX-02 — Update error contract (ValidationException)
    // ═══════════════════════════════════════════════════════════════════
    // These tests SHOULD FAIL on current code:
    //   UpdateBookingAsync:101-104 calls SetTitle/SetDescription/SetTimeInterval
    //   without try/catch, so Domain ArgumentException leaks un-wrapped.
    // Expected: ValidationException. Actual (current): ArgumentException.

    [Fact]
    public async Task UpdateBooking_EmptyTitle_ThrowsValidationException()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest(title: "");

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Act & Assert — should be ValidationException, currently throws ArgumentException
        await Assert.ThrowsAsync<ValidationException>(() => svc.UpdateBookingAsync(BookingId, request));
    }

    [Fact]
    public async Task UpdateBooking_EndTimeBeforeStartTime_ThrowsValidationException()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest(start: TimeSpan.FromHours(12), end: TimeSpan.FromHours(10));

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Act & Assert — should be ValidationException, currently throws ArgumentException
        await Assert.ThrowsAsync<ValidationException>(() => svc.UpdateBookingAsync(BookingId, request));
    }

    [Fact]
    public async Task UpdateBooking_OutsideWorkingHours_ThrowsValidationException()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        // Start before 09:00
        var request = CreateValidRequest(start: TimeSpan.FromHours(8), end: TimeSpan.FromHours(10));

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => svc.UpdateBookingAsync(BookingId, request));
    }

    [Fact]
    public async Task UpdateBooking_InvalidTimeStep_ThrowsValidationException()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        // Step=15, 10:07 is invalid
        var request = CreateValidRequest(
            start: TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(7)),
            end: TimeSpan.FromHours(11));

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => svc.UpdateBookingAsync(BookingId, request));
    }

    [Fact]
    public async Task UpdateBooking_TitleTooLong_ThrowsValidationException()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService(
            validationOpts: new ValidationOptions
            {
                UserNameMaxLength = 100,
                EmailMaxLength = 200,
                EquipmentNameMaxLength = 100,
                RoomNameMaxLength = 100,
                RoomDescriptionMaxLength = 500,
                BookingTitleMaxLength = 5,
                BookingDescriptionMaxLength = 2000
            });
        var request = CreateValidRequest(title: "TooLongTitle");

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId, titleMaxLength: 200);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => svc.UpdateBookingAsync(BookingId, request));
    }

    [Fact]
    public async Task UpdateBooking_DescriptionTooLong_ThrowsValidationException()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService(
            validationOpts: new ValidationOptions
            {
                UserNameMaxLength = 100,
                EmailMaxLength = 200,
                EquipmentNameMaxLength = 100,
                RoomNameMaxLength = 100,
                RoomDescriptionMaxLength = 500,
                BookingTitleMaxLength = 200,
                BookingDescriptionMaxLength = 5
            });
        var request = CreateValidRequest(description: "Too long");

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => svc.UpdateBookingAsync(BookingId, request));
    }

    [Fact]
    public async Task UpdateBooking_PastDate_ThrowsValidationException()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest(date: PastDate);

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.UpdateBookingAsync(BookingId, request));
        Assert.Contains("past", ex.Message.ToLower());
    }

    // ═══════════════════════════════════════════════════════════════════
    // REGRESSION: FIX-03 — entity immutability on Update failure
    // ═══════════════════════════════════════════════════════════════════
    // These tests SHOULD FAIL on current code:
    //   UpdateBookingAsync:101-104 mutates existing BEFORE overlap check (124-129).
    //   When conflict occurs, entity is already partially mutated.
    //   Tests verify that after BookingConflictException, entity fields are unchanged.

    [Fact]
    public async Task UpdateBooking_ConflictError_DoesNotMutateEntity()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        var originalTitle = "Original Title";
        var originalDate = FutureDate;
        var originalStart = TimeSpan.FromHours(10);
        var originalEnd = TimeSpan.FromHours(11);
        var originalRoom = RoomId;

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(
            bookingId: BookingId, userId: UserId,
            title: originalTitle, date: originalDate,
            start: originalStart, end: originalEnd,
            roomId: originalRoom);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Request with different time that will conflict
        var request = CreateValidRequest(
            start: TimeSpan.FromHours(14), end: TimeSpan.FromHours(15),
            title: "New Title");

        var otherBooking = CreateBookingEntity(
            bookingId: Guid.NewGuid(), userId: OtherUserId,
            start: TimeSpan.FromHours(14), end: TimeSpan.FromHours(15));
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate,
                TimeSpan.FromHours(14), TimeSpan.FromHours(15), BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { otherBooking });

        // Act
        await Assert.ThrowsAsync<BookingConflictException>(
            () => svc.UpdateBookingAsync(BookingId, request));

        // Assert — entity MUST NOT be mutated after conflict
        Assert.Equal(originalTitle, existing.Title);
        Assert.Equal(originalDate, existing.Date);
        Assert.Equal(originalStart, existing.StartTime);
        Assert.Equal(originalEnd, existing.EndTime);
        Assert.Equal(originalRoom, existing.RoomId);
    }

    [Fact]
    public async Task UpdateBooking_ConflictError_DoesNotCallUpdateAsync()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var request = CreateValidRequest(start: TimeSpan.FromHours(14), end: TimeSpan.FromHours(15));

        var otherBooking = CreateBookingEntity(
            bookingId: Guid.NewGuid(), userId: OtherUserId,
            start: TimeSpan.FromHours(14), end: TimeSpan.FromHours(15));
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate,
                TimeSpan.FromHours(14), TimeSpan.FromHours(15), BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { otherBooking });

        // Act
        await Assert.ThrowsAsync<BookingConflictException>(
            () => svc.UpdateBookingAsync(BookingId, request));

        // Assert
        bookingRepo.Verify(r => r.UpdateAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateBooking_RoomNotFound_DoesNotMutateEntity()
    {
        // Arrange — Note: this test can only be fully verified after FIX-01 (SetRoomId).
        // Before SetRoomId, roomId is not mutated, so entity state is preserved anyway.
        // After SetRoomId, the entity should not be mutated when the new room doesn't exist.
        // For now, this test verifies that NotFoundException is thrown with correct data.
        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService();
        var originalTitle = "Original";
        var nonExistentRoomId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId, title: originalTitle);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        roomRepo.Setup(r => r.ExistsAsync(nonExistentRoomId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var request = CreateValidRequest(roomId: nonExistentRoomId, title: "New Title");

        // Act
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => svc.UpdateBookingAsync(BookingId, request));

        Assert.Equal("Room", ex.EntityName);
        Assert.Equal(nonExistentRoomId, ex.EntityId);

        // Entity should not be mutated
        Assert.Equal(originalTitle, existing.Title);
        Assert.Equal(RoomId, existing.RoomId);
    }

    [Fact]
    public async Task UpdateBooking_LateFieldError_DoesNotLeavePartialMutation()
    {
        // Arrange: SetTitle succeeds but SetTimeInterval fails (EndTime before StartTime).
        // After fix, NO mutation should occur because validation happens before mutation.
        // Before fix, title IS mutated because SetTitle runs before SetTimeInterval.
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        var originalTitle = "Original Title";

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId, title: originalTitle);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Valid title, invalid time (end before start)
        var request = CreateValidRequest(
            title: "New Title",
            start: TimeSpan.FromHours(12),
            end: TimeSpan.FromHours(10));

        // Act
        await Assert.ThrowsAsync<ValidationException>(
            () => svc.UpdateBookingAsync(BookingId, request));

        // Assert — title MUST NOT have changed (partial mutation)
        // NOTE: This assertion MAY FAIL on current code because SetTitle executes
        // before SetTimeInterval throws. If title IS "New Title", that's the bug.
        Assert.Equal(originalTitle, existing.Title);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REGRESSION: FIX-01 — Change room on Update
    // ═══════════════════════════════════════════════════════════════════
    // FIX-01 applied: SetRoomId added to Domain, tests enabled.
    [Fact]
    public async Task UpdateBooking_ChangeRoom_UpdatesRoomId()
    {
        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService();
        var newRoomId = OtherRoomId;
        var request = CreateValidRequest(roomId: newRoomId, title: "Moved");

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);
        roomRepo.Setup(r => r.ExistsAsync(newRoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        bookingRepo.Setup(r => r.FindOverlappingAsync(newRoomId, FutureDate, StartTime, EndTime, BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        var result = await svc.UpdateBookingAsync(BookingId, request);

        Assert.Equal(newRoomId, result.RoomId);
        bookingRepo.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateBooking_ChangeRoom_ChecksOverlapInNewRoom()
    {
        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService();
        var newRoomId = OtherRoomId;
        var request = CreateValidRequest(roomId: newRoomId);

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);
        roomRepo.Setup(r => r.ExistsAsync(newRoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Setup conflict in the NEW room
        var otherBooking = CreateBookingEntity(bookingId: Guid.NewGuid(), userId: OtherUserId, roomId: newRoomId);
        bookingRepo.Setup(r => r.FindOverlappingAsync(newRoomId, FutureDate, StartTime, EndTime, BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { otherBooking });

        await Assert.ThrowsAsync<BookingConflictException>(
            () => svc.UpdateBookingAsync(BookingId, request));
    }

    [Fact]
    public async Task UpdateBooking_ChangeRoom_NewRoomNotFound_ThrowsNotFoundException()
    {
        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService();
        var nonExistentRoomId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var request = CreateValidRequest(roomId: nonExistentRoomId);

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);
        roomRepo.Setup(r => r.ExistsAsync(nonExistentRoomId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => svc.UpdateBookingAsync(BookingId, request));
        Assert.Equal("Room", ex.EntityName);
        Assert.Equal(nonExistentRoomId, ex.EntityId);
    }

    [Fact]
    public async Task UpdateBooking_SameRoom_NoChange()
    {
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest(roomId: RoomId, title: "Same Room");

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId, roomId: RoomId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, StartTime, EndTime, BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        var result = await svc.UpdateBookingAsync(BookingId, request);

        Assert.Equal(RoomId, result.RoomId);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REGRESSION: FIX-04 — IsOwn for Manager
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetCalendarBookings_ManagerOwnBooking_IsOwnTrue()
    {
        // Arrange: Manager has their OWN booking — IsOwn should be true
        var (svc, _, bookingRepo, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.Manager);

        var managerUser = CreateUser(id: UserId, firstName: "Manager", lastName: "Self");
        var ownBooking = CreateBookingEntity(userId: UserId, user: managerUser,
            title: "My Own Meeting");

        bookingRepo.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { ownBooking });

        // Act
        var result = await svc.GetCalendarBookingsAsync(
            DateOnly.FromDateTime(FutureDate), DateOnly.FromDateTime(FutureDate));

        // Assert
        var dto = Assert.Single(result);
        Assert.True(dto.IsOwn); // Manager's OWN booking
        Assert.Equal("My Own Meeting", dto.Title);
    }

    [Fact]
    public async Task GetCalendarBookings_ManagerOtherBooking_IsOwnFalseButHasDetails()
    {
        // Arrange: Manager views someone else's booking
        // IsOwn should be FALSE (not owner), but details should be visible
        var (svc, _, bookingRepo, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.Manager);

        var otherUser = CreateUser(id: OtherUserId, firstName: "Alice", lastName: "Wonder");
        var otherBooking = CreateBookingEntity(userId: OtherUserId, user: otherUser,
            title: "Alice Meeting", description: "Alice details");

        bookingRepo.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { otherBooking });

        // Act
        var result = await svc.GetCalendarBookingsAsync(
            DateOnly.FromDateTime(FutureDate), DateOnly.FromDateTime(FutureDate));

        // Assert
        var dto = Assert.Single(result);
        // FIX: Current code sets IsOwn=true for Manager even for other's bookings.
        // After fix, IsOwn should be FALSE for other's bookings.
        Assert.False(dto.IsOwn, "Manager should have IsOwn=false for other people's bookings");
        // But details are still visible for Manager
        Assert.Equal("Alice Meeting", dto.Title);
        Assert.Equal("Alice details", dto.Description);
        Assert.Equal("Alice", dto.UserFirstName);
        Assert.Equal("Wonder", dto.UserLastName);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REGRESSION: FIX-05 — TotalMinutes for step validation, single source
    // ═══════════════════════════════════════════════════════════════════
    // Current code uses .Minutes % TimeStepMinutes (component 0-59).
    // For TimeStepMinutes=45: time 1:30 has .Minutes=30, 30%45=30≠0 — rejected.
    // With TotalMinutes: 90%45=0 — accepted.
    // These tests SHOULD FAIL on current code if step=45 is used with
    // times where Minutes component is not a multiple of 45.

    [Fact]
    public async Task CreateBooking_Step45_AcceptsTimeWithNonZeroMinutes()
    {
        // Arrange: TimeStep=45, time 10:45-11:30 (both have Minutes=45,30)
        // 9:45 = 585 mins, (585-540)%45=0 ✓. But .Minutes=45, 45%45=0 ✓
        // 10:30 = 630 mins, (630-540)%45=0 ✓. But .Minutes=30, 30%45=30 ≠ 0 ✗
        // Current code rejects 11:30 because EndTime.Minutes (30) % 45 ≠ 0.
        // After fix (TotalMinutes): 690 % 45 = 0 → accepted.
        var customOpts = new BookingOptions
        {
            WorkingHoursStart = TimeSpan.FromHours(9),
            WorkingHoursEnd = TimeSpan.FromHours(18),
            BookingTimeStepMinutes = 45
        };

        var (svc, roomRepo, bookingRepo, userCtx, _) = CreateService(bookingOpts: customOpts);

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // 9:45 = StartTime, 10:30 = EndTime — both align to 45-min grid
        var start = TimeSpan.FromHours(9).Add(TimeSpan.FromMinutes(45));  // 9:45
        var end = TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(30));   // 10:30
        var request = CreateValidRequest(start: start, end: end);

        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, start, end, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        // Act
        var result = await svc.CreateBookingAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(start, result.StartTime);
        Assert.Equal(end, result.EndTime);
        bookingRepo.Verify(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateBooking_Step45_RejectsTimeOffGrid()
    {
        // Arrange: TimeStep=45, time 10:15-11:00 → not on 45-min grid
        var customOpts = new BookingOptions
        {
            WorkingHoursStart = TimeSpan.FromHours(9),
            WorkingHoursEnd = TimeSpan.FromHours(18),
            BookingTimeStepMinutes = 45
        };

        var (svc, roomRepo, _, userCtx, _) = CreateService(bookingOpts: customOpts);

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // 10:15 — 15 minutes % 45 ≠ 0, should fail
        var request = CreateValidRequest(
            start: TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(15)),
            end: TimeSpan.FromHours(11));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.CreateBookingAsync(request));
        Assert.Contains("45", ex.Message);
    }

    [Fact]
    public async Task CheckAvailability_Step45_AcceptsTimeWithNonZeroMinutes()
    {
        // Same as CreateBooking but for CheckAvailability
        var customOpts = new BookingOptions
        {
            WorkingHoursStart = TimeSpan.FromHours(9),
            WorkingHoursEnd = TimeSpan.FromHours(18),
            BookingTimeStepMinutes = 45
        };

        var (svc, roomRepo, bookingRepo, _, _) = CreateService(bookingOpts: customOpts);

        roomRepo.Setup(r => r.ExistsAsync(RoomId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var start = TimeSpan.FromHours(9).Add(TimeSpan.FromMinutes(45));  // 9:45
        var end = TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(30));   // 10:30

        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, start, end, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        // Act
        var result = await svc.CheckAvailabilityAsync(RoomId, FutureDate, start, end);

        // Assert
        Assert.True(result.IsAvailable);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REGRESSION: FIX-06 — Calendar date range validation
    // ═══════════════════════════════════════════════════════════════════
    // These tests SHOULD FAIL on current code:
    //   GetCalendarBookingsAsync does not validate startDate > endDate.

    [Fact]
    public async Task GetCalendarBookings_StartDateAfterEndDate_ThrowsValidationException()
    {
        // Arrange
        var (svc, _, _, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var endDate = DateOnly.FromDateTime(FutureDate);
        var startDate = endDate.AddDays(1); // start after end

        // Act & Assert
        // Currently no validation → goes to repository. After fix → ValidationException.
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            svc.GetCalendarBookingsAsync(startDate, endDate));
        Assert.Contains("start", ex.Message.ToLower());
    }

    [Fact]
    public async Task GetCalendarBookings_SingleDayRange_ReturnsBookings()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var singleDate = DateOnly.FromDateTime(FutureDate);
        var booking = CreateBookingEntity(userId: UserId);

        bookingRepo.Setup(r => r.GetByDateRangeAsync(singleDate, singleDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { booking });

        // Act
        var result = await svc.GetCalendarBookingsAsync(singleDate, singleDate);

        // Assert
        Assert.Single(result);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REGRESSION: Auth coverage gaps
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetMyBookings_Unauthenticated_ThrowsAccessDenied()
    {
        // Arrange
        var (svc, _, _, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync((UserRole?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AccessDeniedException>(() => svc.GetMyBookingsAsync());
        Assert.Contains("authenticated", ex.Message.ToLower());
    }

    [Fact]
    public async Task GetCalendarBookings_Unauthenticated_ThrowsAccessDenied()
    {
        // Arrange
        var (svc, _, _, userCtx, _) = CreateService();

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync((UserRole?)null);

        var startDate = DateOnly.FromDateTime(FutureDate);
        var endDate = DateOnly.FromDateTime(FutureDate);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AccessDeniedException>(() =>
            svc.GetCalendarBookingsAsync(startDate, endDate));
        Assert.Contains("authenticated", ex.Message.ToLower());
    }

    // ═══════════════════════════════════════════════════════════════════
    // REGRESSION: Overlap exclusion and verify excludeBookingId
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateBooking_ExcludesSelfFromOverlap_VerifyExcludeBookingId()
    {
        // Arrange
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        var newStart = TimeSpan.FromHours(14);
        var newEnd = TimeSpan.FromHours(15);
        var request = CreateValidRequest(start: newStart, end: newEnd, title: "Updated");

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId);
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, newStart, newEnd, BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        // Act
        await svc.UpdateBookingAsync(BookingId, request);

        // Assert — verify excludeBookingId IS passed as BookingId
        bookingRepo.Verify(
            r => r.FindOverlappingAsync(RoomId, FutureDate, newStart, newEnd, BookingId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateBooking_DoesNotExcludeOtherConflictingBooking()
    {
        // Arrange: There is ANOTHER booking that conflicts (not self).
        // Should throw BookingConflictException.
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        var newStart = TimeSpan.FromHours(14);
        var newEnd = TimeSpan.FromHours(15);
        var request = CreateValidRequest(start: newStart, end: newEnd);

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId,
            start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(11));
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Another user's booking that overlaps with the NEW time
        var otherBooking = CreateBookingEntity(
            bookingId: Guid.NewGuid(), userId: OtherUserId,
            start: newStart, end: newEnd);
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate, newStart, newEnd, BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { otherBooking });

        // Act & Assert
        await Assert.ThrowsAsync<BookingConflictException>(
            () => svc.UpdateBookingAsync(BookingId, request));
    }

    // ═══════════════════════════════════════════════════════════════════
    // REGRESSION: Overlap in Update (full/partial/nested)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateBooking_Overlap_FullOverlap_ThrowsConflict()
    {
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest(start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(12));

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId,
            start: TimeSpan.FromHours(9), end: TimeSpan.FromHours(10));
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var other = CreateBookingEntity(bookingId: Guid.NewGuid(), userId: OtherUserId,
            start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(12));
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate,
                TimeSpan.FromHours(10), TimeSpan.FromHours(12), BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { other });

        await Assert.ThrowsAsync<BookingConflictException>(
            () => svc.UpdateBookingAsync(BookingId, request));
    }

    [Fact]
    public async Task UpdateBooking_Overlap_PartialOverlap_ThrowsConflict()
    {
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest(start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(12));

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId,
            start: TimeSpan.FromHours(9), end: TimeSpan.FromHours(10));
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var other = CreateBookingEntity(bookingId: Guid.NewGuid(), userId: OtherUserId,
            start: TimeSpan.FromHours(11), end: TimeSpan.FromHours(13));
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate,
                TimeSpan.FromHours(10), TimeSpan.FromHours(12), BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { other });

        await Assert.ThrowsAsync<BookingConflictException>(
            () => svc.UpdateBookingAsync(BookingId, request));
    }

    [Fact]
    public async Task UpdateBooking_Overlap_NestedOverlap_ThrowsConflict()
    {
        var (svc, _, bookingRepo, userCtx, _) = CreateService();
        var request = CreateValidRequest(start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(13));

        userCtx.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserId);
        userCtx.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(UserRole.User);

        var existing = CreateBookingEntity(bookingId: BookingId, userId: UserId,
            start: TimeSpan.FromHours(9), end: TimeSpan.FromHours(10));
        bookingRepo.Setup(r => r.GetByIdAsync(BookingId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var other = CreateBookingEntity(bookingId: Guid.NewGuid(), userId: OtherUserId,
            start: TimeSpan.FromHours(11), end: TimeSpan.FromHours(12));
        bookingRepo.Setup(r => r.FindOverlappingAsync(RoomId, FutureDate,
                TimeSpan.FromHours(10), TimeSpan.FromHours(13), BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { other });

        await Assert.ThrowsAsync<BookingConflictException>(
            () => svc.UpdateBookingAsync(BookingId, request));
    }
}
