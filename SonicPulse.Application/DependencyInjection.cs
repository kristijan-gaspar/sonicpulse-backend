using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SonicPulse.Application.Detections.Handlers;
using SonicPulse.Application.Detections.Validators;
using SonicPulse.Application.Hotspots.Handlers;

namespace SonicPulse.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<SubmitDetectionHandler>();
        services.AddScoped<GetDetectionByIdHandler>();
        services.AddScoped<GetDetectionsByDeviceHandler>();
        services.AddScoped<ProcessDetectionHandler>();
        services.AddScoped<GetHotspotsHandler>();

        services.AddValidatorsFromAssemblyContaining<SubmitDetectionRequestValidator>();

        return services;
    }
}
