using MeetSpace.Application.Abstractions;
using MeetSpace.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeetSpace.DataAccess;

public static class DependencyInjection
{
    /// <summary>
    /// Registers DataAccess services with PostgreSQL (Npgsql) provider.
    /// This is the production configuration.
    /// Does NOT require a running PostgreSQL at registration time.
    /// </summary>
    public static IServiceCollection AddMeetSpaceDataAccess(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "MeetSpace")
    {
        var connectionString = configuration.GetConnectionString(connectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Fallback for development: app starts without PostgreSQL
            connectionString = "Host=localhost;Port=5432;Database=meetspace;Username=meetspace;Password=meetspace";
        }

        services.AddDbContext<MeetSpaceDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
            });
        });

        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();

        return services;
    }

    /// <summary>
    /// Registers DataAccess services with SQLite in-memory provider.
    /// Used ONLY for testing, NOT for production.
    /// The connection must be kept open for the lifetime of the tests.
    /// </summary>
    public static IServiceCollection AddMeetSpaceDataAccessSqlite(
        this IServiceCollection services,
        Microsoft.Data.Sqlite.SqliteConnection keepAliveConnection)
    {
        services.AddDbContext<MeetSpaceDbContext>(options =>
        {
            options.UseSqlite(keepAliveConnection);
        });

        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();

        return services;
    }
}
