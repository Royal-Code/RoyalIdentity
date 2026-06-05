# Structural Foundation: RoyalIdentity

## Solution Layout

```
royal-identity/
├── .ai/foundation/          ← AI context files (this directory)
├── RoyalIdentity/           ← Core library: domain, contracts, contexts, handlers, endpoints
├── RoyalIdentity.Pipelines/ ← Pipeline infrastructure: abstractions, chains, dispatcher, mapping
├── RoyalIdentity.Storage.InMemory/ ← In-memory storage implementation
├── RoyalIdentity.Razor/     ← UI: Razor Components (Blazor Server)
├── RoyalIdentity.Server/    ← Host: WebApplication, Program.cs, config
├── RoyalIdentity.Users/     ← (directory exists, no active csproj — planned)
├── RoyalIdentity.Web/       ← (directory exists, no active csproj — planned)
├── Tests.Pipelines/         ← Unit tests for pipeline infrastructure
├── Tests.Integration/       ← End-to-end flow tests
├── Tests.Host/              ← Hosted application tests
├── Tests.WebApp/            ← Web UI tests
├── Tests.Identity/          ← Identity/user tests
├── Tests.Endpoints/         ← Endpoint creation tests
├── ADR-002.md through ADR-005.md
├── redesign-todo.md
└── Directory.Build.props    ← Root shared build config
```

---

## Project Dependency Graph

```
RoyalIdentity.Server
  ├── RoyalIdentity.Razor
  ├── RoyalIdentity.Storage.InMemory
  └── RoyalIdentity
        └── RoyalIdentity.Pipelines    (no dependencies — base layer)
```

`RoyalIdentity.Pipelines` is the **only leaf project with no dependencies**. It defines all pipeline abstractions and infrastructure. Everything builds on it.

`RoyalIdentity` depends only on `RoyalIdentity.Pipelines` and Microsoft.IdentityModel + ASP.NET Core packages. It must not take dependencies on storage implementations.

`RoyalIdentity.Storage.InMemory` depends on `RoyalIdentity` — the only layer allowed to reference `IStorage` implementations.

---

## RoyalIdentity.Pipelines — Internal Structure

```
Abstractions/
  IContextBase.cs           ← Root context interface
  AbstractContextBase.cs    ← Default impl (HttpContext, ContextItems, IResponseHandler?)
  IContextPipeline.cs       ← SendAsync(TContext, CancellationToken)
  IPipelineDispatcher.cs    ← SendAsync(IContextBase, CancellationToken)
  IHandler.cs               ← Handle(TContext, CancellationToken)
  IDecorator.cs             ← Decorate(TContext, Func<Task> next, CancellationToken)
  IValidator.cs             ← Validate(TContext, CancellationToken) : ValueTask
  IEndpointHandler.cs       ← TryCreateContextAsync(HttpContext) : EndpointCreationResult
  IResponseHandler.cs       ← CreateResponseAsync(CancellationToken) : Task<IResult>
  ContextItems.cs           ← Type-safe dictionary for pipeline state
  ResponseContext.cs        ← Base for response state
  EndpointCreationResult.cs ← Discriminated union: context | error handler
  EndpointErrorResults.cs   ← Standard error response handlers
  ErrorResponseParameters.cs

Configurations/
  IPipelineConfigurationBuilder.cs  ← Fluent builder interface
  PipelineConfigurationBuilder.cs   ← Implementation
  DefaultPipelineConfigurationBuilder.cs

Infrastructure/
  PipelineDispatcher.cs             ← Runtime type dispatch via MakeGenericType
  ValidatorChain.cs                 ← short-circuit on context.Response != null
  DecoratorChain.cs                 ← full middleware: controls next() invocation
  HandlerChain.cs                   ← terminal, always calls handler
  PipelineExtensions.cs
  Builder/
    ChainBuilder.cs
    DecoratorBuilder.cs
    HandlerBuilder.cs
    ValidatorBuilder.cs
    ICompletable.cs

Defaults/
  ResponseHandler.cs         ← Default response writer
  ErrorResponseResult.cs     ← Standard error IResult
  OkHandler.cs               ← Default OK response
  ProblemsExtensions.cs      ← ProblemDetails helpers

Mapping/
  DefaultServerEndpoints.cs  ← MapServerEndpoint<TEndpoint> extension
  ServerEndpoint.cs          ← Static class with EndpointHandler delegate (the actual request flow)
```

