# Structural Foundation: RoyalIdentity

## Purpose

This document explains the stable structure of the solution:

- which architectural family a project belongs to;
- which dependency directions are allowed;
- where a responsibility should be implemented;
- where to look before changing an area;
- which tests should prove a change.

It is deliberately **not** a repository index. It must not enumerate every current file, type, endpoint, option or
completed redesign. Those lists become incorrect as soon as code moves.

Use the following sources for volatile information:

- `rg --files` and `dotnet sln RoyalIdentity.sln list` for the current tree;
- `.ai/plans/` and `.ai/plans/plans-roadmap-02.md` for active and completed implementation work;
- `.ai/backlogs/` for deferred capabilities;
- `redesign-todo.md` and current `[Redesign]` attributes for remaining redesign debt;
- `AGENTS.md` for repository-wide working rules that must apply in every session;
- `adrs/` for accepted architectural decisions.

When this document names a path, treat it as a **responsibility anchor**, not as a promise that every concrete
file beneath it will always exist.

---

## 1. Structural Map

The solution contains several architectural families. Do not apply the conventions of one family to another.

| Family | Responsibility | Stable anchors |
|---|---|---|
| Pipeline infrastructure | Generic context pipelines, chains, dispatch and HTTP result plumbing; no product semantics | `RoyalIdentity.Pipelines/` |
| IdP core | OAuth/OIDC domain, contexts, endpoints, handlers, contracts, options and realm-aware behavior | `RoyalIdentity/` |
| Shared security | Product-owned cryptography, password hashing and key-material primitives reusable across project families | `RoyalIdentity.Security/` |
| Pure persistence data | Persistence entities, payloads and provider-neutral data concerns; no core/domain dependencies | `RoyalIdentity.Data.Configuration/`, `RoyalIdentity.Data.Operational/` |
| IdP persistence adapters | Translation between core storage facades and pure data models; provider mappings and migrations | `RoyalIdentity.Storage.EntityFramework*/` |
| Rich domain modules | Feature-Slice domain families with their own persistence and an explicit integration adapter | `RoyalIdentity.UserAccounts*/`; future module families follow the same pattern |
| Account UI | Razor components, presentation services, view models and localization resources | `RoyalIdentity.Razor/` |
| Composition roots | Select and wire implementations; own environment-specific startup, never reusable domain behavior | `RoyalIdentity.Server/`, `RoyalIdentity.Demo/`, `RoyalIdentity.Migrations/`, `Aspire/`, `Tests.Host/` |
| Verification | Unit, contract, integration, architecture and opt-in browser/provider acceptances | `Tests.*/`, `Aspire/Aspire.Tests/`, `scripts/` |
| Architecture and planning | Decisions, foundations, active plans, backlog and repository rules | `adrs/`, `.ai/`, `AGENTS.md` |

The two main architectural styles are:

1. **IdP pipeline architecture** for `RoyalIdentity` and `RoyalIdentity.Pipelines`.
2. **Feature-Slice module architecture** only for rich domain modules such as `RoyalIdentity.UserAccounts` and
   future `RoyalIdentity.KMS`.

See [architecture.md](architecture.md) before changing or creating a rich module. Do not import Feature-Slice
folders, command patterns or module persistence rules into the IdP core.

---

## 2. Dependency Direction

Dependencies point inward toward abstractions and pure models. Composition roots are the only place where all
selected implementations meet.

```text
Composition roots
  ├─ Account UI ───────────────────────────────→ IdP core
  ├─ IdP EF adapter ─────────→ IdP core + pure Data.*
  ├─ Module .Integration ────→ IdP core + pure module
  ├─ Provider projects ──────→ owning adapter/module + provider package
  └─ IdP core ───────────────→ Pipelines + Security

Pure module ─────────────────→ RoyalCode libraries + EF Core, never IdP core
Pipelines ───────────────────→ no project dependencies
Pure Data.* ─────────────────→ persistence abstractions, never IdP core
```

### Required boundaries

