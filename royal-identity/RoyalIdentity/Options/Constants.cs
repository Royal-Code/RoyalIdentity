using Microsoft.IdentityModel.Tokens;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Options;

#pragma warning disable S3218 // 

internal static class Constants
{
    public const string ServerName = "RoyalIdentity";
    public const string ServerAuthenticationType = ServerName;
    public const string ExternalAuthenticationMethod = "external";
    public const string DefaultHashAlgorithm = "SHA256";

    public static readonly TimeSpan DefaultCookieTimeSpan = TimeSpan.FromHours(10);
    public static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(60);

    public static readonly HashSet<string> SupportedResponseTypes =
    [
        ResponseTypes.Code,
        ResponseTypes.Token,
        ResponseTypes.IdToken
    ];

    private static readonly Func<string, bool> ContainsSupportedResponseType = SupportedResponseTypes.Contains;

    public static bool ResponseTypesIsSuported(ICollection<string> responseTypes)
    {
        return responseTypes.All(ContainsSupportedResponseType);
    }

    public static readonly List<string> SupportedCodeChallengeMethods = new()
    {
        CodeChallengeMethods.Plain,
        CodeChallengeMethods.Sha256
    };

    public enum ScopeRequirement
    {
        None,
        ResourceOnly,
        IdentityOnly,
        Identity
    }

    public static ScopeRequirement GetResponseTypeScopeRequirement(IEnumerable<string> responseTypes)
    {
        var requeriment = ScopeRequirement.None;
        foreach (var responseType in responseTypes)
        {
            switch (responseType)
            {
                case ResponseTypes.Token:
                    requeriment = requeriment == ScopeRequirement.None 
                        ? ScopeRequirement.ResourceOnly 
                        : ScopeRequirement.Identity;
                    break;
                case ResponseTypes.IdToken:
                    requeriment = requeriment == ScopeRequirement.None
                        ? ScopeRequirement.IdentityOnly
                        : ScopeRequirement.Identity;
                    break;
            }
        }
        return requeriment;
    }



    public static readonly Dictionary<string, IEnumerable<string>> AllowedResponseModesForGrantType =
        new()
        {
            {
                GrantType.AuthorizationCode,
                [
                    ResponseModes.Query,
                    ResponseModes.FormPost,
                    ResponseModes.Fragment
                ]
            },
            {
                GrantType.Hybrid, [ ResponseModes.FormPost]
            }
        };

    public static readonly List<string> SupportedResponseModes = new()
    {
        ResponseModes.FormPost,
        ResponseModes.Query,
        ResponseModes.Fragment
    };

    public static readonly string[] SupportedSubjectTypes =
    {
        "pairwise", "public"
    };

    public static class SigningAlgorithms
    {
        public const string RSA_SHA_256 = "RS256";
    }

    public static readonly List<string> SupportedDisplayModes =
    [
        DisplayModes.Page,
        DisplayModes.Popup,
        DisplayModes.Touch,
        DisplayModes.Wap
    ];

    public static readonly List<string> SupportedPromptModes =
    [
        PromptModes.None,
        PromptModes.Login,
        PromptModes.Consent,
        PromptModes.SelectAccount
    ];

    public static class KnownAcrValues
    {
        public const string HomeRealm = "idp:";
        public const string Tenant = "tenant:";

        public static readonly string[] All = [HomeRealm, Tenant];
    }

    public static readonly Dictionary<string, int> ProtectedResourceErrorStatusCodes = new()
    {
        { ProtectedResourceErrors.InvalidToken, 401 },
        { ProtectedResourceErrors.ExpiredToken, 401 },
        { ProtectedResourceErrors.InvalidRequest, 400 },
        { ProtectedResourceErrors.InsufficientScope, 403 }
    };

    public static readonly Dictionary<string, IEnumerable<string>> ScopeToClaimsMapping =
        new()
        {
            {
                ServerConstants.StandardScopes.Profile,
                [
                    JwtClaimTypes.Name,
                    JwtClaimTypes.FamilyName,
                    JwtClaimTypes.GivenName,
                    JwtClaimTypes.MiddleName,
                    JwtClaimTypes.NickName,
                    JwtClaimTypes.PreferredUserName,
                    JwtClaimTypes.Profile,
                    JwtClaimTypes.Picture,
                    JwtClaimTypes.WebSite,
                    JwtClaimTypes.Gender,
                    JwtClaimTypes.BirthDate,
                    JwtClaimTypes.ZoneInfo,
                    JwtClaimTypes.Locale,
                    JwtClaimTypes.UpdatedAt
                ]
            },
            {
                ServerConstants.StandardScopes.Email,
                [
                    JwtClaimTypes.Email,
                    JwtClaimTypes.EmailVerified
                ]
            },
            {
                ServerConstants.StandardScopes.Address,
                [
                    JwtClaimTypes.Address
                ]
            },
            {
                ServerConstants.StandardScopes.Phone,
                [
                    JwtClaimTypes.PhoneNumber,
                    JwtClaimTypes.PhoneNumberVerified
                ]
            },
            {
                ServerConstants.StandardScopes.OpenId,
                [
                    JwtClaimTypes.Subject
                ]
            }
        };

    public static class UIConstants
    {
        // the limit after which old messages are purged
        public const int CookieMessageThreshold = 2;

        public static class DefaultRoutePathParams
        {
            public const string Error = "errorId";
            public const string Login = "returnUrl";
            public const string Consent = "returnUrl";
            public const string Logout = "logoutId";
            public const string EndSessionCallback = "endSessionId";
            public const string Custom = "returnUrl";
            public const string UserCode = "userCode";
        }

        public static class DefaultRoutePaths
        {
            public const string Login = "/account/login";
            public const string Logout = "/account/logout";
            public const string Consent = "/account/consent";
            public const string Error = "/home/error";
            public const string DeviceVerification = "/device";
        }
    }

    public static class EndpointNames
    {
        public const string Authorize = "Authorize";
        public const string Token = "Token";
        public const string DeviceAuthorization = "DeviceAuthorization";
        public const string Discovery = "Discovery";
        public const string Introspection = "Introspection";
        public const string Revocation = "Revocation";
        public const string EndSession = "Endsession";
        public const string CheckSession = "Checksession";
        public const string UserInfo = "Userinfo";
    }

    public static class ProtocolRoutePaths
    {
        public const string ConnectPathPrefix = "connect";

        public const string Authorize = ConnectPathPrefix + "/authorize";
        public const string AuthorizeCallback = Authorize + "/callback";
        public const string DiscoveryConfiguration = ".well-known/openid-configuration";
        public const string DiscoveryWebKeys = DiscoveryConfiguration + "/jwks";
        public const string Token = ConnectPathPrefix + "/token";
        public const string Revocation = ConnectPathPrefix + "/revocation";
        public const string UserInfo = ConnectPathPrefix + "/userinfo";
        public const string Introspection = ConnectPathPrefix + "/introspect";
        public const string EndSession = ConnectPathPrefix + "/endsession";
        public const string EndSessionCallback = EndSession + "/callback";
        public const string CheckSession = ConnectPathPrefix + "/checksession";
        public const string DeviceAuthorization = ConnectPathPrefix + "/deviceauthorization";

