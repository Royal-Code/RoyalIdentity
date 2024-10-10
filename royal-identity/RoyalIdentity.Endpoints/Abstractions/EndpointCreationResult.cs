
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Endpoints.Defaults;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Endpoints.Abstractions;

/// <summary>
/// A struct containing the result of creating a context object for an endpoint.
/// </summary>
public readonly struct EndpointCreationResult
{
    public readonly IContextBase? context;

    /// <summary>
    /// Create a new <see cref="EndpointCreationResult"/>.
    /// </summary>
    /// <param name="context">The context.</param>
    public EndpointCreationResult(IContextBase context)
    {
        this.context = context;
    }

    /// <summary>
    /// Create a new <see cref="EndpointCreationResult"/> for failure with the response.
    /// </summary>
    /// <param name="httpContext">The HttpContext.</param>
    /// <param name="responseHandler">The <see cref="IResponseHandler"/> for the request.</param>
    public EndpointCreationResult(HttpContext httpContext, IResponseHandler responseHandler)
    {
        context = new ResponseContext(httpContext, responseHandler);
    }

    /// <summary>
    /// Create a new <see cref="EndpointCreationResult"/> for failure with the result.
    /// </summary>
    /// <param name="httpContext">The HttpContext.</param>
    /// <param name="result">The <see cref="IResult"/> for the request.</param>
    public EndpointCreationResult(HttpContext httpContext, IResult result)
    {
        context = new ResponseContext(httpContext, result);
    }

    /// <summary>
    /// Checks whether the context was created correctly or not.
    /// </summary>
    /// <param name="context">If created correctly, the context object.</param>
    /// <param name="responseHandler">If the entries are invalid, a handler to create the response with the problem occurred.</param>
    /// <returns>
    ///     True if the context object was created correctly for the endpoint, false if there is a problem.
    /// </returns>
    public bool IsValid(
        [NotNullWhen(true)] out IContextBase? context,
        [NotNullWhen(false)] out IResponseHandler? responseHandler)
    {
        if (this.context is null)
        {
            // return a problem details of a bad request infoming the input is invalid
            var problemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid input",
                Detail = "The input provided is invalid"
            };

            responseHandler = new ResponseHandler(Results.BadRequest(problemDetails));
            context = null;
            return false;
        }

        if (this.context.Response is not null)
        {
            context = null;
            responseHandler = this.context.Response;
            return false;
        }

        context = this.context;
        responseHandler = null;
        return true;
    }
}