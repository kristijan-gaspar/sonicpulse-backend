namespace SonicPulse.Application.Detections.Dtos;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, long? NextCursor);
