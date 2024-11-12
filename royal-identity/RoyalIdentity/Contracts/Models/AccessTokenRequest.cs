using RoyalIdentity.Models;
using System.Collections.Specialized;
using System.Security.Claims;
using RoyalIdentity.Contexts.Withs;

namespace RoyalIdentity.Contracts.Models;

public class AccessTokenRequest
{
    public required IWithClient Context { get; init; }

    public required NameValueCollection Raw { get; init; }

    public required ClaimsPrincipal Subject { get; init; }

    public required Resources Resources { get; init; }

    public required string Caller { get; init; }

    /// <summary>
    /// Gets or sets the value of the confirmation method (will become the cnf claim). Must be a JSON object.
    /// </summary>
    /// <value>
    /// The confirmation.
    /// </value>
    public string? Confirmation { get; init; }
}
