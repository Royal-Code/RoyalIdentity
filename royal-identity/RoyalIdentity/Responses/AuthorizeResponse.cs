using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Contexts;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Responses.HttpResults;

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

    public string? Scope => Context.Scope;

    public string? State => Context.State;

    public ValueTask<IResult> CreateResponseAsync(CancellationToken ct)
    {
        IResult result;
        var redirectUri = Context.RedirectUri!;
        var values = ToNameValueCollection();

        if (Context.ResponseMode == Oidc.ResponseModes.Query)
        {
            result = new ResponseToQueryResult(redirectUri, values);
        }
        else if (Context.ResponseMode == Oidc.ResponseModes.Fragment)
        {
            result = new ResponseToFragmentResult(redirectUri, values);
        }
        else if (Context.ResponseMode == Oidc.ResponseModes.FormPost)
        {
            result = new ResponseToFormPostResult(redirectUri, values);
        }
        else
        {
            throw new InvalidOperationException("Unsupported response mode");
        }

        return ValueTask.FromResult(result);
    }

    public bool HasProblem([NotNullWhen(true)] out ProblemDetails? problem)
    {
        problem = null;
        return false;
    }

    private NameValueCollection ToNameValueCollection()
    {
        var collection = new NameValueCollection();

        if (Code.IsPresent())
            collection.Add(Oidc.Authorize.Response.Code, Code);

        if (Token.IsPresent())
            collection.Add(Oidc.Authorize.Response.AccessToken, Token);

        if (TokenType.IsPresent())
            collection.Add(Oidc.Authorize.Response.TokenType, TokenType);

        if (AccessTokenLifetime.HasValue)
            collection.Add(Oidc.Authorize.Response.ExpiresIn, AccessTokenLifetime.Value.ToString());

        if (IdentityToken.IsPresent())
            collection.Add(Oidc.Authorize.Response.IdentityToken, IdentityToken);

        if (Scope.IsPresent())
            collection.Add(Oidc.Authorize.Response.Scope, Scope);

        if (State.IsPresent())
            collection.Add(Oidc.Authorize.Response.State, State);

        if (SessionState.IsPresent())
            collection.Add(Oidc.Authorize.Response.SessionState, SessionState);

        return collection;
    }
}