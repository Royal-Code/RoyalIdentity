using System.Reflection;
using System.Xml.Linq;
using RoyalIdentity.Security;
using RoyalIdentity.UserAccounts;
using RoyalIdentity.UserAccounts.Integration;
using RoyalIdentity.UserAccounts.PostgreSql;
using RoyalIdentity.UserAccounts.Sqlite;
using RoyalIdentity.Users.Contracts;

namespace Tests.Architecture;

/// <summary>
/// Enforces the module-family boundaries from ADR-013/ADR-015 §2.1 at the assembly-reference level:
/// the pure module references neither the IdP core nor ASP.NET; the core never references the module;
/// the <c>.Integration</c> adapter is the only project that knows both. Reference checks use
/// <see cref="Assembly.GetReferencedAssemblies"/>, which lists only assemblies actually bound by the
/// compiler — so an accidental <c>using</c> that crosses a boundary makes the corresponding test fail.
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly Assembly Core = typeof(IUserDirectory).Assembly;                  // RoyalIdentity
    private static readonly Assembly PureModule = typeof(UserAccountsAssemblyMarker).Assembly; // RoyalIdentity.UserAccounts
    private static readonly Assembly Integration = typeof(UserAccountsIntegrationMarker).Assembly;
    private static readonly Assembly PostgreSqlProvider = typeof(PostgreSqlProviderMarker).Assembly;
    private static readonly Assembly SqliteProvider = typeof(SqliteProviderMarker).Assembly;
    private static readonly Assembly SecurityLibrary = typeof(SecurityAssemblyMarker).Assembly; // RoyalIdentity.Security

    private const string CoreName = "RoyalIdentity";
    private const string ModulePrefix = "RoyalIdentity.UserAccounts";
    private const string SecurityName = "RoyalIdentity.Security";

    public static TheoryData<string, Assembly> ProviderAssemblies => new()
    {
        { "PostgreSql", PostgreSqlProvider },
        { "Sqlite", SqliteProvider }
    };

    [Fact]
    public void Core_DoesNotReference_Module()
    {
        var refs = Core.GetReferencedAssemblies().Select(a => a.Name!);
        Assert.DoesNotContain(refs, n => n.StartsWith(ModulePrefix, StringComparison.Ordinal));
    }

    [Fact]
    public void PureModule_DoesNotReference_Core()
    {
        var refs = Core.GetReferencedAssemblies(); // sanity: core assembly exists/loads
        Assert.NotEmpty(refs);

        // Exact-name match (not a "RoyalIdentity*" prefix) is deliberate: the pure module may depend on a leaf
        // technical library such as RoyalIdentity.Security (ADR-016) without that counting as a core dependency.
        var moduleRefs = PureModule.GetReferencedAssemblies().Select(a => a.Name!);
        Assert.DoesNotContain(moduleRefs, n => n == CoreName);
    }

    [Fact]
    public void PureModule_DoesNotDependOn_AspNetCore()
    {
        // Domain/ and Features/ live in this same assembly, so the assembly-level guard covers them too.
        var moduleRefs = PureModule.GetReferencedAssemblies().Select(a => a.Name!);
        Assert.DoesNotContain(moduleRefs, n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Integration_References_Both_Core_And_Module()
    {
        var refs = Integration.GetReferencedAssemblies().Select(a => a.Name!).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(CoreName, refs);
        Assert.Contains("RoyalIdentity.UserAccounts", refs);
    }

    [Theory]
    [MemberData(nameof(ProviderAssemblies))]
    public void Providers_Reference_Module_And_Not_Core(string _, Assembly provider)
    {
        var refs = provider.GetReferencedAssemblies().Select(a => a.Name!).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("RoyalIdentity.UserAccounts", refs);
        Assert.DoesNotContain(refs, n => n == CoreName);
    }

    [Theory]
    [MemberData(nameof(ProviderAssemblies))]
    public void Providers_DoNotDependOn_AspNetCore(string _, Assembly provider)
    {
        var refs = provider.GetReferencedAssemblies().Select(a => a.Name!);
        Assert.DoesNotContain(refs, n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Integration_Is_The_Only_UserAccounts_Family_Assembly_Referencing_Core()
    {
        var familyWithoutIntegration = new[] { PureModule, PostgreSqlProvider, SqliteProvider };

        foreach (var assembly in familyWithoutIntegration)
        {
            var refs = assembly.GetReferencedAssemblies().Select(a => a.Name!);
            Assert.DoesNotContain(refs, n => n == CoreName);
        }

        var integrationRefs = Integration.GetReferencedAssemblies().Select(a => a.Name!);
        Assert.Contains(CoreName, integrationRefs);
    }

    [Fact]
    public void SecurityLibrary_DoesNotReference_Core()
    {
        var refs = SecurityLibrary.GetReferencedAssemblies().Select(a => a.Name!);
        Assert.DoesNotContain(refs, n => n == CoreName);
    }

    [Fact]
    public void SecurityLibrary_DoesNotReference_AnyDomainModule()
    {
        var refs = SecurityLibrary.GetReferencedAssemblies().Select(a => a.Name!);
        Assert.DoesNotContain(refs, n => n.StartsWith(ModulePrefix, StringComparison.Ordinal));
    }

    [Fact]
    public void SecurityLibrary_DoesNotDependOn_AspNetCore()
    {
        var refs = SecurityLibrary.GetReferencedAssemblies().Select(a => a.Name!);
        Assert.DoesNotContain(refs, n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Core_DoesNotExpose_Migrated_Security_Types()
    {
        // Types moved to RoyalIdentity.Security (plan Fase 7). Matched by simple name so the guard does not
        // depend on the now-deleted original namespaces and still fails if any is reintroduced in the core.
        string[] migratedTypeNames =
        [
            "CryptoRandom",
            "Base64Url",
            "PasswordHash",
            "TimeConstantComparer",
            "HashExtensions",
            "ECKeyHelper",
            "SecurityKeyExtensions",
            "X509CertificateExtensions",
            "KeyParameters",
            "KeyEncoding",
            "KeySerializationFormat",
        ];

        var coreTypeNames = Core.GetTypes().Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var name in migratedTypeNames)
            Assert.DoesNotContain(name, coreTypeNames);
    }

    [Fact]
    public void PureModule_MayReference_SecurityLibrary_WithoutBreakingPurity()
    {
        // The security library is a leaf technical library (ADR-016), distinct from the IdP core and from
        // ASP.NET. The pure-module purity rules forbid only the exact core assembly and the ASP.NET prefix,
        // so the RoyalIdentity.UserAccounts -> RoyalIdentity.Security edge (plan Fase 6) is legal.
        Assert.NotEqual(CoreName, SecurityName);
        Assert.False(SecurityName.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));

        var refs = PureModule.GetReferencedAssemblies().Select(a => a.Name!);
        Assert.Contains(SecurityName, refs);
    }

    [Fact]
    public void InMemoryStorage_ProjectReference_Graph_DoesNotReference_UserAccounts()
    {
        var projectReferences = ReadProjectReferences("RoyalIdentity.Storage.InMemory/RoyalIdentity.Storage.InMemory.csproj");

        Assert.Contains(projectReferences, r => r.EndsWith("RoyalIdentity/RoyalIdentity.csproj", StringComparison.Ordinal));
        Assert.DoesNotContain(projectReferences, r => r.Contains("RoyalIdentity.UserAccounts", StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> ReadProjectReferences(string relativeProjectPath)
    {
        var projectPath = Path.Combine(FindRepositoryRoot(), relativeProjectPath);
        var document = XDocument.Load(projectPath);

        return document
            .Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Replace('\\', '/'))
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RoyalIdentity.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
