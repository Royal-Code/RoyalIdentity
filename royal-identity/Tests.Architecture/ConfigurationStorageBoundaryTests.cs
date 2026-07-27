using System.Reflection;
using System.Xml.Linq;
using RoyalIdentity.Data.Configuration;
using RoyalIdentity.Storage.EntityFramework;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;
using RoyalIdentity.Storage.EntityFramework.Sqlite;

namespace Tests.Architecture;

/// <summary>
/// Enforces the boundaries of the core Configuration storage family (ADR-013 §2.1/§2.3,
/// plan-data-configuration-storage DF1 e Fase 1): the pure Data project references neither the IdP core,
/// nor the adapter, nor ASP.NET; only the adapter knows core and Data; providers sit on adapter/Data; the
/// runner sits on providers; the Server may compose only the production adapters/providers; and the core remains
/// provider-neutral.
/// Assembly-level checks catch accidental <c>using</c>s; csproj-graph checks pin the intended references
/// even before later phases bind them in code.
/// </summary>
public class ConfigurationStorageBoundaryTests
{
    private static readonly Assembly DataConfiguration = typeof(ConfigurationDataAssemblyMarker).Assembly;
    private static readonly Assembly Adapter = typeof(EntityFrameworkStorageAssemblyMarker).Assembly;
    private static readonly Assembly SqliteProvider = typeof(EntityFrameworkSqliteAssemblyMarker).Assembly;
    private static readonly Assembly PostgreSqlProvider = typeof(EntityFrameworkPostgreSqlAssemblyMarker).Assembly;

    private const string CoreName = "RoyalIdentity";
    private const string DataName = "RoyalIdentity.Data.Configuration";
    private const string AdapterName = "RoyalIdentity.Storage.EntityFramework";

    public static TheoryData<string, Assembly> ProviderAssemblies => new()
    {
        { "Sqlite", SqliteProvider },
        { "PostgreSql", PostgreSqlProvider }
    };

