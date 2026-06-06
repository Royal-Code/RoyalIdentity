# AGENTS.md

This file provides persistent guidance for Codex when working in this repository.
Keep it focused on rules that should apply to every session. For deeper product
or architecture context, read the foundation files listed below.

## Start Here

Before significant code changes, read:

- `.ai/foundation/product.md` for product goals, OAuth2/OIDC flows, realm rules, business invariants, and active design debt.
- `.ai/foundation/tech.md` for runtime, pipeline semantics, storage abstraction, token handling, and implementation patterns.
- `.ai/foundation/structure.md` for project layout, dependency rules, naming conventions, and high-risk files.

Before modifying an area touched by a plan, inspect `.ai/plans/` first. As of this
repo snapshot, `plan-realm-hardening.md` is completed and
`plan-realm-options-redesign.md` is planned. Deferred product/design notes live in
`.ai/backlogs/backlog-001.md`.

## Commands

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
dotnet test Tests.Pipelines
dotnet test Tests.Identity
dotnet test Tests.Integration
dotnet test Tests.Pipelines --filter "FullyQualifiedName~PipelineDispatcher_Must_Dispatch"
dotnet run --project RoyalIdentity.Server
```

Run the narrowest relevant test project after focused changes. Run
`dotnet test RoyalIdentity.sln` for cross-cutting pipeline, realm, token, storage,
or UI flow changes.

## Product And Architecture

RoyalIdentity is a multi-tenant OpenID Connect / OAuth2 authorization server in
.NET 9. Realms are the top-level isolation boundary. Every data access involving
clients, keys, users, sessions, scopes, tokens, consents, or configuration must be
realm-scoped unless a foundation document explicitly says otherwise.

Every HTTP request flows through:

1. `IEndpointHandler` to parse raw HTTP and create a typed context.
2. A pipeline of `IDecorator<T>`, `IValidator<T>`, and `IHandler<T>`.
3. An `IResponseHandler` set on the context to produce the final HTTP response.

The ASP.NET Core middleware order matters. `UseRealmDiscovery` must run before
`UseAuthentication` because it resolves the `{realm}` route segment and puts the
current realm into `HttpContext` for downstream code.

## Project Boundaries

Respect the dependency layers:

- `RoyalIdentity.Pipelines` is the dependency-free pipeline infrastructure layer.
- `RoyalIdentity` depends on `RoyalIdentity.Pipelines` and owns domain,
  contracts, contexts, handlers, endpoints, options, events, and users.
- `RoyalIdentity.Storage.InMemory` implements storage and depends on
  `RoyalIdentity`.
- `RoyalIdentity.Razor` owns account UI components and UI services.
- `RoyalIdentity.Server` is the host and wiring layer.

Do not make `RoyalIdentity` depend on `RoyalIdentity.Storage.InMemory`,
`RoyalIdentity.Server`, or UI projects. Do not make `RoyalIdentity.Pipelines`
depend on domain code.

## Pipeline Rules

Use the established pipeline registration pattern:

```csharp
services.AddPipelines(builder =>
{
	builder.For<SomeContext>()
		.UseDecorator<LoadClient>()
		.UseValidator<AuthorizeMainValidator>()
		.UseHandler<AuthorizeHandler>();
});
```

Validators signal expected failures by setting `context.Response`; they should not
throw for validation errors. `ValidatorChain` stops when `context.Response != null`.

Decorators may continue the pipeline by calling `next()`. To abort from a
decorator, set any needed `context.Response` and do not call `next()`.

Handlers are terminal pipeline steps and must leave `context.Response` set to a
valid `IResponseHandler`.

## Storage And Realm Isolation

Use `IStorage` and the realm-aware store accessors. Do not add persistence logic
directly to domain services or handlers. When adding storage operations:

1. Add the method to the relevant interface under `RoyalIdentity/Contracts/Storage/`.
2. Implement it in `RoyalIdentity.Storage.InMemory`.
3. Update tests with in-memory storage; tests should not require an external DB.

Cross-realm data access is an architectural bug. Preserve these invariants:

- PKCE is default-on (`RequirePkce = true`) unless explicitly overridden.
- Authorization codes are single-use.
- Signing keys must remain available for validation after token issuance.
- Refresh token consumption tolerance exists to handle client-side persistence
  failures; do not remove it casually.
- Consent is per user, client, scope, and realm.
- Internal realms cannot be deleted or have immutable identity fields changed.

## Constants And Protocol Strings

Use `Constants.*` for protocol and server strings:

- `Constants.Oidc.*` for OAuth2/OIDC values.
- `Constants.Server.*` for server-specific names and keys.
- `Constants.Jwt.*` for project-specific JWT values.

Use `JwtRegisteredClaimNames.*` for standard JWT claim names. Do not reintroduce
legacy `OidcConstants`, `JwtClaimTypes`, or `ServerConstants` classes.

## UI Rules

Account UI lives in `RoyalIdentity.Razor` as Razor Components. Keep business logic
out of components. UI pages should use the matching `I*PageService`:

- `GetViewModelAsync()` for GET/rendering state.
- `ProcessAsync()` for POST/actions.

Account pages use static server rendering. GET and POST are separate component
instances, and scoped services have HTTP request lifetime rather than circuit
lifetime. Be careful with assumptions that only hold in interactive Blazor Server.

## Areas Under Redesign

The `[Redesign]` attribute marks code intended for future removal or restructuring.
Do not model new code after `[Redesign]` members and do not stabilize or extend
those patterns unless the active plan explicitly requires it.

Known unstable areas include:

- `Client.AllowedScopes` and `Client.AllowOfflineAccess`, pending an
  `AllowedResources`-style model.
- The user/session model under `RoyalIdentity/Users/`.
- Scope/resource hierarchy types such as `ResourceServer` and related models.
- Realm-specific options and CORS, covered by
  `.ai/plans/plan-realm-options-redesign.md`.

## Code Style

- Target framework is `net9.0`.
- Nullable and implicit usings are enabled globally.
- Use tabs with width 4 for C# indentation.
- Prefer file-scoped namespace style unless the surrounding file uses otherwise.
- Primary constructors are preferred for simple cases.
- Keep changes scoped to the task and follow nearby patterns.

## Verification

For documentation-only changes, no build is normally required. For code changes,
run a relevant build or test command and report any command that could not be run.

Use focused tests first, then broaden when the change touches shared behavior,
cross-project contracts, request pipelines, realm isolation, token behavior,
storage interfaces, or UI flows.
