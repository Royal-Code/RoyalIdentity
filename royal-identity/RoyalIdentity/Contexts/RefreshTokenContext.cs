using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Models.Tokens;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts;

public class RefreshTokenContext : TokenEndpointContextBase
{
    private ClaimsPrincipal? subject;

    public RefreshTokenContext(
        HttpContext httpContext,
        NameValueCollection raw,
        string grantType,
        ContextItems? items = null) : base(httpContext, raw, grantType, items)
    {

    }

    public string? Token { get; private set; }

    public RefreshToken? RefreshToken { get; set; }

    public DateTime? TokenFirstConsumedAt { get; set; }

    public override ClaimsPrincipal? GetSubject()
    {
        if (subject is null && RefreshToken is not null)
        {
            subject = RefreshToken.CreatePrincipal();
        }

        return subject;
    }

    public override void Load(ILogger logger)
    {
        ClientId = Raw.Get(TokenRequest.ClientId);
        Scope = Raw.Get(TokenRequest.Scope);
        Token = Raw.Get(TokenRequest.RefreshToken);
    }

#pragma warning disable CS8774

    private bool hasRefreshToken;

    [MemberNotNull(nameof(Token), nameof(RefreshToken))]
    public void AssertHasRefreshToken()
    {
        if (hasRefreshToken)
            return;

        hasRefreshToken = Items.Get<Asserts>()?.HasToken ?? false;
        if (!hasRefreshToken)
            throw new InvalidOperationException("Refresh Token was required, but is missing");
    }
}
