using MeetSpace.Application.Abstractions;
using MeetSpace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeetSpace.DataAccess.Repositories;

public class RoomRepository : IRoomRepository
{
    private readonly MeetSpaceDbContext _context;

    public RoomRepository(MeetSpaceDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsAsync(Guid roomId, CancellationToken ct = default)
    {
        return await _context.Rooms
            .AsNoTracking()
            .AnyAsync(r => r.Id == roomId, ct);
    }

    public async Task<Room?> GetByIdAsync(Guid roomId, CancellationToken ct = default)
    {
        return await _context.Rooms
            .AsNoTracking()
            .Include(r => r.RoomEquipments)
            .ThenInclude(re => re.Equipment)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);
    }
}
