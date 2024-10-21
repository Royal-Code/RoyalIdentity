using RoyalIdentity.Models;
using System.Collections.Specialized;
using System.Security.Claims;

namespace RoyalIdentity.Contexts;

public class AuthorizeValidationContext
{
    /// <summary>
    /// The request parameters, usually obtained from the redirect Url.
    /// </summary>
    public required NameValueCollection Parameters { get; init; }

    /// <summary>
    /// The authenticated user, if any.
    /// </summary>
    public ClaimsPrincipal? Subject { get; init; }

    /// <summary>
    /// Handler para geração de respostas de erros.
    /// </summary>
    public ValidationError? Error { get; set; }
}