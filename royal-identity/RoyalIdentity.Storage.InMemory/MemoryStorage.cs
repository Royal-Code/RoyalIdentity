using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Security.Claims;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Keys;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Users.Defaults;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Storage.InMemory;

public class MemoryStorage
{
    public ConcurrentDictionary<string, Client> Clients { get; } = new()
    {
        ["client"] = new Client
        {
            Id = "client",
            Name = "Client",
            AllowedScopes = { "openid", "profile", "email" },
            AllowedResponseTypes = { "code" },
            RedirectUris = { "http://localhost:5000/**", "https://localhost:5001/**" }
        }
    };

    public ConcurrentDictionary<string, IdentityResource> IdentityResources { get; } = new()
    {
        ["openid"] = new IdentityResource
        {
            Name = "openid",
            DisplayName = "Your user identifier",
            Description = "Your user identifier",
            Required = true,
            Emphasize = false,
            ShowInDiscoveryDocument = true
        },
        ["profile"] = new IdentityResource
        {
            Name = "profile",
            DisplayName = "Your profile data",
            Description = "Your profile data",
            Required = false,
            Emphasize = false,
            ShowInDiscoveryDocument = true
        },
        ["email"] = new IdentityResource
        {
            Name = "email",
            DisplayName = "Your email address",
            Description = "Your email address",
            Required = false,
            Emphasize = false,
            ShowInDiscoveryDocument = true
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

    public ConcurrentDictionary<string, KeyParameters> KeyParameters { get; } = new();

    public ConcurrentDictionary<string, AccessToken> AccessTokens { get; } = new();

    public ConcurrentDictionary<string, AuthorizationCode> AuthorizationCodes { get; } = new();

    public ConcurrentDictionary<string, NameValueCollection> AuthorizeParameters { get; } = new();

    public ConcurrentDictionary<string, Consent> Consents { get; } = new();

    public ConcurrentDictionary<string, IdentitySession> UserSessions { get; } = new();
}