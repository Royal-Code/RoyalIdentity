# Matriz de Testes: `RoyalIdentity.UserAccounts`

**Status:** rascunho inicial para guiar as Fases 7-10.

Esta matriz complementa `plan-users-accounts-module-v2.md`. A Fase 10 cobre contract tests, DI opt-in, seeds e
regressão, mas os testes não devem ficar concentrados só nela. Cada fase deve entregar cobertura compatível com o
risco que introduz.

## Objetivo

1. Tornar explícitos os cenários que precisam existir antes da troca do fake pelo módulo real.
2. Evitar que bugs de domínio, persistência, realm isolation e claims só apareçam em testes de ponta a ponta.
3. Separar testes de domínio, persistência, casos de uso, integração com IdP e regressão.

## Estado Atual

| Área | Cobertura atual | Gap principal |
|---|---|---|
| Options do módulo | Parcial em `Tests.UserAccounts` | variações combinadas de login/email/projeções |
| `UserAccount` domínio | Boa cobertura inicial | casos negativos de coleção/email/roles e eventos |
| Scope properties domínio | Boa cobertura inicial | mutações de draft/pending/rejected e required prospectivo |
| Persistência | Coberta (Fase 7) | round-trip completo, índices, constraints e queries (Sqlite in-memory) |
| Casos de uso | Coberta (Fase 8) | create/find/login/auth/claims/properties via `UserAccountUseCasesTests` (18 casos) |
| Integração IdP | Coberta (Fase 9) | portas `.Integration` sobre o módulo real + Sqlite via `UserAccountsIntegrationTests` (9 casos); contract tests compartilhados fake×módulo na Fase 10 |
| Regressão end-to-end | Coberta (Fase 10) | suite do IdP contra fake + regressao HTTP opt-in com `UserAccountsAppFactory` |

## Fase 4/Options

- `LoginWithEmail = true` com `AllowDuplicateEmail = true` deve falhar na validação.
- `EmailAsUsername = true` com `AllowDuplicateEmail = true` deve falhar na validação.
- Projeções fixas default devem conter `profile/name`, `profile/preferred_username`, `profile/role`,
  `email/email` e `email/email_verified`.
- Projeções fixas com `Include = false` não devem emitir claim.
- Colisão entre projeção fixa e propriedade dinâmica deve ser detectada por claim type.
- Options copiadas por realm não devem compartilhar coleções mutáveis entre realms.

## Fase 5/Accounts Domain

- Criar conta inicializa `RealmId`, `SubjectId`, `Username`, `NormalizedUsername`, `DisplayName`, `IsActive`,
  `CreatedAt`, `UpdatedAt`, `BlockState` e credencial local.
- Alterar username não altera `SubjectId`.
- `AddEmail` rejeita realm diferente.
- `AddEmail` rejeita email duplicado dentro da mesma conta.
- Primeiro email adicionado vira primário.
- Troca de email primário limpa o primário anterior.
- `AddRole` rejeita realm diferente e role duplicada normalizada.
- `RemoveRole` deve ser idempotente para role inexistente.
- `Block` e `Unblock` alteram somente o estado administrativo, sem mexer em lockout de credencial.
- `AuthenticateLocal` falha para conta inativa.
- `AuthenticateLocal` falha para conta bloqueada.
- `AuthenticateLocal` falha quando não há senha.
- Falha de senha incrementa contador e cria lockout quando atinge limite.
- Sucesso de senha zera contador de falhas.
- Lockout expirado deve ser limpo antes da autenticação.
- `SetPassword` e `ChangePassword` emitem evento de credential changed.

## Fase 6/Scope Properties Domain

- Criar `PropertyScope` cria uma version draft inicial.
- `ApproveVersion` arquiva a version ativa anterior.
- Draft criada a partir de ativa copia todas as `PropertyDefinitionVersion`.
- Alterar definition em draft não muda projeção/escrita ativa antes da aprovação.
- `AddDefinition`, `UpdateDefinition`, `ActivateDefinition` e `DeactivateDefinition` devem falhar em version ativa,
  pending, archived ou rejected.