- `RoyalIdentity.Pipelines` contains no RoyalIdentity product semantics and does not reference the core.
- `RoyalIdentity` does not reference UI, hosts, EF providers or rich domain modules.
- `RoyalIdentity.Data.*` does not reference core domain types.
- `RoyalIdentity.Storage.EntityFramework` is the IdP adapter that may know both core storage contracts and
  `Data.*` persistence models.
- Provider projects own provider-specific mappings and migrations; provider concerns do not leak into the core
  adapter or domain contracts.
- A pure rich module does not reference `RoyalIdentity`. Only its `.Integration` project knows the module and
  the core edge ports.
- Hosts select implementations and configuration. They do not become a home for business rules.

If a proposed reference violates these arrows, introduce or use an existing port at the owning boundary instead
of adding the reference.

Architecture tests under `Tests.Architecture/` guard important boundaries. A new dependency rule that matters to
the design should normally gain a guard there.

---

## 3. Where Responsibilities Belong

Use this table before searching for a particular filename.

| Responsibility | Owner / location |
|---|---|
| Generic pipeline abstractions and chain execution | `RoyalIdentity.Pipelines/Abstractions`, `Infrastructure`, `Configurations`, `Mapping`, `Defaults` |
| HTTP parsing and creation of a typed protocol context | `RoyalIdentity/Endpoints/` |
| Request state for one protocol operation | `RoyalIdentity/Contexts/` |
| Reusable context capability | `RoyalIdentity/Contexts/Withs/` |
| Request parameter extraction or typed pipeline item | `RoyalIdentity/Contexts/Parameters/`, `Contexts/Items/` |
| Cross-cutting pipeline enrichment/interception | `RoyalIdentity/Contexts/Decorators/` |
| Expected protocol validation | `RoyalIdentity/Contexts/Validators/` |
| Terminal use-case execution | `RoyalIdentity/Handlers/` |
| Protocol response construction and HTTP results | `RoyalIdentity/Responses/` |
| Core domain models and token/key/resource models | `RoyalIdentity/Models/` |
| Core-owned service and storage ports | `RoyalIdentity/Contracts/` |
| Default core implementation of a port | the corresponding `RoyalIdentity/Contracts/Defaults/` or established core feature area |
| Protocol and server constants | `RoyalIdentity/Options/Constants*.cs` through `Constants.*` |
| Server/realm/feature configuration | `RoyalIdentity/Options/` |
| Realm discovery and authentication integration | `RoyalIdentity/Authentication/` and core ASP.NET extensions |
| Account UI rendering | `RoyalIdentity.Razor/Components/` |
| Account UI orchestration | `RoyalIdentity.Razor/Services/` through `I*PageService` contracts |
| UI input/output models | `RoyalIdentity.Razor/ViewModels/` or the owning component area when genuinely local |
| Localized presentation text | `RoyalIdentity.Razor/Resources/`; never core options or protocol descriptions |
| Configuration/Operational persistence shape | the owning `RoyalIdentity.Data.*` project |
| Translation from persistence to core storage facades | `RoyalIdentity.Storage.EntityFramework/` |
| Provider-specific mapping/migration | the owning `.Sqlite` or `.PostgreSql` project |
| Rich account behavior and invariants | `RoyalIdentity.UserAccounts/` Feature-Slice module |
| Translation from a rich module to IdP ports | `RoyalIdentity.{Module}.Integration/` |
| Environment wiring and startup | the relevant composition root |

### Placement questions

Before adding a type, ask in order:

1. Is this generic pipeline machinery or RoyalIdentity product behavior?
2. Is it core OAuth/OIDC behavior, presentation behavior, persistence behavior or a rich module feature?
3. Which project owns the contract?
4. Does the implementation need knowledge from both sides of a boundary? If so, it is probably an adapter.
5. Which existing test family should prove the contract?

Avoid folders named only by technical object kind inside a Feature-Slice domain. Conversely, do not force
business-feature folders into the protocol core, whose stable organization is endpoint/pipeline responsibility.

---

## 4. IdP Request Pipeline

Every protocol request follows the same structural path:

```text
route
  → IEndpointHandler (raw HTTP → typed context or endpoint-level error)
  → decorators (enrich, intercept, optionally abort)
  → validators (expected rejection by setting context.Response)
  → terminal handler
  → IResponseHandler / IResult
```

