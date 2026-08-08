using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Security.Encoding;
using Tests.Integration.Prepare;

namespace Tests.Integration.Users;

public class CheckSessionCookieLifecycleTests : IClassFixture<LogCapturingAppFactory>, IDisposable
{
    private readonly LogCapturingAppFactory factory;

    public CheckSessionCookieLifecycleTests(LogCapturingAppFactory factory)
    {
        this.factory = factory;
        factory.ClearLog();
    }

    [Fact]
    public async Task Login_WritesTheSameOpaqueStateToTheProtectedTicketAndBrowserCookie()
    {
        var client = CreateRawClient();

        var response = await PostLoginAsync(client, factory.Handles.Demo, factory.Handles.Alice);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authenticationCookie = RequireCookie(response, AuthenticationCookieName(factory.Handles.Demo));
        var checkSessionCookie = RequireCookie(response, CheckSessionCookieName(factory.Handles.Demo));
        var ticket = Unprotect(factory.Handles.Demo, authenticationCookie.Value.ToString());
        Assert.Contains(ticket.Properties.Items, item => item.Value == checkSessionCookie.Value.ToString());
        Assert.Equal($"/{factory.Handles.Demo.Path}", checkSessionCookie.Path.ToString());
        Assert.True(checkSessionCookie.Secure);
        Assert.False(checkSessionCookie.HttpOnly);
        Assert.Empty(checkSessionCookie.Domain.ToString());
        Assert.Contains("samesite=none", checkSessionCookie.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(checkSessionCookie.Value.ToString(), factory.AllLogText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProtectedTicketState_FlowsThroughTheRequestAndProducesTheAdvertisedSessionState()
    {
        var client = CreateRawClient();
        var login = await PostLoginAsync(client, factory.Handles.Demo, factory.Handles.Alice);
        var userAgentState = RequireCookie(login, CheckSessionCookieName(factory.Handles.Demo)).Value.ToString();
        const string redirectUri = "https://localhost:5000/callback";
        var authorizePath = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", factory.Handles.DemoClient.ClientId)
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid")
            .AddQueryString("redirect_uri", redirectUri)
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        var response = await SendAsync(
            client,
            HttpMethod.Get,
            authorizePath,
            RequestCookieHeader(login));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        var sessionState = HttpUtility.ParseQueryString(location.Query)["session_state"];
        Assert.NotNull(sessionState);
        var segments = sessionState.Split('.');
        Assert.Equal(4, segments.Length);
        Assert.Equal("v1", segments[0]);
        var origin = Encoding.UTF8.GetString(Base64Url.Decode(segments[1]));
        var salt = Base64Url.Decode(segments[3]);
        var expectedHash = SHA256.HashData(CreateCanonicalSessionStateInput(
            factory.Handles.DemoClient.ClientId,
            origin,
            userAgentState,
            salt));
        Assert.Equal(Base64Url.Encode(expectedHash), segments[2]);
    }

    [Fact]
    public async Task RepeatedLoginAndUserChange_EachCreateANewBrowserState()
    {
        var client = CreateRawClient();
        var first = await PostLoginAsync(client, factory.Handles.Demo, factory.Handles.Alice);
        var firstState = RequireCookie(first, CheckSessionCookieName(factory.Handles.Demo)).Value.ToString();
        var firstCookies = RequestCookieHeader(first);

        var repeated = await PostLoginAsync(client, factory.Handles.Demo, factory.Handles.Alice, firstCookies);
        var repeatedState = RequireCookie(repeated, CheckSessionCookieName(factory.Handles.Demo)).Value.ToString();
        var repeatedCookies = RequestCookieHeader(repeated);

        var changedUser = await PostLoginAsync(client, factory.Handles.Demo, factory.Handles.Bob, repeatedCookies);
        var changedUserState = RequireCookie(changedUser, CheckSessionCookieName(factory.Handles.Demo)).Value.ToString();

        Assert.NotEqual(firstState, repeatedState);
        Assert.NotEqual(repeatedState, changedUserState);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-the-protected-state")]
    public async Task ValidTicket_RepairsAnAbsentOrDivergentBrowserCookie(string? browserState)
    {
        var client = CreateRawClient();
        var login = await PostLoginAsync(client, factory.Handles.Demo, factory.Handles.Alice);
        var authenticationCookie = RequireCookie(login, AuthenticationCookieName(factory.Handles.Demo));
        var expectedState = RequireCookie(login, CheckSessionCookieName(factory.Handles.Demo)).Value.ToString();
        var requestCookies = CookiePair(authenticationCookie);
        if (browserState is not null)
            requestCookies += $"; {CheckSessionCookieName(factory.Handles.Demo)}={browserState}";

        var response = await SendAsync(
            client,
            HttpMethod.Get,
            $"{factory.Handles.Demo.Path}/test/account/profile",
            requestCookies);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var repaired = RequireCookie(response, CheckSessionCookieName(factory.Handles.Demo));
        Assert.Equal(expectedState, repaired.Value.ToString());
    }

    [Fact]
    public async Task TicketWithoutState_IsRenewedAndBecomesTheOnlySourceForTheBrowserCookie()
    {
        var client = CreateRawClient();
        var login = await PostLoginAsync(client, factory.Handles.Demo, factory.Handles.Alice);
        var authenticationCookie = RequireCookie(login, AuthenticationCookieName(factory.Handles.Demo));
        var previousState = RequireCookie(login, CheckSessionCookieName(factory.Handles.Demo)).Value.ToString();
        var ticket = Unprotect(factory.Handles.Demo, authenticationCookie.Value.ToString());
        var stateProperty = Assert.Single(ticket.Properties.Items, item => item.Value == previousState);
        ticket.Properties.Items.Remove(stateProperty.Key);
        var legacyCookie = Protect(factory.Handles.Demo, ticket);
        var requestCookies = $"{AuthenticationCookieName(factory.Handles.Demo)}={legacyCookie}; " +
            $"{CheckSessionCookieName(factory.Handles.Demo)}={previousState}";

        var response = await SendAsync(
            client,
            HttpMethod.Get,
            $"{factory.Handles.Demo.Path}/test/account/profile",
            requestCookies);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var renewedAuthentication = RequireCookie(response, AuthenticationCookieName(factory.Handles.Demo));
        var renewedCheckSession = RequireCookie(response, CheckSessionCookieName(factory.Handles.Demo));
        var renewedTicket = Unprotect(factory.Handles.Demo, renewedAuthentication.Value.ToString());
        var renewedState = renewedCheckSession.Value.ToString();
        Assert.NotEqual(previousState, renewedState);
        Assert.Contains(renewedTicket.Properties.Items, item => item.Value == renewedState);
    }

    [Fact]
    public async Task SlidingRenewal_PreservesTheProtectedStateWithoutReissuingAMatchingBrowserCookie()
    {
        var client = CreateRawClient();
        var login = await PostLoginAsync(client, factory.Handles.Demo, factory.Handles.Alice);
        var authenticationCookie = RequireCookie(login, AuthenticationCookieName(factory.Handles.Demo));
        var stateCookie = RequireCookie(login, CheckSessionCookieName(factory.Handles.Demo));
        var ticket = Unprotect(factory.Handles.Demo, authenticationCookie.Value.ToString());
        ticket.Properties.IssuedUtc = DateTimeOffset.UtcNow.AddMinutes(-40);
        ticket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(20);
        var slidingCookie = Protect(factory.Handles.Demo, ticket);
        var requestCookies = $"{AuthenticationCookieName(factory.Handles.Demo)}={slidingCookie}; " +
            CookiePair(stateCookie);

        var response = await SendAsync(
            client,
            HttpMethod.Get,
            $"{factory.Handles.Demo.Path}/test/account/profile",
            requestCookies);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var renewedAuthentication = RequireCookie(response, AuthenticationCookieName(factory.Handles.Demo));
        var renewedTicket = Unprotect(factory.Handles.Demo, renewedAuthentication.Value.ToString());
        Assert.Contains(renewedTicket.Properties.Items, item => item.Value == stateCookie.Value.ToString());
        Assert.DoesNotContain(
            ReadCookies(response),
            cookie => cookie.Name.ToString() == CheckSessionCookieName(factory.Handles.Demo));
    }

    [Fact]
    public async Task RejectedSession_DeletesTheBrowserCookie()
    {
        var subject = await CharacterizationSeed.SeedUserAsync(factory, factory.Handles.Demo);
        var client = CreateRawClient();
        var login = await PostLoginAsync(client, factory.Handles.Demo, subject);
        var cookies = RequestCookieHeader(login);
        var session = await CharacterizationSeed.FindSessionAsync(factory, factory.Handles.Demo, subject);
        Assert.NotNull(session);
        await EndSessionAsync(factory.Handles.Demo, session.Id);

        var response = await SendAsync(
            client,
            HttpMethod.Get,
            $"{factory.Handles.Demo.Path}/test/account/profile",
            cookies);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        AssertDeleted(response, CheckSessionCookieName(factory.Handles.Demo), $"/{factory.Handles.Demo.Path}");
    }

    [Fact]
    public async Task Logout_DeletesTheBrowserCookieUsingItsIssuingNameAndPath()
    {
        var client = CreateRawClient();
        var login = await PostLoginAsync(client, factory.Handles.Demo, factory.Handles.Alice);

        var response = await SendAsync(
            client,
            HttpMethod.Get,
            $"{factory.Handles.Demo.Path}/test/account/logout",
            RequestCookieHeader(login));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertDeleted(response, CheckSessionCookieName(factory.Handles.Demo), $"/{factory.Handles.Demo.Path}");
    }

    [Fact]
    public async Task DisabledFeature_RemovesResidualStateAndDoesNotWriteANewBrowserCookie()
    {
        var client = CreateRawClient();
        var login = await PostLoginAsync(client, factory.Handles.Demo, factory.Handles.Alice);
        var cookies = RequestCookieHeader(login);
        await factory.UpdateRealmAsync(
            factory.Handles.Demo,
            options => options.Endpoints.EnableCheckSessionEndpoint = false);

        try
        {
            var response = await SendAsync(
                client,
                HttpMethod.Get,
                $"{factory.Handles.Demo.Path}/test/account/profile",
                cookies);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertDeleted(response, CheckSessionCookieName(factory.Handles.Demo), $"/{factory.Handles.Demo.Path}");
            var renewedAuthentication = RequireCookie(response, AuthenticationCookieName(factory.Handles.Demo));
            var renewedTicket = Unprotect(factory.Handles.Demo, renewedAuthentication.Value.ToString());
            Assert.DoesNotContain(renewedTicket.Properties.Items, item => item.Value ==
                RequireCookie(login, CheckSessionCookieName(factory.Handles.Demo)).Value.ToString());

            var newLogin = await PostLoginAsync(client, factory.Handles.Demo, factory.Handles.Bob, cookies);
            Assert.DoesNotContain(
                ReadCookies(newLogin),
                cookie => cookie.Name.ToString() == CheckSessionCookieName(factory.Handles.Demo)
                    && !IsDeletion(cookie));
        }
        finally
        {
            await factory.UpdateRealmAsync(
                factory.Handles.Demo,
                options => options.Endpoints.EnableCheckSessionEndpoint = true);
        }
    }

    [Fact]
    public async Task TwoRealms_UseDifferentCookieNamesPathsAndProtectedStates()
    {
        var secondRealm = await CreateRealmAsync();
        var secondHandle = new TestRealmHandle(secondRealm.Id, secondRealm.Path);
        var secondSubject = await CharacterizationSeed.SeedUserAsync(factory, secondHandle);
        var client = CreateRawClient();

        var firstLogin = await PostLoginAsync(client, factory.Handles.Demo, factory.Handles.Alice);
        var secondLogin = await PostLoginAsync(client, secondHandle, secondSubject);
        var firstState = RequireCookie(firstLogin, CheckSessionCookieName(factory.Handles.Demo));
        var secondState = RequireCookie(secondLogin, CheckSessionCookieName(secondHandle));

        Assert.NotEqual(firstState.Name.ToString(), secondState.Name.ToString());
        Assert.NotEqual(firstState.Value.ToString(), secondState.Value.ToString());
        Assert.Equal($"/{factory.Handles.Demo.Path}", firstState.Path.ToString());
        Assert.Equal($"/{secondHandle.Path}", secondState.Path.ToString());

        var foreignRequest = await SendAsync(
            client,
            HttpMethod.Get,
            $"{secondHandle.Path}/test/account/profile",
            RequestCookieHeader(firstLogin));
        Assert.Equal(HttpStatusCode.Redirect, foreignRequest.StatusCode);
    }

    public void Dispose() => factory.ClearLog();

    private HttpClient CreateRawClient()
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

    private static Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        TestRealmHandle realm,
        TestSubjectHandle subject,
        string? cookies = null)
        => SendAsync(
            client,
            HttpMethod.Post,
            $"{realm.Path}/test/account/login",
            cookies,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = subject.Username,
                ["password"] = subject.Password,
            }));

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string? cookies,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        if (cookies is not null)
            request.Headers.TryAddWithoutValidation(HeaderNames.Cookie, cookies);
        return await client.SendAsync(request);
    }

