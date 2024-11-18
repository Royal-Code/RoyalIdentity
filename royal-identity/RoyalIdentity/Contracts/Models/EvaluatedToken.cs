using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Models;
using System.Security.Claims;

namespace RoyalIdentity.Contracts.Models;

public class EvaluatedToken
{
    /// <summary>
    /// Gets or sets the claims of the token.
    /// </summary>
    /// <value>
    /// The claims.
    /// </value>
    public required ClaimsPrincipal Principal { get; set; }

    /// <summary>
    /// Gets or sets the client.
    /// </summary>
    /// <value>
    /// The client.
    /// </value>
    public required Client Client { get; set; }

    /// <summary>
    /// Gets or sets the JWT.
    /// </summary>
    /// <value>
    /// The JWT.
    /// </value>
    public string? Jwt { get; set; }

    /// <summary>
    /// Gets or sets the reference token identifier (in case of access token validation).
    /// </summary>
    /// <value>
    /// The reference token identifier.
    /// </value>
    public string? ReferenceTokenId { get; set; }

    /// <summary>
    /// Gets or sets the refresh token (in case of refresh token validation).
    /// </summary>
    /// <value>
    /// The reference token identifier.
    /// </value>
    public RefreshToken? RefreshToken { get; set; }
}
