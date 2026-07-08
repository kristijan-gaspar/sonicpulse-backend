using Microsoft.AspNetCore.Mvc;
using SonicPulse.Application.Detections.Handlers;

namespace SonicPulse.Api.Controllers;

[ApiController]
[Route("api/devices")]
public sealed class DevicesController(
    GetDetectionsByDeviceHandler getByDeviceHandler) : ControllerBase
{
    [HttpGet("{deviceId:guid}/detections")]
    public async Task<IActionResult> GetDetections(
        Guid deviceId,
        [FromQuery] long? cursor = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var result = await getByDeviceHandler.HandleAsync(deviceId, cursor, limit, ct);
        return Ok(result);
    }
}
