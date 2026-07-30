# Technical Foundation: RoyalIdentity

## Runtime & Framework

- **Target Framework**: .NET 10.0 (`net10.0`), `LangVersion: latest`
- **Global settings** (Directory.Build.props): `ImplicitUsings = enable`, `Nullable = enable`
- **Package version pins**: `AspVer=10.0.0`, `ExtVer=10.0.0`, `IdVer=8.14.0` — all package refs use these properties
- The repo carries an upgrade path through net7.0/net8.0/net9.0 to the current **net10.0**

---

## Key Dependencies

### ASP.NET Core (10.0.0)

- `Microsoft.AspNetCore.Authentication.OpenIdConnect` — OIDC client authentication
- `Microsoft.Extensions.Http.Polly` — HTTP client with retry/circuit breaker for back-channel calls
- Minimal APIs for all OIDC endpoint routing (not MVC controllers)
- Razor Components (Blazor Server) for UI
- Cookie Authentication + custom Policy Scheme for realm-based session management

### Token / Identity (8.14.0)

- `Microsoft.IdentityModel.Protocols.OpenIdConnect` — OIDC protocol types, OpenIdConnectGrantTypes
- `Microsoft.IdentityModel.Tokens` — SecurityKey, token validation parameters
- `System.IdentityModel.Tokens.Jwt` — JwtSecurityToken, signing handlers
- `System.Security.Cryptography` — key operations (RSA, EC)

### Testing

- xUnit (all test projects)
- In-memory storage (no external DB or services required for any test)

---

## Architecture: Pipeline System

The entire request processing path is built around a **type-safe, generic chain-of-responsibility pipeline** defined in `RoyalIdentity.Pipelines`.

### Abstractions (namespace `RoyalIdentity.Pipelines.Abstractions`)

```
IContextBase          — base context: HttpContext + ContextItems + IResponseHandler?
AbstractContextBase   — default implementation of IContextBase
IContextPipeline<T>   — Task SendAsync(T context, CancellationToken ct)
IPipelineDispatcher   — Task SendAsync(IContextBase context, CancellationToken ct)
IHandler<T>           — Task Handle(T context, CancellationToken ct)
IDecorator<T>         — Task Decorate(T context, Func<Task> next, CancellationToken ct)
IValidator<T>         — ValueTask Validate(T context, CancellationToken ct)
IEndpointHandler      — Task<EndpointCreationResult> TryCreateContextAsync(HttpContext)
IResponseHandler      — Task<IResult> CreateResponseAsync(CancellationToken ct)
ContextItems          — type-safe dictionary for passing state through pipeline stages
ResponseContext       — base for response state
EndpointCreationResult — discriminated union: valid context OR error response handler
```

### Chain Execution Semantics

**Validators** (`ValidatorChain<TContext, TValidator, TChain>`):
- Call `validator.Validate(context, ct)`
- Check `context.Response is null` — if set by validator (error case), stop chain
- Short-circuit pattern: validator signals failure by setting `context.Response`, not by throwing

**Decorators** (`DecoratorChain<TContext, TDecorator, TChain>`):
- Call `decorator.Decorate(context, () => next.SendAsync(context, ct), ct)`
- Decorator controls whether to call `next` — full middleware power
- Can pre-process, post-process, or abort

**Handlers** (`HandlerChain<TContext, THandler>`):
- Terminal node: `handler.Handle(context, ct)`
- Must set `context.Response` with a valid `IResponseHandler`

### Pipeline Configuration

Fluent builder via `IPipelineConfigurationBuilder`:
```csharp
builder.For<TContext>()
    .UseDecorator<TDecorator>()  // multiple allowed, ordered
    .UseValidator<TValidator>()  // multiple allowed, ordered
    .UseHandler<THandler>();      // exactly one, terminates chain
```

Pipelines are registered per context type as `IContextPipeline<TContext>` in the DI container.

### Dispatch

`PipelineDispatcher` uses runtime reflection (`MakeGenericType`) to resolve `PipelineDispatcher<TContext>` from the actual context type, then delegates to `IContextPipeline<TContext>`. This allows the top-level `IPipelineDispatcher` to accept any `IContextBase` without knowing the concrete type at the call site.

### HTTP Entry Point

`ServerEndpoint<TEndpoint>` (static class, minimal API delegate):
1. Call `endpointHandler.TryCreateContextAsync(httpContext)` — create typed context from raw HTTP
2. If creation fails → return `responseHandler.CreateResponseAsync()`
3. `pipelineDispatcher.SendAsync(context, ct)` — execute full pipeline
4. `context.Response.CreateResponseAsync(ct)` — generate `IResult`
5. Any exception → `Results.Problem(500)`

