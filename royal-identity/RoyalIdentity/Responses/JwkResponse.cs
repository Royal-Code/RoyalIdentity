using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Responses.HttpResults;

namespace RoyalIdentity.Responses;

public class JwkResponse : IResponseHandler
{
    private readonly JwkResult result;

    public JwkResponse(IReadOnlyList<JsonWebKey> jwks, int? maxAge)
    {
        result = new JwkResult(jwks, maxAge);
    }

    public ValueTask<IResult> CreateResponseAsync(CancellationToken ct)
    {
        return new(result);
    }

    public bool HasProblem([NotNullWhen(true)] out ProblemDetails? problem)
    {
        problem = null;
        return false;
    }
}