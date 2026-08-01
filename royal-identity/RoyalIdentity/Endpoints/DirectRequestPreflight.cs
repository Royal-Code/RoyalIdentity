using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Endpoints;

/// <summary>
/// Checks the shape of a direct (form POST) request before a typed context exists: how many times each core
/// parameter may appear, and which client authentication mechanism the request is presenting.
/// </summary>
/// <remarks>
/// <para>
/// This runs before any credential is read, any store is consulted and any evaluator produces a side effect —
/// in particular before <c>private_key_jwt</c> burns the assertion's <c>jti</c> in the replay store (DF7). A
/// malformed request must not be able to consume the identifier a later, legitimate request would need.
/// </para>
/// <para>
/// It is also the single place that decides precedence between mechanisms. Each evaluator used to carry its own
/// version of that rule, which is how a malformed <c>Authorization</c> header could quietly fall through to the
/// connection certificate.
/// </para>
/// </remarks>
internal static class DirectRequestPreflight
{
    /// <summary>
    /// Parameters every direct request shares. None of them may be repeated: they are read as scalars, and a
    /// repetition would let the value that authenticates differ from the value that was validated.
    /// </summary>
    private static readonly string[] ClientAuthenticationParameters =
    [
        Oidc.Token.Request.ClientId,
        Oidc.Token.Request.ClientSecret,
        Oidc.Token.Request.ClientAssertion,
        Oidc.Token.Request.ClientAssertionType,
    ];

    /// <summary>
    /// Single-valued parameters of a token request. <c>resource</c> is deliberately absent: RFC 8707 §2.1
    /// declares it repeatable, and every occurrence is preserved in the order received.
    /// </summary>
    public static readonly string[] TokenRequestParameters =
    [
        .. ClientAuthenticationParameters,
        Oidc.Token.Request.GrantType,
        Oidc.Token.Request.Code,
        Oidc.Token.Request.CodeVerifier,
        Oidc.Token.Request.RedirectUri,
        Oidc.Token.Request.RefreshToken,
        Oidc.Token.Request.Scope,
    ];

    /// <summary>Single-valued parameters of a revocation request.</summary>
    public static readonly string[] RevocationRequestParameters =
    [
        .. ClientAuthenticationParameters,
        Oidc.Revocation.Request.Token,
        Oidc.Revocation.Request.TokenTypeHint,
    ];

    /// <summary>
    /// Validates the request shape and decides the client authentication mechanism.
    /// </summary>
    /// <returns>
    /// <c>true</c> when the request is well formed, with <paramref name="attempt"/> describing the mechanism;
    /// <c>false</c> when it is not, with <paramref name="failure"/> carrying the response.
    /// </returns>
    public static bool TryEvaluate(
        HttpContext httpContext,
        NameValueCollection raw,
        IReadOnlyList<string> singleValuedParameters,
        ILogger logger,
        [NotNullWhen(true)] out ClientAuthenticationAttempt? attempt,
        out EndpointCreationResult failure)
    {
        attempt = null;

        foreach (var parameter in singleValuedParameters)
        {
            var values = raw.GetValues(parameter);
            if (values is null || values.Length <= 1)
                continue;

            logger.LogWarning("The parameter {Parameter} was sent more than once", parameter);

            failure = InvalidRequest(httpContext, $"The parameter {parameter} must be sent at most once");
            return false;
        }

        var hasAssertion = IsPresent(raw, Oidc.Token.Request.ClientAssertion);
        var hasAssertionType = IsPresent(raw, Oidc.Token.Request.ClientAssertionType);

        if (hasAssertion != hasAssertionType)
        {
            // Form, not authentication: the pair is incomplete, so no authentication method was selected yet
            // and RFC 7523 §3.2 has nothing to say about it (DF15).
            logger.LogWarning("The client assertion parameter pair is incomplete");

            failure = InvalidRequest(
                httpContext,
                "client_assertion and client_assertion_type must be sent together");
            return false;
        }

        var authorizationHeaders = httpContext.Request.Headers.Authorization;
        if (authorizationHeaders.Count > 1)
        {
            // RFC 9110 §11.6.2 allows a single Authorization header. Picking one of several would let the
            // credential that authenticates differ from the credential that was inspected.
            logger.LogWarning("The request carried more than one Authorization header");

            failure = InvalidRequest(httpContext, "Only one Authorization header may be sent");
            return false;
        }

        // The key existing is the whole decision. Looking at the value — even only to reject empty or
        // whitespace — is how the previous version let unusable headers through: any content-based test has a
        // shape it does not recognise, and the answer for an unrecognised shape must be "refuse", never
        // "assume nothing was presented".
        var hasAuthorizationHeader = httpContext.Request.Headers.ContainsKey(HeaderNames.Authorization);
        var hasPostSecret = IsPresent(raw, Oidc.Token.Request.ClientSecret);

        var mechanisms = (hasAuthorizationHeader ? 1 : 0) + (hasPostSecret ? 1 : 0) + (hasAssertion ? 1 : 0);
        if (mechanisms > 1)
        {
            logger.LogWarning("The request presented more than one client authentication mechanism");

            failure = InvalidRequest(
                httpContext,
                "Only one client authentication mechanism may be used per request");
            return false;
        }

        if (hasAssertion)
        {
            var assertionType = raw.Get(Oidc.Token.Request.ClientAssertionType);
            if (assertionType != Oidc.ClientAssertionTypes.JwtBearer)
            {
                // The pair is complete, so a client authentication method was selected and the failure belongs
                // to authentication. The description stays generic for the same reason every other
                // invalid_client does (DF15).
                logger.LogWarning("Unsupported client assertion type");

                failure = EndpointErrorResults.BadRequest(
                    httpContext,
                    Oidc.Token.Errors.InvalidClient,
                    "Client authentication failed");
                return false;
            }

            attempt = new ClientAuthenticationAttempt(ClientAuthenticationSource.ClientAssertion);
        }
        else if (hasAuthorizationHeader)
        {
            // Any scheme, not only Basic. An Authorization header the endpoint cannot use is still a client
            // trying to authenticate: treating it as "nothing presented" is what let it reach the connection
            // certificate or the no-secret path instead of being refused.
            attempt = new ClientAuthenticationAttempt(ClientAuthenticationSource.AuthorizationHeader);
        }
        else if (hasPostSecret)
        {
            attempt = new ClientAuthenticationAttempt(ClientAuthenticationSource.PostBody);
        }
        else
        {
            attempt = ClientAuthenticationAttempt.NoneAttempt;
        }

        failure = default;
        return true;
    }

    // Presence is the key existing, not the value being meaningful: an empty client_secret is still a client
    // trying to authenticate with a secret, and the evaluators have always read it that way.
    private static bool IsPresent(NameValueCollection raw, string parameter)
        => raw.GetValues(parameter) is not null;

    private static EndpointCreationResult InvalidRequest(HttpContext httpContext, string description)
        => EndpointErrorResults.BadRequest(httpContext, Oidc.Token.Errors.InvalidRequest, description);
}
