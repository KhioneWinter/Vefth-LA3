
using JustGo.Flights.CheckInBoarding.Models;
using Microsoft.EntityFrameworkCore;

namespace JustGo.Flights.CheckInBoarding.Data;

public class CheckInDbContext(DbContextOptions<CheckInDbContext> options) : DbContext(options)
{
    public const string Schema = "checkin_boarding";
    public DbSet<Flight> Flights => Set<Flight>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Flight>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.Status).HasConversion<string>();
        });
    }
}
