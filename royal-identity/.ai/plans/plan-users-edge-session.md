# Plan: Users — Redesign da Borda + Sessão (camadas A + C)

## Status: IN_PROGRESS

## Progresso

`███████░░` **78%** - 7 de 9 fases concluidas

| Fase | Estado |
|---|---|
| Fase 1 - ADRs (governanca) | Concluida |
| Fase 2 - Testes de caracterizacao | Concluida |
| Fase 3 - Contratos e tipos de borda | Concluida |
| Fase 4 - Conta in-memory + SubjectId + autenticador | Concluida |
| Fase 5 - Sessao (modelo + store puro + service) | Concluida |
| Fase 6 - "Ativo" unificado | Concluida |
| Fase 7 - Principal + LoginFlowService + telas finas | Concluida |
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

Concluida. **Baseline verde:** `dotnet test RoyalIdentity.sln` ⇒ **180 testes, 0 falhas** (Pipelines 3, Identity 6,
Integration 171 — dos quais **14 novos** de caracterizacao). `Tests.Endpoints` nao esta na solution; `Tests.WebApp`
nao tem `[Fact]`.

**Novos artefatos (em `Tests.Integration`):**
- `Prepare/CharacterizationSeed.cs` — helpers: `SeedUser` (usuario unico por chamada — **nao** mexe em alice/bob,
  que sao estado mutavel compartilhado no singleton de storage), `GetDetails`, `FindSession` (por realm), `PostLoginAsync`.
- `Prepare/BackChannelCapturingAppFactory.cs` — `AppFactory` que substitui `IBackChannelLogoutNotifier` por um fake
  capturador (ultima registro de DI vence), para assertar quem foi notificado no logout.
- `Prepare/ControlledTimeAppFactory.cs` - `AppFactory` com `TimeProvider` controlado para caracterizar regras de
  frescor de autenticacao sem `Task.Delay`.
- `Characterization/UserSessionCharacterizationTests.cs` (8): login cria sessao ativa realm-scoped; senha invalida nao
  cria sessao e incrementa contador; sucesso zera; inativo ⇒ rejeitado + mensagem generica + sem sessao; **lockout** apos
  `MaxFailedAccessAttempts` (3) ⇒ ate a senha correta e rejeitada (prova lockout) + mensagem generica; cookie de sessao
  encerrada ⇒ rejeitado (302 p/ login); emissao de code registra o client na sessao; logout encerra a sessao.
- `Characterization/PromptInteractionCharacterizationTests.cs` (4): baseline autenticado sem prompt emite code;
  `prompt=login`, `max_age=0` e `Client.UserSsoLifetime` expirado forcam login; `UserSsoLifetime` e coberto por
  teste direto com relogio controlado.
- `Characterization/BackChannelLogoutCharacterizationTests.cs` (2): logout notifica back-channel dos clients registrados
  na sessao, com `Subject` = nome do usuario (comportamento ATUAL) e `SessionId` corretos; logout tambem grava
  `LogoutCallbackMessage` com front-channel dos clients da sessao, incluindo `iss`, `sid` e `SignOutIframeUrl`.

**Ajuste de test-host:** `{realm}/test/account/logout` agora retorna a URI calculada pelo `SignOutManager`, permitindo
ler o `logoutId` do `LogoutCallbackMessage` sem depender da renderizacao Razor da pagina de processamento.

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
- [x] Criar `Subject`, `AuthenticationResult`, `UserClaimDto`, `UserSession`, `UserSessionClient` (records; `UserSessionClient` com igualdade por `ClientId` — pontos1 §1).
- [x] Criar `ISubjectStore`, `ILocalUserAuthenticator`, `IUserPropertyProvider`, `IUserSessionService`, `ISubjectPrincipalFactory`.
- [x] Criar o gateway de contas `IUserDirectory` (Q1) com getters realm-bound, e o `IAuthorizationContextResolver` (Q2).
- [x] Criar o accessor de realm corrente (`ICurrentRealmAccessor`), com implementacao HTTP e fake/test helper se necessario.
- [~] **Re-escopado para Fase 5** (decisao registrada abaixo): a troca do contrato `IUserSessionStore` para as assinaturas puras (`Create/FindById/RecordClient/End`) toca a impl in-memory + 4 consumidores (`DefaultIdentityUser`, `DefaultSignOutManager`, `HttpContextExtensions`, `MemoryStorage`) — i.e. trabalho da Fase 5 (que reimplementa o store puro). Fazer agora violaria "so interfaces/tipos, sem alterar comportamento" e arriscaria o baseline. A **forma-alvo** do store puro fica definida via `IUserSessionService` + `UserSession`/`UserSessionClient`; **nao** ha contrato duplicado de sessao com o mesmo nome.
- [x] Definir a estrategia de transicao do `IStorage` (documentada no "Resultado da Fase 3").
- [x] Marcar `UserSession.ExpiresAt` como **reservado** (XML-doc "sem comportamento nesta fase" — pontos1 §6). Credencial `ExpiresAt` **nao se aplica a borda** (credencial e dado do modulo de contas, fora deste plano).
- [x] `dotnet build` verde (contratos compilando; nada referencia ainda alem do registro de DI do accessor).

