using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SonicPulse.Domain.Entities;

namespace SonicPulse.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<Detection> Detections => Set<Detection>();
    public DbSet<Hotspot> Hotspots => Set<Hotspot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        SetDetectionLocationPoints();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetDetectionLocationPoints()
    {
        var entries = ChangeTracker
            .Entries<Detection>()
            .Where(entry =>
                entry.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            var location = entry.Entity.Location;

            entry.Property<Point>("LocationPoint").CurrentValue =
                new Point(location.Longitude, location.Latitude)
                {
                    SRID = 4326
                };
        }
    }
}
