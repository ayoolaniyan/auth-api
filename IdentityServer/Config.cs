using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;

namespace IdentityServer
{
    public class Config
    {
        public static IEnumerable<Client> Clients =>
            new Client[]
            {
                   new Client
                   {
                        ClientId = "inventoryClient",
                        AllowedGrantTypes = GrantTypes.ClientCredentials,
                        ClientSecrets =
                        {
                            new Secret("secret".Sha256())
                        },
                        AllowedScopes = { "inventoryAPI" }
                   },
                   new Client
                   {
                       ClientId = "inventories_mvc_client",
                       ClientName = "Inventory MVC Web App",
                       AllowedGrantTypes = GrantTypes.Hybrid,
                       RequirePkce = false,
                       AllowRememberConsent = false,
                       RedirectUris = new List<string>()
                       {
                           "https://localhost:5181/signin-oidc",
                           "http://localhost:5181/signin-oidc" // Docker
                       },
                       PostLogoutRedirectUris = new List<string>()
                       {
                           "https://localhost:5181/signout-callback-oidc",
                           "http://localhost:5181/signout-callback-oidc" // Docker
                       },
                       ClientSecrets = new List<Secret>
                       {
                           new Secret("secret".Sha256())
                       },
                       AllowedScopes = new List<string>
                       {
                           IdentityServerConstants.StandardScopes.OpenId,
                           IdentityServerConstants.StandardScopes.Profile,
                           IdentityServerConstants.StandardScopes.Address,
                           IdentityServerConstants.StandardScopes.Email,
                           "inventoryAPI",
                           "roles"
                       },

                       // Refresh token rotation: the client may request offline_access, and every
                       // refresh returns a new refresh token while the one just used stops working.
                       AllowOfflineAccess = true,
                       RefreshTokenUsage = TokenUsage.OneTimeOnly,
                       // Each refresh extends the lifetime by the sliding window, up to the absolute
                       // limit counted from login; after that the user has to sign in again.
                       RefreshTokenExpiration = TokenExpiration.Sliding,
                       SlidingRefreshTokenLifetime = (int)TimeSpan.FromDays(15).TotalSeconds,
                       AbsoluteRefreshTokenLifetime = (int)TimeSpan.FromDays(30).TotalSeconds,
                       // Short-lived access tokens are fine now that the client renews them itself.
                       AccessTokenLifetime = (int)TimeSpan.FromMinutes(5).TotalSeconds,
                       // Re-read the user's claims (e.g. roles) on every refresh.
                       UpdateAccessTokenClaimsOnRefresh = true
                   }
            };

        public static IEnumerable<ApiScope> ApiScopes =>
           new ApiScope[]
           {
               new ApiScope("inventoryAPI", "Inventory API")
           };

        public static IEnumerable<ApiResource> ApiResources =>
          new ApiResource[]
          {
               //new ApiResource("inventoryAPI", "Inventory API")
          };

        public static IEnumerable<IdentityResource> IdentityResources =>
          new IdentityResource[]
          {
              new IdentityResources.OpenId(),
              new IdentityResources.Profile(),
              new IdentityResources.Address(),
              new IdentityResources.Email(),
              new IdentityResource(
                    "roles",
                    "Your role(s)",
                    new List<string>() { "role" })
          };

        public static List<TestUser> TestUsers =>
            new List<TestUser>
            {
                new TestUser
                {
                    SubjectId = "5BE86359-073C-434B-AD2D-A3932222DABE",
                    Username = "green",
                    Password = "root",
                    Claims = new List<Claim>
                    {
                        new Claim(JwtClaimTypes.GivenName, "green"),
                        new Claim(JwtClaimTypes.FamilyName, "brown")
                    }
                }
            };
    }
}