**Criterios de aceite:** contratos/tipos existem no core; solucao compila; nao ha contrato duplicado de sessao com o mesmo nome; a transicao de `IStorage` esta documentada no proprio plano/fase.

**Testes:** build; testes de igualdade de `UserSessionClient` (dedup por `ClientId`).

### Resultado da Fase 3

Concluida (aditiva, **sem alterar comportamento**). **Build + suite verdes:** `dotnet test RoyalIdentity.sln` ⇒
**183 testes, 0 falhas** (Pipelines 3, Identity 9 — **+3 novos** de igualdade de `UserSessionClient`, Integration 171).

**Tipos novos (`RoyalIdentity/Users/`):**
- `Subject(SubjectId, DisplayName, IsActive)` — record enxuto da borda (substitui o `IdentityUser` rico).
- `AuthenticationResult` + enum `AuthenticationFailureReason {NotFound, Inactive, InvalidCredentials, Blocked}` —
  resultado unico (factories `Succeeded(subject)` / `Failed(reason)`).
- `UserClaimDto(Type, Value, ValueType?)` — projecao primitiva do seam de claims.
- `UserSession` (class serializavel: `Id/SubjectId/AuthenticationMethod/IdentityProvider/StartedAt/IsActive/Clients`,
  `ExpiresAt` **reservado**) — guarda `SubjectId`, nao o usuario vivo. `Clients` e `HashSet<UserSessionClient>` (dedup).
- `UserSessionClient(ClientId, FirstSeenAt, LastSeenAt)` — record com **igualdade por `ClientId`** (dedup; coberto por teste).

**Contratos novos (`RoyalIdentity/Users/Contracts/`):** `ISubjectStore`, `ILocalUserAuthenticator`, `IUserPropertyProvider`,
`IUserSessionService`, `ISubjectPrincipalFactory`, `IUserDirectory` (gateway Q1), `IAuthorizationContextResolver` (Q2).
**Accessor:** `RoyalIdentity/Contracts/ICurrentRealmAccessor.cs` + impl HTTP `RoyalIdentity/Authentication/CurrentRealmAccessor.cs`
(le `HttpContext.GetCurrentRealm()`; so resolve realm — sem I/O de cookie), registrado em DI (`AddScoped`). Testes podem usar fake.

**Decisao — `IUserSessionStore` puro fica para a Fase 5 (re-escopo consciente):** a troca in-place do contrato existente
exige reimplementar a impl in-memory e migrar os 4 consumidores (`DefaultIdentityUser`, `DefaultSignOutManager`,
`HttpContextExtensions`, `MemoryStorage`) — exatamente o que a Fase 5 faz ("Implementar `UserSessionStore` in-memory puro").
Antecipar isso na Fase 3 violaria o principio "apenas interfaces/tipos, sem alterar comportamento" e arriscaria o baseline
verde. Por isso a Fase 3 **introduz** a forma-alvo (`UserSession`/`UserSessionClient` + `IUserSessionService`) e **mantem**
o `IUserSessionStore` legado intacto; **nao** existe contrato de sessao duplicado com o mesmo nome (criterio atendido).

