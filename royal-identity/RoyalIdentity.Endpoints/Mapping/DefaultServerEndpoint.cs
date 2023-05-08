
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Endpoints.Abstractions;

namespace RoyalIdentity.Endpoints.Mapping;

public static class ServerEndpoint<TEndpoint, TContext>
    where TEndpoint : IEndpointHandler<TContext>
    where TContext : class, IContextBase
{
    public static async Task<IResult> EndpointHandler(
        HttpContext httpContext,
        TEndpoint endpointHandler,
        IContextPipeline<TContext> contextPipeline)
    {
        // try to create a context from the http context for the endpoint
        var valid = await endpointHandler.TryCreateContextAsync(httpContext, out var context);

        if (!valid)
        {
            // return a problem details of a bad request infoming the input is invalid
            var problemDetails = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid input",
                Detail = "The input provided is invalid"
            };

            return Results.BadRequest(problemDetails);
        }

        // send the context through the pipeline to be processed
        await contextPipeline.SendAsync(context);

        // get the response handler from the context
        var responseHandler = context.Response;

        if (responseHandler is null) 
        { 
            // generate a internal server error
            var problemDetails = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An internal server error has occurred"
            };

            return Results.Json(problemDetails, statusCode: StatusCodes.Status500InternalServerError);
        }

        // return the response from the response handler
        return await responseHandler.CreateResponseAsync();
    }
}

public static class DefaultServerEndpoints
{
    public static RouteHandlerBuilder MapServerEndpoint<TEndpoint, TContext>(this WebApplication app, string pattern)
    where TEndpoint : IEndpointHandler<TContext>
    where TContext : class, IContextBase
    {
        return app.Map(pattern, ServerEndpoint<TEndpoint, TContext>.EndpointHandler);
    }
}