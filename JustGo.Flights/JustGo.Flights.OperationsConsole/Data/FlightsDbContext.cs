using JustGo.Flights.OperationsConsole.Models;
using Microsoft.EntityFrameworkCore;

namespace JustGo.Flights.OperationsConsole.Data;

public class FlightsDbContext(DbContextOptions<FlightsDbContext> options) : DbContext(options)
{
    public const string Schema = "opeations_console";
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