**Estrategia de transicao do `IStorage` (a executar nas Fases 5 e 9):**
- `GetUserStore`/`GetUserDetailsStore` → **removidos**; as portas de conta passam pelo gateway `IUserDirectory`
  (impl in-memory na Fase 4). Adapters temporarios, se necessarios, marcados para remocao na Fase 9.
- `GetUserSessionStore(realm)` → **mantido**, **re-tipado** para o `IUserSessionStore` puro na Fase 5 (reusa o nome;
  `GetCurrentSessionAsync` sai do store e vai para o `IUserSessionService`).
- Consumidores migram na Fase 5 (sessao) e Fase 7 (principal/fluxo); limpeza final e remocao de tipos legados na Fase 9.

**Criterios de aceite:** atendidos — contratos/tipos existem no core; **solucao compila**; **nao ha contrato de sessao
duplicado**; a transicao do `IStorage` esta documentada aqui.

**Teste novo:** `Tests.Identity/Users/UserSessionClientTests.cs` (igualdade por `ClientId` + dedup em `HashSet`).

---

## Fase 4 - Conta in-memory + SubjectId + autenticador

**O que/como:** criar o **registro de conta minimo** no `Storage.InMemory` (fake/referencia) com `SubjectId`
**imutavel** (≠ `Username`), e implementar `ISubjectStore` + `ILocalUserAuthenticator` + `LockoutPolicy` +
(stub) `IUserPropertyProvider`. Indices por `SubjectId` e por `Username` normalizado. Seeds com `SubjectId`
**deterministico**. A resolucao de login (username/email) e do autenticador, **nao** do store (pontos1 §2).
`LockoutPolicy` fica, nesta fase, na implementacao default/in-memory do autenticador; no futuro pertence ao
modulo `UsersAccounts`.

**Tarefas:**
- [x] Criar registro de conta in-memory (subjectId, username, displayName, isActive, passwordHash, contadores de lockout, roles, claims) — fake/referencia, **nao** o agregado rico. (`UserDetails` ganhou `SubjectId`.)
- [x] Resolver `RealmMemoryStore`/`MemoryStorage` por `SubjectId` via **dual-resolution** (username ou `SubjectId`); seeds com `SubjectId` deterministico. Indice real por `SubjectId` fica para o modulo `UsersAccounts`.
- [x] Implementar `ISubjectStore` (FindBySubjectId, IsActive) e `ILocalUserAuthenticator` (resolve login → verifica `IPasswordProtector` → `LockoutPolicy`).
- [x] Implementar `LockoutPolicy` unica (le `AccountOptions.PasswordOptions`; incrementa/zera; lockout deriva de `LastPasswordError` + duracao — sem campo `LockoutEndAt` no modelo atual).
- [x] Implementar o gateway `IUserDirectory` (Q1) in-memory, expondo as portas de conta realm-bound (lendo de `MemoryStorage`); registrar no DI. **Sem realm em metodo.**
- [x] Regra de imutabilidade: `SubjectId` e chave estavel e separado de `Username` (campos distintos); **nao ha API de update na borda nesta fase** ⇒ a rejeicao explicita de troca de `SubjectId` fica como criterio da futura fase admin/modulo `UsersAccounts` (registrado).
- [x] Registrar os novos servicos no DI (`ServiceCollectionExtensions`/`Storage.InMemory`).
- [x] Auditar testes que assumem `sub` == username: com a **dual-resolution** no `UserStore` (resolve por username **ou** `SubjectId`) **nenhum** teste existente quebrou; em vez de "ajustar", foi **adicionado** teste positivo provando `sub` = `SubjectId` (≠ username).

**Criterios de aceite:** dado um login, o autenticador devolve `AuthenticationResult` com `Subject` correto;
`sub` = `SubjectId` (≠ username); lockout funciona num lugar so; `SubjectId` e usado como chave estavel e nao deriva de username.

**Testes:** unidade — senha ausente rejeita; falha incrementa/sucesso zera; lockout temporario expira;
**trocar `Username` nao muda `SubjectId`**; tentativa de trocar `SubjectId` rejeitada se existir API de update nesta fase.

### Resultado da Fase 4

