using JustGo.Flights.Notification.Models;
using Microsoft.EntityFrameworkCore;

namespace JustGo.Flights.Notification.Data;

public class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public const string Schema = "notification";
    public DbSet<Notice> Notices => Set<Notice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Notice>(e =>
        {
            e.HasKey(n => n.Id);
            e.Property(n => n.Type).HasConversion<string>();
            // NULLS NOT DISTINCT so a cancellation (NewDepartureUtc = null) can only be stored once per flight
            e.HasIndex(n => new { n.FlightId, n.Type, n.NewDepartureUtc })
                .IsUnique()
                .AreNullsDistinct(false);
        });
    }
}
