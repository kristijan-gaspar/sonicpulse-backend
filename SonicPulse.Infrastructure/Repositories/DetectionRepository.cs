using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SonicPulse.Application.Abstractions;
using SonicPulse.Domain.Entities;
using SonicPulse.Domain.Enums;
using SonicPulse.Domain.Rules;
using SonicPulse.Domain.ValueObjects;
using SonicPulse.Infrastructure.Persistence;
using Coordinates = SonicPulse.Domain.ValueObjects.Coordinates;

namespace SonicPulse.Infrastructure.Repositories;

public sealed class DetectionRepository(AppDbContext db) : IDetectionRepository
{
    public async Task AddAsync(Detection detection, CancellationToken ct)
    {
        // Unlike HotspotRepository.AddAsync, this DOES self-commit - submission
        // is a single-step operation with no other repository writes to batch
        // with (see SubmitDetectionHandler).
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

    public Task<Detection?> GetForProcessingAsync(Guid id, CancellationToken ct)
        => db.Detections.FirstOrDefaultAsync(d => d.Id == id, ct); // tracked, no AsNoTracking

    public async Task<IReadOnlyList<Detection>> FindCandidatesAsync(
        Coordinates around, DateTime receivedAtUtc, GroupingRules rules, CancellationToken ct)
    {
        var point = new Point(around.Longitude, around.Latitude) { SRID = 4326 };
        double searchRadius = rules.RadiusMeters * rules.CandidateSearchExpansionFactor;
        var from = receivedAtUtc - rules.TimeWindow;
        var to = receivedAtUtc + rules.TimeWindow;

        return await db.Detections
            .Where(d => d.ReceivedAtUtc >= from && d.ReceivedAtUtc <= to)
            .Where(d => d.ProcessingStatus != DetectionProcessingStatus.Failed)
            .Where(d => EF.Property<Point>(d, "LocationPoint").IsWithinDistance(point, searchRadius))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Detection>> GetByHotspotIdAsync(Guid hotspotId, CancellationToken ct)
        => await db.Detections
            .Where(d => d.HotspotId == hotspotId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetPendingIdsAsync(CancellationToken ct)
        => await db.Detections
            .AsNoTracking()
            .Where(d => d.ProcessingStatus == DetectionProcessingStatus.Pending)
            .OrderBy(d => d.SequenceNumber)
            .Select(d => d.Id)
            .ToListAsync(ct);
}
