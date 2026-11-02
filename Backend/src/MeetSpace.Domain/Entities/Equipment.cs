namespace MeetSpace.Domain.Entities;

public class Equipment
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public ICollection<RoomEquipment> RoomEquipments { get; private set; } = new List<RoomEquipment>();

    private Equipment() { }

    public Equipment(string name, int nameMaxLength)
    {
        Id = Guid.NewGuid();
        SetName(name, nameMaxLength);
    }

    public void SetName(string name, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > maxLength)
            throw new ArgumentException($"Equipment name must not exceed {maxLength} characters.");
        Name = name;
    }
}
