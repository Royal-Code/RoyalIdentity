namespace RoyalIdentity.Endpoints.Abstractions;

/// <summary>
/// <para>
///     A pipeline service for processing an endpoint context from the authentication server.
/// </para>
/// <para>
///     For each configured endpoint, a context object is created via an <see cref="IEndpointHandler{TContext}"/>.
/// </para>
/// <para>
///     The context created must be sent for processing through a pipeline, 
///     where it (the context) can pass through various validators, decorators and finally be processed.
/// </para>
/// <para>
///     In the end, the result of the processing should generate 
///     the <see cref="IResponseHandler"/> and assign it to the context object.
/// </para>
/// </summary>
/// <typeparam name="TContext">Type of context object.</typeparam>
public interface IContextPipeline<in TContext>
    where TContext : class, IContextBase
{
    /// <summary>
    /// Sends the context to be processed through the pipeline.
    /// </summary>
    /// <param name="context">The context object.</param>
    /// <param name="ct">The <see cref="CancellationToken"/>.</param>
    /// <returns>A <see cref="Task"/> for async processing.</returns>
    Task SendAsync(TContext context, CancellationToken ct);
}