Concluida. **Build + suite verdes:** `dotnet test RoyalIdentity.sln` ⇒ **191 testes, 0 falhas** (Pipelines 3,
Identity 9, Integration 179 — **+8 novos**: 1 de flip de `sub` + 7 de autenticador/lockout/imutabilidade).

**Modelo / seeds:**
- `UserDetails` ganhou `SubjectId` (imutavel, ≠ `Username`); `MemoryStorage.AliceSubjectId`/`BobSubjectId`
  (GUIDs opacos, deterministicos, **nao** derivados do username), expostos para os testes.
- `DefaultIdentityUser.CreatePrincipalAsync` agora emite `sub = details.SubjectId` (era `details.Username`).

**Facades in-memory novas (`RoyalIdentity.Storage.InMemory`):** `MemorySubjectStore` (`ISubjectStore`),
`MemoryLocalUserAuthenticator` (`ILocalUserAuthenticator` — resolve login username/email → `IPasswordProtector` →
`LockoutPolicy`; **nao** inicia sessao), `MemoryUserPropertyProvider` (`IUserPropertyProvider` — stub funcional,
so ligado ao `IProfileService` na Fase 8), `LockoutPolicy` (unica), `MemoryUserDirectory` (`IUserDirectory`,
getters realm-bound; registrado no DI in-memory).

**Decisao — keying / resolucao por `SubjectId`:** o fake store mantem `UsersDetails` keyed por **username**
(ripple minimo: seed, `MemoryStorage.Storage.cs`, helper de teste inalterados) e resolve por `SubjectId` via
**dual-resolution** no `UserStore.Resolve(key)` (username **ou** `SubjectId` — nunca colidem: `SubjectId` e GUID).
Isso foi decisivo: como `CreateIdentity` usa `sub` como `nameType`, `Identity.Name` passou a ser o `SubjectId`;
a dual-resolution faz os 3 caminhos (login por username; `IProfileService.GetUserDetailsAsync(sub)`; Razor
`GetUserAsync(Identity.Name)`) funcionarem **sem** quebrar nada nem trocar assinaturas/consumidores. Index real
fica para o modulo `UsersAccounts` (produção); no fake, scan e suficiente.

**Lembrete para Fase 5:** revisar `DefaultSignOutManager`/back-channel logout para usar `SubjectId`; ate a migracao
do store/servico de sessao, ainda ha trecho legado com `session.User.UserName`.

**Lockout num lugar so:** `LockoutPolicy` (usada pelo novo autenticador). O lockout do `DefaultIdentityUser`
legado coexiste ate a Fase 9 (remocao do caminho antigo); o novo caminho (Fase 7, `LoginFlowService`) ja usa so a `LockoutPolicy`.

**Imutabilidade de `SubjectId`:** `SubjectId` e campo proprio, estavel a troca de `Username` (teste). **Nao existe
API de update na borda** ⇒ a **rejeicao** ativa de alterar `SubjectId` e criterio da futura fase admin/modulo.

**`sub` ≠ username — sem regressao:** nenhum teste existente assertava `sub == username` em token/userinfo de forma
acoplada (mapa da Fase 2); os que usam `SubjectFactory.Create("alice", ...)` constroem o principal a mao (sub fixo),
nao dependem do store. Novo teste `SubjectIdCharacterizationTests` prova o flip end-to-end (id_token `sub` =
`AliceSubjectId`).

**Testes novos:** `Tests.Integration/Characterization/SubjectIdCharacterizationTests.cs` (flip de `sub` no id_token) e
`Tests.Integration/Users/LocalUserAuthenticatorTests.cs` (7: not found/inactive/sem hash/senha errada incrementa/
sucesso zera + devolve `SubjectId`/lockout apos N falhas e **expira**/`SubjectId` estavel a troca de username).

---

## Fase 5 - Sessao (modelo + store puro + service)

**O que/como:** `UserSession` serializavel (guarda `SubjectId`, nao usuario) com `Clients` deduplicados;
`IUserSessionStore` **puro** in-memory (sem `HttpContext`; realm-bound via `GetUserSessionStore(realm)`);
`IUserSessionService` para current/validacao/start/end/record-client, resolvendo o realm do **ambiente** por
`ICurrentRealmAccessor` (sem realm em metodo). Migrar consumidores: `DefaultCodeFactory` (RecordClient), validacao de cookie,
`DefaultSignOutManager` (usar `SubjectId`), `EndSessionHandler`.

