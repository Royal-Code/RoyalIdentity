using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Authentication;
using RoyalIdentity.Contracts.Defaults;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Security.Encoding;
using AuthenticationProperties = Microsoft.AspNetCore.Authentication.AuthenticationProperties;

namespace Tests.Identity.Authentication;

public class SessionStateFormatTests
{
    private const string ClientId = "client-A";
    private const string Origin = "https://rp.example:8443";
    private const string UserAgentState = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private static readonly byte[] Salt = [
        0, 1, 2, 3, 4, 5, 6, 7,
        8, 9, 10, 11, 12, 13, 14, 15,
        16, 17, 18, 19, 20, 21, 22, 23,
        24, 25, 26, 27, 28, 29, 30, 31,
    ];

    [Fact]
    public void Format_MatchesTheSharedBigEndianLengthPrefixedVector()
    {
        var value = SessionStateFormat.Create(ClientId, Origin, UserAgentState, Salt);

        Assert.Equal(
            "v1.aHR0cHM6Ly9ycC5leGFtcGxlOjg0NDM.mHupvWilxn_qkn5x14iGwYrG-oMEhuMs-2Lyehagd-A." +
            "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8",
            value);
        Assert.DoesNotContain(' ', value);
    }

    [Fact]
    public void Parser_RoundTripsTheOriginHashAndSalt()
    {
        var value = SessionStateFormat.Create(ClientId, Origin, UserAgentState, Salt);

        var success = SessionStateFormat.TryParse(value, out var parsed);

        Assert.True(success);
        Assert.NotNull(parsed);
        Assert.Equal(Origin, parsed.Origin);
        Assert.Equal(Salt, parsed.Salt);
        Assert.Equal(SessionStateFormat.HashSize, parsed.Hash.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v2.a.b.c")]
    [InlineData("v1.a.b")]
    [InlineData("v1.a.b.c.d")]
    [InlineData("v1.a b.c.d")]
    [InlineData("v1.!.b.c")]
    public void Parser_RejectsInvalidEnvelopeShape(string? value)
    {
        Assert.False(SessionStateFormat.TryParse(value, out _));
    }

    [Fact]
    public void Parser_RejectsPaddedBase64UrlEvenWhenTheDecoderAcceptsIt()
    {
        var value = SessionStateFormat.Create(ClientId, Origin, UserAgentState, Salt);
        var segments = value.Split('.');
        segments[1] += "=";

        Assert.False(SessionStateFormat.TryParse(string.Join('.', segments), out _));
    }

    [Fact]
    public void Parser_RejectsAnOriginThatIsNotCanonical()
    {
        var canonical = SessionStateFormat.Create(ClientId, Origin, UserAgentState, Salt);
        var segments = canonical.Split('.');
        segments[1] = Base64Url.Encode(System.Text.Encoding.UTF8.GetBytes("https://RP.EXAMPLE:8443"));

        Assert.False(SessionStateFormat.TryParse(string.Join('.', segments), out _));
    }

    [Fact]
    public void Origin_IsDerivedFromTheRedirectUriUsingBrowserOriginRules()
    {
        Assert.Equal("https://rp.example", SessionStateFormat.GetOrigin("https://RP.EXAMPLE:443/callback?q=1"));
        Assert.Equal("http://rp.example:8080", SessionStateFormat.GetOrigin("http://rp.example:8080/callback"));
        Assert.Equal(
            "https://xn--exmple-xta.test:8443",
            SessionStateFormat.GetOrigin("https://exâmple.test:8443/callback"));
        Assert.Equal(
            "https://[2001:db8::1]:8443",
            SessionStateFormat.GetOrigin("https://[2001:db8::1]:8443/callback"));
    }

    [Theory]
    [InlineData("urn:example:callback")]
    [InlineData("com.example.app://callback")]
    [InlineData("urn:ietf:wg:oauth:2.0:oob")]
    [InlineData("/relative")]
    [InlineData("https:///callback")]
    public void Origin_RejectsValuesWithoutAnHttpBrowserOrigin(string value)
    {
        Assert.Throws<ArgumentException>(() => SessionStateFormat.GetOrigin(value));
        Assert.False(SessionStateFormat.TryGetOrigin(value, out _));
    }

    [Fact]
    public void CanonicalEncoding_DistinguishesChangesInSeparateFields()
    {
        var otherState = Base64Url.Encode(Enumerable.Repeat((byte)1, 32).ToArray());
        var first = SessionStateFormat.Create("client", Origin, UserAgentState, Salt);
        var second = SessionStateFormat.Create("client-A", Origin, otherState, Salt);

        Assert.NotEqual(first.Split('.')[2], second.Split('.')[2]);
    }

    [Fact]
    public void Format_RejectsAUserAgentStateWithLessThan256Bits()
    {
        var weakState = Base64Url.Encode(new byte[31]);

        Assert.Throws<ArgumentException>(() => SessionStateFormat.Create(ClientId, Origin, weakState, Salt));
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void GeneratorGate_RejectsEveryIneligibleRequest(
        bool endpointEnabled,
        bool isOpenId,
        bool redirectValidated)
    {
        Assert.False(DefaultSessionStateGenerator.ShouldGenerate(endpointEnabled, isOpenId, redirectValidated));
    }

    [Fact]
    public void GeneratorGate_AcceptsAnEligibleOpenIdRequest()
    {
        Assert.True(DefaultSessionStateGenerator.ShouldGenerate(true, true, true));
    }

    [Fact]
    public void Manager_GeneratesCanonicalIndependent256BitStates()
    {
        var manager = new CheckSessionStateManager();

        var first = manager.CreateState();
        var second = manager.CreateState();

        Assert.NotEqual(first, second);
        Assert.True(CheckSessionStateManager.IsValidState(first));
        Assert.True(Base64Url.TryDecode(first, out var bytes));
        Assert.Equal(CheckSessionStateManager.StateEntropyBytes, bytes.Length);
    }

    [Fact]
    public void Manager_UsesTheProtectedPropertyAsTheCanonicalTicketState()
    {
        var manager = new CheckSessionStateManager();
        var properties = new AuthenticationProperties();

        var first = manager.GetOrCreateState(properties);
        var second = manager.GetOrCreateState(properties);

        Assert.Equal(first, second);
        Assert.Equal(first, properties.Items[Constants.Server.CheckSessionStateAuthenticationProperty]);
    }

    [Fact]
    public void Manager_PublishesOneCanonicalStateForTheRequest()
    {
        var manager = new CheckSessionStateManager();
        var context = new DefaultHttpContext();

        var first = manager.GetOrCreateRequestState(context);
        var second = manager.GetOrCreateRequestState(context);

        Assert.Equal(first, second);
        Assert.True(CheckSessionStateManager.TryGetRequestState(context, out var published));
        Assert.Equal(first, published);
    }

    [Fact]
    public void CookieIdentity_IsQualifiedAndScopedByRealm()
    {
        var options = new AuthenticationOptions { CheckSessionCookieName = ".test.session" };
        var realm = CreateRealm("tenant-a", options);

        Assert.Equal(".test.session.tenant-a", CheckSessionStateManager.GetCookieName(options, realm));
        Assert.Equal("/tenant-a", CheckSessionStateManager.GetCookiePath(realm));

        var cookie = CheckSessionStateManager.CreateCookieOptions(realm);
        Assert.Null(cookie.Domain);
        Assert.Equal("/tenant-a", cookie.Path);
        Assert.True(cookie.Secure);
        Assert.Equal(SameSiteMode.None, cookie.SameSite);
        Assert.False(cookie.HttpOnly);
        Assert.True(cookie.IsEssential);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("check/session")]
    [InlineData("check;session")]
    [InlineData("check\nsession")]
    [InlineData("sessão")]
    public void Options_RejectInvalidCheckSessionCookieBaseNames(string name)
    {
        var options = new AuthenticationOptions { CheckSessionCookieName = name };

        Assert.Contains(
            options.Validate(),
            error => error.Contains("CheckSessionCookieName", StringComparison.Ordinal));
    }

    [Fact]
    public void Options_RejectThePredictableAuthenticationCookieCollision()
    {
        var options = new AuthenticationOptions
        {
            CookieName = ".same",
            CheckSessionCookieName = ".same",
        };

        Assert.Contains(options.Validate(), error => error.Contains("collision", StringComparison.Ordinal));
    }

    [Fact]
    public void Manager_IsRegisteredAsScoped()
    {
        var services = new ServiceCollection();

        services.AddOpenIdConnectProviderServices();

        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(CheckSessionStateManager));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    private static Realm CreateRealm(string path, AuthenticationOptions authentication)
    {
        var serverOptions = new ServerOptions { Authentication = authentication };
        return new Realm("realm-id", "realm.example", path, "Realm", false, new RealmOptions(serverOptions));
    }
}
