using System.Collections.Specialized;
using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contexts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Responses;

public class AuthorizationCodeResponse : IResponseHandler
{
    public AuthorizationCodeResponse(AuthorizeContext context, string code, string sessionState,
        string? identityToken = null)
    {
        Context = context;
        Code = code;
        SessionState = sessionState;
        IdentityToken = identityToken;
    }

    public AuthorizeContext Context { get; }

    public string Code { get; }

    public string SessionState { get; }

    public string? IdentityToken { get; }

    public string? Scope => Context.RequestedScopes.ToSpaceSeparatedString();

    public string? State => Context.State;

    public ValueTask<IResult> CreateResponseAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    private string BuildRedirectUri()
    {
        var uri = Context.RedirectUri!;
        var query = ToNameValueCollection().ToQueryString();

        uri = Context.ResponseMode == OidcConstants.ResponseModes.Query
            ? uri.AddQueryString(query)
            : uri.AddHashFragment(query);

        return uri;
    }

    private NameValueCollection ToNameValueCollection()
    {
        var collection = new NameValueCollection();
        collection.Add("code", Code);


        if (IdentityToken.IsPresent())
        {
            collection.Add("id_token", IdentityToken);
        }

        if (Scope.IsPresent())
        {
            collection.Add("scope", Scope);
        }

        if (State.IsPresent())
        {
            collection.Add("state", State);
        }

        collection.Add("session_state", SessionState);

        return collection;
    }
}