        public const string MtlsPathPrefix = ConnectPathPrefix + "/mtls";
        public const string MtlsToken = MtlsPathPrefix + "/token";
        public const string MtlsRevocation = MtlsPathPrefix + "/revocation";
        public const string MtlsIntrospection = MtlsPathPrefix + "/introspect";
        public const string MtlsDeviceAuthorization = MtlsPathPrefix + "/deviceauthorization";

        public static readonly string[] CorsPaths =
        [
            DiscoveryConfiguration,
            DiscoveryWebKeys,
            Token,
            UserInfo,
            Revocation
        ];
    }

    public static class EnvironmentKeys
    {
        public const string ServerBasePath = "idsvr:ServerBasePath";
        public const string SignOutCalled = "idsvr:ServerSignOutCalled";
    }

    public static class TokenTypeHints
    {
        public const string RefreshToken = "refresh_token";
        public const string AccessToken = "access_token";
    }

    public static readonly ICollection<string> SupportedTokenTypeHints =
    [
        TokenTypeHints.RefreshToken,
        TokenTypeHints.AccessToken
    ];

    public static class RevocationErrors
    {
        public const string UnsupportedTokenType = "unsupported_token_type";
    }

    public static class Filters
    {
        // filter for claims from an incoming access token (e.g. used at the user profile endpoint)
        public static readonly string[] ProtocolClaimsFilter =
        [
            JwtClaimTypes.AccessTokenHash,
            JwtClaimTypes.Audience,
            JwtClaimTypes.AuthorizedParty,
            JwtClaimTypes.AuthorizationCodeHash,
            JwtClaimTypes.ClientId,
            JwtClaimTypes.Expiration,
            JwtClaimTypes.IssuedAt,
            JwtClaimTypes.Issuer,
            JwtClaimTypes.JwtId,
            JwtClaimTypes.Nonce,
            JwtClaimTypes.NotBefore,
            JwtClaimTypes.ReferenceTokenId,
            JwtClaimTypes.SessionId,
            JwtClaimTypes.Scope
        ];

        // filter list for claims returned from profile service prior to creating tokens
        public static readonly string[] ClaimsServiceFilterClaimTypes =
        [
            // TODO: consider JwtClaimTypes.AuthenticationContextClassReference,
            JwtClaimTypes.AccessTokenHash,
            JwtClaimTypes.Audience,
            JwtClaimTypes.AuthenticationMethod,
            JwtClaimTypes.AuthenticationTime,
            JwtClaimTypes.AuthorizedParty,
            JwtClaimTypes.AuthorizationCodeHash,
            JwtClaimTypes.ClientId,
            JwtClaimTypes.Expiration,
            JwtClaimTypes.IdentityProvider,
            JwtClaimTypes.IssuedAt,
            JwtClaimTypes.Issuer,
            JwtClaimTypes.JwtId,
            JwtClaimTypes.Nonce,
            JwtClaimTypes.NotBefore,
            JwtClaimTypes.ReferenceTokenId,
            JwtClaimTypes.SessionId,
            JwtClaimTypes.Subject,
            JwtClaimTypes.Scope,
            JwtClaimTypes.Confirmation
        ];

        public static readonly string[] JwtRequestClaimTypesFilter =
        [
            JwtClaimTypes.Audience,
            JwtClaimTypes.Expiration,
            JwtClaimTypes.IssuedAt,
            JwtClaimTypes.Issuer,
            JwtClaimTypes.NotBefore,
            JwtClaimTypes.JwtId
        ];
    }

    public static class WsFedSignOut
    {
        public const string LogoutUriParameterName = "wa";
        public const string LogoutUriParameterValue = "wsignoutcleanup1.0";
    }

    public static class AuthorizationParamsStore
    {
        public const string MessageStoreIdParameterName = "authzId";
    }

    public static class CurveOids
    {
        public const string P256 = "1.2.840.10045.3.1.7";
        public const string P384 = "1.3.132.0.34";
        public const string P521 = "1.3.132.0.35";
    }
}

public static class OidcConstants
{
    public static class GrantType
    {
        public const string Hybrid = "hybrid";
        public const string AuthorizationCode = "authorization_code";
        public const string Implicit = "implicit";
        public const string ClientCredentials = "client_credentials";
        //public const string ResourceOwnerPassword = "password" --- Removido --- não será mais usado
        public const string DeviceFlow = "urn:ietf:params:oauth:grant-type:device_code";
    }

    public static class AuthorizeRequest
    {
        public const string Scope = "scope";
        public const string ResponseType = "response_type";
        public const string ClientId = "client_id";
        public const string RedirectUri = "redirect_uri";
        public const string State = "state";
        public const string ResponseMode = "response_mode";
        public const string Nonce = "nonce";
        public const string Display = "display";
        public const string Prompt = "prompt";
        public const string MaxAge = "max_age";
        public const string UiLocales = "ui_locales";
        public const string IdTokenHint = "id_token_hint";
        public const string LoginHint = "login_hint";
        public const string AcrValues = "acr_values";
        public const string CodeChallenge = "code_challenge";
        public const string CodeChallengeMethod = "code_challenge_method";
        public const string Request = "request";
        public const string RequestUri = "request_uri";
        public const string Resource = "resource";
        public const string DPoPKeyThumbprint = "dpop_jkt";
    }

    public static class AuthorizeErrors
    {
        // OAuth2 errors
        public const string InvalidRequest = "invalid_request";
        public const string UnauthorizedClient = "unauthorized_client";
        public const string AccessDenied = "access_denied";
        public const string UnsupportedResponseType = "unsupported_response_type";
        public const string InvalidScope = "invalid_scope";
        public const string ServerError = "server_error";
        public const string TemporarilyUnavailable = "temporarily_unavailable";
        public const string UnmetAuthenticationRequirements = "unmet_authentication_requirements";

        // OIDC errors
        public const string InteractionRequired = "interaction_required";
        public const string LoginRequired = "login_required";
        public const string AccountSelectionRequired = "account_selection_required";
        public const string ConsentRequired = "consent_required";
        public const string InvalidRequestUri = "invalid_request_uri";
        public const string InvalidRequestObject = "invalid_request_object";
        public const string RequestNotSupported = "request_not_supported";
        public const string RequestUriNotSupported = "request_uri_not_supported";
        public const string RegistrationNotSupported = "registration_not_supported";

        // resource indicator spec
        public const string InvalidTarget = "invalid_target";
    }

    public static class AuthorizeResponseFields
    {
        public const string Scope = "scope";
        public const string Code = "code";
        public const string AccessToken = "access_token";
        public const string ExpiresIn = "expires_in";
        public const string TokenType = "token_type";
        public const string RefreshToken = "refresh_token";
        public const string IdentityToken = "id_token";
        public const string State = "state";
        public const string SessionState = "session_state";
        public const string Issuer = "iss";
        public const string Error = "error";
        public const string ErrorDescription = "error_description";
    }