### Component contracts

- An endpoint parses HTTP and creates the correct context. It does not perform the whole use case.
- A decorator may call `next()` to continue or omit it to abort. It may inspect the response while the stack
  unwinds when post-processing is required.
- A validator reports expected invalid input through `context.Response`; it does not throw for validation flow.
- A handler is terminal and must leave a valid response.
- Pipeline order is behavior. Any order that carries a security or protocol invariant should have a focused
  architecture or integration regression.

Pipeline components are a public composition surface. Target the narrowest reusable capability contract such as
`IWithClient`, `IWithResources`, `IWithRedirectUri` or `IWithPrompt`. Do not narrow an entire component to one
concrete context because a single branch needs specialized behavior; specialize only that branch.

### Registration and routing anchors

To find current composition rather than relying on a list here:

```powershell
rg -n "builder\.For<|UseDecorator|UseValidator|UseHandler" RoyalIdentity
rg -n "MapPipeline|MapOpenIdConnectProviderEndpoints" RoyalIdentity
rg -n "AddOpenIdConnectProviderServices" RoyalIdentity RoyalIdentity.Server RoyalIdentity.Demo Tests.Host
```

Discovery metadata must prove reachable runtime. Do not advertise an endpoint, response mode, authentication
method or extension merely because a constant or option exists.

---

## 5. Realm Isolation and Storage

`Realm` is the top-level isolation boundary. Realm-owned clients, keys, resources, sessions, consents, codes and
tokens are reached through realm-aware facades. Account data is reached through realm-bound user-directory ports.

### Core IdP data

- Core storage contracts live under `RoyalIdentity/Contracts/Storage/` and are exposed through `IStorage`.
- Consumers ask the gateway for a store bound to the current realm; they do not query EF contexts directly.
- Persisted Configuration and Operational shapes live in the corresponding pure `Data.*` family.
- `RoyalIdentity.Storage.EntityFramework` translates between core models/contracts and persistence models.
- Provider projects own provider details, migrations and provider-specific acceptances.

### Rich module data

Rich modules own their own persistence. `UserAccounts` data does not move through the IdP EF adapter. Its
`.Integration` project binds the realm and implements core-owned account ports using module features.

### Changing storage

Before adding or changing a storage operation:

1. Read `.ai/plans/plan-data-storage-matrix.md`; do not re-infer semantics already closed there.
2. Change the owning contract and record any new ownership/semantics in the matrix.
3. Implement the owning adapter/module and every supported provider.
4. Add or update provider-neutral contracts in `Tests.Storage` or the owning module test project.
5. Preserve atomic decisions as single storage operations; do not recreate check-then-write races in services.

Payload compatibility follows [ADR-020](../../adrs/ADR-020.md). Do not copy version policy into individual
features or invent pre-release upcasters.

Cross-realm access is an architectural defect even when the identifier is opaque or cryptographically protected.
Protected data must still be checked against the current realm when the payload carries realm identity.

---

## 6. Rich Domain Module Families

Feature-Slice rules are defined in [architecture.md](architecture.md). The structural summary is:

```text
RoyalIdentity.{Module}/             pure domain + features + own persistence
RoyalIdentity.{Module}.Integration/ IdP adapter; the only bridge to the core
RoyalIdentity.{Module}.Sqlite/      provider concerns and migrations
RoyalIdentity.{Module}.PostgreSql/  provider concerns and migrations
RoyalIdentity.{Module}.Api/.Web     optional separate delivery projects
```

Stable rules:

- The pure module never references the IdP core, hosts or ASP.NET UI.
- The core never references the module.
- `.Integration` implements core-owned ports and translates realm/core concepts to module primitives/options.
- Module API/UI are siblings, not folders inside the pure module.
- Domain and feature conventions come from `architecture.md` and the external RoyalCode references, not from the
  IdP pipeline conventions in this document.

---

## 7. Account UI

Account pages use Razor Components with static server rendering. GET and POST are separate component instances;
scoped services have request lifetime, not circuit lifetime.

### UI boundary

