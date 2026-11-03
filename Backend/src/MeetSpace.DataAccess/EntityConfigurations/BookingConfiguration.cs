using MeetSpace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MeetSpace.DataAccess.EntityConfigurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .ValueGeneratedNever();

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.Description)
            .HasMaxLength(2000);

        builder.Property(b => b.UserId)
            .IsRequired();

        builder.Property(b => b.RoomId)
            .IsRequired();

        builder.Property(b => b.Date)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(b => b.StartTime)
            .IsRequired()
            .HasColumnType("time without time zone");

        builder.Property(b => b.EndTime)
            .IsRequired()
            .HasColumnType("time without time zone");

        builder.HasIndex(b => new { b.RoomId, b.Date });

        builder.HasIndex(b => b.UserId);

        builder.HasOne(b => b.User)
            .WithMany(u => u.Bookings)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Room)
            .WithMany(r => r.Bookings)
            .HasForeignKey(b => b.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        // RowVersion is provider-specific — registered in OnModelCreating
        // only for Npgsql provider (SQLite doesn't support it).
    }
}
