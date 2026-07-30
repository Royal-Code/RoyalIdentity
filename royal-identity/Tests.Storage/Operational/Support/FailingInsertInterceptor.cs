using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Tests.Storage.Operational.Support;

/// <summary>
/// Fails the <c>INSERT</c> into one table with an error that is <b>not</b> a uniqueness conflict, leaving reads
/// working. EF wraps a command failure raised during <c>SaveChanges</c> in a <c>DbUpdateException</c>, which is
/// exactly the shape a store has to tell apart from a real conflict.
/// <para>
/// Reads are left alone on purpose: it is the branch where the store's confirmation query succeeds and finds
/// nothing that must propagate the failure instead of reporting a replay.
/// </para>
/// </summary>
internal sealed class FailingInsertInterceptor(string table) : DbCommandInterceptor
{
    /// <summary>Message the staged failure carries, so a scenario can identify it.</summary>
    public const string FailureMessage = "staged infrastructure failure";

    private int armed;

    /// <summary>Starts failing. Migrations and any seeding before this run untouched.</summary>
    public void Arm() => Interlocked.Exchange(ref armed, 1);

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Fail(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Fail(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Fail(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Fail(command);
        return ValueTask.FromResult(result);
    }

    private void Fail(DbCommand command)
    {
        if (Volatile.Read(ref armed) is not 1)
            return;

        var text = command.CommandText;
        if (text.Contains($"INSERT INTO \"{table}\"", StringComparison.OrdinalIgnoreCase)
            || text.Contains($"INSERT INTO {table}", StringComparison.OrdinalIgnoreCase))
        {
            throw new DbUpdateStagedFailureException(FailureMessage);
        }
    }
}

/// <summary>The staged failure itself, distinguishable from anything the provider would raise on its own.</summary>
internal sealed class DbUpdateStagedFailureException(string message) : Exception(message);
