using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts;
using RoyalIdentity.Events;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Users.Defaults;

/// <summary>
/// Borda application service that orchestrates local login (ADR-014 §2.10): authenticate
/// (<see cref="ILocalUserAuthenticator"/>) → start the session (<see cref="IUserSessionService"/>) → build
/// the minimal principal (<see cref="ISubjectPrincipalFactory"/>) → write the cookie → decide the routing
/// <see cref="LoginFlowOutcome"/>. The <b>session is created here, at sign-in</b> — not as a side-effect of
/// credential verification. The UI knows none of <c>Subject</c>/<c>UserSession</c>/<c>ClaimsPrincipal</c>/
/// cookie; it only maps the returned outcome to a redirect/render.
/// </summary>
public sealed class LoginFlowService(
    IUserDirectory userDirectory,
    IUserSessionService userSessionService,
    ISubjectPrincipalFactory subjectPrincipalFactory,
    IConsentService consentService,
    IAuthorizationContextResolver authorizationContextResolver,
    ICurrentRealmAccessor realmAccessor,
    IEventDispatcher eventDispatcher,
    IHttpContextAccessor httpContextAccessor,
    ILogger<LoginFlowService> logger)
{
    public async Task<LoginFlowResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var context = await authorizationContextResolver.ResolveAsync(request.ReturnUrl, ct);

        var realm = context?.Client.Realm
            ?? (realmAccessor.TryGetCurrentRealm(out var current) ? current : null);
        if (realm is null)
        {
            logger.LogWarning("Login attempted without a realm context");
            return new LoginFlowResult(LoginFlowOutcome.Error, ErrorMessage: "No realm context.");
        }

        var authenticator = userDirectory.GetLocalAuthenticator(realm);
        var authResult = await authenticator.AuthenticateLocalAsync(request.Login, request.Password, ct);

        // The credential is valid but a required action gates completion (ADR-017 §2.3): issue no SSO session,
        // cookie or token until it is satisfied. The challenge token (Fase 5) and the UI live elsewhere (Q12).
        if (authResult.RequiredAction is { } requiredAction)
        {
            logger.LogInformation(
                "Login for {Login} requires action {Action} before completing",
                request.Login, requiredAction.Type);

            var outcome = requiredAction.Type switch
            {
                RequiredActionType.ChangePassword => LoginFlowOutcome.RequiresPasswordChange,
                _ => LoginFlowOutcome.RequiresPasswordChange,
            };

            return new LoginFlowResult(outcome, request.ReturnUrl);
        }

        if (!authResult.Success)
        {
            var message = ErrorMessageFor(realm, authResult.Reason);
            await eventDispatcher.DispatchAsync(
                new UserLoginFailureEvent(request.Login, message, authResult.Reason, context),
                realm);
            return new LoginFlowResult(LoginFlowOutcome.Error, ErrorMessage: message);
        }

        var subject = authResult.Subject!;

        // session is created here (sign-in), not during credential verification.
        var session = await userSessionService.StartAsync(
            subject, Oidc.AuthMethods.Password, Server.LocalIdentityProvider, ct);

        var principal = subjectPrincipalFactory.Create(subject, session);
        await WriteCookieAsync(realm, principal, request.RememberLogin);

        await eventDispatcher.DispatchAsync(new UserLoginSuccessEvent(request.Login, subject.SubjectId, context), realm);

        logger.LogInformation("User logged in: {SubjectId}, Session id: {SessionId}", subject.SubjectId, session.Id);

        return await Route(context, request.ReturnUrl, principal, ct);
    }

    private async Task WriteCookieAsync(Realm realm, System.Security.Claims.ClaimsPrincipal principal, bool remember)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext is required to write the authentication cookie.");

        var accountOptions = realm.Options.Account;

        AuthenticationProperties? props = null;
        if (remember && accountOptions.AllowRememberLogin)
        {
            props = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.Add(accountOptions.RememberMeLoginDuration)
            };
        }

        var sid = principal.FindFirst(JwtRegisteredClaimNames.Sid)
            ?? throw new InvalidOperationException("SessionId claim is required, but it is not present in the principal");

        props ??= new AuthenticationProperties();
        props.Items[JwtRegisteredClaimNames.Sid] = sid.Value;

        var scheme = httpContext.GetRealmAuthenticationScheme();
        await httpContext.SignInAsync(scheme, principal, props);
    }

    private async Task<LoginFlowResult> Route(
        Contexts.AuthorizationContext? context,
        string? returnUrl,
        System.Security.Claims.ClaimsPrincipal principal,
        CancellationToken ct)
    {
        if (context is not null)
        {
            var consented = await consentService.ValidateConsentAsync(principal, context.Client, context.Resources, ct);
            if (!consented)
                return new LoginFlowResult(LoginFlowOutcome.RequiresConsent, returnUrl);

            return context.RedirectUri.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? new LoginFlowResult(LoginFlowOutcome.Callback, returnUrl)
                : new LoginFlowResult(LoginFlowOutcome.SignedInPage, returnUrl);
        }

        if (returnUrl.IsMissing())
            return new LoginFlowResult(LoginFlowOutcome.Profile);

        var uri = new Uri(returnUrl!, UriKind.RelativeOrAbsolute);
        if (uri is { IsAbsoluteUri: true, IsLoopback: false })
            return new LoginFlowResult(LoginFlowOutcome.InvalidReturnUrl, returnUrl, $"No consent request matching request: {uri}");

        return new LoginFlowResult(LoginFlowOutcome.LocalRedirect, returnUrl);
    }

    private static string ErrorMessageFor(Realm realm, AuthenticationFailureReason? reason)
    {
        var account = realm.Options.Account;
        return reason switch
        {
            AuthenticationFailureReason.Inactive => account.InactiveUserErrorMessage,
            AuthenticationFailureReason.Blocked => account.BlockedUserErrorMessage,
            _ => account.InvalidCredentialsErrorMessage
        };
    }
}
