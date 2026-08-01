using System.Xml.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.Cookies;
using RoyalIdentity.Configuration;
using RoyalIdentity.Authentication;
using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace Tests.Architecture;

/// <summary>
/// Guards the runtime/test boundary and the per-request lifetime rules introduced by
/// <c>plan-oidc-session-management</c> DF17, DF22 and DF23.
/// </summary>
public class CheckSessionBoundaryTests
{
    [Fact]
    public void RuntimeProjects_DoNotReferencePlaywrightOrTheBrowserHarness()
    {
        var root = ProjectReferenceReader.FindRepositoryRoot();
        var offenders = Directory
            .EnumerateFiles(root, "RoyalIdentity*.csproj", SearchOption.AllDirectories)
            .Where(IsTrackedProjectFile)
            .SelectMany(project => ReadReferences(project)
                .Where(reference => reference.Contains("Playwright", StringComparison.OrdinalIgnoreCase)
                    || reference.Contains("Tests.Browser", StringComparison.OrdinalIgnoreCase))
                .Select(reference => $"{Path.GetRelativePath(root, project)} -> {reference}"))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void BrowserHarness_RemainsOutsideTheDefaultSolution()
    {
        var root = ProjectReferenceReader.FindRepositoryRoot();
        var solution = File.ReadAllText(Path.Combine(root, "RoyalIdentity.sln"));

        Assert.DoesNotContain("Tests.Browser", solution, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CookieConfigurator_DoesNotCaptureRequestScopedOrRealmSpecificState()
    {
        var configurator = typeof(ConfigureRealmCookieAuthenticationOptions);
        var constructorDependencies = configurator
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        var fields = configurator
            .GetFields(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic)
            .Select(field => field.FieldType)
            .ToArray();

        Assert.DoesNotContain(typeof(CheckSessionStateManager), constructorDependencies);
        Assert.DoesNotContain(typeof(CheckSessionStateManager), fields);
        Assert.DoesNotContain(typeof(Realm), fields);
        Assert.DoesNotContain(typeof(AuthenticationOptions), fields);
        Assert.DoesNotContain(typeof(IServiceProvider), fields);

        var source = File.ReadAllText(Path.Combine(
            ProjectReferenceReader.FindRepositoryRoot(),
            "RoyalIdentity",
            "Authentication",
            "ConfigureRealmCookieAuthenticationOptions.cs"));
        Assert.Contains("context.HttpContext.RequestServices", source, StringComparison.Ordinal);
        Assert.Contains("GetRequiredService<CheckSessionStateManager>()", source, StringComparison.Ordinal);

        var realm = new Realm(
            null,
            "demo.test",
            "demo",
            "Demo",
            false,
            new RealmOptions(new ServerOptions()));
        var cookieOptions = new CookieAuthenticationOptions();
        new ConfigureRealmCookieAuthenticationOptions(new SnapshotStub(realm)).Configure(
            $"{Constants.Server.RealmAuthenticationNamePrefix}{realm.Path}",
            cookieOptions);
        var callbackTarget = cookieOptions.Events.OnValidatePrincipal.Target;
        var callbackFields = callbackTarget?.GetType()
            .GetFields(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic)
            .Select(field => field.FieldType)
            .ToArray() ?? [];

        Assert.DoesNotContain(typeof(CheckSessionStateManager), callbackFields);
        Assert.DoesNotContain(typeof(Realm), callbackFields);
        Assert.DoesNotContain(typeof(AuthenticationOptions), callbackFields);
        Assert.DoesNotContain(typeof(IServiceProvider), callbackFields);
    }

    [Fact]
    public void BrowserHarness_NeverUsesAWildcardPostMessageTargetOrigin()
    {
        var root = Path.Combine(ProjectReferenceReader.FindRepositoryRoot(), "Tests.Browser");
        var offenders = Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(IsTrackedProjectFile)
            .Where(path => Regex.IsMatch(
                File.ReadAllText(path),
                "postMessage\\s*\\([^;]*,\\s*['\\\"]\\*['\\\"]\\s*\\)",
                RegexOptions.CultureInvariant | RegexOptions.Singleline))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static bool IsTrackedProjectFile(string path)
        => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !path.Contains($"{Path.DirectorySeparatorChar}old-is4{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static IEnumerable<string> ReadReferences(string project)
    {
        var document = XDocument.Load(project);
        return document
            .Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!);
    }

    private sealed class SnapshotStub(Realm realm) : IConfigurationSnapshot
    {
        public bool IsLoaded => true;

        public ServerOptions ServerOptions { get; } = new();

        public IReadOnlyCollection<string> RealmPaths => [realm.Path];

        public DateTimeOffset LoadedAtUtc => DateTimeOffset.UtcNow;

        public DateTimeOffset? LastRefreshFailureUtc => null;

        public Realm? FindRealmByPath(string path)
            => string.Equals(path, realm.Path, StringComparison.Ordinal) ? realm : null;
    }
}