- Components render state, bind forms and translate a typed result into navigation or re-rendering.
- Business and protocol orchestration lives in the matching `I*PageService` implementation.
- Page services consume core ports/pipelines; components do not reach into persistence adapters.
- Presentation messages are resolved at the UI boundary. OAuth/OIDC error codes and protocol descriptions remain
  protocol values and are not translated.
- The core owns only the `IUiLocaleCatalog` capability contract and locale policy/matching. RESX catalogues,
  marker types and presentable message codes belong to `RoyalIdentity.Razor/Localization/` and `Resources/`.
- Tenant-provided names/descriptions are data, not localization resources, and remain normally encoded.
- HTML/markup stays in components; resource files contain text and placeholders only.

### SSR cautions

- Named forms, antiforgery and multiple forms on one page interact at the endpoint/component boundary; prove the
  actual POST path with an HTTP test.
- Do not rely on component instance state surviving a request.
- Localized validation must be tested on the rendered SSR response, not only by resolving resource keys.
- Request localization runs after realm discovery and before authentication/rendering that consumes culture.

Use `RoyalIdentity.Razor/Components/`, `Services/`, `ViewModels/` and `Resources/` as responsibility anchors. Find
the current route/component/service pair with `rg` instead of extending an inventory in this file.

---

## 8. Configuration and Composition

Configuration belongs to the narrowest owner that can enforce it:

- server-wide hosting/protocol defaults belong to `ServerOptions`;
- realm-specific protocol/UI policy belongs to `RealmOptions` or a feature options object composed by it;
- ordered locale policy belongs to `RealmOptions.Internationalization`; shipped translations belong to the
  composed UI catalogue and are not configuration data;
- rich account policy belongs to the `UserAccounts` module options, not core `AccountOptions`;
- provider configuration belongs to composition roots/provider projects.

Adding a property is not enough. Trace every consumer and decide whether it reads the server value, realm value
or module value. Update validation, cloning/materialization, serializers, seeds/fixtures and discovery metadata
when applicable.

Composition roots select:

- persistence providers and connection strategy;
- rich module adapters/providers;
- replay-protection and other explicit strategies;
- UI presence and host-specific middleware;
- environment configuration and startup validation.

Reusable behavior does not belong in `Program.cs` or host-only extensions. Conversely, the core must not choose a
provider or silently install a strategy whose selection belongs to the host.

---

## 9. Test Topology

Choose the test project by the contract being proved, not by the production filename being edited.

| Contract under test | Primary location |
|---|---|
| Generic pipeline chains/results | `Tests.Pipelines/` |
| Pure IdP algorithms and focused core behavior without host composition | `Tests.Identity/` |
| Shared security primitives | `Tests.Security/` |
| Provider-neutral IdP storage contracts and provider acceptances | `Tests.Storage/` |
| Rich `UserAccounts` domain/features/providers | `Tests.UserAccounts/` |
| HTTP endpoints, composed pipelines, realm behavior, account UI and OIDC flows | `Tests.Integration/` |
| Dependency, visibility, composition and source-shape invariants | `Tests.Architecture/` |
| Storage-agnostic application target used by integration fixtures | `Tests.Host/` (not a test suite) |
| Relying-party/web harness behavior | `Tests.WebApp/` |
| Real browser/session-management acceptance | `Tests.Browser/` through its opt-in script |
| Full local orchestration acceptance | `Aspire/Aspire.Tests/` or the owning opt-in script |

Rules:

- Put a regression where the affected phase/feature gate will execute it.
- A filtered command closes work only when it selects the intended test, not merely any test or zero tests.
- Prefer one fixture name per behavioral surface so filters remain discriminating.
- Use architecture tests for structural invariants and integration tests for behavior; a text scan does not
  replace an HTTP/security regression.
- Default tests remain self-contained. PostgreSQL, browser and full Aspire acceptances are explicit opt-ins.
- Cross-cutting changes to pipelines, realm isolation, configuration, storage or UI flows require the full
  solution suite after focused tests.

---

## 10. Naming and Protocol Values