**Tarefas:**
- [x] Implementar `UserSessionStore` in-memory puro (`Create/FindById/RecordClient(dedup)/End`), sem `IHttpContextAccessor`. (`MemoryStorage` deixou de injetar `IHttpContextAccessor`.)
- [x] Implementar `IUserSessionService` (`DefaultUserSessionService` no core) sem realm em metodo (`GetCurrentAsync(principal)`, `IsSessionValidAsync(principal)`, `StartAsync(subject, method, idp)`, `EndAsync(sid)`, `RecordClientAsync(sid, clientId)`); realm resolvido por `ICurrentRealmAccessor`; registrado no DI.
- [x] `StartAsync` recebe **amr** e **idp**, populando `UserSession.AuthenticationMethod`/`IdentityProvider`.
- [x] Atualizar `IStorage.GetUserSessionStore(realm)`/`MemoryStorage.Storage.cs` para o contrato puro; `GetCurrentSessionAsync` removido do store (a "sessao atual" vive no `IUserSessionService`). `RealmMemoryStore.UserSessions` agora e `ConcurrentDictionary<string, UserSession>`.
- [x] Migrar `DefaultCodeFactory.AddClientIdAsync` → `RecordClientAsync` (via `GetUserSessionStore(context.Realm)` — realm-bound por fabrica; equivalente ao service).
- [x] Migrar `HttpContextExtensions.ValidateUserSessionAsync` (cookie `OnValidatePrincipal`) → `IUserSessionService.IsSessionValidAsync`.
- [x] Migrar `DefaultSignOutManager`/`EndSessionHandler` para `SubjectId` (sem `session.User.UserName`) e `IUserSessionService.EndAsync`/`FindByIdAsync`.

**Criterios de aceite:** sessao nao referencia usuario rico; store nao toca `HttpContext`; servico usa accessor de realm, nao `HttpContext` direto; clients deduplicados;
logout SSO (front/back-channel) intacto via `SubjectId`; code registra client; operacoes de sessao realm-scoped **sem realm em parametro** (fabrica/accessor).

**Testes:** integracao — code registra client (sem duplicar); logout encerra e notifica; cookie de sessao
encerrada e rejeitado; back-channel usa `SubjectId`.

### Resultado da Fase 5

Concluida (inclui a troca in-place do `IUserSessionStore` re-escopada da Fase 3). **Build + suite verdes:**
`dotnet test RoyalIdentity.sln` ⇒ **196 testes, 0 falhas** (Pipelines 3, Identity 9, Integration 184 — **+5 novos**:
1 de dedup de client no fluxo + 4 unit do `DefaultUserSessionService`/store puro).

**Modelo + store + service:**
- `RealmMemoryStore.UserSessions` re-tipado para `ConcurrentDictionary<string, UserSession>` (guarda `SubjectId`, nao o usuario).
- `IUserSessionStore` (contrato) **reescrito puro**: `CreateAsync`/`FindByIdAsync`/`RecordClientAsync(dedup)`/`EndAsync`;
  **sem** `HttpContext`, **sem** `GetCurrentSessionAsync`. Impl `UserSessionStore` agora so depende do dict + `TimeProvider`.
- `MemoryStorage` deixou de receber `IHttpContextAccessor` (store puro nao precisa).
- `DefaultUserSessionService` (core, `IUserSessionService`): `GetCurrentAsync`/`IsSessionValidAsync` (presente-e-ativa;
  ausente ⇒ invalida) / `StartAsync(subject, amr, idp)` / `EndAsync` / `RecordClientAsync`; realm via `ICurrentRealmAccessor`
  (nunca em metodo); registrado em DI (Scoped).

