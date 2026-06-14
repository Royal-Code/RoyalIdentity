# Plan: Users — Redesign da Borda + Sessão (camadas A + C)

## Status: IN_PROGRESS

## Progresso

`██░░░░░░░` **22%** - 2 de 9 fases concluidas

| Fase | Estado |
|---|---|
| Fase 1 - ADRs (governanca) | Concluida |
| Fase 2 - Testes de caracterizacao | Concluida |
| Fase 3 - Contratos e tipos de borda | Pendente |
| Fase 4 - Conta in-memory + SubjectId + autenticador | Pendente |
| Fase 5 - Sessao (modelo + store puro + service) | Pendente |
| Fase 6 - "Ativo" unificado | Pendente |
| Fase 7 - Principal + LoginFlowService + telas finas | Pendente |
| Fase 8 - Claims por propriedade (seam) | Pendente |
| Fase 9 - Limpeza, remocao e regressao final | Pendente |

> **Manutencao deste plano (instrucao direta):** ao concluir as tarefas de uma fase, marque cada tarefa com
> `- [x]`, troque o **Estado** da fase para `Concluida` na tabela acima, e **atualize a barra de progresso**
> (preencha um bloco `█` por fase concluida, ajuste o `%` e o `X de 9`). Ex.: 3 fases ⇒ `███░░░░░░` **33%** - 3 de 9.

---

## Contexto

Este plano implementa o **trabalho imediato** do redesign de Users descrito em
[an-users-arch.md](../analisys/an-users-arch.md) (documento-base, que reescopa
[an-users-final.md](../analisys/an-users-final.md) e consolida m1/m2/avaliacoes/con2). Nasce do item
**Users** do [redesign-todo.md](../../redesign-todo.md).

O subsistema atual mistura responsabilidades: `IdentityUser` rico carregando servicos; `UserDetails` POCO
paralelo; `IUserStore`/`IUserDetailsStore` sendo a mesma classe; `IdentitySession` guardando o usuario vivo;
`UserSessionStore` lendo `HttpContext`; `sub` = `Username`; lockout em 3 lugares; sessao criada como efeito
colateral da verificacao de senha; "ativo" inconsistente em 3 caminhos; `LoginPageService` carregando a
maquina de estados do login; e o `IdentityRevalidatingAuthenticationStateProvider` quebrado.

**Escopo deste plano = camadas A (borda) + C (sessao)** — a refatoracao **dentro do IdP** atras de *facades*,
mantendo o `Storage.InMemory` como implementacao fake/referencia (dev/test/integracao/demo). O **modelo rico
de contas** (camada B) e os **modulos de persistencia** (`Data.*`, `Storage.EntityFramework`, `Caching`,
`UsersAccounts`, `KMS`) sao **trabalho posterior**, com ADRs e planos proprios (ver "Planos futuros").

---

## Objetivo

1. Substituir o objeto rico `IdentityUser` por **contratos de borda** (facades) + **modelo enxuto**.
2. `SubjectId` **imutavel**, separado de `Username`.
3. Sessao **serializavel** (sem objeto rico), store **puro** (sem `HttpContext`), "sessao atual" num **service**.
4. **Unificar "ativo"** (conta vs sessao); sessao ausente ⇒ invalida quando ha `sid`.
5. **Sessao deixa de ser efeito colateral** da verificacao de senha.
6. **Telas como cola**: `LoginFlowService` orquestra; `LoginPageService` vira adaptador.
7. **Desenhar as facades** (`ISubjectStore`, `ILocalUserAuthenticator`, `IUserPropertyProvider`,
   `IUserSessionStore`, `IUserSessionService`, `ISubjectPrincipalFactory`) de modo que os modulos futuros
   encaixem **sem reescrever a borda**.
8. **Preservar** todas as invariantes de negocio (ver secao "Invariantes").

---

## Fora de Escopo (vira plano/ADR proprio — ver "Planos futuros")

- Modulo `RoyalIdentity.UsersAccounts` (dominio rico: emails opcional/multiplo/ficticio, ID externo,
  propriedades dinamicas por escopo, eventos, inbox/outbox, replicacao, casos de uso administrativos).
- Projetos de dados `RoyalIdentity.Data.Configuration`/`.Operational` e `Storage.EntityFramework`
  (+ `.Postgre`/`.Sqlite`), `Storage.Caching`. **Persistencia continua in-memory** neste plano.
- Modulo `RoyalIdentity.KMS`; projetos de API e de UI dos modulos.
- Ligar a **regra** do `SecurityStamp` (invalidacao de cookie/sessao) — fica reservado (so campo, se util).
- Login externo / MFA / passwordless — apenas **costura** (contratos extensiveis), sem implementacao.

---

## Decisoes fechadas (recap do arch §11 — para este plano ser autossuficiente)

- Nomes de projetos de dados: `RoyalIdentity.Data.Configuration`/`.Operational` (futuro).
- Borda **100% facade**: nao guarda entidade rica; usa `Subject` + contratos.
- Edge contract de lookup: **`ISubjectStore`** (evita colisao com `IUserStore` atual).
- Autenticacao local: **`ILocalUserAuthenticator`** (resolve identificador + verifica credencial + lockout);
  o **IdP orquestra** (sessao, principal, cookie, prompt/consent). Fronteira no arch §5.1.
