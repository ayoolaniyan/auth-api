using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ApiGateway.RateLimiting;

public static class ServiceCollectionExtensions
{
    private const int SegmentsPerWindow = 6;

    /// <summary>
    /// Limits requests per caller with a sliding window. Callers with a valid access token get their
    /// own limit (by user, or by client when the token has no user); everyone else is limited by IP.
    /// Rejected requests get 429 with a Retry-After header.
    /// </summary>
    /// <remarks>
    /// Needs <c>UseAuthentication()</c> before <c>UseRateLimiter()</c> so the caller is known. Counters
    /// are kept in memory, so each gateway instance has its own.
    /// </remarks>
    public static IServiceCollection AddGatewayRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
            ?? new RateLimitingOptions();
        var window = TimeSpan.FromSeconds(options.WindowSeconds);
        // The sliding window limiter doesn't say when a permit frees up, so Retry-After is one
        // segment: the soonest the oldest requests can drop out of the window.
        var segment = window / SegmentsPerWindow;

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = (context, _) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var reported)
                    ? reported
                    : segment;
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var caller = GetCaller(httpContext.User);
                return caller != null
                    ? SlidingWindow(caller, options.AuthenticatedPermitLimit, window)
                    : SlidingWindow($"ip:{httpContext.Connection.RemoteIpAddress}", options.AnonymousPermitLimit, window);
            });
        });

        return services;
    }

    // "sub" is mapped to NameIdentifier by the JWT handler; client-credentials tokens only have client_id.
    private static string? GetCaller(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var subject = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (subject != null)
        {
            return $"user:{subject}";
        }

        var clientId = user.FindFirst("client_id")?.Value;
        return clientId != null ? $"client:{clientId}" : null;
    }

    private static RateLimitPartition<string> SlidingWindow(string key, int permitLimit, TimeSpan window) =>
        RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            SegmentsPerWindow = SegmentsPerWindow,
            QueueLimit = 0
        });
}