**Consumidores migrados:** cookie `OnValidatePrincipal` (`HttpContextExtensions.ValidateUserSessionAsync` →
`IUserSessionService.IsSessionValidAsync`); `DefaultCodeFactory` (→ `RecordClientAsync`); `DefaultProfileService`
(`GetUserSessionAsync` → `FindByIdAsync`); `EndSessionHandler` (`FindByIdAsync` + `Clients.Single().ClientId`);
`DefaultSignOutManager` (`EndAsync`; **back-channel `Subject = session.SubjectId`** em vez de `session.User.UserName`;
itera `UserSessionClient`).

**Login (ponte ate a Fase 7):** `DefaultIdentityUser` ainda cria a sessao na verificacao de senha, mas agora monta
um `UserSession` e chama `sessionStore.CreateAsync` (em vez do antigo `StartSessionAsync(IdentityUser)`); o principal
le `idp`/`amr` da sessao. `CredentialsValidationResult`/`ValidateCredentialsResult`/`ISignInManager.SignInAsync`/
`IdentityUser.CreatePrincipalAsync` passaram de `IdentitySession` → `UserSession`. **A remocao do efeito colateral
(sessao criada so no sign-in) fica para a Fase 7** (`LoginFlowService`); `IUserSessionService.StartAsync` ja existe e
sera adotado la.

**Caracterizacao atualizada (mudanca de comportamento visivel):** `BackChannelLogoutCharacterizationTests` agora
asserta `notified.Subject == session.SubjectId` (era `== username`) — exatamente o que o teste da Fase 2 fixou para
tornar a mudanca deliberada. `FindSession` (helper) passou a achar a sessao por `SubjectId` (resolvendo username→sub).

**`IdentitySession` (legado):** ainda existe o tipo, mas **nao** e mais usado por nenhum store/fluxo (so resta a
classe). Remocao formal na Fase 9.

**Testes novos:** `Tests.Integration/Users/DefaultUserSessionServiceTests.cs` (Start cria ativa com `SubjectId`;
`IsSessionValid` ativa/ausente/encerrada; `GetCurrent` por `sid`; `RecordClient` dedup) e
`CodeIssuance_SameClientTwice_RecordedOnce` (dedup no fluxo HTTP).

---

## Fase 6 - "Ativo" unificado

**O que/como:** separar **conta ativa** de **sessao valida** numa unica regra; **sessao ausente ⇒ invalida**
quando o principal tem `sid`. Atualizar os consumidores hoje divergentes.

**Tarefas:**
- [x] `IProfileService.IsActiveAsync` = `ISubjectStore.IsActiveAsync(sub)` (via `IUserDirectory`, realm de `client.Realm`) **&&** (se ha `sid`) `IUserSessionService.IsSessionValidAsync(principal)`.
- [x] Alinhar `ActiveUserValidator` (token endpoint) e `PromptLoginDecorator` (authorize) a regra unica — ambos ja chamam `IProfileService.IsActiveAsync`, agora unificado.
- [x] Confirmar `ValidateUserSessionAsync` (cookie) usando `IsSessionValidAsync` (ausente ⇒ false) — migrado na Fase 5; agora os 3 caminhos compartilham a mesma definicao de "sessao valida".
- [x] Documentar a semantica de sessao nula (XML-doc em `IsActiveAsync` + `IUserSessionService`).

**Criterios de aceite:** os 3 caminhos de "ativo" usam a mesma regra; sessao ausente com `sid` ⇒ invalida;
sem regressao nos fluxos de token/authorize/userinfo.

**Testes:** integracao — token endpoint falha com sessao ausente; userinfo nao retorna claims p/ conta
inativa; authorize re-pede login com sessao encerrada/invalida.

### Resultado da Fase 6

Concluida. **Build + suite verdes:** `dotnet test RoyalIdentity.sln` ⇒ **199 testes, 0 falhas** (Pipelines 3,
Identity 9, Integration 187 — **+3 novos** de aceite da regra unica).

**Regra unica (`DefaultProfileService.IsActiveAsync`):** `conta ativa` (via `IUserDirectory.GetSubjectStore(client.Realm).IsActiveAsync(sub)`)
**&&** (se o principal tem `sid`) `IUserSessionService.IsSessionValidAsync(principal)`. **Sessao ausente com `sid` ⇒ invalida**
(antes: ausente ⇒ tratada como ativa — divergia do cookie). Principal **sem** `sid` ⇒ so checa conta. `DefaultProfileService`
passou a depender de `IUserDirectory` + `IUserSessionService` (alem do `IStorage`, ainda usado por `GetProfileDataAsync` ate a Fase 8).

