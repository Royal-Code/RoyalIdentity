using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Playwright;
using Tests.Integration.Prepare;

namespace Tests.Browser;

/// <summary>
/// Opt-in Chromium acceptance for OpenID Connect Session Management. This project is intentionally outside
/// <c>RoyalIdentity.sln</c>; run it only through <c>scripts/Test-CheckSessionBrowser.ps1</c>.
/// </summary>
public class CheckSessionBrowserTests : IClassFixture<CheckSessionBrowserFixture>
{
    private readonly CheckSessionBrowserFixture fixture;

    public CheckSessionBrowserTests(CheckSessionBrowserFixture fixture)
        => this.fixture = fixture;

    [Fact]
    public async Task CurrentStateAndUserSwitch_ProduceUnchangedChangedAndSilentRenewal()
    {
        var context = await CreateContextAsync();
        try
        {
            var page = await context.NewPageAsync();
            await LoginAsync(page, fixture.DemoRealm, fixture.Alice);
            var first = await AuthorizeAsync(page, fixture.DemoRealm, CheckSessionBrowserFixture.DemoClientId);

            Assert.Equal("unchanged", await CheckAsync(
                page,
                fixture.PrimaryRp,
                fixture.DemoRealm,
                CheckSessionBrowserFixture.DemoClientId,
                first["session_state"]));

            await LoginAsync(page, fixture.DemoRealm, fixture.Alice);
            Assert.Equal("changed", await CheckAsync(
                page,
                fixture.PrimaryRp,
                fixture.DemoRealm,
                CheckSessionBrowserFixture.DemoClientId,
                first["session_state"]));

            var renewed = await AuthorizeAsync(
                page,
                fixture.DemoRealm,
                CheckSessionBrowserFixture.DemoClientId,
                prompt: "none");
            Assert.False(string.IsNullOrEmpty(renewed["code"]));
            Assert.False(string.IsNullOrEmpty(renewed["session_state"]));
            Assert.NotEqual(first["session_state"], renewed["session_state"]);

            await LoginAsync(page, fixture.DemoRealm, fixture.Bob);
            Assert.Equal("changed", await CheckAsync(
                page,
                fixture.PrimaryRp,
                fixture.DemoRealm,
                CheckSessionBrowserFixture.DemoClientId,
                renewed["session_state"]));
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task MalformedClientOriginAndSource_AreHandledWithoutWildcardMessaging()
    {
        var context = await CreateContextAsync();
        try
        {
            var page = await context.NewPageAsync();
            await LoginAsync(page, fixture.DemoRealm, fixture.Alice);
            var response = await AuthorizeAsync(page, fixture.DemoRealm, CheckSessionBrowserFixture.DemoClientId);
            var state = response["session_state"];

            await OpenCheckPageAsync(
                page,
                fixture.PrimaryRp,
                fixture.DemoRealm,
                CheckSessionBrowserFixture.DemoClientId,
                state,
                auto: false);
            Assert.Equal(
                fixture.OpOrigin.GetLeftPart(UriPartial.Authority),
                await page.EvaluateAsync<string>("window.__checkSession.targetOrigin"));
            await page.EvaluateAsync("window.__checkSession.sendMalformed()");
            Assert.Equal("error", await WaitForStatusAsync(page, "error"));

            await OpenCheckPageAsync(
                page,
                fixture.PrimaryRp,
                fixture.DemoRealm,
                CheckSessionBrowserFixture.DemoClientId,
                state,
                auto: false);
            await page.EvaluateAsync(
                "([clientId, state]) => window.__checkSession.send(clientId, state)",
                new[] { "different-client", state });
            Assert.Equal("changed", await WaitForStatusAsync(page, "changed"));

            Assert.Equal("error", await CheckAsync(
                page,
                fixture.AlternateRp,
                fixture.DemoRealm,
                CheckSessionBrowserFixture.DemoClientId,
                state));

            await OpenCheckPageAsync(
                page,
                fixture.PrimaryRp,
                fixture.DemoRealm,
                CheckSessionBrowserFixture.DemoClientId,
                state,
                auto: false);
            await page.EvaluateAsync("window.__checkSession.sendFromSibling()");
            await page.WaitForTimeoutAsync(350);
            var ignored = await SnapshotAsync(page);
            Assert.Equal("pending", ignored.Status);
            Assert.Equal(0, ignored.ResponseCount);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task PromptNone_ReturnsLoginRequiredAndConsentRequiredWithoutUi()
    {
        var context = await CreateContextAsync();
        try
        {
            var page = await context.NewPageAsync();
            var anonymous = await AuthorizeAsync(
                page,
                fixture.DemoRealm,
                CheckSessionBrowserFixture.DemoClientId,
                prompt: "none");
            Assert.Equal("login_required", anonymous["error"]);
            Assert.False(anonymous.ContainsKey("code"));

            await LoginAsync(page, fixture.DemoRealm, fixture.Alice);
            var consent = await AuthorizeAsync(
                page,
                fixture.DemoRealm,
                CheckSessionBrowserFixture.ConsentClientId,
                prompt: "none");
            Assert.Equal("consent_required", consent["error"]);
            Assert.False(consent.ContainsKey("code"));
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task TwoRealms_KeepCookiesAndSessionStateIndependent()
    {
        var context = await CreateContextAsync();
        try
        {
            var page = await context.NewPageAsync();
            await LoginAsync(page, fixture.DemoRealm, fixture.Alice);
            var demo = await AuthorizeAsync(page, fixture.DemoRealm, CheckSessionBrowserFixture.DemoClientId);
            await LoginAsync(page, fixture.SecondRealm, fixture.Alice);
            var second = await AuthorizeAsync(page, fixture.SecondRealm, CheckSessionBrowserFixture.SecondClientId);
            var cookies = await context.CookiesAsync();
            var demoCookie = Assert.Single(cookies, cookie =>
                cookie.Name == $".roid.browser-demo.{fixture.DemoRealm.Path}");
            var secondCookie = Assert.Single(cookies, cookie =>
                cookie.Name == $".roid.browser-second.{fixture.SecondRealm.Path}");

            Assert.NotEqual(demo["session_state"], second["session_state"]);
            Assert.NotEqual(demoCookie.Value, secondCookie.Value);
            Assert.Equal($"/{fixture.DemoRealm.Path}", demoCookie.Path);
            Assert.Equal($"/{fixture.SecondRealm.Path}", secondCookie.Path);
            Assert.Equal("unchanged", await CheckAsync(
                page,
                fixture.PrimaryRp,
                fixture.DemoRealm,
                CheckSessionBrowserFixture.DemoClientId,
                demo["session_state"]));
            Assert.Equal("unchanged", await CheckAsync(
                page,
                fixture.PrimaryRp,
                fixture.SecondRealm,
                CheckSessionBrowserFixture.SecondClientId,
                second["session_state"]));

            await page.GotoAsync(new Uri(
                fixture.OpOrigin,
                $"/{fixture.DemoRealm.Path}/test/account/logout").ToString());
            Assert.Equal("changed", await CheckAsync(
                page,
                fixture.PrimaryRp,
                fixture.DemoRealm,
                CheckSessionBrowserFixture.DemoClientId,
                demo["session_state"]));
            Assert.Equal("unchanged", await CheckAsync(
                page,
                fixture.PrimaryRp,
                fixture.SecondRealm,
                CheckSessionBrowserFixture.SecondClientId,
                second["session_state"]));
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task UnavailableCookie_ReturnsChangedOnceWithoutRpLoop()
    {
        var context = await CreateContextAsync();
        try
        {
            var page = await context.NewPageAsync();
            await LoginAsync(page, fixture.DemoRealm, fixture.Alice);
            var response = await AuthorizeAsync(page, fixture.DemoRealm, CheckSessionBrowserFixture.DemoClientId);

            await context.AddInitScriptAsync(script:
                "Object.defineProperty(Document.prototype, 'cookie', "
                + "{ configurable: true, get: () => '', set: () => true });");
            Assert.Equal("changed", await CheckAsync(
                page,
                fixture.PrimaryRp,
                fixture.DemoRealm,
                CheckSessionBrowserFixture.DemoClientId,
                response["session_state"]));
            await page.WaitForTimeoutAsync(350);
            var snapshot = await SnapshotAsync(page);
            Assert.Equal("changed", snapshot.Status);
            Assert.Equal(1, snapshot.ResponseCount);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    private Task<IBrowserContext> CreateContextAsync()
        => fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
        });

    private async Task LoginAsync(IPage page, TestRealmHandle realm, TestSubjectHandle subject)
    {
        var expected = new Uri(
            fixture.OpOrigin,
            $"/{realm.Path}/test/account/login").ToString();
        await page.GotoAsync(fixture.PrimaryRp.BuildLoginUrl(
            fixture.OpOrigin,
            realm.Path,
            subject.Username,
            subject.Password));
        await page.WaitForURLAsync(expected);
        Assert.Equal(expected, page.Url);
    }

    private async Task<IReadOnlyDictionary<string, string>> AuthorizeAsync(
        IPage page,
        TestRealmHandle realm,
        string clientId,
        string? prompt = null)
    {
        await page.GotoAsync(fixture.BuildAuthorizeUrl(realm, clientId, fixture.PrimaryRp, prompt));
        await page.WaitForURLAsync(url => url.StartsWith(
            new Uri(fixture.PrimaryRp.Origin, "/callback").ToString(),
            StringComparison.Ordinal));

        var query = QueryHelpers.ParseQuery(new Uri(page.Url).Query);
        return query.ToDictionary(
            pair => pair.Key,
            pair => Value(pair.Value),
            StringComparer.Ordinal);
    }

    private async Task<string> CheckAsync(
        IPage page,
        BrowserRpHost rp,
        TestRealmHandle realm,
        string clientId,
        string state)
    {
        await OpenCheckPageAsync(page, rp, realm, clientId, state, auto: true);
        return await WaitForStatusAsync(page, "unchanged", "changed", "error");
    }

    private async Task OpenCheckPageAsync(
        IPage page,
        BrowserRpHost rp,
        TestRealmHandle realm,
        string clientId,
        string state,
        bool auto)
    {
        await page.GotoAsync(rp.BuildCheckUrl(fixture.OpOrigin, realm.Path, clientId, state, auto));
        await page.WaitForFunctionAsync("document.body.dataset.ready === 'true'");
    }

    private static async Task<string> WaitForStatusAsync(IPage page, params string[] expected)
    {
        await page.WaitForFunctionAsync(
            "expected => expected.includes(document.getElementById('status').textContent)",
            expected);
        return (await page.Locator("#status").TextContentAsync())!;
    }

    private static Task<CheckSnapshot> SnapshotAsync(IPage page)
        => page.EvaluateAsync<CheckSnapshot>("window.__checkSession.snapshot()");

    private static string Value(StringValues value)
        => value.Count == 0 ? string.Empty : value[^1]!;

    private sealed class CheckSnapshot
    {
        public string Status { get; init; } = string.Empty;

        public int ResponseCount { get; init; }
    }
}
