using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Endpoints.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Contexts;

public class UserInfoContext : EndpointContextBase, IWithBearerToken
{
    public UserInfoContext(HttpContext httpContext, ContextItems items, string token)
        : base(httpContext, new(), items)
    {
        Token = token;
    }

    public string Token { get; }

    public EvaluatedToken? EvaluatedToken { get; set; }

#pragma warning disable CS8774

    private bool hasToken;

    [MemberNotNull(nameof(EvaluatedToken))]
    public void AssertHasToken()
    {
        if (hasToken)
            return;

        hasToken = Items.Get<Asserts>()?.HasToken ?? false;
        if (!hasToken)
            throw new InvalidOperationException("Bearer Token was required, but is missing");
    }
}