---

## RoyalIdentity — Internal Structure

```
Authentication/
  RealmDiscoveryMiddleware.cs            ← Extracts realm from route, populates HttpContext
  RealmsAuthenticationSchemeProvider.cs  ← Routes auth to per-realm cookie scheme
  RealmAuthenticationHandler.cs         ← Per-realm auth handler
  ConfigureRealmCookieAuthenticationOptions.cs

Contexts/                               ← One context class per endpoint operation
  EndpointContextBase.cs                ← Base: Raw (NameValueCollection), Realm, Options
  IEndpointContextBase.cs
  IAuthorizationContextBase.cs          ← For auth-code/implicit flows (ResponseMode, Nonce, etc.)
  ITokenEndpointContextBase.cs          ← For token endpoint (GrantType, Subject getter)
  AuthorizeContext.cs                   ← /connect/authorize
  AuthorizeValidateContext.cs           ← Authorize request validation context
  AuthorizationCodeContext.cs           ← Code redemption
  ClientCredentialsContext.cs           ← Client credentials grant
  RefreshTokenContext.cs                ← Refresh token grant
  RevocationContext.cs                  ← Token revocation
  EndSessionContext.cs                  ← Logout
  UserInfoContext.cs                    ← UserInfo endpoint
  DiscoveryContext.cs                   ← .well-known endpoint
  JwkContext.cs                         ← JWKS endpoint

  Decorators/                           ← Cross-cutting context enrichment
    LoadClient.cs                       ← Loads Client from store into context
    EvaluateClient.cs                   ← Validates client credentials
    EvaluateBearerToken.cs              ← Validates bearer token for protected endpoints
    LoadCode.cs                         ← Loads authorization code
    LoadRefreshToken.cs                 ← Loads refresh token
    ResourcesDecorator.cs               ← Loads requested scopes/resources
    ClientResourceDecorator.cs          ← Enriches with client's allowed resources
    ConsentDecorator.cs                 ← Consent flow intercept
    PromptLoginDecorator.cs             ← Handles prompt=login
    StateHashDecorator.cs               ← Calculates session state hash
    EndSessionDecorator.cs              ← End-session flow enrichment
    ProcessRequestObject.cs             ← Handles JAR (Request Object)

  Validators/                           ← Validation-only; set context.Response on failure
    AuthorizeMainValidator.cs           ← client_id, response_type, client enabled
    RedirectUriValidator.cs             ← redirect_uri vs registered URIs
    AuthorizationResourcesValidator.cs  ← requested scopes vs allowed scopes
    PkceValidator.cs                    ← PKCE params present when required
    PkceMatchValidator.cs               ← code_verifier vs stored challenge
    GrantTypeValidator.cs               ← grant_type vs client's AllowedGrantTypes
    ResourcesValidator.cs               ← resource validation
    ActiveUserValidator.cs              ← user is still active (IProfileService.IsActiveAsync)
    RevocationValidator.cs              ← token revocation request params
    EndSessionValidator.cs              ← end session request params
    ResponseTypeEqualityComparer.cs     ← Utility: normalize response_type comparison

  Items/                                ← Named context item wrappers
    Token.cs                            ← Token item holder (used for log obfuscation)

  Parameters/                           ← Named parameter extractors
    BearerParameters.cs
    ClientParameters.cs
    CodeParameters.cs
    RefreshParameters.cs

  Withs/                                ← Capability interfaces for contexts
    IWithClient.cs                      ← Context has loaded Client
    IWithResources.cs                   ← Context has loaded resources
    IWithAuthorizationCode.cs           ← Context has loaded auth code
    IWithRefreshToken.cs                ← Context has loaded refresh token
    IWithBearerToken.cs                 ← Context has bearer token result
    IWithCodeChallenge.cs               ← Context has PKCE code challenge
    IWithRedirectUri.cs                 ← Context has redirect URI
    IWithPrompt.cs                      ← Context has prompt parameter
    IWithAcr.cs                         ← Context has ACR values

Contracts/                              ← Service interfaces (domain boundary)
  ITokenFactory.cs
  ICodeFactory.cs
  IProfileService.cs
  IClientSecretChecker.cs
  IClientSecretEvaluator.cs
  IAuthorizeRequestValidator.cs
  IExtensionGrant.cs
  IExtensionsGrantsProvider.cs
  IBackChannelLogoutNotifier.cs
  IEventDispatcher.cs
  IEventObserver.cs
  IRedirectUriValidator.cs
  ISessionStateGenerator.cs
  IServerJob.cs

  Storage/
    IClientStore.cs
    IAccessTokenStore.cs
    IRefreshTokenStore.cs
    IAuthorizationCodeStore.cs
    IAuthorizeParametersStore.cs
    IUserConsentStore.cs
    IKeyStore.cs
    IMessageStore.cs
    IReplayCache.cs

  Defaults/                             ← Default implementations of contracts
    DefaultServerJobsStartup.cs
    DefaultReplayDistributedCache.cs
    DefaultReplayNoCache.cs
    ProtectedDataMessageStore.cs
    SecretsEvaluators/
    Jobs/

  Models/
    AuthorizationValidationResult.cs
    TokenEvaluationResult.cs
    BearerTokenResult.cs
    EvaluatedCredential.cs
    EvaluatedToken.cs
    Messages/
      Message.cs
      ErrorMessage.cs

Endpoints/                              ← IEndpointHandler implementations (HTTP → context)
  AuthorizeEndpoint.cs
  AuthorizeCallbackEndpoint.cs
  TokenEndpoint.cs
  UserInfoEndpoint.cs
  DiscoveryEndpoint.cs
  JwkEndpoint.cs
  RevocationEndpoint.cs
  EndSessionEndpoint.cs
  CheckSessionEndpoint.cs

Handlers/                               ← IHandler<TContext> implementations (terminal pipeline step)
  AuthorizeHandler.cs
  AuthorizationCodeHandler.cs
  ClientCredentialsHandler.cs
  RefreshTokenHandler.cs
  UserInfoHandler.cs
  DiscoveryHandler.cs
  JwkHandler.cs
  RevocationHandler.cs
  EndSessionHandler.cs

Models/
  Realm.cs + RealmRoutes              ← Multi-tenancy root entity
  Client.cs                            ← OAuth2 client
  ClientSecret.cs
  Consent.cs
  ErrorDetails.cs
  TokenExpiration.cs (Absolute/Sliding enum)

  Tokens/
    TokenBase.cs
    AccessToken.cs
    IdentityToken.cs
    RefreshToken.cs
    AuthorizationCode.cs
    AccessTokenType.cs (Jwt/Reference enum)

  Keys/
    KeyParameters.cs
    KeyEncoding.cs
    KeySerializationFormat.cs
    ValidationKeysInfo.cs

  Scopes/
    ScopeBase.cs
    IdentityScope.cs
    ApiScope.cs
    ApiResource.cs
    ResourceServer.cs
    RequestedScopes.cs
    AllScopes.cs
    ScopeType.cs
    ScopeVisibility.cs

Options/
  ServerOptions.cs
  RealmOptions.cs
  DiscoveryOptions.cs
  EndpointsOptions.cs
  KeyOptions.cs
  AuthenticationOptions.cs
  InputLengthRestrictions.cs
  CspOptions.cs + CspLevel.cs
  MutualTlsOptions.cs
  LoggingOptions.cs

Events/
  Event.cs (base)
  EventCategories.cs
  EventTypes.cs
  AccessTokenIssuedEvent.cs
  IdentityTokenIssuedEvent.cs
  RefreshTokenIssuedEvent.cs
  CodeIssuedEvent.cs
  UserLoginSuccessEvent.cs
  UserLoginFailureEvent.cs
  UserLogoutSuccessEvent.cs

Extensions/                             ← Extension methods on framework types
  HttpRequestExtensions.cs
  HttpResponseExtensions.cs
  SecurityKeyExtensions.cs
  ClientExtensions.cs
  DateTimeExtensions.cs
  WildcardExtensions.cs

Responses/HttpResults/                  ← IResult implementations for OIDC responses
  TokenResult.cs
  TokenEndpointParameters.cs
  UserInfoResult.cs
  JwkResult.cs
  CheckSessionResult.cs
  ResponseToQueryResult.cs
  ResponseToFragmentResult.cs
  ResponseToFormPostResult.cs

Users/                                  ← User management (under redesign)
  Contracts/
    IUserStore.cs
    IUserDetailsStore.cs
    IPasswordProtector.cs
  Contexts/                             ← User flow contexts
  Defaults/
  CredentialsValidationResult.cs
  ISignOutManager.cs

Utils/
  Base64Url.cs
  CryptoRandom.cs
  Json.cs
  JwtUtil.cs (inferred — JWT operations)
  PasswordHash.cs
  TimeConstantComparer.cs
  ClaimComparer.cs
  NameType.cs
  X509.cs
  Caching/
    ICached.cs
    CachedValue.cs
    DefaultCached.cs
    CacheOptions.cs
    CacheDictionary.cs
    CacheExtensions.cs
```

