using Microsoft.AspNetCore.Http;
using RoyalIdentity.Models;
using System.Collections.Specialized;
using System.Security.Claims;

namespace RoyalIdentity.Contracts;

public class AccessTokenRequest
{
    public required HttpContext HttpContext { get; set; }

    public required NameValueCollection Raw { get; set; }

    public required ClaimsPrincipal Subject { get; set; }

    public required Resources Resources { get; set; }

    public required Client Client { get; set; }

    public required string Caller { get; set; }
}
