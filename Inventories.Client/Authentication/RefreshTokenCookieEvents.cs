using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Inventories.Client.Authentication
{
    /// <summary>
    /// Keeps the access token in the login cookie fresh. On every authenticated request, if the
    /// access token expires within <see cref="RefreshBeforeExpiry"/>, it is renewed with the refresh
    /// token and the new access token, refresh token and expiry are written back to the cookie.
    /// </summary>
    /// <remarks>
    /// Runs before controllers, so both the Inventory API calls and the userinfo call always see a
    /// valid token. If IdentityServer rejects the refresh token the user is signed out, and
    /// [Authorize] pages send them back to the login page.
    /// </remarks>
    public class RefreshTokenCookieEvents : CookieAuthenticationEvents
    {
        private static readonly TimeSpan RefreshBeforeExpiry = TimeSpan.FromSeconds(60);

        private readonly RefreshTokenService _refreshTokenService;

        public RefreshTokenCookieEvents(RefreshTokenService refreshTokenService)
        {
            _refreshTokenService = refreshTokenService ?? throw new ArgumentNullException(nameof(refreshTokenService));
        }

        public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            var expiresAt = context.Properties.GetTokenValue("expires_at");
            var refreshToken = context.Properties.GetTokenValue(OpenIdConnectParameterNames.RefreshToken);

            // Sessions from before refresh tokens were enabled can't be renewed; sign in again.
            if (expiresAt == null || refreshToken == null)
            {
                await SignOutAsync(context);
                return;
            }

            var expires = DateTimeOffset.Parse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (expires - RefreshBeforeExpiry > DateTimeOffset.UtcNow)
            {
                return;
            }

            var (status, tokens) = await _refreshTokenService.RefreshAsync(refreshToken, context.HttpContext.RequestAborted);
            switch (status)
            {
                case RefreshStatus.Refreshed:
                    context.Properties.UpdateTokenValue(OpenIdConnectParameterNames.AccessToken, tokens!.AccessToken);
                    context.Properties.UpdateTokenValue(OpenIdConnectParameterNames.RefreshToken, tokens.RefreshToken);
                    context.Properties.UpdateTokenValue("expires_at",
                        tokens.ExpiresAt.ToString("o", CultureInfo.InvariantCulture));
                    // Re-issue the cookie so the browser stores the new tokens.
                    context.ShouldRenew = true;
                    break;

                case RefreshStatus.Rejected:
                    await SignOutAsync(context);
                    break;

                case RefreshStatus.Failed:
                    // Likely temporary (IdentityServer unreachable): keep the session and try
                    // again on the next request.
                    break;
            }
        }

        private static async Task SignOutAsync(CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
