namespace MeetSpace.Domain.Entities;

public class Room
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Capacity { get; private set; }
    public int Floor { get; private set; }
    public string? Description { get; private set; }
    public string? PhotoKey { get; private set; }

    public ICollection<RoomEquipment> RoomEquipments { get; private set; } = new List<RoomEquipment>();
    public ICollection<Booking> Bookings { get; private set; } = new List<Booking>();

    private Room() { }

    public Room(string name, int capacity, int floor, string? description = null)
    {
        Id = Guid.NewGuid();
        SetName(name);
        SetCapacity(capacity);
        SetFloor(floor);
        Description = description;
    }

    public void SetName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > Constants.DomainConstants.RoomNameMaxLength)
            throw new ArgumentException($"Room name must not exceed {Constants.DomainConstants.RoomNameMaxLength} characters.");
        Name = name;
    }

    public void SetCapacity(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentException("Capacity must be at least 1.");
        Capacity = capacity;
    }

    public void SetFloor(int floor)
    {
        if (floor < 1)
            throw new ArgumentException("Floor must be at least 1.");
        Floor = floor;
    }

    public void SetDescription(string? description)
    {
        if (description != null && description.Length > Constants.DomainConstants.RoomDescriptionMaxLength)
            throw new ArgumentException($"Description must not exceed {Constants.DomainConstants.RoomDescriptionMaxLength} characters.");
        Description = description;
    }

    public void SetPhotoKey(string? photoKey)
    {
        PhotoKey = photoKey;
    }
}
