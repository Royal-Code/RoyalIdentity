using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Tests.Integration.Prepare;

/// <summary>
/// <see cref="PersistentStorageAppFactory"/> variant that keeps every formatted log message, so a test can assert
/// what the server did <b>not</b> write — a credential value leaking into a log is invisible to any assertion
/// made only over the HTTP response.
/// </summary>
public class LogCapturingAppFactory : PersistentStorageAppFactory
{
    public ConcurrentQueue<string> LogMessages { get; } = new();

    /// <summary>Every captured message, joined so a single assertion can cover the whole log.</summary>
    public string AllLogText => string.Join("\n", LogMessages);

    public void ClearLog() => LogMessages.Clear();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
            services.AddSingleton<ILoggerProvider>(new CapturingLoggerProvider(LogMessages)));
    }

    private sealed class CapturingLoggerProvider(ConcurrentQueue<string> messages) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Enqueue(formatter(state, exception));

            if (exception is not null)
                messages.Enqueue(exception.ToString());
        }
    }
}
