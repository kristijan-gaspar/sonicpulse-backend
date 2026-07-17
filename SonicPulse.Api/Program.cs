using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;
using SonicPulse.Api.Configuration;
using SonicPulse.Api.Middleware;
using SonicPulse.Application;
using SonicPulse.Domain.Rules;
using SonicPulse.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

builder.Services.AddControllers();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);


builder.Services.Configure<GroupingOptions>(
    builder.Configuration.GetSection(GroupingOptions.SectionName));

builder.Services.AddSingleton(sp =>
{
    var o = sp.GetRequiredService<IOptions<GroupingOptions>>().Value;
    return new GroupingRules(
        TimeSpan.FromSeconds(o.TimeWindowSeconds),
        o.RadiusMeters,
        o.MinDeviceCount,
        o.CandidateSearchExpansionFactor);
});

builder.Services.AddOpenApi();

var app = builder.Build();

// Force GroupingRules construction now, not on first request: invalid
// config (e.g. MinDeviceCount = 1 in appsettings) crashes startup instead
// of surfacing as a 500 on whichever request happens to hit it first.
app.Services.GetRequiredService<GroupingRules>();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
