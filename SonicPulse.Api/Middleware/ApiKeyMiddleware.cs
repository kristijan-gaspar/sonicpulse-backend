using System.Security.Cryptography;
using System.Text;
using SonicPulse.Api.Extensions;

namespace SonicPulse.Api.Middleware;

public sealed class ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private const string HeaderName = "X-Api-Key";
    private readonly byte[] _expectedKey =
        Encoding.UTF8.GetBytes(configuration["ApiKey"]
            ?? throw new InvalidOperationException("ApiKey is not configured."));

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var provided)
            || !FixedTimeEquals(provided.ToString()))
        {
            await context.Response.WriteProblemAsync(
                StatusCodes.Status401Unauthorized, "Missing or invalid API key.");
            return;
        }

        await next(context);
    }

    private bool FixedTimeEquals(string provided)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return providedBytes.Length == _expectedKey.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, _expectedKey);
    }
}
