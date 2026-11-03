using MeetSpace.Application.Abstractions;
using MeetSpace.DataAccess;
using MeetSpace.Domain.Entities;
using MeetSpace.Domain.Enums;
using MeetSpace.Domain.Rules;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MeetSpace.Tests.DataAccess;

public class BookingRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    private static readonly BookingTimeRules TimeRules = new()
    {
        WorkingHoursStart = TimeSpan.FromHours(9),
        WorkingHoursEnd = TimeSpan.FromHours(18),
        TimeStepMinutes = 15
    };

    private const int TitleMaxLength = 200;
    private const int DescriptionMaxLength = 2000;
    private const int UserNameMaxLength = 100;
    private const int EmailMaxLength = 200;

    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RoomId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherRoomId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid BookingId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static readonly DateTime TestDate = new(2026, 11, 15, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime OtherDate = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly TimeSpan StartTime = TimeSpan.FromHours(10);
    private static readonly TimeSpan EndTime = TimeSpan.FromHours(11);

    public BookingRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddMeetSpaceDataAccessSqlite(_connection);

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

    private IBookingRepository CreateRepository()
    {
        return _provider.GetRequiredService<IBookingRepository>();
    }

    private async Task SeedUserAsync(MeetSpaceDbContext ctx, Guid id, string firstName = "John", string lastName = "Doe",
        UserRole role = UserRole.User)
    {
        var email = $"{firstName.ToLower()}.{id.ToString().Substring(0, 8)}@test.com";
        var user = new User(firstName, lastName, email, "hash",
            UserNameMaxLength, EmailMaxLength, role);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
    }

    private async Task SeedRoomAsync(MeetSpaceDbContext ctx, Guid id, string name = "Room A")
    {
        var room = new Room(name, 10, 1, 200);
        typeof(Room).GetProperty(nameof(Room.Id))!.SetValue(room, id);
        ctx.Rooms.Add(room);
        await ctx.SaveChangesAsync();
    }

    private Booking CreateBookingEntity(
        Guid? id = null, Guid? userId = null, Guid? roomId = null,
        DateTime? date = null, TimeSpan? start = null, TimeSpan? end = null,
        string title = "Test Meeting", string? description = null)
    {
        var booking = new Booking(
            title: title,
            userId: userId ?? UserId,
            roomId: roomId ?? RoomId,
            date: date ?? TestDate,
            startTime: start ?? StartTime,
            endTime: end ?? EndTime,
            rules: TimeRules,
            titleMaxLength: TitleMaxLength,
            description: description,
            descriptionMaxLength: DescriptionMaxLength);

        if (id.HasValue)
            typeof(Booking).GetProperty(nameof(Booking.Id))!.SetValue(booking, id.Value);

        return booking;
    }

    private async Task<Booking> SeedBookingAsync(MeetSpaceDbContext ctx, Booking booking)
    {
        ctx.Bookings.Add(booking);
        await ctx.SaveChangesAsync();
        return booking;
    }

    private MeetSpaceDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<MeetSpaceDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new MeetSpaceDbContext(options);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetByIdAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetByIdAsync_ExistingBooking_ReturnsBookingWithNavigation()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId, "Alice", "Smith");
        await SeedRoomAsync(ctx, RoomId, "Conference Room");
        var booking = CreateBookingEntity(id: BookingId, userId: UserId, roomId: RoomId);
        await SeedBookingAsync(ctx, booking);

        var repo = CreateRepository();

        // Act
        var result = await repo.GetByIdAsync(BookingId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingId, result!.Id);
        Assert.Equal("Test Meeting", result.Title);
        Assert.Equal(UserId, result.UserId);
        Assert.Equal(RoomId, result.RoomId);
        Assert.Equal(TestDate, result.Date);
        Assert.Equal(StartTime, result.StartTime);
        Assert.Equal(EndTime, result.EndTime);

        // Navigation properties loaded
        Assert.NotNull(result.User);
        Assert.Equal("Alice", result.User!.FirstName);
        Assert.Equal("Smith", result.User!.LastName);

        Assert.NotNull(result.Room);
        Assert.Equal("Conference Room", result.Room!.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingBooking_ReturnsNull()
    {
        // Arrange
        var repo = CreateRepository();

        // Act
        var result = await repo.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_TrackedEntity_CanMutateAndSave()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId);
        var booking = CreateBookingEntity(id: BookingId, userId: UserId, roomId: RoomId);
        await SeedBookingAsync(ctx, booking);

        var repo = CreateRepository();

        // Act
        var tracked = await repo.GetByIdAsync(BookingId);
        Assert.NotNull(tracked);
        tracked!.SetTitle("Updated Title", 200);
        await repo.UpdateAsync(tracked);

        // Assert – verify through new DbContext (no ChangeTracker)
        using var freshCtx = NewContext();
        var reloaded = await freshCtx.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == BookingId);
        Assert.NotNull(reloaded);
        Assert.Equal("Updated Title", reloaded!.Title);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetByUserIdAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetByUserIdAsync_WithBookings_ReturnsOrderedByDateDesc()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId, "Bob", "Marley");
        await SeedRoomAsync(ctx, RoomId, "Room X");
        await SeedRoomAsync(ctx, OtherRoomId, "Room Y");

        var booking1 = CreateBookingEntity(id: Guid.NewGuid(), userId: UserId, roomId: RoomId,
            date: TestDate, start: TimeSpan.FromHours(9), end: TimeSpan.FromHours(10), title: "First");
        var booking2 = CreateBookingEntity(id: Guid.NewGuid(), userId: UserId, roomId: OtherRoomId,
            date: OtherDate, start: TimeSpan.FromHours(14), end: TimeSpan.FromHours(15), title: "Second");
        await SeedBookingAsync(ctx, booking1);
        await SeedBookingAsync(ctx, booking2);

        var repo = CreateRepository();

        // Act
        var result = await repo.GetByUserIdAsync(UserId);

        // Assert
        Assert.Equal(2, result.Count);
        var list = result.ToList();
        // Ordered by Date descending, then StartTime
        Assert.Equal("Second", list[0].Title); // OtherDate is later
        Assert.Equal("First", list[1].Title);  // TestDate is earlier

        // Navigation loaded
        Assert.All(result, b => Assert.NotNull(b.User));
        Assert.All(result, b => Assert.NotNull(b.Room));
        Assert.All(result, b => Assert.Equal("Bob", b.User!.FirstName));
        Assert.All(result, b => Assert.Equal("Marley", b.User!.LastName));
    }

    [Fact]
    public async Task GetByUserIdAsync_NoBookings_ReturnsEmpty()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        var repo = CreateRepository();

        // Act
        var result = await repo.GetByUserIdAsync(UserId);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByUserIdAsync_OnlyReturnsThatUsersBookings()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId, "User1", "Test");
        await SeedUserAsync(ctx, OtherUserId, "User2", "Other");
        await SeedRoomAsync(ctx, RoomId);

        var myBooking = CreateBookingEntity(id: Guid.NewGuid(), userId: UserId, title: "My Booking");
        var otherBooking = CreateBookingEntity(id: Guid.NewGuid(), userId: OtherUserId, title: "Other Booking");
        await SeedBookingAsync(ctx, myBooking);
        await SeedBookingAsync(ctx, otherBooking);

        var repo = CreateRepository();

        // Act
        var result = await repo.GetByUserIdAsync(UserId);

        // Assert
        Assert.Single(result);
        Assert.Equal("My Booking", result.First().Title);
    }

    // ═══════════════════════════════════════════════════════════════════
    // FindOverlappingAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FindOverlappingAsync_OverlappingBooking_ReturnsIt()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedUserAsync(ctx, OtherUserId);
        await SeedRoomAsync(ctx, RoomId);

        var existing = CreateBookingEntity(id: BookingId, userId: UserId,
            start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(12));
        await SeedBookingAsync(ctx, existing);

        var repo = CreateRepository();

        // Act – query overlaps [09:00–11:00)
        var overlapping = await repo.FindOverlappingAsync(
            RoomId, TestDate, TimeSpan.FromHours(9), TimeSpan.FromHours(11), excludeBookingId: null);

        // Assert
        Assert.Single(overlapping);
        Assert.Equal(BookingId, overlapping.First().Id);
    }

    [Fact]
    public async Task FindOverlappingAsync_NoOverlap_ReturnsEmpty()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId);

        var existing = CreateBookingEntity(id: BookingId, userId: UserId,
            start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(11));
        await SeedBookingAsync(ctx, existing);

        var repo = CreateRepository();

        // Act – query [11:00–12:00) — adjacent, no overlap
        var overlapping = await repo.FindOverlappingAsync(
            RoomId, TestDate, TimeSpan.FromHours(11), TimeSpan.FromHours(12), excludeBookingId: null);

        // Assert
        Assert.Empty(overlapping);
    }

    [Fact]
    public async Task FindOverlappingAsync_AdjacentBefore_NoOverlap()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId);

        var existing = CreateBookingEntity(id: BookingId, userId: UserId,
            start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(11));
        await SeedBookingAsync(ctx, existing);

        var repo = CreateRepository();

        // Act – query [09:00–10:00) — ends exactly when existing starts
        var overlapping = await repo.FindOverlappingAsync(
            RoomId, TestDate, TimeSpan.FromHours(9), TimeSpan.FromHours(10), excludeBookingId: null);

        // Assert – [09:00, 10:00) and [10:00, 11:00) do not overlap
        Assert.Empty(overlapping);
    }

    [Fact]
    public async Task FindOverlappingAsync_DifferentDate_NoOverlap()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId);

        var existing = CreateBookingEntity(id: BookingId, userId: UserId,
            date: TestDate, start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(11));
        await SeedBookingAsync(ctx, existing);

        var repo = CreateRepository();

        // Act – different date
        var overlapping = await repo.FindOverlappingAsync(
            RoomId, OtherDate, TimeSpan.FromHours(10), TimeSpan.FromHours(11), excludeBookingId: null);

        // Assert
        Assert.Empty(overlapping);
    }

    [Fact]
    public async Task FindOverlappingAsync_DifferentRoom_NoOverlap()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId, "Room A");
        await SeedRoomAsync(ctx, OtherRoomId, "Room B");

        var existing = CreateBookingEntity(id: BookingId, userId: UserId,
            roomId: RoomId, start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(11));
        await SeedBookingAsync(ctx, existing);

        var repo = CreateRepository();

        // Act – different room
        var overlapping = await repo.FindOverlappingAsync(
            OtherRoomId, TestDate, TimeSpan.FromHours(10), TimeSpan.FromHours(11), excludeBookingId: null);

        // Assert
        Assert.Empty(overlapping);
    }

    [Fact]
    public async Task FindOverlappingAsync_ExcludeBookingId_ExcludesSelf()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedUserAsync(ctx, OtherUserId);
        await SeedRoomAsync(ctx, RoomId);

        // Same slot, one existing
        var existing = CreateBookingEntity(id: BookingId, userId: UserId,
            start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(11));
        await SeedBookingAsync(ctx, existing);

        var repo = CreateRepository();

        // Act – exclude our own booking ID
        var overlapping = await repo.FindOverlappingAsync(
            RoomId, TestDate, TimeSpan.FromHours(10), TimeSpan.FromHours(11), excludeBookingId: BookingId);

        // Assert – self excluded, no conflicts
        Assert.Empty(overlapping);
    }

    [Fact]
    public async Task FindOverlappingAsync_ExcludeBookingId_StillFindsOtherConflicts()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedUserAsync(ctx, OtherUserId);
        await SeedRoomAsync(ctx, RoomId);

        var booking1 = CreateBookingEntity(id: BookingId, userId: UserId,
            start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(11));
        var booking2 = CreateBookingEntity(id: Guid.NewGuid(), userId: OtherUserId,
            start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(11));
        await SeedBookingAsync(ctx, booking1);
        await SeedBookingAsync(ctx, booking2);

        var repo = CreateRepository();

        // Act – exclude booking1, should still see booking2
        var overlapping = await repo.FindOverlappingAsync(
            RoomId, TestDate, TimeSpan.FromHours(10), TimeSpan.FromHours(11), excludeBookingId: BookingId);

        // Assert
        Assert.Single(overlapping);
        Assert.NotEqual(BookingId, overlapping.First().Id);
    }

    [Fact]
    public async Task FindOverlappingAsync_NestedOverlap_DetectsBothDirections()
    {
        // Arrange – existing [09:00–13:00), query [10:00–12:00) — nested inside
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId);

        var existing = CreateBookingEntity(id: BookingId, userId: UserId,
            start: TimeSpan.FromHours(9), end: TimeSpan.FromHours(13));
        await SeedBookingAsync(ctx, existing);

        var repo = CreateRepository();

        // Act
        var overlapping = await repo.FindOverlappingAsync(
            RoomId, TestDate, TimeSpan.FromHours(10), TimeSpan.FromHours(12), excludeBookingId: null);

        // Assert
        Assert.Single(overlapping);
    }

    [Fact]
    public async Task FindOverlappingAsync_PartialOverlapStart_Detected()
    {
        // Arrange – existing [10:00–12:00), query [11:00–13:00)
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId);

        var existing = CreateBookingEntity(id: BookingId, userId: UserId,
            start: TimeSpan.FromHours(10), end: TimeSpan.FromHours(12));
        await SeedBookingAsync(ctx, existing);

        var repo = CreateRepository();

        // Act
        var overlapping = await repo.FindOverlappingAsync(
            RoomId, TestDate, TimeSpan.FromHours(11), TimeSpan.FromHours(13), excludeBookingId: null);

        // Assert
        Assert.Single(overlapping);
    }

    [Fact]
    public async Task FindOverlappingAsync_PartialOverlapEnd_Detected()
    {
        // Arrange – existing [11:00–13:00), query [10:00–12:00)
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId);

        var existing = CreateBookingEntity(id: BookingId, userId: UserId,
            start: TimeSpan.FromHours(11), end: TimeSpan.FromHours(13));
        await SeedBookingAsync(ctx, existing);

        var repo = CreateRepository();

        // Act
        var overlapping = await repo.FindOverlappingAsync(
            RoomId, TestDate, TimeSpan.FromHours(10), TimeSpan.FromHours(12), excludeBookingId: null);

        // Assert
        Assert.Single(overlapping);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetByDateRangeAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetByDateRangeAsync_RangeIncludesBoth_ReturnsBoth()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId, "User1", "One");
        await SeedUserAsync(ctx, OtherUserId, "User2", "Two");
        await SeedRoomAsync(ctx, RoomId, "Room");

        var booking1 = CreateBookingEntity(id: Guid.NewGuid(), userId: UserId,
            date: TestDate, title: "Day 1");
        var booking2 = CreateBookingEntity(id: Guid.NewGuid(), userId: OtherUserId,
            date: OtherDate, title: "Day 2");
        await SeedBookingAsync(ctx, booking1);
        await SeedBookingAsync(ctx, booking2);

        var repo = CreateRepository();

        // Act – range covers both dates
        var results = await repo.GetByDateRangeAsync(
            DateOnly.FromDateTime(TestDate), DateOnly.FromDateTime(OtherDate));

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, b => b.Title == "Day 1");
        Assert.Contains(results, b => b.Title == "Day 2");

        // Navigation loaded
        Assert.All(results, b => Assert.NotNull(b.User));
        Assert.All(results, b => Assert.NotNull(b.Room));
    }

    [Fact]
    public async Task GetByDateRangeAsync_EmptyRange_ReturnsEmpty()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId);

        var booking = CreateBookingEntity(id: Guid.NewGuid(), userId: UserId,
            date: TestDate);
        await SeedBookingAsync(ctx, booking);

        var repo = CreateRepository();

        // Act – range before TestDate
        var results = await repo.GetByDateRangeAsync(
            DateOnly.FromDateTime(OtherDate), DateOnly.FromDateTime(OtherDate.AddDays(1)));

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetByDateRangeAsync_SingleDayBoundaries_IncludesExactDay()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId);

        var bookingOnTestDate = CreateBookingEntity(id: Guid.NewGuid(), userId: UserId,
            date: TestDate, title: "On TestDate");
        var bookingBefore = CreateBookingEntity(id: Guid.NewGuid(), userId: UserId,
            date: TestDate.AddDays(-1), title: "Before");
        var bookingAfter = CreateBookingEntity(id: Guid.NewGuid(), userId: UserId,
            date: TestDate.AddDays(1), title: "After");
        await SeedBookingAsync(ctx, bookingOnTestDate);
        await SeedBookingAsync(ctx, bookingBefore);
        await SeedBookingAsync(ctx, bookingAfter);

        var repo = CreateRepository();

        // Act – exactly TestDate only
        var results = await repo.GetByDateRangeAsync(
            DateOnly.FromDateTime(TestDate), DateOnly.FromDateTime(TestDate));

        // Assert
        Assert.Single(results);
        Assert.Equal("On TestDate", results.First().Title);
    }

    [Fact]
    public async Task GetByDateRangeAsync_OrderedCorrectly()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId);

        var early = CreateBookingEntity(id: Guid.NewGuid(), userId: UserId,
            date: TestDate, start: TimeSpan.FromHours(9), end: TimeSpan.FromHours(10), title: "Early");
        var late = CreateBookingEntity(id: Guid.NewGuid(), userId: UserId,
            date: TestDate, start: TimeSpan.FromHours(17), end: TimeSpan.FromHours(18), title: "Late");
        await SeedBookingAsync(ctx, early);
        await SeedBookingAsync(ctx, late);

        var repo = CreateRepository();

        // Act
        var results = await repo.GetByDateRangeAsync(
            DateOnly.FromDateTime(TestDate), DateOnly.FromDateTime(TestDate));

        // Assert
        Assert.Equal(2, results.Count);
        var list = results.ToList();
        Assert.Equal("Early", list[0].Title);
        Assert.Equal("Late", list[1].Title);
    }

    // ═══════════════════════════════════════════════════════════════════
    // AddAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AddAsync_ValidBooking_SavesToDatabase()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId);

        var repo = CreateRepository();
        var booking = CreateBookingEntity(id: BookingId, userId: UserId, roomId: RoomId,
            title: "New Booking", description: "Test desc");

        // Act
        await repo.AddAsync(booking);

        // Assert – read through fresh DbContext
        using var freshCtx = NewContext();
        var reloaded = await freshCtx.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == BookingId);
        Assert.NotNull(reloaded);
        Assert.Equal("New Booking", reloaded!.Title);
        Assert.Equal("Test desc", reloaded!.Description);
        Assert.Equal(UserId, reloaded!.UserId);
        Assert.Equal(RoomId, reloaded!.RoomId);
        Assert.Equal(TestDate, reloaded!.Date);
        Assert.Equal(StartTime, reloaded!.StartTime);
        Assert.Equal(EndTime, reloaded!.EndTime);
    }

    // ═══════════════════════════════════════════════════════════════════
    // UpdateAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateAsync_ExistingBooking_UpdatesInDatabase()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId);
        var booking = CreateBookingEntity(id: BookingId, userId: UserId, roomId: RoomId,
            title: "Old Title");
        await SeedBookingAsync(ctx, booking);

        var repo = CreateRepository();

        // Get tracked entity
        var tracked = await repo.GetByIdAsync(BookingId);
        Assert.NotNull(tracked);
        tracked!.SetTitle("New Title", 200);
        tracked.SetDate(OtherDate);
        tracked.SetTimeInterval(TimeSpan.FromHours(14), TimeSpan.FromHours(15), TimeRules);

        // Act
        await repo.UpdateAsync(tracked);

        // Assert – read through fresh DbContext
        using var freshCtx = NewContext();
        var reloaded = await freshCtx.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == BookingId);
        Assert.NotNull(reloaded);
        Assert.Equal("New Title", reloaded!.Title);
        Assert.Equal(OtherDate, reloaded!.Date);
        Assert.Equal(TimeSpan.FromHours(14), reloaded!.StartTime);
        Assert.Equal(TimeSpan.FromHours(15), reloaded!.EndTime);
    }

    [Fact]
    public async Task UpdateAsync_DetachedEntity_AttachesAndUpdates()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId);
        var booking = CreateBookingEntity(id: BookingId, userId: UserId, roomId: RoomId,
            title: "Old Title");
        await SeedBookingAsync(ctx, booking);

        // Clear tracked entities so we can test the Attach path
        ctx.ChangeTracker.Clear();

        // Create detached entity with same ID (simulating cross-context scenario)
        var detached = CreateBookingEntity(id: BookingId, userId: UserId, roomId: RoomId,
            title: "Detached Update", date: OtherDate);

        var repo = CreateRepository();

        // Act
        await repo.UpdateAsync(detached);

        // Assert – read through fresh DbContext
        using var freshCtx = NewContext();
        var reloaded = await freshCtx.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == BookingId);
        Assert.NotNull(reloaded);
        Assert.Equal("Detached Update", reloaded!.Title);
        Assert.Equal(OtherDate, reloaded!.Date);
    }

    // ═══════════════════════════════════════════════════════════════════
    // DeleteAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteAsync_ExistingBooking_RemovesFromDatabase()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId);
        var booking = CreateBookingEntity(id: BookingId, userId: UserId, roomId: RoomId);
        await SeedBookingAsync(ctx, booking);

        var repo = CreateRepository();
        var tracked = await repo.GetByIdAsync(BookingId);
        Assert.NotNull(tracked);

        // Act
        await repo.DeleteAsync(tracked!);

        // Assert – read through fresh DbContext
        using var freshCtx = NewContext();
        var reloaded = await freshCtx.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == BookingId);
        Assert.Null(reloaded);
    }

    [Fact]
    public async Task DeleteAsync_DetachedEntity_AttachesAndDeletes()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId);
        var booking = CreateBookingEntity(id: BookingId, userId: UserId, roomId: RoomId);
        await SeedBookingAsync(ctx, booking);

        // Clear tracked entities so we can test the Attach path
        ctx.ChangeTracker.Clear();

        // Create detached entity with same ID
        var detached = CreateBookingEntity(id: BookingId, userId: UserId, roomId: RoomId);

        var repo = CreateRepository();

        // Act
        await repo.DeleteAsync(detached);

        // Assert
        using var freshCtx = NewContext();
        var reloaded = await freshCtx.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == BookingId);
        Assert.Null(reloaded);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Integration: multiple bookings
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AddMultipleBookings_DifferentDates_AllSaved()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId);

        var repo = CreateRepository();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        // Act
        await repo.AddAsync(CreateBookingEntity(id: id1, date: TestDate, title: "B1"));
        await repo.AddAsync(CreateBookingEntity(id: id2, date: OtherDate, title: "B2"));
        await repo.AddAsync(CreateBookingEntity(id: id3, date: OtherDate.AddDays(1), title: "B3"));

        // Assert
        using var freshCtx = NewContext();
        var count = await freshCtx.Bookings.AsNoTracking().CountAsync();
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task AddAndDelete_ThenQuery_ShowsEmpty()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedUserAsync(ctx, UserId);
        await SeedRoomAsync(ctx, RoomId);
        var repo = CreateRepository();

        var booking = CreateBookingEntity(id: BookingId);
        await repo.AddAsync(booking);
        var tracked = await repo.GetByIdAsync(BookingId);
        await repo.DeleteAsync(tracked!);

        // Assert
        using var freshCtx = NewContext();
        var exists = await freshCtx.Bookings.AsNoTracking().AnyAsync(b => b.Id == BookingId);
        Assert.False(exists);
    }
}
