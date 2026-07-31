using System.Net;
using System.Text.Json;

namespace Tests.Integration.Prepare;

/// <summary>
/// The protocol error a response actually carried, read from the wire.
/// </summary>
/// <remarks>
/// Conformance is the exact value of the JSON <c>error</c> field, the status and the normative headers (DF2).
/// Finding the expected code somewhere inside <c>error_description</c> does not satisfy the contract, which is
/// why this type parses the payload instead of letting tests search the body for a substring.
/// </remarks>
public sealed class ProtocolError
{
    public required string Error { get; init; }

    public string? Description { get; init; }

    public string? Uri { get; init; }

    public required HttpStatusCode StatusCode { get; init; }

    public string? ContentType { get; init; }

    public string? CacheControl { get; init; }

    /// <summary>Response headers, so a test can assert <c>WWW-Authenticate</c> or <c>Allow</c>.</summary>
    public required IReadOnlyDictionary<string, string> Headers { get; init; }

    /// <summary>
    /// Exactly what the client can observe about the classification. Two refusals that must be
    /// indistinguishable compare equal here and nowhere else, so an anti-oracle assertion never accidentally
    /// depends on a header or a status that happens to match.
    /// </summary>
    public (string Error, string? Description) Answer => (Error, Description);
}

/// <summary>
/// Reads and asserts the protocol error of an HTTP response.
/// </summary>
public static class ProtocolErrorResponse
{
    /// <summary>
    /// Reads the error payload, failing the test when the response is not a well-formed error envelope.
    /// </summary>
    public static async Task<ProtocolError> ReadErrorAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(body);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            Assert.Fail($"Expected a JSON error payload, got ({(int)response.StatusCode}): {body}");
            throw;
        }

        if (!root.TryGetProperty("error", out var error) || error.ValueKind is not JsonValueKind.String)
            Assert.Fail($"The error payload has no string \"error\" field: {body}");

        return new ProtocolError
        {
            Error = error.GetString()!,
            Description = ReadOptional(root, "error_description"),
            Uri = ReadOptional(root, "error_uri"),
            StatusCode = response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.ToString(),
            CacheControl = ReadHeader(response, "Cache-Control"),
            Headers = ReadHeaders(response)
        };
    }

    /// <summary>
    /// Reads the error payload and asserts the code, the status and the response shape every protocol error
    /// shares. Returns it so the caller can go on asserting what is specific to its case.
    /// </summary>
    public static async Task<ProtocolError> AssertErrorAsync(
        this HttpResponseMessage response,
        string expectedError,
        HttpStatusCode expectedStatusCode = HttpStatusCode.BadRequest)
    {
        var error = await response.ReadErrorAsync();

        Assert.Equal(expectedError, error.Error);
        Assert.Equal(expectedStatusCode, error.StatusCode);
        Assert.Equal("application/json; charset=UTF-8", error.ContentType);
        Assert.Equal("no-store, no-cache, max-age=0", error.CacheControl);

        return error;
    }

    private static string? ReadOptional(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? ReadHeader(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var values))
            return string.Join(", ", values);

        return response.Content.Headers.TryGetValues(name, out var contentValues)
            ? string.Join(", ", contentValues)
            : null;
    }

    private static IReadOnlyDictionary<string, string> ReadHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in response.Headers)
            headers[header.Key] = string.Join(", ", header.Value);

        foreach (var header in response.Content.Headers)
            headers[header.Key] = string.Join(", ", header.Value);

        return headers;
    }
}
