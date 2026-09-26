using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace IdentityServer.RateLimiting;

public static class RateLimitingExtensions
{
    private const string LoginPath = "/Account/Login";
    private const string TokenPath = "/connect/token";
    private const string ClientIdItemKey = "RateLimiting:ClientId";

    /// <summary>
    /// Limits login form submissions per IP and token endpoint requests per client. Other requests
    /// are not limited. Rejected requests get 429 with a Retry-After header.
    /// </summary>
    /// <remarks>Counters are kept in memory, so each IdentityServer instance has its own.</remarks>
    public static IServiceCollection AddIdentityServerRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
            ?? new RateLimitingOptions();
        var window = TimeSpan.FromSeconds(options.WindowSeconds);

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? (int)Math.Ceiling(retryAfter.TotalSeconds)
                    : options.WindowSeconds;
                var response = context.HttpContext.Response;
                response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

                // The login page is shown in a browser, so explain instead of returning an empty page.
                if (IsLogin(context.HttpContext.Request))
                {
                    await response.WriteAsync(
                        $"Too many login attempts. Try again in {retryAfterSeconds} seconds.", cancellationToken);
                }
            };

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var request = httpContext.Request;
                if (IsLogin(request))
                {
                    return FixedWindow($"login:{httpContext.Connection.RemoteIpAddress}", options.LoginPermitLimit, window);
                }

                if (IsTokenRequest(request))
                {
                    var client = httpContext.Items[ClientIdItemKey] as string
                        ?? $"ip:{httpContext.Connection.RemoteIpAddress}";
                    return FixedWindow($"token:{client}", options.TokenPermitLimit, window);
                }

                return RateLimitPartition.GetNoLimiter(string.Empty);
            });
        });

        return services;
    }

    /// <summary>
    /// Adds the rate limiter, preceded by a step that finds the client_id of token requests (the
    /// partition function can't read the request body itself). Call before <c>UseIdentityServer()</c>.
    /// </summary>
    public static IApplicationBuilder UseIdentityServerRateLimiting(this IApplicationBuilder app)
    {
        app.Use(async (httpContext, next) =>
        {
            if (IsTokenRequest(httpContext.Request))
            {
                httpContext.Items[ClientIdItemKey] = await GetClientIdAsync(httpContext.Request);
            }
            await next();
        });

        return app.UseRateLimiter();
    }

    // Clients send their id either in a Basic Authorization header (IdentityModel's default) or in
    // the form body (client_secret_post, used by the ASP.NET Core OpenID Connect handler).
    // The form is buffered, so IdentityServer can still read it afterwards.
    private static async Task<string?> GetClientIdAsync(HttpRequest request)
    {
        if (AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out var header)
            && "Basic".Equals(header.Scheme, StringComparison.OrdinalIgnoreCase)
            && header.Parameter != null)
        {
            try
            {
                var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
                var separator = credentials.IndexOf(':');
                if (separator > 0)
                {
                    return Uri.UnescapeDataString(credentials[..separator]);
                }
            }
            catch (FormatException)
            {
                // Not valid Base64; IdentityServer rejects the request, it's limited by IP here.
            }
        }

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            var clientId = form["client_id"].ToString();
            return string.IsNullOrEmpty(clientId) ? null : clientId;
        }

        return null;
    }

    private static bool IsLogin(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) && request.Path.Equals(LoginPath, StringComparison.OrdinalIgnoreCase);

    private static bool IsTokenRequest(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) && request.Path.Equals(TokenPath, StringComparison.OrdinalIgnoreCase);

    private static RateLimitPartition<string> FixedWindow(string key, int permitLimit, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0
        });
}
