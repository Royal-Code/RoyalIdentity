using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Defaults;
using RoyalIdentity.Contracts.Storage;

namespace Tests.Integration.Prepare;

/// <summary>
/// WebApplicationFactory variant that intercepts all IEventDispatcher calls via
/// CapturingEventDispatcher, allowing tests to assert on dispatched events.
/// DispatchEvents does not need to be enabled — capture happens before the inner dispatcher.
/// </summary>
public class EventCapturingAppFactory : AppFactory
{
    public TestEventCapture EventCapture { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(EventCapture);
            // Override IEventDispatcher — last registration wins on GetRequiredService<T>().
            services.AddTransient<IEventDispatcher>(sp =>
            {
                var inner = new DefaultEventDispatcher(
                    sp.GetRequiredService<IStorage>(),
                    sp);
                return new CapturingEventDispatcher(inner, EventCapture);
            });
        });
    }
}
