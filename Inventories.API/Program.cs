using Inventories.API;
using Inventories.API.Caching;
using Inventories.API.Data;
using Inventories.API.Observability;
using Inventories.API.ServiceDiscovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<InventoriesContext>(opt => opt.UseInMemoryDatabase("InventoriesContext"));

builder.Services.AddControllers();

// Traces, metrics and logs go to the OpenTelemetry collector (see Observability/ and the
// "OpenTelemetry" section in appsettings).
builder.Services.AddObservability(builder.Configuration, builder.Environment);

// Inventory reads are cached in Redis and shared by every instance. Redis is deliberately
// not part of /health: if it goes down the API keeps serving from the database.
builder.Services.AddRedisDistributedCache(builder.Configuration);

// Consul polls /health; the gateway only routes to instances whose check passes.
builder.Services.AddHealthChecks();
builder.Services.AddConsulServiceDiscovery(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["IdentityServer:Authority"];
        options.RequireHttpsMetadata = builder.Configuration.GetValue("IdentityServer:RequireHttpsMetadata", true);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false
        };
    });

builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("ClientIdPolicy", policy => policy.RequireClaim("client_id", "inventoryClient", "inventories_mvc_client"));
    });

var app = builder.Build();
app.SeedDatabase();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