    [Fact]
    public void DataConfiguration_DoesNotReference_Core_Adapter_Or_AspNetCore()
    {
        var refs = DataConfiguration.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

        Assert.DoesNotContain(refs, n => n == CoreName);
        Assert.DoesNotContain(refs, n => n.StartsWith(AdapterName, StringComparison.Ordinal));
        Assert.DoesNotContain(refs, n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void DataConfiguration_DependsOn_EntityFrameworkCore_Only_AsDataStack()
    {
        var refs = DataConfiguration.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

        Assert.Contains(refs, n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(refs, n => n.StartsWith("RoyalIdentity", StringComparison.Ordinal));
    }

    [Fact]
    public void DataConfiguration_Project_HasNoProjectReferences()
    {
        var projectReferences = ProjectReferenceReader.ReadProjectReferences(
            "RoyalIdentity.Data.Configuration/RoyalIdentity.Data.Configuration.csproj");

        Assert.Empty(projectReferences);
    }

    [Fact]
    public void Adapter_ProjectGraph_References_Core_And_Data_Only()
    {
        var projectReferences = ProjectReferenceReader.ReadProjectReferences(
            "RoyalIdentity.Storage.EntityFramework/RoyalIdentity.Storage.EntityFramework.csproj");

        // The adapter is the only project that knows the core and both pure Data families.
        Assert.Equal(3, projectReferences.Count);
        Assert.Contains(projectReferences, r => r.EndsWith("RoyalIdentity/RoyalIdentity.csproj", StringComparison.Ordinal));
        Assert.Contains(projectReferences, r => r.EndsWith(
            "RoyalIdentity.Data.Configuration/RoyalIdentity.Data.Configuration.csproj", StringComparison.Ordinal));
        Assert.Contains(projectReferences, r => r.EndsWith(
            "RoyalIdentity.Data.Operational/RoyalIdentity.Data.Operational.csproj", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(ProviderAssemblies))]
    public void Providers_DoNotBind_Core_Directly(string _, Assembly provider)
    {
        var refs = provider.GetReferencedAssemblies().Select(a => a.Name!);

        Assert.DoesNotContain(refs, n => n == CoreName);
    }

    [Theory]
    [InlineData("RoyalIdentity.Storage.EntityFramework.Sqlite/RoyalIdentity.Storage.EntityFramework.Sqlite.csproj", 3)]
    [InlineData("RoyalIdentity.Storage.EntityFramework.PostgreSql/RoyalIdentity.Storage.EntityFramework.PostgreSql.csproj", 3)]
    public void Providers_ProjectGraph_References_Adapter_And_Data_Only(string providerProject, int expectedReferences)
    {
        // A provider references the adapter plus the pure Data projects of the families it maps. Since Fase 7
        // of plan-data-operational-storage both providers map both families, and neither may reach the core.
        var projectReferences = ProjectReferenceReader.ReadProjectReferences(providerProject);

        Assert.Equal(expectedReferences, projectReferences.Count);
        Assert.Contains(projectReferences, r => r.EndsWith(
            "RoyalIdentity.Storage.EntityFramework/RoyalIdentity.Storage.EntityFramework.csproj", StringComparison.Ordinal));
        Assert.Contains(projectReferences, r => r.EndsWith(
            "RoyalIdentity.Data.Configuration/RoyalIdentity.Data.Configuration.csproj", StringComparison.Ordinal));
        Assert.Contains(projectReferences, r => r.EndsWith(
            "RoyalIdentity.Data.Operational/RoyalIdentity.Data.Operational.csproj", StringComparison.Ordinal));
        Assert.All(
            projectReferences,
            r => Assert.DoesNotContain("RoyalIdentity/RoyalIdentity.csproj", r, StringComparison.Ordinal));
    }

    [Fact]
    public void MigrationsRunner_ProjectGraph_References_Providers_Only()
    {
        var projectReferences = ProjectReferenceReader.ReadProjectReferences(
            "RoyalIdentity.Migrations/RoyalIdentity.Migrations.csproj");

        string[] allowedProviders =
        [
            "RoyalIdentity.Storage.EntityFramework.Sqlite/RoyalIdentity.Storage.EntityFramework.Sqlite.csproj",
            "RoyalIdentity.Storage.EntityFramework.PostgreSql/RoyalIdentity.Storage.EntityFramework.PostgreSql.csproj",
            "RoyalIdentity.UserAccounts.Sqlite/RoyalIdentity.UserAccounts.Sqlite.csproj",
            "RoyalIdentity.UserAccounts.PostgreSql/RoyalIdentity.UserAccounts.PostgreSql.csproj",
        ];

        // ADR-013: this executable is the composition root of two independent module families. Referencing both
        // provider families applies their migrations; it does not translate core and UserAccounts types and
        // therefore does not assume the role reserved for RoyalIdentity.UserAccounts.Integration.
        Assert.Equal(allowedProviders.Length, projectReferences.Count);
        Assert.All(
            projectReferences,
            reference => Assert.Contains(
                allowedProviders,
                allowed => reference.EndsWith(allowed, StringComparison.Ordinal)));
        Assert.DoesNotContain(projectReferences, r => r.EndsWith(
            "RoyalIdentity/RoyalIdentity.csproj", StringComparison.Ordinal));
        Assert.DoesNotContain(projectReferences, r => r.Contains("RoyalIdentity.Demo", StringComparison.Ordinal));
        Assert.DoesNotContain(projectReferences, r => r.Contains("Tests.", StringComparison.Ordinal));
    }

    [Fact]
    public void Server_ProjectGraph_AllowsOnly_ProductionComposition_Dependencies()
    {
        var projectReferences = ProjectReferenceReader.ReadProjectReferences(
            "RoyalIdentity.Server/RoyalIdentity.Server.csproj");

        string[] allowedProjects =
        [
            "RoyalIdentity/RoyalIdentity.csproj",
            "RoyalIdentity.Razor/RoyalIdentity.Razor.csproj",
            "RoyalIdentity.Storage.InMemory/RoyalIdentity.Storage.InMemory.csproj", // transitional until Plan 4 Fase 3
            "RoyalIdentity.Storage.EntityFramework/RoyalIdentity.Storage.EntityFramework.csproj",
            "RoyalIdentity.Storage.EntityFramework.PostgreSql/RoyalIdentity.Storage.EntityFramework.PostgreSql.csproj",
            "RoyalIdentity.UserAccounts.Integration/RoyalIdentity.UserAccounts.Integration.csproj",
            "RoyalIdentity.UserAccounts.PostgreSql/RoyalIdentity.UserAccounts.PostgreSql.csproj",
        ];

        Assert.All(
            projectReferences,
            reference => Assert.Contains(
                allowedProjects,
                allowed => reference.EndsWith(allowed, StringComparison.Ordinal)));
        Assert.DoesNotContain(projectReferences, r => r.Contains(".Sqlite/", StringComparison.Ordinal));
        Assert.DoesNotContain(projectReferences, r => r.Contains("RoyalIdentity.Demo", StringComparison.Ordinal));
        Assert.DoesNotContain(projectReferences, r => r.Contains("RoyalIdentity.Data.", StringComparison.Ordinal));
        Assert.DoesNotContain(projectReferences, r => r.Contains("RoyalIdentity.Migrations", StringComparison.Ordinal));
        Assert.DoesNotContain(projectReferences, r => r.Contains("Tests.", StringComparison.Ordinal));
    }

    [Fact]
    public void Hosts_And_Migrations_DoNotAcquire_InverseEntryPointReferences()
    {
        var testHostReferences = ProjectReferenceReader.ReadProjectReferences("Tests.Host/Tests.Host.csproj");
        var migrationsReferences = ProjectReferenceReader.ReadProjectReferences(
            "RoyalIdentity.Migrations/RoyalIdentity.Migrations.csproj");

        Assert.DoesNotContain(testHostReferences, r => r.Contains("RoyalIdentity.Server", StringComparison.Ordinal));
        Assert.DoesNotContain(testHostReferences, r => r.Contains("RoyalIdentity.Demo", StringComparison.Ordinal));
        Assert.DoesNotContain(migrationsReferences, r => r.Contains("RoyalIdentity.Server", StringComparison.Ordinal));
        Assert.DoesNotContain(migrationsReferences, r => r.Contains("RoyalIdentity.Demo", StringComparison.Ordinal));
    }

    [Fact]
    public void IntegrationTests_ReferenceServerThroughTheNamedAlias()
    {
        var projectPath = Path.Combine(
            ProjectReferenceReader.FindRepositoryRoot(),
            "Tests.Integration",
            "Tests.Integration.csproj");
        var document = XDocument.Load(projectPath);
        var serverReference = document
            .Descendants("ProjectReference")
            .Single(reference => reference
                .Attribute("Include")!
                .Value
                .Replace('\\', '/')
                .EndsWith("RoyalIdentity.Server/RoyalIdentity.Server.csproj", StringComparison.Ordinal));

        Assert.Equal("RoyalIdentityServer", serverReference.Element("Aliases")?.Value);
    }

    [Fact]
    public void ProtocolExtension_LivesInCore_WithoutHostUiOrProviderDependencies()
    {
        var method = typeof(RoyalIdentity.Extensions.ApplicationBuilderExtensions)
            .GetMethod(nameof(RoyalIdentity.Extensions.ApplicationBuilderExtensions.UseRoyalIdentityProtocol));
        var coreReferences = typeof(RoyalIdentity.Extensions.ApplicationBuilderExtensions).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ToArray();

        Assert.NotNull(method);
        Assert.DoesNotContain(coreReferences, name => name == "RoyalIdentity.Razor");
        Assert.DoesNotContain(coreReferences, name => name == "RoyalIdentity.Server");
        Assert.DoesNotContain(coreReferences, name => name.StartsWith(AdapterName, StringComparison.Ordinal));
        Assert.DoesNotContain(coreReferences, name => name.StartsWith("RoyalIdentity.UserAccounts", StringComparison.Ordinal));
    }

    [Fact]
    public void Server_Source_DoesNotInspectOrApplyMigrations()
    {
        var serverDirectory = Path.Combine(ProjectReferenceReader.FindRepositoryRoot(), "RoyalIdentity.Server");
        string[] forbiddenCalls =
        [
            ".EnsureCreated(",
            ".EnsureCreatedAsync(",
            ".Migrate(",
            ".MigrateAsync(",
            "GetPendingMigrations",
        ];

        var sourceFiles = Directory
            .EnumerateFiles(serverDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        foreach (var sourceFile in sourceFiles)
        {
            var source = File.ReadAllText(sourceFile);
            Assert.DoesNotContain(
                forbiddenCalls,
                call => source.Contains(call, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ServerAndTestHost_UseTheSharedProtocolExtension_WithoutSharingPrograms()
    {
        var root = ProjectReferenceReader.FindRepositoryRoot();
        var serverProgram = File.ReadAllText(Path.Combine(root, "RoyalIdentity.Server", "Program.cs"));
        var testHostProgram = File.ReadAllText(Path.Combine(root, "Tests.Host", "Program.cs"));

        Assert.Contains("app.UseRouting();", serverProgram, StringComparison.Ordinal);
        Assert.Contains("app.UseRouting();", testHostProgram, StringComparison.Ordinal);
        Assert.Contains("app.UseRoyalIdentityProtocol();", serverProgram, StringComparison.Ordinal);
        Assert.Contains("app.UseRoyalIdentityProtocol();", testHostProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("app.UseRealmDiscovery();", serverProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("app.UseRealmDiscovery();", testHostProgram, StringComparison.Ordinal);
        Assert.True(
            serverProgram.IndexOf("app.UseRouting();", StringComparison.Ordinal) <
            serverProgram.IndexOf("app.UseRoyalIdentityProtocol();", StringComparison.Ordinal));
        Assert.True(
            testHostProgram.IndexOf("app.UseRouting();", StringComparison.Ordinal) <
            testHostProgram.IndexOf("app.UseRoyalIdentityProtocol();", StringComparison.Ordinal));
        Assert.True(
            serverProgram.IndexOf("app.UseRoyalIdentityProtocol();", StringComparison.Ordinal) <
            serverProgram.IndexOf("app.UseAntiforgery();", StringComparison.Ordinal));
        Assert.True(
            testHostProgram.IndexOf("app.UseRoyalIdentityProtocol();", StringComparison.Ordinal) <
            testHostProgram.IndexOf("app.UseAntiforgery();", StringComparison.Ordinal));
    }

    [Fact]
    public void Core_DoesNotReference_DataOrAdapter()
    {
        var projectReferences = ProjectReferenceReader.ReadProjectReferences("RoyalIdentity/RoyalIdentity.csproj");

        Assert.DoesNotContain(projectReferences, r => r.Contains("RoyalIdentity.Data.Configuration", StringComparison.Ordinal));
        Assert.DoesNotContain(projectReferences, r => r.Contains("RoyalIdentity.Data.Operational", StringComparison.Ordinal));
        Assert.DoesNotContain(projectReferences, r => r.Contains("RoyalIdentity.Storage.EntityFramework", StringComparison.Ordinal));

        var refs = typeof(RoyalIdentity.Contracts.Storage.IStorage).Assembly
            .GetReferencedAssemblies().Select(a => a.Name!);
        Assert.DoesNotContain(refs, n => n == DataName || n.StartsWith(AdapterName, StringComparison.Ordinal));
    }
}