- Claims por **`IUserPropertyProvider`** com **primitivos** (nomes de identity scopes + claim types).
- Sessao: contrato/service no IdP; persistencia (futura) em `Data.Operational`; in-memory agora.
- "Ativo": separar **conta ativa** (`ISubjectStore.IsActiveAsync`) de **sessao valida**
  (`IUserSessionService.IsSessionValidAsync(principal)`); ausente ⇒ invalida.
- **Realm nunca e parametro de metodo** (decisao do autor): portas recebem realm na construcao (fabrica);
  servicos resolvem realm do ambiente por um accessor (`ICurrentRealmAccessor`, fechado na ADR-014) ou de
  um argumento que ja carrega realm (`client.Realm`).
- Contratos no **core**; `Storage.InMemory` implementa (igual hoje).
- ADRs: **ADR-013** (arquitetura modular & fronteiras) + **ADR-014** (borda + sessao). Refinam a ADR-005.

---

## Decisoes (rodada 2 — fechadas)

- **Q1 = Gateway dedicado.** As portas de conta (`ISubjectStore`/`ILocalUserAuthenticator`/`IUserPropertyProvider`)
  vem de um **gateway dedicado de contas** no core — **`IUserDirectory`** (fechado na ADR-014) — com getters
  realm-bound (`GetSubjectStore(realm)`, `GetLocalAuthenticator(realm)`, `GetPropertyProvider(realm)`),
  implementado in-memory agora e pelo modulo `UsersAccounts` depois. O `IStorage` fica **so com dados do IdP**
  (incl. sessao). Troca in-memory→modulo = registro de DI.
- **Q2 = Resolver dedicado.** `GetAuthorizationContextAsync` vai para um **`IAuthorizationContextResolver`**
  (fechado na ADR-014) no core, usado por login/consent/logout; `SessionContextService` (Razor) delega. O
  `ISignInManager` nao mantem esse metodo.

---

## Contratos-alvo (no `RoyalIdentity`)

| Contrato | Responsabilidade | Impl. neste plano |
|---|---|---|
| `ISubjectStore` | `FindBySubjectIdAsync(sub) → Subject?`; `IsActiveAsync(sub)`. Realm ligado na **construcao** (fabrica). | in-memory |
| `ILocalUserAuthenticator` | `AuthenticateLocalAsync(login, password) → AuthenticationResult` (resolve login + verifica + lockout). Realm na construcao. | in-memory |
| `IUserPropertyProvider` | `GetClaimsAsync(sub, identityScopeNames, claimTypes) → UserClaimDto[]`. Realm na construcao. | in-memory |
| `IUserDirectory` (gateway) | getters realm-bound: `GetSubjectStore(realm)`/`GetLocalAuthenticator(realm)`/`GetPropertyProvider(realm)`. Impl. in-memory agora; modulo `UsersAccounts` depois. | in-memory |
| `IProfileService` (mantem) | orquestra claims; consome `IUserPropertyProvider`; `IsActiveAsync` combinada. Realm via `client.Realm` (arg existente). | core |
| `IUserSessionStore` (puro) | `Create/FindById/RecordClient/End` (sem `HttpContext`). Realm-bound via `IStorage.GetUserSessionStore(realm)`. | in-memory |
| `ICurrentRealmAccessor` | fornece o realm corrente para servicos de orquestracao sem espalhar `HttpContext.GetCurrentRealm()`. Impl. HTTP le o realm do `HttpContext`; testes podem usar fake. | core |
| `IUserSessionService` | current, `IsSessionValidAsync(principal)`, start/end, record client. Realm resolvido por `ICurrentRealmAccessor`, nunca em metodo. | core |
| `ISubjectPrincipalFactory` | `Create(subject, session) → ClaimsPrincipal` minimo (sub/name/auth_time/sid/idp/amr). Sem realm (le de subject/session). **Nao emite roles/claims de perfil.** | core |
| `LoginFlowService` | orquestra login → **flow-result (enum)**. Realm do ambiente. | core (`RoyalIdentity`); Razor apenas adapta UI |
| `IAuthorizationContextResolver` | `ResolveAsync(returnUrl) → AuthorizationContext?`; usado por login/consent/logout; `SessionContextService` delega. | core |

Tipos de borda (core): `Subject(SubjectId, DisplayName, IsActive)`,
`AuthenticationResult(Success | Reason{NotFound|Inactive|InvalidCredentials|Blocked} + Subject)`,
`UserClaimDto(Type, Value, ValueType?)`, `UserSession(...)`, `UserSessionClient(ClientId, FirstSeenAt, LastSeenAt)`.

**Regra de resolucao de realm (decidida):** `Realm` **nunca** e parametro de metodo. Duas formas:
- **Portas realm-bound** (`ISubjectStore`, `ILocalUserAuthenticator`, `IUserPropertyProvider`, `IUserSessionStore`)
  recebem o realm **na construcao** (a fabrica liga o realm; metodos sem realm; **nao leem `HttpContext`**) — o
  padrao `GetXStore(realm)` ja existente.
