// Ignore Spelling: Cors
using Microsoft.AspNetCore.Authentication.Cookies;
using Polly;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Options;

#pragma warning disable S3218 // 

public static partial class Constants
{
    public static class Server
    {
        public const string Name = "RoyalIdentity";
        public const string AuthenticationScheme = Name;
        public const string AuthenticationName = Name;

        public const string RealmAuthenticationNamePrefix = "Realm:";
        public const string RealmRouteKey = "realm";
        public const string RealmCurrentKey = "realm.current";

        public const string DefaultCookieAuthenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        public const string ExternalAuthenticationMethod = "external";

        public static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(10);

        public static readonly IReadOnlyCollection<string> SupportedResponseTypes =
        [
            Oidc.ResponseTypes.Code,
            Oidc.ResponseTypes.Token,
            Oidc.ResponseTypes.IdToken
        ];

        public static class Realms
        {
            public const string ServerRealm = "server";
            public const string ServerDomain = "royalidentity.server";
            public const string ServerDisplayName = "RoyalIdentity Server";

            public const string AccountRealm = "account";
            public const string AccountDomain = "royalidentity.account";
            public const string AccountDisplayName = "RoyalIdentity Account Realm";

            public const string AdminRealm = "admin";
            public const string AdminDomain = "royalidentity.admin";
            public const string AdminDisplayName = "RoyalIdentity Administrative Realm";
        }

        public const string LocalIdentityProvider = "local";
        public const string CookiePrefix = ".roid.";
        public const string ExternalCookieAuthenticationScheme = $"{DefaultCookieAuthenticationScheme}.External";
        public const string DefaultCookieName = $"{CookiePrefix}user";
        public const string DefaultCheckSessionCookieName = $"{CookiePrefix}session";
        public const string AccessTokenAudience = "{0}resources";
        public static readonly TimeSpan DefaultCookieTimeSpan = TimeSpan.FromHours(1);
        public const string JwtRequestClientKey = "roid.jwtrequesturi.client";

        public static class StandardScopes
        {
            public const string OpenId = "openid";
            public const string Profile = "profile";
            public const string Email = "email";
            public const string Address = "address";
            public const string Phone = "phone";
            public const string OfflineAccess = "offline_access";
        }

        public static class LocalApi
        {
            public const string AuthenticationScheme = "ServerAccessToken";
            public const string ScopeName = "RoyalServerApi";
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

        public static class HttpClients
        {
            public const int DefaultTimeoutSeconds = 10;
            public const string JwtRequestUriHttpClient = "RoyalIdentity:JwtRequestUriClient";
            public const string BackChannelLogoutHttpClient = "RoyalIdentity:BackChannelLogoutClient";

            public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
            {
                return Policy
                    .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                    .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }

            public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
            {
                return Policy
                    .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
            }
        }
    }

    public static class UI
    {
        public static class Routes
        {
            public const string RealmRouteParameter = $"{{{Server.RealmRouteKey}}}";

            public const string Login = $"/{RealmRouteParameter}/{Names.Account}/{Names.Login}";

            public const string SignedIn = $"/{RealmRouteParameter}/{Names.Account}/{Names.SignedIn}";

            public const string Consent = $"/{RealmRouteParameter}/{Names.Account}/{Names.Consent}";

            public const string Consented = $"/{RealmRouteParameter}/{Names.Account}/{Names.Consented}";

            public const string Logout = $"/{RealmRouteParameter}/{Names.Account}/{Names.Logout}";

            public const string LoggingOut = $"/{RealmRouteParameter}/{Names.Account}/{Names.LoggingOut}";

            public const string LoggedOut = $"/{RealmRouteParameter}/{Names.Account}/{Names.LoggedOut}";

            public const string DeviceVerification = $"/{RealmRouteParameter}/{Names.Account}/{Names.Device}";

            public const string Profile = $"/{RealmRouteParameter}/{Names.Account}/{Names.Profile}";

            public const string SelectDomain = $"/{Names.Account}/{Names.Domain}";

            public const string AccessDenied = $"/{Names.Account}/{Names.AccessDenied}";

            public const string RealmAccessDenied = $"/{RealmRouteParameter}/{Names.Account}/{Names.AccessDenied}";

            public const string Error = $"/{Names.Error}";

