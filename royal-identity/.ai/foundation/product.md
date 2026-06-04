# Product Foundation: RoyalIdentity

## What This System Is

RoyalIdentity is a **multi-tenant OpenID Connect / OAuth 2.0 authorization server** built from scratch in .NET 9. It is a deliberate rearchitecture and spiritual successor to IdentityServer4 (IS4), created to address IS4's limitations: no multi-tenancy, no built-in UI, no independent user management, and a procedural/sequential internal design that resists modification and testing.

The goal is a **complete, turnkey, production-ready product** deployable as a Docker image — comparable in completeness to Keycloak, but built natively on ASP.NET Core with full per-realm customization capability.

---

## Why It Exists (Motivations, Not Marketing)

| IS4 Limitation | RoyalIdentity Decision |
|---|---|
| No multi-tenant (realm) support | Realms are first-class citizens — all data and config is realm-scoped |
| No built-in UI | Complete Razor-based UI: login, consent, logout, admin |
| ASP.NET Identity required for users | Custom user management, realm-specific per design |
| Procedural pipeline, hard to extend | Pipeline architecture: validators/decorators/handlers |
| Tight coupling in tests | Integration-first tests with in-memory storage |

Source of truth for these decisions: ADR-002 through ADR-005.

---

## Realms — The Central Multi-Tenancy Concept

A **Realm** is the top-level organizational boundary. All data and configuration is scoped to a realm.

- **Identified by URL path segment**: `/{realm}/connect/token`, `/{realm}/account/login`
- **Route pattern**: `{realm}/{protocol}/{operation}` and `{realm}/{area}/{action}`
- **A realm owns**: clients, keys, scopes/resources, users, sessions, configuration
- **Internal realms** (server, account, admin) are system-managed and cannot be deleted or have their domain changed
- **Known internal realms**: ServerRealm, AccountRealm, AdminRealm
- **Demo realm** provided in in-memory storage for development

A middleware (`RealmDiscoveryMiddleware`) identifies the realm from the route before any authentication or pipeline processing. The `Realm` model carries `RealmOptions`, which inherits from `ServerOptions` — realm-level config overrides server defaults.

**Implication**: Any feature that accesses data (clients, keys, users, scopes) MUST pass through realm-scoped storage. Cross-realm data access is architecturally forbidden.

---

## OAuth2 / OIDC Capabilities

### Endpoints Implemented

| Endpoint | Path Pattern | Purpose |
|---|---|---|
| Authorization | `/{realm}/connect/authorize` | Initiates all interactive flows |
| Token | `/{realm}/connect/token` | Exchanges credentials/codes for tokens |
| UserInfo | `/{realm}/connect/userinfo` | Returns authenticated user claims |
| Discovery | `/{realm}/.well-known/openid-configuration` | OIDC metadata document |
| JWKS | `/{realm}/connect/discovery/keys` | Public keys for token validation |
| Revocation | `/{realm}/connect/revocation` | Token revocation |
| End Session | `/{realm}/connect/endsession` | RP-initiated logout |
| Check Session | `/{realm}/connect/checksession` | Session state iframe |

### Flows Implemented

- Authorization Code Flow (with mandatory PKCE by default — `RequirePkce = true`)
- Implicit Flow (token, id_token)
- Hybrid Flow
- Client Credentials Flow
- Refresh Token Flow
- Extension Grants (custom via `IExtensionGrant` + `IExtensionsGrantsProvider`)

### PKCE Rules (Client Defaults)

- `RequirePkce = true` by default
- `AllowPlainTextPkce = false` by default (plain method is insecure, must be explicitly enabled)
- S256 is the expected default method

---

## Domain Model: Key Entities

### Client

OAuth2 client registered within a realm. Critical properties:

- `Id` (client_id), `Realm` reference
- `AllowedGrantTypes`, `AllowedResponseTypes`, `AllowedScopes`
- `RedirectUris`, `PostLogoutRedirectUris`
- `RequirePkce`, `AllowPlainTextPkce`
- `RequireClientSecret`, `ClientSecrets`
- `RequireConsent`, `AllowRememberConsent`, `ConsentLifetime`
- Token lifetimes: `AccessTokenLifetime` (600s), `IdentityTokenLifetime` (600s), `AuthorizationCodeLifetime` (60s)
- Refresh token lifetimes: `AbsoluteRefreshTokenLifetime` (30 days), `SlidingRefreshTokenLifetime` (12h)
- `RefreshTokenExpiration` (Absolute or Sliding)
- `RefreshTokenPostConsumedTimeTolerance` (grace period for refresh token reuse after consumption failure)
- `AlwaysSendClientClaims = true` by default
- `AllowOfflineAccess` is marked `[Redesign]` — pending refactor to "AllowedResources" model

