using System.Reflection;
using RoyalIdentity.Models.Security;

namespace Tests.Architecture;

/// <summary>
/// Guards the pure, ephemeral assessment boundary introduced by plan-rfc9700-security-hardening Fase 1.
/// </summary>
public class Rfc9700BoundaryTests
{
    [Fact]
    public void AssessmentContract_LivesOnlyInTheCoreAndHasNoServiceInterfaceOrSnapshot()
    {
        var core = typeof(ClientSecurityAssessment).Assembly;

        Assert.Equal("RoyalIdentity", core.GetName().Name);
        Assert.Null(core.GetType("RoyalIdentity.Models.Security.IClientSecurityAssessor"));
        Assert.Null(core.GetType("RoyalIdentity.Models.Security.ClientSecurityAssessmentSnapshot"));
        Assert.Empty(typeof(ClientSecurityAssessment).GetInterfaces());
    }

    [Fact]
    public void AssessmentFactory_IsStaticAndConstructionIsNotPublic()
    {
        var factory = typeof(ClientSecurityAssessment).GetMethod(
            nameof(ClientSecurityAssessment.Create),
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(factory);
        Assert.Equal(typeof(ClientSecurityAssessment), factory.ReturnType);
        Assert.Empty(typeof(ClientSecurityAssessment).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void AssessmentSource_HasNoDiIoClockOrPersistenceDependency()
    {
        var files = AssessmentSourceFiles().ToArray();
        var forbidden = new[]
        {
            "Microsoft.Extensions.DependencyInjection",
            "RoyalIdentity.Data",
            "RoyalIdentity.Razor",
            "RoyalIdentity.Storage",
            "EntityFramework",
            "IStorage",
            "TimeProvider",
            "DateTime.Now",
            "DateTime.UtcNow",
            "HttpContext",
        };

        Assert.NotEmpty(files);
        Assert.All(files, file => Assert.All(forbidden, token =>
            Assert.DoesNotContain(token, file.Text, StringComparison.Ordinal)));
    }

    [Fact]
    public void AssessmentTypes_DoNotLeakIntoUiDataOrProviderProjects()
    {
        var root = ProjectReferenceReader.FindRepositoryRoot();
        var projects = new[]
        {
            "RoyalIdentity.Razor",
            "RoyalIdentity.Data.Configuration",
            "RoyalIdentity.Data.Operational",
            "RoyalIdentity.Storage.EntityFramework",
            "RoyalIdentity.Storage.EntityFramework.Sqlite",
            "RoyalIdentity.Storage.EntityFramework.PostgreSql",
        };
        var forbiddenNames = new[]
        {
            nameof(ClientSecurityAssessment),
            nameof(ClientSecurityFinding),
            "ClientSecurityAssessmentSnapshot",
        };

        var offenders = projects
            .SelectMany(project => Directory.EnumerateFiles(
                Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => forbiddenNames.Any(name =>
                File.ReadAllText(path).Contains(name, StringComparison.Ordinal)))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Assessment_IsNotRegisteredOrConsumedAsARuntimeAuthorizationGate()
    {
        var root = ProjectReferenceReader.FindRepositoryRoot();
        var core = Path.Combine(root, "RoyalIdentity");
        var assessmentDirectory = Path.Combine(core, "Models", "Security");
        var offenders = Directory
            .EnumerateFiles(core, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.StartsWith(assessmentDirectory, StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => File.ReadAllText(path).Contains(nameof(ClientSecurityAssessment), StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static IEnumerable<(string Path, string Text)> AssessmentSourceFiles()
    {
        var root = ProjectReferenceReader.FindRepositoryRoot();
        var directory = Path.Combine(root, "RoyalIdentity", "Models", "Security");

        return Directory.EnumerateFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => (Path.GetRelativePath(root, path), File.ReadAllText(path)));
    }
}
