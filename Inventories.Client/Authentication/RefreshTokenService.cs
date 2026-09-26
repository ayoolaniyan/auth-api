using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Inventories.Client.Authentication
{
    public record RefreshedTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

    public enum RefreshStatus
    {
        Refreshed,
        // The refresh token was rejected (used, revoked or expired): the user must sign in again.
        Rejected,
        // IdentityServer couldn't be reached or returned an unexpected error.
        Failed
    }

    /// <summary>
    /// Redeems refresh tokens at IdentityServer's token endpoint.
    /// </summary>
    /// <remarks>
    /// Refresh tokens are one-time-only, so when several requests from the same user find an
    /// expired access token at once, only the first may call IdentityServer; a second call with the
    /// same refresh token would be rejected. Callers with the same refresh token are serialized and
    /// the later ones get the result of the first from a short-lived cache. The lock and cache are
    /// per process, which is enough while the client runs as a single instance.
    /// </remarks>
    public class RefreshTokenService
    {
        private static readonly TimeSpan ResultCacheDuration = TimeSpan.FromMinutes(1);

        // A fixed set of locks picked by hash, so no per-token lock objects accumulate.
        private readonly SemaphoreSlim[] _locks =
            Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<OpenIdConnectOptions> _oidcOptions;
        private readonly IMemoryCache _cache;
        private readonly ILogger<RefreshTokenService> _logger;

        public RefreshTokenService(IHttpClientFactory httpClientFactory, IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
            IMemoryCache cache, ILogger<RefreshTokenService> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _oidcOptions = oidcOptions ?? throw new ArgumentNullException(nameof(oidcOptions));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<(RefreshStatus Status, RefreshedTokens? Tokens)> RefreshAsync(string refreshToken,
            CancellationToken cancellationToken)
        {
            var gate = _locks[(refreshToken.GetHashCode() & int.MaxValue) % _locks.Length];
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (_cache.TryGetValue(CacheKey(refreshToken), out RefreshedTokens? cached))
                {
                    return (RefreshStatus.Refreshed, cached);
                }

                // Same client credentials and discovery document the OpenID Connect handler uses.
                var oidc = _oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
                var configuration = await oidc.ConfigurationManager!.GetConfigurationAsync(cancellationToken);

                var response = await _httpClientFactory.CreateClient("IDPClient").RequestRefreshTokenAsync(
                    new RefreshTokenRequest
                    {
                        Address = configuration.TokenEndpoint,
                        ClientId = oidc.ClientId!,
                        ClientSecret = oidc.ClientSecret,
                        RefreshToken = refreshToken
                    }, cancellationToken);

                if (response.IsError)
                {
                    if (response.Error == "invalid_grant")
                    {
                        _logger.LogInformation("Refresh token was rejected by IdentityServer; signing the user out");
                        return (RefreshStatus.Rejected, null);
                    }

                    _logger.LogWarning(response.Exception, "Refreshing the access token failed: {Error}", response.Error);
                    return (RefreshStatus.Failed, null);
                }

                var tokens = new RefreshedTokens(
                    response.AccessToken!,
                    // With rotation a new refresh token is always returned; keep the old one otherwise.
                    response.RefreshToken ?? refreshToken,
                    DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn));

                _cache.Set(CacheKey(refreshToken), tokens, ResultCacheDuration);
                return (RefreshStatus.Refreshed, tokens);
            }
            finally
            {
                gate.Release();
            }
        }

        private static string CacheKey(string refreshToken) => $"refreshed:{refreshToken}";
    }
}
