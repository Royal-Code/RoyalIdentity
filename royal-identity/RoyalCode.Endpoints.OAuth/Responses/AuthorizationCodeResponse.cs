using System.Collections.Specialized;
using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contexts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Responses.HttpResults;
using static RoyalIdentity.Options.OidcConstants;

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
        IResult result;
        var redirectUri = Context.RedirectUri!;
        var values = ToNameValueCollection();

        if (Context.ResponseMode == ResponseModes.Query)
        {
            result = new CodeResponseToQueryResult(redirectUri, values);
        }
        else if (Context.ResponseMode == ResponseModes.Fragment)
        {
            result = new CodeResponseToFragmentResult(redirectUri, values);
        }
        else if (Context.ResponseMode == ResponseModes.FormPost)
        {
            result = new CodeResponseToFormPostResult(redirectUri, values);
        }
        else
        {
            throw new InvalidOperationException("Unsupported response mode");
        }

        return ValueTask.FromResult(result);
    }

    private NameValueCollection ToNameValueCollection()
    {
        var collection = new NameValueCollection
        {
            { "code", Code }
        };

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