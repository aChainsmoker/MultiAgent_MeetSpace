namespace MeetSpace.Application.Exceptions;

public class NotFoundException : ApplicationException
{
    public string EntityName { get; }
    public Guid EntityId { get; }

    public NotFoundException(string entityName, Guid entityId)
        : base($"{entityName} with id '{entityId}' not found.")
    {
        EntityName = entityName;
        EntityId = entityId;
    }
}
