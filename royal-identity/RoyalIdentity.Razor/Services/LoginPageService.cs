using Microsoft.AspNetCore.Authentication;
using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Razor.ViewModels;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Defaults;
using static RoyalIdentity.Options.Constants.UI;

namespace RoyalIdentity.Razor.Services;

public class LoginPageService(
    ISessionContextService sessionContext,
    LoginFlowService loginFlowService,
    IAuthenticationSchemeProvider schemeProvider,
    IMessageStore messageStore) : ILoginPageService
{
    public async Task<LoginViewModel?> BuildViewModelAsync(string? returnUrl, CancellationToken ct)
    {
        if (!sessionContext.TryGetCurrentRealm(out var realm))
            return null;

        var context = await sessionContext.GetAuthorizationContextAsync(returnUrl);
        var effectiveReturnUrl = returnUrl ?? Routes.BuildProfileUrl(realm.Path);

        var schemes = await schemeProvider.GetAllSchemesAsync();
        var providers = schemes
            .Where(x => x.Name != "RoyalIdentity")
            .Where(x => x.DisplayName != null)
            .Select(x => new ExternalProvider
            {
                DisplayName = x.DisplayName ?? x.Name,
                AuthenticationScheme = x.Name
            });

        var accountOptions = realm.Options.Account;
        var allowLocal = accountOptions.AllowLocalLogin;
        if (context is not null)
        {
            allowLocal = allowLocal && context.Client.EnableLocalLogin;

            if (context.Client.IdentityProviderRestrictions.Any())
            {
                providers = providers
                    .Where(p => context.Client.IdentityProviderRestrictions.Contains(p.AuthenticationScheme));
            }
        }

        return new LoginViewModel
        {
            AllowRememberLogin = accountOptions.AllowRememberLogin,
            EnableLocalLogin = allowLocal,
            ReturnUrl = effectiveReturnUrl,
            Username = context?.LoginHint,
            ExternalProviders = providers.ToArray()
        };
    }

    public async Task<LoginResult> LoginAsync(LoginInputModel input, CancellationToken ct)
    {
        // The borda (LoginFlowService) authenticates, starts the session, writes the cookie and decides the
        // routing outcome. This adapter only resolves the realm (for URL building) and maps the outcome to a
        // redirect/render — it knows nothing of Subject/UserSession/ClaimsPrincipal/cookie.
        if (!sessionContext.TryGetCurrentRealm(out var realm))
            return new LoginResult(LoginResultType.Error, Routes.SelectDomain);

        var result = await loginFlowService.LoginAsync(
            new LoginRequest(input.Username!, input.Password!, input.ReturnUrl, input.RememberLogin), ct);

        return result.Outcome switch
        {
            LoginFlowOutcome.Error =>
                new LoginResult(LoginResultType.Error, ErrorMessage: result.ErrorMessage),

            LoginFlowOutcome.RequiresConsent =>
                new LoginResult(LoginResultType.RequiresConsent, Routes.BuildConsentUrl(realm.Path, input.ReturnUrl)),

            LoginFlowOutcome.SignedInPage =>
                new LoginResult(LoginResultType.SignedInPage, Routes.BuildSignedInUrl(realm.Path, input.ReturnUrl)),

            LoginFlowOutcome.Callback =>
                new LoginResult(LoginResultType.Success, result.ReturnUrl),

            LoginFlowOutcome.LocalRedirect =>
                new LoginResult(LoginResultType.Success, result.ReturnUrl, ForceLoad: true),

            LoginFlowOutcome.Profile =>
                new LoginResult(LoginResultType.Success, Routes.BuildProfileUrl(realm.Path), ForceLoad: true),

            LoginFlowOutcome.InvalidReturnUrl =>
                await BuildErrorPageAsync(result.ErrorMessage, ct),

            _ => new LoginResult(LoginResultType.Error, ErrorMessage: result.ErrorMessage)
        };
    }

    private async Task<LoginResult> BuildErrorPageAsync(string? errorDescription, CancellationToken ct)
    {
        var error = new ErrorMessage { ErrorDescription = errorDescription };
        var errorId = await messageStore.WriteAsync(new Message<ErrorMessage>(error), ct);
        return new LoginResult(LoginResultType.Error, Routes.BuildErrorUrl(errorId), ForceLoad: true);
    }
}
