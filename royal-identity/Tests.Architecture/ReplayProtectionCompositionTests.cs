using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoyalIdentity.Contracts.Defaults.ReplayProtection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;

namespace Tests.Architecture;

/// <summary>
/// Guards the composition rule of plan-replay-protection DF12/DF14 at the host level: the core registers no
/// replay-protection default, and a Production host that declares none refuses to start rather than turning the
/// missing registration into a per-request <c>invalid_client</c>.
/// </summary>
public class ReplayProtectionCompositionTests
{
    [Fact]
    public void CoreRegistration_ProvidesNoReplayProtectionDefault()
    {
        var services = new ServiceCollection();

        services.AddOpenIdConnectProviderServices();

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IReplayProtectionStore));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(ReplayProtectionStartupValidator));
    }

    [Fact]
    public async Task ProductionHost_WithoutADeclaredStrategy_FailsStartup()
    {
        // Production is the point: WebApplication.CreateBuilder only turns on ValidateOnBuild in Development, so
        // without this validator the missing registration would first surface at the first private_key_jwt
        // authentication, indistinguishable from a genuine credential failure.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production",
        });
        builder.Services.AddOpenIdConnectProviderServices();

        Assert.False(builder.Environment.IsDevelopment());

        await using var provider = builder.Services.BuildServiceProvider();
        var validator = new ReplayProtectionStartupValidator(provider);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => validator.StartAsync(default));

        Assert.Contains("AddInMemoryReplayProtection()", error.Message, StringComparison.Ordinal);
        Assert.Contains("AddOperationalReplayProtection()", error.Message, StringComparison.Ordinal);
    }
}