            private static class Names
            {
                public const string Account = "account";
                public const string Login = "login";
                public const string SignedIn = "signedin";
                public const string Consent = "consent";
                public const string Consented = "consented";
                public const string Logout = "logout";
                public const string LoggingOut = "logout/processing";
                public const string LoggedOut = "logout/done";
                public const string Device = "device";
                public const string Profile = "profile";
                public const string Domain = "domain";
                public const string AccessDenied = "access-denied";
                public const string Error = "error";
            }

            public static class Params
            {
                public const string ReturnUrl = "returnUrl";

                public const string LogoutId = "logoutId";

                public const string ErrorId = "errorId";

                public const string UserCode = "userCode";
            }

            public static string BuildLoginUrl(string realm, string returnUrl)
                => $"/{realm}/{Names.Account}/{Names.Login}".AddQueryString(Params.ReturnUrl, returnUrl);

            public static string BuildSignedInUrl(string realm, string returnUrl)
                => $"/{realm}/{Names.Account}/{Names.SignedIn}".AddQueryString(Params.ReturnUrl, returnUrl);

            public static string BuildConsentUrl(string realm, string returnUrl)
                => $"/{realm}/{Names.Account}/{Names.Consent}".AddQueryString(Params.ReturnUrl, returnUrl);

            public static string BuildConsentedUrl(string realm, string returnUrl)
                => $"/{realm}/{Names.Account}/{Names.Consented}".AddQueryString(Params.ReturnUrl, returnUrl);

            public static string BuildLogoutUrl(string realm, string logoutId)
                => $"/{realm}/{Names.Account}/{Names.Logout}".AddQueryString(Params.LogoutId, logoutId);

            public static string BuildLoggingOutUrl(string realm, string logoutId)
                => $"/{realm}/{Names.Account}/{Names.LoggingOut}".AddQueryString(Params.LogoutId, logoutId);

            public static string BuildLoggedOutUrl(string realm)
                => $"/{realm}/{Names.Account}/{Names.LoggedOut}";

            public static string BuildProfileUrl(string realm)
                => $"/{realm}/{Names.Account}/{Names.Profile}";

            public static string BuildErrorUrl(string errorId)
                => Error.AddQueryString(Params.ErrorId, errorId);
        }
    }

    public static class Oidc
    {
        public static class Routes
        {
            private static class Names
            {
                public const string OidcConnect = "connect";
                public const string Callback = $"callback";
                public const string WellKnown = ".well-known";
                public const string Mtls = "mtls";

                public const string Authorize = "authorize";
                public const string DiscoveryConfiguration = "openid-configuration";
                public const string DiscoveryWebKeys = "jwks";
                public const string ProtectedResourceMetadata = "oauth-protected-resource";
                public const string Token = "token";
                public const string Revocation = "revocation";
                public const string UserInfo = "userinfo";
                public const string Introspection = "introspect";
                public const string EndSession = "endsession";
                public const string CheckSession = "checksession";
                public const string DeviceAuthorization = "deviceauthorization";
            }

            public static class Params
            {
                public const string CallbackId = "callbackId";
                public const string Authorization = "authzId";

                /// <summary>
                /// Internal marker appended to the authorize callback URL when the resource owner
                /// denies consent, signalling the pipeline to emit an <c>access_denied</c> error.
                /// </summary>
                public const string ConsentDenied = "consentDenied";
            }

            public const string Authorize = $"{{realm}}/{Names.OidcConnect}/{Names.Authorize}";
            public const string AuthorizeCallback = $"{{realm}}/{Names.OidcConnect}/{Names.Authorize}/{Names.Callback}";
            public const string DiscoveryConfiguration = $"{{realm}}/{Names.WellKnown}/{Names.DiscoveryConfiguration}";
            public const string DiscoveryWebKeys = $"{{realm}}/{Names.WellKnown}/{Names.DiscoveryConfiguration}/{Names.DiscoveryWebKeys}";
            public const string ProtectedResourceMetadata = $"{{realm}}/{Names.WellKnown}/{Names.ProtectedResourceMetadata}";
            public const string Token = $"{{realm}}/{Names.OidcConnect}/{Names.Token}";
            public const string Revocation = $"{{realm}}/{Names.OidcConnect}/{Names.Revocation}";
            public const string UserInfo = $"{{realm}}/{Names.OidcConnect}/{Names.UserInfo}";
            public const string Introspection = $"{{realm}}/{Names.OidcConnect}/{Names.Introspection}";
            public const string EndSession = $"{{realm}}/{Names.OidcConnect}/{Names.EndSession}";
            public const string EndSessionCallback = $"{{realm}}/{Names.OidcConnect}/{Names.EndSession}/{Names.Callback}";
            public const string CheckSession = $"{{realm}}/{Names.OidcConnect}/{Names.CheckSession}";
            public const string DeviceAuthorization = $"{{realm}}/{Names.OidcConnect}/{Names.DeviceAuthorization}";

