using Microsoft.AspNetCore.Http;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Pipelines.Defaults;
using RoyalIdentity.Models;

namespace RoyalIdentity.Extensions;

/// <summary>
/// Sets a protocol error response on a context.
/// </summary>
/// <remarks>
/// <para>
/// There is a single way to signal a protocol error, and it always names the error code first: the value of the
/// JSON <c>error</c> field is never inferred from the description (DF2/DF4). Named shortcuts such as
/// <c>InvalidRequest(description)</c> were removed because an <c>Oidc.*.Errors.*</c> constant could silently
/// take the description position and leave the real code buried in <c>error_description</c>.
/// </para>
/// <para>
/// <c>Tests.Architecture/ProtocolErrorBoundaryTests</c> guards both halves of this rule: no error constant may
/// appear in a description argument, and no ambiguous helper may come back.
/// </para>
/// </remarks>
internal static class ResponseHandlerExtensions
{
    /// <summary>
    /// Responds with a protocol error.
    /// </summary>
    /// <param name="context">The context being processed.</param>
    /// <param name="error">
    /// The exact value written to the JSON <c>error</c> field. Always an <c>Oidc.*.Errors.*</c> constant or an
    /// error code defined by a supported extension.
    /// </param>
    /// <param name="errorDescription">
    /// Human readable diagnostic. Never carries credentials, codes, tokens or assertions, and never decides the
    /// classification.
    /// </param>
    /// <param name="statusCode">The HTTP status code of the response.</param>
    /// <param name="headers">Headers the response semantics require, such as <c>WWW-Authenticate</c>.</param>
    public static void Error(
        this IContextBase context,
        string error,
        string? errorDescription = null,
        int statusCode = StatusCodes.Status400BadRequest,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        context.Response = ResponseHandler.Error(
            error,
            errorDescription,
            statusCode: statusCode,
            headers: headers);
    }

    /// <summary>
    /// Responds with a protocol error already classified by a validation result.
    /// </summary>
    public static void Error(this IContextBase context, ErrorDetails errorDetails)
    {
        context.Response = ResponseHandler.Error(
            errorDetails.Error,
            errorDetails.ErrorDescription,
            errorDetails.ErrorUri);
    }
}
