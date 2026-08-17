using Moq;
using SonicPulse.Application.Abstractions;
using SonicPulse.Application.Detections.Handlers;
using SonicPulse.Domain.Entities;
using SonicPulse.Domain.Enums;
using SonicPulse.Domain.Rules;
using SonicPulse.Domain.Services;
using SonicPulse.Domain.ValueObjects;

namespace SonicPulse.Tests.Application;

public class ProcessDetectionHandlerTests
{
    private static readonly GroupingRules Rules = new(
        timeWindow: TimeSpan.FromSeconds(5),
        radiusMeters: 1000,
        minDeviceCount: 2,
        candidateSearchExpansionFactor: 1.1);

    private static readonly DateTime BaseTime = new(2026, 7, 2, 14, 31, 7, DateTimeKind.Utc);
    private static readonly Coordinates BaseLocation = new(45.8010, 15.9700);

    private readonly List<Detection> _detectionStore = [];
    private readonly Dictionary<Guid, Hotspot> _hotspotStore = [];
    private readonly Mock<IDetectionRepository> _detections = new();
    private readonly Mock<IHotspotRepository> _hotspots = new();
    private readonly ProcessDetectionHandler _handler;

    public ProcessDetectionHandlerTests()
    {
        // Mocks are wired against shared backing collections rather than
        // fixed Setup/Returns per call: these scenarios are inherently
        // stateful (a detection added in step 1 must be visible to
        // FindCandidatesAsync in step 2), which plain per-call stubbing
        // can't express.
        _detections.Setup(r => r.AddAsync(It.IsAny<Detection>(), It.IsAny<CancellationToken>()))
            .Callback<Detection, CancellationToken>((d, _) => _detectionStore.Add(d))
            .Returns(Task.CompletedTask);

        _detections.Setup(r => r.GetForProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _detectionStore.FirstOrDefault(d => d.Id == id));

        _detections.Setup(r => r.FindCandidatesAsync(
                It.IsAny<Coordinates>(), It.IsAny<DateTime>(), It.IsAny<GroupingRules>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Coordinates around, DateTime receivedAtUtc, GroupingRules rules, CancellationToken _) =>
            {
                var from = receivedAtUtc - rules.TimeWindow;
                var to = receivedAtUtc + rules.TimeWindow;
                double searchRadius = rules.RadiusMeters * rules.CandidateSearchExpansionFactor;

                return (IReadOnlyList<Detection>)_detectionStore
                    .Where(d => d.ReceivedAtUtc >= from && d.ReceivedAtUtc <= to)
                    .Where(d => d.ProcessingStatus != DetectionProcessingStatus.Failed)
                    .Where(d => GeoDistance.Meters(around, d.Location) <= searchRadius)
                    .ToList();
            });

        _detections.Setup(r => r.GetByHotspotIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid hotspotId, CancellationToken _) =>
                (IReadOnlyList<Detection>)_detectionStore.Where(d => d.HotspotId == hotspotId).ToList());

        _hotspots.Setup(r => r.AddAsync(It.IsAny<Hotspot>(), It.IsAny<CancellationToken>()))
            .Callback<Hotspot, CancellationToken>((h, _) => _hotspotStore[h.Id] = h)
            .Returns(Task.CompletedTask);

