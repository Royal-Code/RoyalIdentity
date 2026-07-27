using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Defaults.Jobs;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Demo;
using RoyalIdentity.Server;
using RoyalIdentity.Users.Contracts;

namespace Tests.Architecture;

public class HostCompositionTests
{
    [Fact]
    public void ServerPostgreSqlComposition_BuildsCompleteGraphWithoutIo()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ServerProgram).Assembly.GetName().Name,
            EnvironmentName = "Production",
        });
        var services = builder.Services;
        services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RoyalIdentity:Connections:Configuration:ConnectionString"] = PostgreSqlConnection,
                ["RoyalIdentity:Connections:Operational:ConnectionString"] = PostgreSqlConnection,
                ["RoyalIdentity:Connections:UserAccounts:ConnectionString"] = PostgreSqlConnection,
                ["RoyalIdentity:Snapshot:RefreshInterval"] = "00:05:00",
                ["RoyalIdentity:Cleanup:Mode"] = "External",
                ["RoyalIdentity:Cleanup:Interval"] = "00:15:00",
                ["RoyalIdentity:Cleanup:BatchSize"] = "500",
                ["RoyalIdentity:DataProtection:KeyRingPath"] =
                    Path.Combine(Path.GetTempPath(), $"royalidentity-server-graph-{Guid.NewGuid():N}"),
                ["RoyalIdentity:DataProtection:ApplicationName"] = "RoyalIdentity.Server.Tests",
                ["RoyalIdentity:DataProtection:OperationalPayloadProfileId"] = "default",
            });

        services.AddHostServices(builder.Configuration, builder.Environment);

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IStorage));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IUserDirectory));
        Assert.DoesNotContain(services, descriptor => IsForbiddenProductionType(descriptor.ServiceType));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ImplementationType is not null &&
            IsForbiddenProductionType(descriptor.ImplementationType));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ImplementationType == typeof(FirstKeyJob));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IStorage>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUserDirectory>());
    }

    [Fact]
    public void DemoSqliteComposition_BuildsCompleteGraphWithoutOptions()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(DemoProgram).Assembly.GetName().Name,
            EnvironmentName = "Development",
        });
        var services = builder.Services;
        services.AddRazorComponents()
            .AddInteractiveServerComponents();
        services.AddRoyalIdentityDemo();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IStorage>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUserDirectory>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IStorage));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IUserDirectory));
    }

    private static bool IsForbiddenProductionType(Type type)
    {
        var assemblyName = type.Assembly.GetName().Name ?? string.Empty;
        return assemblyName.Contains("Sqlite", StringComparison.Ordinal)
            || assemblyName.Contains("InMemory", StringComparison.Ordinal)
            || assemblyName.Contains("Migrations", StringComparison.Ordinal)
            || assemblyName.Contains("Demo", StringComparison.Ordinal);
    }

    private const string PostgreSqlConnection =
        "Host=127.0.0.1;Port=5432;Database=royalidentity;Username=royalidentity;Password=not-used";

}