---

## RoyalIdentity.Razor — UI Project Structure

Razor Components (Blazor) for the account-facing UI (`/{realm}/account/*`).

> **Rendering mode**: account pages use **Static Server Rendering (SSR)** — `RenderModeForPage` in `App.razor` returns `null` for `IsAccountPages()`, not `InteractiveServer`. `Scoped` services have **HTTP request lifetime** (not circuit lifetime). GET and POST are separate instances.

```
Components/
  Account/
    SignIn/
      LoginPage.razor          ← GET/POST handler for username/password login
      LocalLogin.razor         ← Login form fragment
      SignedIn.razor           ← Post-login redirect/confirmation
      ExternalLoginPicker.razor← External IdP selection
    Consenting/
      ConsentPage.razor        ← GET/POST handler for user consent
    EndSession/
      LoggingOutPage.razor     ← Logout confirmation / in-progress page
  Layout/
    AccountLayout.razor        ← Shared layout for account pages

Services/                      ← UI page services (all registered Scoped)
  ISessionContextService.cs    ← Resolves AuthorizationContext for current request
  SessionContextService.cs
  ILoginPageService.cs         ← Login flow: load ViewModel, process POST
  LoginPageService.cs
  IConsentPageService.cs       ← Consent flow: load ViewModel, process POST
  ConsentPageService.cs
  IEndSessionPageService.cs    ← End-session flow: load ViewModel, process POST
  EndSessionPageService.cs
  IdentityRedirectManager.cs   ← Utility: NavigationManager wrapper for SSR redirects
  IdentityUserManager.cs       ← Thin wrapper: UserManager<T> usage

ViewModels/                    ← Data transfer objects for page rendering
  LoginViewModel.cs            ← What LoginPage.razor needs to render
  LoginInputModel.cs           ← Form fields from login POST
  LoginResult.cs               ← Outcome of LoginPageService.ProcessAsync()
  ConsentViewModel.cs          ← What ConsentPage.razor needs to render
  ConsentInputModel.cs         ← Form fields from consent POST
  ConsentResult.cs             ← Outcome of ConsentPageService.ProcessAsync()
  LogoutViewModel.cs           ← What LoggingOutPage.razor needs to render
  LogoutInputModel.cs          ← Form fields (logout_id from query/form)
  LoggingOutViewModel.cs       ← Intermediate rendering model for logout confirmation
  LogoutResult.cs              ← Outcome of EndSessionPageService.ProcessAsync()
  ExternalProvider.cs          ← DTO for an external login provider option

Extensions/
  RoyalIdentityHttpContextExtensions.cs  ← IsAccountPages(), GetRealm(), etc.
```

