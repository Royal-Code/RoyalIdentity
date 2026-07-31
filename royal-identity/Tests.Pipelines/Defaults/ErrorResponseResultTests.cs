using Microsoft.AspNetCore.Http;
using RoyalIdentity.Pipelines.Defaults;
using System.Text.Json;

namespace Tests.Pipelines.Defaults;

/// <summary>
/// Contract of the generic error writer: what it puts on the wire is exactly what the caller decided.
/// </summary>
/// <remarks>
/// These tests deserialize the body instead of searching it for text, because the whole point of the plan is
/// that <c>error</c> is a field and not a substring of <c>error_description</c> (DF2).
/// </remarks>
public class ErrorResponseResultTests
{
    private static (DefaultHttpContext HttpContext, MemoryStream Body) CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        var body = new MemoryStream();
        httpContext.Response.Body = body;

        return (httpContext, body);
    }

    private static JsonElement ReadJson(MemoryStream body)
    {
        body.Position = 0;
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task Execute_Must_WriteErrorFieldExactly()
    {
        // arrange
        var (httpContext, body) = CreateHttpContext();
        var result = ErrorResponseResult.Create("invalid_grant", "Authorization code is invalid");

        // act
        await result.ExecuteAsync(httpContext);

        // assert
        var json = ReadJson(body);
        Assert.Equal("invalid_grant", json.GetProperty("error").GetString());
        Assert.Equal("Authorization code is invalid", json.GetProperty("error_description").GetString());
    }

    [Fact]
    public async Task Execute_Must_PreserveErrorUri()
    {
        // arrange
        var (httpContext, body) = CreateHttpContext();
        var result = ErrorResponseResult.Create("invalid_scope", "scopes requested are invalid", "https://example.org/e");

        // act
        await result.ExecuteAsync(httpContext);

        // assert
        var json = ReadJson(body);
        Assert.Equal("https://example.org/e", json.GetProperty("error_uri").GetString());
    }

    [Fact]
    public async Task Execute_Must_DefaultTo400()
    {
        // arrange
        var (httpContext, _) = CreateHttpContext();
        var result = ErrorResponseResult.Create("invalid_request");

        // act
        await result.ExecuteAsync(httpContext);

        // assert
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Theory]
    [InlineData(StatusCodes.Status401Unauthorized)]
    [InlineData(StatusCodes.Status404NotFound)]
    [InlineData(StatusCodes.Status405MethodNotAllowed)]
    [InlineData(StatusCodes.Status415UnsupportedMediaType)]
    public async Task Execute_Must_UseTheGivenStatusCode(int statusCode)
    {
        // arrange
        var (httpContext, _) = CreateHttpContext();
        var result = ErrorResponseResult.Create("some_error", statusCode: statusCode);

        // act
        await result.ExecuteAsync(httpContext);

        // assert
        Assert.Equal(statusCode, httpContext.Response.StatusCode);
        Assert.Equal(statusCode, result.StatusCode);
    }

    [Fact]
    public async Task Execute_Must_WriteJsonContentType()
    {
        // arrange
        var (httpContext, _) = CreateHttpContext();
        var result = ErrorResponseResult.Create("invalid_request");

        // act
        await result.ExecuteAsync(httpContext);

        // assert
        Assert.Equal("application/json; charset=UTF-8", httpContext.Response.ContentType);
    }

    [Fact]
    public async Task Execute_Must_WriteNoStoreCacheHeaders()
    {
        // arrange
        var (httpContext, _) = CreateHttpContext();
        var result = ErrorResponseResult.Create("invalid_request");

        // act
        await result.ExecuteAsync(httpContext);

        // assert
        Assert.Equal("no-store, no-cache, max-age=0", httpContext.Response.Headers["Cache-Control"]);
        Assert.Equal("no-cache", httpContext.Response.Headers["Pragma"]);
    }

    [Fact]
    public async Task Execute_Must_WriteTheExplicitHeaders()
    {
        // arrange
        var (httpContext, _) = CreateHttpContext();
        var headers = new Dictionary<string, string>
        {
            ["WWW-Authenticate"] = "Basic realm=\"master\"",
            ["Allow"] = "POST"
        };
        var result = ErrorResponseResult.Create(
            "invalid_client",
            "Client secret validation failed",
            statusCode: StatusCodes.Status401Unauthorized,
            headers: headers);

        // act
        await result.ExecuteAsync(httpContext);

        // assert
        Assert.Equal("Basic realm=\"master\"", httpContext.Response.Headers["WWW-Authenticate"]);
        Assert.Equal("POST", httpContext.Response.Headers["Allow"]);
    }

    [Fact]
    public async Task Execute_Must_NotWriteAnyExtraHeader_WhenNoneWasGiven()
    {
        // arrange
        var (httpContext, _) = CreateHttpContext();
        var result = ErrorResponseResult.Create("invalid_client", statusCode: StatusCodes.Status401Unauthorized);

        // act
        await result.ExecuteAsync(httpContext);

        // assert
        Assert.Empty(result.Headers);
        Assert.False(httpContext.Response.Headers.ContainsKey("WWW-Authenticate"));
    }

    [Fact]
    public async Task Headers_Must_BeFixedAtConstruction()
    {
        // arrange
        var (httpContext, _) = CreateHttpContext();
        var headers = new Dictionary<string, string> { ["Allow"] = "POST" };
        var result = ErrorResponseResult.Create("invalid_request", headers: headers);

        // act: the caller keeps mutating its own dictionary after the error was already classified
        headers["Allow"] = "GET";
        headers["WWW-Authenticate"] = "Basic";
        await result.ExecuteAsync(httpContext);

        // assert
        Assert.Equal("POST", httpContext.Response.Headers["Allow"]);
        Assert.False(httpContext.Response.Headers.ContainsKey("WWW-Authenticate"));
    }

    [Theory]
    [InlineData("Cache-Control")]
    [InlineData("cache-control")]
    [InlineData("Pragma")]
    [InlineData("Content-Type")]
    [InlineData("Content-Length")]
    public void Headers_Must_RejectTheHeadersTheWriterOwns(string reserved)
    {
        // A caller able to send Cache-Control: public would silently opt the response out of no-store, which
        // every protocol error response depends on. The writer owns these four and refuses to be overridden.
        var headers = new Dictionary<string, string> { [reserved] = "public" };

        var exception = Assert.Throws<ArgumentException>(
            () => ErrorResponseResult.Create("invalid_request", headers: headers));

        Assert.Contains(reserved, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_Must_KeepNoStore_EvenNextToAnAllowedHeader()
    {
        // arrange
        var (httpContext, _) = CreateHttpContext();
        var headers = new Dictionary<string, string> { ["Allow"] = "POST" };
        var result = ErrorResponseResult.Create("invalid_request", headers: headers);

        // act
        await result.ExecuteAsync(httpContext);

        // assert
        Assert.Equal("no-store, no-cache, max-age=0", httpContext.Response.Headers["Cache-Control"]);
        Assert.Equal("POST", httpContext.Response.Headers["Allow"]);
    }

    [Fact]
    public void Headers_Must_NotBeMutableThroughACast()
    {
        // The property type is read-only, but a plain Dictionary behind it could be cast back and mutated.
        var result = ErrorResponseResult.Create(
            "invalid_request",
            headers: new Dictionary<string, string> { ["Allow"] = "POST" });

        var asMutable = Assert.IsAssignableFrom<IDictionary<string, string>>(result.Headers);

        Assert.Throws<NotSupportedException>(() => asMutable.Add("WWW-Authenticate", "Basic"));
        Assert.Throws<NotSupportedException>(() => asMutable["Allow"] = "GET");
        Assert.Equal("POST", result.Headers["Allow"]);
    }

    [Fact]
    public void NoHeaders_Must_NotBeMutableThroughACast()
    {
        // The empty case used to be a shared static dictionary: mutating it through a cast would have leaked
        // into every other error response in the process.
        var result = ErrorResponseResult.Create("invalid_request");

        var asMutable = Assert.IsAssignableFrom<IDictionary<string, string>>(result.Headers);

        Assert.Throws<NotSupportedException>(() => asMutable.Add("Allow", "POST"));
        Assert.Empty(ErrorResponseResult.Create("invalid_grant").Headers);
    }

    [Fact]
    public async Task Execute_Must_AcceptAnExtensionErrorCode()
    {
        // arrange: the contract is a string and not a closed enum, so RFC 8707 and extension grants keep working.
        var (httpContext, body) = CreateHttpContext();
        var result = ErrorResponseResult.Create("invalid_target", "resource indicator not allowed for this client");

        // act
        await result.ExecuteAsync(httpContext);

        // assert
        var json = ReadJson(body);
        Assert.Equal("invalid_target", json.GetProperty("error").GetString());
    }
}
