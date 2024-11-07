using Microsoft.AspNetCore.Mvc;

namespace RoyalIdentity.Endpoints.Defaults;

public static class ProblemsExtensions
{
    public const string Error = "error";
    public const string ErrorDescription = "error_description";

    public static ProblemDetails IncludeErrorsProperties(this ProblemDetails problemDetails)
    {
        if (problemDetails.Extensions.ContainsKey(Error))
            return problemDetails;

        problemDetails.Extensions.Add(Error, problemDetails.Title);
        problemDetails.Extensions.Add(ErrorDescription, problemDetails.Detail);

        return problemDetails;
    }
}
