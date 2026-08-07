using System.Xml.Linq;
using RoyalIdentity.Contracts.Localization;

namespace Tests.Architecture;

/// <summary>
/// Fase 2 (plan-localization.md) — the localization seam only ever points from the UI to the core (DF7), and
/// consumers go through the framework abstraction instead of <c>ResourceManager</c> or a generated designer
/// class (DF3).
/// </summary>
public class LocalizationBoundaryTests
{
    [Fact]
    public void Core_DoesNotReferenceTheRazorUi()
    {
        var references = ProjectReferenceReader
            .ReadProjectReferences(Path.Combine("RoyalIdentity", "RoyalIdentity.csproj"));

        Assert.DoesNotContain(references, reference =>
            reference.Contains("RoyalIdentity.Razor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheLocaleCatalogContract_LivesInTheCoreWithAnEmptyDefault()
    {
        // The core must be able to answer "which locales can this host render" without the UI being composed;
        // otherwise discovery would either depend on Razor or promise locales that do not exist.
        Assert.Equal("RoyalIdentity", typeof(IUiLocaleCatalog).Assembly.GetName().Name);
        Assert.Equal("RoyalIdentity", typeof(EmptyUiLocaleCatalog).Assembly.GetName().Name);
        Assert.True(typeof(IUiLocaleCatalog).IsAssignableFrom(typeof(EmptyUiLocaleCatalog)));
    }

    [Fact]
    public void LocalizationConsumers_UseTheFrameworkAbstractionRatherThanResourceManager()
    {
        var offenders = ProductSourceFiles()
            .Where(file => file.Text.Contains("new ResourceManager(", StringComparison.Ordinal)
                || file.Text.Contains(".ResourceManager.GetString(", StringComparison.Ordinal))
            .Select(file => file.Path)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ResourceCatalogues_DeclareNoGeneratedDesignerClass()
    {
        // A designer class would re-introduce compile-time keys and bypass IStringLocalizer entirely.
        var root = Path.Combine(ProjectReferenceReader.FindRepositoryRoot(), "RoyalIdentity.Razor", "Resources");

        Assert.Empty(Directory.EnumerateFiles(root, "*.Designer.cs", SearchOption.AllDirectories));
    }

    [Fact]
    public void ResourceValues_CarryTextRatherThanMarkup()
    {
        var root = Path.Combine(ProjectReferenceReader.FindRepositoryRoot(), "RoyalIdentity.Razor", "Resources");
        var offenders = Directory
            .EnumerateFiles(root, "*.resx", SearchOption.AllDirectories)
            .SelectMany(file => XDocument.Load(file).Root!.Elements("data")
                .Where(element => element.Element("value")!.Value.Contains('<', StringComparison.Ordinal))
                .Select(element => $"{Path.GetFileName(file)}:{element.Attribute("name")!.Value}"))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static IEnumerable<(string Path, string Text)> ProductSourceFiles()
    {
        var root = ProjectReferenceReader.FindRepositoryRoot();

        return new[] { "RoyalIdentity", "RoyalIdentity.Razor" }
            .SelectMany(project => Directory.EnumerateFiles(
                Path.Combine(root, project),
                "*.cs",
                SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(path => (Path.GetRelativePath(root, path), File.ReadAllText(path)));
    }
}
