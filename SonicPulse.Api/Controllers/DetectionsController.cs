using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SonicPulse.Api.Extensions;
using SonicPulse.Application.Detections.Dtos;
using SonicPulse.Application.Detections.Handlers;

namespace SonicPulse.Api.Controllers;

[ApiController]
[Route("api/detections")]
public sealed class DetectionsController(
    IValidator<SubmitDetectionRequest> validator,
    SubmitDetectionHandler submitHandler,
    GetDetectionByIdHandler getByIdHandler) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting(RateLimitingExtensions.DetectionSubmitPolicy)]
    public async Task<IActionResult> Submit(
        SubmitDetectionRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(validation.ToModelState());

        var result = await submitHandler.HandleAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await getByIdHandler.HandleAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }
}
