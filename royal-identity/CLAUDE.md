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

Active plans (check status before modifying affected areas):

- [.ai/plans/plan-users-accounts-module-v2.md](.ai/plans/plan-users-accounts-module-v2.md) — PLANNED (camada B: `RoyalIdentity.UserAccounts` module — rich accounts, own persistence, properties-by-scope, `.Integration` adapter; ADR-015. Fase 1 done)

Architectural Decision Records (accepted decisions; read before changing the affected area):

- [adrs/](adrs/) — ADR-001..015 (rearchitecture, realms, tests, Razor SSR, users, constants, IRealmManager, multi-realm isolation, resources/scopes model, client type / full scope allowed, resource indicators / protected resource metadata, **ADR-013 modular architecture & boundaries**, **ADR-014 users edge + session redesign — refines ADR-005**, **ADR-015 `UserAccounts` module — `.Integration` adapter + claims seam `IUserClaimsProvider`; amends ADR-013/014**)

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

## Code Style

- Tabs, width 4 (`indent_size = 4` in `.editorconfig`)
- `Nullable enable`, `ImplicitUsings enable`, `LangVersion latest` — applied globally via `Directory.Build.props`
- File-scoped namespaces preferred (`csharp_style_namespace_declarations = block_scoped:silent`)
- Primary constructors preferred for simple cases (`csharp_style_prefer_primary_constructors = true:suggestion`)
- See [.ai/rules/code-style.rules.md](.ai/rules/code-style.rules.md) for repository-specific code smells such as LINQ query expression syntax.
- Follow the instructions in `.ai\references\external-libraries\instructions.md` and the internal references for the design of specific components/classes/patterns documented in this file.

## ADR

ADRs are project architecture decisions. 

Rules for ADRs:
- They should not include the solution design, only the decisions;
- They are stored in the `adrs\` directory;
- They follow the naming convention: `ADR-{NNN}.md`.

ADRs with good structure are ADR-001 through ADR-009.
ADRs with acceptable structure: ADR-010 and ADR-011.
ADRs with poor structure are ADR-012 through ADR-015; these contain a design rather than a decision.
