# Architecture Foundation: RoyalIdentity Modules (Feature-Slice)

Operational cut of [feature-slice-architecture.md](../references/architecture/feature-slice-architecture.md)
for **this** solution. That reference holds the full rationale, both lenses, and complete
examples. This file is the directive version: what applies here, where, and how.

**Feature-Slice applies ONLY to domain modules** (`RoyalIdentity.UsersAccounts`,
`RoyalIdentity.KMS`). The IdP core, the pipeline, the storage adapters, and the pure-data
projects each have their own architecture — do **not** impose Feature-Slice on them.

---

## 1. Which projects use this architecture

| Project | Kind | Feature-Slice? | Architecture to follow |
|---|---|---|---|
| `RoyalIdentity.Pipelines` | pipeline infra | No | dependency-free chain-of-responsibility (see `tech.md`) |
| `RoyalIdentity` (core/IdP) | OIDC engine | No | endpoint → pipeline (`IDecorator`/`IValidator`/`IHandler`) → response (`structure.md`) |
| `RoyalIdentity.Storage.*` (InMemory, EntityFramework[.Postgre/.Sqlite], Caching) | storage adapters | No | implement the core `IStorage` facades |
| `RoyalIdentity.Data.Configuration` / `.Operational` | **pure data** | No | `DbContext` + persistence entities + queries only. **No domain, no core types, no business rules** (ADR-013 §2.2) |
| `RoyalIdentity.Razor` | IdP account UI | No | Razor SSR + `I*PageService` (`structure.md`) |
| `RoyalIdentity.Server` | host | No | composition/wiring |
| **`RoyalIdentity.UsersAccounts`** | **domain module** | **Yes** | this document |
| **`RoyalIdentity.KMS`** | **domain module** | **Yes** | this document |
| `RoyalIdentity.{Module}.Api` / `.Web` | module API/UI | Web layer only | separate sibling projects; consume the module's `Features` |

> If you are unsure whether code belongs to a Feature-Slice module: does it have a rich
> domain with invariants + its own persistence? If not, it is core/data/adapter — not a module.

---

## 2. Module rule (ADR-013)

A domain module = **`Domain` + `Features` + `Infrastructure` + `Integration`** in one project.

- **API and UI are NOT in the module.** They are separate sibling projects
  (`*.Api`, `*.Web`) — ADR-013 §2.4. So a module has **no `Web/` folder**.
- The module owns its **own persistence** (own `DbContext`). It is **not** adapted by
  `RoyalIdentity.Storage.EntityFramework` (that adapter is for IdP data only).
- The module references core `RoyalIdentity` **only** for the edge-port contracts and the
  primitive seam DTOs (`Subject`, `UserClaimDto`, `AuthenticationResult`). The core
  **never** references the module (DI wires the implementation in the host).
- **Lens: default to Gritante** (screaming) for RoyalIdentity modules — few aggregates,
  high cohesion. Be consistent within a module. Use Explícita only if a module's domain
  grows large and shared.

---

## 3. Module layout — `UsersAccounts` (Gritante)

```text
Modules/UsersAccounts/
  RoyalIdentity.UsersAccounts/              ← module: domain + features + persistence + IdP seam
    Features/
      Accounts/
        Domain/        UserAccount, SubjectId, Username, AccountStatus, Email, ...
        Events/        UserAccountCreated, EmailAdded, PasswordChanged, ...
        Commons/       CreateAccount, UpdateAccount, GetAccountDetails, SearchAccounts, + DTOs/filters
        ChangePassword/        ChangePassword.cs        (business-intent feature)
        LinkExternalIdentity/  LinkExternalIdentity.cs
      ScopeProperties/   (dynamic per-scope properties, if modeled as its own concept)
    Infrastructure/
      Data/          UsersAccountsDbContext, mappings, ConfigureUsersAccounts (WorkContext)
      Searches/      selectors + named order-by
      Messaging/     outbox/inbox for domain events, replication
    Integration/     ← implements the core edge ports (see §7)
      SubjectStore.cs                : ISubjectStore
      LocalUserAuthenticator.cs      : ILocalUserAuthenticator
      UserPropertyProvider.cs        : IUserPropertyProvider
      UsersAccountsUserDirectory.cs  : IUserDirectory   (realm-bound factory)
  RoyalIdentity.UsersAccounts.Api/          ← separate project: admin API
  RoyalIdentity.UsersAccounts.Web/          ← separate project: admin UI (Razor)
```

