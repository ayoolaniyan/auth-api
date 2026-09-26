using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer
{
    public static class SeedData
    {
        // Applies pending migrations for both IdentityServer stores, syncs clients from
        // Config.cs and seeds the other configuration data when the tables are empty.
        public static void InitializeDatabase(IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();

            scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.Migrate();

            var context = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
            context.Database.Migrate();

            // Clients are re-synced from Config.cs on every start (not only when the table is
            // empty), so settings such as token lifetimes also reach existing databases.
            // Removing a client cascades to its secrets, scopes, redirect URIs, etc.
            var clientCache = scope.ServiceProvider.GetRequiredService<ICache<Client>>();
            foreach (var client in Config.Clients)
            {
                var existing = context.Clients.FirstOrDefault(c => c.ClientId == client.ClientId);
                if (existing != null)
                {
                    context.Clients.Remove(existing);
                    // Delete first: ClientId is unique, so the replacement can't be inserted yet.
                    context.SaveChanges();
                }
                context.Clients.Add(client.ToEntity());

                // Drop the Redis copy so the new settings apply now instead of after it expires.
                clientCache.RemoveAsync(client.ClientId).GetAwaiter().GetResult();
            }
            context.SaveChanges();

            if (!context.IdentityResources.Any())
            {
                foreach (var resource in Config.IdentityResources)
                {
                    context.IdentityResources.Add(resource.ToEntity());
                }
                context.SaveChanges();
            }

            if (!context.ApiScopes.Any())
            {
                foreach (var scopeItem in Config.ApiScopes)
                {
                    context.ApiScopes.Add(scopeItem.ToEntity());
                }
                context.SaveChanges();
            }

            if (!context.ApiResources.Any())
            {
                foreach (var resource in Config.ApiResources)
                {
                    context.ApiResources.Add(resource.ToEntity());
                }
                context.SaveChanges();
            }
        }
    }
}
