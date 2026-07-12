using SonicPulse.Domain.Entities;

namespace SonicPulse.Application.Abstractions;

public interface IHotspotRepository
{
    Task AddAsync(Hotspot hotspot, CancellationToken ct);
    Task<Hotspot?> GetByIdAsync(Guid id, CancellationToken ct);
}