            public const string MtlsToken = $"{{realm}}/{Names.OidcConnect}/{Names.Mtls}/{Names.Token}";
            public const string MtlsRevocation = $"{{realm}}/{Names.OidcConnect}/{Names.Mtls}/{Names.Revocation}";
            public const string MtlsIntrospection = $"{{realm}}/{Names.OidcConnect}/{Names.Mtls}/{Names.Introspection}";
            public const string MtlsDeviceAuthorization = $"{{realm}}/{Names.OidcConnect}/{Names.Mtls}/{Names.DeviceAuthorization}";

            public static readonly string[] CorsPaths =
            [
                DiscoveryConfiguration,
                DiscoveryWebKeys,
                ProtectedResourceMetadata,
                Token,
                UserInfo,
                Revocation
            ];

            public static string BuildAuthorizeUrl(string realm)
                => $"{realm}/{Names.OidcConnect}/{Names.Authorize}";

            public static string BuildAuthorizeCallbackUrl(string realm)
                => $"{realm}/{Names.OidcConnect}/{Names.Authorize}/{Names.Callback}";

            public static string BuildDiscoveryConfigurationUrl(string realm)
                => $"{realm}/{Names.WellKnown}/{Names.DiscoveryConfiguration}";

            public static string BuildDiscoveryWebKeysUrl(string realm)
                => $"{realm}/{Names.WellKnown}/{Names.DiscoveryConfiguration}/{Names.DiscoveryWebKeys}";

            public static string BuildProtectedResourceMetadataUrl(string realm)
                => $"{realm}/{Names.WellKnown}/{Names.ProtectedResourceMetadata}";

            public static string BuildTokenUrl(string realm)
                => $"{realm}/{Names.OidcConnect}/{Names.Token}";

            public static string BuildRevocationUrl(string realm)
                => $"{realm}/{Names.OidcConnect}/{Names.Revocation}";

            public static string BuildUserInfoUrl(string realm)
                => $"{realm}/{Names.OidcConnect}/{Names.UserInfo}";

            public static string BuildIntrospectionUrl(string realm)
                => $"{realm}/{Names.OidcConnect}/{Names.Introspection}";

            public static string BuildEndSessionUrl(string realm)
                => $"{realm}/{Names.OidcConnect}/{Names.EndSession}";

            public static string BuildEndSessionCallbackUrl(string realm)
                => $"{realm}/{Names.OidcConnect}/{Names.EndSession}/{Names.Callback}";

            public static string BuildCheckSessionUrl(string realm)
                => $"{realm}/{Names.OidcConnect}/{Names.CheckSession}";

            public static string BuildDeviceAuthorizationUrl(string realm)
                => $"{realm}/{Names.OidcConnect}/{Names.DeviceAuthorization}";

            public static string BuildMtlsTokenUrl(string realm)
                => $"{realm}/{Names.OidcConnect}/{Names.Mtls}/{Names.Token}";

            public static string BuildMtlsRevocationUrl(string realm)
                => $"{realm}/{Names.OidcConnect}/{Names.Mtls}/{Names.Revocation}";

            public static string BuildMtlsIntrospectionUrl(string realm)
                => $"{realm}/{Names.OidcConnect}/{Names.Mtls}/{Names.Introspection}";

            public static string BuildMtlsDeviceAuthorizationUrl(string realm)
                => $"{realm}/{Names.OidcConnect}/{Names.Mtls}/{Names.DeviceAuthorization}";
        }

        public static class TokenTypeHints
        {
            public const string RefreshToken = "refresh_token";
            public const string AccessToken = "access_token";
        }

        public static class ResponseTypes
        {
            public const string Code = "code";
            public const string Token = "token";
            public const string IdToken = "id_token";
        }

        public static class Authorize
        {
            public static class Request
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
                public const string RequestObject = "request";
                public const string RequestObjectUri = "request_uri";
                public const string Resource = "resource";
                public const string DPoPKeyThumbprint = "dpop_jkt";
            }

