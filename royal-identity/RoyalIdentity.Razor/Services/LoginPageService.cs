using Microsoft.AspNetCore.Authentication;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Events;
using RoyalIdentity.Extensions;
using RoyalIdentity.Razor.ViewModels;
using RoyalIdentity.Users;
using static RoyalIdentity.Options.Constants.UI;

namespace RoyalIdentity.Razor.Services;

public class LoginPageService(
    ISessionContextService sessionContext,
    ISignInManager signInManager,
    IAuthenticationSchemeProvider schemeProvider,
    IEventDispatcher eventDispatcher,
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
        var context = await sessionContext.GetAuthorizationContextAsync(input.ReturnUrl);

        Models.Realm? realm;
        if (context is not null)
            realm = context.Client.Realm;
        else if (!sessionContext.TryGetCurrentRealm(out realm))
            return new LoginResult(LoginResultType.Error, Routes.SelectDomain);

        var authResult = await signInManager.AuthenticateUserAsync(realm, input.Username!, input.Password!, ct);

        if (!authResult.Success)
        {
            await eventDispatcher.DispatchAsync(new UserLoginFailureEvent(input.Username!, authResult.ErrorMessage, context));
            return new LoginResult(LoginResultType.Error, ErrorMessage: authResult.ErrorMessage);
        }

        await eventDispatcher.DispatchAsync(new UserLoginSuccessEvent(input.Username!, authResult.User, context));
        var user = await signInManager.SignInAsync(authResult.User, authResult.Session, input.RememberLogin, ct);

        if (context is not null)
        {
            if (await signInManager.ConsentRequired(user, context.Client, context.Resources, ct))
                return new LoginResult(LoginResultType.RequiresConsent, Routes.BuildConsentUrl(realm.Path, input.ReturnUrl));

            if (!context.RedirectUri.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return new LoginResult(LoginResultType.SignedInPage, Routes.BuildSignedInUrl(realm.Path, input.ReturnUrl));

            return new LoginResult(LoginResultType.Success, input.ReturnUrl);
        }

        if (input.ReturnUrl.IsMissing())
            return new LoginResult(LoginResultType.Success, Routes.BuildProfileUrl(realm.Path), ForceLoad: true);

        var uri = new Uri(input.ReturnUrl!, UriKind.RelativeOrAbsolute);

        if (uri is { IsAbsoluteUri: true, IsLoopback: false })
        {
            var error = new ErrorMessage { ErrorDescription = $"No consent request matching request: {uri}" };
            var errorId = await messageStore.WriteAsync(new Message<ErrorMessage>(error), ct);
            return new LoginResult(LoginResultType.Error, Routes.BuildErrorUrl(errorId), ForceLoad: true);
        }

        return new LoginResult(LoginResultType.Success, uri.ToString(), ForceLoad: true);
    }
}