- `SubmitVersionForApproval` deve aceitar somente draft.
- `RejectVersion` deve aceitar somente draft ou pending.
- `ApproveVersion` deve aceitar somente draft ou pending.
- `AddDefinition` deve reutilizar `PropertyDefinition` estável quando o claim type já existir no scope.
- `PropertyDefinitionVersion.ClaimType` deve continuar preenchido mesmo sem navegação `PropertyDefinition` carregada.
- `DeactivateDefinition` em draft deve preservar `UserAccountPropertyValue` existente e remover projeção apenas após aprovação.
- `PropertyScope.Deactivate` deve preservar valores e suprimir projeção/escrita.
- `IsRequired` deve validar novas escritas, mas não deve quebrar projeção de contas existentes sem valor.
- `IsCollection = false` rejeita múltiplos valores.
- `MinItems`/`MaxItems` validam cardinalidade de coleção.
- `MinLength`/`MaxLength` validam texto.
- `RegexPattern` só é aceito para texto; em outros tipos é erro de configuração.
- `Range` valida `Integer`, `Decimal`, `Date`, `DateTime` e `Time`.
- `Range` em `Text` ou `Boolean` é erro de configuração.
- `AllowedValues` deve ser parseado pelo `ValueType` e comparado em formato canônico.
- Validador customizado ausente deve gerar erro de configuração.
- Validador customizado registrado deve receber `RealmId`, `SubjectId`, `ClaimType`, valor bruto, valor canônico,
  valor parseado e parâmetros.
- Valores persistidos devem apontar para `PropertyDefinition`, não para `PropertyDefinitionVersion`.
- `UserAccountClaimProjector` deve aplicar interseção por scope solicitado e claim type autorizado.
- Conta inativa não deve emitir claims.

## Fase 7/Persistência

- Round-trip de `UserAccount` completo: credencial, emails, roles, block state e property values.
- Round-trip de `PropertyScope` completo: versions, definitions, definition versions, status e active version.
- `PropertyDefinitionVersion.ClaimType` deve persistir como coluna e funcionar sem include da navegação.
- `ActiveVersionId` deve apontar para a version ativa após `SaveChanges`, inclusive quando aprovada antes do insert.
- Constraints únicas:
  - `(RealmId, SubjectId)`
  - `(RealmId, NormalizedUsername)`
  - `(RealmId, UserAccountId, NormalizedAddress)`
  - `(RealmId, UserAccountId, NormalizedName)`
  - `(RealmId, Name)` em `PropertyScope`
  - `(RealmId, ClaimType)` em `PropertyDefinition`
  - `(PropertyScopeVersionId, PropertyDefinitionId)`
  - `(UserAccountId, PropertyDefinitionId, Ordinal)`
- Queries principais devem filtrar por `RealmId`.
- Query por login username deve usar `NormalizedUsername`.
- Query por email deve considerar email primário/verificado conforme policy do módulo.
- Persistência de `PropertyValidationRules` deve preservar range, allowed values, regex, cardinalidade e custom validators.
- Troca de `ValueType` com valores existentes deve ser bloqueada ou exigir migração explícita.
- SQLite in-memory deve cobrir round-trip sem banco externo.

## Fase 8/Casos de Uso

- Criar conta gera `SubjectId` quando não informado.
- Criar conta aceita `SubjectId` determinístico quando permitido pelo caso de uso.
- Criar conta rejeita subject duplicado no mesmo realm.
- Criar conta permite mesmo subject em realms diferentes.
- Buscar por `(RealmId, SubjectId)` não retorna conta de outro realm.
- Buscar login por username respeita normalização.
- Buscar login por email respeita `LoginWithEmail`, `EmailAsUsername`, `AllowDuplicateEmail`, primário e verificado.
- Autenticação local retorna motivo interno correto para inactive, blocked, password not set, invalid credentials e lockout.
- Projeção de claims via caso de uso deve combinar fixed fields, roles e dynamic values.
- Definir properties deve validar contra active version e falhar para version inativa ou scope inativo.

## Fase 9/Integração IdP

- `UserAccountsUserDirectory` deve ser realm-bound: portas retornadas não recebem realm no método.
- `.Integration` deve ser o único projeto que conhece `Realm` do IdP.
- `SubjectStore` deve mapear `SubjectId` para subject do IdP sem vazar `UserAccount.Id`.
- `LocalUserAuthenticator` deve manter erro externo genérico e reason interno preservado.
- `UserClaimsProvider` deve retornar `Claim` da BCL somente na borda, não no módulo puro.
- Scopes e claim types recebidos do IdP devem ser tratados como interseção, não como autorização implícita do módulo.
- Claims não configuradas no IdP não devem ser emitidas mesmo existindo no módulo.
- Claims configuradas no IdP mas ausentes no módulo não devem ser emitidas.

