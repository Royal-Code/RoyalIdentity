# Architecture Foundation: RoyalIdentity Modules (Feature-Slice)

Operational cut of [feature-slice-architecture.md](../references/architecture/feature-slice-architecture.md)
for **this** solution. That reference holds the full rationale, both lenses, and complete
examples. This file is the directive version: what applies here, where, and how.

**Feature-Slice applies ONLY to domain modules** (`RoyalIdentity.UserAccounts`,
`RoyalIdentity.KMS`). The IdP core, the pipeline, the storage adapters, and the pure-data
projects each have their own architecture — do **not** impose Feature-Slice on them.

A rich module ships as a **family of projects**: the **pure module** (domain + features +
own persistence, **with no reference to the IdP core**), a separate **`.Integration`** adapter
(implements the IdP edge ports; the only project that references the core), and per-provider
persistence projects (**`.PostgreSql`**, **`.Sqlite`**). API/UI are separate projects too.

---

## 1. Which projects use this architecture

| Project | Kind | Feature-Slice? | Architecture to follow |
|---|---|---|---|
| `RoyalIdentity.Pipelines` | pipeline infra | No | dependency-free chain-of-responsibility (see `tech.md`) |
| `RoyalIdentity` (core/IdP) | OIDC engine | No | endpoint → pipeline (`IDecorator`/`IValidator`/`IHandler`) → response (`structure.md`) |
| `RoyalIdentity.Storage.*` (InMemory, EntityFramework[.PostgreSql/.Sqlite], Caching) | storage adapters | No | implement the core `IStorage` facades |
| `RoyalIdentity.Data.Configuration` / `.Operational` | **pure data** | No | `DbContext` + persistence entities + queries only. **No domain, no core types, no business rules** (ADR-013 §2.2) |
| `RoyalIdentity.Razor` | IdP account UI | No | Razor SSR + `I*PageService` (`structure.md`) |
| `RoyalIdentity.Server` | host | No | composition/wiring |
| **`RoyalIdentity.UserAccounts`** | **domain module (pure)** | **Yes** | this document; **no reference to `RoyalIdentity`** |
| **`RoyalIdentity.KMS`** | **domain module (pure)** | **Yes** | this document |
| `RoyalIdentity.{Module}.Integration` | **IdP adapter** | No (seam) | references core + module; implements the edge ports; the **only** bridge to the IdP |
| `RoyalIdentity.{Module}.PostgreSql` / `.Sqlite` | module persistence providers | No | provider mappings/migrations for the module's own `DbContext` |
| `RoyalIdentity.{Module}.Api` / `.Web` | module API/UI | Web layer only | separate sibling projects; consume the module's `Features` |

> If you are unsure whether code belongs to a Feature-Slice module: does it have a rich
> domain with invariants + its own persistence? If not, it is core/data/adapter — not a module.

---

## 2. Module rule (ADR-013 / ADR-015)

A rich domain module is a **family of projects**, not one:

- **`RoyalIdentity.{Module}`** (pure) = **`Domain` + `Features` + `Infrastructure`** with its **own persistence**
  (own `DbContext`). It references **RoyalCode libs + EFCore only** — **never** the IdP core `RoyalIdentity`.
- **`RoyalIdentity.{Module}.Integration`** = the adapter that **implements the IdP edge ports** by delegating to the
  module's features. It is the **only** project that references both `RoyalIdentity` (core) and the module. The host
  wires it via `AddXxxForRoyalIdentity(...)`. This mirrors how `Storage.EntityFramework` adapts `Data.*` (ADR-013 §2.3).
- **`RoyalIdentity.{Module}.PostgreSql` / `.Sqlite`** = provider-specific mappings/migrations for the module's `DbContext`.
  `.Sqlite` also backs in-memory-connection tests.
- **API and UI are NOT in the module.** They are separate `*.Api`/`*.Web` projects (ADR-013 §2.4). A module has **no `Web/` folder**.
- The module owns its **own persistence** — it is **not** adapted by `RoyalIdentity.Storage.EntityFramework` (that is for IdP data only).
- The core **never** references the module; the pure module **never** references the core. Only `.Integration` knows both.
- **Lens: default to Gritante** (screaming) for RoyalIdentity modules — few aggregates, high cohesion. Be consistent within a module.

---

## 3. Module family layout — `UserAccounts` (Gritante)