            public static class Errors
            {
                // OAuth2 errors
                public const string InvalidRequest = "invalid_request";
                public const string UnauthorizedClient = "unauthorized_client";
                public const string AccessDenied = "access_denied";
                public const string UnsupportedResponseType = "unsupported_response_type";
                public const string UnsupportedResponseMode = "unsupported_response_mode";
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

            public static class Response
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
        }

        public static class Token
        {
            public static class Request
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

            public static class Errors
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

            public static class Response
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

            public static class RequestTypes
            {
                public const string Bearer = "bearer";
                public const string Pop = "pop";
            }

            public static class Types
            {
                public const string AccessToken = "access_token";
                public const string IdentityToken = "id_token";
                public const string RefreshToken = "refresh_token";
                public const string Code = "code";
            }

            public static class TypeIdentifiers
            {
                public const string AccessToken = "urn:ietf:params:oauth:token-type:access_token";
                public const string IdentityToken = "urn:ietf:params:oauth:token-type:id_token";
                public const string RefreshToken = "urn:ietf:params:oauth:token-type:refresh_token";
                public const string Saml11 = "urn:ietf:params:oauth:token-type:saml1";
                public const string Saml2 = "urn:ietf:params:oauth:token-type:saml2";
                public const string Jwt = "urn:ietf:params:oauth:token-type:jwt";
            }
        }

        public static class Errors
        {
            public static class Revocation
            {
                public const string UnsupportedTokenType = "unsupported_token_type";
            }
        }

        public static class ResponseModes
        {
            public const string FormPost = "form_post";
            public const string Query = "query";
            public const string Fragment = "fragment";
        }

        public static class PromptModes
        {
            public const string None = "none";
            public const string Login = "login";
            public const string Consent = "consent";
            public const string SelectAccount = "select_account";
            public const string Create = "create";
        }

        public static class CodeChallenge
        {
            public static class Methods
            {
                public const string Plain = "plain";
                public const string Sha256 = "S256";
            }
        }

        public static class SubjectTypes
        {
            public const string Pairwise = "pairwise";
            public const string Public = "public";
        }

        public static class DisplayModes
        {
            public const string Page = "page";
            public const string Popup = "popup";
            public const string Touch = "touch";
            public const string Wap = "wap";
        }

        public static class Endpoint
        {
            public static class AuthMethods
            {
                public const string BasicAuthentication = "client_secret_basic";
                public const string PostBody = "client_secret_post";
                public const string PrivateKeyJwt = "private_key_jwt";
                public const string TlsClientAuth = "tls_client_auth";
                public const string SelfSignedTlsClientAuth = "self_signed_tls_client_auth";
            }
        }

        public static class AuthSchemes
        {
            public const string AuthorizationHeaderBearer = "Bearer";
            public const string AuthorizationHeaderDPoP = "DPoP";
            public const string FormPostBearer = "access_token";
            public const string QueryStringBearer = "access_token";
            public const string AuthorizationHeaderPop = "PoP";
            public const string FormPostPop = "pop_access_token";
            public const string QueryStringPop = "pop_access_token";
        }

        public static class ClientAssertionTypes
        {
            public const string JwtBearer = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
            public const string SamlBearer = "urn:ietf:params:oauth:client-assertion-type:saml2-bearer";
        }

        public static class ProtectedResource
        {
            public static class Metadata
            {
                public const string Resource = "resource";
                public const string AuthorizationServers = "authorization_servers";
                public const string ScopesSupported = "scopes_supported";
                public const string BearerMethodsSupported = "bearer_methods_supported";
                public const string ResourceName = "resource_name";
                public const string ResourceDocumentation = "resource_documentation";
                public const string ResourcePolicyUri = "resource_policy_uri";
                public const string ResourceTosUri = "resource_tos_uri";
            }

            public static class BearerMethods
            {
                public const string Header = "header";
                public const string Body = "body";
            }

            public static class Errors
            {
                public const string InvalidToken = "invalid_token";
                public const string ExpiredToken = "expired_token";
                public const string InvalidRequest = "invalid_request";
                public const string InsufficientScope = "insufficient_scope";
            }
        }

        public static class Revocation
        {
            public static class Request
            {
                public const string Token = "token";
                public const string TokenTypeHint = "token_type_hint";
            }
        }

        public static class Introspection
        {
            public static class Request
            {
                public const string Token = "token";
                public const string TokenTypeHint = "token_type_hint";
            }
        }

