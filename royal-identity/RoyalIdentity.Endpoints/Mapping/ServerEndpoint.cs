using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Endpoints.Defaults;

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
    /// <param name="realm">The realm of the endpoint.</param>
    /// <param name="endpointHandler">The endpoint handler.</param>
    /// <param name="pipelineDispatcher">The pipeline dispatcher.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>
    ///     The result of processing the request.
    /// </returns>
    public static async Task<IResult> EndpointHandler(
        HttpContext httpContext,
        TEndpoint endpointHandler,
        IPipelineDispatcher pipelineDispatcher,
        ILogger<TEndpoint> logger)
    {
        IResponseHandler? responseHandler;
        IContextBase? context;

        /////////////////////////////////////////////////////////////////////
        // Try to create a context from the http context for the endpoint
        /////////////////////////////////////////////////////////////////////

        try
        {
            var result = await endpointHandler.TryCreateContextAsync(httpContext);

            if (!result.IsValid(out context, out responseHandler))
            {
                return await responseHandler.CreateResponseAsync(httpContext.RequestAborted);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create a context using the endpoint handler");

            return InternalServerError();
        }

        /////////////////////////////////////////////////////////////////////
        // Send the context through the pipeline to be processed
        /////////////////////////////////////////////////////////////////////

        try
        {
            await pipelineDispatcher.SendAsync(context, httpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute the pipeline");

            return InternalServerError();
        }

        /////////////////////////////////////////////////////////////////////
        // Creates the Result from the ResponseHandler.
        /////////////////////////////////////////////////////////////////////

        try
        {
            // get the response handler from the context
            responseHandler = context.Response;

            if (responseHandler is null)
            {
                logger.LogError("Endpoint did not produce a ResponseHandler");

                return InternalServerError();
            }

            // return the response from the response handler
            return await responseHandler.CreateResponseAsync(httpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create the Result from the ResponseHandler");

            return InternalServerError();
        }
    }

    private static IResult InternalServerError()
    {
        // generate a internal server error
        return ErrorResponseResult.Create(
            "internal_server_error",
            "An internal server error has occurred",
            statusCode: StatusCodes.Status500InternalServerError);
    }
}
