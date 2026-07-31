using Microsoft.AspNetCore.Http;
using RoyalIdentity.Pipelines.Abstractions;
using System.Collections.Frozen;
using System.Text.Json;

namespace RoyalIdentity.Pipelines.Defaults;

/// <summary>
/// Writes an error payload as JSON, with an explicit status code and an explicit, immutable set of
/// additional headers.
/// </summary>
/// <remarks>
/// This type is deliberately neutral to the protocol: it never chooses an error code, a status code or a
/// header, it only writes what the caller decided. Selecting OAuth codes and their normative headers is the
/// responsibility of <c>RoyalIdentity</c>.
/// </remarks>
public sealed class ErrorResponseResult : IResult, IStatusCodeHttpResult
{
    /// <summary>
    /// Headers this writer owns. A caller cannot supply them, because doing so would let a response opt out of
    /// the <c>no-store</c> guarantee every protocol error depends on, or disagree with the body actually
    /// written.
    /// </summary>
    private static readonly FrozenSet<string> ReservedHeaders = new[]
    {
        "Cache-Control",
        "Pragma",
        "Content-Type",
        "Content-Length"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static ErrorResponseResult Create(
        string error,
        string? description = null,
        string? uri = null,
        int statusCode = StatusCodes.Status400BadRequest,
        IReadOnlyDictionary<string, string>? headers = null)
        => new(new ErrorResponseParameters
        {
            Error = error,
            ErrorDescription = description,
            ErrorUri = uri
        }, statusCode, headers);

    private readonly ErrorResponseParameters error;

    public ErrorResponseResult(
        ErrorResponseParameters error,
        int statusCode = StatusCodes.Status400BadRequest,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        this.error = error;
        StatusCode = statusCode;
        Headers = Snapshot(headers);
    }

    // Frozen so the response cannot change after it was decided: the caller's dictionary is theirs to keep
    // mutating, a header written on the wire must be the one chosen when the error was classified, and the
    // exposed collection has no mutable implementation to cast back to.
    private static FrozenDictionary<string, string> Snapshot(IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null || headers.Count is 0)
            return FrozenDictionary<string, string>.Empty;

        var copy = new Dictionary<string, string>(headers.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            if (ReservedHeaders.Contains(header.Key))
            {
                throw new ArgumentException(
                    $"'{header.Key}' is written by the error response itself and cannot be supplied by the " +
                    "caller.",
                    nameof(headers));
            }

            copy[header.Key] = header.Value;
        }

        return copy.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public int? StatusCode { get; }

    /// <summary>
    /// The additional headers written with the response, fixed at construction time.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>
    /// The error payload written as the response body.
    /// </summary>
    public ErrorResponseParameters Error => error;

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var response = httpContext.Response;

        // status code
        response.StatusCode = StatusCode ?? StatusCodes.Status400BadRequest;

        // no cache
        response.Headers["Cache-Control"] = "no-store, no-cache, max-age=0";
        response.Headers["Pragma"] = "no-cache";

        // headers required by the semantics the caller selected (for example WWW-Authenticate or Allow)
        foreach (var header in Headers)
            response.Headers[header.Key] = header.Value;

        // write json
        var json = JsonSerializer.Serialize(error, ErrorResponseJsonContenxt.Default.ErrorResponseParameters);
        response.ContentType = "application/json; charset=UTF-8";
        await response.WriteAsync(json);
        await response.Body.FlushAsync();
    }
}
