using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Integration.Prepare;

public class ControlledTimeAppFactory : AppFactory
{
    public ControlledTimeProvider Clock { get; } =
        new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

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
