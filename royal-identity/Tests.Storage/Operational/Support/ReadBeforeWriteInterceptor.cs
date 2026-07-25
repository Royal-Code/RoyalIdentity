using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Tests.Storage.Operational.Support;

/// <summary>
/// Pins the interleaving a single-use primitive has to survive: every participant completes its <c>SELECT</c>
/// before any of them runs its <c>DELETE</c>.
/// <para>
/// Without this, "concurrent consumers" depends on the scheduler landing every caller inside the window between
/// the read and the delete. It usually does, but a proof that only usually holds is not a proof: a regression
/// that ignored the affected-row count could pass by luck. Here the interleaving is a precondition of the test
/// rather than an outcome of timing.
/// </para>
/// </summary>
internal sealed class ReadBeforeWriteInterceptor(int participants) : DbCommandInterceptor
{
    private readonly TaskCompletionSource everyoneRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int armed;
    private int reads;

    /// <summary>Starts gating. Warm-up queries before this run untouched.</summary>
    public void Arm() => Interlocked.Exchange(ref armed, 1);

    /// <summary>Whether every participant reached the gate, so the scenario knows the interleaving happened.</summary>
    public bool Interleaved => everyoneRead.Task.IsCompletedSuccessfully;

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref armed) is 1 && IsArtifactQuery(command))
        {
            if (Interlocked.Increment(ref reads) >= participants)
                everyoneRead.TrySetResult();

            // A generous ceiling: it only ever fires if the scenario wired fewer participants than it starts,
            // in which case failing loudly beats hanging the suite.
            await everyoneRead.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }

        return result;
    }

    private static bool IsArtifactQuery(DbCommand command)
        => command.CommandText.Contains("protocol_artifacts", StringComparison.OrdinalIgnoreCase)
            && command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);
}
