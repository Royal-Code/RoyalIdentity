using RoyalIdentity.Utils;
using System.Security.Claims;

namespace RoyalIdentity.Models.Tokens;

#pragma warning disable S107 // Methods should not have too many parameters

/// <summary>
/// Models an authorization code.
/// </summary>
public class AuthorizationCode
{
    public AuthorizationCode(string clientId, ClaimsPrincipal subject, string sessionState,
        DateTime creationTime, int lifetime,
        Resources resources, string redirectUri)
    {
        Code = CryptoRandom.CreateUniqueId();
        ClientId = clientId;
        Subject = subject;
        SessionState = sessionState;
        CreationTime = creationTime;
        Lifetime = lifetime;
        Resources = resources;
        RedirectUri = redirectUri;
    }

    /// <summary>
    /// Gets or sets the Code or Id of the code.
    /// </summary>
    /// <value>
    /// The Code.
    /// </value>
    public string Code { get; }

    /// <summary>
    /// Gets or sets the ID of the client.
    /// </summary>
    /// <value>
    /// The ID of the client.
    /// </value>
    public string ClientId { get; }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    /// <value>
    /// The subject.
    /// </value>
    public ClaimsPrincipal Subject { get; }

    /// <summary>
    /// Gets or sets the session state.
    /// </summary>
    public string SessionState { get; }

    /// <summary>
    /// Gets or sets the creation time.
    /// </summary>
    /// <value>
    /// The creation time.
    /// </value>
    public DateTime CreationTime { get; }

    /// <summary>
    /// Gets or sets the lifetime in seconds.
    /// </summary>
    /// <value>
    /// The lifetime.
    /// </value>
    public int Lifetime { get; }

    /// <summary>
    /// Gets or sets the requested scopes.
    /// </summary>
    /// <value>
    /// The requested scopes.
    /// </value>
    public Resources Resources { get; }

    /// <summary>
    /// Gets or sets the redirect URI.
    /// </summary>
    /// <value>
    /// The redirect URI.
    /// </value>
    public string RedirectUri { get; }

    /// <summary>
    /// Gets or sets the nonce.
    /// </summary>
    /// <value>
    /// The nonce.
    /// </value>
    public string? Nonce { get; set; }

    /// <summary>
    /// Gets or sets the hashed state (to output s_hash claim).
    /// </summary>
    /// <value>
    /// The hashed state.
    /// </value>
    public string? StateHash { get; set; }

    /// <summary>
    /// Gets or sets the session identifier.
    /// </summary>
    /// <value>
    /// The session identifier.
    /// </value>
    public string? SessionId { get; set; }

    /// <summary>
    /// Gets or sets the code challenge.
    /// </summary>
    /// <value>
    /// The code challenge.
    /// </value>
    public string? CodeChallenge { get; set; }

    /// <summary>
    /// Gets or sets the code challenge method.
    /// </summary>
    /// <value>
    /// The code challenge method
    /// </value>
    public string? CodeChallengeMethod { get; set; }

    /// <summary>
    /// Gets or sets properties
    /// </summary>
    /// <value>
    /// The properties
    /// </value>
    public IDictionary<string, string>? Properties { get; set; }
}