- **Servicos de orquestracao** (`IUserSessionService`, `LoginFlowService`, `DefaultSignOutManager`) resolvem o
  realm corrente do **ambiente** por um accessor (`ICurrentRealmAccessor`) ou de um arg que ja
  carrega realm (`client.Realm`). O accessor isola o core do `HttpContext`; a implementacao HTTP pode usar
  `HttpContext.GetCurrentRealm()`. `ISubjectPrincipalFactory` nao precisa de realm.

> **Vantagem:** a fabrica aceita realm explicito, entao funciona tambem fora de request (jobs futuros: GC de
> sessao). **Q1 (fechada):** as portas de conta vem de um **gateway dedicado** (`IUserDirectory`)
> com getters realm-bound; o store de sessao ja vem de `IStorage.GetUserSessionStore(realm)`.

---

## De → Para (resumo)

| Atual | Destino |
|---|---|
| `IdentityUser`/`DefaultIdentityUser` | removido → `ILocalUserAuthenticator` + `ISubjectStore` + `ISubjectPrincipalFactory` |
| `UserDetails` (POCO) | registro de conta **in-memory** (fake/referencia) atras das facades |
| `IUserStore` + `IUserDetailsStore` | `ISubjectStore` + `ILocalUserAuthenticator` + `IUserPropertyProvider` |
| `IdentitySession` (guarda usuario) | `UserSession` (guarda `SubjectId`), serializavel |
| `UserSessionStore.GetCurrentSessionAsync` (le HttpContext) | `IUserSessionService.GetCurrentAsync(principal)` (realm do ambiente) |
| `AddClientIdAsync` | `IUserSessionStore.RecordClientAsync` (dedup) |
| `IStorage.GetUserStore`/`GetUserDetailsStore` | removidos → portas via gateway `IUserDirectory` (adapters temporarios ate a remocao) |
| `IStorage.GetUserSessionStore` | **mantido**, re-tipado para o `IUserSessionStore` puro |
| `CreatePrincipalAsync` (no usuario) | `ISubjectPrincipalFactory` |
| `CredentialsValidationResult` + `ValidateCredentialsResult` | `AuthenticationResult` (unico) |
| lockout em 3 lugares | `LockoutPolicy` unica (no autenticador) |
| `IdentityRevalidatingAuthenticationStateProvider` | removido |
| `LoginPageService` (maquina de estados) | `LoginFlowService` orquestra; page service vira adaptador |

---

## Ordem de Execucao e Dependencias

1. **Fase 1 (ADRs)** — pre-requisito de todas (governanca).
2. **Fase 2 (caracterizacao)** — rede de seguranca antes de mudar comportamento.
3. **Fase 3 (contratos/tipos)** — base das fases 4-8.
4. **Fase 4 (conta+auth)** e **Fase 5 (sessao)** — dependem da 3; podem andar em paralelo.
5. **Fase 6 ("ativo")** — depende de 4 e 5.
6. **Fase 7 (principal+fluxo)** — depende de 4 e 5.
7. **Fase 8 (claims/propriedade)** — depende de 4.
8. **Fase 9 (limpeza)** — ultima; depende de todas.

> **Q1/Q2 fechadas** ("Decisoes (rodada 2)"): portas de conta via **gateway dedicado** (`IUserDirectory`) e
> contexto de interacao via **`IAuthorizationContextResolver`** dedicado.

Build/test: `dotnet build RoyalIdentity.sln`; `$env:Logging__EventLog__LogLevel__Default = "None"; dotnet test RoyalIdentity.sln --no-build --nologo`.

---

## Fase 1 - ADRs (governanca)

**O que/como:** registrar as decisoes antes de codar. ADR-013 documenta a arquitetura modular e as fronteiras
(facades; `Data.*` sem depender do core; modulos `UsersAccounts`/`KMS`; in-memory fake/referencia). ADR-014
documenta o redesign da borda+sessao (composicao sobre heranca refinando ADR-005, `SubjectId` imutavel,
contratos de borda, semantica de sessao/"ativo", fluxo de login). Seguir o formato de [adrs/](../../adrs/).

**Tarefas:**
- [x] Escrever `adrs/ADR-013.md` — Arquitetura modular & fronteiras (contexto, decisao, consequencias).
- [x] Escrever `adrs/ADR-014.md` — Redesign da borda Users + sessao (refina ADR-005; `SubjectId` imutavel; contratos §5; "sessao valida"; dedup de clients; fluxo de login).
- [x] Fechar nomes finais de `IUserDirectory`, `IAuthorizationContextResolver` e `ICurrentRealmAccessor` (ou equivalentes) na ADR-014.
- [x] Registrar na ADR-014 a decisao posterior: portas realm-bound via fabrica/gateway; servicos de orquestracao sem realm em metodo, usando accessor de realm para **resolucao de realm** (o accessor cobre so isso; I/O de cookie — sign-in/sign-out — segue via `HttpContext`).
- [x] Registrar na ADR-014 que `ISubjectPrincipalFactory` emite apenas principal minimo de sessao; roles/claims de perfil saem pelo `IProfileService`/`IUserPropertyProvider`. **Trade-off:** roles/claims deixam o cookie (sem impacto em token/userinfo, que usam o `IProfileService`) — futura UI admin com `[Authorize(Roles)]` precisara de mecanismo proprio (claims transformation / principal do realm admin).
- [x] Referenciar ADR-013/014 no `CLAUDE.md` e `AGENTS.md` (lista de ADRs) e marcar ADR-005 como **refinada**.