### Scope Hierarchy (post-redesign terminology)

The scope model was renamed from IS4 terminology. Mapping:

| Old (IS4) | New (RoyalIdentity) |
|---|---|
| IdentityResources | IdentityScopes |
| ApiScopes | Scopes |
| ApiResources | Resources (or ResourceServer) |

Current model:
- **IdentityScope** — maps to user claims (e.g., `openid` → `sub`; `profile` → name/email claims)
- **ApiScope** — fine-grained operation scope (e.g., read, write)
- **ApiResource / ResourceServer** — a service that exposes resources (e.g., an API)
- **ScopeBase** — common base for all scope types
- **ScopeVisibility** — Hidden, Public, Public_Descriptive (controls discovery document exposure)
- **RequestedScopes** — runtime holder for the space-separated scope string from a request

**Planned further redesign** (from redesign-todo.md): ResourceServer → Resource → Scope hierarchy where requesting a Resource grants all its Scopes, and only the Scope names appear in tokens.

### Tokens

- **AccessToken** — JWT or reference token; contains ClientId, Issuer, Claims, Audiences, Lifetime, JTI, `AccessTokenType`
- **IdentityToken** — carries authentication event claims about the user
- **RefreshToken** — tracks SubjectId, SessionId, AccessTokenId, consumed scopes; has `ConsumedTime` for single-use detection
- **AuthorizationCode** — short-lived (default 60s), single-use code issued in code flows
- **TokenBase** — abstract base with CreationTime, Lifetime, Claims

### Keys

- Realm-scoped signing/validation keys
- `KeyParameters` with NotBefore/Expires dates
- `IKeyStore.ListAllCurrentKeysIdsAsync()` — only keys valid today (for signing)
- `IKeyStore.ListAllKeysIdsAsync()` — all keys including expired (for validation)
- Serialization: XML, JSON, PKCS12 (`KeySerializationFormat`)
- `ValidationKeysInfo` — wraps security keys for JWKS endpoint

---

## Business Rules (Invariants to Preserve)

1. **Realm isolation**: No operation should access data across realm boundaries.
2. **PKCE default-on**: `RequirePkce = true` on all clients unless explicitly overridden.
3. **Single-use codes**: Authorization codes must be consumed exactly once.
4. **Token validation requires key availability**: All keys used for signing must remain in the JWKS endpoint even after expiry (for validation of previously-issued tokens).
5. **Refresh token consumption tolerance**: `RefreshTokenPostConsumedTimeTolerance` allows re-request within a window to handle client-side persistence failures — do not remove this without understanding the client failure scenario.
6. **Consent persistence**: User consent decisions are stored per-user-per-client-per-scope and expire per `Client.ConsentLifetime`.
7. **Internal realms are immutable**: Cannot delete or change domain of realm with `Internal = true`.

---

## Users (Current State — Under Redesign)

Current state (per redesign-todo.md): There is confusion between `IdentityUser`, `UserDetails`, `IUserStore`, `IUserDetailsStore`, `IdentitySession`, and `IUserSessionStore`. This is a known design debt.

**Decision (ADR-005)**: RoyalIdentity will have its own user management, not dependent on ASP.NET Identity. User data and rules are configurable per realm. ASP.NET Identity may be used as reference only.

**Do not assume user/session model is stable**. The redesign-todo explicitly calls for unification.

---

## UI (Current State)

- Built in Razor Components (Blazor Server mode)
- Covers full authentication flows: login, logout, consent
- Admin screens planned: users, clients, realm configuration
- Current known design debt: too much logic inside Razor components — should be extracted into `UILoginService`, `UIConsentService`, etc.
- Localization: all text currently hardcoded in English; localization support is planned but not implemented

---

## Events

`IEventDispatcher` + `IEventObserver` provide an audit trail:
- `UserLoginSuccessEvent`, `UserLoginFailureEvent`, `UserLogoutSuccessEvent`
- `AccessTokenIssuedEvent`, `IdentityTokenIssuedEvent`, `RefreshTokenIssuedEvent`, `CodeIssuedEvent`
- Event categories and types defined in `EventCategories` and `EventTypes`

---

## Known Pending Redesigns (Active Design Debt)

From redesign-todo.md and `[Redesign]` attributes in code:

1. **Scope/Resource model**: `Client.AllowedScopes` and `Client.AllowOfflineAccess` need replacement with an `AllowedResources` model following ResourceServer → Resource → Scope hierarchy
2. **User/session unification**: Merge `IdentityUser`, `UserDetails`, `IUserStore`, `IUserDetailsStore`
3. **UI service extraction**: Move logic from Razor components to dedicated UI services
4. **Localization**: Add localization support for all UI text