    public static class DeviceAuthorizationResponse
    {
        public const string DeviceCode = "device_code";
        public const string UserCode = "user_code";
        public const string VerificationUri = "verification_uri";
        public const string VerificationUriComplete = "verification_uri_complete";
        public const string ExpiresIn = "expires_in";
        public const string Interval = "interval";
    }

    public static class EndSessionRequest
    {
        public const string IdTokenHint = "id_token_hint";
        public const string LogoutHint = "logout_hint";
        public const string ClientId = "client_id";
        public const string PostLogoutRedirectUri = "post_logout_redirect_uri";
        public const string State = "state";
        public const string UiLocales = "ui_locales";
    }

    public static class TokenRequest
    {
        public const string GrantType = "grant_type";
        public const string RedirectUri = "redirect_uri";
        public const string ClientId = "client_id";
        public const string ClientSecret = "client_secret";
        public const string ClientAssertion = "client_assertion";
        public const string ClientAssertionType = "client_assertion_type";
        public const string Assertion = "assertion";
        public const string Code = "code";
        public const string RefreshToken = "refresh_token";
        public const string Scope = "scope";
        public const string UserName = "username";
        public const string Password = "password";
        public const string CodeVerifier = "code_verifier";
        public const string TokenType = "token_type";
        public const string Algorithm = "alg";
        public const string Key = "key";
        public const string DeviceCode = "device_code";

        // token exchange
        public const string Resource = "resource";
        public const string Audience = "audience";
        public const string RequestedTokenType = "requested_token_type";
        public const string SubjectToken = "subject_token";
        public const string SubjectTokenType = "subject_token_type";
        public const string ActorToken = "actor_token";
        public const string ActorTokenType = "actor_token_type";

        // ciba
        public const string AuthenticationRequestId = "auth_req_id";
    }

    public static class BackchannelAuthenticationRequest
    {
        public const string Scope = "scope";
        public const string ClientNotificationToken = "client_notification_token";
        public const string AcrValues = "acr_values";
        public const string LoginHintToken = "login_hint_token";
        public const string IdTokenHint = "id_token_hint";
        public const string LoginHint = "login_hint";
        public const string BindingMessage = "binding_message";
        public const string UserCode = "user_code";
        public const string RequestedExpiry = "requested_expiry";
        public const string Request = "request";
        public const string Resource = "resource";
        public const string DPoPKeyThumbprint = "dpop_jkt";
    }

    public static class BackchannelAuthenticationRequestErrors
    {
        public const string InvalidRequestObject = "invalid_request_object";
        public const string InvalidRequest = "invalid_request";
        public const string InvalidScope = "invalid_scope";
        public const string ExpiredLoginHintToken = "expired_login_hint_token";
        public const string UnknownUserId = "unknown_user_id";
        public const string UnauthorizedClient = "unauthorized_client";
        public const string MissingUserCode = "missing_user_code";
        public const string InvalidUserCode = "invalid_user_code";
        public const string InvalidBindingMessage = "invalid_binding_message";
        public const string InvalidClient = "invalid_client";
        public const string AccessDenied = "access_denied";
        public const string InvalidTarget = "invalid_target";
    }

    public static class TokenRequestTypes
    {
        public const string Bearer = "bearer";
        public const string Pop = "pop";
    }

    public static class TokenErrors
    {
        public const string InvalidRequest = "invalid_request";
        public const string InvalidClient = "invalid_client";
        public const string InvalidGrant = "invalid_grant";
        public const string UnauthorizedClient = "unauthorized_client";
        public const string UnsupportedGrantType = "unsupported_grant_type";
        public const string UnsupportedResponseType = "unsupported_response_type";
        public const string InvalidScope = "invalid_scope";
        public const string AuthorizationPending = "authorization_pending";
        public const string AccessDenied = "access_denied";
        public const string SlowDown = "slow_down";
        public const string ExpiredToken = "expired_token";
        public const string InvalidTarget = "invalid_target";
        public const string InvalidDPoPProof = "invalid_dpop_proof";
        public const string UseDPoPNonce = "use_dpop_nonce";
    }

    public static class TokenResponse
    {
        public const string AccessToken = "access_token";
        public const string ExpiresIn = "expires_in";
        public const string TokenType = "token_type";
        public const string RefreshToken = "refresh_token";
        public const string IdentityToken = "id_token";
        public const string Error = "error";
        public const string ErrorDescription = "error_description";
        public const string BearerTokenType = "Bearer";
        public const string DPoPTokenType = "DPoP";
        public const string IssuedTokenType = "issued_token_type";
        public const string Scope = "scope";
    }

    public static class BackchannelAuthenticationResponse
    {
        public const string AuthenticationRequestId = "auth_req_id";
        public const string ExpiresIn = "expires_in";
        public const string Interval = "interval";
    }

    public static class PushedAuthorizationRequestResponse
    {
        public const string ExpiresIn = "expires_in";
        public const string RequestUri = "request_uri";
    }

    public static class TokenIntrospectionRequest
    {
        public const string Token = "token";
        public const string TokenTypeHint = "token_type_hint";
    }

    public static class RevocationRequest
    {
        public const string Token = "token";
        public const string TokenTypeHint = "token_type_hint";
    }

    public static class RegistrationResponse
    {
        public const string Error = "error";
        public const string ErrorDescription = "error_description";
        public const string ClientId = "client_id";
        public const string ClientSecret = "client_secret";
        public const string RegistrationAccessToken = "registration_access_token";
        public const string RegistrationClientUri = "registration_client_uri";
        public const string ClientIdIssuedAt = "client_id_issued_at";
        public const string ClientSecretExpiresAt = "client_secret_expires_at";
        public const string SoftwareStatement = "software_statement";
    }