`KMS` follows the same shape (aggregates: Key/Certificate/Secret; business-intent features
like `RotateKey/`, `IssueCertificate/`). Its `Integration/` layer exposes the KMS-to-IdP
seam fixed by the future KMS ADR. Do not make the core reference KMS; expect a core-owned
facade/port such as `IKeyStore` or its successor.

---

## 4. Domain — rules

- Aggregate roots inherit `AggregateRoot<TId>` (`RoyalCode.Aggregates`); collect events with
  `AddEvent`. Events inherit `DomainEventBase`. Value objects implement `IValidable` with
  `Rules.Set<T>()`.
- Business methods return `Result`/`Result<T>` (SmartProblems). **Never throw for expected
  flow.**
- Conventions: protected parameterless ctor wrapped in `#nullable disable/restore`; classes
  **not sealed**; protected `Id`/`Code` setters; member order fields → ctors → properties →
  methods; XML docs on public members.
- Keep the aggregate's domain **flat inside its folder**. Do **not** split into
  `Entities/`, `ValueObjects/`, `Services/`, `Policies/`.
- Full examples: `feature-slice-architecture.md` §6 and `../references/external-libraries/domain.md`.

---

## 5. Features — rules

- **`Commons/`** = CRUD-like features (`Create*`, `Update*`, `Get*Details`, `Search*`) +
  shared contracts (`*Details`, `*Summary`, `*Filters`).
- **`{Feature}/`** = one folder per **business-intent** use case (`ChangePassword/`,
  `RotateKey/`). Cross-aggregate features go directly under `Features/`.
- **Semantic names.** No `Request`/`Response`/`Command`/`Query`/`Event` suffixes when a
  domain name exists.
- **Writes** = `RoyalCode.SmartCommands`: partial class + `[Command]` method; the generator
  produces the handler. Validate with `HasProblems` (`RuleSet`). Prefer `WithWorkContext`.
- **Reads** = `RoyalCode.SmartSearch` (`ICriteria<T>`: `FilterBy`/`OrderBy`/`Select<TDto>()`)
  + `RoyalCode.SmartSelector` for projection. There is **no `[Query]` attribute** — do not
  invent one.
- HTTP mapping: see §6 — keep the module web-agnostic.

---

## 6. Infrastructure — rules

- `Data/` — own `DbContext`, EF mappings, and a `ConfigureXxx(IWorkContextBuilder)`
  extension that registers model/repositories/searches/commands/queries by assembly.
- `Searches/` — selectors and named order-by (register here when projection must unwrap
  value objects, e.g. `Code.Value`).
- `Messaging/` — outbox/inbox for domain events; replication.
- `Gateways/` — external services (interface + impl, `HttpClientFactory`, `ToResultAsync()`,
  Polly). Prefer the name `Gateways`.
- **HTTP stays out of the module.** Do **not** put SmartCommands `Map*` attributes on the
  module's commands (that would pull ASP.NET routing into the module). Map endpoints
  **manually in the `*.Api` project** against the generated handlers.

---

## 7. IdP integration seam (RoyalIdentity-specific — read this)

The module exposes itself to the IdP by **implementing core-owned ports**, not by HTTP.
Put these implementations in `Integration/`.