        _hotspots.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                _hotspotStore.TryGetValue(id, out var h) ? h : null);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _handler = new ProcessDetectionHandler(_detections.Object, _hotspots.Object, unitOfWork.Object, Rules);
    }

    private static Detection MakeDetection(
        Coordinates location, DateTime receivedAtUtc, DeviceId? deviceId = null)
        => Detection.Create(deviceId ?? DeviceId.New(), -8.5, location, 12.0, receivedAtUtc, null);

    [Fact]
    public async Task HandleAsync_TwoDevicesWithin4_9Seconds_FormsGroup()
    {
        var a = MakeDetection(BaseLocation, BaseTime);
        var b = MakeDetection(BaseLocation, BaseTime.AddSeconds(4.9));
        _detectionStore.AddRange([a, b]);

        await _handler.HandleAsync(a.Id, default);

        Assert.NotNull(a.HotspotId);
        Assert.Equal(a.HotspotId, b.HotspotId);
        Assert.Equal(DetectionProcessingStatus.Processed, a.ProcessingStatus);
    }

    [Fact]
    public async Task HandleAsync_TwoDevicesWith5_1SecondsGap_StaysLone()
    {
        var a = MakeDetection(BaseLocation, BaseTime);
        var b = MakeDetection(BaseLocation, BaseTime.AddSeconds(5.1));
        _detectionStore.AddRange([a, b]);

        await _handler.HandleAsync(a.Id, default);

        Assert.Null(a.HotspotId);
        Assert.Equal(DetectionProcessingStatus.Processed, a.ProcessingStatus);
    }

    [Fact]
    public async Task HandleAsync_SymmetricWindow_LaterDetectionProcessedFirst_StillGroupsWithEarlierOne()
    {
        // Y has a LATER timestamp but is saved and processed first (as a lone
        // detection, since nothing else exists yet). X, saved after Y but with
        // an EARLIER timestamp, is processed next. A backward-only window
        // centered on X's own time would never see Y (Y is "in X's future"
        // relative to X); the symmetric window does.
        var y = MakeDetection(BaseLocation, BaseTime.AddSeconds(3));
        _detectionStore.Add(y);
        await _handler.HandleAsync(y.Id, default);
        Assert.Null(y.HotspotId); // lone at this point, nothing to group with

        var x = MakeDetection(BaseLocation, BaseTime);
        _detectionStore.Add(x);
        await _handler.HandleAsync(x.Id, default);

        Assert.NotNull(x.HotspotId);
        Assert.Equal(x.HotspotId, y.HotspotId);
    }

    [Fact]
    public async Task HandleAsync_GroupOfTwoPending_OnlyDequeuedDetectionBecomesProcessed()
    {
        var a = MakeDetection(BaseLocation, BaseTime);
        var b = MakeDetection(BaseLocation, BaseTime.AddSeconds(1));
        _detectionStore.AddRange([a, b]);

        await _handler.HandleAsync(a.Id, default);

        Assert.Equal(DetectionProcessingStatus.Processed, a.ProcessingStatus);
        Assert.Equal(DetectionProcessingStatus.Pending, b.ProcessingStatus); // deliberately left Pending
        Assert.Equal(a.HotspotId, b.HotspotId); // but already attached to the hotspot

        await _handler.HandleAsync(b.Id, default);

        Assert.Equal(DetectionProcessingStatus.Processed, b.ProcessingStatus);
        Assert.Equal(a.HotspotId, b.HotspotId);
    }

    [Fact]
    public async Task HandleAsync_TransitiveGrouping_ExpandsHotspotOnceMiddleDetectionGetsItsOwnTurn()
    {
        // A sits geographically between B and C: A-B and A-C are within
        // rules, but B-C are not. If B is processed first, its own search
        // (centered on B) never sees C. Only when A gets its own turn (search
        // centered on A) does the group correctly expand to include C.
        var offset = 700.0 / 111_195.0; // ~700 m of latitude
        var a = MakeDetection(BaseLocation, BaseTime);
        var b = MakeDetection(new Coordinates(BaseLocation.Latitude + offset, BaseLocation.Longitude), BaseTime.AddSeconds(1));
        var c = MakeDetection(new Coordinates(BaseLocation.Latitude - offset, BaseLocation.Longitude), BaseTime.AddSeconds(2));
        _detectionStore.AddRange([a, b, c]);

        Assert.True(GeoDistance.Meters(b.Location, c.Location) > Rules.RadiusMeters);

        await _handler.HandleAsync(b.Id, default);

        Assert.NotNull(b.HotspotId);
        Assert.Equal(b.HotspotId, a.HotspotId);
        Assert.Null(c.HotspotId); // not reachable from B's own search

        await _handler.HandleAsync(a.Id, default);

        Assert.Equal(a.HotspotId, c.HotspotId); // now reachable from A's own search
        Assert.Equal(DetectionProcessingStatus.Processed, a.ProcessingStatus);
        Assert.Equal(DetectionProcessingStatus.Pending, c.ProcessingStatus); // still awaiting its own turn
    }

    [Fact]
    public async Task HandleAsync_ReprocessingAlreadyAssignedPendingMember_DoesNotChangeRadius()
    {
        // A+B form a hotspot when A is processed (B stays Pending, per the
        // "own turn" design). B later gets its own turn re-discovering the
        // exact same pair - no new information arrived, so the radius must
        // not change just because B was reprocessed.
        var a = MakeDetection(BaseLocation, BaseTime);
        var b = MakeDetection(BaseLocation, BaseTime.AddSeconds(4.9));
        _detectionStore.AddRange([a, b]);

        await _handler.HandleAsync(a.Id, default);
        var radiusAfterFormation = _hotspotStore[a.HotspotId!.Value].RadiusMeters;

        await _handler.HandleAsync(b.Id, default);
        var radiusAfterReprocessing = _hotspotStore[a.HotspotId!.Value].RadiusMeters;

        Assert.Equal(radiusAfterFormation, radiusAfterReprocessing);
    }

    [Fact]
    public async Task HandleAsync_AnchorBelongsToLosingBridgeHotspot_DoesNotThrow()
    {
        // Hotspot B forms first, entirely on its own - A's detections don't
        // exist in the store yet, so B can't reach them.
        var bMember1 = MakeDetection(BaseLocation, BaseTime.AddSeconds(8));
        var bMember2 = MakeDetection(BaseLocation, BaseTime.AddSeconds(8.1));
        _detectionStore.AddRange([bMember1, bMember2]);
        await _handler.HandleAsync(bMember1.Id, default);
        var hotspotB = bMember2.HotspotId!.Value;

        // Hotspot A forms next: anchorPartner's own +-5s window ([T-5, T+5])
        // doesn't reach B's members (8s/8.1s away), so A forms cleanly,
        // separate from B.
        var anchorPartner = MakeDetection(BaseLocation, BaseTime);
        var anchor = MakeDetection(BaseLocation, BaseTime.AddSeconds(4));
        _detectionStore.AddRange([anchorPartner, anchor]);
        await _handler.HandleAsync(anchorPartner.Id, default);
        var hotspotA = anchor.HotspotId!.Value;
        Assert.NotEqual(hotspotA, hotspotB);

        // Now anchor gets its own turn. Its window ([T-1, T+9]) reaches both
        // its own A-partner and both of B's members - bridging picks B (tie
        // on member count broken by more recent timestamp), which filters
        // the anchor itself OUT of the group (it belongs to A, not B). Every
        // remaining candidate already belongs to B. This must not throw.
        var ex = await Record.ExceptionAsync(() => _handler.HandleAsync(anchor.Id, default));

        Assert.Null(ex);
        // Anchor belongs to the losing hotspot (A) and isn't part of B's
        // winning group, so per the documented bridging-loser behavior it's
        // left untouched - still A, just marked Processed for this pass.
        Assert.Equal(hotspotA, anchor.HotspotId);
        Assert.Equal(DetectionProcessingStatus.Processed, anchor.ProcessingStatus);
    }

    [Fact]
    public async Task HandleAsync_MultipleDetectionsFromOneDevice_AllRemainAssignedToHotspot()
    {
        var deviceA = DeviceId.New();
        var a1 = MakeDetection(BaseLocation, BaseTime, deviceA);
        var a2 = MakeDetection(BaseLocation, BaseTime.AddSeconds(1), deviceA);
        var a3 = MakeDetection(BaseLocation, BaseTime.AddSeconds(2), deviceA);
        var b = MakeDetection(BaseLocation, BaseTime.AddSeconds(3));
        _detectionStore.AddRange([a1, a2, a3, b]);

        await _handler.HandleAsync(a1.Id, default);

        Assert.NotNull(a1.HotspotId);
        Assert.Equal(a1.HotspotId, a2.HotspotId);
        Assert.Equal(a1.HotspotId, a3.HotspotId);
        Assert.Equal(a1.HotspotId, b.HotspotId);
        // Dedup only affects the spatial (centroid/radius) calculation, not
        // membership or DeviceCount.
        Assert.Equal(2, _hotspotStore[a1.HotspotId!.Value].DeviceCount);
    }

    [Fact]
    public async Task HandleAsync_FailedDetectionIsExcludedFromCandidates()
    {
        var failed = MakeDetection(BaseLocation, BaseTime);
        failed.MarkFailed();
        var incoming = MakeDetection(BaseLocation, BaseTime.AddSeconds(1));
        _detectionStore.AddRange([failed, incoming]);

        await _handler.HandleAsync(incoming.Id, default);

        Assert.Null(incoming.HotspotId);
        Assert.Equal(DetectionProcessingStatus.Processed, incoming.ProcessingStatus);
    }
}
