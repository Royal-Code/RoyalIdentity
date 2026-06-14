using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using System.Collections.Concurrent;

namespace Tests.Integration.Prepare;

/// <summary>
/// <see cref="AppFactory"/> variant that replaces <see cref="IBackChannelLogoutNotifier"/> with a
/// capturing fake, so tests can assert which clients were notified on logout (and with which subject/sid).
/// Last DI registration wins, so the capturing notifier is the one injected into the sign-out manager.
/// </summary>
public class BackChannelCapturingAppFactory : AppFactory
{
    public ConcurrentBag<LogoutBackChannelRequest> BackChannelCapture { get; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(BackChannelCapture);
            services.AddTransient<IBackChannelLogoutNotifier>(
                _ => new CapturingBackChannelLogoutNotifier(BackChannelCapture));
        });
    }
}

public class CapturingBackChannelLogoutNotifier(ConcurrentBag<LogoutBackChannelRequest> capture)
    : IBackChannelLogoutNotifier
{
    public Task SendAsync(LogoutBackChannelRequest request, CancellationToken ct)
    {
        capture.Add(request);
        return Task.CompletedTask;
    }
}
