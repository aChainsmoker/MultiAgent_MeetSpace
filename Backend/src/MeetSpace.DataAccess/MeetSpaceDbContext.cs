using MeetSpace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MeetSpace.DataAccess;

public class MeetSpaceDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Equipment> Equipments => Set<Equipment>();
    public DbSet<RoomEquipment> RoomEquipments => Set<RoomEquipment>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public MeetSpaceDbContext(DbContextOptions<MeetSpaceDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MeetSpaceDbContext).Assembly);

        if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            // RowVersion is provider-specific: only for Npgsql (PostgreSQL)
            // SQLite does not support IsRowVersion(), so we guard it.
            modelBuilder.Entity<Booking>(b =>
            {
                b.Property<byte[]>("RowVersion")
                    .IsRowVersion()
                    .HasColumnName("row_version");
            });
        }
        else
        {
            // SQLite: TimeSpan is stored as TEXT by default, but TEXT comparison
            // for < and > operators is not supported by EF Core SQLite provider's
            // LINQ translator (it tries to emit SQLite operators that don't exist).
            // Converting TimeSpan to ticks (long) allows numeric comparison that
            // SQLite query translator supports.
            // PostgreSQL keeps native "time without time zone" type (see BookingConfiguration).
            modelBuilder.Entity<Booking>(b =>
            {
                var timeSpanToTicks = new ValueConverter<TimeSpan, long>(
                    v => v.Ticks,
                    v => TimeSpan.FromTicks(v));

                b.Property(b => b.StartTime)
                    .HasConversion(timeSpanToTicks);

                b.Property(b => b.EndTime)
                    .HasConversion(timeSpanToTicks);
            });
        }
    }
}
