using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Integration.Prepare;

public class ControlledTimeAppFactory : PersistentStorageAppFactory
{
    public ControlledTimeProvider Clock { get; } =
        new(TimeProvider.System.GetUtcNow());

    /// <summary>
    /// Ensures provisioning/startup has completed, then starts a scenario from the current real instant. The
    /// runner seeds signing keys with the system clock, so a historical fixed instant could put those keys in
    /// this factory's future and make the host fail before the scenario begins.
    /// </summary>
    public void ResetClock()
    {
        _ = Services;
        Clock.SetUtcNow(TimeProvider.System.GetUtcNow());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // base.ConfigureWebHost provisions Configuration first. Move the controlled clock to an instant after
        // that seed and before hosted-service validation starts.
        Clock.SetUtcNow(TimeProvider.System.GetUtcNow());

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}

public class ControlledTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => utcNow;

    public void SetUtcNow(DateTimeOffset value)
    {
        utcNow = value;
    }

    public void Advance(TimeSpan value)
    {
        utcNow = utcNow.Add(value);
    }
}
