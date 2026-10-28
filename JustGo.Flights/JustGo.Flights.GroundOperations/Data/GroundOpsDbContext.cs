using JustGo.Flights.GroundOperations.Models;
using Microsoft.EntityFrameworkCore;

namespace JustGo.Flights.GroundOperations.Data;

public class GroundOpsDbContext(DbContextOptions<GroundOpsDbContext> options) : DbContext(options)
{
    public const string Schema = "ground_operations";
    public DbSet<Turnaround> Turnarounds => Set<Turnaround>();
    public DbSet<TurnaroundTask> Tasks => Set<TurnaroundTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Turnaround>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).ValueGeneratedNever();
            e.Property(t => t.Status).HasConversion<string>();
            e.HasMany(t => t.Tasks).WithOne().HasForeignKey(t => t.TurnaroundId);
        });
        modelBuilder.Entity<TurnaroundTask>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).ValueGeneratedNever();
            e.Property(t => t.Type).HasConversion<string>();
            e.Property(t => t.Status).HasConversion<string>();
            e.HasIndex(t => new { t.TurnaroundId, t.Type }).IsUnique();
        });
    }
}