**Criterios de aceite:** ADR-013 e ADR-014 existem, aceitas, coerentes com o arch; nomes finais registrados; `CLAUDE.md` e `AGENTS.md` atualizados.

**Testes:** n/a (governanca documental).

### Resultado da Fase 1

Concluida. Entregaveis:
- `adrs/ADR-013.md` — Arquitetura modular & fronteiras (storages como facades; `Data.Configuration`/`.Operational` puros sem depender do core; `Storage.EntityFramework` (+`.Postgre`/`.Sqlite`) e `Storage.Caching`; modulos `UsersAccounts`/`KMS` = dominio+persistencia, API/UI em projetos separados; in-memory fake/referencia; `Abstractions` adiado).
- `adrs/ADR-014.md` — Redesign da borda Users + sessao (refina ADR-005): composicao sobre heranca, `SubjectId` imutavel ≠ username, tabela de contratos §2.3, regra "Realm nunca e parametro de metodo" (§2.5, accessor so resolve realm; cookie I/O via `HttpContext`), sessao serializavel + store puro + "ativo" unificado (§2.6/2.7), principal minimo + trade-off de roles fora do cookie (§2.8), seam de claims so com primitivos (§2.9), login como cola (§2.10), remocoes (§2.11).
- **Nomes fechados** na ADR-014 §2.4: `IUserDirectory` (gateway de contas), `IAuthorizationContextResolver` (recebe `GetAuthorizationContextAsync`), `ICurrentRealmAccessor` (wrap de `HttpContext.GetCurrentRealm()`).
- ADR-005 marcada como **refinada** (banner apontando para ADR-013/014); lista de ADRs atualizada em `CLAUDE.md` (ADR-001..014) e referencia adicionada em `AGENTS.md` (Start Here + area sob redesign).

---

## Fase 2 - Testes de caracterizacao

**O que/como:** garantir cobertura do comportamento **atual** de login/sessao/logout/"ativo" no nivel **HTTP**
(em `Tests.Integration`), resiliente ao refactor interno. Sao a rede de seguranca: devem continuar verdes ao
fim de cada fase seguinte. Onde ja houver cobertura, complementar; nao alterar comportamento aqui.

**Tarefas:**
- [x] Cobrir: login valido cria sessao ativa realm-scoped; login invalido **nao** cria sessao e incrementa falha; sucesso zera falhas.
- [x] Cobrir: usuario inativo/bloqueado nao autentica (mensagem generica).
- [x] Cobrir: cookie com sessao encerrada e rejeitado; logout encerra sessao e notifica clients.
- [x] Cobrir: emissao de code registra o client na sessao; back/front-channel logout usam os clients da sessao.
- [x] Cobrir: isolamento de realm (sessao de A nao vale em B); `prompt=login`/`max_age`/`UserSsoLifetime` forcam interacao.
- [x] Mapear quais testes assertam `sub` == username (serao atualizados na Fase 4, quando `SubjectId` ≠ username).
- [x] Rodar a suite completa e registrar baseline verde.

**Criterios de aceite:** suite verde; os comportamentos acima estao cobertos por testes HTTP.

**Testes:** os proprios (esta fase **e** testes).

### Resultado da Fase 2

Concluida. **Baseline verde:** `dotnet test RoyalIdentity.sln` ⇒ **178 testes, 0 falhas** (Pipelines 3, Identity 6,
Integration 169 — dos quais **12 novos** de caracterizacao). `Tests.Endpoints` nao esta na solution; `Tests.WebApp`
nao tem `[Fact]`.

**Novos artefatos (em `Tests.Integration`):**
- `Prepare/CharacterizationSeed.cs` — helpers: `SeedUser` (usuario unico por chamada — **nao** mexe em alice/bob,
  que sao estado mutavel compartilhado no singleton de storage), `GetDetails`, `FindSession` (por realm), `PostLoginAsync`.
- `Prepare/BackChannelCapturingAppFactory.cs` — `AppFactory` que substitui `IBackChannelLogoutNotifier` por um fake
  capturador (ultima registro de DI vence), para assertar quem foi notificado no logout.
- `Characterization/UserSessionCharacterizationTests.cs` (8): login cria sessao ativa realm-scoped; senha invalida nao
  cria sessao e incrementa contador; sucesso zera; inativo ⇒ rejeitado + mensagem generica + sem sessao; **lockout** apos
  `MaxFailedAccessAttempts` (3) ⇒ ate a senha correta e rejeitada (prova lockout) + mensagem generica; cookie de sessao
  encerrada ⇒ rejeitado (302 p/ login); emissao de code registra o client na sessao; logout encerra a sessao.