**Service pattern** for UI pages: each Razor component injects the matching `I*PageService`, calls `GetViewModelAsync()` on GET and `ProcessAsync()` on POST. The service calls into `ISessionContextService` (resolves `AuthorizationContext`), then uses the core pipeline/stores to do work, and returns a typed `*Result` that the component converts to a redirect or re-render. Components contain no business logic.

---

## Architectural Layers & Allowed Dependencies

```
Layer 4: RoyalIdentity.Server (host, wiring)
    ↓
Layer 3: RoyalIdentity.Razor (UI) + RoyalIdentity.Storage.InMemory (data)
    ↓
Layer 2: RoyalIdentity (domain, contracts, endpoints, handlers, contexts)
    ↓
Layer 1: RoyalIdentity.Pipelines (infrastructure, no domain knowledge)
```

**Dependency rules** (inferred from csproj):
- `RoyalIdentity.Pipelines` → no project references (foundation layer)
- `RoyalIdentity` → only `RoyalIdentity.Pipelines`
- `RoyalIdentity.Storage.InMemory` → `RoyalIdentity`
- `RoyalIdentity.Razor` → `RoyalIdentity`
- `RoyalIdentity.Server` → all above

**Forbidden**: `RoyalIdentity` must NOT reference `RoyalIdentity.Storage.InMemory` or `RoyalIdentity.Server`. `RoyalIdentity.Pipelines` must NOT reference `RoyalIdentity`.

