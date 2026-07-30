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
- [.ai/plans/plan-users-accounts-sqlite-hardening.md](.ai/plans/plan-users-accounts-sqlite-hardening.md) — COMPLETED (3/3 fases; backing hardening toward replacing the in-memory fake per ADR-018. Fase 1: real concurrency **retry** — `[WithRetryOnConcurrency]` on pure-mutation credential use cases, scoped manual retry on the four token/verification flows, `AuthenticateLocalCredential` fail-closed without retry (Q4), fixed `user_account.concurrency_conflict` typeId on exhaustion. Fase 2: `IDesignTimeDbContextFactory` + initial migration per provider (`.Sqlite`/`.PostgreSql`, incl. a manual fix for EF's scaffolder not knowing `xmin` is a reserved PostgreSQL system column), validated against a real ephemeral PostgreSQL 17 via Podman. Fase 3: single reusable module seed (`Tests.UserAccounts/UserAccountsModuleSeed.cs`, linked into `Tests.Integration`) replacing the duplicated Alice/Bob seeding, opt-in OIDC regression expanded to 6 tests (Q9); full solution suite green — 563 passed + 1 PostgreSQL opt-in skipped)

- [.ai/plans/plan-data-storage-baseline.md](.ai/plans/plan-data-storage-baseline.md) — COMPLETED (5/5 fases; storage contracts characterized and `plan-data-storage-matrix.md` fixed as the normative semantics)
- [.ai/plans/plan-data-configuration-storage.md](.ai/plans/plan-data-configuration-storage.md) — COMPLETED (7/7 fases; Configuration family over EF — ServerOptions/realms/clients/signing keys, async snapshot, key protectors, migration/seed runner and reviewable SQL, SQLite + real PostgreSQL 17)
- [.ai/plans/plan-data-operational-storage.md](.ai/plans/plan-data-operational-storage.md) — COMPLETED (8/8 fases; Operational family over EF — single-use authorization codes (MP-2) and conditional refresh transitions (MP-3) under real concurrency, realm-bound authorize parameters with absolute TTL (MP-5), cleanup/purge behind an explicitly selected execution mode (MP-6/MP-7), per-realm payload protection, split migrations histories per family (DF23), complete `AddEntityFrameworkStorage()` gateway, SQLite + real PostgreSQL 17. Fase 8 fixed three product defects the in-memory fake masked: a captive `IClientSecretChecker` singleton, a malformed derived issuer, and a validator that depended on the issuer being cached into `RealmOptions`)
- [.ai/plans/plan-data-test-migration.md](.ai/plans/plan-data-test-migration.md) — COMPLETED (9/9 fases; production Server on externally provisioned PostgreSQL, zero-configuration ephemeral SQLite Demo, default integration tests on EF/SQLite + real UserAccounts, definitive atomic contracts and removal of the fake/fallbacks)

- [.ai/plans/plan-replay-protection.md](.ai/plans/plan-replay-protection.md) — COMPLETED (3/3 fases; real replay
  protection for `private_key_jwt`. `IReplayCache` became `IReplayProtectionStore` — one atomic `TryAddAsync`,
  keyed by realm and issuer, taking a `CancellationToken`. **No default registration:** every composition root
  declares `AddInMemoryReplayProtection()` or `AddOperationalReplayProtection()`, and
  `ReplayProtectionStartupValidator` fails startup in any environment when none, two, or an inconsistent strategy
  is declared. The durable backing is the Operational table `replay_handles`, whose primary key
  `(realm_id, issuer, purpose, handle_digest)` **is** the decision — a conflict answers replay with no prior read
  and no expiration comparison. Its own length-prefixed `ReplayHandleDigest` does not reuse
  `OperationalLookupDigest`, whose high-entropy justification does not transfer to a client-chosen `jti`; cleanup
  of replay handles is strict (`ExpiresAtUtc < now`) because the artifact is still acceptable at that instant.
  `Authentication.ClientAssertionMaxLifetime` (default 10 min, range 1 s–1 h) makes retention a server value.
  Server uses the durable backing, Demo the in-memory one. Fase 1 also fixed two product defects the missing
  coverage hid: `client_credentials` with `private_key_jwt` returned 500, and a replay-store infrastructure
  failure came disguised as `invalid_client`)

Active plans (check status before modifying affected areas):

No implementation plan is currently active. `Tests.Host` is storage-agnostic and `Tests.Integration` uses
`PersistentStorageAppFactory` by default: Configuration + Operational share one isolated SQLite in-memory
database and UserAccounts owns another. PostgreSQL storage/Aspire acceptances remain local opt-in. The production
Server is PostgreSQL-only and must be provisioned by `RoyalIdentity.Migrations`; `RoyalIdentity.Demo` is the
self-provisioned ephemeral local experience.

Roadmap of the plans that come after the ones above: [.ai/plans/plans-roadmap-02.md](.ai/plans/plans-roadmap-02.md) (supersedes `plans-roadmap-01.md`) — includes `.ai/plans/plan-data-macro.md`, the sequencing map for the IdP's own data persistence work.

Architectural Decision Records (accepted decisions; read before changing the affected area):

- [adrs/](adrs/) — ADR-001..019 (rearchitecture, realms, tests, Razor SSR, users, constants, IRealmManager, multi-realm isolation, resources/scopes model, client type / full scope allowed, resource indicators / protected resource metadata, **ADR-013 modular architecture & boundaries**, **ADR-014 users edge + session redesign — refines ADR-005**, **ADR-015 `UserAccounts` module — `.Integration` adapter + claims seam `IUserClaimsProvider`; amends ADR-013/014**, **ADR-016 shared technical library `RoyalIdentity.Security` (leaf technical lib in the product namespace — not the external `RoyalCode.*` ecosystem); amends ADR-013**, **ADR-017 account security lifecycle — `RequiredAction`, `SecurityStamp` + `SessionsValidAfter`, `IUserSecurityStateProvider`/`ISessionRevocationService` seams, per-realm `SecurityLifecycleOptions`; amends ADR-014/015**, **ADR-018 in-memory storage fake was transitional and its removal is recorded in §4; amends ADR-013/014/015**)

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

# Run the zero-configuration demo
dotnet run --project RoyalIdentity.Demo

# Validate disposable PostgreSQL provisioning + Server startup/OIDC
./scripts/Test-ServerPostgreSql.ps1

# Present the same private_key_jwt assertion twice against the durable replay backing (PostgreSQL 17)
./scripts/Test-ReplayProtectionPostgreSql.ps1
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

- 4 spaces for C# indentation (`indent_size = 4` in `.editorconfig`)
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
