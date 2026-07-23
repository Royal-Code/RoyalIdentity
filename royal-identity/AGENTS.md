# AGENTS.md

This file provides persistent guidance for Codex when working in this repository.
Keep it focused on rules that should apply to every session. For deeper product
or architecture context, read the foundation files listed below.

## Start Here

Before significant code changes, read:

- `.ai/foundation/product.md` for product goals, OAuth2/OIDC flows, realm rules, business invariants, and active design debt.
- `.ai/foundation/tech.md` for runtime, pipeline semantics, storage abstraction, token handling, and implementation patterns.
- `.ai/foundation/structure.md` for project layout, dependency rules, naming conventions, and high-risk files.
- `.ai/foundation/architecture.md` for the Feature-Slice architecture used by domain modules (`UserAccounts`, `KMS`) — the family layout (pure module + `.Integration` adapter + `.PostgreSql`/`.Sqlite`) and which projects deliberately do not use it.
- `.ai/rules/code-style.rules.md` for repository-specific code style rules and code smells.

Before modifying an area touched by a plan, inspect `.ai/plans/` first. Deferred
product/design notes live in `.ai/backlogs/backlog-001.md`.

Completed plans, in order: `.ai/plans/plan-users-edge-session.md` (users/session
edge redesign), `.ai/plans/plan-users-accounts-module-v2.md` (the `UserAccounts`
module — rich accounts, own persistence, properties-by-scope, `.Integration`
adapter; 10/10 phases done), `.ai/plans/plan-users-security-lifecycle.md` (password
history/expiration, action tokens, `SecurityStamp`/`SessionsValidAfter`
invalidation, lockout, email/phone verification), `.ai/plans/plan-royalidentity-security.md`
(shared `RoyalIdentity.Security` library for crypto/password hashing/key material),
and `.ai/plans/plan-users-accounts-sqlite-hardening.md` (3/3 phases: real
concurrency retry via `RoyalCode.SmartCommands`/`.WorkContext` `0.1.0`; EF
migrations for `.Sqlite`/`.PostgreSql` replacing `EnsureCreated`, PostgreSQL one
validated against a real ephemeral PostgreSQL 17 via Podman; single reusable
module seed, `Tests.UserAccounts/UserAccountsModuleSeed.cs`, linked into
`Tests.Integration`, replacing duplicated Alice/Bob seeding, plus an expanded
opt-in OIDC regression), and `.ai/plans/plan-data-storage-baseline.md` (5/5
phases: full inventory of the IdP storage contracts in
`.ai/plans/plan-data-storage-matrix.md` — ownership, seeds, per-operation
semantics closed via DF15-DF25, public changes MP-1..MP-10, and the per-store
migration order — plus the provider-neutral contract suite `Tests.Storage`,
which future EF providers reuse by adding fixtures only), and
`.ai/plans/plan-data-configuration-storage.md` (7/7 phases: pure Configuration data model;
SQLite/PostgreSQL mappings and migrations; EF stores for ServerOptions, realms, clients and signing keys;
async snapshot; explicit Plain/Data Protection/AES key protectors; dedicated migration/seed runner and SQL;
provider-neutral P2 contracts and acceptances validated against PostgreSQL 17 real). Treat each as the
implemented target architecture before changing the area it covers.

There is no active implementation plan. The next macro-plan item is the draft
`.ai/plans/plan-data-operational-storage.md` (Plan 3), whose Q1-Q12 must be answered and converted into closed
decisions before implementation starts. Until it and the test-migration plan are complete,
resources/scopes remain volatile per baseline DF22, the default host remains in-memory, and the Configuration
adapter must not be promoted as a partial production `IStorage`/`IStorageProvider`. The complete EF gateway is
composed only when Operational exists; new work must consume `.ai/plans/plan-data-storage-matrix.md` without
re-inferring its closed semantics.

Accepted architectural decisions live in `adrs/` (ADR-001..018). Read the relevant
ADR before changing the affected area. Notably for the users/session area:
`ADR-013` (modular architecture & boundaries — storages as facades) and `ADR-014`
(users edge + session redesign, which **refines** `ADR-005`), implemented by
`.ai/plans/plan-users-edge-session.md`; `ADR-015` (`RoyalIdentity.UserAccounts`
module — rich accounts, own persistence, `.Integration` adapter, claims seam
`IUserClaimsProvider`), which **amends** `ADR-013`/`ADR-014`; `ADR-016` (shared
`RoyalIdentity.Security` library, in the product namespace, not the external
`RoyalCode.*` ecosystem), which **amends** `ADR-013`; `ADR-017` (account security
lifecycle — `RequiredAction`, `SecurityStamp`/`SessionsValidAfter`,
`IUserSecurityStateProvider`/`ISessionRevocationService`), which **amends**
`ADR-014`/`ADR-015`; and `ADR-018` (the in-memory storage fake is transitional —
converge tests on the module + Sqlite, no further fake feature-parity), which
**amends** `ADR-013`/`ADR-014`/`ADR-015`.

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
.NET 10 (`net10.0`). Realms are the top-level isolation boundary. Every data access involving
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

