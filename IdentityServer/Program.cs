using Duende.IdentityServer.Models;
using IdentityServer;
using IdentityServerHost.Quickstart.UI;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Load secrets from the repo-root .env file (if present) into environment variables.
// Existing environment variables take precedence.
DotNetEnv.Env.NoClobber().TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();    
builder.Services.AddRazorPages();

var connectionString = new SqlConnectionStringBuilder(
    builder.Configuration.GetConnectionString("IdentityServerDb")
        ?? throw new InvalidOperationException("Connection string 'IdentityServerDb' not found."))
{
    Password = builder.Configuration["MSSQL_SA_PASSWORD"]
        ?? throw new InvalidOperationException("MSSQL_SA_PASSWORD is not set. Copy .env-example to .env and set it.")
}.ConnectionString;
var migrationsAssembly = typeof(Program).Assembly.GetName().Name;

builder.Services
    .AddIdentityServer(options =>
    {
        options.KeyManagement.Enabled = false;
    })
    .AddConfigurationStore(options =>
    {
        options.ConfigureDbContext = b => b.UseSqlServer(connectionString,
            sql => sql.MigrationsAssembly(migrationsAssembly));
    })
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
app.UseIdentityServer();
app.UseAuthorization();
app.MapRazorPages();
app.MapDefaultControllerRoute();

app.Run();
