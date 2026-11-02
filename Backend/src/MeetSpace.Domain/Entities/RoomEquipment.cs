namespace MeetSpace.Domain.Entities;

public class RoomEquipment
{
    public Guid RoomId { get; private set; }
    public Guid EquipmentId { get; private set; }

    public Room Room { get; private set; } = null!;
    public Equipment Equipment { get; private set; } = null!;

    private RoomEquipment() { }

    public RoomEquipment(Guid roomId, Guid equipmentId)
    {
        RoomId = roomId;
        EquipmentId = equipmentId;
    }

    public RoomEquipment(Room room, Equipment equipment)
    {
        Room = room ?? throw new ArgumentNullException(nameof(room));
        Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        RoomId = room.Id;
        EquipmentId = equipment.Id;
    }
}
