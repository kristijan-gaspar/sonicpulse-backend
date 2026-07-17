using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SonicPulse.Application.Abstractions;
using SonicPulse.Infrastructure.Persistence;
using SonicPulse.Infrastructure.Processing;
using SonicPulse.Infrastructure.Repositories;

namespace SonicPulse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException(
                        "ConnectionStrings:DefaultConnection is not configured."),
                npgsql => npgsql.UseNetTopologySuite()));

        services.AddScoped<IDetectionRepository, DetectionRepository>();
        services.AddScoped<IHotspotRepository, HotspotRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IDetectionProcessingQueue, ChannelDetectionQueue>();
        services.AddHostedService<DetectionProcessingWorker>();

        return services;
    }
}
