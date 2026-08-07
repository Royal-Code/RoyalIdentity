using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using RoyalIdentity.Contracts.Localization;
using RoyalIdentity.Razor;
using Tests.Integration.Prepare;

namespace Tests.Integration.Localization;

/// <summary>
/// Fase 2 (plan-localization.md) — the two RESX catalogues resolve every key in every shipped culture
/// (DF2/DF3/DF4), the three cultures stay in parity, and resources carry text rather than markup (DF15).
/// </summary>
/// <remarks>
/// The resolution tests are what prove the marker types' base name: <see cref="IStringLocalizer"/> echoes the
/// key back when it cannot find the catalogue, so a namespace or <c>ResourcesPath</c> mistake fails here
/// instead of silently shipping English key names to users.
/// </remarks>
public class LocalizationCatalogTests : IClassFixture<PersistentStorageAppFactory>
{
    private const int AccountKeyCount = 57;
    private const int ValidationKeyCount = 5;

    private static readonly string[] Cultures = ["en", "pt-BR", "es-419"];

    private readonly PersistentStorageAppFactory factory;

    public LocalizationCatalogTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    public static TheoryData<string> ShippedCultures => [.. Cultures];

    [Theory]
    [MemberData(nameof(ShippedCultures))]
    public void AccountResources_ResolveEveryKeyInEveryShippedCulture(string culture)
    {
        AssertEveryKeyResolves(typeof(AccountResources), ReadKeys("AccountResources.resx"), culture);
    }

    [Theory]
    [MemberData(nameof(ShippedCultures))]
    public void ValidationResources_ResolveEveryKeyInEveryShippedCulture(string culture)
    {
        AssertEveryKeyResolves(typeof(ValidationResources), ReadKeys("ValidationResources.resx"), culture);
    }

    [Fact]
    public void Catalogues_HaveTheInventoriedKeyCount()
    {
        Assert.Equal(AccountKeyCount, ReadKeys("AccountResources.resx").Count);
        Assert.Equal(ValidationKeyCount, ReadKeys("ValidationResources.resx").Count);
    }

    [Fact]
    public void EveryCultureFile_HasTheSameKeySetAsItsNeutralCatalogue()
    {
        foreach (var catalogue in new[] { "AccountResources", "ValidationResources" })
        {
            var neutral = ReadKeys($"{catalogue}.resx").Keys.Order(StringComparer.Ordinal);

            foreach (var culture in Cultures.Where(value => value != "en"))
            {
                var translated = ReadKeys($"{catalogue}.{culture}.resx").Keys.Order(StringComparer.Ordinal);
                Assert.Equal(neutral, translated);
            }
        }
    }

    [Fact]
    public void EveryTranslation_UsesTheSamePlaceholdersAsTheNeutralValue()
    {
        foreach (var catalogue in new[] { "AccountResources", "ValidationResources" })
        {
            var neutral = ReadKeys($"{catalogue}.resx");

            foreach (var culture in Cultures.Where(value => value != "en"))
            {
                var translated = ReadKeys($"{catalogue}.{culture}.resx");

                foreach (var (key, value) in neutral)
                {
                    Assert.Equal(
                        Placeholders(value),
                        Placeholders(translated[key]));
                }
            }
        }
    }

    [Fact]
    public void TheSixCatalogueFiles_SumTheInventoriedEntryCount()
    {
        var total = Cultures
            .SelectMany(culture => new[] { "AccountResources", "ValidationResources" }
                .Select(catalogue => culture == "en" ? $"{catalogue}.resx" : $"{catalogue}.{culture}.resx"))
            .Sum(file => ReadKeys(file).Count);

        Assert.Equal((AccountKeyCount + ValidationKeyCount) * Cultures.Length, total);
    }

    [Fact]
    public void Resources_ContainTextRatherThanMarkup()
    {
        // DF15: markup, URLs and encoding decisions belong to the components, not to a translated string.
        foreach (var file in Directory.EnumerateFiles(ResourcesDirectory, "*.resx"))
        {
            foreach (var (key, value) in ReadKeys(Path.GetFileName(file)))
            {
                Assert.False(
                    Regex.IsMatch(value, "<[a-zA-Z/!]", RegexOptions.CultureInvariant),
                    $"{Path.GetFileName(file)}:{key} contains markup: '{value}'.");
            }
        }
    }

    [Fact]
    public void ComposedHost_ExposesTheShippedCataloguesThroughTheUiLocaleCatalog()
    {
        var catalog = factory.Services.GetRequiredService<IUiLocaleCatalog>();

        Assert.Equal("en", catalog.NeutralLocale);
        Assert.Equal(Cultures, catalog.AvailableLocales);
        Assert.True(catalog.Supports("pt-BR"));
        Assert.True(catalog.Supports("PT-br"));
        Assert.False(catalog.Supports("fr"));
    }

    [Fact]
    public void AHostWithoutAComposedUi_PromisesNoLocales()
    {
        var catalog = new EmptyUiLocaleCatalog();

        Assert.Null(catalog.NeutralLocale);
        Assert.Empty(catalog.AvailableLocales);
        Assert.False(catalog.Supports("en"));
    }

    private void AssertEveryKeyResolves(Type marker, IReadOnlyDictionary<string, string> keys, string culture)
    {
        var localizer = factory.Services.GetRequiredService<IStringLocalizerFactory>().Create(marker);
        var previous = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

            foreach (var key in keys.Keys)
            {
                var localized = localizer[key];

                Assert.False(localized.ResourceNotFound, $"{marker.Name}.{key} is missing for '{culture}'.");
                Assert.NotEqual(key, localized.Value);
                Assert.False(string.IsNullOrWhiteSpace(localized.Value));
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static string ResourcesDirectory => Path.Combine(
        FindSolutionRoot(),
        "RoyalIdentity.Razor",
        "Resources");

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RoyalIdentity.sln")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException("RoyalIdentity.sln was not found above the test output.");
    }

    private static IReadOnlyDictionary<string, string> ReadKeys(string fileName)
        => XDocument
            .Load(Path.Combine(ResourcesDirectory, fileName))
            .Root!
            .Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")!.Value,
                StringComparer.Ordinal);

    private static IReadOnlyList<string> Placeholders(string value)
        => [.. Regex
            .Matches(value, @"\{\d+\}", RegexOptions.CultureInvariant)
            .Select(match => match.Value)
            .Order(StringComparer.Ordinal)];
}