    public static class ClientMetadata
    {
        public const string RedirectUris = "redirect_uris";
        public const string ResponseTypes = "response_types";
        public const string GrantTypes = "grant_types";
        public const string ApplicationType = "application_type";
        public const string Contacts = "contacts";
        public const string ClientName = "client_name";
        public const string LogoUri = "logo_uri";
        public const string ClientUri = "client_uri";
        public const string PolicyUri = "policy_uri";
        public const string TosUri = "tos_uri";
        public const string JwksUri = "jwks_uri";
        public const string Jwks = "jwks";
        public const string SectorIdentifierUri = "sector_identifier_uri";
        public const string Scope = "scope";
        public const string PostLogoutRedirectUris = "post_logout_redirect_uris";
        public const string FrontChannelLogoutUri = "frontchannel_logout_uri";
        public const string FrontChannelLogoutSessionRequired = "frontchannel_logout_session_required";
        public const string BackchannelLogoutUri = "backchannel_logout_uri";
        public const string BackchannelLogoutSessionRequired = "backchannel_logout_session_required";
        public const string SoftwareId = "software_id";
        public const string SoftwareStatement = "software_statement";
        public const string SoftwareVersion = "software_version";
        public const string SubjectType = "subject_type";
        public const string TokenEndpointAuthenticationMethod = "token_endpoint_auth_method";
        public const string TokenEndpointAuthenticationSigningAlgorithm = "token_endpoint_auth_signing_alg";
        public const string DefaultMaxAge = "default_max_age";
        public const string RequireAuthenticationTime = "require_auth_time";
        public const string DefaultAcrValues = "default_acr_values";
        public const string InitiateLoginUri = "initiate_login_uri";
        public const string RequestUris = "request_uris";
        public const string IdentityTokenSignedResponseAlgorithm = "id_token_signed_response_alg";
        public const string IdentityTokenEncryptedResponseAlgorithm = "id_token_encrypted_response_alg";
        public const string IdentityTokenEncryptedResponseEncryption = "id_token_encrypted_response_enc";
        public const string UserinfoSignedResponseAlgorithm = "userinfo_signed_response_alg";
        public const string UserInfoEncryptedResponseAlgorithm = "userinfo_encrypted_response_alg";
        public const string UserinfoEncryptedResponseEncryption = "userinfo_encrypted_response_enc";
        public const string RequestObjectSigningAlgorithm = "request_object_signing_alg";
        public const string RequestObjectEncryptionAlgorithm = "request_object_encryption_alg";
        public const string RequestObjectEncryptionEncryption = "request_object_encryption_enc";
        public const string RequireSignedRequestObject = "require_signed_request_object";
        public const string AlwaysUseDPoPBoundAccessTokens = "dpop_bound_access_tokens";
    }

    public static class TokenTypes
    {
        public const string AccessToken = "access_token";
        public const string IdentityToken = "id_token";
        public const string RefreshToken = "refresh_token";
        public const string Code = "code";
    }

    public static class TokenTypeIdentifiers
    {
        public const string AccessToken = "urn:ietf:params:oauth:token-type:access_token";
        public const string IdentityToken = "urn:ietf:params:oauth:token-type:id_token";
        public const string RefreshToken = "urn:ietf:params:oauth:token-type:refresh_token";
        public const string Saml11 = "urn:ietf:params:oauth:token-type:saml1";
        public const string Saml2 = "urn:ietf:params:oauth:token-type:saml2";
        public const string Jwt = "urn:ietf:params:oauth:token-type:jwt";
    }

    public static class AuthenticationSchemes
    {
        public const string AuthorizationHeaderBearer = "Bearer";
        public const string AuthorizationHeaderDPoP = "DPoP";

        public const string FormPostBearer = "access_token";
        public const string QueryStringBearer = "access_token";

        public const string AuthorizationHeaderPop = "PoP";
        public const string FormPostPop = "pop_access_token";
        public const string QueryStringPop = "pop_access_token";
    }

    public static class GrantTypes
    {
        //public const string Password = "password" --- removido ---
        public const string AuthorizationCode = "authorization_code";
        public const string ClientCredentials = "client_credentials";
        public const string RefreshToken = "refresh_token";
        public const string Saml2Bearer = "urn:ietf:params:oauth:grant-type:saml2-bearer";
        public const string JwtBearer = "urn:ietf:params:oauth:grant-type:jwt-bearer";
        public const string DeviceCode = "urn:ietf:params:oauth:grant-type:device_code";
        public const string TokenExchange = "urn:ietf:params:oauth:grant-type:token-exchange";
        public const string Ciba = "urn:openid:params:grant-type:ciba";
    }

    public static class ClientAssertionTypes
    {
        public const string JwtBearer = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
        public const string SamlBearer = "urn:ietf:params:oauth:client-assertion-type:saml2-bearer";
    }

    public static class ResponseTypes
    {
        public const string Code = "code";
        public const string Token = "token";
        public const string IdToken = "id_token";
        // TODO: remove
        //public const string IdTokenToken = "id_token token";
        //public const string CodeIdToken = "code id_token";
        //public const string CodeToken = "code token";
        //public const string CodeIdTokenToken = "code id_token token";
    }

    public static class ResponseModes
    {
        public const string FormPost = "form_post";
        public const string Query = "query";
        public const string Fragment = "fragment";
    }

    public static class DisplayModes
    {
        public const string Page = "page";
        public const string Popup = "popup";
        public const string Touch = "touch";
        public const string Wap = "wap";
    }

    public static class PromptModes
    {
        public const string None = "none";
        public const string Login = "login";
        public const string Consent = "consent";
        public const string SelectAccount = "select_account";
        public const string Create = "create";
    }

    public static class CodeChallengeMethods
    {
        public const string Plain = "plain";
        public const string Sha256 = "S256";
    }

    public static class ProtectedResourceErrors
    {
        public const string InvalidToken = "invalid_token";
        public const string ExpiredToken = "expired_token";
        public const string InvalidRequest = "invalid_request";
        public const string InsufficientScope = "insufficient_scope";
    }

    public static class EndpointAuthenticationMethods
    {
        public const string BasicAuthentication = "client_secret_basic";
        public const string PostBody = "client_secret_post";
        public const string PrivateKeyJwt = "private_key_jwt";
        public const string TlsClientAuth = "tls_client_auth";
        public const string SelfSignedTlsClientAuth = "self_signed_tls_client_auth";
    }

    public static class AuthenticationMethods
    {
        public const string FacialRecognition = "face";
        public const string FingerprintBiometric = "fpt";
        public const string Geolocation = "geo";
        public const string ProofOfPossessionHardwareSecuredKey = "hwk";
        public const string IrisScanBiometric = "iris";
        public const string KnowledgeBasedAuthentication = "kba";
        public const string MultipleChannelAuthentication = "mca";
        public const string MultiFactorAuthentication = "mfa";
        public const string OneTimePassword = "otp";
        public const string PersonalIdentificationOrPattern = "pin";
        public const string ProofOfPossessionKey = "pop";
        public const string Password = "pwd";
        public const string RiskBasedAuthentication = "rba";
        public const string RetinaScanBiometric = "retina";
        public const string SmartCard = "sc";
        public const string ConfirmationBySms = "sms";
        public const string ProofOfPossessionSoftwareSecuredKey = "swk";
        public const string ConfirmationByTelephone = "tel";
        public const string UserPresenceTest = "user";
        public const string VoiceBiometric = "vbm";
        public const string WindowsIntegratedAuthentication = "wia";
    }

    public static class Algorithms
    {
        public const string None = "none";

        public static class Symmetric
        {
            public const string HS256 = "HS256";
            public const string HS384 = "HS384";
            public const string HS512 = "HS512";
        }

        public static class Asymmetric
        {
            public const string RS256 = "RS256";
            public const string RS384 = "RS384";
            public const string RS512 = "RS512";

            public const string ES256 = "ES256";
            public const string ES384 = "ES384";
            public const string ES512 = "ES512";

            public const string PS256 = "PS256";
            public const string PS384 = "PS384";
            public const string PS512 = "PS512";
        }
    }