- `Characterization/PromptInteractionCharacterizationTests.cs` (3): baseline autenticado sem prompt emite code;
  `prompt=login` e `max_age=0` forcam login (mesmo caminho do `PromptLoginDecorator` que serve `UserSsoLifetime`).
- `Characterization/BackChannelLogoutCharacterizationTests.cs` (1): logout notifica back-channel dos clients registrados
  na sessao, com `Subject` = nome do usuario (comportamento ATUAL) e `SessionId` corretos.

> Cobertura ja existente reutilizada como rede: `LoginPageTests` (login valido/invalido), `LoginConsentUIFlowTests`
> (consent + denial `access_denied`), `EndSessionTests` (logout message), `RealmIsolationTests`
> (`SessionIsolation_LoginInRealmA_DoesNotAuthenticateRealmB`, code com `RealmId`).

**Mapa `sub == username` (a ajustar na Fase 4, quando `SubjectId` ≠ username):**
- `Tests.Host/HostEndpoints.cs` — `{realm}/test/account/profile` faz `users.GetUserAsync(context.User.GetSubjectId())`,
  i.e. **assume `sub` como chave de username** no store. Na Fase 4 o lookup precisa ser por `SubjectId`.
- `Tests.Integration/Prepare/LoginTests.cs::Login_Profile` — depende do profile acima (assert `userName == "alice"`).
- `Tests.Integration/Endpoints/CodeTokenTests.cs`, `RefreshTokenTests.cs`, `Realm/RealmIsolationTests.cs` e o helper
  `Prepare/SubjectFactory.cs` — usam `SubjectFactory.Create("alice", ...)`, fixando `sub = "alice"` (principal construido
  a mao; **nao quebram**, mas conflatam `sub` com username). Fase 4: usar `SubjectId` deterministico ≠ username nos seeds/asserts.
- `Tests.Integration/Endpoints/UserInfoTests.cs` — so verifica **presenca** das claims `sub`/`name` (seguro).

**Nota de isolamento:** os contadores de falha/lockout e o flag `IsActive` sao mutaveis e vivem no singleton de storage; por
isso todo teste que os altera usa `SeedUser` (usuario unico) — nunca alice/bob — evitando contaminacao entre classes de teste.

---

## Fase 3 - Contratos e tipos de borda

**O que/como:** introduzir no projeto `RoyalIdentity` os **contratos** e **tipos** da tabela "Contratos-alvo",
**sem** alterar comportamento ainda (apenas as interfaces e os tipos imutaveis). Isso permite as fases 4-8
implementarem contra eles. Tipos persistiveis/serializaveis, sem dependencia de servico.

**Tarefas:**
- [ ] Criar `Subject`, `AuthenticationResult`, `UserClaimDto`, `UserSession`, `UserSessionClient` (records; `UserSessionClient` com igualdade por `ClientId` — pontos1 §1).
- [ ] Criar `ISubjectStore`, `ILocalUserAuthenticator`, `IUserPropertyProvider`, `IUserSessionService`, `ISubjectPrincipalFactory`.
- [ ] Criar o gateway de contas `IUserDirectory` (Q1) com getters realm-bound, e o `IAuthorizationContextResolver` (Q2).
- [ ] Criar o accessor de realm corrente (`ICurrentRealmAccessor`), com implementacao HTTP e fake/test helper se necessario.
- [ ] Substituir o contrato existente `IUserSessionStore` por assinaturas puras e realm-aware (`Create/FindById/RecordClient/End`), usando adapters temporarios se necessario para manter a compilacao entre fases.
- [ ] Definir a estrategia de transicao do `IStorage`: `GetUserStore`/`GetUserDetailsStore` devem sair; `GetUserSessionStore` deve apontar para o contrato puro; adapters temporarios precisam ter fase de remocao marcada.
- [ ] Marcar `UserSession.ExpiresAt`/credencial `ExpiresAt` como **reservados** (XML-doc "sem comportamento nesta fase" — pontos1 §6).
- [ ] `dotnet build` verde (contratos compilando; nada referencia ainda).

**Criterios de aceite:** contratos/tipos existem no core; solucao compila; nao ha contrato duplicado de sessao com o mesmo nome; a transicao de `IStorage` esta documentada no proprio plano/fase.

**Testes:** build; testes de igualdade de `UserSessionClient` (dedup por `ClientId`).

### Resultado da Fase 3

A preencher quando a fase for executada.

---

## Fase 4 - Conta in-memory + SubjectId + autenticador

**O que/como:** criar o **registro de conta minimo** no `Storage.InMemory` (fake/referencia) com `SubjectId`
**imutavel** (≠ `Username`), e implementar `ISubjectStore` + `ILocalUserAuthenticator` + `LockoutPolicy` +
(stub) `IUserPropertyProvider`. Indices por `SubjectId` e por `Username` normalizado. Seeds com `SubjectId`
**deterministico**. A resolucao de login (username/email) e do autenticador, **nao** do store (pontos1 §2).
`LockoutPolicy` fica, nesta fase, na implementacao default/in-memory do autenticador; no futuro pertence ao
modulo `UsersAccounts`.