- `UsersAccounts` implements `ISubjectStore`, `ILocalUserAuthenticator`,
  `IUserPropertyProvider`, exposed through `IUserDirectory`. `KMS` implements the
  key-management seam fixed by the KMS ADR; the core must not reference KMS directly.
- **Primitives only across the seam (ADR-014 §2.9).** `IUserPropertyProvider` receives
  identity-scope **names** + claim types and returns `UserClaimDto[]`. The module **never**
  sees `IdentityScope`, `RequestedResources`, or any rich core type.
- **Realm is bound at construction, never a method parameter (ADR-014 §2.5).** The
  factory/`IUserDirectory` binds the realm; realm-bound ports do not take a realm and do not
  read `HttpContext`. Orchestration services resolve the realm via `ICurrentRealmAccessor`.

```csharp
// Integration/ — realm-bound port; primitives only; no HttpContext, no realm param.
internal sealed class UserPropertyProvider(/* realm-bound deps */) : IUserPropertyProvider
{
    public Task<UserClaimDto[]> GetClaimsAsync(
        string subjectId, IEnumerable<string> identityScopeNames, IEnumerable<string> claimTypes,
        CancellationToken ct) => /* project account properties → UserClaimDto[] */;
}

// IUserDirectory binds the realm and returns the realm-bound ports.
internal sealed class UsersAccountsUserDirectory(/* module persistence */) : IUserDirectory
{
    public ISubjectStore GetSubjectStore(Realm realm) => /* new realm-bound store */;
    // GetLocalAuthenticator(realm), GetPropertyProvider(realm)
}
```

> Swapping the in-memory backing for the real module is a **DI registration change** in the
> host. The edge (ADR-014) must not be rewritten when the module lands.

---

## 8. Result, Problems, validation

- Validate input with `Rules.Set<T>()` in `HasProblems`; compose flow with
  `Result.Map`/`Continue`/`Match`. Reserve exceptions for bugs.
- Categories → status: `InvalidParameter` 400, `ValidationFailed` 422, `NotAllowed` 403,
  `InvalidState` 409, `NotFound` 404. Login errors stay **generic** (anti-enumeration); keep
  the internal reason for events/audit (ADR-014 §2.10).

---

## 9. Dependency direction

```text
RoyalIdentity.UsersAccounts / .KMS
  references RoyalIdentity only for core-owned edge ports and primitive seam DTOs

RoyalIdentity.{Module}.Api / .Web
  references the module and maps/administers its Features

RoyalIdentity.Server
  references RoyalIdentity + selected modules/API/UI projects and wires DI
```

- Core does **not** reference modules. Modules do **not** reference each other's internals.
- Inside a module: `Domain` depends on nothing; `Features` → `Domain` + `IWorkContext`;
  `Infrastructure`/`Integration` → `Domain` + `Features`; nothing in `Domain`/`Features`
  references ASP.NET. Enforce with architecture tests.

---

## 10. Agent quick rules

- Building `UsersAccounts`/`KMS` → Feature-Slice (this doc). Touching core/data/adapters →
  their own architecture; do not import module patterns there.
- Module = Domain + Features + Infrastructure + Integration. **No `Web/` inside**; API/UI are
  separate projects; module owns its persistence.
- Writes → SmartCommands (no `Map*` in the module). Reads → SmartSearch/`ICriteria` (no
  `[Query]`). Domain returns `Result`/`Problems`.
- Seam = **primitives only**; **realm bound at construction**, never a method param, never
  `HttpContext` in a realm-bound port.
- Do not split domain by object type. Use semantic names. Default lens = Gritante.

**References:** [feature-slice-architecture.md](../references/architecture/feature-slice-architecture.md)
(full), `../references/external-libraries/*.md` (SmartCommands/Search/Selector/Validations/Problems/WorkContext/domain),
[ADR-013](../../adrs/ADR-013.md) (module boundaries), [ADR-014](../../adrs/ADR-014.md) (edge ports + seam).