    public static class Discovery
    {
        public const string Issuer = "issuer";

        // endpoints
        public const string AuthorizationEndpoint = "authorization_endpoint";
        public const string DeviceAuthorizationEndpoint = "device_authorization_endpoint";
        public const string TokenEndpoint = "token_endpoint";
        public const string UserInfoEndpoint = "userinfo_endpoint";
        public const string IntrospectionEndpoint = "introspection_endpoint";
        public const string RevocationEndpoint = "revocation_endpoint";
        public const string DiscoveryEndpoint = ".well-known/openid-configuration";
        public const string JwksUri = "jwks_uri";
        public const string EndSessionEndpoint = "end_session_endpoint";
        public const string CheckSessionIframe = "check_session_iframe";
        public const string RegistrationEndpoint = "registration_endpoint";
        public const string MtlsEndpointAliases = "mtls_endpoint_aliases";
        public const string PushedAuthorizationRequestEndpoint = "pushed_authorization_request_endpoint";

        // common capabilities
        public const string FrontChannelLogoutSupported = "frontchannel_logout_supported";
        public const string FrontChannelLogoutSessionSupported = "frontchannel_logout_session_supported";
        public const string BackChannelLogoutSupported = "backchannel_logout_supported";
        public const string BackChannelLogoutSessionSupported = "backchannel_logout_session_supported";
        public const string GrantTypesSupported = "grant_types_supported";
        public const string CodeChallengeMethodsSupported = "code_challenge_methods_supported";
        public const string ScopesSupported = "scopes_supported";
        public const string SubjectTypesSupported = "subject_types_supported";
        public const string ResponseModesSupported = "response_modes_supported";
        public const string ResponseTypesSupported = "response_types_supported";
        public const string ClaimsSupported = "claims_supported";
        public const string TokenEndpointAuthenticationMethodsSupported = "token_endpoint_auth_methods_supported";

        // more capabilities
        public const string ClaimsLocalesSupported = "claims_locales_supported";
        public const string ClaimsParameterSupported = "claims_parameter_supported";
        public const string ClaimTypesSupported = "claim_types_supported";
        public const string DisplayValuesSupported = "display_values_supported";
        public const string AcrValuesSupported = "acr_values_supported";
        public const string IdTokenEncryptionAlgorithmsSupported = "id_token_encryption_alg_values_supported";
        public const string IdTokenEncryptionEncValuesSupported = "id_token_encryption_enc_values_supported";
        public const string IdTokenSigningAlgorithmsSupported = "id_token_signing_alg_values_supported";
        public const string OpPolicyUri = "op_policy_uri";
        public const string OpTosUri = "op_tos_uri";

        public const string RequestObjectEncryptionAlgorithmsSupported =
            "request_object_encryption_alg_values_supported";

        public const string RequestObjectEncryptionEncValuesSupported =
            "request_object_encryption_enc_values_supported";

        public const string RequestObjectSigningAlgorithmsSupported = "request_object_signing_alg_values_supported";
        public const string RequestParameterSupported = "request_parameter_supported";
        public const string RequestUriParameterSupported = "request_uri_parameter_supported";
        public const string RequireRequestUriRegistration = "require_request_uri_registration";
        public const string ServiceDocumentation = "service_documentation";

        public const string TokenEndpointAuthSigningAlgorithmsSupported =
            "token_endpoint_auth_signing_alg_values_supported";

        public const string UILocalesSupported = "ui_locales_supported";
        public const string UserInfoEncryptionAlgorithmsSupported = "userinfo_encryption_alg_values_supported";
        public const string UserInfoEncryptionEncValuesSupported = "userinfo_encryption_enc_values_supported";
        public const string UserInfoSigningAlgorithmsSupported = "userinfo_signing_alg_values_supported";
        public const string TlsClientCertificateBoundAccessTokens = "tls_client_certificate_bound_access_tokens";

        public const string AuthorizationResponseIssParameterSupported =
            "authorization_response_iss_parameter_supported";

        public const string PromptValuesSupported = "prompt_values_supported";

        // CIBA
        public const string BackchannelTokenDeliveryModesSupported = "backchannel_token_delivery_modes_supported";
        public const string BackchannelAuthenticationEndpoint = "backchannel_authentication_endpoint";

        public const string BackchannelAuthenticationRequestSigningAlgValuesSupported =
            "backchannel_authentication_request_signing_alg_values_supported";

        public const string BackchannelUserCodeParameterSupported = "backchannel_user_code_parameter_supported";

        // DPoP
        public const string DPoPSigningAlgorithmsSupported = "dpop_signing_alg_values_supported";

        // PAR
        public const string RequirePushedAuthorizationRequests = "require_pushed_authorization_requests";
    }

    public static class BackchannelTokenDeliveryModes
    {
        public const string Poll = "poll";
        public const string Ping = "ping";
        public const string Push = "push";
    }

    public static class Events
    {
        public const string BackChannelLogout = "http://schemas.openid.net/event/backchannel-logout";
    }

    public static class BackChannelLogoutRequest
    {
        public const string LogoutToken = "logout_token";
    }

    public static class StandardScopes
    {
        /// <summary>REQUIRED. Informs the Authorization Server that the Client is making an OpenID Connect request. If the <c>openid</c> scope value is not present, the behavior is entirely unspecified.</summary>
        public const string OpenId = "openid";

        /// <summary>OPTIONAL. This scope value requests access to the End-User's default profile Claims, which are: <c>name</c>, <c>family_name</c>, <c>given_name</c>, <c>middle_name</c>, <c>nickname</c>, <c>preferred_username</c>, <c>profile</c>, <c>picture</c>, <c>website</c>, <c>gender</c>, <c>birthdate</c>, <c>zoneinfo</c>, <c>locale</c>, and <c>updated_at</c>.</summary>
        public const string Profile = "profile";

        /// <summary>OPTIONAL. This scope value requests access to the <c>email</c> and <c>email_verified</c> Claims.</summary>
        public const string Email = "email";

        /// <summary>OPTIONAL. This scope value requests access to the <c>address</c> Claim.</summary>
        public const string Address = "address";

        /// <summary>OPTIONAL. This scope value requests access to the <c>phone_number</c> and <c>phone_number_verified</c> Claims.</summary>
        public const string Phone = "phone";

        /// <summary>This scope value MUST NOT be used with the OpenID Connect Implicit Client Implementer's Guide 1.0. See the OpenID Connect Basic Client Implementer's Guide 1.0 (http://openid.net/specs/openid-connect-implicit-1_0.html#OpenID.Basic) for its usage in that subset of OpenID Connect.</summary>
        public const string OfflineAccess = "offline_access";
    }

    public static class HttpHeaders
    {
        public const string DPoP = "DPoP";
        public const string DPoPNonce = "DPoP-Nonce";
    }
}

/// <summary>
/// Commonly used claim types
/// </summary>
public static class JwtClaimTypes
{
    /// <summary>Unique Identifier for the End-User at the Issuer.</summary>
    public const string Subject = "sub";

