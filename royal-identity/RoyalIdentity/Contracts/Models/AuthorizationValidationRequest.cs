using System.Collections.Specialized;

namespace RoyalIdentity.Contracts.Models;

public class AuthorizationValidationRequest
{
    /// <summary>
    /// The request parameters, usually obtained from the redirect Url.
    /// </summary>
    public required NameValueCollection Parameters { get; init; }
}