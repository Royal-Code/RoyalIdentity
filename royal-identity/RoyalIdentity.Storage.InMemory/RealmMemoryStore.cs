using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Keys;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Users;
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
        UserAccounts = realm.Id == MemoryStorage.DemoRealm.Id
            ? DemoUsers()
            : new ConcurrentDictionary<string, MemoryUserAccount>();
    }

    /// <summary>Constructor for programmatically created realms — starts with no clients.</summary>
    internal RealmMemoryStore(Realm realm)
    {
        this.realm = realm;
        Clients = new ConcurrentDictionary<string, Client>();
        UserAccounts = new ConcurrentDictionary<string, MemoryUserAccount>();
    }

    public ConcurrentDictionary<string, Client> Clients { get; }

    public ConcurrentDictionary<string, KeyParameters> KeyParameters { get; } = new();

    public ConcurrentDictionary<string, AccessToken> AccessTokens { get; } = new();

    public ConcurrentDictionary<string, RefreshToken> RefreshTokens { get; } = new();

    public ConcurrentDictionary<string, AuthorizationCode> AuthorizationCodes { get; } = new();

    public ConcurrentDictionary<string, Consent> UserConsents { get; } = new();

    public ConcurrentDictionary<string, UserSession> UserSessions { get; } = new();

    public ConcurrentDictionary<string, IdentityScope> IdentityScopes { get; } = new()
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
            Scopes =
            [
                new Scope(ScopeVisibility.Public, "api", "API", "Access to the API")
                {
                    Required = true,
                    Emphasize = false,
                },
                new Scope(ScopeVisibility.Public, "api:read", "API read", "Read values from the API")
                {
                    Required = false,
                    Emphasize = false,
                    ShowInDiscoveryDocument = true,
                },
                new Scope(ScopeVisibility.Public, "api:write", "API write", "Write values from the API")
                {
                    Required = false,
                    Emphasize = true,
                    ShowInDiscoveryDocument = true,
                }
            ],
            ProtectedResources =
            [
                new ProtectedResource("https://api.demo.local/apiserver")
                {
                    DisplayName = "API Server",
                }
            ],
        }
    };

    public ConcurrentDictionary<string, MemoryUserAccount> UserAccounts { get; }

    #region Factory

    private static ConcurrentDictionary<string, MemoryUserAccount> DemoUsers()
    {
        return new ConcurrentDictionary<string, MemoryUserAccount>
        {
            ["alice"] = new MemoryUserAccount
            {
                SubjectId = MemoryStorage.AliceSubjectId,
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
            ["bob"] = new MemoryUserAccount
            {
                SubjectId = MemoryStorage.BobSubjectId,
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
    }

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
                AllowedIdentityScopes = { "openid", "profile" },
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
                AllowedIdentityScopes = { "openid", "profile", "email" },
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
                AllowedIdentityScopes = { "openid", "profile", "email" },
                AllowedResourceServers = { "apiserver" },
                AllowedResponseTypes = { "code" },
                RequireConsent = true,
                RedirectUris = { "http://localhost/callback", "http://localhost:5000/**", "https://localhost:5001/**" }
            }
        };
        return clients;
    }

    #endregion
}