    /// <summary>End-User's full name in displayable form including all name parts, possibly including titles and suffixes, ordered according to the End-User's locale and preferences.</summary>
    public const string Name = "name";

    /// <summary>Given name(s) or first name(s) of the End-User. Note that in some cultures, people can have multiple given names; all can be present, with the names being separated by space characters.</summary>
    public const string GivenName = "given_name";

    /// <summary>Surname(s) or last name(s) of the End-User. Note that in some cultures, people can have multiple family names or no family name; all can be present, with the names being separated by space characters.</summary>
    public const string FamilyName = "family_name";

    /// <summary>Middle name(s) of the End-User. Note that in some cultures, people can have multiple middle names; all can be present, with the names being separated by space characters. Also note that in some cultures, middle names are not used.</summary>
    public const string MiddleName = "middle_name";

    /// <summary>Casual name of the End-User that may or may not be the same as the given_name. For instance, a nickname value of Mike might be returned alongside a given_name value of Michael.</summary>
    public const string NickName = "nickname";

    /// <summary>Shorthand name by which the End-User wishes to be referred to at the RP, such as janedoe or j.doe. This value MAY be any valid JSON string including special characters such as @, /, or whitespace. The relying party MUST NOT rely upon this value being unique</summary>
    /// <remarks>The RP MUST NOT rely upon this value being unique, as discussed in http://openid.net/specs/openid-connect-basic-1_0-32.html#ClaimStability </remarks>
    public const string PreferredUserName = "preferred_username";

    /// <summary>URL of the End-User's profile page. The contents of this Web page SHOULD be about the End-User.</summary>
    public const string Profile = "profile";

    /// <summary>URL of the End-User's profile picture. This URL MUST refer to an image file (for example, a PNG, JPEG, or GIF image file), rather than to a Web page containing an image.</summary>
    /// <remarks>Note that this URL SHOULD specifically reference a profile photo of the End-User suitable for displaying when describing the End-User, rather than an arbitrary photo taken by the End-User.</remarks>
    public const string Picture = "picture";

    /// <summary>URL of the End-User's Web page or blog. This Web page SHOULD contain information published by the End-User or an organization that the End-User is affiliated with.</summary>
    public const string WebSite = "website";

    /// <summary>End-User's preferred e-mail address. Its value MUST conform to the RFC 5322 [RFC5322] addr-spec syntax. The relying party MUST NOT rely upon this value being unique</summary>
    public const string Email = "email";

    /// <summary>"true" if the End-User's e-mail address has been verified; otherwise "false".</summary>
    ///  <remarks>When this Claim Value is "true", this means that the OP took affirmative steps to ensure that this e-mail address was controlled by the End-User at the time the verification was performed. The means by which an e-mail address is verified is context-specific, and dependent upon the trust framework or contractual agreements within which the parties are operating.</remarks>
    public const string EmailVerified = "email_verified";

    /// <summary>End-User's gender. Values defined by this specification are "female" and "male". Other values MAY be used when neither of the defined values are applicable.</summary>
    public const string Gender = "gender";

    /// <summary>End-User's birthday, represented as an ISO 8601:2004 [ISO8601‑2004] YYYY-MM-DD format. The year MAY be 0000, indicating that it is omitted. To represent only the year, YYYY format is allowed. Note that depending on the underlying platform's date related function, providing just year can result in varying month and day, so the implementers need to take this factor into account to correctly process the dates.</summary>
    public const string BirthDate = "birthdate";

    /// <summary>String from the time zone database (https://data.iana.org/time-zones/tz-link.html) representing the End-User's time zone. For example, Europe/Paris or America/Los_Angeles.</summary>
    public const string ZoneInfo = "zoneinfo";

    /// <summary>End-User's locale, represented as a BCP47 [RFC5646] language tag. This is typically an ISO 639-1 Alpha-2 [ISO639‑1] language code in lowercase and an ISO 3166-1 Alpha-2 [ISO3166‑1] country code in uppercase, separated by a dash. For example, en-US or fr-CA. As a compatibility note, some implementations have used an underscore as the separator rather than a dash, for example, en_US; Relying Parties MAY choose to accept this locale syntax as well.</summary>
    public const string Locale = "locale";

    /// <summary>End-User's preferred telephone number. E.164 (https://www.itu.int/rec/T-REC-E.164/e) is RECOMMENDED as the format of this Claim, for example, +1 (425) 555-1212 or +56 (2) 687 2400. If the phone number contains an extension, it is RECOMMENDED that the extension be represented using the RFC 3966 [RFC3966] extension syntax, for example, +1 (604) 555-1234;ext=5678.</summary>
    public const string PhoneNumber = "phone_number";

    /// <summary>True if the End-User's phone number has been verified; otherwise false. When this Claim Value is true, this means that the OP took affirmative steps to ensure that this phone number was controlled by the End-User at the time the verification was performed.</summary>
    /// <remarks>The means by which a phone number is verified is context-specific, and dependent upon the trust framework or contractual agreements within which the parties are operating. When true, the phone_number Claim MUST be in E.164 format and any extensions MUST be represented in RFC 3966 format.</remarks>
    public const string PhoneNumberVerified = "phone_number_verified";

    /// <summary>End-User's preferred postal address. The value of the address member is a JSON structure containing some or all of the members defined in http://openid.net/specs/openid-connect-basic-1_0-32.html#AddressClaim </summary>
    public const string Address = "address";

    /// <summary>Audience(s) that this ID Token is intended for. It MUST contain the OAuth 2.0 client_id of the Relying Party as an audience value. It MAY also contain identifiers for other audiences. In the general case, the aud value is an array of case sensitive strings. In the common special case when there is one audience, the aud value MAY be a single case sensitive string.</summary>
    public const string Audience = "aud";

    /// <summary>Issuer Identifier for the Issuer of the response. The iss value is a case sensitive URL using the https scheme that contains scheme, host, and optionally, port number and path components and no query or fragment components.</summary>
    public const string Issuer = "iss";

    /// <summary>The time before which the JWT MUST NOT be accepted for processing, specified as the number of seconds from 1970-01-01T0:0:0Z</summary>
    public const string NotBefore = "nbf";

    /// <summary>The exp (expiration time) claim identifies the expiration time on or after which the token MUST NOT be accepted for processing, specified as the number of seconds from 1970-01-01T0:0:0Z</summary>
    public const string Expiration = "exp";

    /// <summary>Time the End-User's information was last updated. Its value is a JSON number representing the number of seconds from 1970-01-01T0:0:0Z as measured in UTC until the date/time.</summary>
    public const string UpdatedAt = "updated_at";

    /// <summary>The iat (issued at) claim identifies the time at which the JWT was issued, , specified as the number of seconds from 1970-01-01T0:0:0Z</summary>
    public const string IssuedAt = "iat";

    /// <summary>Authentication Methods References. JSON array of strings that are identifiers for authentication methods used in the authentication.</summary>
    public const string AuthenticationMethod = "amr";

