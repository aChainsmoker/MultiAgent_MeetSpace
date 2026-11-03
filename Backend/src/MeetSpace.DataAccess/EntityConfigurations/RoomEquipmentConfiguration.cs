using MeetSpace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MeetSpace.DataAccess.EntityConfigurations;

public class RoomEquipmentConfiguration : IEntityTypeConfiguration<RoomEquipment>
{
    public void Configure(EntityTypeBuilder<RoomEquipment> builder)
    {
        builder.ToTable("room_equipments");

        builder.HasKey(re => new { re.RoomId, re.EquipmentId });

        builder.Property(re => re.RoomId)
            .ValueGeneratedNever();

        builder.Property(re => re.EquipmentId)
            .ValueGeneratedNever();

        builder.HasOne(re => re.Room)
            .WithMany(r => r.RoomEquipments)
            .HasForeignKey(re => re.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(re => re.Equipment)
            .WithMany(e => e.RoomEquipments)
            .HasForeignKey(re => re.EquipmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
