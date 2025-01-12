using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contexts.Parameters;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts;

public class EndSessionContext : EndpointContextBase, IWithClient
{
    public EndSessionContext(
        HttpContext httpContext,
        NameValueCollection raw,
        ClaimsPrincipal principal,
        ContextItems items) : base(httpContext, raw, items)
    {
        Principal = principal;

        raw.TryGet(EndSessionRequest.IdTokenHint, out var idTokenHint);
        IdTokenHint = idTokenHint;

        raw.TryGet(EndSessionRequest.LogoutHint, out var logoutHint);
        LogoutHint = logoutHint;

        raw.TryGet(EndSessionRequest.ClientId, out var clientId);
        ClientId = clientId;

        raw.TryGet(EndSessionRequest.PostLogoutRedirectUri, out var postLogoutRedirectUri);
        PostLogoutRedirectUri = postLogoutRedirectUri;

        raw.TryGet(EndSessionRequest.State, out var state);
        State = state;

        raw.TryGet(EndSessionRequest.UiLocales, out  var uiLocales);
        UiLocales = uiLocales;
    }

    public ClaimsPrincipal Principal { get; set; }

    public bool IsAuthenticated => Principal.IsAuthenticated();

    public string? IdTokenHint { get; }

    public string? LogoutHint { get; }

    

    public string? PostLogoutRedirectUri { get; }

    public string? State { get; }

    public string? UiLocales { get; }

    public EvaluatedToken? IdToken { get; set; }

    public bool IsClientRequired => false;

    public string? ClientId { get; set; }

    /// <summary>
    /// Client parameters.
    /// </summary>
    public ClientParameters ClientParameters { get; } = new();

}
