using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Endpoints.Abstractions;

namespace RoyalIdentity.Endpoints.Mapping;

/// <summary>
/// <para>
///     Default implementation for handling a server endpoint.
/// </para>
/// </summary>
/// <typeparam name="TEndpoint">The type of handler for the endpoint.</typeparam>
public static class ServerEndpoint<TEndpoint>
    where TEndpoint : IEndpointHandler
{
    /// <summary>
    /// <para>
    ///     Implementation for mapping AspNetCore endpoints where a handler from the authentication server 
    ///     will be processed through the context pipeline.
    /// </para>
    /// <para>
    ///     For each endpoint, a handler will process the input and create a context object,
    ///     which identifies the request operation.
    /// </para>
    /// <para>
    ///     The context created is sent through the pipeline for the request to be processed.
    /// </para>
    /// <para>
    ///     Finally, once the context has been processed,
    ///     the same object should have a handler for creating the Response.
    /// </para>
    /// </summary>
    /// <param name="httpContext">The AspNetCore HttpContext.</param>
    /// <param name="endpointHandler">The endpoint handler.</param>
    /// <param name="pipelineDispatcher">The pipeline dispatcher.</param>
    /// <returns>
    ///     The result of processing the request.
    /// </returns>
    public static async Task<IResult> EndpointHandler(
        HttpContext httpContext,
        TEndpoint endpointHandler,
        IPipelineDispatcher pipelineDispatcher)
    {
        // try to create a context from the http context for the endpoint
        var result = await endpointHandler.TryCreateContextAsync(httpContext);

        if (!result.IsValid(out var context, out var responseHandler))
        {
            return await responseHandler.CreateResponseAsync(httpContext.RequestAborted);
        }

        // send the context through the pipeline to be processed
        await pipelineDispatcher.SendAsync(context, httpContext.RequestAborted);

        // get the response handler from the context
        responseHandler = context.Response;

        if (responseHandler is null) 
        { 
            // generate a internal server error
            var problemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An internal server error has occurred"
            };

            return Results.Json(problemDetails, statusCode: StatusCodes.Status500InternalServerError);
        }

        // return the response from the response handler
        return await responseHandler.CreateResponseAsync(httpContext.RequestAborted);
    }
}
