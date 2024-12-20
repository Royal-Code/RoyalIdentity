using RoyalIdentity.Models;
using System.Collections.Specialized;
using System.Security.Claims;
using RoyalIdentity.Contexts.Withs;

namespace RoyalIdentity.Contracts.Models;

public class AccessTokenRequest
{
    [Obsolete]
    public required IWithClient Context { get; init; }

    [Obsolete]
    public required NameValueCollection Raw { get; init; }

    public required ClaimsPrincipal User { get; init; }

    public required Client Client { get; init; }

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
