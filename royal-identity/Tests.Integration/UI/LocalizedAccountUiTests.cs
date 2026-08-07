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
        // Document direction values and the framework's own attribute vocabulary.
        "ltr", "rtl", "post", "get", "text", "password", "checkbox", "hidden", "submit", "button",
        "username", "off", "on", "true", "false", "alert", "stylesheet", "module",
    ];

    /// <summary>
    /// Attributes whose value a user reads. Everything else is markup or wiring.
    /// </summary>
    private static readonly string[] PresentableAttributes = ["placeholder", "title", "alt", "aria-label"];

    /// <summary>
    /// Finds product text a component would render in English regardless of culture.
    /// </summary>
    /// <remarks>
    /// The first version of this scanner was green while four real residues shipped: it stopped at
    /// <c>@code</c>, ignored attributes, and skipped any text node that also contained a Razor expression —
    /// which is exactly the shape of <c>@L["..."] this application will access</c>. It now covers the whole
    /// file, reads the presentable attributes, and splits mixed nodes so an English fragment beside a
    /// localized one is still seen.
    /// </remarks>
    private static IEnumerable<string> FindPresentableLiterals(string path)
    {
        var name = Path.GetFileName(path);

        // Razor directives and C# comments are declarations and prose about the code, not rendered text;
        // both would otherwise register as product strings.
        var text = string.Join(
            "\n",
            File.ReadAllLines(path).Where(line =>
                !Regex.IsMatch(
                    line.TrimStart(),
                    @"^(@(using|inject|page|layout|attribute|inherits|implements|typeparam|namespace|rendermode|preservewhitespace)|//|\*)")));

        // A rendered text node lives on one line. Allowing it to span newlines let a ">" from a generic
        // type argument pair with a "<" further down, turning C# declarations into fake "prose".
        foreach (Match node in Regex.Matches(text, ">([^<>\n]+)<", RegexOptions.CultureInvariant))
        {
            // Splitting on Razor expressions is what exposes an English fragment sitting next to @L[...].
            foreach (var fragment in Regex.Split(node.Groups[1].Value, @"@[A-Za-z_][\w\.]*(?:\[[^\]]*\])?|@\([^)]*\)|@\{|\}"))
            {
                if (IsProductText(fragment, out var candidate))
                    yield return $"{name}: '{candidate}'";
            }
        }

        foreach (var attribute in PresentableAttributes)
        {
            var pattern = attribute + @"\s*=\s*""([^""]*)""";
            foreach (Match match in Regex.Matches(text, pattern, RegexOptions.CultureInvariant))
            {
                if (IsProductText(match.Groups[1].Value, out var candidate))
                    yield return $"{name}: {attribute}='{candidate}'";
            }
        }
    }

    private static bool IsProductText(string fragment, out string candidate)
    {
        candidate = fragment.Trim();

        if (candidate.Length < 3
            || candidate.Contains('@', StringComparison.Ordinal)
            || AllowedLiterals.Contains(candidate, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // A dotted token with no spaces is an identifier or a namespace, never a sentence.
        if (!candidate.Contains(' ', StringComparison.Ordinal) && candidate.Contains('.', StringComparison.Ordinal))
            return false;

        // Prose may start lowercase: a fragment appended after @L["..."] — the shape of the residue this
        // scanner first missed — reads "this application will access", not "This ...".
        var isSentence = Regex.IsMatch(
            candidate,
            @"^[A-Za-z][A-Za-z]*(?: [A-Za-z][A-Za-z,']*)+[.!?…]*$",
            RegexOptions.CultureInvariant);

        // A single word is product text too: a button reading "Continue" or a state reading "Loading..." was
        // invisible to the previous version, which demanded either several words or exactly one final mark.
        var isWord = Regex.IsMatch(candidate, @"^[A-Za-z][A-Za-z']{2,}[.!?…]*$", RegexOptions.CultureInvariant);

        return isSentence || isWord;
    }

    private Task<string> GetAsync(string relativePath, string culture)
        => GetAbsoluteAsync($"/{factory.Handles.Demo.Path}/{relativePath}", culture);

    private async Task<string> GetAbsoluteAsync(string path, string culture)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(culture));

        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        // Razor encodes non-ASCII as HTML entities ("Usu&#xE1;rio"), so comparing against the literal
        // "Usuário" would fail on a page that is in fact correctly translated. Decoding is what makes the
        // assertion about the language rather than about the encoder.
        return System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
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