---

## Naming Conventions

| Type | Pattern | Example |
|---|---|---|
| Context | `{Operation}Context` | `AuthorizeContext`, `TokenEndpointContextBase` |
| Endpoint handler | `{Operation}Endpoint` | `AuthorizeEndpoint`, `TokenEndpoint` |
| Terminal handler | `{Operation}Handler` | `AuthorizeHandler`, `ClientCredentialsHandler` |
| Validator | `{Concern}Validator` | `PkceValidator`, `AuthorizeMainValidator` |
| Decorator | verb/noun describing action | `LoadClient`, `EvaluateBearerToken`, `ConsentDecorator` |
| Store interface | `I{Entity}Store` | `IClientStore`, `IAccessTokenStore` |
| Service interface | `I{Operation}` or `I{Noun}` | `ITokenFactory`, `IProfileService` |
| Options class | `{Concern}Options` | `RealmOptions`, `DiscoveryOptions` |
| Event | `{Action}Event` | `UserLoginSuccessEvent`, `AccessTokenIssuedEvent` |
| Constants class | `Constants.{Group}.*` | `Constants.Oidc.Authorize.Request`, `Constants.Server` |
| "With" interface | `IWith{Capability}` | `IWithClient`, `IWithCodeChallenge` |

Constants: all protocol strings live in the single `Constants` static class (`RoyalIdentity/Options/Constants.cs`), organized by nested static classes: `Constants.Oidc.*` (OIDC/OAuth2 spec values), `Constants.Server.*` (server-specific), `Constants.Jwt.*` (JWT claim types not in `JwtRegisteredClaimNames`). Use `JwtRegisteredClaimNames.*` for standard JWT claims (Sub, Aud, Iss, etc.) — they come from `System.IdentityModel.Tokens.Jwt` which has a `global using` in `Global.Usings.cs`. Never use string literals for protocol values.

---

## Conventions for Adding New Features

### New OIDC Endpoint

1. Create `*Context : EndpointContextBase` in `RoyalIdentity/Contexts/`
2. Create `*Endpoint : IEndpointHandler` in `RoyalIdentity/Endpoints/` — parses HTTP request, creates context
3. Create `*Handler : IHandler<*Context>` in `RoyalIdentity/Handlers/` — main business logic
4. Add validators in `RoyalIdentity/Contexts/Validators/`
5. Add decorators in `RoyalIdentity/Contexts/Decorators/`
6. Register pipeline in `AddOpenIdConnectProviderServices()` using `builder.For<*Context>()`
7. Add route in `MapOpenIdConnectProviderEndpoints()` using `app.MapServerEndpoint<*Endpoint>(pattern)`
8. Add `EnableXxxEndpoint` flag to `EndpointsOptions`

