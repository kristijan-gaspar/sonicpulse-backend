using Microsoft.AspNetCore.Mvc;
using SonicPulse.Application.Hotspots.Handlers;

namespace SonicPulse.Api.Controllers;

[ApiController]
[Route("api/hotspots")]
public sealed class HotspotsController(GetHotspotsHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int sinceHours = 24, CancellationToken ct = default)
        => Ok(await handler.HandleAsync(sinceHours, ct));
}
