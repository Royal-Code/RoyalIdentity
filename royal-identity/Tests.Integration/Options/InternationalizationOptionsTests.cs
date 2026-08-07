using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts;
using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;

namespace Tests.Integration.Options;

/// <summary>
/// Fase 1 (plan-localization.md) — <see cref="InternationalizationOptions"/> is the realm-scoped localization
/// policy (DF1/DF21): active by default, an ordered and case-insensitively distinct locale list (DF22), and a
/// fallback that must belong to it. Normalization canonicalizes tags; validation names what it cannot accept.
/// </summary>
public class InternationalizationOptionsTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public InternationalizationOptionsTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public void Defaults_MatchTheDecidedProductPolicy()
    {
        var options = new InternationalizationOptions();

        Assert.True(options.Enabled);
        Assert.Equal("en", options.DefaultLocale);
        Assert.Equal(["en", "pt-BR", "es-419"], options.SupportedLocales);
        Assert.Empty(options.Validate());
    }

    [Fact]
    public void CopyConstructor_CreatesAnIndependentCopyPreservingOrder()
    {
        var source = new InternationalizationOptions { Enabled = false, DefaultLocale = "pt-BR" };
        source.SupportedLocales.Clear();
        source.SupportedLocales.AddRange(["pt-BR", "es-419", "en"]);

        var copy = new InternationalizationOptions(source);

        Assert.False(copy.Enabled);
        Assert.Equal("pt-BR", copy.DefaultLocale);
        Assert.Equal(["pt-BR", "es-419", "en"], copy.SupportedLocales);

        source.SupportedLocales.Add("fr");
        source.DefaultLocale = "fr";
        source.Enabled = true;

        Assert.Equal(["pt-BR", "es-419", "en"], copy.SupportedLocales);
        Assert.Equal("pt-BR", copy.DefaultLocale);
        Assert.False(copy.Enabled);
    }

    [Fact]
    public void RealmOptionsCopy_DoesNotShareTheLocaleList()
    {
        var source = new RealmOptions(new ServerOptions());
        source.Internationalization.SupportedLocales.Clear();
        source.Internationalization.SupportedLocales.AddRange(["pt-BR", "en"]);

        var copy = new RealmOptions(source);
        source.Internationalization.SupportedLocales.Add("es-419");

        Assert.NotSame(source.Internationalization, copy.Internationalization);
        Assert.Equal(["pt-BR", "en"], copy.Internationalization.SupportedLocales);
    }

    [Theory]
    [InlineData("pt-br", "pt-BR")]
    [InlineData("PT-BR", "pt-BR")]
    [InlineData("ES-419", "es-419")]
    [InlineData("EN", "en")]
    [InlineData("en", "en")]
    public void Normalize_CanonicalizesTagsAndTheDefault(string configured, string canonical)
    {
        var options = Configured(configured);
        options.DefaultLocale = configured;

        options.Normalize();

        Assert.Equal([canonical], options.SupportedLocales);
        Assert.Equal(canonical, options.DefaultLocale);
        Assert.Empty(options.Validate());
    }

    [Fact]
    public void Normalize_DropsLaterCaseInsensitiveDuplicatesAndKeepsConfiguredOrder()
    {
        var options = Configured("pt-BR", "EN", "pt-br", "en", "es-419", "ES-419");

        options.Normalize();

        Assert.Equal(["pt-BR", "en", "es-419"], options.SupportedLocales);
    }

    [Fact]
    public void Normalize_KeepsAnUnknownTagSoValidationCanNameIt()
    {
        var options = Configured("en", "zz-ZZ", "zz-zz");

        options.Normalize();

        // The unknown tag is still deduplicated, so a repeated invalid value is reported once.
        Assert.Equal(["en", "zz-ZZ"], options.SupportedLocales);
        var error = Assert.Single(options.Validate());
        Assert.Contains("zz-ZZ", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsAnEmptySupportedSet()
    {
        var options = new InternationalizationOptions();
        options.SupportedLocales.Clear();

        Assert.Contains(
            options.Validate(),
            error => error.Contains("must contain at least one locale", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("zz-ZZ")]
    [InlineData("xx-XX-XX")]
    [InlineData("not a tag")]
    [InlineData("en_US")]
    [InlineData("")]
    public void Validate_RejectsWhatIsNotAKnownLanguageTag(string tag)
    {
        // "en_US" and "" are the traps: CultureInfo resolves both — the first to the custom name "en_us", the
        // second to the invariant culture — so neither may be accepted as a realm locale.
        var options = Configured("en", tag);

        Assert.Contains(
            options.Validate(),
            error => error.Contains("is not a known locale", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RequiresTheDefaultToBeOneOfTheSupportedLocales()
    {
        var options = Configured("pt-BR", "es-419");
        options.DefaultLocale = "en";

        var error = Assert.Single(options.Validate());
        Assert.Contains("must be one of", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AcceptsADefaultThatDiffersOnlyByCasing()
    {
        var options = Configured("pt-BR");
        options.DefaultLocale = "PT-br";

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void Validate_RejectsAnUnknownDefault()
    {
        var options = Configured("en");
        options.DefaultLocale = "zz-ZZ";

        Assert.Contains(
            options.Validate(),
            error => error.Contains("DefaultLocale 'zz-ZZ' is not a known locale", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_DoesNotDependOnEnabled()
    {
        // Disabling negotiation does not make the policy irrelevant: the UI still renders in DefaultLocale.
        var options = Configured("pt-BR");
        options.Enabled = false;
        options.DefaultLocale = "en";

        Assert.NotEmpty(options.Validate());
    }

    [Fact]
    public async Task SeededRealms_AreMaterializedWithTheProductDefaults()
    {
        foreach (var handle in new[] { factory.Handles.Demo, factory.Handles.Server })
        {
            var realm = await factory.LoadRealmAsync(handle);

            AssertProductDefaults(realm.Options.Internationalization);
        }
    }

    [Fact]
    public async Task ARealmCreatedAtRuntime_IsBornWithTheProductDefaults()
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var path = $"i18n-{CryptoRandom.CreateUniqueId(6)}";

        var realm = await manager.CreateAsync(path, $"{path}.test", "Localization Defaults Realm");

        AssertProductDefaults(realm.Options.Internationalization);
    }

    private static void AssertProductDefaults(InternationalizationOptions internationalization)
    {
        Assert.True(internationalization.Enabled);
        Assert.Equal("en", internationalization.DefaultLocale);
        Assert.Equal(["en", "pt-BR", "es-419"], internationalization.SupportedLocales);
        Assert.Empty(internationalization.Validate());
    }

    private static InternationalizationOptions Configured(params string[] locales)
    {
        var options = new InternationalizationOptions();
        options.SupportedLocales.Clear();
        options.SupportedLocales.AddRange(locales);
        options.DefaultLocale = locales[0];
        return options;
    }
}
