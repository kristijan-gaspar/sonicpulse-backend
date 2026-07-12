using SonicPulse.Domain.Entities;
using SonicPulse.Domain.Rules;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Application.Abstractions;

public interface IDetectionRepository
{
    Task AddAsync(Detection detection, CancellationToken ct);
    Task<Detection?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Detection>> GetByDeviceIdAsync(
        DeviceId deviceId, long? afterSequenceNumber, int limit, CancellationToken ct);

    // Tracked fetch for mutation (AssignToHotspot/MarkProcessed/MarkFailed) during
    // grouping orchestration. Distinct from GetByIdAsync, which is AsNoTracking
    // and used only by the read-only GET endpoint.
    Task<Detection?> GetForProcessingAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Detection>> FindCandidatesAsync(
        Coordinates around, DateTime receivedAtUtc, GroupingRules rules, CancellationToken ct);

    Task<IReadOnlyList<Detection>> GetByHotspotIdAsync(Guid hotspotId, CancellationToken ct);

    Task<IReadOnlyList<Guid>> GetPendingIdsAsync(CancellationToken ct);
}
