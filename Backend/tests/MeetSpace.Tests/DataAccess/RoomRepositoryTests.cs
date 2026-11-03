using MeetSpace.Application.Abstractions;
using MeetSpace.DataAccess;
using MeetSpace.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MeetSpace.Tests.DataAccess;

public class RoomRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    private const int RoomNameMaxLength = 200;
    private const int RoomDescriptionMaxLength = 2000;
    private const int EquipmentNameMaxLength = 100;

    private static readonly Guid RoomId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid RoomWithoutEquipmentId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid NonExistentRoomId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid EquipmentId1 = Guid.Parse("AAAA0000-AAAA-AAAA-AAAA-AAAAAAAAAAAA");
    private static readonly Guid EquipmentId2 = Guid.Parse("BBBB0000-BBBB-BBBB-BBBB-BBBBBBBBBBBB");

    public RoomRepositoryTests()
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

    private IRoomRepository CreateRepository()
    {
        return _provider.GetRequiredService<IRoomRepository>();
    }

    private MeetSpaceDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<MeetSpaceDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new MeetSpaceDbContext(options);
    }

    private async Task SeedRoomWithEquipmentAsync(MeetSpaceDbContext ctx)
    {
        var room = new Room("Conference Room", 10, 1, RoomNameMaxLength, "A large room", RoomDescriptionMaxLength);
        typeof(Room).GetProperty(nameof(Room.Id))!.SetValue(room, RoomId);
        ctx.Rooms.Add(room);

        var eq1 = new Equipment("Projector", EquipmentNameMaxLength);
        typeof(Equipment).GetProperty(nameof(Equipment.Id))!.SetValue(eq1, EquipmentId1);
        ctx.Equipments.Add(eq1);

        var eq2 = new Equipment("Whiteboard", EquipmentNameMaxLength);
        typeof(Equipment).GetProperty(nameof(Equipment.Id))!.SetValue(eq2, EquipmentId2);
        ctx.Equipments.Add(eq2);

        // Link equipment to room
        ctx.RoomEquipments.Add(new RoomEquipment(RoomId, EquipmentId1));
        ctx.RoomEquipments.Add(new RoomEquipment(RoomId, EquipmentId2));

        await ctx.SaveChangesAsync();
    }

    private async Task SeedRoomWithoutEquipmentAsync(MeetSpaceDbContext ctx)
    {
        var room = new Room("Empty Room", 5, 2, RoomNameMaxLength, description: null);
        typeof(Room).GetProperty(nameof(Room.Id))!.SetValue(room, RoomWithoutEquipmentId);
        ctx.Rooms.Add(room);
        await ctx.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════════
    // ExistsAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExistsAsync_ExistingRoom_ReturnsTrue()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedRoomWithEquipmentAsync(ctx);
        var repo = CreateRepository();

        // Act
        var exists = await repo.ExistsAsync(RoomId);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_NonExistingRoom_ReturnsFalse()
    {
        // Arrange
        var repo = CreateRepository();

        // Act
        var exists = await repo.ExistsAsync(NonExistentRoomId);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsAsync_EmptyDatabase_ReturnsFalse()
    {
        // Arrange
        var repo = CreateRepository();

        // Act
        var exists = await repo.ExistsAsync(Guid.NewGuid());

        // Assert
        Assert.False(exists);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetByIdAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetByIdAsync_RoomWithEquipment_ReturnsRoomWithEquipment()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedRoomWithEquipmentAsync(ctx);
        var repo = CreateRepository();

        // Act
        var result = await repo.GetByIdAsync(RoomId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RoomId, result!.Id);
        Assert.Equal("Conference Room", result.Name);
        Assert.Equal(10, result.Capacity);
        Assert.Equal(1, result.Floor);
        Assert.Equal("A large room", result.Description);

        // Equipment loaded via RoomEquipments -> Equipment
        Assert.NotNull(result.RoomEquipments);
        Assert.Equal(2, result.RoomEquipments.Count);

        var equipmentNames = result.RoomEquipments
            .Select(re => re.Equipment?.Name)
            .OrderBy(n => n)
            .ToList();
        Assert.Contains("Projector", equipmentNames);
        Assert.Contains("Whiteboard", equipmentNames);

        // Verify these are the correct Equipment items
        Assert.All(result.RoomEquipments, re =>
        {
            Assert.NotNull(re.Equipment);
            Assert.NotEqual(Guid.Empty, re.Equipment!.Id);
        });
    }

    [Fact]
    public async Task GetByIdAsync_RoomWithoutEquipment_ReturnsRoomWithEmptyEquipment()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedRoomWithoutEquipmentAsync(ctx);
        var repo = CreateRepository();

        // Act
        var result = await repo.GetByIdAsync(RoomWithoutEquipmentId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Empty Room", result!.Name);
        Assert.Equal(5, result.Capacity);
        Assert.Equal(2, result.Floor);
        Assert.Null(result.Description);

        // No equipment
        Assert.NotNull(result.RoomEquipments);
        Assert.Empty(result.RoomEquipments);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingRoom_ReturnsNull()
    {
        // Arrange
        var repo = CreateRepository();

        // Act
        var result = await repo.GetByIdAsync(NonExistentRoomId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_VerifyReadThroughFreshContext()
    {
        // Arrange: seed via one context
        await SeedRoomWithEquipmentAsync(CreateDbContext());

        // Act: read through a completely fresh context
        using var freshCtx = NewContext();
        var repo = new MeetSpace.DataAccess.Repositories.RoomRepository(freshCtx);
        var result = await repo.GetByIdAsync(RoomId);

        // Assert: data was actually persisted, not just in ChangeTracker
        Assert.NotNull(result);
        Assert.Equal("Conference Room", result!.Name);
        Assert.Equal(2, result.RoomEquipments.Count);
    }

    [Fact]
    public async Task GetByIdAsync_MultipleRooms_ReturnsCorrectOne()
    {
        // Arrange
        var ctx = CreateDbContext();
        await SeedRoomWithEquipmentAsync(ctx);

        // Add a second room
        var secondRoomId = Guid.NewGuid();
        var secondRoom = new Room("Second Room", 20, 3, RoomNameMaxLength);
        typeof(Room).GetProperty(nameof(Room.Id))!.SetValue(secondRoom, secondRoomId);
        ctx.Rooms.Add(secondRoom);
        await ctx.SaveChangesAsync();

        var repo = CreateRepository();

        // Act – get first room
        var result1 = await repo.GetByIdAsync(RoomId);
        var result2 = await repo.GetByIdAsync(secondRoomId);

        // Assert
        Assert.NotNull(result1);
        Assert.Equal("Conference Room", result1!.Name);

        Assert.NotNull(result2);
        Assert.Equal("Second Room", result2!.Name);
        Assert.Empty(result2!.RoomEquipments);
    }
}