| Responsibility | Convention |
|---|---|
| Context | `{Operation}Context` |
| Endpoint parser | `{Operation}Endpoint` |
| Terminal pipeline step | `{Operation}Handler` |
| Validator | `{Concern}Validator` |
| Decorator | verb or concern describing its pipeline role |
| Store port | `I{Entity}Store` |
| Options | `{Concern}Options` |
| Event | `{Action}Event` |
| Context capability | `IWith{Capability}` |

Protocol/server strings use `Constants.*`:

- `Constants.Oidc.*` for OAuth/OIDC values;
- `Constants.Server.*` for server-specific values;
- `Constants.Jwt.*` for project-specific JWT values;
- `JwtRegisteredClaimNames.*` for registered JWT claim names.

Do not introduce parallel constants classes or protocol string literals. Semantic extension values that are
intentionally open to third parties remain strings at their public boundary.

---

## 11. Change Recipes

### Add or change an OIDC endpoint

1. Verify the governing specification and neighboring plan/ADR.
2. Add or reuse the typed context and capability contracts.
3. Parse raw HTTP in an `IEndpointHandler`.
4. Compose decorators and validators in security-sensitive order.
5. Add one terminal handler and the appropriate response boundary.
6. Register the pipeline and map the route.
7. Publish discovery metadata only after the route/runtime is reachable under the same gate.
8. Add focused endpoint tests, pipeline/component tests where useful, and an architecture guard for any new
   invariant that source structure alone must preserve.

### Add a pipeline component

1. Target the narrowest capability interface that represents its requirements.
2. Preserve expected-failure semantics (`context.Response`) and cancellation.
3. Register it in DI and in every applicable pipeline.
4. Test both its behavior and any load-bearing position/order.

### Add a storage operation

Follow the storage workflow in section 5. Never implement a missing atomic contract as multiple calls in a
handler merely to avoid changing the owning store.

### Add configuration

1. Identify the owner: server, realm, module or provider.
2. Define secure/default behavior and validation.
3. Update materialization/copy/serialization and development fixtures under ADR-020.
4. Update consumers and metadata together.
5. Test invalid startup/refresh and last-known-good behavior when the option participates in snapshots.

### Add an account UI page or behavior

1. Put presentation in Razor and orchestration in an `I*PageService`.
2. Keep form input/result types at the UI boundary.
3. Localize presentable product text; preserve protocol and tenant data semantics.
4. Prove GET and POST separately under static SSR, including antiforgery and realm-bound return navigation.

### Add a rich module feature

Stop here and follow [architecture.md](architecture.md). Do not use the IdP endpoint/pipeline recipe inside a
Feature-Slice module.

---

## 12. High-Risk Relationships

Risk belongs to relationships, not permanently to a hand-maintained list of filenames.

Treat these changes as cross-cutting:

- realm discovery relative to request localization, authentication and authorization;
- decorator/validator order and abort/unwind behavior;
- authentication response construction, redirect validation and browser framing;
- client authentication mechanism precedence and secret redaction;
- token/code/refresh single-use and atomic storage transitions;
- configuration snapshot validation/publication and persisted payload shape;
- discovery metadata relative to enabled and reachable runtime;
- core/module/data/UI dependency direction;
- account SSR forms, antiforgery and return URLs;
- any identifier or protected message crossing a realm boundary.

For these relationships, inspect callers and consumers with `rg`, run focused tests first, then the full solution.

---

## 13. Navigation and Discovery

Prefer live discovery over maintaining another index:

```powershell
# Projects currently in the solution
dotnet sln RoyalIdentity.sln list

# Files and symbols
rg --files
rg -n "SymbolName" .

# Project dependency edges
rg -n "ProjectReference" --glob "*.csproj"

# Pipeline registrations and route mappings
rg -n "builder\.For<|UseDecorator|UseValidator|UseHandler|MapPipeline" RoyalIdentity

# Storage contracts and their implementations/tests
rg -n "interface I.*Store|class .*Store" RoyalIdentity RoyalIdentity.Storage.EntityFramework Tests.Storage

# Active plans touching a surface
rg -n "SurfaceOrTypeName" .ai/plans .ai/backlogs
```

When a command reveals that this document names a removed path, update the **rule or anchor**, not by copying the
new tree wholesale. The goal is that a reader knows how to find the truth and where new code belongs even after
the implementation evolves.
