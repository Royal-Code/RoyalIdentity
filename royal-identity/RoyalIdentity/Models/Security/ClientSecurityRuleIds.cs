namespace RoyalIdentity.Models.Security;

/// <summary>
/// Stable identifiers for OAuth client configuration rules assessed as part of RFC 9700 hardening.
/// Presentation layers may localize these identifiers, but must not persist localized findings.
/// </summary>
public static class ClientSecurityRuleIds
{
    /// <summary>RFC 9700 section 2.1 exact redirect URI matching.</summary>
    public const string RedirectExactMatch = "RFC9700-2.1-REDIRECT-EXACT-MATCH";

    /// <summary>RFC 9700 section 2.6 encrypted authorization responses.</summary>
    public const string HttpsAuthorizationRedirect = "RFC9700-2.6-HTTPS-AUTHORIZATION-REDIRECT";

    /// <summary>RFC 6749 section 3.1.2 absolute redirect URI requirement.</summary>
    public const string AbsoluteAuthorizationRedirect = "RFC6749-3.1.2-ABSOLUTE-AUTHORIZATION-REDIRECT";

    /// <summary>RFC 6749 section 3.1.2 prohibition of fragments in redirect URIs.</summary>
    public const string NoAuthorizationRedirectFragment = "RFC6749-3.1.2-NO-AUTHORIZATION-REDIRECT-FRAGMENT";

    /// <summary>RFC 9700 section 2.1.1 PKCE usage.</summary>
    public const string Pkce = "RFC9700-2.1.1-PKCE";

    /// <summary>RFC 9700 section 2.1.1 non-disclosing PKCE challenge method.</summary>
    public const string PkceS256 = "RFC9700-2.1.1-PKCE-S256";

    /// <summary>RFC 9700 section 2.1.2 access tokens outside the authorization response.</summary>
    public const string NoFrontChannelAccessToken = "RFC9700-2.1.2-NO-FRONTCHANNEL-ACCESS-TOKEN";

    /// <summary>RFC 9700 section 2.4 prohibition of the password grant.</summary>
    public const string NoPasswordGrant = "RFC9700-2.4-NO-PASSWORD-GRANT";

    /// <summary>RFC 9700 section 2.2.2 replay protection for public-client refresh tokens.</summary>
    public const string PublicRefreshTokenReplayProtection = "RFC9700-2.2.2-PUBLIC-REFRESH-REPLAY";

    /// <summary>RFC 9700 section 2.5 confidential-client authentication.</summary>
    public const string ClientAuthentication = "RFC9700-2.5-CLIENT-AUTHENTICATION";

    /// <summary>RFC 9700 section 2.5 asymmetric client authentication.</summary>
    public const string AsymmetricClientAuthentication = "RFC9700-2.5-ASYMMETRIC-CLIENT-AUTHENTICATION";

    /// <summary>RFC 9700 section 2.2.1 sender-constrained access tokens.</summary>
    public const string SenderConstrainedAccessTokens = "RFC9700-2.2.1-SENDER-CONSTRAINED-ACCESS-TOKENS";
}
