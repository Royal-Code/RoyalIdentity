using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Contexts;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Responses.HttpResults;

namespace RoyalIdentity.Responses;

/// <summary>
/// Authorize endpoint error response that redirects the error back to the client's
/// <c>redirect_uri</c> (per OAuth2/OIDC), respecting the requested <c>response_mode</c>.
/// </summary>
/// <remarks>
/// Used when the request is valid enough that the <c>redirect_uri</c> has already been validated
/// (e.g. the resource owner denied consent → <c>access_denied</c>). Mirrors <see cref="AuthorizeResponse"/>,
/// emitting <c>error</c>/<c>error_description</c>/<c>state</c> instead of the success parameters.
/// </remarks>
public class AuthorizeErrorResponse : IResponseHandler
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizeErrorResponse"/> class.
    /// </summary>
    /// <param name="context">The authorize context that owns the validated redirect URI and response mode.</param>
    /// <param name="error">The protocol error code to return to the client.</param>
    /// <param name="errorDescription">An optional human-readable error description.</param>
    public AuthorizeErrorResponse(AuthorizeContext context, string error, string? errorDescription = null)
    {
        Context = context;
        Error = error;
        ErrorDescription = errorDescription;
    }

    /// <summary>
    /// Gets the authorize context used to create the error response.
    /// </summary>
    public AuthorizeContext Context { get; }

    /// <summary>
    /// Gets the protocol error code returned to the client.
    /// </summary>
    public string Error { get; }

    /// <summary>
    /// Gets the optional human-readable error description returned to the client.
    /// </summary>
    public string? ErrorDescription { get; }

    /// <summary>
    /// Gets the state value from the original authorize request.
    /// </summary>
    public string? State => Context.State;

    /// <inheritdoc />
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

    /// <inheritdoc />
    public bool HasProblem([NotNullWhen(true)] out ProblemDetails? problem)
    {
        problem = null;
        return false;
    }

    private NameValueCollection ToNameValueCollection()
    {
        var collection = new NameValueCollection
        {
            { Oidc.Authorize.Response.Error, Error }
        };

        if (ErrorDescription.IsPresent())
            collection.Add(Oidc.Authorize.Response.ErrorDescription, ErrorDescription);

        if (State.IsPresent())
            collection.Add(Oidc.Authorize.Response.State, State);

        return collection;
    }
}
