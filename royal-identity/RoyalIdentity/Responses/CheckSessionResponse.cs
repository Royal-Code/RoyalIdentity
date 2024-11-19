using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Responses.HttpResults;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Responses;

public class CheckSessionResponse : IResponseHandler
{
    public static readonly CheckSessionResponse Instance = new();

    private readonly CheckSessionResult result = new();

    public ValueTask<IResult> CreateResponseAsync(CancellationToken ct) => ValueTask.FromResult<IResult>(result);

    public bool HasProblem([NotNullWhen(true)] out ProblemDetails? problem)
    {
        throw new NotImplementedException();
    }
}
