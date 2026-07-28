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
/// the factory binds the realm loaded from its own composition only when persisting.
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

    public string? Description { get => client.Description; set => client.Description = value; }

    public bool Enabled { get => client.Enabled; set => client.Enabled = value; }

    public ClientType ClientType { get => client.ClientType; set => client.ClientType = value; }

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

    public bool AllowAllResourceServers
    {
        get => client.AllowAllResourceServers;
        set => client.AllowAllResourceServers = value;
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

    public bool IncludeJwtId { get => client.IncludeJwtId; set => client.IncludeJwtId = value; }

    public bool AlwaysSendClientClaims
    {
        get => client.AlwaysSendClientClaims;
        set => client.AlwaysSendClientClaims = value;
    }

    public string? ClientClaimsPrefix
    {
        get => client.ClientClaimsPrefix;
        set => client.ClientClaimsPrefix = value;
    }

    public bool EnableLocalLogin
    {
        get => client.EnableLocalLogin;
        set => client.EnableLocalLogin = value;
    }

    public int? UserSsoLifetime
    {
        get => client.UserSsoLifetime;
        set => client.UserSsoLifetime = value;
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

    public int AbsoluteRefreshTokenLifetime
    {
        get => client.AbsoluteRefreshTokenLifetime;
        set => client.AbsoluteRefreshTokenLifetime = value;
    }

    public int SlidingRefreshTokenLifetime
    {
        get => client.SlidingRefreshTokenLifetime;
        set => client.SlidingRefreshTokenLifetime = value;
    }

    public int? ConsentLifetime
    {
        get => client.ConsentLifetime;
        set => client.ConsentLifetime = value;
    }

    public TokenExpiration RefreshTokenExpiration
    {
        get => client.RefreshTokenExpiration;
        set => client.RefreshTokenExpiration = value;
    }

    public TimeSpan RefreshTokenPostConsumedTimeTolerance
    {
        get => client.RefreshTokenPostConsumedTimeTolerance;
        set => client.RefreshTokenPostConsumedTimeTolerance = value;
    }

    public ISet<string> AllowedGrantTypes => client.AllowedGrantTypes;

    public ISet<string> AllowedResponseTypes => client.AllowedResponseTypes;

    public ISet<string> AllowedIdentityScopes => client.AllowedIdentityScopes;

    public ISet<string> AllowedIdentityTokenSigningAlgorithms =>
        client.AllowedIdentityTokenSigningAlgorithms;

    public ISet<string> AllowedAccessTokenSigningAlgorithms =>
        client.AllowedAccessTokenSigningAlgorithms;

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
