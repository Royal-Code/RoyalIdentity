using Microsoft.Extensions.Localization;
using RoyalIdentity.Users;

namespace RoyalIdentity.Razor.Localization;

/// <summary>
/// Presentation-only outcomes the account screens report (plan-localization DF11). Each maps to an
/// <see cref="AccountResources"/> key, so a redirect or a protected message carries a code — never a sentence
/// baked in one language.
/// </summary>
public enum AccountUiMessageCode
{
    LoginInvalidCredentials,
    LoginInvalidReturnUrl,
    ConsentRequestNotFound,
    ConsentRememberNotAllowed,
    ConsentRequiredScopeNotGranted,
    LogoutIdRequired,
    LogoutIdNotFound,
    DomainNotFound,
    ErrorGeneric,
}

/// <summary>
/// Resolves message codes to text in the request's culture. This is the last edge before rendering: nothing
/// upstream of it holds a presentable string.
/// </summary>
public static class AccountUiMessages
{
    /// <summary>
    /// The <see cref="AccountResources"/> key backing each code. Kept explicit rather than derived from the
    /// enum name so a rename cannot silently start resolving to a missing key.
    /// </summary>
    public static IReadOnlyDictionary<AccountUiMessageCode, string> ResourceKeys { get; } =
        new Dictionary<AccountUiMessageCode, string>
        {
            [AccountUiMessageCode.LoginInvalidCredentials] = "Login_InvalidCredentials",
            [AccountUiMessageCode.LoginInvalidReturnUrl] = "Login_InvalidReturnUrl",
            [AccountUiMessageCode.ConsentRequestNotFound] = "Consent_RequestNotFound",
            [AccountUiMessageCode.ConsentRememberNotAllowed] = "Consent_RememberNotAllowed",
            [AccountUiMessageCode.ConsentRequiredScopeNotGranted] = "Consent_RequiredScopeNotGranted",
            [AccountUiMessageCode.LogoutIdRequired] = "Logout_IdRequired",
            [AccountUiMessageCode.LogoutIdNotFound] = "Logout_IdNotFound",
            [AccountUiMessageCode.DomainNotFound] = "Domain_NotFound",
            [AccountUiMessageCode.ErrorGeneric] = "Error_Generic",
        };

    /// <summary>
    /// Maps a core login outcome onto its presentation code. The core deliberately reports fewer codes than
    /// the UI has messages: it decides what happened, not what is shown.
    /// </summary>
    public static AccountUiMessageCode From(LoginFlowErrorCode code) => code switch
    {
        LoginFlowErrorCode.InvalidCredentials => AccountUiMessageCode.LoginInvalidCredentials,
        LoginFlowErrorCode.InvalidReturnUrl => AccountUiMessageCode.LoginInvalidReturnUrl,
        // A missing realm context is an infrastructure fault; the user is told nothing about it beyond the
        // generic failure, exactly like a bad credential.
        LoginFlowErrorCode.NoRealmContext => AccountUiMessageCode.LoginInvalidCredentials,
        // No catch-all: a reason added to the core must be given a message here deliberately, instead of
        // silently inheriting "invalid credentials".
        _ => throw new ArgumentOutOfRangeException(
            nameof(code), code, "This login failure has no presentation message."),
    };

    public static string Resolve(this IStringLocalizer<AccountResources> localizer, AccountUiMessageCode code)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return localizer[ResourceKeys[code]];
    }
}
