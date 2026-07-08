using Microsoft.EntityFrameworkCore;
using SonicPulse.Application.Abstractions;
using SonicPulse.Domain.Entities;
using SonicPulse.Domain.ValueObjects;
using SonicPulse.Infrastructure.Persistence;

namespace SonicPulse.Infrastructure.Repositories;

public sealed class DetectionRepository(AppDbContext db) : IDetectionRepository
{
    public async Task AddAsync(Detection detection, CancellationToken ct)
    {
        db.Detections.Add(detection);
        await db.SaveChangesAsync(ct);
    }

    public Task<Detection?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.Detections.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<Detection>> GetByDeviceIdAsync(
        DeviceId deviceId, long? afterSequenceNumber, int limit, CancellationToken ct)
    {
        var query = db.Detections.AsNoTracking()
            .Where(d => d.DeviceId == deviceId);

        if (afterSequenceNumber is not null)
            query = query.Where(d => d.SequenceNumber < afterSequenceNumber.Value);

        return await query
            .OrderByDescending(d => d.SequenceNumber)
            .Take(limit + 1)
            .ToListAsync(ct);
    }
}