**3 caminhos alinhados:** token endpoint (`ActiveUserValidator`) e authorize (`PromptLoginDecorator`) ja chamavam
`IsActiveAsync`; cookie (`ValidateUserSessionAsync`) usa `IsSessionValidAsync` desde a Fase 5 — agora todos compartilham a
mesma definicao de "sessao valida".

**Mudanca de comportamento + ajuste de testes:** como a conta agora e resolvida por `ISubjectStore` (so por `SubjectId`) e a
sessao ausente passou a invalidar, os testes que fabricavam principal via `SubjectFactory.Create("alice", ...)` (sub=username,
`sid` aleatorio **sem** sessao) precisaram de conta real + sessao ativa. Solucao: novo `SubjectFactory.CreateWithSession(storage,
realm, subjectId, name, role)` (semeia sessao ativa e usa `MemoryStorage.AliceSubjectId`). Migrados os exchanges de token:
`CodeTokenTests` (11), `RefreshTokenTests` (1), `SigningAlgorithmTests` (1). Os usos **sem** exchange (consent, isolamento de
store em `RealmIsolationTests`) seguem com `SubjectFactory.Create`. `DefaultTokenFactory` exige `sid` no principal (access/id/refresh),
entao "principal sem sid" nao e opcao para esses testes — dai semear a sessao.

**Decisao realm:** o check de conta usa `client.Realm` (explicito, via gateway); o check de sessao usa o `IUserSessionService`
ambiente (`ICurrentRealmAccessor`). Nos caminhos que chamam `IsActiveAsync` (token/authorize, sempre HTTP) o realm ambiente ==
`client.Realm`, entao sao coerentes.

**Testes novos:** `Tests.Integration/Characterization/ActiveRuleCharacterizationTests.cs` — (1) token endpoint **rejeita** auth_code
quando a sessao esta **ausente** (conta ativa, `sid` sem sessao); (2) authorize **re-pede login** quando a sessao foi encerrada;
(3) userinfo **nao** retorna claims de perfil (so `sub`) para conta inativa.

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
- [x] Implementar `ISubjectPrincipalFactory` (`DefaultSubjectPrincipalFactory`: sub/name/auth_time/sid/idp/amr); usado pelo `LoginFlowService` no lugar de `DefaultIdentityUser.CreatePrincipalAsync` (que ficou orfao → removido na Fase 9). Sem roles/claims de perfil no principal de sessao.
- [x] Verificar que **nenhum consumidor le roles/claims do principal de cookie** — so `ProfilePage.razor` os **exibe** (cosmetico, sem teste). Coberto por teste unitario (`SubjectPrincipalFactoryTests`) + e2e (`SessionPrincipalCharacterizationTests`: cookie pos-login carrega so as 6 claims minimas, sem role/email).
- [x] Implementar `LoginFlowService` (orquestracao + enum `LoginFlowOutcome`); **sessao criada aqui** (no sign-in, via `IUserSessionService.StartAsync`), nao na verificacao de senha.
- [x] **Remover `ISignInManager`/`DefaultSignInManager`** (totalmente substituidos — sem codigo morto): login agora e `LoginFlowService`; contexto via `IAuthorizationContextResolver`; consent via `IConsentService`.
- [x] Reduzir `LoginPageService` a adaptador de tela (resolve realm + traduz `LoginFlowOutcome` → URL/redirect; mantem a escrita de error-message no `IMessageStore` para o caso open-redirect).
- [x] Verificar `ConsentPageService`/`EndSessionPageService` — confirmados finos (sem store/sessao/`IdentityUser`/`ClaimsPrincipal`).
- [x] Ajustar eventos: `UserLoginSuccessEvent` deixou de carregar `IdentityUser` → agora `(username, subjectId, context)`; motivo interno preservado no `UserLoginFailureEvent`, mensagem generica para UI.
- [x] Mover `GetAuthorizationContextAsync` → `IAuthorizationContextResolver` (`DefaultAuthorizationContextResolver`); `AuthenticationExtensions`/`SessionContextService` delegam a ele; metodo saiu junto com o `ISignInManager`.

