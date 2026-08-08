// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
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
    internal AuthorizeResponse(AuthorizeContext context, string code, string? sessionState, string issuer)
    {
        Context = context;
        Code = code;
        SessionState = sessionState;
        Issuer = issuer;
    }

    public AuthorizeContext Context { get; }

    public string Code { get; }

    public string? SessionState { get; }

    /// <summary>Gets the issuer identifier included according to RFC 9207.</summary>
    public string Issuer { get; }

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

        collection.Add(Oidc.Authorize.Response.Code, Code);

        if (Scope.IsPresent())
            collection.Add(Oidc.Authorize.Response.Scope, Scope);

        if (State.IsPresent())
            collection.Add(Oidc.Authorize.Response.State, State);

        if (SessionState.IsPresent())
            collection.Add(Oidc.Authorize.Response.SessionState, SessionState);

        collection.Add(Oidc.Authorize.Response.Issuer, Issuer);

        return collection;
    }
}
