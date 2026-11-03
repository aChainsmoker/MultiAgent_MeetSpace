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
        Assert.True(dto.IsOwn); // Manager sees everything as own
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
}