Rich domain modules (`RoyalIdentity.UserAccounts`, and future `RoyalIdentity.KMS`)
follow a separate Feature-Slice architecture — see `.ai/foundation/architecture.md`.
Each ships as a family: the **pure module** (domain + features + own persistence;
depends only on RoyalCode libraries + EF Core; **never** references the core), a
separate **`.Integration`** adapter (references both core and module; implements
the core-owned edge ports; the only bridge between them), and per-provider
projects (`.PostgreSql`/`.Sqlite`). The core never references the module. Only
`.Integration` knows both sides.

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

Use `IStorage` and the realm-aware store accessors for IdP data. Account/user
edge data goes through `IUserDirectory` and its realm-bound ports, not through
`IStorage`. Do not add persistence logic directly to domain services or handlers.
When adding storage operations:

1. Add the method to the relevant interface under `RoyalIdentity/Contracts/Storage/`.
2. Implement it in `RoyalIdentity.Storage.InMemory`.
3. Update tests with in-memory storage; tests should not require an external DB.
4. Add or update the provider-neutral contract tests in `Tests.Storage` (scenarios
   never reference fake types; only the fixture/harness does) and record the
   semantics in `.ai/plans/plan-data-storage-matrix.md`.

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
- The user/session edge under `RoyalIdentity/Users/`, redesigned per `ADR-014`
  and `.ai/plans/plan-users-edge-session.md`: use `Subject`, `IUserDirectory`,
  `ILocalUserAuthenticator`, `IUserClaimsProvider`, pure `IUserSessionStore`,
  `IUserSessionService`, `ISubjectPrincipalFactory`, and `LoginFlowService`.
  (`IUserClaimsProvider` is the ADR-014-amended name for `IUserPropertyProvider`;
  the rename landed in `plan-users-accounts-module-v2.md` Fase 2, now completed.)
  Do not reintroduce removed legacy types such as `IdentityUser`, `UserDetails`,
  `IUserStore`, `IUserDetailsStore`, `IdentitySession`, `ISignInManager`, or
  credentials-result structs.
- Scope/resource hierarchy types such as `ResourceServer` and related models.
- Realm-specific options and CORS, covered by
  `.ai/plans/plan-realm-options-redesign.md`.

## External RoyalCode Libraries (`UserAccounts` module family only)

Only `RoyalIdentity.UserAccounts` and its `.Integration`/`.PostgreSql`/`.Sqlite`
family depend on the external `RoyalCode.*` ecosystem (the pure module is
"RoyalCode libs + EF Core only" — see Project Boundaries above). The core
`RoyalIdentity` IdP does not use these libraries; it has its own pipeline/
`context.Response` conventions.

See `.ai/references/external-libraries/instructions.md` for the index of
per-library docs (SmartCommands, WorkContext, SmartSearch, SmartSelector,
SmartProblems, SmartValidations, Domain), the `.md`/`.ai-rules.md` pairing
convention, and precedence rules.

## Code Style

- Target framework is `net10.0` (via `Directory.Build.props`).
- Nullable and implicit usings are enabled globally.
- Use tabs with width 4 for C# indentation.
- Prefer file-scoped namespace style unless the surrounding file uses otherwise.
- Primary constructors are preferred for simple cases.
- Follow `.ai/rules/code-style.rules.md` for repository-specific style rules, including the preference for method-chain LINQ over query expression syntax.
- Keep changes scoped to the task and follow nearby patterns.
- For the `UserAccounts` module family, follow "External RoyalCode Libraries" above for library-specific patterns.

## ADR

ADRs are project architecture decisions. 

Rules for ADRs:
- They should not include the solution design, only the decisions;
- They are stored in the `adrs\` directory;
- They follow the naming convention: `ADR-{NNN}.md`.

ADRs with good structure are ADR-001 through ADR-009, and ADR-016.
ADRs with acceptable structure: ADR-010 and ADR-011.
ADRs with poor structure are ADR-012 through ADR-015; these contain a design rather than a decision.

## Verification

For documentation-only changes, no build is normally required. For code changes,
run a relevant build or test command and report any command that could not be run.

Use focused tests first, then broaden when the change touches shared behavior,
cross-project contracts, request pipelines, realm isolation, token behavior,
storage interfaces, or UI flows.
