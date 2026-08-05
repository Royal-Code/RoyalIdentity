// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
namespace RoyalIdentity.Options;

public class InputLengthRestrictions
{
    private const int Default = 100;

    public InputLengthRestrictions()
    { }

    public InputLengthRestrictions(InputLengthRestrictions other)
    {
        ClientId = other.ClientId;
        ClientSecret = other.ClientSecret;
        Scope = other.Scope;
        RedirectUri = other.RedirectUri;
        Nonce = other.Nonce;
        UiLocale = other.UiLocale;
        LoginHint = other.LoginHint;
        AcrValues = other.AcrValues;
        GrantType = other.GrantType;
        UserName = other.UserName;
        Password = other.Password;
        CspReport = other.CspReport;
        IdentityProvider = other.IdentityProvider;
        ExternalError = other.ExternalError;
        AuthorizationCode = other.AuthorizationCode;
        RefreshToken = other.RefreshToken;
        TokenHandle = other.TokenHandle;
        Jwt = other.Jwt;
    }

    /// <summary>
    /// Max length for client_id
    /// </summary>
    public int ClientId { get; set; } = Default;

    /// <summary>
    /// Max length for external client secrets
    /// </summary>
    public int ClientSecret { get; set; } = Default;

    /// <summary>
    /// Max length for scope
    /// </summary>
    public int Scope { get; set; } = 300;

    /// <summary>
    /// Max length for redirect_uri
    /// </summary>
    public int RedirectUri { get; set; } = 400;

    /// <summary>
    /// Max length for nonce
    /// </summary>
    public int Nonce { get; set; } = 300;

    /// <summary>
    /// Max length for ui_locale
    /// </summary>
    public int UiLocale { get; set; } = Default;

    /// <summary>
    /// Max length for login_hint
    /// </summary>
    public int LoginHint { get; set; } = Default;

    /// <summary>
    /// Max length for acr_values
    /// </summary>
    public int AcrValues { get; set; } = 300;

    /// <summary>
    /// Max length for grant_type
    /// </summary>
    public int GrantType { get; set; } = Default;

    /// <summary>
    /// Max length for username
    /// </summary>
    public int UserName { get; set; } = Default;

    /// <summary>
    /// Max length for password
    /// </summary>
    public int Password { get; set; } = Default;

    /// <summary>
    /// Max length for CSP reports
    /// </summary>
    public int CspReport { get; set; } = 2000;

    /// <summary>
    /// Max length for external identity provider name
    /// </summary>
    public int IdentityProvider { get; set; } = Default;

    /// <summary>
    /// Max length for external identity provider errors
    /// </summary>
    public int ExternalError { get; set; } = Default;

    /// <summary>
    /// Max length for authorization codes
    /// </summary>
    public int AuthorizationCode { get; set; } = Default;

    /// <summary>
    /// Max length for refresh tokens
    /// </summary>
    public int RefreshToken { get; set; } = Default;

    /// <summary>
    /// Max length for token handles
    /// </summary>
    public int TokenHandle { get; set; } = Default;

    /// <summary>
    /// Max length for JWTs
    /// </summary>
    public int Jwt { get; set; } = 51200;

    /// <summary>
    /// Min length for the code challenge
    /// </summary>
    public int CodeChallengeMinLength { get; } = 43;

    /// <summary>
    /// Max length for the code challenge
    /// </summary>
    public int CodeChallengeMaxLength { get; } = 128;

    /// <summary>
    /// Min length for the code verifier
    /// </summary>
    public int CodeVerifierMinLength { get; } = 43;

    /// <summary>
    /// Max length for the code verifier
    /// </summary>
    public int CodeVerifierMaxLength { get; } = 128;
}
