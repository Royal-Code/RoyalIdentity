using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Razor.Localization;
using Tests.Integration.Prepare;

namespace Tests.Integration.UI;

/// <summary>
/// Fase 4 (plan-localization.md) — the error page distinguishes the two things
/// <see cref="ErrorMessage"/> can carry: a presentation code, resolved in the reader's culture, and a literal
/// description, printed exactly as it arrived (DF11).
/// </summary>
/// <remarks>
/// This is the regression for the defect an external review found: the page printed
/// <see cref="ErrorMessage.ErrorDescription"/> raw while Fase 4 had started storing resource keys in it, so a
/// user would read "Consent_RequestNotFound". It lives in a UI fixture, and in the phase's own gate, because a
/// regression nobody runs is not a regression.
/// </remarks>
public class ErrorPageLocalizationTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public ErrorPageLocalizationTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData("en", "The consent request was not found.")]
    [InlineData("pt-BR", "A solicitação de consentimento não foi encontrada.")]
    [InlineData("es-419", "No se encontró la solicitud de consentimiento.")]
    public async Task TheErrorPage_RendersAMessageCodeTranslated_NeverAsARawKey(string culture, string expected)
    {
        // This is the regression for the defect the review found: the page printed ErrorDescription raw, and
        // Fase 4 had started storing resource keys there, so users would read "Consent_RequestNotFound".
        var errorId = await WriteErrorAsync(new ErrorMessage
        {
            MessageCode = AccountUiMessages.ResourceKeys[AccountUiMessageCode.ConsentRequestNotFound],
        });

        var html = await ReadErrorPageAsync(errorId, culture);

        Assert.Contains(expected, html, StringComparison.Ordinal);
        Assert.DoesNotContain("Consent_RequestNotFound", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheErrorPage_KeepsALiteralDescriptionExactlyAsItArrived()
    {
        // Protocol descriptions are not translatable content; resolving every string as a resource would be
        // the mirror-image defect.
        const string literal = "The request is missing the response_type parameter.";
        var errorId = await WriteErrorAsync(new ErrorMessage
        {
            Error = "invalid_request",
            ErrorDescription = literal,
        });

        var html = await ReadErrorPageAsync(errorId, "pt-BR");

        Assert.Contains(literal, html, StringComparison.Ordinal);
        Assert.Contains("invalid_request", html, StringComparison.Ordinal);
    }

    private async Task<string> WriteErrorAsync(ErrorMessage error)
    {
        using var scope = factory.Services.CreateScope();
        var messageStore = scope.ServiceProvider.GetRequiredService<IMessageStore>();

        return await messageStore.WriteAsync(new Message<ErrorMessage>(error), default);
    }

    private async Task<string> ReadErrorPageAsync(string errorId, string culture)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.Add(
            new System.Net.Http.Headers.StringWithQualityHeaderValue(culture));

        var response = await client.GetAsync($"/error?errorId={errorId}");
        response.EnsureSuccessStatusCode();

        return System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private HttpClient CreateClient()
        => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
}
