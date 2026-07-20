# External RoyalCode Libraries — Reference Index

This directory documents the external `RoyalCode.*` libraries consumed by the `RoyalIdentity.UserAccounts`
module family (`RoyalIdentity.UserAccounts`, `.Integration`, `.PostgreSql`, `.Sqlite`). The pure module is
"RoyalCode libraries + EF Core only" (see `../../foundation/architecture.md` §9) — it never references the
core. The core `RoyalIdentity` IdP does **not** depend on this ecosystem; it has its own pipeline/
`context.Response` conventions (root `CLAUDE.md`/`AGENTS.md`). Package versions are pinned in the root
`Directory.Build.props` (`Rc*Ver` properties).

## Document pairs

Each library ships two files here:

- `*.md` — conceptual guide: concepts, examples, gotchas.
- `*.ai-rules.md` — dense, AI-oriented companion: tables of `using`/package, do/don't rules. Read this one
  first when generating code; use the `*.md` for the fuller explanation.

Precedence when a doc disagrees with what you see in the installed package: XML docs/IntelliSense of the
installed version > `*.ai-rules.md` > `*.md`. Each doc's header states the version it was verified against —
if the installed version differs, confirm signatures in the IDE before generating code.

## Index

| Library | Read it for | Files |
|---|---|---|
| SmartCommands | `[Command]`, `[WithWorkContext]`, `[WithValidateModel]`, `[WithRetryOnConcurrency]`, generated Minimal API endpoints | [smart-commands.md](smart-commands.md) / [smart-commands.ai-rules.md](smart-commands.ai-rules.md) |
| WorkContext / UnitOfWork / Repositories | `IWorkContext`, `IUnitOfWork`, `ConcurrencyException`, Sqlite/PostgreSql provider setup and migrations | [workcontext.md](workcontext.md) / [workcontext.ai-rules.md](workcontext.ai-rules.md) |
| SmartSearch | `ICriteria<T>`, `[Criterion]` filters, `Like`/`Contains`/case-insensitive semantics, ordering, DTO projection | [smartsearch.md](smartsearch.md) / [smartsearch.ai-rules.md](smartsearch.ai-rules.md) |
| SmartSelector | Generating DTO projections (`[AutoSelect<T>]`) consumed by SmartSearch/SmartCommands | [selector.md](selector.md) / [selector.ai-rules.md](selector.ai-rules.md) |
| SmartProblems | `Result`/`Result<T>`/`Problem`/`Problems`, `FindResult`, `OkMatch`/`CreatedMatch`/`AcceptedMatch`/`NoContentMatch` | [problems.md](problems.md) / [problems.ai-rules.md](problems.ai-rules.md) |
| SmartValidations | `Rules.Set<T>()`, `IValidable`, `HasProblems` | [validations.md](validations.md) / [validations.ai-rules.md](validations.ai-rules.md) |
| Domain (`Entities`/`DomainEvents`/`Aggregates`) | Base `Entity`/aggregate types, domain events | [domain.md](domain.md) / [domain.ai-rules.md](domain.ai-rules.md) |

## Module-specific conventions live elsewhere

Rules that combine several of these libraries in ways specific to this repo's Feature-Slice module family —
aggregate/value-object conventions, entity constructor shape, member order, the `Commons/`/`{Feature}/` folder
split, HTTP mapping — live in `../../foundation/architecture.md`, not here. Read it first; it takes precedence
over generic library guidance whenever the two overlap.