**Criterios de aceite:** a tela nao conhece `Subject`/`UserSession`/`ClaimsPrincipal`/stores/cookie; o roteamento
de login vive no `LoginFlowService`; sessao so e criada no sign-in; eventos nao carregam objeto rico de usuario;
principal de sessao contem apenas claims protocolares minimas.

**Testes:** UI flow — login invalido renderiza erro generico; login sem authorize → profile; com authorize sem
consent → callback; com consent → tela de consent; consent denial → `access_denied`; `returnUrl` absoluta
nao-loopback/invalida nao causa open redirect e cai no fluxo de erro esperado.

### Resultado da Fase 7

Concluida. **Build + suite verdes:** `dotnet test RoyalIdentity.sln` ⇒ **201 testes, 0 falhas** (Pipelines 3,
Identity 9, Integration 189 — **+2 novos**: principal minimo unit + e2e).

**Novos servicos de borda (core):**
- `DefaultSubjectPrincipalFactory` (`ISubjectPrincipalFactory`) — principal minimo (sub/name/auth_time/sid/idp/amr),
  **sem roles/claims de perfil**.
- `LoginFlowService` (`RoyalIdentity/Users/Defaults/`) — orquestra `ILocalUserAuthenticator` →
  `IUserSessionService.StartAsync` (**sessao criada no sign-in**) → `ISubjectPrincipalFactory` → cookie
  (`HttpContext.SignInAsync`) → eventos → roteamento `LoginFlowOutcome`
  (Error/RequiresConsent/Callback/SignedInPage/Profile/LocalRedirect/InvalidReturnUrl). Tipos `LoginRequest`/
  `LoginFlowResult`/`LoginFlowOutcome` em `RoyalIdentity/Users/`.
- `DefaultAuthorizationContextResolver` (`IAuthorizationContextResolver`) — recebe o `GetAuthorizationContextAsync`
  que vivia no `ISignInManager`.

**Removidos:** `ISignInManager` + `DefaultSignInManager` (substituidos por `LoginFlowService` + resolver — sem codigo
morto). `DefaultIdentityUser.CreatePrincipalAsync`/`AuthenticateAndStartSessionAsync` ficaram **orfaos** (login nao
passa mais por `IdentityUser`); `IdentityUser`/`CredentialsValidationResult` saem na Fase 9.

**Consumidores migrados:** `AuthenticationExtensions.GetAuthorizationContextAsync` → resolver; `LoginPageService`
virou **adaptador** (resolve realm + traduz outcome → URL; mantem `IMessageStore` so para o error-page do open-redirect);
test host `{realm}/test/account/login` → `LoginFlowService`. `SessionContextService` inalterado (delega via extension).

**Sessao no sign-in (fim do efeito colateral):** a sessao agora e criada **apenas** no `LoginFlowService` (sign-in);
`DefaultIdentityUser.AuthenticateAndStartSessionAsync` nao e mais chamado. **Lockout num lugar so de fato:** o caminho
vivo usa so a `LockoutPolicy` (via `MemoryLocalUserAuthenticator`); o lockout legado do `IdentityUser` virou codigo morto.

**Principal minimo confirmado:** tokens/userinfo continuam com roles/email (vem do `IProfileService`, nao do cookie —
ADR-014 §2.8). So `ProfilePage.razor` **exibe** as claims do cookie (cosmetico). Eventos sem objeto rico.

**Testes novos:** `Tests.Integration/Users/SubjectPrincipalFactoryTests.cs` (factory emite exatamente as 6 claims) e
`Tests.Integration/Characterization/SessionPrincipalCharacterizationTests.cs` (cookie pos-login = so claims minimas,
`sub`=`AliceSubjectId`, sem role/email; novo endpoint de teste `{realm}/test/account/principal`). Roteamento de login
(invalido→erro; sem authorize→profile; authorize sem consent→callback; com consent→tela; denial→`access_denied`) ja
coberto por `LoginPageTests`/`LoginConsentUIFlowTests` (verdes contra o novo fluxo).

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
