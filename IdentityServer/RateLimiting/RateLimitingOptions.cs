namespace IdentityServer.RateLimiting;

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    // Login form submissions per IP address per window (slows down password guessing).
    public int LoginPermitLimit { get; set; } = 5;

    // Token endpoint requests per client per window. Refreshes for all users of a client come from
    // the same server, so this is per client_id rather than per IP.
    public int TokenPermitLimit { get; set; } = 300;

    public int WindowSeconds { get; set; } = 60;
}
