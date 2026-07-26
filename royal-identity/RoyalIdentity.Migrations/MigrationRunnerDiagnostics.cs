using System.Data.Common;
using Microsoft.Extensions.Logging;

namespace RoyalIdentity.Migrations;

/// <summary>
/// Redacts anything secret from what the runner prints (plan DF28). It is public so the guarantee can be
/// asserted directly: a provider error message routinely echoes the connection string that produced it, and
/// that string carries the password.
/// </summary>
public static class MigrationRunnerDiagnostics
{
    public static string Sanitize(string message, MigrationRunnerOptions? options)
    {
        if (options is null)
            return message;

        var sanitized = message;

        // Both connections are redacted: a run may carry two, and a message from the Operational family would
        // otherwise leak the credentials of a database the Configuration redaction never looks at.
        foreach (var connection in Connections(options))
            sanitized = Redact(sanitized, connection, out var invalid) is var redacted && invalid
                ? "A connection string given to the migration runner is invalid."
                : redacted;

        if (!string.IsNullOrWhiteSpace(options.AesKeyEnvironmentVariable))
        {
            var aesKey = Environment.GetEnvironmentVariable(options.AesKeyEnvironmentVariable);
            if (!string.IsNullOrEmpty(aesKey))
                sanitized = sanitized.Replace(aesKey, "[REDACTED]", StringComparison.Ordinal);
        }

        return sanitized;
    }

    /// <summary>The distinct connections this run may have touched — one when both families share a database.</summary>
    private static IEnumerable<string> Connections(MigrationRunnerOptions options)
        => new[] { options.ConfigurationConnection, options.ResolvedOperationalConnection }
            .Where(connection => !string.IsNullOrWhiteSpace(connection))
            .Distinct(StringComparer.Ordinal);

    private static string Redact(string message, string connection, out bool invalidConnection)
    {
        var sanitized = message.Replace(connection, "[REDACTED CONNECTION]", StringComparison.Ordinal);
        invalidConnection = false;

        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connection };
            foreach (var name in new[] { "Password", "Pwd" })
            {
                if (builder.TryGetValue(name, out var value) && value is not null)
                {
                    var secret = Convert.ToString(value);
                    if (!string.IsNullOrEmpty(secret))
                        sanitized = sanitized.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
                }
            }
        }
        catch (ArgumentException)
        {
            // Invalid connection strings are themselves rejected by the provider; never echo the original value.
            invalidConnection = true;
        }

        return sanitized;
    }
}

internal sealed class MigrationRunnerConsoleLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
            Console.Error.WriteLine($"{logLevel}: {formatter(state, exception)}");
    }
}
