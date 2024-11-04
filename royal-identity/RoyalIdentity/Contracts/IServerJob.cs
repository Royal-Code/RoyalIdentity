namespace RoyalIdentity.Contracts;

public interface IServerJob
{
    /// <summary>
    /// Get the job name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Determines if is a background job.
    /// </summary>
    public bool Background { get; }

    /// <summary>
    /// Execute the job.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public Task RunAsync(CancellationToken ct);
}