        public static class EndSession
        {
            public static class Request
            {
                public const string IdTokenHint = "id_token_hint";
                public const string LogoutHint = "logout_hint";
                public const string ClientId = "client_id";
                public const string PostLogoutRedirectUri = "post_logout_redirect_uri";
                public const string State = "state";
                public const string UiLocales = "ui_locales";
            }
        }

        public static class Events
        {
            public const string BackChannelLogout = "http://schemas.openid.net/event/backchannel-logout";
        }

        public static class HttpHeaders
        {
            public const string DPoP = "DPoP";
            public const string DPoPNonce = "DPoP-Nonce";
        }

        public static class AuthMethods
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

        // Low-priority groups (features not yet implemented)

        public static class Device
        {
            public static class AuthorizationResponse
            {
                public const string DeviceCode = "device_code";
                public const string UserCode = "user_code";
                public const string VerificationUri = "verification_uri";
                public const string VerificationUriComplete = "verification_uri_complete";
                public const string ExpiresIn = "expires_in";
                public const string Interval = "interval";
            }
        }

        public static class Backchannel
        {
            public static class Request
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
                public const string RequestObject = "request";
                public const string Resource = "resource";
                public const string DPoPKeyThumbprint = "dpop_jkt";
            }

            public static class Errors
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

            public static class Response
            {
                public const string AuthenticationRequestId = "auth_req_id";
                public const string ExpiresIn = "expires_in";
                public const string Interval = "interval";
            }

            public static class DeliveryModes
            {
                public const string Poll = "poll";
                public const string Ping = "ping";
                public const string Push = "push";
            }
        }

        public static class Par
        {
            public static class Response
            {
                public const string ExpiresIn = "expires_in";
                public const string RequestUri = "request_uri";
            }
        }

        public static class Registration
        {
            public static class Response
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
        }

        public static class BackChannelLogout
        {
            public static class Request
            {
                public const string LogoutToken = "logout_token";
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
            public const string ProtectedResources = "protected_resources";

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
            public const string RequestObjectEncryptionAlgorithmsSupported = "request_object_encryption_alg_values_supported";
            public const string RequestObjectEncryptionEncValuesSupported = "request_object_encryption_enc_values_supported";
            public const string RequestObjectSigningAlgorithmsSupported = "request_object_signing_alg_values_supported";
            public const string RequestParameterSupported = "request_parameter_supported";
            public const string RequestUriParameterSupported = "request_uri_parameter_supported";
            public const string RequireRequestUriRegistration = "require_request_uri_registration";
            public const string ServiceDocumentation = "service_documentation";
            public const string TokenEndpointAuthSigningAlgorithmsSupported = "token_endpoint_auth_signing_alg_values_supported";
            public const string UILocalesSupported = "ui_locales_supported";
            public const string UserInfoEncryptionAlgorithmsSupported = "userinfo_encryption_alg_values_supported";
            public const string UserInfoEncryptionEncValuesSupported = "userinfo_encryption_enc_values_supported";
            public const string UserInfoSigningAlgorithmsSupported = "userinfo_signing_alg_values_supported";
            public const string TlsClientCertificateBoundAccessTokens = "tls_client_certificate_bound_access_tokens";
            public const string AuthorizationResponseIssParameterSupported = "authorization_response_iss_parameter_supported";
            public const string PromptValuesSupported = "prompt_values_supported";

            // CIBA
            public const string BackchannelTokenDeliveryModesSupported = "backchannel_token_delivery_modes_supported";
            public const string BackchannelAuthenticationEndpoint = "backchannel_authentication_endpoint";
            public const string BackchannelAuthenticationRequestSigningAlgValuesSupported = "backchannel_authentication_request_signing_alg_values_supported";
            public const string BackchannelUserCodeParameterSupported = "backchannel_user_code_parameter_supported";

            // DPoP
            public const string DPoPSigningAlgorithmsSupported = "dpop_signing_alg_values_supported";

            // PAR
            public const string RequirePushedAuthorizationRequests = "require_pushed_authorization_requests";
        }
    }

}

public static partial class Constants
{