    private AuthenticationTicket Unprotect(TestRealmHandle realm, string value)
        => CookieOptions(realm).TicketDataFormat.Unprotect(value)
            ?? throw new InvalidOperationException("The authentication cookie could not be unprotected.");

    private string Protect(TestRealmHandle realm, AuthenticationTicket ticket)
        => CookieOptions(realm).TicketDataFormat.Protect(ticket);

    private CookieAuthenticationOptions CookieOptions(TestRealmHandle realm)
        => factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get($"{Constants.Server.RealmAuthenticationNamePrefix}{realm.Path}");

    private async Task EndSessionAsync(TestRealmHandle realmHandle, string sessionId)
    {
        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var realm = await factory.LoadRealmAsync(realmHandle);
        await storage.GetUserSessionStore(realm).EndAsync(sessionId, default);
    }

    private async Task<RoyalIdentity.Models.Realm> CreateRealmAsync()
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var suffix = CryptoRandom.CreateUniqueId(6);
        var path = $"check-session-{suffix}";
        var realm = await manager.CreateAsync(path, $"{path}.test", $"Check Session {suffix}");
        factory.Resources.SetResourceServer(
            realm.Id,
            TestConfigurationResourceSource.CreateDemoResourceServer());
        await factory.RefreshConfigurationAsync();
        return realm;
    }

    private static string AuthenticationCookieName(TestRealmHandle realm)
        => $"{Constants.Server.DefaultCookieName}.{realm.Path}";

    private static string CheckSessionCookieName(TestRealmHandle realm)
        => $"{Constants.Server.DefaultCheckSessionCookieName}.{realm.Path}";

    private static IReadOnlyList<SetCookieHeaderValue> ReadCookies(HttpResponseMessage response)
        => response.Headers.TryGetValues(HeaderNames.SetCookie, out var values)
            ? values.Select(value => SetCookieHeaderValue.Parse(value)).ToArray()
            : [];

    private static SetCookieHeaderValue RequireCookie(HttpResponseMessage response, string name)
        => Assert.Single(ReadCookies(response), cookie => cookie.Name.ToString() == name);

    private static string RequestCookieHeader(HttpResponseMessage response)
        => string.Join("; ", ReadCookies(response)
            .Where(cookie => !IsDeletion(cookie))
            .Select(CookiePair));

    private static string CookiePair(SetCookieHeaderValue cookie)
        => $"{cookie.Name}={cookie.Value}";

    private static bool IsDeletion(SetCookieHeaderValue cookie)
        => cookie.MaxAge <= TimeSpan.Zero
            || cookie.Expires <= DateTimeOffset.UnixEpoch
            || cookie.Value.ToString().Length == 0;

    private static void AssertDeleted(HttpResponseMessage response, string name, string path)
    {
        var cookie = Assert.Single(
            ReadCookies(response),
            candidate => candidate.Name.ToString() == name && IsDeletion(candidate));
        Assert.Equal(path, cookie.Path.ToString());
    }

    private static byte[] CreateCanonicalSessionStateInput(
        string clientId,
        string origin,
        string userAgentState,
        byte[] salt)
    {
        using var stream = new MemoryStream();
        AppendField(stream, Encoding.UTF8.GetBytes("v1"));
        AppendField(stream, Encoding.UTF8.GetBytes(clientId));
        AppendField(stream, Encoding.UTF8.GetBytes(origin));
        AppendField(stream, Encoding.UTF8.GetBytes(userAgentState));
        AppendField(stream, salt);
        return stream.ToArray();
    }

    private static void AppendField(Stream stream, ReadOnlySpan<byte> value)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        stream.Write(length);
        stream.Write(value);
    }
}
