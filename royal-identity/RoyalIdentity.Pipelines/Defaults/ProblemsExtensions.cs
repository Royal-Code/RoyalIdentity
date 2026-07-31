using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Pipelines.Defaults;

public static class ProblemsExtensions
{
    /// <summary>
    /// Converts a <see cref="ProblemDetails"/> into an error response.
    /// </summary>
    /// <param name="problemDetails">The problem to convert.</param>
    /// <param name="fallbackError">
    /// The error code used when the problem carries no title. It is a parameter, and not a constant here,
    /// because choosing a protocol error code is the core's decision and this project stays neutral (DF19).
    /// </param>
    public static ErrorResponseResult ToErrorResult(this ProblemDetails problemDetails, string fallbackError)
    {
        return new ErrorResponseResult(
            new ErrorResponseParameters
            {
                Error = problemDetails.Title ?? fallbackError,
                ErrorDescription = problemDetails.Detail,
                ErrorUri = problemDetails.Instance
            },
            problemDetails.Status ?? StatusCodes.Status400BadRequest);
    }
}
