# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Foundation Knowledge

Before any significant work, read these files — they contain context about the product, technology decisions, and structural rules that cannot be derived from the code alone:

- [.ai/foundation/product.md](.ai/foundation/product.md) — domain, OAuth2/OIDC flows, business rules, active design debt
- [.ai/foundation/tech.md](.ai/foundation/tech.md) — stack, pipeline execution semantics, storage abstraction, patterns to follow
- [.ai/foundation/structure.md](.ai/foundation/structure.md) — project dependency graph, directory map, naming conventions, where new code belongs
- [.ai/foundation/architecture.md](.ai/foundation/architecture.md) — Feature-Slice architecture for domain modules (`UserAccounts`, `KMS`); the family layout (pure module + `.Integration` + `.PostgreSql`/`.Sqlite`); which projects use it and which deliberately don't
- [.ai/rules/code-style.rules.md](.ai/rules/code-style.rules.md) — repository-specific code style rules and code smells

Completed refactoring plans (useful as historical record and for understanding design decisions):

- [.ai/plans/plan-constants-refactoring.md](.ai/plans/plan-constants-refactoring.md) — COMPLETED
- [.ai/plans/plan-contexts-redesign.md](.ai/plans/plan-contexts-redesign.md) — COMPLETED
- [.ai/plans/plan-ui-screens-refactoring.md](.ai/plans/plan-ui-screens-refactoring.md) — COMPLETED
- [.ai/plans/plan-realm-hardening.md](.ai/plans/plan-realm-hardening.md) — COMPLETED (realm isolation, events, branding, IRealmManager)
- [.ai/plans/plan-realm-options-redesign.md](.ai/plans/plan-realm-options-redesign.md) — COMPLETED (per-realm RealmOptions, copy-on-create, CORS)
- [.ai/plans/plan-resources-redesign.md](.ai/plans/plan-resources-redesign.md) — COMPLETED (Resources/Scopes model: IdentityScope, ResourceServer, Scope; client AllowedResources; signing chain; Resource Indicators / Protected Resource Metadata)
- [.ai/plans/plan-users-edge-session.md](.ai/plans/plan-users-edge-session.md) — COMPLETED (users edge + session redesign; ADR-013/014; `SubjectId`, `IUserDirectory`, `ICurrentRealmAccessor`, pure session store, `LoginFlowService`)
- [.ai/plans/plan-users-accounts-module-v2.md](.ai/plans/plan-users-accounts-module-v2.md) — COMPLETED (10/10 fases; camada B: `RoyalIdentity.UserAccounts` module — rich accounts, own persistence, properties-by-scope, `.Integration` adapter; ADR-015)
- [.ai/plans/plan-users-security-lifecycle.md](.ai/plans/plan-users-security-lifecycle.md) — COMPLETED (account credentials & security lifecycle: password history/expiration enforcement, action tokens, `SecurityStamp` + `SessionsValidAfter` invalidation, lockout/admin block window, email/phone verification, session/refresh revocation, events + audit; ADR-017. Review-006 noted a follow-up: concurrency **retry** decided but not implemented in the real flow — see plan-users-accounts-sqlite-hardening)
- [.ai/plans/plan-royalidentity-security.md](.ai/plans/plan-royalidentity-security.md) — COMPLETED (8/8 fases; shared `RoyalIdentity.Security` library — crypto, password hashing, key material; ADR-016; removed duplication between the core and `UserAccounts`)

Active plans (check status before modifying affected areas):

