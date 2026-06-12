# Plan: Resources / Scopes Redesign

## Status: IN_PROGRESS

## Progresso

`█████---` **63%** - 5 de 8 fases concluidas

---

## Contexto

Este plano nasce do item **Resources** do `redesign-todo.md`.

O modelo de scopes/resources herdado do IdentityServer4 ficou confuso e meio-refatorado:

- Existem **4 tipos** (`ScopeType`: `Identity`, `ResourceServer`, `ApiResource`, `ApiScope`) numa hierarquia de **3 niveis para API**: `ResourceServer` -> `ApiResource` -> `ApiScope` (e o `ResourceServer` ainda agrega `IdentityScope`s).
- O cluster esta dividido em **dois namespaces**: `RoyalIdentity.Models.Resources` (`ScopeBase`, `ScopeType`, `RequestedScopes`) e `RoyalIdentity.Models.Scopes` (`IdentityScope`, `ApiScope`, `ApiResource`, `ResourceServer`, `AllScopes`, `ScopeVisibility`).
- `IdentityScope` ja foi renomeado (era `IdentityResource`), mas o objeto `RequestedScopes` ainda usa membros antigos (`IdentityResources`, `ApiResources`, `ApiScopes`) e carrega as 3 camadas ao mesmo tempo; `ToScopeClaims()` percorre 5 caminhos.
- Dividas marcadas: `ApiResource.ApiSecrets` e `ApiResource.AllowedAccessTokenSigningAlgorithms` sao `[Obsolete]`; `Client.AllowedScopes` e `Client.AllowOfflineAccess` sao `[Redesign]`.

---

## Objetivo

Colapsar o modelo para **3 conceitos** claros e alinhados ao OAuth2, com semantica explicita de token/aud/autorizacao:

1. **IdentityScope** — claims de identidade enviados ao cliente (openid/profile/email-style). Avulso, do realm.
2. **ResourceServer** — um servico que expoe recursos protegidos (uma WebApi). Dono de `Scope`s, de `Audience` e dos secrets.
3. **Scope** — uma operacao/funcionalidade exposta por um `ResourceServer` (read/write/grupo de endpoints).

E ainda: refatorar `RequestedScopes` -> `RequestedResources`, consolidar o namespace, redesenhar a permissao do client (`AllowedResources`), adicionar Resource Indicators/Protected Resource Metadata (ADR-012) e o consentimento agrupado por ResourceServer.

---

## Fora de Escopo

- **Introspection endpoint** e **reference tokens** continuam no backlog (`.ai/backlogs/backlog-001.md`). Este plano apenas **modela** os secrets no `ResourceServer`; nao implementa autenticacao de ResourceServer no introspect (sera um `secret evaluator` de ResourceServer no plano futuro).
- Autorizacao fina (UMA / permissions / policies estilo Keycloak Authorization Services) — fora de escopo.
- **SDK/middleware para o proprio Resource Server hospedar o metadata RFC 9728** (`.well-known/oauth-protected-resource`) automaticamente — fora de escopo (ADR-012); o RoyalIdentity apenas **gera** o documento.
- Persistencia em banco (continua in-memory).

---

## Estado Atual Auditado

Modelo atual (`RoyalIdentity/Models/Scopes/` + namespace `Models.Resources`):

| Tipo | Namespace | Papel atual |
|---|---|---|
| `ScopeBase` | `Models.Resources` | base: Type, Visibility, Enabled, Name, DisplayName, Description, ShowInDiscoveryDocument |
| `ScopeType` (enum) | `Models.Resources` | Identity, ResourceServer, ApiResource, ApiScope |
| `ScopeVisibility` (enum) | `Models.Scopes` | Public, Internal |
| `IdentityScope` | `Models.Scopes` | UserClaims, Required, Emphasize |
| `ApiScope` | `Models.Scopes` | Required, Emphasize |
| `ApiResource` | `Models.Scopes` | `Scopes` (ApiScope[]), `[Obsolete]` ApiSecrets, `[Obsolete]` AllowedAccessTokenSigningAlgorithms, AllowScopeRequests |
| `ResourceServer` | `Models.Scopes` | `Resources` (ApiResource[]) + `IdentityScopes` (IdentityScope[]) + AllowScopeRequests |
| `RequestedScopes` | `Models.Resources` | objeto por-request; membros antigos; `ToScopeClaims()`/`GetRequestedIdentityClaimsTypes()`/`IntersectConsentScopes()`/`CopyTo()` |
| `AllScopes` | `Models.Scopes` | snapshot dos 4 tipos |

Fluxo de runtime:

```
scope (string) na request
  -> IResourceStore.FindResourcesByScopeAsync  (resolve nome -> 1 dos 4 tipos; MemoryStorage tem 4 dicionarios)
  -> RequestedScopes (no contexto, via IWithResources)
  -> ResourcesDecorator / ClientResourceDecorator   (valida + filtra por client)
  -> AuthorizationResourcesValidator / ResourcesValidator
  -> DefaultConsentService / ConsentPageService / ConsentViewModel
  -> DefaultTokenFactory / DefaultTokenClaimsService  (claims scope + identity claims + aud)
  -> DiscoveryHandler (scopes_supported) / UserInfoHandler
  -> persistido em AuthorizationCode / RefreshToken
```

Consumidores nucleo a tocar (~15): `ResourcesDecorator`, `ClientResourceDecorator`, `AuthorizationResourcesValidator`, `ResourcesValidator`, `DefaultConsentService`, `ConsentPageService`, `ConsentViewModel`/`ConsentInputModel`/`ConsentPage.razor`, `DefaultTokenFactory`, `DefaultTokenClaimsService`, `DiscoveryHandler`, `UserInfoHandler`, `ResourceStore` (+ `MemoryStorage`/`RealmMemoryStore` seeding), `Client`, `ResourcesExtensions`, e os contratos `IResourceStore`/`IWithResources` + modelos `AuthorizationCode`/`RefreshToken`.

---

## Modelo Alvo (decisoes fechadas)

**Conceitos (3):**

- **`IdentityScope`** (mantido): claims de identidade; avulso, pertence ao realm. `UserClaims`, `Required`, `Emphasize`.
- **`ResourceServer`** (= renomeado do antigo `ApiResource`): dono de `Scope`s e de `ProtectedResource`s. Campos: `Scopes` (`Scope[]`), `Audience` (string; default = `Name`, usado no caminho por `scope`), `ProtectedResources` (`ProtectedResource[]`, ADR-012), `Secrets` (para introspection futuro), `AllowedAccessTokenSigningAlgorithms`, `AllowScopeRequests`, `Visibility`, `Enabled`.
- **`Scope`** (= renomeado do antigo `ApiScope`): operacao de um `ResourceServer`. `Name` **unico/global no realm**, `Required`, `Emphasize`. **So existe dentro de um ResourceServer.**

**`ProtectedResource`** (ADR-012): protected resource RFC 8707/9728 pertencente a um `ResourceServer`. Campos: `ResourceUri` (URI absoluta, sem fragmento, unica por realm), `ShowInDiscoveryDocument`, `DisplayName`, `DocumentationUri`, `PolicyUri`, `TosUri`. **Sem `Enabled` proprio** — a disponibilidade deriva do `ResourceServer.Enabled` pai. O parametro OAuth `resource` casa contra `ProtectedResource.ResourceUri`.

**Removidos:** o `ResourceServer`-grupo atual (que agregava `ApiResource`s) e o nivel intermediario `ApiResource`. `ScopeType` passa a ter 3 valores: `Identity`, `ResourceServer`, `Scope`. `ResourceServer.IdentityScopes` **removido** (IdentityScope e do realm, nao do RS).

