using RoyalIdentity.Contracts.Defaults;
using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace Tests.Identity.Validators;

public class RedirectUriValidationTests
{
    private readonly DefaultRedirectUriValidator validator = new();

    [Fact]
    public void Defaults_AreExactOrdinalAndWildcardDisabled()
    {
        var options = new RedirectUriValidationOptions();

        Assert.Equal(RedirectUriComparison.Ordinal, options.Comparison);
        Assert.False(options.AllowWildcard);
        Assert.Empty(options.Validate());
    }

    [Fact]
    public void Copy_IsIndependent()
    {
        var original = new RedirectUriValidationOptions
        {
            Comparison = RedirectUriComparison.OrdinalIgnoreCase,
            AllowWildcard = true,
        };

        var copy = new RedirectUriValidationOptions(original);
        copy.Comparison = RedirectUriComparison.Ordinal;
        copy.AllowWildcard = false;

        Assert.Equal(RedirectUriComparison.OrdinalIgnoreCase, original.Comparison);
        Assert.True(original.AllowWildcard);
    }

    [Fact]
    public void Validate_RejectsUnknownComparison()
    {
        var options = new RedirectUriValidationOptions
        {
            Comparison = (RedirectUriComparison)42,
        };

        Assert.Single(options.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("client/callback")]
    [InlineData("http://client.example/callback")]
    [InlineData("https://client.example/callback#fragment")]
    [InlineData("*:/client.example/callback")]
    [InlineData("https://*/**")]
    [InlineData("https://*.com/**")]
    public void RegisteredUriValidation_RejectsUnsafeOrOpenPatterns(string? registeredUri)
    {
        var errors = new RedirectUriValidationOptions().ValidateRegisteredUri(registeredUri);

        Assert.NotEmpty(errors);
    }

    [Theory]
    [InlineData("https://client.example/callback")]
    [InlineData("https://client.example/**")]
    [InlineData("https://client.example/*")]
    [InlineData("https://client.example:*/callback")]
    [InlineData("https://*.client.example/callback")]
    public void RegisteredUriValidation_AcceptsHttpsAndBoundedWildcardPatterns(string registeredUri)
    {
        Assert.Empty(new RedirectUriValidationOptions().ValidateRegisteredUri(registeredUri));
    }

    [Fact]
    public async Task ExactMatching_IsOrdinalByDefault()
    {
        var client = ClientWithRedirect("https://client.example/Callback");
        var options = new RedirectUriValidationOptions();

        Assert.True(await validator.IsRedirectUriValidAsync(
            "https://client.example/Callback", client, options, default));
        Assert.False(await validator.IsRedirectUriValidAsync(
            "https://client.example/callback", client, options, default));
    }

    [Fact]
    public async Task IgnoreCase_MustBeExplicit()
    {
        var client = ClientWithRedirect("https://client.example/Callback");
        var options = new RedirectUriValidationOptions
        {
            Comparison = RedirectUriComparison.OrdinalIgnoreCase,
        };

        Assert.True(await validator.IsRedirectUriValidAsync(
            "HTTPS://CLIENT.EXAMPLE/callback", client, options, default));
    }

    [Fact]
    public async Task Wildcard_MustBeExplicitAndRespectsTheComparisonMode()
    {
        var client = ClientWithRedirect("https://client.example/**");
        var options = new RedirectUriValidationOptions();

        Assert.False(await validator.IsRedirectUriValidAsync(
            "https://client.example/account/callback", client, options, default));

        options.AllowWildcard = true;
        Assert.True(await validator.IsRedirectUriValidAsync(
            "https://client.example/account/callback", client, options, default));
        Assert.False(await validator.IsRedirectUriValidAsync(
            "https://CLIENT.example/account/callback", client, options, default));

        options.Comparison = RedirectUriComparison.OrdinalIgnoreCase;
        Assert.True(await validator.IsRedirectUriValidAsync(
            "https://CLIENT.example/account/callback", client, options, default));
    }

    [Fact]
    public async Task Wildcard_TreatsEveryNonWildcardCharacterAsALiteral()
    {
        var client = ClientWithRedirect("https://client.example/callback?value=(safe)/**");
        var options = new RedirectUriValidationOptions { AllowWildcard = true };

        Assert.True(await validator.IsRedirectUriValidAsync(
            "https://client.example/callback?value=(safe)/next", client, options, default));
        Assert.False(await validator.IsRedirectUriValidAsync(
            "https://client.example/callback?value=safe/next", client, options, default));
    }

    [Theory]
    [InlineData("http://client.example/callback")]
    [InlineData("https://client.example/callback#fragment")]
    [InlineData("client/callback")]
    [InlineData("https://client.example/*")]
    public async Task RequestedUri_MustAlwaysBeAbsoluteFragmentFreeHttpsWithoutWildcards(string requestedUri)
    {
        var client = ClientWithRedirect(requestedUri);
        var options = new RedirectUriValidationOptions
        {
            Comparison = RedirectUriComparison.OrdinalIgnoreCase,
            AllowWildcard = true,
        };

        Assert.False(await validator.IsRedirectUriValidAsync(requestedUri, client, options, default));
    }

    [Fact]
    public async Task PostLogoutRedirect_UsesTheSamePolicy()
    {
        var client = new Client();
        client.PostLogoutRedirectUris.Add("https://client.example/logout/**");
        var options = new RedirectUriValidationOptions { AllowWildcard = true };

        Assert.True(await validator.IsPostLogoutRedirectUriValidAsync(
            "https://client.example/logout/callback", client, options, default));
        Assert.False(await validator.IsPostLogoutRedirectUriValidAsync(
            "http://client.example/logout/callback", client, options, default));
    }

    [Fact]
    public async Task Cancellation_IsObservedBeforeMatching()
    {
        var client = ClientWithRedirect("https://client.example/callback");
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await validator.IsRedirectUriValidAsync(
                "https://client.example/callback",
                client,
                new RedirectUriValidationOptions(),
                source.Token));
    }

    private static Client ClientWithRedirect(string redirectUri)
    {
        var client = new Client();
        client.RedirectUris.Add(redirectUri);
        return client;
    }
}
