# Decisões finais do plano UserAccounts — rodada 04

Data: 2026-06-16

## Escopo

Fecha os itens que ficaram **delegados ou pendentes** após [an-users-plan-03.md](an-users-plan-03.md)
(confirmados no parecer [an-users-plan-avail-A.md](an-users-plan-avail-A.md)):

1. **Split de `AccountOptions`** item-a-item (era a maior área aberta — plan-03 §6 só fechou `AllowDuplicateEmail`).
2. **Schema e persistência das propriedades por escopo** (P-02.2 — antes só remetido à ADR-015).
3. **`PostgreSql`** em toda a solução (padronizar grafia, inclusive o lado IdP).
4. **Ajuste dos nomes** no ripple de `IUserClaimsProvider`.
5. **Options do próprio módulo** (mecanismo das opções por realm do `UserAccounts`).
6. **Confirmação** da consequência operacional da interseção (§4 do plan-03).

Entrada para corrigir/fechar: `plan-users-accounts-module.md`, `architecture.md`, emenda à `ADR-014`, nova `ADR-015`,
e o `CLAUDE.md`/roadmap onde aplicável.

---

## 1. Split de `AccountOptions` (item-a-item)

**Princípio (plan-03 §6):** **um único dono** por opção (sem duplicar regra nos dois lados). O **IdP** fica com
fluxo/protocolo/cookie/UI de login. O **`UserAccounts`** fica com regra de conta, identificador de login, email,
username, credencial, lockout e ciclo de vida. Onde a UI de login do IdP precisa de um flag de afordância de conta
(ex.: link "esqueci a senha"), ela **lê a política do módulo pela integração** — não duplica o flag.

### `AccountOptions`