`DefaultServerEndpoints.MapServerEndpoint<TEndpoint>(app, pattern)` wraps this as a Minimal API route.

---

## ASP.NET Core Integration

### Realm Middleware

`RealmDiscoveryMiddleware` runs before authentication in the ASP.NET Core pipeline. It:
- Extracts the realm path segment from the route
- Loads `Realm` + `RealmOptions` from `IStorage.GetRealmStore()`
- Stores current realm in `HttpContext` items under `Server.RealmCurrentKey`

### Authentication Scheme

Custom `RealmsAuthenticationSchemeProvider` routes authentication to realm-specific cookie schemes. `RealmAuthenticationHandler` handles authentication per realm. `ConfigureRealmCookieAuthenticationOptions` configures cookie options per realm.

### DI Registration Entry Points

- `IServiceCollection.AddOpenIdConnectProviderServices()` — registers all core services
- `IEndpointRouteBuilder.MapOpenIdConnectProviderEndpoints()` — maps all OIDC routes
- `IApplicationBuilder.UseRealmDiscovery()` — adds realm middleware

---

## Storage & Persistence

### Abstraction Layer (`RoyalIdentity.Contracts.Storage`)

All storage is abstracted. The main gateway is `IStorage`:
- `IStorage.GetClientStore(realm)` → `IClientStore`
- `IStorage.GetResourceStore(realm)` → resource store
- `IStorage.GetKeyStore(realm)` → `IKeyStore`
- `IStorage.Realms` → global realm catalog; every realm-owned store is reached through a `Realm`

Individual store interfaces:
- `IClientStore` — `FindClientByIdAsync`, `FindEnabledClientByIdAsync`
- `IAccessTokenStore` — `StoreAsync`, `GetAsync`, `RemoveAsync`, `RemoveReferenceTokensAsync`
- `IRefreshTokenStore` — store/read/remove plus mandatory conditional `TryConsumeAsync`/`TryUpdateAsync`
- `IAuthorizationCodeStore` — store/read/remove plus mandatory atomic, binding-aware
  `ConsumeAuthorizationCodeAsync`
- `IAuthorizeParametersStore` — stores authorization request parameters (for redirect-based flows)
- `IUserConsentStore` — `StoreUserConsentAsync`, `GetUserConsentAsync`, `RemoveUserConsentAsync`
- `IKeyStore` — `ListAllCurrentKeysIdsAsync`, `ListAllKeysIdsAsync`, `GetKeyAsync`, `GetKeysAsync`
- `IMessageStore` — protected data storage (uses data protection)
- `IReplayProtectionStore` — single-use protection for handles that must never be accepted twice (`TryAddAsync`)

### Entity Framework implementations

`RoyalIdentity.Storage.EntityFramework` adapts the pure `RoyalIdentity.Data.Configuration` and
`RoyalIdentity.Data.Operational` models to the core storage facades. The `.Sqlite` and `.PostgreSql` projects own
provider mappings and migrations. `RoyalIdentity.UserAccounts` owns its separate persistence family and reaches
the IdP only through `.Integration`.

`RoyalIdentity.Server` composes PostgreSQL only and never migrates or seeds. `RoyalIdentity.Migrations` provisions
Configuration, Operational and UserAccounts externally, with one explicit connection per DbContext.
`RoyalIdentity.Demo` and `Tests.Integration` use isolated SQLite in-memory databases; resources/scopes remain a
deliberately volatile bridge pending their redesign.

**Constraint**: Every adapter must satisfy the complete core contracts it exposes. Add persistence behavior to the
owning store/module and its provider-neutral contract suite, never directly to domain services or handlers.

---

## Token Handling

### Factory Layer

`ITokenFactory`:
- `CreateIdentityTokenAsync()` — builds identity token with user claims
- `CreateAccessTokenAsync()` — builds access token with scope claims
- `CreateRefreshTokenAsync()` — builds refresh token linking subject, session, access token

`ICodeFactory.CreateCodeAsync()` — creates authorization code

### JWT Signing

`DefaultJwtFactory` signs tokens using keys from `IKeyStore`. Token claims are assembled from context state. `JwtUtil` provides JWT operations. Tokens are serialized to compact JWS format.

Reference tokens: stored via `IAccessTokenStore`, only a random ID is issued to client.

