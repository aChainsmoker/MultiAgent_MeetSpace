using MeetSpace.Domain.Enums;

namespace MeetSpace.Application.Abstractions;

public interface ICurrentUserContext
{
    Task<Guid?> GetCurrentUserIdAsync(CancellationToken ct = default);

    Task<UserRole?> GetCurrentUserRoleAsync(CancellationToken ct = default);
}
