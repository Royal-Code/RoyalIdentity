using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Endpoints.Abstractions;

namespace RoyalIdentity.Endpoints.Defaults;

public static class ProblemsExtensions
{
    public static ErrorResponseResult ToErrorResult(this ProblemDetails problemDetails)
    {
        return new ErrorResponseResult(
            new ErrorResponseParameters
            {
                Error = problemDetails.Title ?? "invalid_request",
                ErrorDescription = problemDetails.Detail,
                ErrorUri = problemDetails.Instance
            },
            problemDetails.Status ?? 400);
    }
}
