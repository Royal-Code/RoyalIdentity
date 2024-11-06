using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Endpoints.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Responses;

public class DiscoveryResponse : IResponseHandler
{
    private readonly Dictionary<string, object> entries;

    public DiscoveryResponse(Dictionary<string, object> entries)
    {
        this.entries = entries;
    }

    public ValueTask<IResult> CreateResponseAsync(CancellationToken ct)
    {
        return ValueTask.FromResult(Results.Json(entries));
    }

    public bool HasProblem([NotNullWhen(true)] out ProblemDetails? problem)
    {
        problem = null;
        return false;
    }
}
