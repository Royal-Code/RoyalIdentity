using System.Collections.Concurrent;
using System.Collections.Specialized;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Users.Defaults;

namespace RoyalIdentity.Storage.InMemory;

public class MemoryStorage
{
    public ConcurrentDictionary<string, AccessToken> AccessTokens { get; } = new();

    public ConcurrentDictionary<string, AuthorizationCode> AuthorizationCodes { get; } = new();

    public ConcurrentDictionary<string, NameValueCollection> AuthorizeParameters { get; } = new();

    public ConcurrentDictionary<string, Client> Clients { get; } = new();

    public ConcurrentDictionary<string, IdentityResource> IdentityResources { get; } = new();

    public ConcurrentDictionary<string, ApiScope> ApiScopes { get; } = new();

    public ConcurrentDictionary<string, ApiResource> ApiResources { get; } = new();

    public ConcurrentDictionary<string, Consent> Consents { get; } = new();

    public ConcurrentDictionary<string, UserDetails> UsersDetails { get; } = new();

    public ConcurrentDictionary<string, IdentitySession> UserSessions { get; } = new();
}