## Fase 10/Contracts e Regressão

- Contract tests compartilhados para `IUserDirectory`. **[OK]** `UserDirectoryContractTests` (base abstrata) roda contra
  `InMemory` (fake) e `UserAccountsSqlite` (módulo real): subject store, autenticação local, lockout/reset de contador,
  passwordless/inactive, realm isolation e claims de seed.
- Mesmo contrato executado contra fake in-memory e contra `UserAccounts` real com SQLite in-memory. **[OK]**
- Seeds Alice/Bob devem produzir os mesmos `sub`, claims, roles e resultado de autenticação esperados hoje. **[OK]**
- Suite do IdP deve rodar contra fake atual. **[OK]** (default `Tests.Integration`).
- Suite do IdP deve rodar contra módulo opt-in. **[Parcial]** Hoje a regressão opt-in é **representativa** —
  `UserAccountsOptInRegressionTests` (5 testes HTTP) sobre `UserAccountsAppFactory`. Rodar a **suíte inteira** contra o
  módulo depende da substituição do fake (ADR-018 / backlog), pois o restante da suíte e os seeds dinâmicos
  (`CharacterizationSeed`) ainda escrevem no `MemoryStorage`.
- Regressão de realm isolation: dados criados em um realm não aparecem em outro. **[OK]**
- Regressão de login/logout/session deve continuar verde com fake e com módulo opt-in. **[OK]**
- Build completo da solution deve rodar antes de considerar a fase finalizada. **[OK]**

### Concorrência (pré-plano §10) — `ConcurrencyTests` (módulo + Sqlite)

Provados via conexão Sqlite in-memory compartilhada entre escopos (interleaving real; o conflito é detectado, não
simulado). Estratégia: optimistic concurrency + retry via `UserAccount.Version` (credencial/contador/stamp) e UPDATE
condicional idempotente para tokens (Q11).

1. **Duas falhas simultâneas** → o `Version` rejeita o writer obsoleto; nenhum incremento é perdido (após retry, 2).
2. **Sucesso × falha** → a falha obsoleta não persiste sobre o login válido; a conta não fica bloqueada.
3. **Consumo duplo de token** → o UPDATE condicional consome no máximo uma vez (um `true`, um `false`).
4. **Nova emissão × token antigo** → reemitir revoga o anterior; o token antigo deixa de ser consumível.
5. **Troca/reset de senha × login obsoleto** → conflito de `Version`; `SecurityStamp` e `SessionsValidAfter` movem.
6. **Admin unlock × falha obsoleta** → o unlock vence; a falha obsoleta não re-bloqueia (conflito de `Version`).
7. **Verificação de email × troca de email** → o token, ligado ao `TargetValue`, verifica só o valor vinculado (o novo
   primário permanece não-verificado).

### Comportamentos module-specific (cobertos fora do contrato compartilhado)

`required action`, verificação de email/telefone e invalidação/`SessionsValidAfter` **não** entram no contrato
fake×módulo: o fake (`MemoryLocalUserAuthenticator`) não modela esses fluxos (ex.: nunca retorna `RequiresAction`;
`GetSecurityStateProvider` é `null`). Forçar paridade exigiria reimplementar o ciclo de segurança no fake — fora do
escopo desta matriz (que pede o contrato de `IUserDirectory`, não a paridade de todo o ciclo). Cobertura module-side:
`UserAccountUseCasesTests` (required action / expired / recovery / verificação / lockout / unlock / block),
`UserAccountsIntegrationTests` (security-state Q15) e `Tests.Integration/SessionLifecycleTests` (invalidação por estado).
**Decisão fechada ([ADR-018](../../adrs/ADR-018.md)):** o fake in-memory é transitório — **não** se investe em paridade;
o alvo é substituí-lo pelo módulo + Sqlite in-memory, com um **seed reutilizável do módulo** como primeiro passo.

## Critério Mínimo Antes de Trocar o Fake

- Todos os testes de domínio e persistência do módulo verdes.
- Contract tests do fake e do módulo real verdes.
- Suite do IdP verde com fake.
- Suite do IdP verde com módulo opt-in.
- Nenhuma query de conta, credencial, property ou claim sem filtro de realm.