    public static class Filters
    {
        // filter for claims from an incoming access token (e.g. used at the user profile endpoint)
        public static readonly string[] ProtocolClaimsFilter =
        [
            JwtRegisteredClaimNames.AtHash,
            JwtRegisteredClaimNames.Aud,
            JwtRegisteredClaimNames.Azp,
            JwtRegisteredClaimNames.CHash,
            Jwt.ClaimTypes.ClientId,
            JwtRegisteredClaimNames.Exp,
            JwtRegisteredClaimNames.Iat,
            JwtRegisteredClaimNames.Iss,
            JwtRegisteredClaimNames.Jti,
            JwtRegisteredClaimNames.Nonce,
            JwtRegisteredClaimNames.Nbf,
            Jwt.ClaimTypes.ReferenceTokenId,
            JwtRegisteredClaimNames.Sid,
            Jwt.ClaimTypes.Scope
        ];

        // filter list for claims returned from profile service prior to creating tokens
        public static readonly string[] ClaimsServiceFilterClaimTypes =
        [
            JwtRegisteredClaimNames.Acr,
            JwtRegisteredClaimNames.AtHash,
            JwtRegisteredClaimNames.Aud,
            JwtRegisteredClaimNames.Amr,
            JwtRegisteredClaimNames.AuthTime,
            JwtRegisteredClaimNames.Azp,
            JwtRegisteredClaimNames.CHash,
            Jwt.ClaimTypes.ClientId,
            JwtRegisteredClaimNames.Exp,
            Jwt.ClaimTypes.IdentityProvider,
            JwtRegisteredClaimNames.Iat,
            JwtRegisteredClaimNames.Iss,
            JwtRegisteredClaimNames.Jti,
            JwtRegisteredClaimNames.Nonce,
            JwtRegisteredClaimNames.Nbf,
            Jwt.ClaimTypes.ReferenceTokenId,
            JwtRegisteredClaimNames.Sid,
            JwtRegisteredClaimNames.Sub,
            Jwt.ClaimTypes.Scope,
            Jwt.ClaimTypes.Confirmation
        ];

        public static readonly string[] JwtRequestClaimTypesFilter =
        [
            JwtRegisteredClaimNames.Aud,
            JwtRegisteredClaimNames.Exp,
            JwtRegisteredClaimNames.Iat,
            JwtRegisteredClaimNames.Iss,
            JwtRegisteredClaimNames.Nbf,
            JwtRegisteredClaimNames.Jti
        ];
    }

    public static class CurveOids
    {
        public const string P256 = "1.2.840.10045.3.1.7";
        public const string P384 = "1.3.132.0.34";
        public const string P521 = "1.3.132.0.35";
    }

    public static class IdentityProfileTypes
    {
        public const string User = nameof(User);
        public const string Client = nameof(Client);
    }

    public static class Jwt
    {
        public static class ClaimTypes
        {
            // Project-specific
            public const string IdentityProvider = "idp";
            public const string Role = "role";
            public const string Roles = "roles";
            public const string ReferenceTokenId = "reference_token_id";

            // OAuth2/OIDC extensions (not in JwtRegisteredClaimNames)
            public const string Scope = "scope";
            public const string Confirmation = "cnf";
            public const string StateHash = "s_hash";
            public const string Events = "events";
            public const string ClientId = "client_id";
            public const string Actor = "act";
            public const string MayAct = "may_act";
            public const string Id = "id";

            // OIDC standard profile claims (not in JwtRegisteredClaimNames)
            public const string MiddleName = "middle_name";
            public const string NickName = "nickname";
            public const string PreferredUserName = "preferred_username";
            public const string Profile = "profile";
            public const string Picture = "picture";
            public const string EmailVerified = "email_verified";
            public const string Gender = "gender";
            public const string ZoneInfo = "zoneinfo";
            public const string Locale = "locale";
            public const string PhoneNumber = "phone_number";
            public const string PhoneNumberVerified = "phone_number_verified";
            public const string Address = "address";
            public const string UpdatedAt = "updated_at";

            // JWT header / DPoP claims
            public const string Algorithm = "alg";
            public const string JsonWebKey = "jwk";
            public const string DPoPHttpMethod = "htm";
            public const string DPoPHttpUrl = "htu";
            public const string DPoPAccessTokenHash = "ath";

            public static class JwtTypes
            {
                public const string AccessToken = "at+jwt";
                public const string AuthorizationRequest = "oauth-authz-req+jwt";
                public const string DPoPProofToken = "dpop+jwt";
            }
        }

        public static class ConfirmationMethods
        {
            public const string JsonWebKey = "jwk";
            public const string JwkThumbprint = "jkt";
            public const string X509ThumbprintSha256 = "x5t#S256";
        }
    }
}

