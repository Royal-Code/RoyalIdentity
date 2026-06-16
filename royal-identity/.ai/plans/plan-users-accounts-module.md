# Plan: Módulo de Contas de Usuário (`RoyalIdentity.UsersAccounts`)

## Status: PLANEJAMENTO (rascunho) — aguardando respostas das **Questões em aberto** (§ "Questões")

## Progresso

`░░░░░░░░░` **0%** - 0 de 9 fases (planejamento inicial; nenhuma fase iniciada)

| Fase | Estado |
|---|---|
| Fase 1 - ADR do módulo (governança) | Não iniciada |
| Fase 2 - Esqueleto do módulo (projeto + libs + arquitetura) | Não iniciada |
| Fase 3 - Domínio de contas (agregado `UserAccount`) | Não iniciada |
| Fase 4 - Propriedades dinâmicas por escopo | Não iniciada |
| Fase 5 - Persistência própria | Não iniciada |
| Fase 6 - Features / casos de uso administrativos básicos | Não iniciada |
| Fase 7 - Integração com a borda (`Integration/`) | Não iniciada |
| Fase 8 - Troca de DI + paridade de testes | Não iniciada |
| Fase 9 - Diferidos (eventos/outbox/replicação) + regressão final | Não iniciada |

> **Manutenção deste plano (instrução direta):** ao concluir as tarefas de uma fase, marque cada tarefa com
> `- [x]`, troque o **Estado** da fase para `Concluida` na tabela acima, e **atualize a barra de progresso**
> (preencha um bloco `█` por fase concluida, ajuste o `%` e o `X de 9`). Ex.: 3 fases ⇒ `███░░░░░░` **33%** - 3 de 9.
>
> **Antes de executar:** este é o **planejamento inicial**. As **Questões em aberto** (§ próprio) precisam ser
> respondidas e promovidas a "Decisões fechadas" antes de iniciar a Fase 2. As fases abaixo são **provisórias**
> e podem mudar conforme as respostas.

---

## Contexto

Este plano cria o módulo **`RoyalIdentity.UsersAccounts`** — a **camada B (contas de usuário)** das três camadas
definidas em [an-users-arch.md](../analisys/an-users-arch.md) (borda / contas / sessão). É o **item 1** do
[plans-roadmap-01.md](plans-roadmap-01.md) e o sucessor natural do plano de borda já concluído
([plan-users-edge-session.md](plan-users-edge-session.md), **COMPLETED**).

