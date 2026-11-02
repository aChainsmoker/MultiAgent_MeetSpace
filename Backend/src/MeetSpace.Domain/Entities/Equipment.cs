namespace MeetSpace.Domain.Entities;

public class Equipment
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public ICollection<RoomEquipment> RoomEquipments { get; private set; } = new List<RoomEquipment>();

    private Equipment() { }

    public Equipment(string name)
    {
        Id = Guid.NewGuid();
        SetName(name);
    }

    public void SetName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > Constants.DomainConstants.EquipmentNameMaxLength)
            throw new ArgumentException($"Equipment name must not exceed {Constants.DomainConstants.EquipmentNameMaxLength} characters.");
        Name = name;
    }
}
