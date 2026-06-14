# Clarificação — Borda, Módulos e Reescopo do Redesign de Usuários/Sessão/Identidade

> **O que é:** resposta a [an-users-pontos1.md](an-users-pontos1.md) (7 ajustes) e
> [an-users-pontos2.md](an-users-pontos2.md) (visão ampliada, escrita pelo autor do projeto).
> **Reescopa** [an-users-final.md](an-users-final.md), que passa a ser apenas insumo da camada de borda+sessão.
>
> **Tese central (de pontos2):** o `an-users-final` remodelou a **borda** do IdP, mas a modelagem cresceu
> até virar — implicitamente — um **módulo de contas de usuário**, sem desenhar fronteiras. Há, na verdade,
> **três modelos distintos** a separar (**borda / contas / sessão**) e uma **arquitetura modular de
> persistência** por trás dos *storages* (que são *facades*). A implementação *in-memory* é uma
> **implementação fake/referência** (dev/test/integração/**realm Demo**), **não** a forma de produção.
>
> **Avaliação:** a crítica de pontos2 está **correta** (§1). Este documento clarifica a separação, mapeia
> cada elemento do `an-users-final` à sua camada, consolida os requisitos do módulo de contas, responde aos
> 7 pontos do pontos1, e revê a governança (qual ADR vem antes). **Consenso base:** [an-user-con2.md](an-user-con2.md).
>
> **Decisões de nomes/recomendações (rodada com o autor):** os esquemas de §11 foram **aceitos**; o nome dos
> projetos de dados ficou **`RoyalIdentity.Data.*`** (Q1). Edge contract renomeado para **`ISubjectStore`**;
> contrato de login local nomeado como **`ILocalUserAuthenticator`**; contratos permanecem no **core**;
> ADRs **simplificados** para 013 (arquitetura) + 014 (borda+sessão) + futura (módulo). Detalhes nas seções
> correspondentes.
>
> **Método:** leitura de pontos1/pontos2/final/código. Nenhum build/teste (documental).

---

## 1. Por que esta revisão (validação da crítica)

O `an-users-final` é um bom caminho para resolver a confusão atual, mas **borrou a fronteira borda↔módulo**.
Onde ele cruzou a linha (e por que pontos2 está certo):

| Sintoma no `an-users-final` | Por que é de **módulo de contas**, não de borda |
|---|---|
| `UserAccount` com perfil, roles, claims, security state, credencial, identidades externas | É um **agregado administrativo** — domínio de gestão de contas, não o que o IdP usa por request. |
| "Converter `UserClaim` → `Claim` **só na borda**" | A própria frase admite que o modelo **é de módulo**; a borda só consome a projeção. |
| `SecurityStamp`, `PasswordCredential` (lifecycle), `MustChangePassword`, multi-credencial | Ciclo de vida de credencial = gestão de contas, não emissão de token. |
| `FindByLoginAsync` aplicando `EmailAsUsername`/`LoginWithEmail` no *store* | Política de login/identificadores = regra de conta, não persistência pura (pontos1 §2). |
| Tratar o `IUserAccountStore` in-memory como destino | In-memory é **fake/referência** (dev/test/integração/demo); produção usa módulos EFCore atrás de *facades*. |

**Conclusão:** o `an-users-final` acertou o *diagnóstico da dor* (objeto rico ligado a serviços, sessão
acoplada, `sub`=username, etc.), mas precisa ser **fatiado em três camadas** e encaixado numa **arquitetura
modular** onde os *storages* são *facades*.

> Ponto de ancoragem real: o `IStorage.GetXStore(realm)` **já é** o padrão de *facade*; a in-memory é uma
> implementação; futuras implementações EFCore são outras. E o `structure.md` já lista
> `RoyalIdentity.Users/` e `RoyalIdentity.Web/` como **diretórios planejados** (sem csproj ativo) — ou seja,
> a intenção de separar projetos já existia.

---

## 2. As três preocupações (a separação central)

| Camada | Dono (projeto) | Responsabilidade | NÃO é dela |
|---|---|---|---|
| **A. Borda** | `RoyalIdentity` (IdP) | O que o IdP precisa **por request** para OIDC: orquestrar login, montar principal, emitir claims, validar "ativo", consentir, logout. Define **contratos/facades**. | Persistir/administrar contas; armazenar credenciais; propriedades dinâmicas. |
| **B. Contas de usuário** | `RoyalIdentity.UsersAccounts` (módulo, **fora** do IdP) | Domínio rico de contas: dados OIDC, emails (opcional/múltiplo/fictício), ID externo, **propriedades dinâmicas por escopo**, credenciais, lockout por realm, eventos, inbox/outbox, replicação, **casos de uso administrativos**, persistência. | Pipeline OIDC, sessões, cookies. |
| **C. Sessão** | IdP (operacional) — persistência em `RoyalIdentity.Data.Operational` | Modelo operacional da sessão SSO: `sid/sub/realm/idp/amr/auth_time/clients/active`, ciclo de vida, validação. | Dados de conta; credencial. |

Regra de ouro: **a borda nunca conhece o interior do módulo de contas**; fala com ele por *facade*
(contrato no IdP). O módulo de contas **não conhece** pipeline/sessão/cookie.

---

## 3. Arquitetura modular (nomes decididos com o autor)

Os *storages* do IdP são **facades**; atrás deles ficam módulos. In-memory = **fake/referência**
(dev/test/integração/demo); EFCore = produção.

```
RoyalIdentity.Pipelines            (base, sem dependências)
        ▲
RoyalIdentity  (IdP)               contratos/facades (ISubjectStore, ILocalUserAuthenticator, IProfileService,
        │                          IUserSessionStore, ...), modelo de BORDA, modelo/serviços de SESSÃO,
        │                          fluxo de login/consent/logout
        │  (facades implementadas por →)
        ├── RoyalIdentity.Storage.InMemory               (existente; fake/referência: dev/test/integração/demo)
        ├── RoyalIdentity.Storage.EntityFramework        (produção; impl. das facades do IdP via Data.*)
        │        └── RoyalIdentity.Storage.EntityFramework.Postgre / .Sqlite   (mapeamentos/migrations por provedor)
        ├── RoyalIdentity.Storage.Caching                (cache sobre as implementações de storage)
        │
        ├── RoyalIdentity.Data.Configuration             (DADOS/EFCore sem depender do core: realms, clients, resources, keys, options + DbContext + queries)
        ├── RoyalIdentity.Data.Operational               (DADOS/EFCore sem depender do core: sessions, tokens, codes, consents  ← persistência da camada C)
        │
        ├── RoyalIdentity.UsersAccounts                  (MÓDULO de contas — camada B; domínio + persistência + casos de uso)
        └── RoyalIdentity.KMS                            (MÓDULO vault de chaves/segredos/certificados; domínio + persistência)
```

Notas e recomendações:
- **Direção de dependência (decidido):** os contratos/facades ficam no **core `RoyalIdentity`**; quem
  implementa referencia o core (igual ao `Storage.InMemory` hoje). **Refinamento:** os projetos
  **`RoyalIdentity.Data.*` não dependem do core `RoyalIdentity`**. Eles podem depender de EFCore e conter
  `DbContext`, entidades de persistência e queries, mas não conhecem tipos/contratos da biblioteca IdP.
  Quem adapta `Data.*` às facades do IdP é o `Storage.EntityFramework`. Logo **`RoyalIdentity.Abstractions`
  é desnecessário agora** (mudança estrutural grande, adiada).
- **Escopo do `Storage.EntityFramework` (decidido):** ele adapta **somente os dados do IdP** (`Data.Configuration`
  e `Data.Operational`) para os contratos do core. O módulo **`RoyalIdentity.UsersAccounts` não é adaptado
  pelo `Storage.EntityFramework` do IdP**; ele possui domínio/persistência próprios e, quando necessário,
  expõe sua própria implementação/integração para as portas de borda (`ISubjectStore`,
  `ILocalUserAuthenticator`, `IUserPropertyProvider`).
- **Divisão `Data.Configuration` × `Data.Operational`:** ciclos de vida/volumes muito diferentes (config é
  raramente escrita; operacional é alta rotatividade + TTL/cache).
- **Mapeamentos de provedor** (Postgre/Sqlite) ficam **sob `Storage.EntityFramework.*`**, não nos projetos
  de dados — assim o nome dos `Data.*` fica livre de provedor.
- **Sessão (camada C):** o **contrato/serviço** (`IUserSessionStore`/`IUserSessionService`) fica no **IdP**;
  a **persistência** vai para `Data.Operational` (EFCore). In-memory cobre dev/test.
- **Módulos `UsersAccounts` e `KMS`:** contêm **apenas domínio + persistência**. **API e UI são projetos
  separados** (um conjunto de projetos de API e de UI para cada módulo) — não vivem dentro do módulo. Não
  tratados agora.

---

## 4. Reescopo do `an-users-final` pelas três camadas

Cada elemento do `an-users-final` re-endereçado:

| Elemento (an-users-final) | Camada destino | Observação |
|---|---|---|
| `UserAccount` (rico) | **B (módulo)** | Vira a entidade administrativa do `UsersAccounts`; **não** é tipo da biblioteca IdP. |
| `UserSecurityState` / `SecurityStamp` | **B (módulo)** | Estado de segurança da conta. `SecurityStamp` é consumido na borda só para invalidar sessão (futuro). |
| `PasswordCredential` (+ lifecycle) | **B (módulo)** | Credencial é dado de conta; **verificação** também (o módulo verifica e aplica lockout). |
| `UserClaim` (bag) | **B (módulo)** → vira **propriedades dinâmicas por escopo** (§6/§7) | A borda consome via `IProfileService` (projeção por identity scope). |
| `ExternalIdentity` | **B (módulo)** | Identidades federadas vinculadas à conta. |
| `LockoutPolicy` | **B (módulo)** | Política por realm, sobre dados da conta. A borda só recebe "bloqueado/ok". |
| Resultado único de autenticação | **A/B (contrato de borda)** | O módulo devolve um **resultado/sujeito autenticado** mínimo à borda. |
| `UserSession` / `UserSessionClient` | **C (IdP operacional)** | Modelo de sessão; persistência no módulo operacional. |
| `IUserSessionStore` (puro) / `IUserSessionService` | **C (contrato/serviço no IdP)** | Contrato no IdP; impl. EFCore no operacional; in-memory p/ teste. |
| `ISubjectPrincipalFactory` | **A (borda)** | Monta `ClaimsPrincipal` a partir do sujeito+sessão; puro de borda. |
| Fluxo de login (login-flow) | **A (borda)** | Orquestra: autenticar (chama módulo) → iniciar sessão → principal → cookie → resultado. |
| Semântica de "ativo" unificada | **A+C** | "Conta ativa" = pergunta ao módulo; "sessão válida" = serviço de sessão. |
| `IUserAccountStore` (facade) → **`ISubjectStore`** | **A define / B implementa** | Contrato de borda (renomeado — evita colisão com o `IUserStore` atual); backing = `UsersAccounts` e sua persistência própria (prod) ou in-memory (dev/test). |
| Remoção de `IdentityUser`/`UserDetails`/resultados/provider | **A (borda)** | Limpeza da borda, como já previsto. |

**Resumo:** quase todo o "modelo rico" do `an-users-final` é **camada B (módulo)**. A biblioteca IdP fica
com **contratos/facades + modelo de borda enxuto + sessão + fluxo**.

---

## 5. Contratos da borda (modelo enxuto do IdP — nomes finais)

A borda **não** precisa do agregado rico. Esta é a **trava contra o `UserAccount` voltar ao core**: nomes
finais + responsabilidade exata de cada contrato. Os contratos ficam no projeto `RoyalIdentity`; a
implementação in-memory cobre dev/test/integração/demo. Em produção, contratos operacionais/configuracionais
do IdP são adaptados por `Storage.EntityFramework`, enquanto contratos ligados a contas de usuário são
implementados pelo módulo `UsersAccounts` ou por um pacote de integração próprio dele.

| Contrato (borda, no IdP) | Responsabilidade exata | NÃO faz |
|---|---|---|
| `ISubjectStore` | `FindBySubjectIdAsync(realm, sub) → Subject?`; `IsActiveAsync(realm, sub)`. Lookup mínimo do sujeito por `sub`. | Não devolve agregado rico; não resolve login; não modela email. |
| `ILocalUserAuthenticator` | `AuthenticateLocalAsync(realm, login, password) → AuthenticationResult`. **Autentica um usuário por login local**, resolve identificador (username/email/fictício), **verifica credencial** e **aplica lockout**. O resultado expõe o `Subject` que a borda usará protocolarmente. | Não inicia sessão; não escreve cookie; não decide prompt/consent. |
| `IProfileService` (mantém) | Orquestra claims do token/userinfo. Internamente chama o módulo via `IUserPropertyProvider` passando **só primitivos**. | Não conhece o modelo rico de conta. |
| `IUserPropertyProvider` | `GetClaimsAsync(realm, sub, identityScopeNames: string[], claimTypes: string[]) → UserClaimDto[]`. Projeção propriedades→claims (§7). | Não recebe `IdentityScope`/`RequestedResources` (só nomes/strings). |
| `IUserSessionStore` (puro) | `Create/FindById/RecordClient/End`. Persistência de sessão. | Sem `HttpContext`; sem "current". |
| `IUserSessionService` | Current session, `IsSessionValidAsync`, start/end, record client. | — |
| `ISubjectPrincipalFactory` | `Create(realm, subject, session) → ClaimsPrincipal` (claims obrigatórias: `sub/name/auth_time/sid/idp/amr`). | — |
| `LoginFlowService` | **Orquestra** o login: `ILocalUserAuthenticator` → `IUserSessionService.StartAsync` → `ISubjectPrincipalFactory` → cookie → **flow-result (enum)**. | Sem regra de conta; sem render. |

Tipos de borda (mínimos, no IdP): `Subject(SubjectId, DisplayName, IsActive)`,
`AuthenticationResult(Success | Reason{NotFound|Inactive|InvalidCredentials|Blocked} + Subject)`,
`UserClaimDto(Type, Value, ValueType?)`.

### 5.1 Fronteira autenticação (módulo) × fluxo (IdP) — regra explícita

| Faz parte de… | Responsabilidades |
|---|---|
| **Módulo de contas** (camada B) | resolver identificador de login (username/email/fictício); verificar senha/credencial; aplicar **lockout** por realm; responder **"conta ativa?"**; projetar **propriedades→claims**. |
| **IdP / borda** (camada A) | `prompt`/`max_age`/`UserSsoLifetime`; restrições de **client** e de **IdP**; **consent**; iniciar/validar **sessão** (`IUserSessionService`); montar **principal**; escrever **cookie**; **redirect**; responder **"sessão válida?"**; **decisão combinada** "pode emitir token?". |

Pontos-chave (resolvem pontos1 §2 e §3):
- **Sem `FindByLoginAsync` com política no store** — a resolução de login é do `ILocalUserAuthenticator` (módulo).
- **Sem campos de email na borda** — email é dado de conta (camada B), exposto só como **claim** via §7.
- **Sem tipos ricos do IdP no módulo** — o seam de claims passa **nomes de scope + claim types** (strings).

---

## 6. Módulo de contas — requisitos consolidados (de pontos2 §4)

`RoyalIdentity.UsersAccounts` (futuro; **não** se constrói agora — §10). Requisitos a especificar em
análise/ADR próprios:

- **Dados OIDC obrigatórios** + **usuários e configuração por realm**.
- **Email**: opcional, **múltiplo**, **fictício** (auto-gerado, configurável por realm, opcional).
- **ID externo** (identificador do usuário em sistema legado).
- **Propriedades dinâmicas por escopo** (§7) — o coração do modelo de perfil.
- **Credenciais** (senha + futuro externo/MFA/passwordless) e **lockout por realm**.
- **Casos de uso administrativos** (cadastro, manutenção, recuperação/troca de senha, ativar/desativar).
- **Eventos de domínio**, **Inbox/Outbox**, **replicação entre instâncias**.
- **Persistência própria** (EFCore/Postgre/Sqlite, mesmo padrão de provedores).
- **API e UI em projetos separados** (não dentro do módulo) — o módulo é **domínio + persistência**.

Estes requisitos **excedem** o redesign-todo "Users" (que é remediar a borda). Por isso o módulo é trabalho
**posterior**, com seu próprio documento e ADR.

---

## 7. Propriedades dinâmicas por escopo ↔ Identity Scopes (a costura elegante)

Ideia de pontos2 §4 (e a melhor parte da visão): o módulo define **escopos de propriedade**, cada um
**vinculado a um Identity Scope** do IdP; cada escopo define N **propriedades**:

```
PropertyScope (↔ IdentityScope.Name)
  └─ Property { Name(claimType), Value, ValueType, DisplayName, Help, IsSensitive, Validation[...] }
```

Como encaixa no IdP **sem acoplar**: o `IProfileService` **permanece no core** (orquestra) e chama o módulo
via `IUserPropertyProvider.GetClaimsAsync(realm, sub, identityScopeNames, claimTypes)` passando **apenas
primitivos** (nomes de identity scopes e claim types). O módulo:
1. recebe os **nomes** dos identity scopes solicitados (não `IdentityScope`/`RequestedResources`);
2. seleciona os property scopes correspondentes;
3. projeta as propriedades em `UserClaimDto` (aplicando sensibilidade/validação);
4. devolve à borda, que converte para `Claim` e monta token/userinfo.

> **Regra de desacoplamento (pontos1 §7 / comentário do autor):** o módulo **nunca** vê tipos ricos do IdP
> (`IdentityScope`, `RequestedResources`) — só strings/DTOs. Isso mantém `UsersAccounts` independente do core.

Ganhos: substitui o *bag* plano de claims **e** a montagem ad-hoc do `DefaultProfileService` por um modelo
**tipado, validado, configurável por realm e ancorado nos Identity Scopes** (que acabaram de ser
redesenhados no plano de resources). Resolve pontos1 §3 de forma estrutural: email/propriedades vivem aqui.

> Esta é a maior ampliação de visão do pontos2 e merece destaque: o perfil do usuário passa a ser
> **dirigido por escopo**, fechando o ciclo com o modelo de resources/scopes do IdP.

---

## 8. Resposta aos 7 pontos do pontos1

| # | Ponto | Resolução nesta arquitetura |
|---|---|---|
| 1 | Dedup de clients (`HashSet<class>` dedup por referência) | **Sessão (C):** usar `record UserSessionClient` com igualdade por `ClientId`, **ou** `Dictionary<string,...>`, **ou** `HashSet<string>` na 1ª fase. Nunca `IList<string>` sem dedup. |
| 2 | `FindByLoginAsync` concentra policy no store | **Resolvido por camada:** store puro busca por índices; **resolução de login é do módulo** (`ILocalUserAuthenticator`/resolver). Ver §5. |
| 3 | Campos de email/login ausentes | **Resolvido por camada:** email/identificadores são **dados de conta** (camada B), expostos à borda como claims via propriedades-por-escopo (§7). A borda não modela email. |
| 4 | Imutabilidade do `SubjectId` verificável | **Regra + teste:** o store (qualquer impl.) **rejeita** alterar `SubjectId` de conta existente; teste de "trocar username não muda `sub`" + teste que rejeita troca de `SubjectId`. |
| 5 | Fronteira da UI ainda vaza | **Adotar `LoginFlowService`/`LoginInteractionService`** (borda) que orquestra autenticação→sessão→principal→cookie→**flow-result**; `LoginPageService` vira **adaptador de tela** (view model + traduz resultado). Confirma e fortalece a S1 do final. |
| 6 | Semântica de `ExpiresAt` indefinida | **Marcar reservado nesta fase** (`UserSession.ExpiresAt`, `PasswordCredential.ExpiresAt`) — sem comportamento; definir no plano a interação com cookie lifetime/`UserSsoLifetime`/expiração de senha/lockout. Evitar campo "ativo" sem regra. |
| 7 | Referenciar con1 ou declarar consenso | **Declarado:** [an-user-con2.md](an-user-con2.md) é o consenso base; `an-user-con1.md` existe mas não foi usado. (Banner adicionado ao `an-users-final`.) |

Observação: pontos1 §5 é o mais importante dos sete e converge com pontos2 — a borda precisa de um
**serviço de fluxo** para a UI ser cola de verdade.

---

## 9. Governança revisada (ordem dos ADRs)

pontos2 §5: **antes** do ADR das decisões do `an-users-final`, é preciso um ADR da **arquitetura modular**.
Cadeia **simplificada** (decisão da rodada — antes eram 3, agora 2 + futura):

1. **ADR-013 — Arquitetura modular & fronteiras:** IdP como biblioteca de borda + *facades* (contratos no
   core); `RoyalIdentity.Data.Configuration`/`.Operational` (dados puros, sem dependência do core);
   `Storage.EntityFramework` (+ `.Postgre`/`.Sqlite`) e `Storage.Caching`; in-memory como fake/referência;
   módulos de domínio `UsersAccounts` e `KMS` (com API/UI em projetos separados). *Storages são facades.*
2. **ADR-014 — Redesign da borda Users + sessão:** composição sobre herança (refina **ADR-005**),
   `SubjectId` imutável, contratos de borda (§5, incl. `ISubjectStore`), `IProfileService` como costura de
   claims, modelo/serviço de sessão (camada C, semântica de "sessão válida", dedup de clients), fluxo de
   login (`LoginFlowService`), remoção de `IdentityUser`/`UserDetails`/provider quebrado.
3. **(Futura) ADR do módulo `UsersAccounts`** — domínio rico, propriedades-por-escopo, emails, ID externo,
   eventos, inbox/outbox, replicação, casos de uso. Acompanha o desenvolvimento do módulo.

> A ADR-005 é **refinada** (não negada): gestão própria de usuários por realm permanece — agora realizada
> pelo **módulo** `UsersAccounts` + composição. Numeração 013/014 a confirmar na abertura das ADRs.

---

## 10. Reescopo do trabalho: imediato × futuro

**Imediato** (atende o redesign-todo "Users" sem construir o módulo):
- Refatorar a **borda + sessão** do IdP **atrás das facades**, com **modelo de borda enxuto**.
- `SubjectId` imutável; semântica única de "ativo"; **sessão deixa de ser efeito colateral**; `LoginFlowService`;
  `ISubjectPrincipalFactory`; remover o revalidating provider quebrado; dedup de clients da sessão.
- **In-memory permanece** como fake/referência (dev/test/integração/demo), com índices/seed determinístico.
- **Projetar as facades** (`ILocalUserAuthenticator`, `ISubjectStore`, `IUserSessionStore`, `IProfileService`,
  `IUserPropertyProvider`) de modo que `UsersAccounts` e sua persistência própria **encaixem depois** sem
  reescrever a borda.

**Futuro** (módulos, com análises/ADRs próprios):
- `RoyalIdentity.UsersAccounts` (domínio rico §6/§7 + persistência + casos de uso). API/UI em projetos separados.
- `RoyalIdentity.Data.Configuration`/`.Operational`, `Storage.EntityFramework` (+ `.Postgre`/`.Sqlite`), `Storage.Caching`.
- `RoyalIdentity.KMS`. API/UI em projetos separados.

Isto reconcilia **anti-YAGNI** (não construir o módulo agora) com a **visão modular** (desenhar as costuras
agora) — exatamente o que pontos2 pede ao dizer que a in-memory não é o destino e que o real são módulos
atrás de facades.

---

## 11. Decisões (rodada com o autor — fechadas)

| # | Decisão | **Resolução** |
|---|---|---|
| Q1 | Nomes dos projetos de dados | **`RoyalIdentity.Data.Configuration`** × **`.Operational`** (dados puros); provedores sob `Storage.EntityFramework.*`. ("Entities"/nomes soltos descartados.) |
| Q2 | Onde fica a sessão | **Contrato/serviço no IdP**; **persistência** em `Data.Operational`. |
| Q3 | Escopo imediato | **Borda + sessão agora**; módulo de contas depois. |
| Q4 | Borda armazena entidade? | **Não** — 100% *facade*→módulo; borda usa `Subject` + `IProfileService`. |
| Q5 | Auth/credencial/lockout | **No módulo** (dados+verify+policy), exposto à borda por `ILocalUserAuthenticator`; **IdP orquestra** (fronteira em §5.1). |
| Q6 | Propriedades por escopo | Contrato (`IUserPropertyProvider`) **já preparado** com primitivos; riqueza no módulo. |
| Q7 | ADRs | **Simplificado:** ADR-013 (arquitetura) + ADR-014 (borda+sessão) + futura (módulo). |
| — | Dependência de contratos | Contratos no **core**; `Data.*` pode usar EFCore, mas não depende do core; só `Storage.EntityFramework` adapta `Data.*` às facades do IdP. `Abstractions` adiado. |
| — | Escopo do `Storage.EntityFramework` | Adapta apenas dados do IdP (`Data.Configuration`/`Data.Operational`). `UsersAccounts` tem persistência própria e não é adaptado por esse projeto. |
| — | Edge contract | `IUserStore` (borda) renomeado para **`ISubjectStore`** (evita colisão). |
| — | In-memory | **Fake/referência** (dev/test/integração/demo), não "só teste". |

---

## 12. Conclusão

A crítica de pontos2 procede: o `an-users-final` resolveu a **dor da borda**, mas misturou nela um
**módulo de contas** e tratou a **in-memory** como destino. A clarificação é separar **três camadas** —
**borda** (IdP), **contas** (`RoyalIdentity.UsersAccounts`, módulo) e **sessão** (IdP operacional) — dentro
de uma **arquitetura modular** onde os *storages* são **facades** e os módulos EFCore
(`Data.Configuration`/`.Operational`, Postgre/Sqlite) são a persistência real; a in-memory permanece como
**fake/referência** (dev/test/integração/demo). O **modelo rico** do `an-users-final` migra para o
**módulo de contas** (enriquecido com emails opcionais/múltiplos/fictícios, ID externo e **propriedades
dinâmicas por escopo ancoradas nos Identity Scopes**); a **biblioteca IdP** fica com **facades + modelo de
borda enxuto (§5) + sessão + fluxo de login**. O **trabalho imediato** é a refatoração de **borda+sessão atrás
das facades** (sem construir o módulo), desenhando as costuras para os módulos futuros. A **governança**:
**ADR-013** (arquitetura modular & fronteiras) → **ADR-014** (borda + sessão) → **ADR futura** (módulo de
contas). Os 7 pontos do pontos1 estão endereçados (§8), a maioria **estruturalmente** pela separação de
camadas; as decisões de arquitetura (§11) foram **fechadas** com o autor nesta rodada.
