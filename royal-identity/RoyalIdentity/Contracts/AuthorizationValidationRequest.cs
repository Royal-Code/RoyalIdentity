using System.Collections.Specialized;
using System.Security.Claims;

namespace RoyalIdentity.Contracts;

public class AuthorizationValidationRequest
{
    /// <summary>
    /// The request parameters, usually obtained from the redirect Url.
    /// </summary>
    public required NameValueCollection Parameters { get; init; }

    /// <summary>
    /// The authenticated user, if any.
    /// </summary>
    public ClaimsPrincipal? Subject { get; init; }
}