**Tarefas:**
- [ ] Criar registro de conta in-memory (subjectId, username, displayName, isActive, passwordHash, contadores de lockout, roles, claims) — fake/referencia, **nao** o agregado rico.
- [ ] Reindexar `RealmMemoryStore`/`MemoryStorage` por `SubjectId` (+ indice por username normalizado); seeds com `SubjectId` deterministico.
- [ ] Implementar `ISubjectStore` (FindBySubjectId, IsActive) e `ILocalUserAuthenticator` (resolve login → verifica `IPasswordProtector` → `LockoutPolicy`).
- [ ] Implementar `LockoutPolicy` unica (le `AccountOptions.PasswordOptions`; incrementa/zera/calcula `LockoutEndAt`).
- [ ] Implementar o gateway `IUserDirectory` (Q1) in-memory, expondo as portas de conta realm-bound (lendo de `MemoryStorage`); registrar no DI. **Sem realm em metodo.**
- [ ] Regra de imutabilidade: `SubjectId` e chave estavel; se houver operacao de update nesta fase, ela **rejeita** alterar `SubjectId`; se nao houver, registrar a rejeicao como criterio da futura fase admin/modulo.
- [ ] Registrar os novos servicos no DI (`ServiceCollectionExtensions`/`Storage.InMemory`).
- [ ] Auditar e **atualizar testes existentes que assumem `sub` == username** (asserts de `sub` em token/userinfo); usar os `SubjectId` determinísticos dos seeds (≠ username) nesses asserts.

**Criterios de aceite:** dado um login, o autenticador devolve `AuthenticationResult` com `Subject` correto;
`sub` = `SubjectId` (≠ username); lockout funciona num lugar so; `SubjectId` e usado como chave estavel e nao deriva de username.

**Testes:** unidade — senha ausente rejeita; falha incrementa/sucesso zera; lockout temporario expira;
**trocar `Username` nao muda `SubjectId`**; tentativa de trocar `SubjectId` rejeitada se existir API de update nesta fase.

### Resultado da Fase 4

A preencher quando a fase for executada.

---

## Fase 5 - Sessao (modelo + store puro + service)

**O que/como:** `UserSession` serializavel (guarda `SubjectId`, nao usuario) com `Clients` deduplicados;
`IUserSessionStore` **puro** in-memory (sem `HttpContext`; realm-bound via `GetUserSessionStore(realm)`);
`IUserSessionService` para current/validacao/start/end/record-client, resolvendo o realm do **ambiente** por
`ICurrentRealmAccessor` (sem realm em metodo). Migrar consumidores: `DefaultCodeFactory` (RecordClient), validacao de cookie,
`DefaultSignOutManager` (usar `SubjectId`), `EndSessionHandler`.

**Tarefas:**
- [ ] Implementar `UserSessionStore` in-memory puro (`Create/FindById/RecordClient(dedup)/End`), sem `IHttpContextAccessor`.
- [ ] Implementar `IUserSessionService` sem realm em metodo (`GetCurrentAsync(principal)`, `IsSessionValidAsync(principal)`, `StartAsync(subject, method)`, `EndAsync(sid)`, `RecordClientAsync(sid, clientId)`); realm resolvido por `ICurrentRealmAccessor`.
- [ ] `StartAsync` recebe o **metodo de autenticacao** (amr) e o **idp**, para popular `UserSession.AuthenticationMethods`/`IdentityProvider` e o principal.
- [ ] Atualizar `IStorage.GetUserSessionStore(realm)`/`MemoryStorage.Storage.cs` para usar o contrato puro, removendo `GetCurrentSessionAsync` do store.
- [ ] Migrar `DefaultCodeFactory.AddClientIdAsync` → `IUserSessionService.RecordClientAsync`.
- [ ] Migrar `HttpContextExtensions.ValidateUserSessionAsync` (cookie `OnValidatePrincipal`) → `IUserSessionService.IsSessionValidAsync`.
- [ ] Migrar `DefaultSignOutManager`/`EndSessionHandler` para `SubjectId` (sem `session.User.UserName`) e `IUserSessionService.EndAsync`.

**Criterios de aceite:** sessao nao referencia usuario rico; store nao toca `HttpContext`; servico usa accessor de realm, nao `HttpContext` direto; clients deduplicados;
logout SSO (front/back-channel) intacto via `SubjectId`; code registra client; operacoes de sessao realm-scoped **sem realm em parametro** (fabrica/accessor).

**Testes:** integracao — code registra client (sem duplicar); logout encerra e notifica; cookie de sessao
encerrada e rejeitado; back-channel usa `SubjectId`.

### Resultado da Fase 5

A preencher quando a fase for executada.

---

## Fase 6 - "Ativo" unificado

**O que/como:** separar **conta ativa** de **sessao valida** numa unica regra; **sessao ausente ⇒ invalida**
quando o principal tem `sid`. Atualizar os consumidores hoje divergentes.