```text
RoyalIdentity.UserAccounts/                 ← pure module: domain + features + own persistence (NO ref to RoyalIdentity)
  Features/
    Accounts/
      Domain/        UserAccount (Id long + SubjectId), Username, AccountStatus, Email, Credential, Roles, Events/
      Commons/       CreateAccount, SeedAccount, GetAccountForSubject, GetAccountForLogin, SearchAccounts, + DTOs/filters
      ChangePassword/  Activate/  Deactivate/  SetScopeProperties/      (business-intent features)
    ScopeProperties/   PropertyScope, PropertyDefinition, UserAccountPropertyValue (+ validation/seed features)
  Infrastructure/
    Data/          UserAccountsDbContext, mappings, ConfigureUserAccounts (WorkContext)
    Searches/      selectors + named order-by
  Options/         UserAccountsRealmOptions, fixed-field claim projection options

RoyalIdentity.UserAccounts.PostgreSql/      ← provider mappings/indexes/migrations
RoyalIdentity.UserAccounts.Sqlite/          ← provider mappings/migrations + SQLite in-memory test support
RoyalIdentity.UserAccounts.Integration/     ← IdP adapter (refs core + module); implements the edge ports
  SubjectStore : ISubjectStore
  LocalUserAuthenticator : ILocalUserAuthenticator
  UserClaimsProvider : IUserClaimsProvider
  UserAccountsUserDirectory : IUserDirectory
  AddUserAccountsForRoyalIdentity(...)
RoyalIdentity.UserAccounts.Api / .Web       ← separate admin API/UI (own plan; out of scope here)
```

`KMS` follows the same family shape (pure module + `.Integration` + providers). Its `.Integration` exposes the
KMS-to-IdP seam fixed by the future KMS ADR. Do not make the core reference KMS; expect a core-owned port (`IKeyStore` or successor).

---

## 4. Domain — rules

- Aggregate roots inherit `AggregateRoot<TId>` (`RoyalCode.Aggregates`); collect events with `AddEvent`. Events inherit
  `DomainEventBase` and key on the **business identity** (`SubjectId`), never the physical surrogate `Id`. Value objects implement
  `IValidable` with `Rules.Set<T>()`.
- Business methods return `Result`/`Result<T>` (SmartProblems). **Never throw for expected flow.**
- Conventions: protected parameterless ctor wrapped in `#nullable disable/restore`; classes **not sealed**; protected
  `Id`/`Code` setters; member order fields → ctors → properties → methods; XML docs on public members.
- Keep the aggregate's domain **flat inside its folder**. Do **not** split into `Entities/`, `ValueObjects/`, `Services/`, `Policies/`.
- Full examples: `feature-slice-architecture.md` §6 and `../references/external-libraries/domain.md`.

---

## 5. Features — rules

- **`Commons/`** = CRUD-like features (`Create*`, `Get*`, `Search*`) + shared contracts (`*Details`, `*Summary`, `*Filters`).
- **`{Feature}/`** = one folder per **business-intent** use case (`ChangePassword/`, `SetScopeProperties/`). Cross-aggregate
  features go directly under `Features/`.
- **Semantic names.** No `Request`/`Response`/`Command`/`Query`/`Event` suffixes when a domain name exists.
- **Writes** = `RoyalCode.SmartCommands`: partial class + `[Command]` method; the generator produces the handler. Validate with
  `HasProblems` (`RuleSet`). Prefer `WithWorkContext`.
- **Reads** = `RoyalCode.SmartSearch` (`ICriteria<T>`: `FilterBy`/`OrderBy`/`Select<TDto>()`) + `RoyalCode.SmartSelector` for
  projection. There is **no `[Query]` attribute** — do not invent one.
- HTTP mapping: see §6 — keep the module web-agnostic.

---

## 6. Infrastructure — rules

- `Data/` — own `DbContext`, EF mappings, and a `ConfigureXxx(IWorkContextBuilder)` extension that registers
  model/repositories/searches/commands/queries by assembly.
- `Searches/` — selectors and named order-by (register here when projection must unwrap value objects, e.g. `SubjectId.Value`).
- `Messaging/` — outbox/inbox for domain events (deferred for `UserAccounts`; events stay in the aggregate, unpersisted).
- `Gateways/` — external services (interface + impl, `HttpClientFactory`, `ToResultAsync()`, Polly). Prefer the name `Gateways`.
- **HTTP stays out of the module.** Do **not** put SmartCommands `Map*` attributes on the module's commands (that would pull
  ASP.NET routing into the module). Map endpoints **manually in the `*.Api` project** against the generated handlers.

---

## 7. IdP integration seam (RoyalIdentity-specific — read this)

The module exposes itself to the IdP through the separate **`.Integration`** project, by **implementing core-owned ports** —
not by HTTP. The **pure module does not reference the core**; only `.Integration` does.

- `UserAccounts.Integration` implements `ISubjectStore`, `ILocalUserAuthenticator`, `IUserClaimsProvider`, exposed through
  `IUserDirectory`. `KMS.Integration` implements the key-management seam; the core must not reference KMS directly.
- **Primitives + BCL only across the seam (ADR-014 §2.9, amended by ADR-015).** `IUserClaimsProvider` receives identity-scope
  **names** + claim types and returns `IReadOnlyList<Claim>` (`System.Security.Claims.Claim` — a BCL type, not a core type).
  The pure module never sees `IdentityScope`/`RequestedResources`/`Client`; it speaks account/email/role/property/value, and
  `.Integration` projects that into `Claim`.
