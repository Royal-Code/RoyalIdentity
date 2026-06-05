# Technical Foundation: RoyalIdentity

## Runtime & Framework

- **Target Framework**: .NET 9.0 (`net9.0`), `LangVersion: latest`
- **Global settings** (Directory.Build.props): `ImplicitUsings = enable`, `Nullable = enable`
- **Package version pins**: `AspVer=9.0.10`, `IdVer=8.14.0` — all package refs use these properties
- The project has compiled artifacts for net7.0, net8.0, and net9.0 in `obj/` dirs, indicating a recent upgrade path

---

## Key Dependencies

### ASP.NET Core (9.0.10)

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
- `IStorage.GetRealmStore()` → realm data

Individual store interfaces:
- `IClientStore` — `FindClientByIdAsync`, `FindEnabledClientByIdAsync`
- `IAccessTokenStore` — `StoreAsync`, `GetAsync`, `RemoveAsync`, `RemoveReferenceTokensAsync`
- `IRefreshTokenStore` — CRUD for refresh tokens
- `IAuthorizationCodeStore` — `StoreAuthorizationCodeAsync`, `GetAuthorizationCodeAsync`, `RemoveAuthorizationCodeAsync`
- `IAuthorizeParametersStore` — stores authorization request parameters (for redirect-based flows)
- `IUserConsentStore` — `StoreUserConsentAsync`, `GetUserConsentAsync`, `RemoveUserConsentAsync`
- `IKeyStore` — `ListAllCurrentKeysIdsAsync`, `ListAllKeysIdsAsync`, `GetKeyAsync`, `GetKeysAsync`
- `IMessageStore` — protected data storage (uses data protection)
- `IReplayCache` — nonce/code replay prevention

### In-Memory Implementation (`RoyalIdentity.Storage.InMemory`)

`MemoryStorage` uses `ConcurrentDictionary` for all stores. Pre-seeds four realms: Server, Account, Admin, Demo. This is the only current storage implementation — production persistence (SQL, etc.) is not yet implemented.

**Constraint**: Any new storage implementation must implement all store interfaces from `IStorage` downward. Never add persistence logic directly to domain services.

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

`IReplayCache` prevents replay of nonces and one-time tokens. Two implementations:
- `DefaultReplayDistributedCache` — uses distributed cache (production)
- `DefaultReplayNoCache` — no replay protection (development/testing only)

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
    └── AccountOptions (per-realm account settings)
```

`RealmOptions` contains a `ServerOptions` reference for server-wide defaults. Realm-level settings override server defaults. `EndpointContextBase.Options` exposes `RealmOptions`; `EndpointContextBase.ServerOptions` exposes the root `ServerOptions`.

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
- Standard `dotnet build` / `dotnet test` — no custom scripts observed

### Tests

- All xUnit, in-memory storage
- ADR-003 mandates: integration-focused, no external dependencies, no database
- Test projects: `Tests.Pipelines`, `Tests.Integration`, `Tests.Host`, `Tests.WebApp`, `Tests.Identity`, `Tests.Endpoints`
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
3. **New storage**: add interface to `IStorage`, implement in `MemoryStorage` first
4. **New option**: add property to appropriate `*Options` class; for realm-specific config, add to `RealmOptions`; for server-wide config, add to `ServerOptions`
5. **Error signaling in validators**: set `context.Response` — never throw for expected validation failures
6. **Decorator abort**: do not call `next()` to abort pipeline from a decorator
7. **New UI page**: inject `I*PageService` in Razor component, call `GetViewModelAsync()` on GET and `ProcessAsync()` on POST — no business logic in components