### Token Validation

`ITokenValidator.ValidateJwtAccessTokenAsync()`:
- Verifies signature against keys from `IKeyStore.ListAllKeysIdsAsync()` (includes expired keys)
- Validates expiry, audience, issuer, scope, `typ` header
- Returns `TokenEvaluationResult` containing `ClaimsPrincipal`

### Token Revocation

`RevocationEndpoint` → `RevocationContext` → `RevocationHandler`:
- Removes access tokens from `IAccessTokenStore`
- Removes refresh tokens from `IRefreshTokenStore`
- Identifies token type via `type_hint` parameter

### Replay Protection

`IReplayProtectionStore` refuses a handle that has already been presented — today the `jti` of a
`private_key_jwt` client assertion, consumed only by `PrivateKeyJwtSecretEvaluator`.

The contract is one operation: `TryAddAsync(realmId, issuer, purpose, handle, expiration, ct)`, an atomic
add-if-absent returning `false` when the handle was already registered. There is deliberately no `ExistsAsync` to
pair with an `AddAsync`: two concurrent callers both pass a check before either writes, which is the very replay
being prevented. Records are keyed by realm **and** issuer, so no client can burn another's identifier. While a
record is retained a conflict answers replay — implementations never consult its expiration — so correctness never
depends on the clock or on pruning.

`AddOpenIdConnectProviderServices()` registers **no default**. Each composition root declares its backing —
`AddInMemoryReplayProtection()` (single instance only; warns on construction) or
`AddOperationalReplayProtection()` (durable, shared by every instance reading the same Operational database) — and
`ReplayProtectionStartupValidator` fails startup, in every environment, when a composition declares none, more
than one, or one inconsistent with the store actually resolved. `WebApplication.CreateBuilder` only enables
container validation in Development, which is why the check is a hosted service and not left to `ValidateOnBuild`.

The durable backing is table `replay_handles`, whose primary key `(realm_id, issuer, purpose, handle_digest)` is
itself the decision: the second insert violates it, and that violation is the answer. The handle is stored as a
`ReplayHandleDigest` — length-prefixed fields, versioned — which deliberately does not reuse
`OperationalLookupDigest`, because that type skips HMAC on the grounds that its handles are generated here with
high entropy and a `jti` is chosen by the client. Cleanup of replay handles is strict (`ExpiresAtUtc < now`): a
handle's expiration already includes the tolerated clock skew, and the artifact is still acceptable at that exact
instant.

How far ahead an assertion may claim to expire is capped by `Authentication.ClientAssertionMaxLifetime` (default
10 minutes; accepted range 1 second to 1 hour), so the record's retention is a server value and not the client's
choice. See [plan-replay-protection.md](../plans/plan-replay-protection.md).

---

## Security Model

### Client Authentication

`IClientSecretChecker` (singleton) chains evaluators (`IClientSecretEvaluator`). Methods:
- HTTP Basic (Authorization header `client_id:client_secret`)
- POST body (`client_id` + `client_secret` parameters)
- `private_key_jwt` — JWT signed by client's private key
- `tls_client_auth` — mutual TLS
- No secret (public clients, `RequireClientSecret = false`)

`EvaluateClient` decorator handles client authentication in token endpoint pipelines.

### Bearer Token Evaluation

`EvaluateBearerToken` decorator for UserInfo and other endpoints requiring authenticated tokens. Resolves and validates bearer from Authorization header, stores result in `ContextItems`.

### PKCE

Enforced via `PkceValidator` (checks parameters present when required) and `PkceMatchValidator` (verifies code_verifier against stored code_challenge). Validators are part of the authorization code redemption pipeline.

---

## Configuration System

### Options Hierarchy

```
ServerOptions
└── RealmOptions (contains ServerOptions reference)
    ├── DiscoveryOptions
    ├── EndpointsOptions (enable/disable per endpoint)
    ├── KeyOptions
    ├── AuthenticationOptions (cookie lifetime, scheme)
    ├── InputLengthRestrictions (max lengths for all OIDC params)
    ├── CspOptions
    ├── MutualTlsOptions
    ├── UIOptions (paths: LoginPath, LogoutPath, ConsentPath, etc.)
    ├── LoggingOptions
    └── AccountOptions (IdP account-flow/UI settings only)
```

