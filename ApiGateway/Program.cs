using ApiGateway;
using ApiGateway.Observability;
using ApiGateway.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"ocelot.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

// Traces, metrics and logs go to the OpenTelemetry collector (see Observability/ and the
// "OpenTelemetry" section in appsettings).
builder.Services.AddObservability(builder.Configuration, builder.Environment);

// Also the default scheme, so UseAuthentication() identifies the caller for rate limiting
// before Ocelot runs its own per-route authentication.
builder.Services.AddAuthentication("IdentityApiKey")
    .AddJwtBearer("IdentityApiKey", x =>
    {
        x.Authority = builder.Configuration["IdentityServer:Authority"]; // IDENTITY SERVER URL
        x.RequireHttpsMetadata = builder.Configuration.GetValue("IdentityServer:RequireHttpsMetadata", true);
        x.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false
        };
    });

// Per-user limits for API calls (see RateLimiting/ and the RateLimiting section in appsettings.json).
builder.Services.AddGatewayRateLimiting(builder.Configuration);

// Downstream hosts are resolved from Consul (see GlobalConfiguration.ServiceDiscoveryProvider in ocelot.json).
builder.Services.AddOcelot()
    .AddConsul<ServiceAddressConsulServiceBuilder>();

var app = builder.Build();


app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.MapControllers();

await app.UseOcelot();

app.Run();
