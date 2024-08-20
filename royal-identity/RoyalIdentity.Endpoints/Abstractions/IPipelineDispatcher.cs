namespace RoyalIdentity.Endpoints.Abstractions;

/// <summary>
/// Service to identify the context pipeline for a given context object 
/// and send the context to be processed by the pipeline.
/// </summary>
public interface IPipelineDispatcher
{
    /// <summary>
    /// Dispatches the context object to the correct pipeline to process it.
    /// </summary>
    /// <param name="context">The context object.</param>
    /// <param name="ct">The <see cref="CancellationToken"/>.</param>
    /// <returns>A <see cref="Task"/> for async processing.</returns>
    Task SendAsync(IContextBase context, CancellationToken ct);
}