| Opção | Dono | Por quê | Vigora neste plano? |
|---|---|---|---|
| `AllowLocalLogin` | **IdP** | Gate do **fluxo** de login local (combinado com `Client.EnableLocalLogin`); o módulo só autentica quando chamado. | Sim (inalterado) |
| `AllowRememberLogin` | **IdP** | Persistência de cookie = I/O de cookie é do IdP (ADR-014 §2.5). | Sim (inalterado) |
| `RememberMeLoginDuration` | **IdP** | Idem cookie. | Sim (inalterado) |
| `AutomaticRedirectAfterSignOut` | **IdP** | Comportamento de **logout/UI**. | Sim (inalterado) |
| `InvalidCredentialsErrorMessage` | **IdP** | Mensagem genérica da UI (anti-enumeração); o módulo devolve só o **reason** (enum). Já `[Redesign "Usar Resource"]`. | Sim (vira Resource depois) |
| `InactiveUserErrorMessage` | **IdP** | Idem. | Sim |
| `BlockedUserErrorMessage` | **IdP** | Idem. | Sim |
| `AllowRegistration` | **UserAccounts** | Ciclo de vida da conta (auto-cadastro). | Política sim; **UI deferida** (#5) |
| `AllowForgotPassword` | **UserAccounts** | Recuperação de credencial. **Consolidar** (duplicado em `PasswordOptions`). | Política sim; fluxo recuperação → #3 |
| `AllowChangePassword` | **UserAccounts** | Credencial. **Consolidar** (duplicado em `PasswordOptions`). | Política sim |
| `AllowUpdateProfile` | **UserAccounts** | Edição de perfil. | Política sim; casos admin → #5 |
| `AllowChangeEmail` | **UserAccounts** | Política de email. | Política sim; casos admin → #5 |
| `AllowChangeUsername` | **UserAccounts** | Política de username (separado do `SubjectId` imutável). | Política sim |
| `AllowChangePhoneNumber` | **UserAccounts** | Telefone é dado de conta. | Política sim; casos admin → #5 |
| `AllowDeleteAccount` | **UserAccounts** | Ciclo de vida. | Política sim; casos admin → #5 |
| `EmailAsUsername` | **UserAccounts** | **Resolução de identificador de login** é do módulo (ADR-014 / pontos1 §2). | **Sim** (autenticador) |
| `LoginWithEmail` | **UserAccounts** | Idem resolução de login. | **Sim** (autenticador) |
| `AllowDuplicateEmail` | **UserAccounts** | Já decidido (plan-03 §6). | Sim |
| `VerifyEmail` | **UserAccounts** | Política de verificação de email. | Política sim; fluxo verificação → #3 |
| `PasswordOptions` | **UserAccounts** | Política de credencial (ver abaixo). | Lockout **sim**; resto → #3 |
| `AllowTwoFactorAuthentication` | **fora de escopo** | MFA → plano #7. | Não |
| `AllowSocialLogin` | **fora de escopo** | Federação → plano #6. | Não |

### `PasswordOptions` (todo o objeto → **UserAccounts**)

| Opção | Vigora neste plano? |
|---|---|
| `MaxFailedAccessAttempts`, `AccountLockoutDurationMinutes` | **Sim** — lockout no `ILocalUserAuthenticator` (D1/D2). |
| `MinimumLength`, `MaximumLength`, `RequireSpecialCharacters`, `RequireDigit`, `RequireLowercase`, `RequireUppercase`, `MinimumUniqueCharacters`, `DisallowUsernameInPassword`, `DisallowBirthdateInPassword`, `DisallowedWordsInPassword` | Política mora no módulo; **enforcement** ao `SetPassword`/`ChangePassword`. |
| `EnablePasswordExpiration`, `PasswordExpirationDays`, `EnforcePasswordHistory`, `PasswordHistoryCount` | Política mora no módulo; **enforcement → plano #3** (security lifecycle). |
| `AllowForgotPassword`, `AllowChangePassword` | **Duplicados** com `AccountOptions` → consolidar **num lugar só** (credencial, no módulo). |

**Consequências:**
- A `AccountOptions` do IdP **encolhe** para o conjunto IdP; o restante migra para a options do módulo (§5).
- Eliminar a **duplicação** `AllowForgotPassword`/`AllowChangePassword` (hoje em dois lugares).
- Durante a coexistência (fake), o autenticador in-memory segue lendo `realm.Options.Account`; ao módulo entrar, passa a
  ler a options do módulo (§5). A integração faz a ponte.
- A página de login do IdP usa só `AllowLocalLogin`/`AllowRememberLogin` (+ provedores externos); afordâncias de conta
  (esqueci/registrar) refletem a **política do módulo** via integração (fonte única).

---

## 2. Schema e persistência das propriedades por escopo

**Modelo canônico — três níveis (definição × valor × emissão):**

```text
PropertyScope            (schema, por realm)      unique (RealmId, Name);  Name ↔ IdentityScope.Name; Status {Draft, Active}
  └─ PropertyDefinition  (schema, por realm)      unique (RealmId, ClaimType)   ← "ClaimType único por Realm"
        { ClaimType, ValueType, DisplayName, Help, IsSensitive, IsRequired, Multiplicity {Single, Multi}, Validation[…], IsActive }

UserAccountPropertyValue (valor, por conta)       index (RealmId, SubjectId, ClaimType)[, Ordinal]   ← "um valor por linha"
  { RealmId, SubjectId, ClaimType, Value, ValueType?, Ordinal }
```

- **`ClaimType` é único por realm na definição** (chave natural `(RealmId, ClaimType)`): um claim type ⇒ exatamente uma
  definição ⇒ um escopo, por realm.
- **Um valor por linha** em `UserAccountPropertyValue`. **Single** ⇒ 1 linha (índice **único** `(RealmId, SubjectId, ClaimType)`);
  **Multi** ⇒ N linhas (`Ordinal` desempata; índice **não-único**).
- **Campos fixos NÃO entram** nessa tabela (plan-03 §5): `Username`/`DisplayName`/`Emails`/`Roles`/`ExternalId` são colunas
  de 1ª classe do `UserAccount`, projetados por **opções de projeção** `{ScopeName, ClaimType, Include}` (na options do módulo, §5).
- **Vocabulário (plan-03 §2):** domínio/persistência falam **propriedade / definição / valor**; `ClaimType`/`Value` são
  strings (a *matéria* da projeção), **distintos** do tipo `System.Security.Claims.Claim`, que só aparece **na borda/integração**.

### Como o `IUserClaimsProvider` monta o resultado (interseção — §4)

Dado `(subjectId, identityScopeNames, claimTypes)` do IdP, a integração:
1. **Campos fixos:** para cada opção `{ScopeName ∈ identityScopeNames, Include=true, ClaimType ∈ claimTypes}` → emite o valor do campo.
2. **Dinâmicas:** `UserAccountPropertyValue ⋈ PropertyDefinition` onde `Definition.ScopeName ∈ identityScopeNames` **e**
   `ClaimType ∈ claimTypes` **e** `IsActive` → emite `(ClaimType, Value, ValueType)`.
3. **Roles:** se o claim type `role` ∈ `claimTypes` e há mapeamento de role → emite roles.
4. Conta inexistente/inativa ⇒ `[]` (paridade com o fake).
5. Converte para `IReadOnlyList<Claim>` **na integração** (não no domínio).

> **Regra de unicidade ampliada:** `ClaimType` é único por realm **através de campos fixos + definições dinâmicas**.
> A configuração deve **rejeitar colisão** (ex.: projeção fixa `name` e uma definição dinâmica `name` no mesmo realm),
> evitando claim duplicada. Como o `ClaimType` é único, a projeção **não precisa deduplicar**.

### Opções de leitura (performance) — prós/contras

- **(A) Ler as linhas sob demanda** (join definição × valor, filtrar por escopo+tipo). **Recomendada agora.**
  Prós: canônica, simples, sempre consistente. Contras: N linhas por projeção.
- **(B) Read model desnormalizado por `(conta, escopo)` em JSON** — `UserAccountScopeClaims { RealmId, SubjectId, ScopeName, ClaimsJson, UpdatedAt }`,
  mantido **na escrita**: a projeção de um escopo lê **1 linha**. Prós: leitura rápida (o ganho que o dono citou). Contras:
  manutenção na escrita + risco de inconsistência. **Aditivo** — a fonte de verdade continua a tabela de valores.
- **(C) Cache** (in-memory/distribuído) sobre o resultado do provider. **Futuro** (estratégia de cache fica para depois,
  conforme o dono).

**Recomendação:** **(A) já**, modelando a tabela de valores de modo que **(B)** possa ser acrescentado **sem mudar schema**
(o JSON é derivado); **(C)** diferido. Justificativa: correção e simplicidade primeiro; otimização de leitura quando houver
métrica que a justifique — sem comprometer o schema canônico.

**Índices/unicidade (resumo):** `PropertyScope` unique `(RealmId, Name)`; `PropertyDefinition` unique `(RealmId, ClaimType)`;
`UserAccountPropertyValue` unique `(RealmId, SubjectId, ClaimType)` para Single (com `Ordinal` para Multi). `RealmId`
**estrutural** em todas (R-01.4).

---

## 3. Padronização de nomes de provedor: `PostgreSql`

**Decisão:** usar a grafia **`PostgreSql`** em **toda** a solução (o lado IdP também). `Sqlite` permanece `Sqlite`.

| Antes (docs/planejado) | Depois |
|---|---|
| `RoyalIdentity.UserAccounts.Postgre` (cogitado) | `RoyalIdentity.UserAccounts.PostgreSql` |
| `RoyalIdentity.Storage.EntityFramework.Postgre` (ADR-013/roadmap/arch — **não construído**) | `RoyalIdentity.Storage.EntityFramework.PostgreSql` |

Família final do módulo: `RoyalIdentity.UserAccounts` / `.Integration` / `.PostgreSql` / `.Sqlite`.

**Consequências (doc-only, pois esses projetos ainda não existem):** atualizar ADR-013 §2.3, `plans-roadmap-01.md` item 2,
e a tabela de `architecture.md` §1 para `PostgreSql`.

---

## 4. Ajuste de nomes (ripple de `IUserClaimsProvider`)

A renomeação `IUserPropertyProvider → IUserClaimsProvider` (plan-03 §2) **propaga**:

| Antes | Depois |
|---|---|
| `IUserPropertyProvider` | `IUserClaimsProvider` |
| `IUserDirectory.GetPropertyProvider(realm)` | `IUserDirectory.GetClaimsProvider(realm)` |
| `MemoryUserPropertyProvider` (fake) | `MemoryUserClaimsProvider` |
| retorno `IReadOnlyList<UserClaimDto>` | `IReadOnlyList<Claim>` (remove `UserClaimDto`) |

Atualizar consumidores: `DefaultProfileService` e testes da borda. **Vocabulário:** o **domínio** do módulo continua em
`PropertyScope`/`PropertyDefinition`/valor; **`Claim`** só na borda/integração. (Não renomear os tipos de domínio para "Claim".)

---

## 5. Options do próprio módulo

**Decisão:** o `UserAccounts` tem **options próprias por realm** (não pendura na `AccountOptions`/`RealmOptions` do IdP).
Mesmo padrão **copy-on-create por realm** já adotado no IdP (`plan-realm-options-redesign`, COMPLETED).

Nome proposto: **`UserAccountsRealmOptions`** (a confirmar na ADR-015). Conteúdo (recebe o que migrou no §1 + §2/§5):

- **Identificador de login:** `EmailAsUsername`, `LoginWithEmail`.
- **Email:** `AllowDuplicateEmail`, `VerifyEmail`, múltiplos, **fictício** (pattern por realm ex.: `mycompany_{username}@…`,
  `IsVerified` default), `AllowChangeEmail`.
- **Username:** `AllowChangeUsername`.
- **Conta/ciclo de vida:** `AllowRegistration`, `AllowUpdateProfile`, `AllowChangePhoneNumber`, `AllowDeleteAccount`.
- **Credencial:** política de senha (complexidade/lockout/expiração/histórico — enforcement conforme §1), `AllowForgotPassword`/`AllowChangePassword` (consolidados).
- **`SubjectId`:** permitir informar no cadastro (default: gerar) — plan-03 §7.
- **Projeção de campos fixos:** lista `{ ScopeName, ClaimType, Include }` por campo fixo (plan-03 §5).

**Quem lê:** o autenticador e o claims provider do módulo leem `UserAccountsRealmOptions`; a **integração/host** configura
essas options por realm. A `AccountOptions` do IdP deixa de carregar essas regras.

---

## 6. Consequência confirmada: interseção com o catálogo do IdP

**Confirmado (plan-03 §4 / dono):** um claim type só é **emitido** se **também** estiver declarado no
`IdentityScope.UserClaims` do IdP para um escopo solicitado — a interseção exige que o IdP o tenha **anunciado/autorizado**
(coerente com discovery/`ShowClaims` e consent). Portanto **adicionar uma propriedade é configuração de dois lados**:
`PropertyDefinition` no `UserAccounts` **e** o claim type no `IdentityScope.UserClaims` do IdP.

- É o **comportamento atual** dos seeds (role não sai porque nenhum identity scope a declara) — não é regressão.
- A automação futura (plan-02 Q-G2: `PropertyScope` *draft → active → evento cria o `IdentityScope` no IdP*) **fica para depois**;
  por ora a coerência é **operacional/administrativa**.

---

## Pontos já fechados (recap, sem reabrir)

net10.0; resources **COMPLETED** (⇒ `IdentityScope.Name` estável); módulo `UserAccounts` (User singular); seam retorna
`IReadOnlyList<Claim>` via `IUserClaimsProvider`; projetos `UserAccounts`/`.Integration`/`.PostgreSql`/`.Sqlite` (módulo puro
**sem** ref ao IdP); IdP autoritativo sobre scopes/claims (interseção); `SubjectId` gerado/externo-opcional/imutável/único por
realm `(RealmId, SubjectId)`; eventos no agregado sem persistir; manter fake → migrar para **SQLite in-memory**; cortar
endpoints/admin (→ #5); pré-flight das libs `RoyalCode.*`; contract tests de borda; coexistência opt-in.

## Próximos ajustes nos documentos (consolidado plan-03 + plan-04)

1. **`plan-users-accounts-module.md`** — renomear para `UserAccounts`; separar `.Integration`/`.PostgreSql`/`.Sqlite`;
   incluir o split de `AccountOptions` (§1), o schema de propriedades (§2), `UserAccountsRealmOptions` (§5), `IUserClaimsProvider`
   (§4), e a consequência de interseção (§6); corrigir `net9.0→net10.0` e resources `IN_PROGRESS→COMPLETED`.
2. **`.ai/foundation/architecture.md`** — `UserAccounts` singular; `Integration/` deixa de ser pasta interna e vira **projeto**;
   tabela com `.PostgreSql`/`.Sqlite`; seam usando `IUserClaimsProvider`/`Claim`.
3. **Emendar `ADR-014`** — `IUserPropertyProvider → IUserClaimsProvider`; retorno `IReadOnlyList<Claim>`; remover `UserClaimDto`;
   renomear `IUserDirectory.GetPropertyProvider → GetClaimsProvider`.
4. **Criar `ADR-015`** — família de projetos + **projeto de integração como padrão**; schema de propriedades por escopo (§2);
   projeção de campos fixos (§5); `UserAccountsRealmOptions` + **split de `AccountOptions`** (§1); eventos não persistidos;
   `RealmId` estrutural; interseção de claims (§6).
5. **`CLAUDE.md`** — corrigir resources para COMPLETED; passar a citar `UserAccounts`.
6. **`plans-roadmap-01.md` / `backlog`** — `PostgreSql`; registrar outbox/inbox/replicação e o security-lifecycle como planos futuros.

---

## Referências

- Rodadas anteriores: [an-users-plan-01.md](an-users-plan-01.md), [an-users-plan-02.md](an-users-plan-02.md),
  [an-users-plan-03.md](an-users-plan-03.md), [an-users-plan-avail-A.md](an-users-plan-avail-A.md).
- Plano: [plan-users-accounts-module.md](../plans/plan-users-accounts-module.md).
- Base: [ADR-013](../../adrs/ADR-013.md), [ADR-014](../../adrs/ADR-014.md), [architecture.md](../foundation/architecture.md).
- Código de referência do split: `RoyalIdentity/Options/AccountOptions.cs`, `RoyalIdentity/Options/PasswordOptions.cs`,
  `RoyalIdentity.Storage.InMemory/MemoryLocalUserAuthenticator.cs` (lê `realm.Options.Account`).
