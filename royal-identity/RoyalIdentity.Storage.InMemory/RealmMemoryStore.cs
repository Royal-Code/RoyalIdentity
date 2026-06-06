using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Keys;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Utils;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using static RoyalIdentity.Options.Constants;

namespace RoyalIdentity.Storage.InMemory;

public class RealmMemoryStore
{
    private readonly Realm realm;

    public RealmMemoryStore(Realm realm, bool isServer)
    {
        this.realm = realm;
        Clients = isServer ? ServerClients(realm) : DemoClients(realm);
        InitializeResources();
    }

    /// <summary>Constructor for programmatically created realms — starts with no clients.</summary>
    internal RealmMemoryStore(Realm realm)
    {
        this.realm = realm;
        Clients = new ConcurrentDictionary<string, Client>();
        InitializeResources();
    }

    private void InitializeResources()
    {
        var apiResource = ResourceServers.First().Value.Resources.First();
        ApiResources[apiResource.Name] = apiResource;
        var apiscopes = apiResource.Scopes;
        foreach (var item in apiscopes)
        {
            ApiScopes[item.Name] = item;
        }
    }

    public ConcurrentDictionary<string, Client> Clients { get; }

    public ConcurrentDictionary<string, KeyParameters> KeyParameters { get; } = new();

    public ConcurrentDictionary<string, AccessToken> AccessTokens { get; } = new();

    public ConcurrentDictionary<string, RefreshToken> RefreshTokens { get; } = new();

    public ConcurrentDictionary<string, AuthorizationCode> AuthorizationCodes { get; } = new();

    public ConcurrentDictionary<string, Consent> UserConsents { get; } = new();

    public ConcurrentDictionary<string, IdentitySession> UserSessions { get; } = new();

    public ConcurrentDictionary<string, IdentityScope> IdentityResources { get; } = new()
    {
        [Server.StandardScopes.OpenId] = new IdentityScope(
            ScopeVisibility.Public,
            Server.StandardScopes.OpenId,
            "Your user identifier",
            "Your user identifier",
            [JwtRegisteredClaimNames.Sub])
        {
            Required = true,
            Emphasize = false,
            ShowInDiscoveryDocument = true,
        },
        [Server.StandardScopes.Profile] = new IdentityScope(
            ScopeVisibility.Public,
            Server.StandardScopes.Profile,
            "Your profile data",
            "Your profile data, which are: name, family_name, given_name, middle_name, nickname, preferred_username, profile, picture, website, gender, birthdate, zoneinfo, locale, and updated_at.0",
            [
                JwtRegisteredClaimNames.Name,
                JwtRegisteredClaimNames.FamilyName,
                JwtRegisteredClaimNames.GivenName,
                Jwt.ClaimTypes.MiddleName,
                Jwt.ClaimTypes.NickName,
                Jwt.ClaimTypes.PreferredUserName,
                Jwt.ClaimTypes.Profile,
                Jwt.ClaimTypes.Picture,
                JwtRegisteredClaimNames.Website,
                Jwt.ClaimTypes.Gender,
                JwtRegisteredClaimNames.Birthdate,
                Jwt.ClaimTypes.ZoneInfo,
                Jwt.ClaimTypes.Locale,
                Jwt.ClaimTypes.UpdatedAt
            ])
        {
            Required = false,
            Emphasize = false,
            ShowInDiscoveryDocument = true,
        },
        [Server.StandardScopes.Email] = new IdentityScope(
            ScopeVisibility.Public,
            Server.StandardScopes.Email,
            "Your email address",
            "Your email address",
            [
                JwtRegisteredClaimNames.Email,
                Jwt.ClaimTypes.EmailVerified
            ])
        {
            Required = false,
            Emphasize = false,
            ShowInDiscoveryDocument = true,
        },
        [Server.StandardScopes.Address] = new IdentityScope(
            ScopeVisibility.Public,
            Server.StandardScopes.Address, 
            "Your address",
            "Your address",
            [Jwt.ClaimTypes.Address])
        {
            Required = false,
            Emphasize = false,
            ShowInDiscoveryDocument = true
        },
        [Server.StandardScopes.Phone] = new IdentityScope(
            ScopeVisibility.Public,
            Server.StandardScopes.Phone,
            "Your phone number",
            "Your phone number",
            [
                Jwt.ClaimTypes.PhoneNumber,
                Jwt.ClaimTypes.PhoneNumberVerified
            ])
        {
            Required = false,
            Emphasize = false,
            ShowInDiscoveryDocument = true,
        }
    };

    public ConcurrentDictionary<string, ResourceServer> ResourceServers { get; } = new()
    {
        ["apiserver"] = new ResourceServer(ScopeVisibility.Public, "apiserver", "API Server", "Access to the API Server")
        {
            Resources =
            [
                new ApiResource(ScopeVisibility.Public, "api1", "API 1", "Access to the API 1")
                {
                    Name = "api",
                    DisplayName = "API",
                    Scopes = 
                    [
                        new ApiScope(ScopeVisibility.Public, "api", "API", "Access to the API")
                        {
                            Required = true,
                            Emphasize = false,
                        }
                    ]
                }
            ],
        }
    };

    public ConcurrentDictionary<string, ApiResource> ApiResources { get; } = [];

    public ConcurrentDictionary<string, ApiScope> ApiScopes { get; } = new()
    {
        ["api:read"] = new ApiScope(ScopeVisibility.Public, "api:read", "API read", "Read values from the API")
        {
            Required = false,
            Emphasize = false,
            ShowInDiscoveryDocument = true
        }
        ,
        ["api:write"] = new ApiScope(ScopeVisibility.Public, "api:write", "API write", "Write values from the API")
        {
            Required = false,
            Emphasize = true,
            ShowInDiscoveryDocument = true
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
                AllowedGrantTypes = ["authorization_code", "refresh_token"],
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
                AllowedGrantTypes = ["authorization_code", "refresh_token"],
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
