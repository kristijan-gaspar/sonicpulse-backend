using Microsoft.AspNetCore.RateLimiting;
using SonicPulse.Api.Configuration;
using System.Globalization;
using System.Threading.RateLimiting;

namespace SonicPulse.Api.Extensions;

public static class RateLimitingExtensions
{
    public const string DetectionSubmitPolicy = "DetectionSubmit";

    public static IServiceCollection AddApiRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetRequiredSection(RateLimitingOptions.SectionName)
            .Get<RateLimitingOptions>()
            ?? throw new InvalidOperationException(
                "RateLimiting configuration is missing.");

        ValidateRateLimitingOptions(options);

        services.AddRateLimiter(rateLimiterOptions =>
        {
            rateLimiterOptions.RejectionStatusCode =
                StatusCodes.Status429TooManyRequests;

            rateLimiterOptions.AddFixedWindowLimiter(
                DetectionSubmitPolicy,
                limiterOptions =>
                {
                    limiterOptions.PermitLimit =
                        options.PermitLimit;

                    limiterOptions.Window =
                        TimeSpan.FromSeconds(
                            options.WindowSeconds);

                    limiterOptions.QueueLimit = 0;

                    limiterOptions.AutoReplenishment = true;
                });

            rateLimiterOptions.OnRejected =
               async (context, _) =>
               {
                   if (context.Lease.TryGetMetadata(
                           MetadataName.RetryAfter,
                           out var retryAfter))
                   {
                       var retryAfterSeconds =
                           (long)Math.Ceiling(
                               retryAfter.TotalSeconds);

                       context.HttpContext.Response.Headers.RetryAfter =
                           retryAfterSeconds.ToString(
                               CultureInfo.InvariantCulture);
                   }

                   await context.HttpContext.Response
                       .WriteProblemAsync(
                           StatusCodes.Status429TooManyRequests,
                           "Too many requests.");
               };
        });

        return services;
    }

    private static void ValidateRateLimitingOptions(RateLimitingOptions options)
    {
        if (options.PermitLimit <= 0)
            throw new InvalidOperationException(
                "RateLimiting:PermitLimit must be greater than 0.");

        if (options.WindowSeconds <= 0)
            throw new InvalidOperationException(
                "RateLimiting:WindowSeconds must be greater than 0.");
    }
}