### New Decorator

- Implement `IDecorator<TContext>` where `TContext` is the target context type
- May need `TContext` to implement a `IWith*` interface to access optional state
- Register as transient in DI
- Insert in pipeline configuration `builder.For<TContext>().UseDecorator<NewDecorator>()`
- If decorator needs to abort pipeline: do not call `next()`
- If decorator signals an error: set `context.Response` before returning

### New Validator

- Implement `IValidator<TContext>`, return `ValueTask`
- Signal validation failure: set `context.Response` with appropriate error result (never throw)
- The chain automatically stops when `context.Response != null`
- Register as transient in DI
- Insert in pipeline configuration after relevant decorators

### New Storage Operation

1. Add method to relevant `I*Store` interface in `RoyalIdentity/Contracts/Storage/`
2. Implement in `RoyalIdentity.Storage.InMemory` for `MemoryStorage`
3. Any future SQL/Redis implementation must implement all store interfaces

### New Option

- Realm-level: add property to `RealmOptions`
- Server-level: add property to `ServerOptions`
- Feature-level: add new `*Options` class, reference from `RealmOptions` or `ServerOptions`

---

## Cross-Cutting Concerns

| Concern | Mechanism |
|---|---|
| Realm isolation | `HttpContext.GetCurrentRealm()` from `RealmDiscoveryMiddleware` |
| Error responses | Set `context.Response` in validators/decorators; `EndpointCreationResult` for pre-pipeline errors |
| Token passing in pipeline | `ContextItems` (type-safe dictionary on `IContextBase`) |
| Context capability declaration | `IWith*` interfaces (e.g., `IWithClient`) on context classes |
| Logging | `ILogger<T>` injected per class |
| Events | `IEventDispatcher.RaiseAsync()` |
| Caching | `ICached<T>`, `CacheDictionary<K,V>`, wrapping `IMemoryCache` |
| Replay protection | `IReplayCache.AddAsync()` / `ExistsAsync()` |
| Data protection | `ProtectedDataMessageStore` (wraps ASP.NET Core Data Protection) |

---

## Critical Files — High Change Risk

| File | Why It's Critical |
|---|---|
| `RoyalIdentity.Pipelines/Infrastructure/ValidatorChain.cs` | Short-circuit logic — changing `context.Response is null` check breaks all validation |
| `RoyalIdentity.Pipelines/Infrastructure/DecoratorChain.cs` | Controls whether decorators can abort pipeline |
| `RoyalIdentity.Pipelines/Mapping/ServerEndpoint.cs` | Request entry point for all OIDC endpoints |
| `RoyalIdentity/Contexts/EndpointContextBase.cs` | Base for all endpoint contexts — realm access |
| `RoyalIdentity/Authentication/RealmDiscoveryMiddleware.cs` | Realm resolution — all subsequent code depends on this |
| `RoyalIdentity/Models/Client.cs` | Core domain model — many validators and handlers read from it |
| `RoyalIdentity/Options/RealmOptions.cs` | Configuration root per realm |
| `RoyalIdentity/Contracts/Storage/*.cs` | Storage interface contracts — all implementations must satisfy |

---

## Areas Under Active Redesign (Do Not Stabilize)

From `redesign-todo.md` and `[Redesign]` attributes in code:

- `Client.AllowedScopes` and `Client.AllowOfflineAccess` — to be replaced by `AllowedResources` model
- `RoyalIdentity/Users/` — user/session model needs unification (not stable)
- `RoyalIdentity/Models/Scopes/ResourceServer.cs` and related — scope hierarchy redesign in progress
Do not add heavy logic to these areas without consulting current context on whether redesign is still pending.
