using System.Collections.Specialized;
using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contexts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Responses.HttpResults;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Responses;

public class AuthorizeResponse : IResponseHandler
{
    public AuthorizeResponse(AuthorizeContext context, string? code, string? sessionState,
        string? identityToken = null, string? token = null)
    {
        Context = context;
        Code = code;
        SessionState = sessionState;
        IdentityToken = identityToken;
        Token = token;
    }

    public AuthorizeContext Context { get; }

    public string? Code { get; }

    public string? SessionState { get; }

    public string? IdentityToken { get; }

    public string? Token { get; }

    public string? TokenType { get; }

    public int? AccessTokenLifetime { get; }

    public string? Scope => Context.RequestedScopes.ToSpaceSeparatedString();

    public string? State => Context.State;

    public ValueTask<IResult> CreateResponseAsync(CancellationToken ct)
    {
        IResult result;
        var redirectUri = Context.RedirectUri!;
        var values = ToNameValueCollection();

        if (Context.ResponseMode == ResponseModes.Query)
        {
            result = new ResponseToQueryResult(redirectUri, values);
        }
        else if (Context.ResponseMode == ResponseModes.Fragment)
        {
            result = new ResponseToFragmentResult(redirectUri, values);
        }
        else if (Context.ResponseMode == ResponseModes.FormPost)
        {
            result = new ResponseToFormPostResult(redirectUri, values);
        }
        else
        {
            throw new InvalidOperationException("Unsupported response mode");
        }

        return ValueTask.FromResult(result);
    }

    private NameValueCollection ToNameValueCollection()
    {
        var collection = new NameValueCollection();

        if (Code.IsPresent())
            collection.Add(AuthorizeResponseFields.Code, Code);

        if (Token.IsPresent())
            collection.Add(AuthorizeResponseFields.AccessToken, Token);

        if (TokenType.IsPresent())
            collection.Add(AuthorizeResponseFields.TokenType, TokenType);

        if (AccessTokenLifetime.HasValue)
            collection.Add(AuthorizeResponseFields.ExpiresIn, AccessTokenLifetime.Value.ToString());

        if (IdentityToken.IsPresent())
            collection.Add(AuthorizeResponseFields.IdentityToken, IdentityToken);

        if (Scope.IsPresent())
            collection.Add(AuthorizeResponseFields.Scope, Scope);

        if (State.IsPresent())
            collection.Add(AuthorizeResponseFields.State, State);

        if (SessionState.IsPresent())
            collection.Add(AuthorizeResponseFields.SessionState, SessionState);

        return collection;
    }
}