**Tarefas:**
- [ ] `IProfileService.IsActiveAsync` = `ISubjectStore.IsActiveAsync(sub)` **&&** (se ha `sid`) `IUserSessionService.IsSessionValidAsync(principal)` (portas realm-bound via `client.Realm`/ambiente).
- [ ] Alinhar `ActiveUserValidator` (token endpoint) e `PromptLoginDecorator` (authorize) a regra unica.
- [ ] Confirmar `ValidateUserSessionAsync` (cookie) usando `IsSessionValidAsync` (ausente ⇒ false).
- [ ] Documentar a semantica de sessao nula (XML-doc/coment).

**Criterios de aceite:** os 3 caminhos de "ativo" usam a mesma regra; sessao ausente com `sid` ⇒ invalida;
sem regressao nos fluxos de token/authorize/userinfo.

**Testes:** integracao — token endpoint falha com sessao inativa/ausente; userinfo nao retorna claims p/ conta
inativa; authorize re-pede login com sessao invalida.

### Resultado da Fase 6

A preencher quando a fase for executada.

---

## Fase 7 - Principal + LoginFlowService + telas finas

**O que/como:** `ISubjectPrincipalFactory` monta o principal minimo de sessao (claims obrigatorias) a partir de
`Subject` + `UserSession`: `sub`, `name`, `auth_time`, `sid`, `idp`, `amr`. **Roles e claims de perfil nao entram
no cookie/principal de sessao por esta factory**; elas saem via `IProfileService`/`IUserPropertyProvider`.
`LoginFlowService` fica no core (`RoyalIdentity`) como servico de aplicacao de borda e orquestra:
`ILocalUserAuthenticator` → `IUserSessionService.StartAsync(subject, method)` → `ISubjectPrincipalFactory` →
cookie sign-in → **flow-result (enum)** (consent / signed-in / callback / profile / error). `LoginPageService`
vira **adaptador**: monta view model e traduz o flow-result em redirect/render. `ConsentPageService`/
`EndSessionPageService` confirmados finos.

**Tarefas:**
- [ ] Implementar `ISubjectPrincipalFactory` (sub/name/auth_time/sid/idp/amr); substituir `DefaultIdentityUser.CreatePrincipalAsync` sem mover roles/claims de perfil para o principal de sessao.
- [ ] Verificar que **nenhum consumidor le roles/claims do principal de cookie** (hoje o `IdentityUser` injeta roles + `details.Claims` no cookie); cobrir com teste que o principal de sessao carrega apenas as claims minimas.
- [ ] Implementar `LoginFlowService` (orquestracao + enum de desfecho); **sessao criada aqui**, nao na verificacao de senha.
- [ ] Refatorar `DefaultSignInManager` para delegar ao `LoginFlowService` (ou absorver), expondo so o necessario.
- [ ] Reduzir `LoginPageService` a adaptador de tela (view model + traduz flow-result).
- [ ] Verificar `ConsentPageService`/`EndSessionPageService` (sem regra de conta/sessao vazando).
- [ ] Ajustar eventos de login/logout para nao dependerem de `IdentityUser`; usar `SubjectId`/`Subject`/snapshot minimo, preservando motivo interno de falha para auditoria e mensagem generica para UI.
- [ ] Mover `GetAuthorizationContextAsync` (de `ISignInManager`) para o `IAuthorizationContextResolver` (Q2); `SessionContextService` (Razor) delega; remover o metodo do `ISignInManager`.

**Criterios de aceite:** a tela nao conhece `Subject`/`UserSession`/`ClaimsPrincipal`/stores/cookie; o roteamento
de login vive no `LoginFlowService`; sessao so e criada no sign-in; eventos nao carregam objeto rico de usuario;
principal de sessao contem apenas claims protocolares minimas.

**Testes:** UI flow — login invalido renderiza erro generico; login sem authorize → profile; com authorize sem
consent → callback; com consent → tela de consent; consent denial → `access_denied`; `returnUrl` absoluta
nao-loopback/invalida nao causa open redirect e cai no fluxo de erro esperado.

### Resultado da Fase 7

A preencher quando a fase for executada.

---

## Fase 8 - Claims por propriedade (seam)

**O que/como:** `IProfileService.GetProfileDataAsync` passa a consumir `IUserPropertyProvider`, enviando
**apenas primitivos** (nomes de identity scopes + claim types) e recebendo `UserClaimDto[]` (convertidos para
`Claim` na borda). Implementacao in-memory do provider projeta as claims a partir do registro de conta. Remove
a montagem ad-hoc atual de claims do `DefaultProfileService`. O modulo nunca ve `IdentityScope`/`RequestedResources`.
**Roles e claims de perfil pertencem a este caminho**, nao ao `ISubjectPrincipalFactory`.

**Tarefas:**
- [ ] Implementar `IUserPropertyProvider` in-memory (projeta claims do registro por nomes de scope/claim types).
- [ ] Refatorar `DefaultProfileService.GetProfileDataAsync` para extrair nomes/claim types e chamar o provider; converter `UserClaimDto` → `Claim` na borda.
- [ ] Garantir que `DefaultTokenClaimsService`/`UserInfoHandler` continuam recebendo as mesmas claims efetivas.
- [ ] **Roles:** emitir roles como claims de perfil pelo `IUserPropertyProvider`/`IProfileService`, preservando o comportamento efetivo atual, mas sem colocá-las no principal minimo de sessao.

