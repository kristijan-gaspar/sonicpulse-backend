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
    public async Task HandleAsync_ReassemblePass_ExcludesAlreadyAssignedMemberFromRecentBatch()
    {
        // A+B form a hotspot when A is processed (B stays Pending, per the
        // "own turn" design). Later, B gets its own turn: its search also
        // finds A (already H1-assigned, 4.9s earlier - still within window)
        // and a brand-new C landing at the exact same instant as B. If
        // recentBatch naively included A, the 4.9s span would nearly floor
        // the time-compactness term; excluding already-assigned A (while
        // still including anchor B) should keep it tight instead.
        var a = MakeDetection(BaseLocation, BaseTime, DeviceId.New());
        var b = MakeDetection(BaseLocation, BaseTime.AddSeconds(4.9), DeviceId.New());
        _detectionStore.AddRange([a, b]);

        await _handler.HandleAsync(a.Id, default);
        Assert.NotNull(a.HotspotId);
        Assert.Equal(a.HotspotId, b.HotspotId);

        var c = MakeDetection(BaseLocation, BaseTime.AddSeconds(4.9), DeviceId.New());
        _detectionStore.Add(c);

        await _handler.HandleAsync(b.Id, default);

        Assert.Equal(a.HotspotId, c.HotspotId);
        var hotspot = _hotspotStore[a.HotspotId!.Value];
        Assert.Equal(3, hotspot.DeviceCount);
        // deviceFactor: 3 devices -> 30. timeCompactness: recentBatch is {b, c},
        // both at the same instant -> 50. Without the fix this would be ~31
        // (recentBatch {a, b, c} spans 4.9s, timeCompactness collapses to ~1).
        Assert.Equal(80, hotspot.Confidence);
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
