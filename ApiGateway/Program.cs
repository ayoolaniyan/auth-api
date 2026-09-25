using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"ocelot.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

builder.Services.AddAuthentication()
    .AddJwtBearer("IdentityApiKey", x =>
    {
        x.Authority = builder.Configuration["IdentityServer:Authority"]; // IDENTITY SERVER URL
        x.RequireHttpsMetadata = builder.Configuration.GetValue("IdentityServer:RequireHttpsMetadata", true);
        x.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false
        };
    });

builder.Services.AddOcelot();

var app = builder.Build();


app.UseRouting();
app.MapControllers();

await app.UseOcelot();

app.Run();
