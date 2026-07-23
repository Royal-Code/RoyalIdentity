using System.Data.Common;
using Microsoft.Extensions.Logging;

namespace RoyalIdentity.Migrations;

internal static class MigrationRunnerDiagnostics
{
	public static string Sanitize(string message, MigrationRunnerOptions? options)
	{
		if (options is null)
			return message;

		var sanitized = message.Replace(
			options.ConfigurationConnection,
			"[REDACTED CONNECTION]",
			StringComparison.Ordinal);
		try
		{
			var builder = new DbConnectionStringBuilder { ConnectionString = options.ConfigurationConnection };
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
			sanitized = "The Configuration connection string is invalid.";
		}

		if (!string.IsNullOrWhiteSpace(options.AesKeyEnvironmentVariable))
		{
			var aesKey = Environment.GetEnvironmentVariable(options.AesKeyEnvironmentVariable);
			if (!string.IsNullOrEmpty(aesKey))
				sanitized = sanitized.Replace(aesKey, "[REDACTED]", StringComparison.Ordinal);
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
