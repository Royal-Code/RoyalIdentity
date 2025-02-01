using RoyalIdentity.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contracts.Models;

public class AccessTokenRequest
{
    /// <summary>
    /// The HttpContext for the current request.
    /// This is used to get the issuer name for the access token and for mutual TLS authentication.
    /// </summary>
    public required HttpContext HttpContext { get; init; }

    public required ClaimsPrincipal User { get; init; }

    public required Client Client { get; init; }

    public required Resources Resources { get; init; }

    /// <summary>
    /// Gets or sets the type of the identity.
    /// </summary>
    /// <value>
    /// The type of the identity. Default values are defined in <see cref="Constants.IdentityProfileTypes"/>.
    /// </value>
    public required string IdentityType { get; set; }

    /// <summary>
    /// Gets or sets the value of the confirmation method (will become the cnf claim). Must be a JSON object.
    /// </summary>
    /// <value>
    /// The confirmation.
    /// </value>
    public string? Confirmation { get; init; }
}