O plano de borda deixou as **facades prontas** (`IUserDirectory` → `ISubjectStore` / `ILocalUserAuthenticator` /
`IUserPropertyProvider`) com uma **implementação in-memory fake/referência** (`MemoryUserDirectory` e cia. em
`RoyalIdentity.Storage.InMemory`). Trocar essa implementação fake pelo **módulo real** é **registro de DI** — a
borda **não** é reescrita (ADR-014 §2.3; critério global #12 do plano de borda).

Este módulo carrega o **modelo rico de contas** que foi deliberadamente mantido **fora** da biblioteca IdP:
o `UserAccount` e submodelos esboçados em [an-users-final.md](../analisys/an-users-final.md) §5, **enriquecidos**
com a visão do autor em [an-users-pontos2.md](../analisys/an-users-pontos2.md) §4 (emails opcionais/múltiplos/
fictícios, ID externo, **propriedades dinâmicas por escopo** ancoradas nos Identity Scopes) e consolidados em
[an-users-arch.md](../analisys/an-users-arch.md) §6/§7.

**Arquitetura:** o módulo segue a **Feature-Slice** definida em
[../foundation/architecture.md](../foundation/architecture.md) (recorte operacional de
[feature-slice-architecture.md](../references/architecture/feature-slice-architecture.md)): módulo =
**`Domain` + `Features` + `Infrastructure` + `Integration`** num único projeto; **API/UI são projetos separados**
(fora deste plano); **persistência própria**; o módulo só referencia o core `RoyalIdentity` para os **contratos de
borda + DTOs primitivos**; lente default **Gritante**; escritas via **SmartCommands**, leituras via **SmartSearch**,
domínio retornando **`Result`/`Problems`**.

---

## Objetivo

1. Criar o projeto `RoyalIdentity.UsersAccounts` na arquitetura Feature-Slice, com domínio rico + persistência própria.
2. Modelar o agregado **`UserAccount`** (dados OIDC mínimos, `SubjectId` imutável, username, status, ID externo, emails).
3. Modelar **propriedades dinâmicas por escopo** (`PropertyScope` ↔ `IdentityScope.Name`) e sua **projeção para claims**.
4. **Implementar as portas de borda** (`ISubjectStore`, `ILocalUserAuthenticator`, `IUserPropertyProvider`,
   `IUserDirectory`) no `Integration/`, com **paridade comportamental** com o fake in-memory atual.
5. Entregar **casos de uso administrativos básicos** como Features (commands/queries), **sem** API/UI (plano próprio).
6. **Trocar o `IUserDirectory` fake pelo do módulo via DI** mantendo a suíte de testes do IdP **verde** (paridade de seeds).
7. **Costurar** (não construir) eventos de domínio / inbox-outbox / replicação e o ciclo de credenciais avançado.

---

## Fora de Escopo (cada um tem plano/fronteira própria)

- **Persistência de dados do IdP + Caching** (`Data.Configuration`/`.Operational`, `Storage.EntityFramework`,
  `.Postgre`/`.Sqlite`, `Storage.Caching`) → [plans-roadmap-01.md](plans-roadmap-01.md) item 2 (`plan-data-persistence`).
  **O módulo tem persistência própria e NÃO é adaptado pelo `Storage.EntityFramework` do IdP** (ADR-013 §2.4).
- **Ciclo de segurança da conta** (expiração de senha, histórico, `SecurityStamp` + invalidação de cookie/sessão,
  lockout administrativo, recuperação/verificação de email/phone) → item 3 (`plan-users-security-lifecycle`). Aqui só
  o **mínimo** para autenticar (verificar senha + lockout temporário, paridade com a borda atual).
- **API e UI administrativas** (projetos separados) → item 5 (`plan-admin-api-ui`). Aqui ficam apenas as **Features**
  (commands/queries) do domínio; **HTTP fica fora do módulo** (architecture.md §6).
- **Federação / login externo** (`ExternalIdentity` vinculada) → item 6. Aqui o modelo `ExternalIdentity` entra
  **reservado** (costura), sem fluxo.
- **MFA / passwordless** → item 7.
- **Sessão** (camada C): já é da borda/operacional do IdP, **não** do módulo de contas.

---

## Decisões já fechadas (herdadas — NÃO reabrir)

Vêm das ADR-013/014, do plano de borda concluído e do `architecture.md`. Servem de trilho:

- **Módulo = domínio + persistência própria** num projeto; **API/UI separados**; **sem `Web/` interno** (ADR-013 §2.4).
- **Borda 100% facade**: o módulo se expõe **implementando** `ISubjectStore`/`ILocalUserAuthenticator`/
  `IUserPropertyProvider` via `IUserDirectory`. O core **nunca** referencia o módulo (DI no host).
- **Seam só com primitivos** (ADR-014 §2.9): `IUserPropertyProvider` recebe **nomes de identity scopes + claim types**
  e devolve `UserClaimDto[]`. O módulo **nunca** vê `IdentityScope`/`RequestedResources`.
- **Realm ligado na construção, nunca em método** (ADR-014 §2.5): os getters de `IUserDirectory` recebem `Realm`;
  as portas retornadas não recebem realm e não leem `HttpContext`.
- **`SubjectId` imutável**, opaco, ≠ `Username`, **não** derivado do username (ADR-014 §2.2).
- **`sub` ≠ username** já vale no IdP; seeds com `SubjectId` **determinístico** (paridade com `MemoryStorage.AliceSubjectId`/`BobSubjectId`).
- **In-memory permanece** como fake/referência (dev/test/integração/demo) — **não** é removido ao módulo nascer (ADR-013 §2.1).
- **Lente default Gritante**; **escritas SmartCommands** (sem `Map*` no módulo), **leituras SmartSearch/`ICriteria`**;
  domínio retorna **`Result`/`Problems`** (sem throw para fluxo esperado).

---

## Contratos da borda que o módulo implementa (alvo do `Integration/`)

Assinaturas **exatas** hoje no core (`RoyalIdentity/Users/Contracts/`) — o módulo deve casar com elas:

| Porta (core) | Assinatura | Comportamento a reproduzir (ref. fake atual) |
|---|---|---|
| `ISubjectStore` | `FindBySubjectIdAsync(subjectId, ct) → Subject?`; `IsActiveAsync(subjectId, ct) → bool` | lookup por `SubjectId`; `Subject(SubjectId, DisplayName, IsActive)`. |
| `ILocalUserAuthenticator` | `AuthenticateLocalAsync(login, password, ct) → AuthenticationResult` | resolve login (username exato → case-insensitive → email se `LoginWithEmail`/`EmailAsUsername`); ordem: `NotFound` → `Inactive` → `Blocked`(lockout) → sem hash⇒`InvalidCredentials` → verifica senha (falha incrementa, sucesso zera). |
| `IUserPropertyProvider` | `GetClaimsAsync(subjectId, identityScopeNames, claimTypes, ct) → IReadOnlyList<UserClaimDto>` | projeta propriedades→`UserClaimDto`; conta inexistente/inativa ⇒ `[]`; **filtro estrito por claim type** (tipo não solicitado ⇒ não emite). |
| `IUserDirectory` | `GetSubjectStore(realm)`; `GetLocalAuthenticator(realm)`; `GetPropertyProvider(realm)` | fábrica realm-bound das três portas. |

Tipos de borda (core, imutáveis — o módulo consome/produz): `Subject(SubjectId, DisplayName, IsActive)`;
`AuthenticationResult` (`Succeeded(subject)` / `Failed(reason)`, `reason ∈ {NotFound, Inactive, InvalidCredentials, Blocked}`);
`UserClaimDto(Type, Value, ValueType?)`.

> **Diferença chave vs. o fake:** o fake (`MemoryUserPropertyProvider`) **ignora** `identityScopeNames` (registro é
> um *bag* plano). O módulo real **particiona propriedades por escopo** e deve honrar `identityScopeNames` **e**
> `claimTypes` (ver Q-G1). Lockout (`MaxFailedAccessAttempts`=3, `AccountLockoutDurationMinutes`=30) lido das
> `AccountOptions.PasswordOptions` do realm (paridade com `LockoutPolicy` atual).

---

## Modelo-alvo do domínio (provisório — refina an-users-final §5 para o módulo)

Diferente do esboço de borda (records POCO), no módulo isto é **domínio rico** (`AggregateRoot`, value objects,
eventos, métodos que retornam `Result`). Esboço **não-normativo na sintaxe**, normativo em responsabilidades:

```
UserAccount : AggregateRoot<…>          (raiz do agregado; coleta eventos com AddEvent)
  SubjectId        (VO imutável; sub; opaco; ≠ username)
  Username         (VO; mutável por policy; normalização p/ busca)
  DisplayName      (name)
  AccountStatus    (Active/Inactive/… — VO/enum; "ativo" da borda lê isto)
  ExternalId?      (identificador legado; lookup secundário — Q-C5)
  Emails           (0..N; principal? verificado? fictício? — Q-C2)
  Credential       (senha: Hash + contadores de lockout; verify + lockout; resto difere ao plano #3)
  Roles            (Q-C6: 1ª classe ou propriedade?)
  Properties       (valores das propriedades dinâmicas por escopo — ver abaixo)
  ExternalIdentities  (reservado; federação — plano #6)

PropertyScope (↔ IdentityScope.Name)         (configuração por realm)
  └─ PropertyDefinition { Name(claimType), ValueType, DisplayName, Help, IsSensitive, Validation[...] }
```

**Projeção para claims (coração do seam, an-users-arch §7):** dado `(subjectId, identityScopeNames, claimTypes)`,
o provider (1) seleciona os `PropertyScope` cujo nome está em `identityScopeNames`; (2) pega as `PropertyDefinition`
cujo `claimType` está em `claimTypes`; (3) lê os valores em `UserAccount.Properties`; (4) aplica sensibilidade/
validação; (5) devolve `UserClaimDto[]`. **Só strings cruzam a fronteira.**

---

## Arquitetura do módulo (layout Gritante — provisório, ver Q-A1)

```text
RoyalIdentity.UsersAccounts/                 ← módulo: domínio + features + persistência + seam IdP
  Features/
    Accounts/
      Domain/        UserAccount, SubjectId, Username, AccountStatus, Email, Credential, ...
      Events/        UserAccountCreated, PasswordChanged, AccountDeactivated, EmailAdded, ...
      Commons/       CreateAccount, UpdateAccount, GetAccountDetails, SearchAccounts (+ DTOs/filtros)
      ChangePassword/        ChangePassword.cs
      SetScopeProperties/    SetScopeProperties.cs
      Activate/ Deactivate/  ...
    ScopeProperties/         PropertyScope, PropertyDefinition (+ admin features)
  Infrastructure/
    Data/          UsersAccountsDbContext, mappings, ConfigureUsersAccounts (WorkContext)
    Searches/      selectors + named order-by
  Integration/     ← implementa as portas de borda do core
    SubjectStore.cs               : ISubjectStore
    LocalUserAuthenticator.cs      : ILocalUserAuthenticator   (resolve login + verifica + lockout)
    UserPropertyProvider.cs        : IUserPropertyProvider     (propriedades→claims, honra scope names)
    UsersAccountsUserDirectory.cs  : IUserDirectory            (fábrica realm-bound)
    UsersAccountsServiceCollectionExtensions.cs                (AddUsersAccounts(...) p/ o host)

RoyalIdentity.UsersAccounts.Api/   ← projeto separado (plano #5; fora deste plano)
RoyalIdentity.UsersAccounts.Web/   ← projeto separado (plano #5; fora deste plano)
```

---

## Questões em aberto (responder antes da Fase 2; promover a "Decisões fechadas")

> Estas são as **dúvidas, incertezas, ambiguidades e oportunidades** levantadas na análise. Cada uma traz
> **impacto**, **opções** e **recomendação**. Marque a decisão escolhida para destravar a execução.

### A. Estrutura, projeto e dependências

- **Q-A1 — Onde fica o projeto fisicamente?** Os projetos hoje são **flat na raiz** da solução
  (`RoyalIdentity/`, `RoyalIdentity.Razor/`, …); o `architecture.md` §3 desenhou `Modules/UsersAccounts/…`.
  *Impacto:* caminhos, `.sln`, futuros `*.Api`/`*.Web`. *Opções:* (a) flat na raiz `RoyalIdentity.UsersAccounts/`
  + solution folder lógico; (b) pasta física `Modules/`. **Recomendação:** (a) — segue a convenção atual do repo;
  agrupar logicamente com solution folder. *(Se (a), atualizar o exemplo do `architecture.md` §3 que mostra `Modules/`.)*

- **Q-A2 — As libs RoyalCode estão disponíveis?** ⚠️ **Maior risco.** `architecture.md` e as referências
  (`external-libraries/*`) assumem **`RoyalCode.SmartCommands`, `.SmartSearch`, `.SmartSelector`,
  `.SmartValidations`, `.SmartProblems`** e libs de domínio (`AggregateRoot<TId>`, `DomainEventBase`). **Nenhum
  projeto do repo usa isso hoje.** *Impacto:* sem feed/pacotes acessíveis, todo o padrão de escrita/leitura/validação
  do módulo fica bloqueado. *Perguntas:* há NuGet feed (versões)? São públicas? *Opções:* (a) referenciar pacotes;
  (b) submódulo/source; (c) **não** adotar e usar EFCore + Minimal API "puro". **Recomendação:** confirmar (a) com
  versões fixadas antes da Fase 2; se indisponível, reavaliar o `architecture.md`.

- **Q-A3 — Target framework.** Docs dizem `net9.0`; há `obj/.../net10.0` e commit recente "Update target framework".
  *Recomendação:* o módulo herda o `Directory.Build.props` (qualquer que seja o alvo único); **confirmar** o valor.

### B. Persistência

- **Q-B1 — Provider da persistência própria.** O módulo tem persistência própria (ADR-013), mas **qual**? *Opções:*
  (a) **EFCore desde já** (Sqlite p/ testes, Postgre prod) — coerente com "own persistence"; (b) começar **in-memory
  no próprio módulo** e adiar EF; (c) reusar a infra do plano #2. **Recomendação:** (a) EFCore com Sqlite nos testes
  do módulo, Postgre como provider de produção, **independente** do `Storage.EntityFramework` do IdP. *(Depende de Q-A2:
  SmartCommands integra com EFCore via WorkContext.)*

- **Q-B2 — Coexistência com o fake durante a migração.** Ao registrar o `IUserDirectory` do módulo, o
  `MemoryUserDirectory` continua para o realm **Demo**/testes do IdP, ou é substituído? *Impacto:* os **206 testes**
  do IdP dependem dos seeds alice/bob. **Recomendação:** manter o in-memory como fake/referência (ADR-013); a troca
  de DI é **opt-in por host/ambiente**; a Fase 8 garante paridade de seeds para a suíte continuar verde **com o módulo**.

### C. Modelo de domínio

- **Q-C1 — Perfil "scope-driven" vs. campos 1ª classe.** Quais claims OIDC (`name`, `given_name`, `email`, …) são
  **propriedades dinâmicas** (sob os escopos `profile`/`email`) e quais são **estruturais** no `UserAccount`?
  *Oportunidade (visão do autor):* perfil **dirigido por escopo**. **Recomendação:** estrutural mínimo
  (`SubjectId`, `Username`, `DisplayName`, `AccountStatus`); **o resto do perfil** como propriedades por escopo,
  com escopos padrão (`profile`, `email`) **semeados** por realm.

- **Q-C2 — Modelo de email.** Opcional, **múltiplo**, **fictício** (auto-gerado, por-realm, opcional),
  `AllowDuplicateEmail`, `VerifyEmail`. *Ambiguidades:* email primário? estado de verificação por email? **regra e
  config do email fictício vivem onde** (RealmOptions.Account? nova options?)? **Recomendação:** `Email { Address,
  IsPrimary, IsVerified, IsFictitious }`, coleção no agregado; geração fictícia como **policy do módulo** lendo
  config do realm; **detalhar na Fase 3 + ADR**.

- **Q-C5 — ID externo (legado).** Único ou múltiplo por conta? Indexado/único por realm? Usado como identificador de
  login? **Recomendação:** 1 `ExternalId?` opcional, índice **não-único** por realm, **não** é credencial de login
  (só correlação) — confirmar.

- **Q-C6 — Roles: 1ª classe ou propriedade?** O fake guarda `Roles` e as emite como claim `role` pelo provider
  (fora do cookie, ADR-014 §2.8). *Opções:* (a) `Roles` 1ª classe no agregado; (b) roles como propriedades de um
  escopo `roles`. **Recomendação:** (a) 1ª classe (autorização é transversal), projetadas como claim `role` via provider.

- **Q-C7 — Tipo da chave do agregado.** `AggregateRoot<string>` com `SubjectId` ou `AggregateRoot<SubjectId>`
  (VO fortemente tipado)? **Recomendação:** VO `SubjectId` fortemente tipado; geração opaca (reusar `CryptoRandom.CreateUniqueId()`, S4).

### D. Credenciais (fronteira com o plano #3)

- **Q-D1 — Quanto do ciclo de senha entra aqui?** `PasswordOptions` tem complexidade, expiração e histórico.
  **Recomendação:** **só** o necessário para `ILocalUserAuthenticator` (verificar hash + lockout) e um
  `SetPassword`/`ChangePassword` **com validação de complexidade**; **expiração, histórico, `SecurityStamp` e
  invalidação de sessão difere ao plano #3**. Confirmar o corte.

- **Q-D2 — Onde mora o lockout.** ADR diz "no módulo". Contadores no agregado `UserAccount.Credential`; política lê
  `realm.Options.Account.PasswordOptions`. **Recomendação:** confirmar — `LockoutPolicy` vira regra de domínio do módulo.

### E. Casos de uso administrativos (fronteira com o plano #5)

- **Q-E1 — Quais casos entram?** Roadmap pede "casos administrativos básicos"; a API/UI é o plano #5. **Recomendação
  (Features sem HTTP):** `CreateAccount`, `UpdateAccount` (perfil/username conforme policy), `GetAccountDetails`,
  `SearchAccounts`, `ChangePassword`, `Activate`/`Deactivate`, `SetScopeProperties`, `AddEmail`/`RemoveEmail`/
  `SetPrimaryEmail`. Registro de conta self-service (`AllowRegistration`) **fica para a UI/plano #5**. Confirmar a lista.

### F. Eventos, Inbox/Outbox, replicação

- **Q-F1 — O que entra agora?** Roadmap diz "fases finais ou diferidas". **Recomendação:** **eventos de domínio**
  entram desde a Fase 3 (idiomático no agregado); **Inbox/Outbox e replicação ficam diferidos** (Fase 9 como costura
  mínima, ou plano próprio). Confirmar.

### G. Integração / seam

- **Q-G1 — Semântica exata da projeção.** Uma propriedade é emitida quando **(o escopo dela ∈ `identityScopeNames`)
  E (o `claimType` dela ∈ `claimTypes`)**? (O fake só aplica o 2º filtro.) **Recomendação:** **ambos** os filtros —
  é o ganho estrutural do modelo por escopo. Garantir que o resultado efetivo para os seeds atuais **não regrida**
  (Fase 8).

- **Q-G2 — Consistência `PropertyScope` ↔ `IdentityScope`.** `IdentityScope` está **em redesenho**
  ([plan-resources-redesign.md](plan-resources-redesign.md), **IN_PROGRESS**). O acoplamento é só por **`Name`** (string,
  via seam), então o módulo **não** depende do tipo. *Questão:* validar que um `PropertyScope` referencia um
  `IdentityScope.Name` existente é responsabilidade de **quem**? **Recomendação:** o módulo **não** valida contra o
  catálogo do IdP (desacoplamento); coerência é **configuração administrativa**; alinhar `Name`s ao resources redesign
  quando estabilizar. Confirmar.

---

## Ordem de execução e dependências

1. **Fase 1 (ADR)** — pré-requisito de tudo; **fecha as Questões** acima como decisões.
2. **Fase 2 (esqueleto)** — depende de Q-A1/A2/A3, B1.
3. **Fase 3 (domínio)** e **Fase 4 (propriedades por escopo)** — dependem da 2; 4 depende de 3.
4. **Fase 5 (persistência)** — depende de 3/4.
5. **Fase 6 (features)** — depende de 3/4/5.
6. **Fase 7 (integração/borda)** — depende de 3/4/5 (e 6 para reuso de queries).
7. **Fase 8 (DI + paridade)** — depende de 7; **mantém os 206 testes do IdP verdes**.
8. **Fase 9 (diferidos + regressão)** — última.

Build/test: `dotnet build RoyalIdentity.sln`; `$env:Logging__EventLog__LogLevel__Default = "None"; dotnet test RoyalIdentity.sln --no-build --nologo`.

---

## Fase 1 - ADR do módulo (governança)

**O que/como:** registrar as decisões do módulo antes de codar (a "(Futura) ADR do módulo `UsersAccounts`" de
an-users-arch §9.3). Provável **ADR-015**. Fecha as **Questões em aberto** como decisões.

**Tarefas:**
- [ ] Escrever `adrs/ADR-015.md` — domínio rico de contas, propriedades por escopo, mapeamento do seam, persistência, diferidos.
- [ ] Resolver e registrar Q-A1..Q-G2 (cada uma vira decisão com justificativa).
- [ ] Referenciar ADR-015 em `CLAUDE.md`/`AGENTS.md`; ligar a ADR-013/014.

**Critérios de aceite:** ADR-015 aceita, coerente com ADR-013/014 e `architecture.md`; Questões fechadas.

**Testes:** n/a (governança).

### Resultado da Fase 1
*a preencher*

---

## Fase 2 - Esqueleto do módulo (projeto + libs + arquitetura)

**O que/como:** criar `RoyalIdentity.UsersAccounts` (layout Gritante), referenciar as libs RoyalCode (Q-A2),
referenciar o core só para os contratos de borda, e fixar a **direção de dependência** com **testes de arquitetura**.

**Tarefas:**
- [ ] Criar o projeto + adicioná-lo à `RoyalIdentity.sln` (local conforme Q-A1).
- [ ] Referências: `RoyalIdentity` (contratos de borda), libs RoyalCode; **proibir** ASP.NET em `Domain`/`Features`.
- [ ] Esqueleto de pastas `Features/`, `Infrastructure/`, `Integration/`.
- [ ] Testes de arquitetura: `Domain` não depende de nada; `Features`→`Domain`+`IWorkContext`; nada de HTTP no módulo; core não referencia o módulo.

**Critérios de aceite:** solução compila; testes de arquitetura passam; libs resolvidas.

**Testes:** arquitetura (dependências) + build.

### Resultado da Fase 2
*a preencher*

---

## Fase 3 - Domínio de contas (agregado `UserAccount`)

**O que/como:** modelar o agregado `UserAccount` + value objects + eventos, métodos retornando `Result`/`Problems`
(sem throw). Inclui dados OIDC mínimos, `SubjectId` imutável, username, status, ID externo, emails e credencial de senha
(mínima: hash + contadores de lockout).

**Tarefas:**
- [ ] `UserAccount : AggregateRoot<…>` (Q-C7); VOs `SubjectId`/`Username`/`AccountStatus`/`Email`/`Credential`.
- [ ] Regras: `SubjectId` imutável (rejeita troca); trocar username não muda `sub`; status controla "ativo".
- [ ] Emails (Q-C2): coleção, primário/verificado/fictício; geração fictícia como policy de realm.
- [ ] `Credential`: set/verify (delegando hash ao `IPasswordProtector` ou equivalente do módulo) + contadores de lockout (Q-D1/D2).
- [ ] Eventos de domínio (`UserAccountCreated`, `PasswordChanged`, `AccountDeactivated`, `EmailAdded`, …) via `AddEvent`.
- [ ] Validação com `Rules.Set<T>()`/`IValidable`.

**Critérios de aceite:** agregado coeso, flat na pasta; invariantes (imutabilidade `SubjectId`, status, lockout) cobertas; sem throw para fluxo.

**Testes:** unidade de domínio — imutabilidade `SubjectId`; lockout incrementa/zera/expira; senha ausente; complexidade na troca.

### Resultado da Fase 3
*a preencher*

---

## Fase 4 - Propriedades dinâmicas por escopo

**O que/como:** modelar `PropertyScope` (↔ `IdentityScope.Name`) com N `PropertyDefinition` (claimType, valueType,
displayName, help, isSensitive, validação) e a **projeção** propriedades→`UserClaimDto` honrando `identityScopeNames`
+ `claimTypes` (Q-G1).

**Tarefas:**
- [ ] Modelar `PropertyScope` + `PropertyDefinition` (configuração por realm); valores em `UserAccount.Properties`.
- [ ] Lógica de projeção (filtro por escopo **e** claim type; sensibilidade/validação).
- [ ] Escopos padrão (`profile`, `email`, `roles`?) semeados por realm (Q-C1/C6), preservando o efetivo atual.
- [ ] **Sem** dependência de `IdentityScope` (só `Name`/strings — Q-G2).

**Critérios de aceite:** projeção determinística por escopo+tipo; só strings na fronteira; paridade com os seeds atuais.

**Testes:** unidade — projeção por escopo/tipo; propriedade sensível; escopo não solicitado ⇒ não emite.

### Resultado da Fase 4
*a preencher*

---

## Fase 5 - Persistência própria

**O que/como:** `UsersAccountsDbContext` + mapeamentos + `ConfigureUsersAccounts(IWorkContextBuilder)` +
`Searches/` (selectors/order-by). Provider conforme Q-B1.

**Tarefas:**
- [ ] `DbContext` + mappings (agregado, emails, propriedades, credencial, escopos).
- [ ] `ConfigureUsersAccounts` registrando modelo/repos/searches/commands por assembly (WorkContext).
- [ ] `Searches/` para projeções que desembrulham VOs (ex.: `SubjectId.Value`).
- [ ] Migrations/provider (Sqlite testes, Postgre prod) **independentes** do `Storage.EntityFramework` do IdP.

**Critérios de aceite:** round-trip persistente do agregado; realm-scoped; índices por `SubjectId`/username/email.

**Testes:** integração de persistência (Sqlite em memória) — save/find por subject/username/email.

### Resultado da Fase 5
*a preencher*

---

## Fase 6 - Features / casos de uso administrativos básicos

**O que/como:** Features `Commons/` (CRUD-like) + business-intent via **SmartCommands** (escritas) e **SmartSearch**
(leituras). **HTTP fora do módulo** — sem `Map*` (architecture.md §6). Lista conforme Q-E1.

**Tarefas:**
- [ ] `Commons/`: `CreateAccount`, `UpdateAccount`, `GetAccountDetails`, `SearchAccounts` (+ DTOs/filtros).
- [ ] Business-intent: `ChangePassword`, `Activate`/`Deactivate`, `SetScopeProperties`, `AddEmail`/`SetPrimaryEmail`.
- [ ] Validação (`HasProblems`/`RuleSet`); `WithWorkContext`; retorno `Result`/`Problems`.
- [ ] Leituras com `ICriteria<T>` + `Select<TDto>()` (SmartSelector).

**Critérios de aceite:** commands/queries cobrem os casos básicos; sem ASP.NET no módulo; categorias→status corretas.

**Testes:** unidade/integração das features (happy path + validação + NotFound/conflito).

### Resultado da Fase 6
*a preencher*

---

## Fase 7 - Integração com a borda (`Integration/`)

**O que/como:** implementar as 4 portas casando com as assinaturas do core, com **paridade comportamental** com o
fake (tabela "Contratos da borda"). Realm ligado na construção; primitivos no seam.

**Tarefas:**
- [ ] `SubjectStore : ISubjectStore` (lookup por `SubjectId` → `Subject`).
- [ ] `LocalUserAuthenticator : ILocalUserAuthenticator` (resolve login username/email; ordem NotFound→Inactive→Blocked→InvalidCredentials; lockout).
- [ ] `UserPropertyProvider : IUserPropertyProvider` (projeção por escopo+tipo; inativo ⇒ `[]`).
- [ ] `UsersAccountsUserDirectory : IUserDirectory` (fábrica realm-bound) + `AddUsersAccounts(...)` p/ o host.

**Critérios de aceite:** portas equivalentes ao fake; sem realm em método; sem `HttpContext`; só primitivos no seam.

**Testes:** unidade das portas (paridade com `LocalUserAuthenticatorTests`/`MemoryUserPropertyProvider`).

### Resultado da Fase 7
*a preencher*

---

## Fase 8 - Troca de DI + paridade de testes

**O que/como:** registrar o `IUserDirectory` do módulo no host (substituindo o fake, conforme Q-B2) e provar que a
suíte do IdP segue **verde** — seeds alice/bob com `SubjectId` **determinístico** (paridade com `MemoryStorage`).

**Tarefas:**
- [ ] Wire `AddUsersAccounts(...)` no `RoyalIdentity.Server` (e/ou `AppFactory` de teste).
- [ ] Seeds de paridade (alice/bob, `SubjectId` determinístico, escopos `profile`/`email` semeados).
- [ ] Rodar a suíte completa do IdP **contra o módulo**; ajustar só o que for paridade legítima.

**Critérios de aceite:** suíte do IdP verde com o módulo; in-memory permanece disponível como fake/referência.

**Testes:** suíte completa (Integration/Identity/Pipelines) + os do módulo.

### Resultado da Fase 8
*a preencher*

---

## Fase 9 - Diferidos (eventos/outbox/replicação) + regressão final

**O que/como:** costura mínima dos diferidos (Q-F1) e regressão final. Inbox/Outbox/replicação ficam como **costura**
ou plano próprio; eventos de domínio já existem desde a Fase 3.

**Tarefas:**
- [ ] Decidir costura de Outbox/Inbox (mínima) ou registrar como plano próprio.
- [ ] Documentar o que ficou diferido (lifecycle→#3, API/UI→#5, federação→#6).
- [ ] Build + suíte completas; validar critérios globais.

**Critérios de aceite:** diferidos documentados; suíte verde; critérios globais satisfeitos.

**Testes:** regressão completa.

### Resultado da Fase 9
*a preencher*

---

## Invariantes a preservar (não regridir — herdadas da borda)

1. Usuário/conta **realm-scoped**; nada cruza realm.
2. `sub` = `SubjectId` estável, imutável, ≠ username/email.
3. Erro de login **genérico** (anti-enumeração); motivo interno para evento/auditoria.
4. Falha de senha incrementa; sucesso zera; lockout por `MaxFailedAccessAttempts`/`AccountLockoutDurationMinutes`.
5. Senha ausente/credencial desabilitada ⇒ sem login por senha.
6. Conta inativa ⇒ sem autenticação e **sem claims de perfil**.
7. Claims projetadas **filtradas pelos identity scopes solicitados** (seam só com primitivos).
8. Resolução de login (username/email/fictício) é do **autenticador** (módulo), não do store.

## Critérios de aceite globais

1. `RoyalIdentity.UsersAccounts` existe na arquitetura Feature-Slice (domínio+features+infra+integração; **sem** API/UI/Web).
2. `UserAccount` rico com `SubjectId` imutável, status, emails, ID externo, credencial mínima e propriedades por escopo.
3. Propriedades dinâmicas por escopo (↔ `IdentityScope.Name`) projetadas para claims honrando escopo **e** tipo.
4. As 4 portas de borda implementadas no `Integration/`, **paridade** com o fake, realm na construção, só primitivos.
5. Persistência própria do módulo (não adaptada pelo `Storage.EntityFramework` do IdP).
6. Features de casos administrativos básicos como SmartCommands/SmartSearch; **HTTP fora do módulo**.
7. Troca de DI (fake→módulo) mantém a suíte do IdP **verde**; in-memory permanece como fake/referência.
8. Core **não** referencia o módulo; `Domain`/`Features` **sem** ASP.NET (testes de arquitetura).
9. Diferidos (lifecycle, API/UI, federação, MFA, outbox/replicação) **costurados, não construídos**.

---

## Riscos

- **Disponibilidade das libs RoyalCode (Q-A2)** — bloqueia o padrão de escrita/leitura/validação. Resolver na Fase 1.
- **Paridade de seeds (Fase 8)** — regressão silenciosa nos 206 testes do IdP se `SubjectId`/escopos divergirem.
- **Acoplamento a `IdentityScope` em redesenho (Q-G2)** — mitigado por usar só `Name` (string) no seam.
- **Escopo inflar para o plano #3/#5** — manter os cortes de credencial-lifecycle e API/UI.

---

## Referências

- Análises: [an-users-arch.md](../analisys/an-users-arch.md) (base; §2 três camadas, §5 seam, §6 requisitos, §7 propriedades por escopo),
  [an-users-final.md](../analisys/an-users-final.md) (§5 modelo rico — insumo), [an-users-pontos2.md](../analisys/an-users-pontos2.md) (visão do autor §4).
- ADRs: [ADR-013](../../adrs/ADR-013.md) (arquitetura modular & fronteiras), [ADR-014](../../adrs/ADR-014.md) (borda + sessão + seam).
- Arquitetura: [../foundation/architecture.md](../foundation/architecture.md) (recorte operacional),
  [feature-slice-architecture.md](../references/architecture/feature-slice-architecture.md) (referência completa),
  [external-libraries/*](../references/external-libraries/) (SmartCommands/Search/Selector/Validations/Problems/WorkContext/domain).
- Planos: [plans-roadmap-01.md](plans-roadmap-01.md) (roadmap; item 1), [plan-users-edge-session.md](plan-users-edge-session.md) (borda — COMPLETED),
  [plan-resources-redesign.md](plan-resources-redesign.md) (IdentityScope — IN_PROGRESS; Q-G2).
- Código (borda/fake a casar): `RoyalIdentity/Users/Contracts/` (portas), `RoyalIdentity.Storage.InMemory/Memory*`
  (referência comportamental), `RoyalIdentity/Options/AccountOptions.cs` + `PasswordOptions.cs` (policy/lockout).
