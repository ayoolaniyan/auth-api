namespace ApiGateway.RateLimiting;

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    // Requests per window for a caller with a valid access token (keyed by user, or by client
    // for client-credentials tokens that have no user).
    public int AuthenticatedPermitLimit { get; set; } = 100;

    // Requests per window for callers without a valid token (keyed by IP address).
    public int AnonymousPermitLimit { get; set; } = 20;

    public int WindowSeconds { get; set; } = 60;
}