**Criterios de aceite:** claims de id_token/userinfo identicas ao comportamento atual para os seeds; roles/profile
claims saem pelo provider/profile service; o provider recebe so strings; sem `System.Security.Claims.Claim` cru cruzando a fronteira do provider.

**Testes:** integracao — id_token/userinfo trazem as claims esperadas por identity scope; conta inativa nao
projeta claims.

### Resultado da Fase 8

A preencher quando a fase for executada.

---

## Fase 9 - Limpeza, remocao e regressao final

**O que/como:** remover os tipos antigos e o codigo morto; ajustar consumidores residuais; rodar a suite
completa e validar os criterios de aceite globais.

**Tarefas:**
- [ ] Remover `IdentityUser`/`DefaultIdentityUser`, `UserDetails`, `IUserStore`, `IUserDetailsStore`, `IdentitySession`, `ValidateCredentialsResult`, `CredentialsValidationResult`.
- [ ] Remover `IdentityRevalidatingAuthenticationStateProvider` (e seu registro no `RoyalIdentityRazorServiceCollectionExtensions`).
- [ ] Ajustar `IdentityUserManager` (Razor) para `ISubjectStore` (ou remover se redundante).
- [ ] Remover/adaptar chamadas residuais de `IStorage.GetUserStore`, `GetUserDetailsStore` e contratos antigos de sessao (incluir `Tests.Host`/`HostEndpoints` e `Tests.Identity`, que referenciam os tipos removidos).
- [ ] Remover registros de DI obsoletos; conferir que nenhum `[Redesign]` de Users remanesce sem destino.
- [ ] Rodar build + suite completa; validar os "Criterios de aceite globais".

**Criterios de aceite:** solucao sem os tipos antigos; suite verde; criterios globais satisfeitos.

**Testes:** suite completa (Integration + Identity + Pipelines) verde, sem regressao.

### Resultado da Fase 9

A preencher quando a fase for executada.

---

## Invariantes a preservar (nao regridir)

1. Usuario/sessao **realm-scoped**; nada cruza realm.
2. Login local so com `AllowLocalLogin`; no authorize, tambem `Client.EnableLocalLogin`; restricoes de IdP valem.
3. Erro de login **generico** por default (anti-enumeracao); motivo interno para evento/auditoria.
4. Falha de senha incrementa/registra; sucesso zera; lockout por `MaxFailedAccessAttempts`/`AccountLockoutDurationMinutes`.
5. Senha ausente/credencial desabilitada ⇒ sem login por senha.
6. **Sessao criada so apos autenticacao bem-sucedida** (no sign-in).
7. Cookie contem `sub/sid/auth_time/amr/idp/name`; cookie validado contra sessao ativa.
8. Logout marca sessao inativa; code registra client; front/back-channel logout pelos clients da sessao.
9. `prompt`/`max_age`/`UserSsoLifetime`/restricoes de IdP avaliados antes de emitir tokens.
10. `IProfileService.IsActiveAsync` valida conta **e** sessao; claims filtradas por identity scope.
11. Consentimento por **SubjectId** + client + scope + realm.

---

## Criterios de aceite globais

1. Entidade de borda enxuta (`Subject`) — sem agregado rico no core.
2. `sub` = `SubjectId` estavel, separado de username/email.
3. `UserSession` serializavel sem objeto de comportamento; store sem `HttpContext`; operacoes de sessao realm-scoped **sem realm em parametro** (fabrica/accessor).
4. "Ativo" unico (conta vs sessao); sessao ausente ⇒ invalida.
5. Telas nao conhecem stores/modelos internos de sessao/`ClaimsPrincipal`/cookie e nao fazem sign-in direto.
6. Lockout num lugar so; mensagens genericas mantidas.
7. Testes existentes (login, consent, endsession, userinfo, realm isolation) verdes.
8. Novos testes cobrem lockout, sessao, separacao `SubjectId`/username e "ativo".
9. Eventos de login/logout nao dependem de `IdentityUser`; carregam `SubjectId`/snapshot minimo.
10. `ISubjectPrincipalFactory` emite apenas principal minimo de sessao; roles/claims de perfil saem via `IProfileService`/`IUserPropertyProvider`.
11. Servicos de core nao espalham `HttpContext.GetCurrentRealm()`; usam accessor de realm quando precisam do realm ambiente.
12. Facades prontas para os modulos futuros encaixarem sem reescrever a borda.

---

## Planos futuros (no backlog)

Este plano cobre **so** a borda+sessao (pronto para implementar). Os demais nao estao prontos — cada um exige
ADR/analise propria antes de virar plano. Foram movidos para [backlog-001.md](../backlogs/backlog-001.md):

- **Persistência de Dados (EFCore: Postgres/Sqlite) e Caching** → `plan-data-persistence`.
- **Módulo de Contas de Usuário (RoyalIdentity.UsersAccounts)** → `plan-users-accounts-module`.
- **Key Management Service (KMS)** → `plan-kms`.

Recomendacao mantida: **um plano por fronteira de ADR**, nessa ordem. API e UI de cada modulo sao projetos separados.
