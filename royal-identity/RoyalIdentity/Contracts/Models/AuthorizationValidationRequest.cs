using Microsoft.AspNetCore.Http;
using System.Collections.Specialized;

namespace RoyalIdentity.Contracts.Models;

/// <summary>
/// Represents a request to validate an authorization request.
/// </summary>
public class AuthorizationValidationRequest
{
    /// <summary>
    /// The current HttpContext.
    /// </summary>
    public required HttpContext HttpContext { get; init; }

    /// <summary>
    /// The request parameters, usually obtained from the redirect Url.
    /// </summary>
    public required NameValueCollection Parameters { get; init; }
}