`RealmOptions` contains a `ServerOptions` reference for server-wide defaults. Realm-level settings override server defaults. `EndpointContextBase.Options` exposes `RealmOptions`; `EndpointContextBase.ServerOptions` exposes the root `ServerOptions`.
Rich account policies such as registration, profile changes, email login, duplicate email, fixed-field claim projection,
and password/lockout rules belong to `RoyalIdentity.UserAccounts.Options.UserAccountsRealmOptions`.

### EndpointsOptions

Explicit enable/disable flags for each endpoint. Future feature work that introduces a new endpoint must add its flag here. Disabled endpoints should return 404.

---

## Observability

- Structured logging via `ILogger<T>` — injected into all handlers, endpoint wrappers
- Exception boundaries in `ServerEndpoint<TEndpoint>` log errors before returning 500
- `IEventDispatcher` / `IEventObserver` — domain events for audit trail (token issued, login, logout)
- No distributed tracing / OpenTelemetry observed in current implementation

---

## Build & Test

### Build

- `Directory.Build.props` — root-level shared properties (framework, nullable, package versions)
- `tests.targets` — shared test configuration imported by all test projects
- Standard `dotnet build` / `dotnet test`; `scripts/Test-*PostgreSql.ps1` provide disposable local opt-in
  acceptances, and `Aspire/Aspire.AppHost` provides the persistent local orchestration

### Tests

- All tests use xUnit.
- `Tests.Integration` runs by default with EF/SQLite plus real `UserAccounts`; each fixture owns its databases and
  Data Protection material.
- `Tests.Storage` runs the provider-neutral contracts, migrations, concurrency and gateway suites on SQLite by
  default and PostgreSQL 17 through explicit local opt-in scripts.
- The solution-wide default remains self-contained: PostgreSQL and Aspire acceptances are opt-in and skipped when
  their environment variables are absent.
- Test projects include `Tests.Pipelines`, `Tests.Identity`, `Tests.Security`, `Tests.Storage`,
  `Tests.UserAccounts`, `Tests.Integration`, `Tests.Architecture`, `Tests.Host`, `Tests.WebApp` and `Aspire.Tests`.
- `Tests.Identity/read.md`: "focus on unit level and, when necessary, integration level — contexts, validators, decorators, handlers and default service implementations will be tested"

---

## HTTP Client (Back-Channel)

`Microsoft.Extensions.Http.Polly` is used for back-channel HTTP calls (e.g., `IBackChannelLogoutNotifier` sending logout notifications to registered URIs). Polly provides retry + circuit breaker policies. Registered via `IServiceCollection.AddHttpClient()` + Polly extension.

---

## Protocol Constants

All OIDC/OAuth2/JWT protocol strings live in `RoyalIdentity/Options/Constants.cs` (single static partial class, multiple files):

- `Constants.Oidc.*` — OIDC spec parameter names, response types, grant types, error codes
- `Constants.Server.*` — server-specific identifiers, cookie names, realm keys
- `Constants.Jwt.ClaimTypes.*` — project-specific JWT claims not in `JwtRegisteredClaimNames`
- `Constants.Jwt.ConfirmationMethods.*` — DPoP/mTLS confirmation methods

Standard JWT claim names (`sub`, `aud`, `iss`, `exp`, etc.) come from `JwtRegisteredClaimNames` (via `global using System.IdentityModel.Tokens.Jwt` in `Global.Usings.cs`). Never add duplicates to `Constants`.

> Legacy classes `OidcConstants`, `ServerConstants`, and `JwtClaimTypes` were deleted in the constants consolidation refactoring. Do not re-introduce them.

---

## Patterns to Follow Consistently

1. **New endpoint**: create `*Endpoint : IEndpointHandler`, context class inheriting `EndpointContextBase`, register in `AddOpenIdConnectProviderServices`, add route in `MapOpenIdConnectProviderEndpoints`
2. **New pipeline step**: implement `IValidator<T>`, `IDecorator<T>`, or `IHandler<T>` → register in DI → add to `builder.For<T>()` chain
3. **New storage**: evolve the owner contract deliberately, implement the EF/module providers, update the
   provider-neutral contracts in `Tests.Storage`, and preserve realm isolation
4. **New option**: add property to appropriate `*Options` class; for realm-specific config, add to `RealmOptions`; for server-wide config, add to `ServerOptions`
5. **Error signaling in validators**: set `context.Response` — never throw for expected validation failures
6. **Decorator abort**: do not call `next()` to abort pipeline from a decorator
7. **New UI page**: inject `I*PageService` in Razor component, call `GetViewModelAsync()` on GET and `ProcessAsync()` on POST — no business logic in components
