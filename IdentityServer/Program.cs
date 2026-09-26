using Duende.IdentityServer.Models;
using IdentityServer;
using IdentityServer.Caching;
using IdentityServer.Observability;
using IdentityServer.RateLimiting;
using IdentityServerHost.Quickstart.UI;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Load secrets from the repo-root .env file (if present) into environment variables.
// Existing environment variables take precedence.
DotNetEnv.Env.NoClobber().TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();    
builder.Services.AddRazorPages();

// Traces, metrics and logs go to the OpenTelemetry collector (see Observability/ and the
// "OpenTelemetry" section in appsettings).
builder.Services.AddObservability(builder.Configuration, builder.Environment);

var connectionString = new SqlConnectionStringBuilder(
    builder.Configuration.GetConnectionString("IdentityServerDb")
        ?? throw new InvalidOperationException("Connection string 'IdentityServerDb' not found."))
{
    Password = builder.Configuration["MSSQL_SA_PASSWORD"]
        ?? throw new InvalidOperationException("MSSQL_SA_PASSWORD is not set. Copy .env-example to .env and set it.")
}.ConnectionString;
var migrationsAssembly = typeof(Program).Assembly.GetName().Name;

// Redis holds the configuration store cache and the Data Protection keys (see Caching/).
builder.Services.AddRedisCaching(builder.Configuration);

// Limits login attempts and token requests (see RateLimiting/ and appsettings.json).
builder.Services.AddIdentityServerRateLimiting(builder.Configuration);

builder.Services
    .AddIdentityServer(options =>
    {
        options.KeyManagement.Enabled = false;

        // Fixed issuer so tokens validate the same whether IdentityServer is reached
        // via its public URL (browser) or its internal Docker hostname (other services).
        var issuerUri = builder.Configuration["IdentityServer:IssuerUri"];
        if (!string.IsNullOrWhiteSpace(issuerUri))
        {
            options.IssuerUri = issuerUri;
        }
    })
    .AddConfigurationStore(options =>
    {
        options.ConfigureDbContext = b => b.UseSqlServer(connectionString,
            sql => sql.MigrationsAssembly(migrationsAssembly));
    })
    // Clients, resources and scopes are read from Redis instead of SQL Server on every request.
    .AddConfigurationStoreCache()
    .AddOperationalStore(options =>
    {
        options.ConfigureDbContext = b => b.UseSqlServer(connectionString,
            sql => sql.MigrationsAssembly(migrationsAssembly));
        options.EnableTokenCleanup = true;
    })
    .AddTestUsers(TestUsers.Users)
    .AddDeveloperSigningCredential();

var app = builder.Build();

SeedData.InitializeDatabase(app);

app.UseStaticFiles();
app.UseRouting();
app.UseIdentityServerRateLimiting();
app.UseIdentityServer();
app.UseAuthorization();
app.MapRazorPages();
app.MapDefaultControllerRoute();

app.Run();
