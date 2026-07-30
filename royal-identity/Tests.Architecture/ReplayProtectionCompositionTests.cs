using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using System.Xml.Linq;
using RoyalIdentity.Contracts.Defaults.ReplayProtection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Demo;
using RoyalIdentity.Extensions;
using RoyalIdentity.Server;

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

    /// <summary>
    /// The Server must resolve the <b>durable</b> backing, not merely something that is not a no-op. Registering
    /// the in-memory one here would still refuse a replay — but only the one its own process saw, which in a
    /// replicated deployment is most of them getting through.
    /// </summary>
    [Fact]
    public async Task ServerComposition_ResolvesTheOperationalBacking()
    {
        var services = ServerServices();

        await using var provider = services.BuildServiceProvider();

        // StartAsync is what enforces that the declaration and the resolved store agree, so asserting the
        // declaration after it has passed is an assertion about the instance too.
        await new ReplayProtectionStartupValidator(provider).StartAsync(default);

        var registration = Assert.Single(provider.GetServices<ReplayProtectionRegistration>());
        Assert.Equal("operational", registration.StrategyName);
        Assert.Equal(
            "RoyalIdentity.Storage.EntityFramework.Operational.Stores.EntityFrameworkReplayProtectionStore",
            registration.StoreType.FullName);

        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IReplayProtectionStore>();
        Assert.IsNotType<InMemoryReplayProtectionStore>(store);
        Assert.Same(registration.StoreType, store.GetType());
    }

    /// <summary>
    /// The Demo resolves the in-memory backing, which is coherent with what it is: one ephemeral process whose
    /// whole database dies with it.
    /// </summary>
    [Fact]
    public async Task DemoComposition_ResolvesTheInMemoryBacking()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(DemoProgram).Assembly.GetName().Name,
            EnvironmentName = "Development",
        });
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddRoyalIdentityDemo();

        await using var provider = builder.Services.BuildServiceProvider();

        await new ReplayProtectionStartupValidator(provider).StartAsync(default);

        var registration = Assert.Single(provider.GetServices<ReplayProtectionRegistration>());
        Assert.Equal("in-memory", registration.StrategyName);

        using var scope = provider.CreateScope();
        Assert.IsType<InMemoryReplayProtectionStore>(
            scope.ServiceProvider.GetRequiredService<IReplayProtectionStore>());
    }

    /// <summary>
    /// <para>
    ///     Pins the set of implementations the product ships. A no-op cannot be detected by inspection — "always
    ///     answers true" is a behavior, not a shape — so what this guards is the only thing that can be guarded
    ///     statically: that a third implementation cannot appear in any productive project capable of referencing
    ///     the contract without failing this test.
    /// </para>
    /// <para>
    ///     The project graph is read from source instead of maintained as an assembly allowlist. A new productive
    ///     project that reaches the core must also be referenced by <c>Tests.Architecture</c>, or the missing
    ///     assembly makes the guard fail. The two known implementations are proven to refuse a replay by their
    ///     own suites.
    /// </para>
    /// </summary>
    [Fact]
    public void TheProduct_ShipsExactlyTheTwoKnownImplementations()
    {
        var implementations = LoadProductAssembliesThatReachTheCore()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(typeof(IReplayProtectionStore).IsAssignableFrom)
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "RoyalIdentity.Contracts.Defaults.ReplayProtection.InMemoryReplayProtectionStore",
                "RoyalIdentity.Storage.EntityFramework.Operational.Stores.EntityFrameworkReplayProtectionStore",
            ],
            implementations);
    }

    private static IServiceCollection ServerServices()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ServerProgram).Assembly.GetName().Name,
            EnvironmentName = "Production",
        });
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
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
                Path.Combine(Path.GetTempPath(), $"royalidentity-replay-guard-{Guid.NewGuid():N}"),
            ["RoyalIdentity:DataProtection:ApplicationName"] = "RoyalIdentity.Server.Tests",
            ["RoyalIdentity:DataProtection:OperationalPayloadProfileId"] = "default",
        });

        builder.Services.AddHostServices(builder.Configuration, builder.Environment);

        return builder.Services;
    }

    private static IReadOnlyList<Assembly> LoadProductAssembliesThatReachTheCore()
    {
        var root = ProjectReferenceReader.FindRepositoryRoot();
        var coreProject = Path.GetFullPath(
            Path.Combine(root, "RoyalIdentity", "RoyalIdentity.csproj"));
        var productProjects = Directory
            .EnumerateDirectories(root, "RoyalIdentity*", SearchOption.TopDirectoryOnly)
            .SelectMany(directory =>
                Directory.EnumerateFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly))
            .Select(Path.GetFullPath)
            .Where(project => ReferencesProject(
                project,
                coreProject,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
            .OrderBy(project => project, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(productProjects);

        return productProjects
            .Select(project =>
            {
                var document = XDocument.Load(project);
                var assemblyName = document
                    .Descendants("AssemblyName")
                    .Select(element => element.Value)
                    .FirstOrDefault()
                    ?? Path.GetFileNameWithoutExtension(project);
                var assemblyPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll");

                Assert.True(
                    File.Exists(assemblyPath),
                    $"Product assembly '{assemblyName}' is not available to Tests.Architecture. " +
                    "Add its project reference so replay-protection implementations cannot escape this guard.");

                return Assembly.LoadFrom(assemblyPath);
            })
            .ToArray();
    }

    private static bool ReferencesProject(
        string project,
        string targetProject,
        HashSet<string> visited)
    {
        if (string.Equals(project, targetProject, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!visited.Add(project))
            return false;

        var projectDirectory = Path.GetDirectoryName(project)!;
        return XDocument
            .Load(project)
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => Path.GetFullPath(Path.Combine(projectDirectory, reference!)))
            .Any(reference => ReferencesProject(reference, targetProject, visited));
    }

    private const string PostgreSqlConnection =
        "Host=127.0.0.1;Port=5432;Database=royalidentity;Username=royalidentity;Password=not-used";
}
