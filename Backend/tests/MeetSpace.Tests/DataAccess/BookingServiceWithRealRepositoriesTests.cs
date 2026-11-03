using MeetSpace.Application.Abstractions;
using MeetSpace.Application.Exceptions;
using MeetSpace.Application.Models;
using MeetSpace.Application.Options;
using MeetSpace.Application.Services;
using MeetSpace.DataAccess;
using MeetSpace.Domain.Entities;
using MeetSpace.Domain.Enums;
using MeetSpace.Domain.Rules;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace MeetSpace.Tests.DataAccess;

/// <summary>
/// Integration tests for BookingService using REAL repositories (BookingRepository + RoomRepository)
/// on SQLite in-memory. Only ICurrentUserContext and ITimeProvider are mocked.
/// </summary>
public class BookingServiceWithRealRepositoriesTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    private static readonly BookingOptions DefaultBookingOptions = new()
    {
        WorkingHoursStart = TimeSpan.FromHours(9),
        WorkingHoursEnd = TimeSpan.FromHours(18),
        BookingTimeStepMinutes = 15
    };

    private static readonly ValidationOptions DefaultValidationOptions = new()
    {
        UserNameMaxLength = 100,
        EmailMaxLength = 200,
        EquipmentNameMaxLength = 100,
        RoomNameMaxLength = 200,
        RoomDescriptionMaxLength = 2000,
        BookingTitleMaxLength = 200,
        BookingDescriptionMaxLength = 2000
    };

    private const int UserNameMaxLength = 100;
    private const int EmailMaxLength = 200;

    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RoomId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherRoomId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid BookingId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static readonly DateTime FixedNow = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime FutureDate = new(2026, 11, 15, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime OtherDate = new(2026, 11, 20, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime PastDate = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly TimeSpan StartTime = TimeSpan.FromHours(10);
    private static readonly TimeSpan EndTime = TimeSpan.FromHours(11);

    public BookingServiceWithRealRepositoriesTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddMeetSpaceDataAccessSqlite(_connection);

        // Configure options
        services.AddSingleton(Options.Create(DefaultBookingOptions));
        services.AddSingleton(Options.Create(DefaultValidationOptions));

        // Mock external dependencies
        var userCtxMock = new Moq.Mock<ICurrentUserContext>();
        var timeMock = new Moq.Mock<ITimeProvider>();

        timeMock.Setup(t => t.GetCurrentTime()).Returns(FixedNow);

        services.AddSingleton(userCtxMock.Object);
        services.AddSingleton(timeMock.Object);
        services.AddSingleton<BookingService>();

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<MeetSpaceDbContext>();
        ctx.Database.EnsureCreated();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // ── Helpers ──

    private MeetSpaceDbContext CreateDbContext()
    {
        return _provider.GetRequiredService<MeetSpaceDbContext>();
    }

    private BookingService CreateService()
    {
        return _provider.GetRequiredService<BookingService>();
    }

    private MeetSpaceDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<MeetSpaceDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new MeetSpaceDbContext(options);
    }

    private async Task SeedUserAsync(Guid id, string firstName = "John", string lastName = "Doe",
        UserRole role = UserRole.User)
    {
        var ctx = CreateDbContext();
        var email = $"{firstName.ToLower()}.{id.ToString().Substring(0, 8)}@test.com";
        var user = new User(firstName, lastName, email, "hash",
            UserNameMaxLength, EmailMaxLength, role);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
    }

    private async Task SeedRoomAsync(Guid id, string name = "Room A")
    {
        var ctx = CreateDbContext();
        var room = new Room(name, 10, 1, 200);
        typeof(Room).GetProperty(nameof(Room.Id))!.SetValue(room, id);
        ctx.Rooms.Add(room);
        await ctx.SaveChangesAsync();
    }

    private static BookingRequestDto CreateRequest(
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

    // ═══════════════════════════════════════════════════════════════════
    // CreateBookingAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateBooking_ValidRequest_CreatesAndPersists()
    {
        // Arrange
        await SeedUserAsync(UserId, "Alice", "Smith");
        await SeedRoomAsync(RoomId, "Main Hall");

        // Override mocks
        var userCtx = _provider.GetRequiredService<ICurrentUserContext>();
        Mock.Get(userCtx)
            .Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserId);

        var svc = CreateService();
        var request = CreateRequest(title: "Scrum", description: "Daily");

        // Act
        var result = await svc.CreateBookingAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Scrum", result.Title);
        Assert.Equal("Daily", result.Description);
        Assert.Equal(UserId, result.UserId);
        Assert.Equal(RoomId, result.RoomId);
        Assert.Equal(FutureDate.Date, result.Date);
        Assert.Equal(StartTime, result.StartTime);
        Assert.Equal(EndTime, result.EndTime);

        // Verify persisted
        using var freshCtx = NewContext();
        var persisted = await freshCtx.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == result.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Scrum", persisted!.Title);
    }

    [Fact]
    public async Task CreateBooking_Overlap_ThrowsBookingConflictException()
    {
        // Arrange
        await SeedUserAsync(UserId, "Alice", "Smith");
        await SeedUserAsync(OtherUserId, "Bob", "Jones");
        await SeedRoomAsync(RoomId, "Main Hall");

        // First, create a booking directly via repository
        var bookingCtx = CreateDbContext();
        var existingBooking = new Booking("Blocked", OtherUserId, RoomId,
            FutureDate.Date, StartTime, EndTime,
            new BookingTimeRules
            {
                WorkingHoursStart = TimeSpan.FromHours(9),
                WorkingHoursEnd = TimeSpan.FromHours(18),
                TimeStepMinutes = 15
            }, 200);
        bookingCtx.Bookings.Add(existingBooking);
        await bookingCtx.SaveChangesAsync();

        var userCtx = _provider.GetRequiredService<ICurrentUserContext>();
        Mock.Get(userCtx)
            .Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserId);

        var svc = CreateService();
        var request = CreateRequest(start: StartTime, end: EndTime); // Same time as existing

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BookingConflictException>(() => svc.CreateBookingAsync(request));
        Assert.Equal(RoomId, ex.RoomId);
        Assert.Equal(FutureDate.Date, ex.Date);

        // Verify no booking was added
        using var freshCtx = NewContext();
        var count = await freshCtx.Bookings.AsNoTracking().CountAsync();
        Assert.Equal(1, count); // Only the original one
    }

    [Fact]
    public async Task CreateBooking_AdjacentNoOverlap_Succeeds()
    {
        // Arrange
        await SeedUserAsync(UserId, "Alice", "Smith");
        await SeedRoomAsync(RoomId, "Main Hall");

        // Create existing [09:00, 10:00)
        var bookingCtx = CreateDbContext();
        var existingBooking = new Booking("Existing", UserId, RoomId,
            FutureDate.Date, TimeSpan.FromHours(9), TimeSpan.FromHours(10),
            new BookingTimeRules
            {
                WorkingHoursStart = TimeSpan.FromHours(9),
                WorkingHoursEnd = TimeSpan.FromHours(18),
                TimeStepMinutes = 15
            }, 200);
        bookingCtx.Bookings.Add(existingBooking);
        await bookingCtx.SaveChangesAsync();

        var userCtx = _provider.GetRequiredService<ICurrentUserContext>();
        Mock.Get(userCtx)
            .Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserId);

        var svc = CreateService();
        // New booking [10:00, 11:00) — adjacent, no overlap
        var request = CreateRequest(start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(11));

        // Act
        var result = await svc.CreateBookingAsync(request);

        // Assert
        Assert.NotNull(result);

        // Verify both exist
        using var freshCtx = NewContext();
        var count = await freshCtx.Bookings.AsNoTracking().CountAsync();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task CreateBooking_RoomNotFound_ThrowsNotFoundException()
    {
        // Arrange
        await SeedUserAsync(UserId);

        var userCtx = _provider.GetRequiredService<ICurrentUserContext>();
        Mock.Get(userCtx)
            .Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserId);

        var svc = CreateService();
        var nonExistentRoom = Guid.NewGuid();
        var request = CreateRequest(roomId: nonExistentRoom);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => svc.CreateBookingAsync(request));
        Assert.Equal("Room", ex.EntityName);
        Assert.Equal(nonExistentRoom, ex.EntityId);
    }

    [Fact]
    public async Task CreateBooking_PastDate_ThrowsValidationException()
    {
        // Arrange
        await SeedUserAsync(UserId);
        await SeedRoomAsync(RoomId);

        var userCtx = _provider.GetRequiredService<ICurrentUserContext>();
        Mock.Get(userCtx)
            .Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserId);

        var svc = CreateService();
        var request = CreateRequest(date: PastDate);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.CreateBookingAsync(request));
        Assert.Contains("past", ex.Message.ToLower());
    }

    // ═══════════════════════════════════════════════════════════════════
    // UpdateBookingAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateBooking_ChangeTime_Succeeds()
    {
        // Arrange
        await SeedUserAsync(UserId, "Alice", "Smith");
        await SeedRoomAsync(RoomId, "Main Hall");

        // Create booking via service
        var userCtx = _provider.GetRequiredService<ICurrentUserContext>();
        var mock = Mock.Get(userCtx);
        mock.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserId);
        mock.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.User);

        var svc = CreateService();
        var createResult = await svc.CreateBookingAsync(
            CreateRequest(start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(11), title: "Original"));

        // Update with new time
        var updateRequest = CreateRequest(
            start: TimeSpan.FromHours(14), end: TimeSpan.FromHours(15), title: "Updated Time");

        // Act
        var updateResult = await svc.UpdateBookingAsync(createResult.Id, updateRequest);

        // Assert
        Assert.Equal("Updated Time", updateResult.Title);
        Assert.Equal(TimeSpan.FromHours(14), updateResult.StartTime);
        Assert.Equal(TimeSpan.FromHours(15), updateResult.EndTime);

        // Verify persisted
        using var freshCtx = NewContext();
        var persisted = await freshCtx.Bookings
            .AsNoTracking()
            .Include(b => b.User)
            .Include(b => b.Room)
            .FirstOrDefaultAsync(b => b.Id == createResult.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Updated Time", persisted!.Title);
        Assert.Equal(TimeSpan.FromHours(14), persisted!.StartTime);
    }

    [Fact]
    public async Task UpdateBooking_ChangeRoom_Succeeds()
    {
        // Arrange
        await SeedUserAsync(UserId, "Alice", "Smith");
        await SeedRoomAsync(RoomId, "Room Alpha");
        await SeedRoomAsync(OtherRoomId, "Room Beta");

        var userCtx = _provider.GetRequiredService<ICurrentUserContext>();
        var mock = Mock.Get(userCtx);
        mock.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserId);
        mock.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.User);

        var svc = CreateService();
        var createResult = await svc.CreateBookingAsync(
            CreateRequest(roomId: RoomId, title: "Original Room"));

        // Update with different room
        var updateRequest = CreateRequest(roomId: OtherRoomId, title: "Moved Room");

        // Act
        var updateResult = await svc.UpdateBookingAsync(createResult.Id, updateRequest);

        // Assert
        Assert.Equal(OtherRoomId, updateResult.RoomId);
        Assert.Equal("Room Beta", updateResult.RoomName);

        // Verify persisted
        using var freshCtx = NewContext();
        var persisted = await freshCtx.Bookings
            .AsNoTracking()
            .Include(b => b.Room)
            .FirstOrDefaultAsync(b => b.Id == createResult.Id);
        Assert.NotNull(persisted);
        Assert.Equal(OtherRoomId, persisted!.RoomId);
        Assert.Equal("Room Beta", persisted!.Room!.Name);
    }

    [Fact]
    public async Task UpdateBooking_ConflictWithOther_ThrowsBookingConflictException()
    {
        // Arrange
        await SeedUserAsync(UserId, "Alice", "Smith");
        await SeedUserAsync(OtherUserId, "Bob", "Jones");
        await SeedRoomAsync(RoomId, "Main Hall");

        var userCtx = _provider.GetRequiredService<ICurrentUserContext>();
        var mock = Mock.Get(userCtx);
        mock.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserId);
        mock.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.User);

        var svc = CreateService();

        // Create my booking at [10:00-11:00)
        var myBooking = await svc.CreateBookingAsync(
            CreateRequest(title: "My Booking"));

        // Create another user's booking at [14:00-15:00) via direct repo
        var ctx = CreateDbContext();
        ctx.Bookings.Add(new Booking("Other Booking", OtherUserId, RoomId,
            FutureDate.Date, TimeSpan.FromHours(14), TimeSpan.FromHours(15),
            new BookingTimeRules
            {
                WorkingHoursStart = TimeSpan.FromHours(9),
                WorkingHoursEnd = TimeSpan.FromHours(18),
                TimeStepMinutes = 15
            }, 200));
        await ctx.SaveChangesAsync();

        // Try to move my booking to [14:00-15:00) — conflicts with other
        var updateRequest = CreateRequest(
            start: TimeSpan.FromHours(14), end: TimeSpan.FromHours(15), title: "Moved");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BookingConflictException>(
            () => svc.UpdateBookingAsync(myBooking.Id, updateRequest));

        // Verify my booking NOT changed
        using var freshCtx = NewContext();
        var persisted = await freshCtx.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == myBooking.Id);
        Assert.NotNull(persisted);
        Assert.Equal("My Booking", persisted!.Title);
        Assert.Equal(TimeSpan.FromHours(10), persisted!.StartTime);
        Assert.Equal(TimeSpan.FromHours(11), persisted!.EndTime);
    }

    [Fact]
    public async Task UpdateBooking_OwnTimeSlot_SameTime_NoSelfConflict()
    {
        // Arrange
        await SeedUserAsync(UserId, "Alice", "Smith");
        await SeedRoomAsync(RoomId, "Main Hall");

        var userCtx = _provider.GetRequiredService<ICurrentUserContext>();
        var mock = Mock.Get(userCtx);
        mock.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserId);
        mock.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.User);

        var svc = CreateService();

        var createResult = await svc.CreateBookingAsync(
            CreateRequest(title: "Original"));

        // Update with same time, different title — should NOT conflict with itself
        var updateRequest = CreateRequest(title: "Same Time New Title");

        // Act
        var updateResult = await svc.UpdateBookingAsync(createResult.Id, updateRequest);

        // Assert
        Assert.Equal("Same Time New Title", updateResult.Title);
        Assert.Equal(StartTime, updateResult.StartTime);
        Assert.Equal(EndTime, updateResult.EndTime);
    }

    // ═══════════════════════════════════════════════════════════════════
    // DeleteBookingAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteBooking_ExistingBooking_RemovesIt()
    {
        // Arrange
        await SeedUserAsync(UserId, "Alice", "Smith");
        await SeedRoomAsync(RoomId, "Main Hall");

        var userCtx = _provider.GetRequiredService<ICurrentUserContext>();
        var mock = Mock.Get(userCtx);
        mock.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserId);
        mock.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.User);

        var svc = CreateService();
        var createResult = await svc.CreateBookingAsync(
            CreateRequest(title: "To Delete"));

        // Act
        await svc.DeleteBookingAsync(createResult.Id);

        // Assert
        using var freshCtx = NewContext();
        var deleted = await freshCtx.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == createResult.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteBooking_NonExisting_ThrowsNotFoundException()
    {
        // Arrange
        await SeedUserAsync(UserId, "Alice", "Smith");

        var userCtx = _provider.GetRequiredService<ICurrentUserContext>();
        var mock = Mock.Get(userCtx);
        mock.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserId);
        mock.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.User);

        var svc = CreateService();
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => svc.DeleteBookingAsync(nonExistentId));
        Assert.Equal("Booking", ex.EntityName);
        Assert.Equal(nonExistentId, ex.EntityId);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetMyBookingsAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetMyBookings_WithBookings_ReturnsThemWithUserAndRoom()
    {
        // Arrange
        await SeedUserAsync(UserId, "Alice", "Smith");
        await SeedUserAsync(OtherUserId, "Bob", "Jones");
        await SeedRoomAsync(RoomId, "Main Hall");

        var userCtx = _provider.GetRequiredService<ICurrentUserContext>();
        Mock.Get(userCtx)
            .Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserId);

        var svc = CreateService();

        // Create booking for current user
        await svc.CreateBookingAsync(CreateRequest(title: "My First"));
        await svc.CreateBookingAsync(CreateRequest(
            start: TimeSpan.FromHours(14), end: TimeSpan.FromHours(15), title: "My Second"));

        // Create booking for other user (via direct repo)
        var ctx = CreateDbContext();
        ctx.Bookings.Add(new Booking("Other's", OtherUserId, RoomId,
            FutureDate.Date, TimeSpan.FromHours(16), TimeSpan.FromHours(17),
            new BookingTimeRules
            {
                WorkingHoursStart = TimeSpan.FromHours(9),
                WorkingHoursEnd = TimeSpan.FromHours(18),
                TimeStepMinutes = 15
            }, 200));
        await ctx.SaveChangesAsync();

        // Act
        var results = await svc.GetMyBookingsAsync();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, b => Assert.Equal(UserId, b.UserId));
        Assert.All(results, b => Assert.Equal("Alice", b.UserFirstName));
        Assert.All(results, b => Assert.Equal("Smith", b.UserLastName));
        Assert.All(results, b => Assert.Equal("Main Hall", b.RoomName));
    }

    [Fact]
    public async Task GetMyBookings_NoBookings_ReturnsEmpty()
    {
        // Arrange
        await SeedUserAsync(UserId);

        var userCtx = _provider.GetRequiredService<ICurrentUserContext>();
        Mock.Get(userCtx)
            .Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserId);

        var svc = CreateService();

        // Act
        var results = await svc.GetMyBookingsAsync();

        // Assert
        Assert.Empty(results);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetCalendarBookingsAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetCalendarBookings_InRange_ReturnsBookings()
    {
        // Arrange
        await SeedUserAsync(UserId, "Alice", "Smith");
        await SeedRoomAsync(RoomId, "Main Hall");

        var userCtx = _provider.GetRequiredService<ICurrentUserContext>();
        var mock = Mock.Get(userCtx);
        mock.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserId);
        mock.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.User);

        var svc = CreateService();

        // Create bookings on different dates
        await svc.CreateBookingAsync(
            CreateRequest(date: FutureDate, title: "Day 15"));
        await svc.CreateBookingAsync(
            CreateRequest(date: OtherDate, title: "Day 20"));

        // Act – query both dates
        var results = await svc.GetCalendarBookingsAsync(
            DateOnly.FromDateTime(FutureDate), DateOnly.FromDateTime(OtherDate));

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, b => Assert.True(b.IsOwn));
        Assert.All(results, b => Assert.Equal("Main Hall", b.RoomName));
    }

    // ═══════════════════════════════════════════════════════════════════
    // CheckAvailabilityAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CheckAvailability_RoomFree_ReturnsAvailable()
    {
        // Arrange
        await SeedRoomAsync(RoomId, "Main Hall");

        var svc = CreateService();

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
        await SeedUserAsync(UserId, "Alice", "Smith");
        await SeedRoomAsync(RoomId, "Main Hall");

        var userCtx = _provider.GetRequiredService<ICurrentUserContext>();
        Mock.Get(userCtx)
            .Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserId);

        var svc = CreateService();

        // Create a booking
        var created = await svc.CreateBookingAsync(
            CreateRequest(start: StartTime, end: EndTime));

        // Act
        var result = await svc.CheckAvailabilityAsync(RoomId, FutureDate, StartTime, EndTime);

        // Assert
        Assert.False(result.IsAvailable);
        Assert.NotNull(result.Conflicts);
        Assert.Single(result.Conflicts);
        Assert.Equal(created.Id, result.Conflicts!.First().BookingId);
    }

    [Fact]
    public async Task CheckAvailability_RoomNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var svc = CreateService();
        var nonExistentRoom = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.CheckAvailabilityAsync(nonExistentRoom, FutureDate, StartTime, EndTime));
        Assert.Equal("Room", ex.EntityName);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Access control (real repos, different users)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteBooking_OtherUsersBooking_ThrowsAccessDenied()
    {
        // Arrange
        await SeedUserAsync(UserId, "Regular", "User", UserRole.User);
        await SeedUserAsync(OtherUserId, "Other", "Person", UserRole.User);
        await SeedRoomAsync(RoomId, "Main Hall");

        var userCtx = _provider.GetRequiredService<ICurrentUserContext>();
        var mock = Mock.Get(userCtx);

        // First create a booking as OtherUserId
        mock.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OtherUserId);
        mock.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.User);

        var svc = CreateService();
        var otherBooking = await svc.CreateBookingAsync(
            CreateRequest(title: "Other's Booking"));

        // Now switch to UserId
        mock.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AccessDeniedException>(
            () => svc.DeleteBookingAsync(otherBooking.Id));
        Assert.Contains("permission", ex.Message.ToLower());
    }

    [Fact]
    public async Task DeleteBooking_ManagerDeletesOtherBooking_Succeeds()
    {
        // Arrange
        await SeedUserAsync(UserId, "Manager", "User", UserRole.Manager);
        await SeedUserAsync(OtherUserId, "Regular", "User", UserRole.User);
        await SeedRoomAsync(RoomId, "Main Hall");

        var userCtx = _provider.GetRequiredService<ICurrentUserContext>();
        var mock = Mock.Get(userCtx);

        // Create booking as OtherUserId
        mock.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OtherUserId);
        mock.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.User);

        var svc = CreateService();
        var otherBooking = await svc.CreateBookingAsync(
            CreateRequest(title: "Other's Booking"));

        // Switch to Manager
        mock.Setup(c => c.GetCurrentUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserId);
        mock.Setup(c => c.GetCurrentUserRoleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Manager);

        // Act
        await svc.DeleteBookingAsync(otherBooking.Id);

        // Assert – booking is gone
        using var freshCtx = NewContext();
        var deleted = await freshCtx.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == otherBooking.Id);
        Assert.Null(deleted);
    }
}
