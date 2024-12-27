using Microsoft.AspNetCore.Http;
using RoyalIdentity.Endpoints.Abstractions;
using System.Text.Json;

namespace RoyalIdentity.Endpoints.Defaults;

public sealed class ErrorResponseResult : IResult, IStatusCodeHttpResult
{
    public static ErrorResponseResult Create(string error, string? description = null, string? uri = null, int statusCode = 400)
        => new(new ErrorResponseParameters
        {
            Error = error,
            ErrorDescription = description,
            ErrorUri = uri
        }, statusCode);

    private readonly ErrorResponseParameters error;

    public ErrorResponseResult(ErrorResponseParameters error, int statusCode = 400)
    {
        this.error = error;
        StatusCode = statusCode;
    }

    public int? StatusCode { get; }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var response = httpContext.Response;

        // status code
        response.StatusCode = StatusCode ?? StatusCodes.Status400BadRequest;

        // no cache
        response.Headers["Cache-Control"] = "no-store, no-cache, max-age=0";
        response.Headers["Pragma"] = "no-cache";

        // write json
        var json = JsonSerializer.Serialize(error, ErrorResponseJsonContenxt.Default.ErrorResponseParameters);
        response.ContentType = "application/json; charset=UTF-8";
        await response.WriteAsync(json);
        await response.Body.FlushAsync();
    }
}
