using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Configuration;
using RoyalIdentity.Models;
using RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;
using RoyalIdentity.Storage.EntityFramework.Sqlite;

namespace Tests.Integration.Prepare;

/// <summary>
/// Test-only client write seam. Relational details stay here; scenarios provide primitives through
/// <see cref="TestClientBuilder"/> and observe the refreshed runtime configuration.
/// </summary>
internal sealed class PersistentClientSetup(
    ConfigurationSqliteDbContext db,
    ClientMaterializer materializer,
    IConfigurationSnapshotRefresher refresher)
{
    public async Task SaveAsync(
        RoyalIdentity.Models.Realm realm,
        string clientId,
        Action<TestClientBuilder> configure,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(realm);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new TestClientBuilder(clientId);
        configure(builder);
        var client = builder.Build(realm);
        var rows = materializer.ToEntitySet(client);

        await db.ClientStringValues
            .Where(row => row.RealmId == realm.Id && row.ClientId == clientId)
            .ExecuteDeleteAsync(ct);
        await db.ClientClaims
            .Where(row => row.RealmId == realm.Id && row.ClientId == clientId)
            .ExecuteDeleteAsync(ct);
        await db.ClientSecrets
            .Where(row => row.RealmId == realm.Id && row.ClientId == clientId)
            .ExecuteDeleteAsync(ct);
        await db.Clients
            .Where(row => row.RealmId == realm.Id && row.ClientId == clientId)
            .ExecuteDeleteAsync(ct);

        db.Clients.Add(rows.Root);
        db.ClientStringValues.AddRange(rows.StringValues);
        db.ClientClaims.AddRange(rows.Claims);
        db.ClientSecrets.AddRange(rows.Secrets);
        await db.SaveChangesAsync(ct);
        await refresher.RefreshAsync(ct);
    }
}

/// <summary>
/// Provider-neutral client setup model. It deliberately has no <see cref="RoyalIdentity.Models.Realm"/> property;
/// the factory binds
/// the realm loaded from its own composition only when persisting.
/// </summary>
public sealed class TestClientBuilder
{
    private readonly Client client;

    internal TestClientBuilder(string clientId)
    {
        client = new Client
        {
            Id = clientId,
            Name = clientId,
        };
    }

    public string Name { get => client.Name; set => client.Name = value; }

    public bool Enabled { get => client.Enabled; set => client.Enabled = value; }

    public bool RequirePkce { get => client.RequirePkce; set => client.RequirePkce = value; }

    public bool AllowPlainTextPkce
    {
        get => client.AllowPlainTextPkce;
        set => client.AllowPlainTextPkce = value;
    }

    public bool RequireClientSecret
    {
        get => client.RequireClientSecret;
        set => client.RequireClientSecret = value;
    }

    public bool AllowOfflineAccess
    {
        get => client.AllowOfflineAccess;
        set => client.AllowOfflineAccess = value;
    }

    public bool RequireConsent { get => client.RequireConsent; set => client.RequireConsent = value; }

    public bool AllowRememberConsent
    {
        get => client.AllowRememberConsent;
        set => client.AllowRememberConsent = value;
    }

    public bool AlwaysIncludeUserClaimsInIdToken
    {
        get => client.AlwaysIncludeUserClaimsInIdToken;
        set => client.AlwaysIncludeUserClaimsInIdToken = value;
    }

    public int AccessTokenLifetime
    {
        get => client.AccessTokenLifetime;
        set => client.AccessTokenLifetime = value;
    }

    public int IdentityTokenLifetime
    {
        get => client.IdentityTokenLifetime;
        set => client.IdentityTokenLifetime = value;
    }

    public int AuthorizationCodeLifetime
    {
        get => client.AuthorizationCodeLifetime;
        set => client.AuthorizationCodeLifetime = value;
    }

    public ISet<string> AllowedGrantTypes => client.AllowedGrantTypes;

    public ISet<string> AllowedResponseTypes => client.AllowedResponseTypes;

    public ISet<string> AllowedIdentityScopes => client.AllowedIdentityScopes;

    public ISet<string> AllowedResourceServers => client.AllowedResourceServers;

    public ISet<string> AllowedScopes => client.AllowedScopes;

    public ISet<string> RedirectUris => client.RedirectUris;

    public ISet<string> PostLogoutRedirectUris => client.PostLogoutRedirectUris;

    public ISet<string> AllowedCorsOrigins => client.AllowedCorsOrigins;

    public ICollection<ClientSecret> Secrets => client.ClientSecrets;

    public ICollection<Claim> Claims => client.Claims;

    internal Client Build(RoyalIdentity.Models.Realm realm)
    {
        client.Realm = realm;
        return client;
    }
}