- [.ai/plans/plan-users-accounts-sqlite-hardening.md](.ai/plans/plan-users-accounts-sqlite-hardening.md) — IN PROGRESS (backing hardening toward replacing the in-memory fake per ADR-018. All open questions decided. Fase 1 concurrency **retry** DONE: `[WithRetryOnConcurrency]` on the pure-mutation credential use cases, scoped manual retry (`IWorkContext.RetryOnConcurrencyAsync`) on the four token/verification flows (with precondition re-validation against the reloaded aggregate), `AuthenticateLocalCredential` excluded from retry but fail-closed on conflict (Q4), exhaustion mapped to a fixed `user_account.concurrency_conflict` typeId, `ConcurrencyTests` proven against the real handlers with genuine EF-identity-map conflicts (11 tests). Fase 2 migrations DONE: `IDesignTimeDbContextFactory` + initial migration per provider (`.Sqlite`/`.PostgreSql`); the PostgreSQL migration needed a manual fix (EF's scaffolder does not know `xmin` is a reserved system column); SQLite migration validated by dedicated `Migrate()` tests, PostgreSQL migration generated/corrected but **not yet run against a real server** (no local Postgres in this environment — Q7 already defers automated PG validation to a future .NET Aspire phase). Fase 3 reusable module seed + module as test backing pending)

Roadmap of the plans that come after the ones above: [.ai/plans/plans-roadmap-02.md](.ai/plans/plans-roadmap-02.md) (supersedes `plans-roadmap-01.md`) — includes `.ai/plans/plan-data-macro.md`, the sequencing map for the IdP's own data persistence work.

Architectural Decision Records (accepted decisions; read before changing the affected area):

- [adrs/](adrs/) — ADR-001..018 (rearchitecture, realms, tests, Razor SSR, users, constants, IRealmManager, multi-realm isolation, resources/scopes model, client type / full scope allowed, resource indicators / protected resource metadata, **ADR-013 modular architecture & boundaries**, **ADR-014 users edge + session redesign — refines ADR-005**, **ADR-015 `UserAccounts` module — `.Integration` adapter + claims seam `IUserClaimsProvider`; amends ADR-013/014**, **ADR-016 shared technical library `RoyalIdentity.Security` (leaf technical lib in the product namespace — not the external `RoyalCode.*` ecosystem); amends ADR-013**, **ADR-017 account security lifecycle — `RequiredAction`, `SecurityStamp` + `SessionsValidAfter`, `IUserSecurityStateProvider`/`ISessionRevocationService` seams, per-realm `SecurityLifecycleOptions`; amends ADR-014/015**, **ADR-018 in-memory storage fake is transitional — converge tests on module + Sqlite in-memory, no further fake feature-parity; amends ADR-013/014/015**)

Backlog (deferred items with design notes):

- [.ai/backlogs/backlog-001.md](.ai/backlogs/backlog-001.md)

## Commands

```bash
# Build entire solution
dotnet build RoyalIdentity.sln

# Run all tests
dotnet test RoyalIdentity.sln

# Run a specific test project
dotnet test Tests.Pipelines
dotnet test Tests.Identity
dotnet test Tests.Integration

# Run a single test by name
dotnet test Tests.Pipelines --filter "FullyQualifiedName~PipelineDispatcher_Must_Dispatch"

# Run the server
dotnet run --project RoyalIdentity.Server
```

## Architecture in Brief

The system is an OpenID Connect / OAuth2 authorization server. Every HTTP request flows through three layers:

1. **Endpoint handler** (`IEndpointHandler`) — reads raw HTTP, produces a typed context object
2. **Pipeline** — chain of `IDecorator<T>` → `IValidator<T>` → `IHandler<T>` registered per context type
3. **Response handler** (`IResponseHandler`) — set on context by the handler, executed last to write the HTTP response

The middleware order in `Program.cs` is significant: `UseRealmDiscovery` must run before `UseAuthentication`. It extracts the `{realm}` route segment and loads `RealmOptions` into `HttpContext` — everything downstream depends on this.

## Key Conventions

**`[Redesign]` attribute** — appears on members marked for future removal or restructuring. Do not model new code after these patterns; do not stabilize or extend them.

**Pipeline configuration** (the pattern used in both production DI and tests):
```csharp
services.AddPipelines(builder =>
{
    builder.For<SomeContext>()
        .UseDecorator<LoadClient>()
        .UseValidator<AuthorizeMainValidator>()
        .UseHandler<AuthorizeHandler>();
});
```

**Validator error signaling** — set `context.Response` to an error handler, never throw. The `ValidatorChain` stops on `context.Response != null`.

**Decorator abort** — do not call `next()` to abort the pipeline from a decorator.

**Constants** — use `Constants.*` for all protocol strings (`Constants.Oidc.*`, `Constants.Server.*`, `Constants.Jwt.*`). Use `JwtRegisteredClaimNames.*` for standard JWT claims. The legacy classes `OidcConstants`, `JwtClaimTypes`, `ServerConstants` were deleted — do not re-introduce them.

## External RoyalCode Libraries (`UserAccounts` module family only)

Only `RoyalIdentity.UserAccounts` and its `.Integration`/`.PostgreSql`/`.Sqlite` family depend on the external `RoyalCode.*` ecosystem (per [architecture.md](.ai/foundation/architecture.md) §9: the pure module is "RoyalCode libs + EFCore only", no reference to the core). The core `RoyalIdentity` IdP does **not** use these libraries — it has its own pipeline/`context.Response` conventions (see Architecture in Brief above).

See [.ai/references/external-libraries/instructions.md](.ai/references/external-libraries/instructions.md) for the index of per-library docs (SmartCommands, WorkContext, SmartSearch, SmartSelector, SmartProblems, SmartValidations, Domain), the `.md`/`.ai-rules.md` pairing convention, and precedence rules.

## Code Style

- Tabs, width 4 (`indent_size = 4` in `.editorconfig`)
- `Nullable enable`, `ImplicitUsings enable`, `LangVersion latest` — applied globally via `Directory.Build.props`
- File-scoped namespaces preferred (`csharp_style_namespace_declarations = block_scoped:silent`)
- Primary constructors preferred for simple cases (`csharp_style_prefer_primary_constructors = true:suggestion`)
- See [.ai/rules/code-style.rules.md](.ai/rules/code-style.rules.md) for repository-specific code smells such as LINQ query expression syntax.
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
