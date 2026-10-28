using JustGo.Flights.BaggageHandling.Models;
using Microsoft.EntityFrameworkCore;

namespace JustGo.Flights.BaggageHandling.Data;

public class BaggageDbContext(DbContextOptions<BaggageDbContext> options) : DbContext(options)
{
    public const string Schema = "baggage_handling";
    public DbSet<BagLoad> BagLoads => Set<BagLoad>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<BagLoad>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.Status).HasConversion<string>();
        });
    }
}