    /// <summary>Session identifier. This represents a Session of an OP at an RP to a User Agent or device for a logged-in End-User. Its contents are unique to the OP and opaque to the RP.</summary>
    public const string SessionId = "sid";

    /// <summary>
    /// Authentication Context Class Reference. String specifying an Authentication Context Class Reference value that identifies the Authentication Context Class that the authentication performed satisfied.
    /// The value "0" indicates the End-User authentication did not meet the requirements of ISO/IEC 29115 level 1.
    /// Authentication using a long-lived browser cookie, for instance, is one example where the use of "level 0" is appropriate.
    /// Authentications with level 0 SHOULD NOT be used to authorize access to any resource of any monetary value.
    ///  (This corresponds to the OpenID 2.0 PAPE nist_auth_level 0.)
    /// An absolute URI or an RFC 6711 registered name SHOULD be used as the acr value; registered names MUST NOT be used with a different meaning than that which is registered.
    /// Parties using this claim will need to agree upon the meanings of the values used, which may be context-specific.
    /// The acr value is a case sensitive string.
    /// </summary>
    public const string AuthenticationContextClassReference = "acr";

    /// <summary>Time when the End-User authentication occurred. Its value is a JSON number representing the number of seconds from 1970-01-01T0:0:0Z as measured in UTC until the date/time. When a max_age request is made or when auth_time is requested as an Essential Claim, then this Claim is REQUIRED; otherwise, its inclusion is OPTIONAL.</summary>
    public const string AuthenticationTime = "auth_time";

    /// <summary>The party to which the ID Token was issued. If present, it MUST contain the OAuth 2.0 Client ID of this party. This Claim is only needed when the ID Token has a single audience value and that audience is different than the authorized party. It MAY be included even when the authorized party is the same as the sole audience. The azp value is a case sensitive string containing a StringOrURI value.</summary>
    public const string AuthorizedParty = "azp";

    /// <summary> Access Token hash value. Its value is the base64url encoding of the left-most half of the hash of the octets of the ASCII representation of the access_token value, where the hash algorithm used is the hash algorithm used in the alg Header Parameter of the ID Token's JOSE Header. For instance, if the alg is RS256, hash the access_token value with SHA-256, then take the left-most 128 bits and base64url encode them. The at_hash value is a case sensitive string.</summary>
    public const string AccessTokenHash = "at_hash";

    /// <summary>Code hash value. Its value is the base64url encoding of the left-most half of the hash of the octets of the ASCII representation of the code value, where the hash algorithm used is the hash algorithm used in the alg Header Parameter of the ID Token's JOSE Header. For instance, if the alg is HS512, hash the code value with SHA-512, then take the left-most 256 bits and base64url encode them. The c_hash value is a case sensitive string.</summary>
    public const string AuthorizationCodeHash = "c_hash";

    /// <summary>State hash value. Its value is the base64url encoding of the left-most half of the hash of the octets of the ASCII representation of the state value, where the hash algorithm used is the hash algorithm used in the alg Header Parameter of the ID Token's JOSE Header. For instance, if the alg is HS512, hash the code value with SHA-512, then take the left-most 256 bits and base64url encode them. The c_hash value is a case sensitive string.</summary>
    public const string StateHash = "s_hash";

    /// <summary>String value used to associate a Client session with an ID Token, and to mitigate replay attacks. The value is passed through unmodified from the Authentication Request to the ID Token. If present in the ID Token, Clients MUST verify that the nonce Claim Value is equal to the value of the nonce parameter sent in the Authentication Request. If present in the Authentication Request, Authorization Servers MUST include a nonce Claim in the ID Token with the Claim Value being the nonce value sent in the Authentication Request. Authorization Servers SHOULD perform no other processing on nonce values used. The nonce value is a case sensitive string.</summary>
    public const string Nonce = "nonce";

    /// <summary>JWT ID. A unique identifier for the token, which can be used to prevent reuse of the token. These tokens MUST only be used once, unless conditions for reuse were negotiated between the parties; any such negotiation is beyond the scope of this specification.</summary>
    public const string JwtId = "jti";

    /// <summary>Defines a set of event statements that each may add additional claims to fully describe a single logical event that has occurred.</summary>
    public const string Events = "events";

    /// <summary>OAuth 2.0 Client Identifier valid at the Authorization Server.</summary>
    public const string ClientId = "client_id";

    /// <summary>OpenID Connect requests MUST contain the "openid" scope value. If the openid scope value is not present, the behavior is entirely unspecified. Other scope values MAY be present. Scope values used that are not understood by an implementation SHOULD be ignored.</summary>
    public const string Scope = "scope";

    /// <summary>The "act" (actor) claim provides a means within a JWT to express that delegation has occurred and identify the acting party to whom authority has been delegated.The "act" claim value is a JSON object and members in the JSON object are claims that identify the actor. The claims that make up the "act" claim identify and possibly provide additional information about the actor.</summary>
    public const string Actor = "act";

    /// <summary>The "may_act" claim makes a statement that one party is authorized to become the actor and act on behalf of another party. The claim value is a JSON object and members in the JSON object are claims that identify the party that is asserted as being eligible to act for the party identified by the JWT containing the claim.</summary>
    public const string MayAct = "may_act";

    /// <summary>
    /// an identifier
    /// </summary>
    public const string Id = "id";

    /// <summary>
    /// The identity provider
    /// </summary>
    public const string IdentityProvider = "idp";

    /// <summary>
    /// The role
    /// </summary>
    public const string Role = "role";

    /// <summary>
    /// The roles
    /// </summary>
    public const string Roles = "roles";

    /// <summary>
    /// The reference token identifier
    /// </summary>
    public const string ReferenceTokenId = "reference_token_id";

    /// <summary>
    /// The confirmation
    /// </summary>
    public const string Confirmation = "cnf";

    /// <summary>
    /// The algorithm
    /// </summary>
    public const string Algorithm = "alg";

    /// <summary>
    /// JSON web key
    /// </summary>
    public const string JsonWebKey = "jwk";

    /// <summary>
    /// The token type
    /// </summary>
    public const string TokenType = "typ";

    /// <summary>
    /// DPoP HTTP method
    /// </summary>
    public const string DPoPHttpMethod = "htm";

    /// <summary>
    /// DPoP HTTP URL
    /// </summary>
    public const string DPoPHttpUrl = "htu";

    /// <summary>
    /// DPoP access token hash
    /// </summary>
    public const string DPoPAccessTokenHash = "ath";

    /// <summary>
    /// Values for strongly typed JWTs
    /// </summary>
    public static class JwtTypes
    {
        /// <summary>
        /// OAuth 2.0 access token
        /// </summary>
        public const string AccessToken = "at+jwt";

        /// <summary>
        /// JWT secured authorization request
        /// </summary>
        public const string AuthorizationRequest = "oauth-authz-req+jwt";

