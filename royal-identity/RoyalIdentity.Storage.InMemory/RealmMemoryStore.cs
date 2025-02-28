using RoyalIdentity.Models;
using RoyalIdentity.Models.Keys;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Utils;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace RoyalIdentity.Storage.InMemory;

public class RealmMemoryStore
{
    private readonly Realm realm;

    public RealmMemoryStore(Realm realm, bool isServer)
    {
        this.realm = realm;
        Clients = isServer ? ServerClients(realm) : DemoClients(realm);
    }

    public ConcurrentDictionary<string, Client> Clients { get; }

    public ConcurrentDictionary<string, KeyParameters> KeyParameters { get; } = new();

    public ConcurrentDictionary<string, IdentitySession> UserSessions { get; } = new();

    public ConcurrentDictionary<string, IdentityResource> IdentityResources { get; } = new()
    {
        ["openid"] = new IdentityResource
        {
            Name = "openid",
            DisplayName = "Your user identifier",
            Description = "Your user identifier",
            Required = true,
            Emphasize = false,
            ShowInDiscoveryDocument = true,
            UserClaims = ["sub"]
        },
        ["profile"] = new IdentityResource
        {
            Name = "profile",
            DisplayName = "Your profile data",
            Description = "Your profile data",
            Required = false,
            Emphasize = false,
            ShowInDiscoveryDocument = true,
            UserClaims =
            [
                "name",
                "family_name", "given_name", "middle_name", "nickname", "preferred_username",
                "profile", "picture", "website"
            ]
        },
        ["email"] = new IdentityResource
        {
            Name = "email",
            DisplayName = "Your email address",
            Description = "Your email address",
            Required = false,
            Emphasize = false,
            ShowInDiscoveryDocument = true,
            UserClaims = ["email", "email_verified"]
        }
    };

    public ConcurrentDictionary<string, ApiScope> ApiScopes { get; } = new()
    {
        ["api"] = new ApiScope
        {
            Name = "api",
            DisplayName = "API",
            Description = "Access to the API",
            Required = true,
            Emphasize = false,
            ShowInDiscoveryDocument = true
        },
        ["api:read"] = new ApiScope
        {
            Name = "api:read",
            DisplayName = "API read",
            Description = "Read values from the API",
            Required = false,
            Emphasize = false,
            ShowInDiscoveryDocument = true
        }
        ,
        ["api:write"] = new ApiScope
        {
            Name = "api:write",
            DisplayName = "API write",
            Description = "Write values from the API",
            Required = false,
            Emphasize = true,
            ShowInDiscoveryDocument = true
        }
    };

    public ConcurrentDictionary<string, ApiResource> ApiResources { get; } = new()
    {
        ["api"] = new ApiResource
        {
            Name = "api",
            DisplayName = "API",
            Scopes = { "api" }
        }
    };

    public ConcurrentDictionary<string, UserDetails> UsersDetails { get; } = new()
    {
        ["alice"] = new UserDetails
        {
            Username = "alice",
            PasswordHash = PasswordHash.Create("alice"),
            DisplayName = "Alice",
            IsActive = true,
            Claims =
            [
                new Claim("email", "Alice@example.com"),
                new Claim("role", "admin")
            ]
        },
        ["bob"] = new UserDetails
        {
            Username = "bob",
            PasswordHash = PasswordHash.Create("bob"),
            DisplayName = "Bob",
            IsActive = true,
            Claims =
            [
                new Claim("email", "bob@example.com"),
                new Claim("role", "admin")
            ]
        }
    };

    #region Factory

    private static ConcurrentDictionary<string, Client> ServerClients(Realm realm)
    {
        ConcurrentDictionary<string, Client> clients = new()
        {
            ["server_admin"] = new Client
            {
                Realm = realm,
                Id = "server_admin",
                Name = "Administrative server portal",
                RequireClientSecret = false,
                AllowOfflineAccess = true,
                AllowedScopes = { "openid", "profile", },
                AllowedResponseTypes = { "code" },
                RedirectUris = { "http://localhost:5200/**", "https://localhost:7200/**" }
            }
        };

        return clients;
    }

    private static ConcurrentDictionary<string, Client> DemoClients(Realm realm)
    {
        ConcurrentDictionary<string, Client> clients = new()
        {
            ["demo_client"] = new Client
            {
                Realm = realm,
                Id = "demo_client",
                Name = "Demo Client",
                RequireClientSecret = false,
                AllowOfflineAccess = true,
                AllowedScopes = { "openid", "profile", "email" },
                AllowedResponseTypes = { "code" },
                RedirectUris = { "http://localhost/callback", "http://localhost:5000/**", "https://localhost:5001/**" }
            },
            ["demo_consent_client"] = new Client
            {
                Realm = realm,
                Id = "demo_consent_client",
                Name = "Demo Consent Client",
                RequireClientSecret = false,
                AllowOfflineAccess = true,
                AllowedScopes = { "openid", "profile", "email", "api", "api:read", "api:write" },
                AllowedResponseTypes = { "code" },
                RequireConsent = true,
                RedirectUris = { "http://localhost/callback", "http://localhost:5000/**", "https://localhost:5001/**" }
            }
        };
        return clients;
    }

    #endregion
}