- **Realm is bound at construction, never a method parameter (ADR-014 §2.5).** `IUserDirectory` (core) still receives the rich
  `Realm`; **`.Integration` is the only project that knows `Realm`** and translates it to `RealmId` + the module's own
  `UserAccountsRealmOptions` before calling the pure module. The realm-bound ports take no realm and read no `HttpContext`.
- **Claims are an intersection (ADR-015 §2.4).** A claim is emitted only when it exists in the module for a requested scope
  **and** its claim type was requested/authorized by the IdP. Adding a property is a two-sided config (module
  `PropertyDefinition` + IdP `IdentityScope.UserClaims`).

```csharp
// {Module}.Integration — realm-bound port; primitives + BCL Claim only; no HttpContext, no realm param.
internal sealed class UserClaimsProvider(/* RealmId + module features */) : IUserClaimsProvider
{
    public Task<IReadOnlyList<Claim>> GetClaimsAsync(
        string subjectId, IReadOnlyCollection<string> identityScopeNames, IReadOnlyCollection<string> claimTypes,
        CancellationToken ct) => /* project fixed fields + roles + dynamic properties → Claim[] (intersection) */;
}

// IUserDirectory binds the realm and returns the realm-bound ports.
internal sealed class UserAccountsUserDirectory(/* module features + options resolver */) : IUserDirectory
{
    public ISubjectStore GetSubjectStore(Realm realm) => /* translate Realm → RealmId + options, new realm-bound store */;
    // GetLocalAuthenticator(realm), GetClaimsProvider(realm)
}
```

> Swapping the in-memory backing for the real module is a **DI registration change** in the host
> (`AddUserAccountsForRoyalIdentity(...)`). The edge (ADR-014) must not be rewritten when the module lands.

---

## 8. Result, Problems, validation

- Validate input with `Rules.Set<T>()` in `HasProblems`; compose flow with `Result.Map`/`Continue`/`Match`. Reserve exceptions for bugs.
- Categories → status: `InvalidParameter` 400, `ValidationFailed` 422, `NotAllowed` 403, `InvalidState` 409, `NotFound` 404.
  Login errors stay **generic** (anti-enumeration); keep the internal reason for events/audit (ADR-014 §2.10).

---

## 9. Dependency direction

```text
RoyalIdentity.UserAccounts / .KMS  (pure)
  → RoyalCode libs + EFCore only.  Does NOT reference RoyalIdentity (core).

RoyalIdentity.{Module}.PostgreSql / .Sqlite
  → the pure module + EF provider.

RoyalIdentity.{Module}.Integration
  → RoyalIdentity (core, for the edge ports + Realm) + the pure module.  Implements the ports.

RoyalIdentity.{Module}.Api / .Web
  → the pure module (admin features).  Does NOT need the IdP core.

RoyalIdentity.Server
  → core + selected modules + their .Integration/providers; wires DI.
```

- Core does **not** reference modules. The **pure module does not reference the core**. Only `.Integration` knows both.
- Inside the pure module: `Domain` depends on nothing; `Features` → `Domain` + `IWorkContext`; `Infrastructure` → `Domain` +
  `Features`; nothing in `Domain`/`Features` references ASP.NET, `HttpContext`, or core types
  (`IdentityScope`/`RequestedResources`/`Client`/`Realm`). Enforce with architecture tests.

---

## 10. Agent quick rules

- Building `UserAccounts`/`KMS` → Feature-Slice (this doc). Touching core/data/adapters → their own architecture; do not import
  module patterns there.
- A rich module = **pure module + `.Integration` + `.PostgreSql`/`.Sqlite`** (+ separate `.Api`/`.Web`). The pure module
  **never references the IdP core**; the edge ports are implemented in `.Integration`.
- Module = Domain + Features + Infrastructure + own persistence. **No `Web/` inside**.
- Writes → SmartCommands (no `Map*` in the module). Reads → SmartSearch/`ICriteria` (no `[Query]`). Domain returns `Result`/`Problems`.
- Seam = **primitives + BCL `Claim`** via `IUserClaimsProvider`; **realm bound at construction**, `Realm` only inside `.Integration`,
  never a method param, never `HttpContext` in a realm-bound port.
- Do not split domain by object type. Use semantic names. Default lens = Gritante.

**References:** [feature-slice-architecture.md](../references/architecture/feature-slice-architecture.md)
(full), `../references/external-libraries/*.md` (SmartCommands/Search/Selector/Validations/Problems/WorkContext/domain),
[ADR-013](../../adrs/ADR-013.md) (module boundaries), [ADR-014](../../adrs/ADR-014.md) (edge ports + seam),
[ADR-015](../../adrs/ADR-015.md) (UserAccounts module + `.Integration` adapter + claims seam `IUserClaimsProvider`).
