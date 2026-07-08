using Microsoft.EntityFrameworkCore;
using SonicPulse.Domain.Entities;

namespace SonicPulse.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<Detection> Detections => Set<Detection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
