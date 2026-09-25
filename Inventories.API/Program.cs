using Inventories.API;
using Inventories.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<InventoriesContext>(opt => opt.UseInMemoryDatabase("InventoriesContext"));

builder.Services.AddControllers();

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

app.Run();
