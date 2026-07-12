using Microsoft.EntityFrameworkCore;
using SonicPulse.Application.Abstractions;
using SonicPulse.Domain.Entities;
using SonicPulse.Infrastructure.Persistence;

namespace SonicPulse.Infrastructure.Repositories;

public sealed class HotspotRepository(AppDbContext db) : IHotspotRepository
{
    public Task AddAsync(Hotspot hotspot, CancellationToken ct)
    {
        db.Hotspots.Add(hotspot);
        return Task.CompletedTask; // does NOT self-commit - caller commits via IUnitOfWork
    }

    public Task<Hotspot?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Hotspots.FirstOrDefaultAsync(h => h.Id == id, ct);
}
