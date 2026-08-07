using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Tests.Integration.Prepare;

namespace Tests.Integration.UI;

/// <summary>
/// Fase 5 (plan-localization.md) — the account screens render in the negotiated culture, the document declares
/// that culture, and no presentable English string survives outside a reviewed technical allowlist.
/// </summary>
public class LocalizedAccountUiTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public LocalizedAccountUiTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData("en", "Log in", "Username", "Password")]
    [InlineData("pt-BR", "Entrar", "Usuário", "Senha")]
    [InlineData("es-419", "Iniciar sesión", "Usuario", "Contraseña")]
    public async Task TheLoginScreen_RendersInTheNegotiatedCulture(
        string culture,
        string title,
        string username,
        string password)
    {
        var html = await GetAsync("account/login", culture);

        Assert.Contains(title, html, StringComparison.Ordinal);
        Assert.Contains(username, html, StringComparison.Ordinal);
        Assert.Contains(password, html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en", "Select domain", "Continue")]
    [InlineData("pt-BR", "Selecionar domínio", "Continuar")]
    [InlineData("es-419", "Seleccionar dominio", "Continuar")]
    public async Task TheDomainScreen_RendersInTheNegotiatedCulture(
        string culture,
        string title,
        string continueLabel)
    {
        // The domain screen is realm-independent by design: it is what chooses the realm.
        var html = await GetAbsoluteAsync("/account/domain", culture);

        Assert.Contains(title, html, StringComparison.Ordinal);
        Assert.Contains(continueLabel, html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("pt-BR")]
    [InlineData("es-419")]
    public async Task TheDocument_DeclaresTheEffectiveCultureAndDirection(string culture)
    {
        var html = await GetAsync("account/login", culture);

        Assert.Contains($"lang=\"{culture}\"", html, StringComparison.Ordinal);
        // The first catalogue is LTR; deriving dir from TextInfo is what keeps RTL possible without a rewrite.
        Assert.Contains("dir=\"ltr\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSameScreen_DiffersBetweenCultures()
    {
        // A catalogue that silently falls back to neutral would pass every "contains" assertion above while
        // rendering identical English pages.
        var english = await GetAsync("account/login", "en");
        var portuguese = await GetAsync("account/login", "pt-BR");

        Assert.NotEqual(english, portuguese);
    }

    [Fact]
    public async Task NoAccountComponent_KeepsAPresentableLiteralOutsideTheReviewedAllowlist()
    {
        var root = FindSolutionRoot();
        var offenders = Directory
            .EnumerateFiles(
                Path.Combine(root, "RoyalIdentity.Razor", "Components"),
                "*.razor",
                SearchOption.AllDirectories)
            .SelectMany(FindPresentableLiterals)
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// Literals that are markup, technical values or tenant data rather than product text. Each entry is a
    /// deliberate exception, not a suppression: adding one is a review decision.
    /// </summary>
    private static readonly string[] AllowedLiterals =
    [
        "ltr", "rtl",
    ];

    private static IEnumerable<string> FindPresentableLiterals(string path)
    {
        var text = File.ReadAllText(path);
        var name = Path.GetFileName(path);

        // Only the markup half of the file renders to a user; the @code block is C#, where comments and
        // identifiers would otherwise register as product text.
        var codeBlock = text.IndexOf("@code", StringComparison.Ordinal);
        if (codeBlock >= 0)
            text = text[..codeBlock];

        // Text nodes between tags: the ones a user reads. Attribute values, code and Razor expressions are
        // excluded because they carry markup and technical values.
        foreach (Match match in Regex.Matches(text, @">([^<>@{}]+)<", RegexOptions.CultureInvariant))
        {
            var candidate = match.Groups[1].Value.Trim();

            if (candidate.Length < 3 || AllowedLiterals.Contains(candidate, StringComparer.Ordinal))
                continue;

            // Product text is words; punctuation, numbers and css-ish fragments are not.
            if (Regex.IsMatch(candidate, @"^[A-Za-z][A-Za-z ,.'!?]{2,}$", RegexOptions.CultureInvariant))
                yield return $"{name}: '{candidate}'";
        }
    }

    private Task<string> GetAsync(string relativePath, string culture)
        => GetAbsoluteAsync($"/{factory.Handles.Demo.Path}/{relativePath}", culture);

    private async Task<string> GetAbsoluteAsync(string path, string culture)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(culture));

        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RoyalIdentity.sln")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException("RoyalIdentity.sln was not found above the test output.");
    }
}
