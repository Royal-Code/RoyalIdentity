using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Responses.HttpResults;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Responses;

public class UserInfoResponse : IResponseHandler
{
    private readonly IDictionary<string, object> userData;

    public UserInfoResponse(IDictionary<string, object> userData)
    {
        this.userData = userData;
    }

    public ValueTask<IResult> CreateResponseAsync(CancellationToken ct)
    {
        return ValueTask.FromResult<IResult>(new UserInfoResult(userData));
    }

    public bool HasProblem([NotNullWhen(true)] out ProblemDetails? problem)
    {
        problem = null;
        return false;
    }
}
