using SonicPulse.Application.Abstractions;
using SonicPulse.Domain.Entities;
using SonicPulse.Domain.Enums;
using SonicPulse.Domain.Rules;
using SonicPulse.Domain.Services;

namespace SonicPulse.Application.Detections.Handlers;

public sealed class ProcessDetectionHandler(
    IDetectionRepository detections,
    IHotspotRepository hotspots,
    IUnitOfWork unitOfWork,
    GroupingRules rules)
{
    public async Task HandleAsync(Guid detectionId, CancellationToken ct)
    {
        var detection = await detections.GetForProcessingAsync(detectionId, ct);
        if (detection is null || detection.ProcessingStatus != DetectionProcessingStatus.Pending)
            return; // deleted, or already processed/failed - nothing to do

        // 1. SQL pre-selection: expanded, symmetric window (rough, over-inclusive).
        var candidates = await detections.FindCandidatesAsync(
            detection.Location, detection.ReceivedAtUtc, rules, ct);

        // 2. Domain independently re-checks the exact rules.
        IReadOnlyList<Detection> group = DetectionGrouper.SelectGroup(detection, candidates, rules);

        if (group.Count == 0)
        {
            detection.MarkProcessed(); // lone detection, HotspotId stays null
            await unitOfWork.SaveChangesAsync(ct);
            return;
        }

        // 3. Does the group touch existing hotspots?
        var existingIds = group
            .Where(d => d.HotspotId is not null)
            .Select(d => d.HotspotId!.Value)
            .Distinct()
            .ToList();

        if (existingIds.Count > 1)
        {
            var (chosen, filtered) = HotspotAssembler.ResolveBridging(group);
            group = filtered;
            existingIds = [chosen];
        }

        Guid hotspotId;
        IReadOnlyList<Detection> finalGroup;

        if (existingIds.Count == 1)
        {
            var id = existingIds[0];
            var existing = await hotspots.GetByIdAsync(id, ct)
                ?? throw new InvalidOperationException($"Hotspot {id} not found.");

            var members = await detections.GetByHotspotIdAsync(id, ct);
            finalGroup = members.Concat(group).DistinctBy(d => d.Id).ToList();

            HotspotAssembler.Reassemble(existing, finalGroup, group, rules);
            hotspotId = existing.Id;
        }
        else
        {
            var hotspot = HotspotAssembler.Assemble(group, rules);
            await hotspots.AddAsync(hotspot, ct);
            hotspotId = hotspot.Id;
            finalGroup = group;
        }

        // 4. Attach every detection to the hotspot. Only the detection that
        //    triggered this run is marked Processed here - the other Pending
        //    members are deliberately left Pending until their own turn, so
        //    a later transitive group expansion (candidate window is centered
        //    on each detection's own location) isn't cut off early.
        foreach (var d in finalGroup)
            d.AssignToHotspot(hotspotId);

        detection.MarkProcessed();

        await unitOfWork.SaveChangesAsync(ct);
    }
}