        /// <summary>
        /// DPoP proof token
        /// </summary>
        public const string DPoPProofToken = "dpop+jwt";
    }

    /// <summary>
    /// Values for the cnf claim
    /// </summary>
    public static class ConfirmationMethods
    {
        /// <summary>
        /// JSON web key
        /// </summary>
        public const string JsonWebKey = "jwk";

        /// <summary>
        /// JSON web key thumbprint
        /// </summary>
        public const string JwkThumbprint = "jkt";

        /// <summary>
        /// X.509 certificate thumbprint using SHA256
        /// </summary>
        public const string X509ThumbprintSha256 = "x5t#S256";
    }
}

public static class ServerConstants
{
    public const string LocalIdentityProvider = "local";
    public const string DefaultCookieAuthenticationScheme = "idsrv";
    public const string SignoutScheme = "idsrv";
    public const string ExternalCookieAuthenticationScheme = "idsrv.external";
    public const string DefaultCheckSessionCookieName = "idsrv.session";
    public const string AccessTokenAudience = "{0}resources";

    public const string JwtRequestClientKey = "idsrv.jwtrequesturi.client";

    /// <summary>
    /// Constants for local RoyalIdentity access token authentication.
    /// </summary>
    public static class LocalApi
    {
        /// <summary>
        /// The authentication scheme when using the AddLocalApi helper.
        /// </summary>
        public const string AuthenticationScheme = "ServerAccessToken";

        /// <summary>
        /// The API scope name when using the AddLocalApiAuthentication helper.
        /// </summary>
        public const string ScopeName = "RoyalServerApi";

        /// <summary>
        /// The authorization policy name when using the AddLocalApiAuthentication helper.
        /// </summary>
        public const string PolicyName = AuthenticationScheme;
    }

    public static class ProtocolTypes
    {
        public const string OpenIdConnect = "oidc";
        public const string WsFederation = "wsfed";
        public const string Saml2p = "saml2p";
    }

    public static class TokenTypes
    {
        public const string IdentityToken = "id_token";
        public const string AccessToken = "access_token";
        public const string RefreshToken = "refresh_token";
        public const string Code = "code";
    }

    public static class ClaimValueTypes
    {
        public const string Json = "json";
    }

    public static class ParsedSecretTypes
    {
        public const string NoSecret = "NoSecret";
        public const string SharedSecret = "SharedSecret";
        public const string X509Certificate = "X509Certificate";
        public const string JwtBearer = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
    }

    public static class SecretTypes
    {
        public const string SharedSecret = "SharedSecret";
        public const string X509CertificateThumbprint = "X509Thumbprint";
        public const string X509CertificateName = "X509Name";
        public const string X509CertificateBase64 = "X509CertificateBase64";
        public const string JsonWebKey = "JWK";
    }

    public static class ProfileDataCallers
    {
        public const string UserInfoEndpoint = "UserInfoEndpoint";
        public const string ClaimsProviderIdentityToken = "ClaimsProviderIdentityToken";
        public const string ClaimsProviderAccessToken = "ClaimsProviderAccessToken";
    }

    public static class ProfileIsActiveCallers
    {
        public const string AuthorizeEndpoint = "AuthorizeEndpoint";
        public const string IdentityTokenValidation = "IdentityTokenValidation";
        public const string AccessTokenValidation = "AccessTokenValidation";
        public const string ResourceOwnerValidation = "ResourceOwnerValidation";
        public const string ExtensionGrantValidation = "ExtensionGrantValidation";
        public const string RefreshTokenValidation = "RefreshTokenValidation";
        public const string AuthorizationCodeValidation = "AuthorizationCodeValidation";
        public const string UserInfoRequestValidation = "UserInfoRequestValidation";
        public const string DeviceCodeValidation = "DeviceCodeValidation";
    }

    public static readonly ICollection<string> SupportedSigningAlgorithms =
    [
        SecurityAlgorithms.RsaSha256,
        SecurityAlgorithms.RsaSha384,
        SecurityAlgorithms.RsaSha512,

        SecurityAlgorithms.RsaSsaPssSha256,
        SecurityAlgorithms.RsaSsaPssSha384,
        SecurityAlgorithms.RsaSsaPssSha512,

        SecurityAlgorithms.EcdsaSha256,
        SecurityAlgorithms.EcdsaSha384,
        SecurityAlgorithms.EcdsaSha512,

        SecurityAlgorithms.HmacSha256,
        SecurityAlgorithms.HmacSha384,
        SecurityAlgorithms.HmacSha512
    ];

    public enum RsaSigningAlgorithm
    {
        RS256,
        RS384,
        RS512,

        PS256,
        PS384,
        PS512
    }

    public enum ECDsaSigningAlgorithm
    {
        ES256,
        ES384,
        ES512
    }

    public static class StandardScopes
    {
        /// <summary>REQUIRED. Informs the Authorization Server that the Client is making an OpenID Connect request. If the <c>openid</c> scope value is not present, the behavior is entirely unspecified.</summary>
        public const string OpenId = "openid";

        /// <summary>OPTIONAL. This scope value requests access to the End-User's default profile Claims, which are: <c>name</c>, <c>family_name</c>, <c>given_name</c>, <c>middle_name</c>, <c>nickname</c>, <c>preferred_username</c>, <c>profile</c>, <c>picture</c>, <c>website</c>, <c>gender</c>, <c>birthdate</c>, <c>zoneinfo</c>, <c>locale</c>, and <c>updated_at</c>.</summary>
        public const string Profile = "profile";

        /// <summary>OPTIONAL. This scope value requests access to the <c>email</c> and <c>email_verified</c> Claims.</summary>
        public const string Email = "email";

        /// <summary>OPTIONAL. This scope value requests access to the <c>address</c> Claim.</summary>
        public const string Address = "address";

        /// <summary>OPTIONAL. This scope value requests access to the <c>phone_number</c> and <c>phone_number_verified</c> Claims.</summary>
        public const string Phone = "phone";

        /// <summary>This scope value MUST NOT be used with the OpenID Connect Implicit Client Implementer's Guide 1.0. See the OpenID Connect Basic Client Implementer's Guide 1.0 (http://openid.net/specs/openid-connect-implicit-1_0.html#OpenID.Basic) for its usage in that subset of OpenID Connect.</summary>
        public const string OfflineAccess = "offline_access";
    }

    public static class PersistedGrantTypes
    {
        public const string AuthorizationCode = "authorization_code";
        public const string ReferenceToken = "reference_token";
        public const string RefreshToken = "refresh_token";
        public const string UserConsent = "user_consent";
        public const string DeviceCode = "device_code";
        public const string UserCode = "user_code";
    }

    public static class UserCodeTypes
    {
        public const string Numeric = "Numeric";
    }

    public static class HttpClients
    {
        public const int DefaultTimeoutSeconds = 10;
        public const string JwtRequestUriHttpClient = "RoyalIdentity:JwtRequestUriClient";
        public const string BackChannelLogoutHttpClient = "RoyalIdentity:BackChannelLogoutClient";
    }
}