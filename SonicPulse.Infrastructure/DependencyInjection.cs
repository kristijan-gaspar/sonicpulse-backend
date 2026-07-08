using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SonicPulse.Application.Abstractions;
using SonicPulse.Infrastructure.Persistence;
using SonicPulse.Infrastructure.Repositories;

namespace SonicPulse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IDetectionRepository, DetectionRepository>();

        return services;
    }
}
