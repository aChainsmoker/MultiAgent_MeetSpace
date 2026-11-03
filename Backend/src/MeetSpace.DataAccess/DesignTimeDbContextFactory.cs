using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MeetSpace.DataAccess;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MeetSpaceDbContext>
{
    public MeetSpaceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MeetSpaceDbContext>();

        // Dummy connection string — EF Core tools generate migration code
        // without connecting to a real PostgreSQL server.
        optionsBuilder.UseNpgsql(
            "Host=dummy;Database=dummy;Username=dummy;Password=dummy",
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
            });

        return new MeetSpaceDbContext(optionsBuilder.Options);
    }
}
