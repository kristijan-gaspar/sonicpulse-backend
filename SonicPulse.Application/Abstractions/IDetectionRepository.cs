using SonicPulse.Domain.Entities;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Application.Abstractions;

public interface IDetectionRepository
{
    Task AddAsync(Detection detection, CancellationToken ct);
    Task<Detection?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Detection>> GetByDeviceIdAsync(
        DeviceId deviceId, long? afterSequenceNumber, int limit, CancellationToken ct);
}