**`RequestedResources`** (= renomeado de `RequestedScopes`, namespace unico `Models.Scopes`): coleciona `IdentityScopes`, `ResourceServers`, `Scopes` pedidos + `OfflineAccess`, `MissingScopes`, `DisabledScopes`. A Fase 6 (ADR-012) estende com os `ProtectedResource`s pedidos (via `resource`). A Fase 5 **colapsa** `MissingScopes`/`DisabledScopes` num unico bucket de invalidos (apontamento 3.1).

**Semantica (decisao #2):**

- **`scope` no token** = apenas os `Scope` (e `IdentityScope`) explicitamente pedidos + `offline_access`. Pedir o nome de um ResourceServer **nao** despeja todos os scopes no token.
- **Autorizacao do client** = ter um `ResourceServer` em `AllowedResourceServers` ⇒ o client pode pedir **qualquer Scope** daquele RS.
- **`aud` por `scope`** = uniao de `ResourceServer.Audience ?? ResourceServer.Name` dos ResourceServers donos dos Scopes pedidos, quando nao ha `resource` explicito para aquele RS.
- **`aud` por `resource`** (ADR-012) = `ProtectedResource.ResourceUri` dos resources pedidos. Quando `resource` e informado para um RS, esse audience explicito domina e o `Audience` legado do RS nao e adicionado para o mesmo RS.
- **`scope + resource`** e aceito; com `resource` presente, cada scope de API e validado pelo seu RS dono: se o RS dono **possui** `ProtectedResource`s, ao menos um `ProtectedResource` dele deve estar entre os `resource`s pedidos (senao `invalid_target`); se o RS dono **nao possui** `ProtectedResource`s, o scope e valido independentemente (flui so pelo eixo de scope, `aud = Audience ?? Name`). Ex.: `scope` de A (sem resources) + `resource` de B e valido.

**Permissao do client (decisao #4 + tipo de client — ver ADR-011)** — substituir `AllowedScopes`(string-misto) e `AllowOfflineAccess`:

- `ClientType` (enum `Public`/`Confidential`, RFC 6749) — propriedade do client. Hoje o tipo e implicito (`RequireClientSecret`); passa a ser explicito. `RequireClientSecret`/secrets seguem como comportamento de runtime; a consistencia entre eles e validada no **CRUD de client futuro**.
- `AllowedIdentityScopes: HashSet<string>`
- `AllowedResourceServers: HashSet<string>` (libera todos os Scopes do RS)
- `AllowedScopes: HashSet<string>` (Scopes individuais)
- `AllowAllResourceServers: bool` (Full Scope Allowed, estilo Keycloak) — opt-in; libera pedir qualquer Scope de qualquer ResourceServer do realm. **So autorizacao**: o token continua levando apenas os scopes pedidos (decisao #2). Valido apenas para `Confidential` (guard enforced no CRUD futuro; por ora invariante documentado).
- `AllowOfflineAccess: bool` (flag)

Regra: um `Scope` pedido e permitido se `AllowAllResourceServers` **ou** esta em `AllowedScopes` **ou** seu ResourceServer dono esta em `AllowedResourceServers`. `IdentityScope` permitido se em `AllowedIdentityScopes`. `offline_access` se `AllowOfflineAccess`.

**Algoritmo de assinatura do access token (decisao #a) — Realm ordena/filtra, RS/Client so filtram (hierarquico):**

- O **Realm** sempre define a **ordem** e a disponibilidade (chaves do realm, na ordem do realm).
- **RS** e **Client** sao apenas **filtros restritivos**, aplicados de forma **hierarquica** (nunca juntos):
  1. Se algum ResourceServer pedido declara `AllowedAccessTokenSigningAlgorithms` -> filtro = **RS** (multi-RS: **intersecao**; APIs incompativeis -> erro).
  2. Senao, se o Client declara `AllowedAccessTokenSigningAlgorithms` -> filtro = **Client**.
  3. Senao -> sem restricao extra (so o Realm).
- Escolhe-se o primeiro algoritmo, **na ordem do realm**, que satisfaz o filtro. Como RS e Client nunca se acumulam, restricoes de tipos diferentes nunca produzem conjunto vazio.
- O **id token** usa o mesmo modelo com o filtro do Client (`AllowedIdentityTokenSigningAlgorithms`), sem dimensao de RS.

**Secrets (decisao #7):** secrets ficam no `ResourceServer` (modelo do IdentityServer) para uso futuro no introspection; `Client.ClientSecrets` permanece para client_credentials. `AllowedAccessTokenSigningAlgorithms` fica no ResourceServer (e tambem no Client, conforme a cadeia acima). Os `[Obsolete]` atuais sao **revertidos** (nao migram para o client).

---

## Ordem de Execucao e Dependencias

1. **Fase 1 (auditoria & decisao)** — pre-requisito de todas.
2. **Fase 2 (modelo de dominio)** — base das demais.
3. **Fase 3 (store)** e **Fase 4 (client AllowedResources)** — dependem da Fase 2; independentes entre si.
4. **Fase 5 (pipeline/token/aud)** — depende de 2, 3, 4.
5. **Fase 6 (Resource Indicators e Protected Resource Metadata)** — depende de 2, 3, 4 e 5.
6. **Fase 7 (consentimento)** — depende de 2, 5 e 6.
7. **Fase 8 (testes)** — depende de tudo.

---

## Fase 1: Auditoria e Decisao

> Cada passo **registra seu resultado** em "Resultado da Fase 1" assim que concluido — nao acumular tudo para o fim.

Passos:

1. Mapear como o `aud` (audience) e derivado hoje no `DefaultTokenFactory`/`DefaultTokenClaimsService` e onde `AccessToken.Audiences` e preenchido. -> registrar.
2. Mapear o consumo de `RequestedScopes` em consent (`DefaultConsentService`, `ConsentPageService`, `ConsentViewModel`) e em claims (`ToScopeClaims`, `GetRequestedIdentityClaimsTypes`). -> registrar.
3. Mapear a persistencia dos scopes pedidos em `AuthorizationCode`/`RefreshToken` e o re-load no refresh. -> registrar.
4. Mapear o seeding de scopes (`RealmMemoryStore`) e todos os testes que criam scopes/resources. -> registrar.
5. Regra **multi-ResourceServer** para algoritmo de assinatura — **decidida:** intersectar entre os ResourceServers que declaram algoritmos (todas as APIs participantes devem aceitar o escolhido); desempate pela ordem do primeiro ResourceServer pedido. -> registrar.
6. Consolidar o contrato final (modelo, `aud`, signing-alg, consent) a partir dos registros dos passos anteriores, antes de codar.

Criterio de aceite: call sites de `IdentityResource/ApiScope/ApiResource/ResourceServer/RequestedScopes` classificados (manter/renomear/remover); resultados dos passos 1-5 registrados em "Resultado da Fase 1"; contrato final consolidado.

### Resultado da Fase 1

**Status:** concluida (passos 1-6).

#### Passo 1 — Derivacao do `aud` (audience)

**Onde `AccessToken.Audiences` e preenchido:** `DefaultTokenFactory.CreateAccessTokenAsync`:
- `aud` do access token = **nomes dos `ApiResource` pedidos**: `request.Resources.ApiResources.Select(x => x.Name).Distinct()` -> `token.Audiences` (l.84-88).
- Mais o **`client.Id`** quando o request e OpenID (`request.Resources.IsOpenId`, l.91-94).
- O id token recebe `aud = client.Id` apenas quando OpenID (`CreateIdentityTokenAsync`, l.190-193); **nao** recebe os ApiResources.

**Como `Audiences` vira o claim `aud`:** `DefaultJwtFactory.CreateJwtPayload` (l.119-122) emite um claim `aud` por item de `token.Audiences` (multiplos viram array JSON). `Audiences` e herdado de `TokenBase` e copiado em `AccessToken.Renew`.

**`DefaultTokenClaimsService`:** **nao** lida com `aud` (so monta claims de scope/identity); o `aud` esta inteiramente no `DefaultTokenFactory`.

**Validacao (`DefaultTokenValidator`):** validacao de audience e **opcional** — `ValidateJwtAccessTokenAsync(realm, jwt, expectedScope, audience)` so valida `aud` quando um `audience` e passado (`parameters.ValidAudience`); senao `ValidateAudience = false` e apenas confere o `typ` de access token (l.44-49). Em um caminho usa `jwt.Audiences.FirstOrDefault()` para obter o clientId (l.195).

**Implicacoes para o redesenho:**
- Hoje o `aud` sai **somente** do `ApiResource` (nivel do meio). Nao usa o `ResourceServer`-grupo nem `ApiScope` avulso — pedir so um `ApiScope` **nao gera `aud`**.
- Alvo: no caminho por `scope`, `aud` = `ResourceServer.Audience ?? ResourceServer.Name` dos ResourceServers donos dos Scopes pedidos. ResourceServer nao e pedido diretamente por `scope`; o caminho explicito por audience/resource fica na ADR-012/Fase 6.
- Manter o comportamento `IsOpenId -> client.Id` no `aud`.
- **Nota cruzada (signing-alg, passo 5):** `AllowedSigningAlgorithms` tambem sai hoje **so** de `request.Resources.ApiResources.FindMatchingSigningAlgorithms()` (access **e** id token, l.80 e l.202). A cadeia RS->Client->Realm decidida e uma **mudanca de comportamento** aqui, tratada na Fase 5.

#### Passo 2 — Consumo de `RequestedScopes` em consent e claims

**Claims (`DefaultTokenClaimsService`):**
- Access token: `resources.ToScopeClaims()` (claim `scope`, l.98) + `resources.GetRequestedIdentityClaimsTypes()` (tipos de claim de identidade p/ o profile, l.110).
- Id token: `resources.RequestedIdentityClaimTypes()` (extension; **so** `IdentityResources.UserClaims`, filtrado, l.47).
- **Inconsistencia:** dois metodos de "tipos de claim de identidade" — `GetRequestedIdentityClaimsTypes()` (RequestedScopes; inclui `ResourceServers.IdentityScopes`) no access token, e `RequestedIdentityClaimTypes()` (ResourcesExtensions; so IdentityResources) no id token. Com a decisao #5 (ResourceServer nao possui IdentityScopes) os dois convergem para **IdentityScopes apenas** -> unificar num metodo so.
- `ToScopeClaims()` percorre **5 caminhos** hoje; no alvo colapsa para **nomes dos Scopes pedidos + IdentityScopes + offline_access** (decisao #2).

**Consent (`DefaultConsentService` + `ConsentPageService`/`ConsentViewModel`):**
- A tela e a comparacao operam **somente** sobre `IdentityResources` + `ApiScope` avulso. `ConsentPageService.BuildConsentViewModel` monta o ViewModel de `context.Resources.IdentityResources` + `context.Resources.ApiScopes` (l.90-91); `ProcessConsentAsync` valida/coleta so esses dois grupos (l.50-74).
- A comparacao usa `resources.IntersectConsentScopes(...)` (l.144 do `DefaultConsentService`), que considera `IdentityResources.Name ∪ ApiScopes.Name`.
- **Gap atual:** `ResourceServer` e `ApiResource` **nao aparecem no consentimento** — pedir scopes de um ApiResource hoje nao gera tela de consent para eles.

**Implicacoes para o redesenho:**
- A decisao #6 (consent agrupado por ResourceServer mostrando os Scopes pedidos) **nao e so cosmetica**: o consent precisa **passar a consentir** os Scopes de ResourceServer (hoje ignorados). Mudam `ConsentViewModel` (grupo por ResourceServer + IdentityScopes), `ConsentPageService` (build/process) e o matching `IntersectConsentScopes` (considerar os Scopes pedidos, nao so ApiScopes).
- Unificar os dois caminhos de tipos de claim de identidade.

**Arquivos:** `DefaultTokenClaimsService` (l.98, 110, 47), `RequestedScopes` (`ToScopeClaims`/`GetRequestedIdentityClaimsTypes`/`IntersectConsentScopes`), `ResourcesExtensions.RequestedIdentityClaimTypes` (l.51), `DefaultConsentService` (l.144), `ConsentPageService` (l.50-91), `ConsentViewModel`/`ConsentInputModel`.

#### Passo 3 — Persistencia dos scopes pedidos e re-load no refresh

**AuthorizationCode:** guarda o **objeto `RequestedScopes` completo** (`AuthorizationCode.Scopes`), criado em `DefaultCodeFactory` a partir de `context.Scopes`. Na troca do code (`AuthorizationCodeHandler`) o `code.Scopes` e **reusado direto** (`Resources = code.Scopes`, `IsOpenId`, `OfflineAccess`) — nao re-resolve do store.

**RefreshToken:** guarda apenas **nomes de scope** em `RefreshToken.RequestedScopes` (`ICollection<string>`), populado de `request.AccessToken.Scopes.ToList()` (`DefaultTokenFactory`, l.251). `TokenBase.Scopes` = valores dos claims `scope` do token; `Audiences` e um `HashSet<string>` proprio (copiado em `AccessToken.Renew`).

**Re-load no refresh (`RefreshTokenHandler`):** **nao** usa `refreshToken.RequestedScopes`; usa os **scope claims do access token armazenado** (`accessToken.Scopes`) e re-resolve via `resourceStore.FindResourcesByScopeAsync(scopes, onlyEnabled: true)` (l.72-73 no caminho `UpdateAccessTokenClaimsOnRefresh`; l.150-151 para o id token). No caminho padrao, `accessToken.Renew` copia claims (scopes) e `Audiences` do token antigo.

**Observacoes para o redesenho:**
- `RefreshToken.RequestedScopes` (collection de string) parece **armazenado mas nao lido** no refresh — candidato a remocao (o refresh re-resolve a partir dos scope claims do access token). Confirmar e limpar.
- `FindResourcesByScopeAsync(..., onlyEnabled: true)` no refresh **descarta scopes desabilitados/removidos** entre emissao e refresh — comportamento a preservar no novo modelo.
- A re-resolucao por nome e o ponto onde o novo modelo entra: nomes -> Scopes (com RS dono -> `aud`). `AuthorizationCode.Scopes` muda de tipo com o rename `RequestedScopes` -> `RequestedResources`.

**Arquivos:** `AuthorizationCode.Scopes`, `DefaultCodeFactory` (l.45), `AuthorizationCodeHandler` (l.50, 61, 77, 99), `RefreshToken.RequestedScopes`, `DefaultTokenFactory.CreateRefreshTokenAsync` (l.251), `RefreshTokenHandler` (l.72-73, 92, 150-151), `TokenBase.Scopes/Audiences`.

#### Passo 4 — Seeding e testes

**Seeding (`RealmMemoryStore`):** 4 dicionarios — `IdentityResources` (IdentityScope), `ResourceServers`, `ApiResources`, `ApiScopes`. Seed:
- IdentityScopes: `openid`, `profile`, `email`, `address`, `phone`.
- 1 ResourceServer `apiserver` -> Resources [ ApiResource (ctor "api1", mas `Name` sobrescrito para "api") -> Scopes [ ApiScope "api" ] ].
- ApiScopes avulsos: `api:read`, `api:write`.
- `InitializeResources()` **achata** o aninhado: copia o 1o ApiResource do 1o ResourceServer para `ApiResources["api"]` e seus scopes para `ApiScopes["api"]` — o scope "api" existe nas duas formas.

**Como resolvem hoje (`FindResourcesByScopeAsync`):** "apiserver" -> ResourceServer; "api" -> **ApiResource** (dict flat); "api:read"/"api:write" -> **ApiScope**; openid/profile/... -> IdentityScope. (Confirma o Passo 1: so "api" gera `aud`.)

**Testes:** nenhum teste cria ResourceServer/ApiResource/IdentityScope — todos usam os scopes **seedados** por nome. Impacto:
- `Client.AllowedScopes = { ... }` (string) em ~10 arquivos (RealmIsolation, Phase4/5, CodeToken, RefreshToken, ClientToken, EndSession, CodeAuthorize, EventIsolation) -> migrar para o novo modelo de client (Fase 4).
- `FindResourcesByScopeAsync([...nomes...])` (CodeToken, RealmIsolation, HostEndpoints) -> continua valido.

**Implicacoes:** store cai de 4 dicts para **2** (`ResourceServers` com `Scopes` + `IdentityScopes`); remover `InitializeResources()`. Re-modelar `api`/`api:read`/`api:write` como **Scopes de um ResourceServer** com `Audience` — hoje "api:read"/"api:write" sao ApiScope avulso (sem aud); no novo modelo **passam a contribuir `aud`** (mudanca na demo data).

**Arquivos:** `RealmMemoryStore` (l.36-45, 61-181), `MemoryStorage.Storage.cs` `GetResourceStore` (l.75-85), `Tests.Integration/*`, `Tests.Host/HostEndpoints.cs`.

#### Passo 5 — Algoritmo de assinatura (multi-ResourceServer)

**Hoje:** `AllowedSigningAlgorithms` sai **so** de `request.Resources.ApiResources.FindMatchingSigningAlgorithms()` (access e id token). Ja intersecta entre multiplos ApiResources (lanca se incompativel); sem fallback para Client/Realm.

**Decidido (precedencia, com ordem):**
1. ResourceServers pedidos que declaram algoritmos -> intersecao das listas (todas as APIs participantes devem aceitar); ordem = a do **primeiro ResourceServer pedido**.
2. senao `Client.AllowedAccessTokenSigningAlgorithms` -> ordem do Client.
3. senao algoritmos disponiveis no Realm -> ordem do Realm.

Pega o primeiro com chave disponivel. **Mudanca:** adiciona niveis Client e Realm (hoje so ApiResource) + ordem; `FindMatchingSigningAlgorithms` passa a operar sobre ResourceServers + a cadeia. Implementado na Fase 5.

#### Passo 6 — Contrato consolidado

**Classificacao dos tipos:**

| Atual | Acao | Alvo |
|---|---|---|
| `IdentityScope` | manter | `IdentityScope` (avulso, do realm) |
| `ApiScope` | renomear | `Scope` (pertence a um ResourceServer) |
| `ApiResource` | renomear | `ResourceServer` (dono de Scopes, Audience, secrets, signing-alg) |
| `ResourceServer` (grupo de ApiResources) | **remover** | — |
| `ResourceServer.IdentityScopes` | **remover** | IdentityScope e do realm |
| `RequestedScopes` | renomear | `RequestedResources` (IdentityScopes/ResourceServers/Scopes) |
| `ApiResource.ApiSecrets` / `AllowedAccessTokenSigningAlgorithms` | manter no RS (des-obsoletar) | `ResourceServer.Secrets` / `AllowedAccessTokenSigningAlgorithms` |
| `Client.AllowedScopes` (string) / `AllowOfflineAccess` | substituir | `AllowedIdentityScopes` + `AllowedResourceServers` + `AllowedScopes` + `AllowAllResourceServers` + flag `AllowOfflineAccess` + `ClientType` (ADR-011) |
| namespace `Models.Resources` | remover | consolidar em `Models.Scopes` |

**Contrato (sintese):**
- `aud` sem `resource` explicito = `ResourceServer.Audience ?? ResourceServer.Name` dos RS donos dos Scopes pedidos + `client.Id` quando OpenID. O caminho explicito por `resource` e definido na ADR-012/Fase 6.
- `scope` no token = apenas Scopes pedidos + IdentityScopes + offline_access.
- Consent: passa a consentir Scopes de ResourceServer (hoje ignorados), agrupado por RS; ajustar `IntersectConsentScopes` e `ConsentViewModel`.
- Unificar os dois metodos de "identity claim types".
- Refresh re-resolve por nome (`FindResourcesByScopeAsync(onlyEnabled:true)`); `RefreshToken.RequestedScopes` (string) provavelmente removivel.
- Signing-alg: cadeia RS -> Client -> Realm com ordem.
- Store: 2 dicts; remover `InitializeResources()`.

**Fase 1 concluida.**

---

## Fase 2: Modelo de Dominio

Arquivos provaveis: `RoyalIdentity/Models/Scopes/*` (consolidar namespace), `ScopeType.cs`, `ScopeBase.cs`, `IdentityScope.cs`, novos `ResourceServer.cs`/`Scope.cs`, `RequestedResources.cs`, `AllScopes.cs`.

Passos:

1. `ScopeType` -> 3 valores (`Identity`, `ResourceServer`, `Scope`); consolidar tudo em `RoyalIdentity.Models.Scopes` (remover uso de `Models.Resources`).
2. `Scope` (antigo `ApiScope`) com dono `ResourceServer`; `ResourceServer` (antigo `ApiResource`) com `Scopes`, `Audience`, `Secrets`, `AllowedAccessTokenSigningAlgorithms`, `AllowScopeRequests`.
3. Remover o `ResourceServer`-grupo antigo, o nivel `ApiResource` e `ResourceServer.IdentityScopes`.
4. `RequestedScopes` -> `RequestedResources` (membros `IdentityScopes`/`ResourceServers`/`Scopes`); reescrever `ToScopeClaims()` (apenas scopes pedidos + identity + offline) e `GetRequestedIdentityClaimsTypes()`.
5. Reverter os `[Obsolete]` de secrets/signing-alg (agora oficiais no `ResourceServer`).

Criterio de aceite: solucao compila; o modelo tem so 3 tipos; `RequestedResources` nao referencia mais ApiResource/grupo.

### Resultado da Fase 2

**Status:** concluida. `dotnet build RoyalIdentity.sln` limpo; `dotnet test` verde (116 — Pipelines 3, Identity 6, Integration 107).

**Modelo:** `ScopeType` reduzido a 3 (`Identity`, `ResourceServer`, `Scope`), tudo consolidado em `RoyalIdentity.Models.Scopes`. Criados `Scope` (ex-`ApiScope`) e `ResourceServer` (ex-`ApiResource`; com `Scopes`, `Audience`, `Secrets` (`ClientSecret`), `AllowedAccessTokenSigningAlgorithms`, `AllowScopeRequests`, `GetAudience()`). Removidos `ApiResource`, o `ResourceServer`-grupo antigo e `ResourceServer.IdentityScopes`. `RequestedScopes` -> `RequestedResources` (membros `IdentityScopes`/`ResourceServers`/`Scopes` + `RequestedScopeNames` para os nomes crus). Os `[Obsolete]` de secrets/signing-alg foram revertidos.

**Migracao mecanica (Opcao A):** ~25 consumidores atualizados para compilar preservando comportamento; `IResourceStore` enxuto (finders obsoletos removidos).

**Trabalho de fases seguintes adiantado (forcado pelo colapso do modelo):**
- **Store (Fase 3):** `MemoryStorage`/`RealmMemoryStore` reshapados de 4 para **2 dicionarios** (`ResourceServers` com `Scope`s aninhados + `IdentityScopes`); `InitializeResources()` removido; seeding migrado (`apiserver` com Scopes `api`/`api:read`/`api:write`). **Fase 3 NAO esta concluida** — so o reshape estrutural foi adiantado; faltam unicidade de `Scope.Name`, `AllowScopeRequests`, filtro de scopes-filhos desabilitados no discovery e testes de `DisabledScopes` (ver "Avaliacao do progresso" / Fase 2).
- **Token (Fase 5):** `aud` agora vem de `ResourceServer.GetAudience()` (default = Name) via `RequestedResources.GetAudiences()`; `ToScopeClaims()` emite **apenas** Scopes pedidos + IdentityScopes + offline (decisao #2); os dois metodos de "identity claim types" unificados para `IdentityScopes`.

**Mudanca de comportamento (intencional):** `api:read`/`api:write` (antes ApiScope avulso, sem `aud`) agora sao Scopes de `apiserver` e passam a contribuir `aud=apiserver`; "api" muda de `aud=api` para `aud=apiserver`. Nenhum teste quebrou.

**Restante real para as proximas fases:**
- **Fase 4** — `Client.AllowedResources` (`AllowedIdentityScopes`/`AllowedResourceServers`/`AllowedScopes`/`AllowAllResourceServers`) + `ClientType`; aposentar `Client.AllowedScopes`(string)/`AllowOfflineAccess`. (Os decorators ainda filtram por `client.AllowedScopes` string.)
- **Fase 5** — cadeia de signing-alg **RS -> Client -> Realm** (hoje so RS-level); revisar decorators para usar `AllowedResources` (depende da Fase 4).
- **Fase 6** — Resource Indicators (RFC 8707) + Protected Resource Metadata (RFC 9728), com `ProtectedResource`s por ResourceServer.
- **Fase 7** — consentimento agrupado por ResourceServer/ProtectedResource (hoje so renomeado/flat).

---

## Fase 3: Store

Arquivos provaveis: `IResourceStore.cs`, `ResourceStore.cs` (in-memory), `MemoryStorage`/`RealmMemoryStore`.

Passos:

1. Enxugar `IResourceStore` (remover os finders `[Obsolete]` por tipo; manter `FindResourcesByScopeAsync` -> `RequestedResources`, `GetAll`/`GetAllEnabled`).
2. `MemoryStorage`/`RealmMemoryStore`: de 4 dicionarios para **2** (`ResourceServers` com `Scope`s aninhados + `IdentityScopes`). Lookup por nome de scope resolve via RS dono.
3. Atualizar o seeding do DemoRealm.
4. **Unicidade global de `Scope.Name`**: indice nome -> (RS, Scope) construido no ctor do `ResourceStore`, que lanca em duplicata (fail-fast) e elimina a ambiguidade de first-match.
5. **Scopes desabilitados no discovery**: `GetAllEnabledResourcesAsync` passa a retornar ResourceServers com **apenas Scopes habilitados** (via copy ctor de `ResourceServer`), para um scope desabilitado de um RS habilitado nao vazar.
6. Testes de store (`ResourceStoreTests`): unicidade, `DisabledScopes`, snapshot de habilitados.

> `AllowScopeRequests` **nao** e tratado aqui — e autorizacao de request (ver Fase 5), nao resolucao de armazenamento.

Criterio de aceite: lookup por nome (scope, identity, resource server) resolve no novo modelo; seeding migrado; nome de scope unico por realm (enforced); discovery nao expoe scopes desabilitados; testes verdes.

### Resultado da Fase 3

**Status:** concluida.

- **Unicidade de `Scope.Name`** (`ResourceStore.BuildScopeIndex`): indice `name -> (ResourceServer, Scope)` montado no ctor; lanca `InvalidOperationException` se o mesmo nome aparecer em dois RS. `FindScope` (first-match, ambiguo) **removido** — a resolucao agora e O(1) pelo indice.
- **Discovery sem scopes desabilitados** (`ResourceStore.GetAllEnabledResourcesAsync`): retorna RS filtrados por `Enabled` **e** com `Scopes` filtrados por `Enabled`, usando o novo **copy ctor** `ResourceServer(ResourceServer)`. Antes, `AllScopes.Scopes` achatava todos os scopes (inclusive desabilitados) de RS habilitados.
- **Copy ctor `ResourceServer(ResourceServer)`** adicionado (convencao copy-on-create; sera reusado no CRUD/Fase 4).
- **RS-direto preservado** no `FindResourcesByScopeAsync` com nota de codigo apontando que a remocao (apontamento 2.1) foi **deferida para a Fase 4**.
- **Testes** (`Tests.Integration/Storage/ResourceStoreTests.cs`, 4 casos): disabled scope -> `DisabledScopes`; `GetAllEnabledResourcesAsync` exclui scope-filho desabilitado; exclui RS desabilitado; ctor lanca em nome de scope duplicado. Suite: **120 testes verdes** (Pipelines 3, Identity 6, Integration 111).
- **`AllowScopeRequests`** reclassificado para a **Fase 5** (autorizacao de request).

---

## Fase 4: Client AllowedResources e ClientType

Arquivos provaveis: `RoyalIdentity/Models/Client.cs`, novo `RoyalIdentity/Models/ClientType.cs` (enum).

Passos:

1. Criar enum `ClientType` (`Public`, `Confidential`) e adicionar `Client.ClientType`. A validacao de consistencia com `RequireClientSecret`/secrets fica para o CRUD de client futuro (ver ADR-011).
2. Adicionar `AllowedIdentityScopes`, `AllowedResourceServers`, `AllowedScopes` (individuais) e `AllowAllResourceServers` (Full Scope Allowed); manter `AllowOfflineAccess` (flag).
3. Remover o `AllowedScopes` antigo (string-misto) e os `[Redesign]`.
4. Implementar a regra de permissao (scope permitido por `AllowAllResourceServers`, por `AllowedScopes`, ou pelo RS em `AllowedResourceServers`).
5. **(apontamento 2.1/2.2/2.3)** Tornar o ResourceServer **nao-requisitavel por nome**: remover o branch de RS-direto em `ResourceStore.FindResourcesByScopeAsync` (l.60-66) — nome de RS vira `MissingScopes`. So `Scope`/`IdentityScope` sao requisitaveis; o `aud` deriva dos RS donos dos Scopes pedidos. Documentar a semantica no bloco de decisoes e na ADR-010. Resolve o gap de aud-sem-autorizacao e o caso de refresh.

Criterio de aceite: client autoriza scopes por tipo; pedir scope de um RS permitido funciona sem listar cada scope; `AllowAllResourceServers` libera todos os RS sem alterar o conteudo do token; `ClientType` presente no modelo; pedir o **nome de um RS** como scope resulta em `invalid_scope` (RS nao e requisitavel).

### Resultado da Fase 4

**Status:** concluida.

- **Modelo (`Client`):** novo enum `ClientType` (`Public`/`Confidential`, default `Public`). Adicionados `AllowedIdentityScopes`, `AllowedResourceServers`, `AllowAllResourceServers` e `AllowedAccessTokenSigningAlgorithms`. `AllowedScopes` **reaproveitado** para scopes individuais de API (nome mantido por ADR-010). `AllowOfflineAccess` mantido (flag). Os `[Redesign]` de `AllowedScopes`/`AllowOfflineAccess` **removidos**. Novo `RoyalIdentity/Models/ClientType.cs`.
- **Regra de permissao (`ResourcesValidator`):** identity scope permitido por `AllowedIdentityScopes`; scope permitido por `AllowAllResourceServers` **ou** `AllowedScopes` **ou** RS-dono em `AllowedResourceServers` (helper `IsScopeAllowed`); offline por `AllowOfflineAccess`. Vale para authorize **e** client_credentials (ambos passam pelo validator). Autorizacao no nivel do **scope** — `resources.ResourceServers` serve so para o `aud`, sem dupla checagem (resolve 2.1).
- **Passo 5 (RS nao requisitavel):** branch RS-direto **removido** do `ResourceStore.FindResourcesByScopeAsync`; nome de RS vira `MissingScopes` (`invalid_scope`). ADR-010 atualizada com a semantica explicita.
- **Migracao (comportamento-equivalente):** seeding (3 clients: `server_admin`/`demo_client` -> `AllowedIdentityScopes`; `demo_consent_client` -> `AllowedIdentityScopes` + `AllowedResourceServers = {apiserver}`) + ~7 arquivos de teste (split do `AllowedScopes` misto; `offline_access` -> flag). Clients de teste confidenciais ficaram no default `Public` — o `ClientType` explicito e a consistencia com segredo sao do CRUD futuro (ADR-011), sem enforcement agora.
- **Testes:** `AllowAllResourceServers` + negativo (`invalid_scope`) via client_credentials; RS-nao-requisitavel (store). `AllowedResourceServers` e `AllowedScopes` individual ja cobertos por `LoginConsentUIFlowTests` e `RealmOptionsPhase4Tests`. Suite: **124 verdes** (Pipelines 3, Identity 6, Integration 115).
- **Fica para a Fase 5:** o `ClientResourceDecorator` ainda usa `client.AllowedScopes` (individual) como default de client_credentials sem `scope` — a resolucao completa do default a partir de `AllowedResources`/`AllowAllResourceServers` entra na Fase 5 (passo 1).

---

## Fase 5: Pipeline, Token e Audience

Arquivos provaveis: `ResourcesDecorator`, `ClientResourceDecorator`, `AuthorizationResourcesValidator`, `ResourcesValidator`, `ResourcesExtensions`, `DefaultTokenFactory`, `DefaultTokenClaimsService`, `DiscoveryHandler`, `UserInfoHandler`, `AuthorizationCode`/`RefreshToken`.

Passos:

1. Decorators/validators: filtrar por `AllowedResources` do client; aplicar `RequestedResources`.
2. Token: `scope` claim so com os pedidos; no caminho sem `resource`, `aud` = `ResourceServer.Audience ?? ResourceServer.Name` dos ResourceServers donos dos scopes pedidos.
3. Signing-alg: Realm ordena/filtra (chaves do realm, na ordem do realm) e RS/Client so filtram, hierarquico (RS, senao Client, senao so Realm) — `ResolveAccessTokenSigningAlgorithms` em `ResourcesExtensions`; a selecao por ordem do realm + filtro ja existe em `IKeyManager.GetSigningCredentialsAsync`.
4. Discovery (`scopes_supported`) e UserInfo coerentes com o novo modelo.
5. **(apontamento 2.4 + 2.5)** `AllowScopeRequests`: quando um ResourceServer tem `AllowScopeRequests = false`, seus Scopes **nao** podem ser pedidos via parametro `scope` (rejeitar como `invalid_scope`) — gate de autorizacao de request, no validator. E rever a restricao de signing do **id token** por ResourceServer (`DefaultTokenFactory` l.202): id token deve usar `Client.AllowedIdentityTokenSigningAlgorithms` (fallback Realm), nao os algoritmos do RS.
6. **(apontamento 3.1) — DECIDIDO: colapsar.** Unificar `MissingScopes`/`DisabledScopes` num unico bucket de scopes invalidos (desabilitado tratado como invalido, uniformemente). Motivo: `DisabledScopes` e praticamente morto no pipeline (todos os decorators usam `onlyEnabled: true` -> desabilitado ja cai em `MissingScopes`) e `GetInvalidScopes()` ja concatena os dois — colapsar nao tem perda observavel e simplifica o modelo/validators.

Criterio de aceite: token de realms/clients diferentes reflete scopes/aud corretos no caminho por `scope`; signing-alg respeita a precedencia; RS com `AllowScopeRequests = false` rejeita pedido de seus scopes; id token nao e restrito pelos algoritmos do RS; classificacao de scope invalido/desabilitado consistente entre todos os decorators.

### Resultado da Fase 5

**Status:** concluida. Build limpo; **124 testes verdes** (Pipelines 3, Identity 6, Integration 115).

- **Passo 6 (colapso — apontamento 3.1):** `DisabledScopes` removido de `RequestedResources`; scope desabilitado agora vira `MissingScopes` (bucket unico de invalidos). `IsValid`/`GetInvalidScopes`/`CopyTo` e `ResourceStore.IsEnabled` ajustados; testes do store atualizados.
- **Passo 5a (gate `AllowScopeRequests`):** `ResourcesValidator` rejeita (`invalid_scope`) scope cujo ResourceServer dono tem `AllowScopeRequests = false`. O owner e resolvido uma vez por scope (reuso no `IsScopeAllowed`).
- **Passo 5b (signing do id token — apontamento 2.5):** id token passa a usar `Client.AllowedIdentityTokenSigningAlgorithms` (fallback Realm) em `AllowedSigningAlgorithms`, nao mais os algoritmos do RS — alinha a assinatura efetiva ao algoritmo ja usado no at_hash/c_hash.
- **Passo 3 (cadeia de signing do access token — decisao #a refinada):** novo `ResolveAccessTokenSigningAlgorithms` (`ResourcesExtensions`) — o **Realm** sempre ordena/filtra; **RS** e **Client** so filtram, hierarquico (RS, senao Client, senao so Realm), nunca juntos. A selecao "primeiro da ordem do realm que passa no filtro" ja existia em `GetSigningCredentialsAsync`. ADR-010 #a e o Modelo Alvo atualizados para esse modelo.
- **Passo 1 (default de client_credentials):** sem `scope`, o `ClientResourceDecorator` resolve o default a partir de `AllowedResources` (`AllowedScopes` + scopes de `AllowedResourceServers`, ou todos se `AllowAllResourceServers`), filtrando por `AllowScopeRequests` — espelha a regra do validator.
- **Passo 4 (discovery/userinfo):** verificados coerentes (discovery usa `resources.Scopes`/`IdentityScopes` por `ShowInDiscoveryDocument`; userinfo usa `RequestedIdentityClaimTypes` = `IdentityScopes`). **Diferido para a Fase 6:** filtrar de `scopes_supported` os scopes de RS com `AllowScopeRequests = false` (a discovery ja sera reformulada na Fase 6 para `protected_resources`).
- **Testes de signing por precedencia** (multi-alg/realm) ficam para a **Fase 8** (exigem realm com multiplas chaves/algoritmos); a suite atual cobre o caminho sem restricao (-> realm default).
- **Revisao pos-Fase 5:** adicionados testes para `client_credentials` com `offline_access` (deve rejeitar mesmo se o client permite refresh) e para `client_credentials` sem `scope` (default resolvido deve aparecer na resposta). Corrigido o abort do `ClientResourceDecorator` e a copia de `RequestedScopeNames` em `RequestedResources.CopyTo`.

---

## Fase 6: Resource Indicators e Protected Resource Metadata

Arquivos provaveis: `ResourceServer`, novo `ProtectedResource`, `RequestedResources`, `IWithResources`, contexts de authorize/token, `AuthorizationCode`, `RefreshToken`, `ResourceStore`, `DiscoveryHandler`, `DefaultTokenFactory`, validators.

Passos:

1. Criar `ProtectedResource` (ADR-012) e adicionar `ResourceServer.ProtectedResources`; manter `ResourceServer.Audience` como audience legado do caminho por `scope`.
2. Indexar `ProtectedResource.ResourceUri` no store, com unicidade por realm; validar URI absoluta, sem fragmento, e politica de `https` com excecao controlada para dev/local.
3. Parsear e persistir o parametro `resource` no authorize e token endpoint (valores repetidos permitidos); `AuthorizationCode`/refresh grants guardam os protected resources autorizados, e o token endpoint so aceita subset. `resource` sem scope de API e valido em qualquer fluxo que emita ou prepare um access token (`client_credentials`, `authorization_code`, implicit/hybrid com `token`); `id_token`-only continua restrito a identity scopes.
4. Implementar validacao/autorizacao: `resource` malformed/com fragmento/desconhecido/de RS desabilitado (`Enabled` derivado do RS pai)/nao permitido -> `invalid_target`; client so pode pedir resource cujo RS dono esteja em `AllowedResourceServers` ou `AllowAllResourceServers`.
5. Implementar semantica de `aud`: sem `resource`, scopes de API geram `ResourceServer.Audience ?? ResourceServer.Name`; com `resource`, o `aud` e `ProtectedResource.ResourceUri` e domina o `Audience` legado para o mesmo RS.
6. Aceitar `scope + resource`: com `resource` presente, validar cada scope pelo RS dono — se o RS dono **possui** `ProtectedResource`s, exigir ao menos um `ProtectedResource` dele entre os `resource`s pedidos (senao `invalid_target`); se o RS dono **nao possui** `ProtectedResource`s, o scope e valido independentemente (eixo de scope). Ex.: `scope` de A (sem resources) + `resource` de B nao deve falhar.
7. Discovery OIDC: remover de `scopes_supported` os scopes de ResourceServers com `AllowScopeRequests = false`; esses scopes nao devem ser anunciados como requisitaveis via parametro `scope`.
8. RFC 9728: publicar `protected_resources` no metadata do Authorization Server e gerar metadata por `ProtectedResource` (`resource`, `authorization_servers`, `scopes_supported`, `bearer_methods_supported`, `resource_name`, docs/policy/tos quando configurados).
9. Ao tocar `DiscoveryHandler`, substituir LINQ query syntax simples (ex.: `from scope in ... where ... select ...`) por method-chain LINQ com lambdas, conforme `.ai/rules/code-style.rules.md`.
10. Testes de authorize/token/refresh/discovery: audience-only, scope-only, scope+resource, multi-resource, resource desconhecido, resource nao permitido, implicit/hybrid resource-only, subset no token/refresh, e `scopes_supported` sem scopes de RS com `AllowScopeRequests = false`.

Criterio de aceite: `resource` RFC 8707 funciona em authorize/token, inclusive audience-only em fluxos que emitem ou preparam access token; `aud` respeita o caminho scope-vs-resource; grants preservam resources autorizados; `invalid_target` cobre resources invalidos; discovery nao anuncia scopes que nao podem ser pedidos por `scope`; discovery publica `protected_resources` e metadata RFC 9728 geravel por protected resource; testes verdes.

### Resultado da Fase 6 (concluida)

**Status:** **concluida** - Resource Indicators (RFC 8707) e Protected Resource Metadata (RFC 9728) implementados e verificados. Testes verdes: `dotnet test RoyalIdentity.sln --no-restore` (**146 testes**: Integration 137, Identity 6, Pipelines 3).

**Feito:**
- **Modelo e store (passos 1-2):** `ProtectedResource` em `ResourceServer.ProtectedResources`; indice por `ResourceUri` com unicidade por realm; validacao fail-fast para URI absoluta HTTPS sem fragmento, com excecao HTTP loopback/localhost para dev/testes.
- **Request/authorization (passos 3-6):** `resource` multi-valor preservado em `NameValueCollection`; authorize e client_credentials resolvem `ProtectedResources`; token endpoint parseia `resource` em `authorization_code` e `refresh_token`; grants aceitam omissao ou subset do conjunto autorizado e rejeitam subset fora dele com `invalid_target`.
- **Persistencia em grants:** `AccessToken.ResourceUris` e `RefreshToken.ResourceUris` guardam os protected resources autorizados; refresh sem subset preserva audiences de resource; refresh com subset reemite access token com o audience reduzido.
- **`aud` (passo 5):** `ProtectedResource.ResourceUri` entra como audience explicito e suprime o audience legado (`ResourceServer.Audience ?? Name`) do mesmo RS; scopes sem `resource` continuam pelo caminho legado.
- **Implicit/hybrid resource-only:** `AuthorizationResourcesValidator` aceita `resource` sem API scope quando `response_type` contem `token`; `id_token`-only permanece restrito a identity scopes.
- **Discovery (passos 7-9):** `scopes_supported` exclui scopes de RS com `AllowScopeRequests = false`; discovery do AS publica `protected_resources`; novo endpoint `/{realm}/.well-known/oauth-protected-resource?resource=...` gera metadata RFC 9728 (`resource`, `authorization_servers`, `scopes_supported`, `bearer_methods_supported`, `resource_name`, docs/policy/tos quando configurados).
- **Correcoes associadas:** authorization_code emite access token com `IdentityProfileTypes.User`; refresh que reemite access token usa o principal do access token armazenado para preservar `auth_time`/`sid`; TestHost usa DataProtection dentro do diretorio de build e logging sem EventLog para evitar falhas de permissao no sandbox.

**Testes adicionados/refinados:**
- `client_credentials`: audience-only, multi-resource, resource desconhecido, resource nao permitido, scope+resource incoerente.
- `authorization_code`: emissao com resource, subset no token endpoint, subset nao autorizado.
- `refresh_token`: preservacao de resource audience, subset autorizado, subset nao autorizado.
- `authorize`: implicit com `resource` sem API scope.
- `discovery/store`: `protected_resources`, metadata RFC 9728, URI invalida/fragmento/http nao-local, duplicate `ResourceUri`, localhost HTTP aceito.

**Falta:** nada pendente na Fase 6. A limitacao conhecida de consentimento para `ProtectedResource`s fica documentada para a Fase 7.

---

## Fase 7: Consentimento

Arquivos provaveis: `DefaultConsentService`, `ConsentPageService`, `ConsentViewModel`/`ConsentInputModel`, `ConsentPage.razor`.

Nota de transicao: a Fase 6 pode introduzir `ProtectedResource`s em authorize, mas a tela e a persistencia de consentimento continuam flat ate esta fase. Requests com `resource` e clients com `RequireConsent = true` devem ser tratados como limitacao conhecida ate a Fase 7; a correcao aqui e mostrar, consentir e validar `ProtectedResource`s agrupados por ResourceServer.

Passos:

1. ViewModel agrupado por **ResourceServer** (dados do RS + seus Scopes pedidos + `ProtectedResource`s pedidos) + IdentityScopes + offline_access.
2. Razor: tela clara e agradavel, agrupando visualmente por ResourceServer e mostrando protected resources audience-only quando existirem.
3. Consentir/negar continua operando sobre os scopes efetivamente pedidos e os protected resources autorizados.

Criterio de aceite: a tela mostra os scopes e protected resources agrupados por ResourceServer de forma clara; consentimento grava/aplica corretamente.

---

## Fase 8: Testes e Regressao

Passos:

1. Testes de modelo/store: lookup, copy, validacao, missing/disabled, unicidade de `ProtectedResource.ResourceUri`.
2. Testes de token: `scope` so com pedidos; `aud` por `ResourceServer.Audience ?? Name` no caminho por `scope`; `aud` por `ProtectedResource.ResourceUri` no caminho por `resource`; `scope + resource` coerente; refresh preservando resources autorizados.
3. Testes de signing por precedencia: realm default, filtro do client quando nao ha filtro de RS, filtro do RS sobrepondo client, multi-RS com intersecao, multi-RS incompativel, e id token usando apenas `Client.AllowedIdentityTokenSigningAlgorithms`.
4. Testes de autorizacao do client: `AllowedScopes`, `AllowedResourceServers`, `AllowAllResourceServers`, `AllowScopeRequests = false`, `resource` permitido/nao permitido, Resource Indicators e consent agrupado.
5. Isolamento por realm onde aplicavel.
6. `dotnet test RoyalIdentity.sln` verde.

Criterio de aceite final:

```powershell
dotnet test RoyalIdentity.sln --no-restore
```

---

## Como Marcar Progresso

- Uma fase so e concluida quando suas tarefas estiverem feitas e o criterio de aceite satisfeito.
- Ao concluir uma fase, atualizar `Progresso` no topo (`0 de 8`) e substituir um `-` por `█` na barra (barra de 8 segmentos, um por fase; mesmo caractere usado nos outros planos).

---

## Avaliacao do progresso

Apontamentos da revisao da execucao, por fase. Cada item: problema, solucao e `Status`.

`Status` pode ser: `avaliar`, `questionado`, `rejeitado`, `valido`, `corrigido`. Ao criar, usar `avaliar`.

### Fase 2

Revisao de 5 pontos levantados sobre a execucao da Fase 2.

#### 2.1 — ResourceServer direto gera `aud` sem autorizacao do client

**Problema:** `ResourceStore.FindResourcesByScopeAsync` resolve o nome de um ResourceServer direto (branch `resourceServers.TryGetValue`, l.60-66) e o `DefaultTokenFactory` emite o `aud` dele; porem `ResourcesValidator` (l.50-68) so valida `IdentityScopes` e `Scopes` contra `client.AllowedScopes` — **nao** itera `ResourceServers`. Logo, pedir `scope=apiserver` passa a validacao e gera `aud=apiserver` sem o RS estar em `AllowedScopes`.

**Avaliacao:** veridico e valido — confirmado no codigo. Foi **introduzido na Fase 2** (no modelo antigo, pedir o RS-grupo nao gerava `aud`; o `aud` vinha so do `ApiResource`). Contradiz a decisao #2: o RS e item da allowed-list e fonte do `aud` **dos scopes pedidos**, nao um scope requisitavel por nome.

**Correcao recomendada:** remover a resolucao de **RS-como-scope** no store (RS direto vira `MissingScopes`). Fecha o gap e alinha ao contrato; o `aud` continua vindo dos RS donos dos Scopes pedidos.

**Decisao:** **dobrar na Fase 4** (passo 5), junto com `AllowedResourceServers`, quando a autorizacao por RS for implementada de fato. Ate la o caminho fica aberto (RS pedido por nome -> `aud` sem checagem de `AllowedScopes`).

**Resolucao (Fase 4):** branch RS-direto removido do `ResourceStore`; pedir nome de RS vira `invalid_scope`. O `aud` deriva so dos RS donos dos Scopes pedidos. Gap fechado.

**Status:** `corrigido` (Fase 4)

#### 2.2 — Contrato "ResourceServer como scope" precisa ficar explicito

**Problema:** o plano/ADR-010 dizem que o `aud` vem dos RS "cujos scopes foram pedidos", mas nao definem se pedir o **nome do RS** e valido. Tambem falta definir `response_type=token` com RS-only — hoje `AuthorizationResourcesValidator` (l.40) exige `Scopes.Any()` e rejeita RS-only.

**Avaliacao:** valido. Decisao registrada: **RS nao e um scope requisitavel** — so `Scope` e `IdentityScope` sao; o `aud` deriva dos RS donos dos Scopes pedidos. O acesso explicito por audience/resource foi especificado na ADR-012 e entra na Fase 6. Com isso, a rejeicao de RS-only em l.40 fica **correta** (nao ha como obter token so com RS pelo parametro `scope`).

**Resolucao (Fase 4):** ADR-010 atualizada (bullet "ResourceServer nao e requisitavel"); implementado no `ResourceStore`. Decisao registrada.

**Status:** `corrigido` (Fase 4)

#### 2.3 — Refresh com `UpdateAccessTokenClaimsOnRefresh` perderia RS direto

**Problema:** `RefreshTokenHandler` (l.72) re-resolve recursos a partir de `accessToken.Scopes` (claims). Como o RS direto nao vira claim `scope`, o `aud` desapareceria nesse caminho.

**Avaliacao:** veridico. **Consequencia de 2.1/2.2** — se o RS deixar de ser requisitavel (correcao do 2.1), torna-se **moot**: o `aud` sempre deriva dos owners dos Scopes pedidos, que sao re-resolvidos a partir dos claims. Sem a correcao, exigiria persistir os nomes crus.

**Resolucao (Fase 4):** com o RS nao-requisitavel, o `aud` sempre deriva dos owners dos Scopes pedidos (re-resolvidos no refresh). Moot, conforme previsto.

**Status:** `corrigido` (Fase 4)

#### 2.4 — Fase 3 NAO esta concluida (over-claim corrigido)

**Problema:** o "Resultado da Fase 2" afirmava que a Fase 3 estava "essencialmente concluida". So o reshape estrutural do store foi adiantado. Faltam: unicidade global de `Scope.Name` (`FindScope` pega o **primeiro match** — nomes duplicados entre RS sao ambiguos), enforcement de `AllowScopeRequests` (ignorado hoje), filtro de **scopes-filhos desabilitados no discovery** (`GetAllEnabledResourcesAsync` filtra RS por `Enabled` mas nao os `Scopes` aninhados; `AllScopes.Scopes` expoe scopes desabilitados de RS habilitados), e testes de `DisabledScopes`.

**Avaliacao:** valido — **exagerei** ao declarar a Fase 3 concluida. Texto do "Resultado da Fase 2" corrigido.

**Resolucao (Fase 3 concluida):** unicidade de `Scope.Name` (indice com fail-fast), filtro de scopes-filhos desabilitados no discovery e testes de `DisabledScopes` **feitos** (ver "Resultado da Fase 3"). `AllowScopeRequests` foi **reclassificado** para a Fase 5 (autorizacao de request, nao armazenamento — passo 5 da Fase 5).

**Status:** `corrigido`

#### 2.5 — Signing-alg pendente (Fase 5) + id token restrito por ResourceServer

**Problema:** falta a cadeia de selecao RS -> Client -> Realm (ja prevista para a Fase 5). Alem disso, o **id token** recebe `AllowedAccessTokenSigningAlgorithms` do ResourceServer (`DefaultTokenFactory` l.202) — o id token e do client, nao da API.

**Avaliacao:** valido. A cadeia ja estava marcada para a Fase 5. A restricao de signing do id token por RS e **pre-existente** e deve ser revista na Fase 5 (id token deve usar `Client.AllowedIdentityTokenSigningAlgorithms`, com fallback Realm).

**Resolucao (Fase 5):** cadeia do access token implementada como Realm-ordena/filtra + RS/Client filtram hierarquico (`ResolveAccessTokenSigningAlgorithms`); id token corrigido para usar `Client.AllowedIdentityTokenSigningAlgorithms` (fallback Realm). ADR-010 #a atualizada.

**Status:** `corrigido` (Fase 5)

### Fase 3

#### 3.1 — `onlyEnabled: true` classifica scope desabilitado como `MissingScopes`, nao `DisabledScopes`

**Problema:** em `ResourceStore.FindResourcesByScopeAsync` (l.83-84), o filtro `(match.Server.Enabled && match.Scope.Enabled)` faz o `if` ser `false` quando o scope esta desabilitado e `onlyEnabled: true`, caindo em `MissingScopes` (l.94) **antes** do `IsEnabled` (l.86) classifica-lo como `DisabledScopes`. **Todos** os decorators e o refresh chamam com `onlyEnabled: true`; so o UserInfo usa `false`. Logo, `DisabledScopes` praticamente **nunca** e populado no pipeline, e o teste inicial (`ResourceStoreTests`, default `onlyEnabled:false`) cobria uma ramificacao que o pipeline nao usa.

**Avaliacao:** veridico e valido. **Pre-existente** (estrutura da Fase 2), **nao** e regressao da Fase 3 — o que faltava era cobertura honesta. Funcionalmente o request continua invalidado nos dois decorators. **Reviravolta:** tornar a classificacao "precisa" (desabilitado -> `DisabledScopes`) **regrediria** o `ClientResourceDecorator`, que so checa `MissingScopes` (passaria a dropar em vez de rejeitar); no `ResourcesDecorator` a imprecisao e **invisivel** (`IsValid` e `GetInvalidScopes()` cobrem os dois buckets).

**Resolucao:** teste do caminho `onlyEnabled: true` adicionado, fixando o comportamento real. **Fase 5 (passo 6): colapsado** — `DisabledScopes` removido; scope desabilitado vira `MissingScopes` (bucket unico de invalidos). `IsValid`/`GetInvalidScopes`/`CopyTo`/`ResourceStore.IsEnabled` e os testes do store atualizados.

**Status:** `corrigido` (Fase 5)
