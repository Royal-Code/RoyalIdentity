namespace RoyalIdentity.Users;

/// <summary>
/// Input to <see cref="Defaults.LoginFlowService"/>: the local credentials and the optional returnUrl that
/// carries the pending authorization request.
/// </summary>
public sealed record LoginRequest(string Login, string Password, string? ReturnUrl, bool RememberLogin);

/// <summary>
/// The routing outcome of a login attempt. The borda (<see cref="Defaults.LoginFlowService"/>) decides the
/// outcome; the UI adapter only translates it into a redirect/render.
/// </summary>
public enum LoginFlowOutcome
{
    /// <summary>Authentication failed (generic message for the UI; specific reason goes to the event).</summary>
    Error,

    /// <summary>Signed in, but the client/request needs consent.</summary>
    RequiresConsent,

    /// <summary>Signed in; resume the OIDC authorize callback at the returnUrl.</summary>
    Callback,

    /// <summary>Signed in; the client uses a non-http redirect, so render the signed-in page.</summary>
    SignedInPage,

    /// <summary>Signed in with no pending request and no returnUrl; go to the profile page.</summary>
    Profile,

    /// <summary>Signed in with no pending request; redirect to a local returnUrl.</summary>
    LocalRedirect,

    /// <summary>The returnUrl is an absolute, non-loopback URL with no matching request (open-redirect guard).</summary>
    InvalidReturnUrl
}

/// <summary>
/// The result of <see cref="Defaults.LoginFlowService.LoginAsync"/>: the <see cref="Outcome"/> plus the
/// returnUrl/message the UI needs to render the next step. Carries no <c>Subject</c>/<c>UserSession</c>/
/// <c>ClaimsPrincipal</c> — the cookie is already written by the time this returns.
/// </summary>
public sealed record LoginFlowResult(LoginFlowOutcome Outcome, string? ReturnUrl = null, string? ErrorMessage = null);
