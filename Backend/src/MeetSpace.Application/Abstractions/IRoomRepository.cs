using MeetSpace.Domain.Entities;

namespace MeetSpace.Application.Abstractions;

public interface IRoomRepository
{
    Task<bool> ExistsAsync(Guid roomId, CancellationToken ct = default);

    Task<Room?> GetByIdAsync(Guid roomId, CancellationToken ct = default);
}
