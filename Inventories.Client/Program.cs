using Inventories.Client.ApiServices;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Net.Http.Headers;
using Inventories.Client.HttpHandlers;
using Duende.IdentityModel.Client;
using Microsoft.IdentityModel.Tokens;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IInventoryApiService, InventoryApiService>();

// Public URL is what the browser uses; internal URL is used for server-to-server calls
// (they differ when running in Docker, see appsettings.Docker.json).
var identityServerUrl = builder.Configuration["IdentityServer:Authority"]!;
var identityServerInternalUrl = builder.Configuration["IdentityServer:InternalAuthority"] ?? identityServerUrl;
var apiGatewayUrl = builder.Configuration["ApiGateway:BaseUrl"]!;
var requireHttpsMetadata = builder.Configuration.GetValue("IdentityServer:RequireHttpsMetadata", true);

// Discovery validation for back-channel calls (e.g. userinfo): the issuer is the public URL,
// but endpoints are served from the internal URL when running in Docker.
builder.Services.AddSingleton(new DiscoveryPolicy
{
    Authority = identityServerUrl,
    RequireHttps = requireHttpsMetadata,
    AdditionalEndpointBaseAddresses = { identityServerInternalUrl }
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.AccessDeniedPath = "/Account/AccessDenied";
    })
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {                    
        options.Authority = identityServerUrl;
        options.MetadataAddress = $"{identityServerInternalUrl.TrimEnd('/')}/.well-known/openid-configuration";
        options.RequireHttpsMetadata = requireHttpsMetadata;

        // Discovery is fetched from the internal URL, so browser redirects must be
        // rewritten back to the public URL.
        if (identityServerInternalUrl != identityServerUrl)
        {
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                context.ProtocolMessage.IssuerAddress = context.ProtocolMessage.IssuerAddress
                    .Replace(identityServerInternalUrl, identityServerUrl);
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToIdentityProviderForSignOut = context =>
            {
                context.ProtocolMessage.IssuerAddress = context.ProtocolMessage.IssuerAddress
                    .Replace(identityServerInternalUrl, identityServerUrl);
                return Task.CompletedTask;
            };
        }

        options.ClientId = "inventories_mvc_client";
        options.ClientSecret = "secret";
        options.ResponseType = "code id_token";

        // options.Scope.Add("openid");
        // options.Scope.Add("profile");
        options.Scope.Add("address");
        options.Scope.Add("email");
        options.Scope.Add("inventoryAPI");
        options.Scope.Add("roles");

        options.ClaimActions.MapUniqueJsonKey("role", "role");

        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {                       
            NameClaimType = JwtClaimTypes.GivenName,
            RoleClaimType = JwtClaimTypes.Role
        };
    });

// 1 create an HttpClient used for accessing the Movies.API
builder.Services.AddTransient<AuthenticationDelegatingHandler>();
           
builder.Services.AddHttpClient("InventoryAPIClient", client =>
{
    client.BaseAddress = new Uri(apiGatewayUrl); // API GATEWAY URL
    client.DefaultRequestHeaders.Clear();
    client.DefaultRequestHeaders.Add(HeaderNames.Accept, "application/json");
}).AddHttpMessageHandler<AuthenticationDelegatingHandler>();

// 2 create an HttpClient used for accessing the IDP
builder.Services.AddHttpClient("IDPClient", client =>
{
    client.BaseAddress = new Uri(identityServerInternalUrl);
    client.DefaultRequestHeaders.Clear();
    client.DefaultRequestHeaders.Add(HeaderNames.Accept, "application/json");
});

builder.Services.AddHttpContextAccessor();

// builder.Services.AddSingleton(new ClientCredentialsTokenRequest
// {                                                
//    Address = "https://localhost:5203/connect/token",
//    ClientId = "inventoryClient",
//    ClientSecret = "secret",
//    Scope = "inventoryAPI"
// });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
