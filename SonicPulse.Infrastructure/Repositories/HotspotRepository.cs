using Microsoft.EntityFrameworkCore;
using SonicPulse.Application.Abstractions;
using SonicPulse.Domain.Entities;
using SonicPulse.Infrastructure.Persistence;

namespace SonicPulse.Infrastructure.Repositories;

public sealed class HotspotRepository(AppDbContext db) : IHotspotRepository
{
    public Task AddAsync(Hotspot hotspot, CancellationToken ct)
    {
        // Unlike DetectionRepository.AddAsync, this does NOT self-commit - the
        // caller (ProcessDetectionHandler) commits via IUnitOfWork alongside
        // the rest of the grouping transaction in the same SaveChangesAsync.
        db.Hotspots.Add(hotspot);
        return Task.CompletedTask;
    }

    public Task<Hotspot?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Hotspots.FirstOrDefaultAsync(h => h.Id == id, ct);

    public async Task<IReadOnlyList<Hotspot>> GetSinceAsync(DateTime sinceUtc, CancellationToken ct)
        => await db.Hotspots
            .AsNoTracking()
            .Where(h => h.LastReceivedAtUtc >= sinceUtc)
            .OrderByDescending(h => h.LastReceivedAtUtc)
            .ToListAsync(ct);
}
