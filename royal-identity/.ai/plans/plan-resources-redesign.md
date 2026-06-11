# Plan: Resources / Scopes Redesign

## Status: IN_PROGRESS

## Progresso

`-------` **0%** - 0 de 7 fases concluidas

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

E ainda: refatorar `RequestedScopes` -> `RequestedResources`, consolidar o namespace, redesenhar a permissao do client (`AllowedResources`) e o consentimento agrupado por ResourceServer.

---

## Fora de Escopo

- **Introspection endpoint** e **reference tokens** continuam no backlog (`.ai/backlogs/backlog-001.md`). Este plano apenas **modela** os secrets no `ResourceServer`; nao implementa autenticacao de ResourceServer no introspect (sera um `secret evaluator` de ResourceServer no plano futuro).
- Autorizacao fina (UMA / permissions / policies estilo Keycloak Authorization Services) — fora de escopo.
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
- **`ResourceServer`** (= renomeado do antigo `ApiResource`): dono de `Scope`s. Campos: `Scopes` (`Scope[]`), `Audience` (string; default = `Name`), `Secrets` (para introspection futuro), `AllowedAccessTokenSigningAlgorithms`, `AllowScopeRequests`, `Visibility`, `Enabled`.
- **`Scope`** (= renomeado do antigo `ApiScope`): operacao de um `ResourceServer`. `Name` **unico/global no realm**, `Required`, `Emphasize`. **So existe dentro de um ResourceServer.**

**Removidos:** o `ResourceServer`-grupo atual (que agregava `ApiResource`s) e o nivel intermediario `ApiResource`. `ScopeType` passa a ter 3 valores: `Identity`, `ResourceServer`, `Scope`. `ResourceServer.IdentityScopes` **removido** (IdentityScope e do realm, nao do RS).

**`RequestedResources`** (= renomeado de `RequestedScopes`, namespace unico `Models.Scopes`): coleciona `IdentityScopes`, `ResourceServers`, `Scopes` pedidos + `OfflineAccess`, `MissingScopes`, `DisabledScopes`.

**Semantica (decisao #2):**

- **`scope` no token** = apenas os `Scope` (e `IdentityScope`) explicitamente pedidos + `offline_access`. Pedir o nome de um ResourceServer **nao** despeja todos os scopes no token.
- **Autorizacao do client** = ter um `ResourceServer` em `AllowedResourceServers` ⇒ o client pode pedir **qualquer Scope** daquele RS.
- **`aud`** = uniao dos `ResourceServer.Audience` dos ResourceServers cujos scopes foram pedidos.

**Permissao do client (decisao #4 + tipo de client — ver ADR-011)** — substituir `AllowedScopes`(string-misto) e `AllowOfflineAccess`:

- `ClientType` (enum `Public`/`Confidential`, RFC 6749) — propriedade do client. Hoje o tipo e implicito (`RequireClientSecret`); passa a ser explicito. `RequireClientSecret`/secrets seguem como comportamento de runtime; a consistencia entre eles e validada no **CRUD de client futuro**.
- `AllowedIdentityScopes: HashSet<string>`
- `AllowedResourceServers: HashSet<string>` (libera todos os Scopes do RS)
- `AllowedScopes: HashSet<string>` (Scopes individuais)
- `AllowAllResourceServers: bool` (Full Scope Allowed, estilo Keycloak) — opt-in; libera pedir qualquer Scope de qualquer ResourceServer do realm. **So autorizacao**: o token continua levando apenas os scopes pedidos (decisao #2). Valido apenas para `Confidential` (guard enforced no CRUD futuro; por ora invariante documentado).
- `AllowOfflineAccess: bool` (flag)

Regra: um `Scope` pedido e permitido se `AllowAllResourceServers` **ou** esta em `AllowedScopes` **ou** seu ResourceServer dono esta em `AllowedResourceServers`. `IdentityScope` permitido se em `AllowedIdentityScopes`. `offline_access` se `AllowOfflineAccess`.

**Algoritmo de assinatura do access token (decisao #a) — precedencia com ordem:**

1. Se algum ResourceServer pedido declara `AllowedAccessTokenSigningAlgorithms` -> lista do **RS**, na ordem do RS.
2. Senao, se o Client declara -> lista do **Client**, na ordem do Client.
3. Senao -> algoritmos disponiveis no **Realm**, na ordem do Realm.

Pega o primeiro para o qual ha chave de assinatura disponivel. **Multi-RS:** quando varios ResourceServers pedidos declaram algoritmos, intersectar as listas (todas as APIs participantes devem aceitar o escolhido); desempate pela ordem do primeiro ResourceServer pedido.

**Secrets (decisao #7):** secrets ficam no `ResourceServer` (modelo do IdentityServer) para uso futuro no introspection; `Client.ClientSecrets` permanece para client_credentials. `AllowedAccessTokenSigningAlgorithms` fica no ResourceServer (e tambem no Client, conforme a cadeia acima). Os `[Obsolete]` atuais sao **revertidos** (nao migram para o client).

---

## Ordem de Execucao e Dependencias

1. **Fase 1 (auditoria & decisao)** — pre-requisito de todas.
2. **Fase 2 (modelo de dominio)** — base das demais.
3. **Fase 3 (store)** e **Fase 4 (client AllowedResources)** — dependem da Fase 2; independentes entre si.
4. **Fase 5 (pipeline/token/aud)** — depende de 2, 3, 4.
5. **Fase 6 (consentimento)** — depende de 2 e 5.
6. **Fase 7 (testes)** — depende de tudo.

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

**Status:** em andamento (passo 1 concluido).

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
- Alvo: `aud` = `ResourceServer.Audience` (default = `Name`) dos ResourceServers donos dos Scopes pedidos (+ ResourceServers pedidos diretamente). A linha `request.Resources.ApiResources.Select(x => x.Name)` passa a `Select(rs => rs.Audience)` sobre os ResourceServers efetivos.
- Manter o comportamento `IsOpenId -> client.Id` no `aud`.
- **Nota cruzada (signing-alg, passo 5):** `AllowedSigningAlgorithms` tambem sai hoje **so** de `request.Resources.ApiResources.FindMatchingSigningAlgorithms()` (access **e** id token, l.80 e l.202). A cadeia RS->Client->Realm decidida e uma **mudanca de comportamento** aqui, tratada na Fase 5.

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

---

## Fase 3: Store

Arquivos provaveis: `IResourceStore.cs`, `ResourceStore.cs` (in-memory), `MemoryStorage`/`RealmMemoryStore`.

Passos:

1. Enxugar `IResourceStore` (remover os finders `[Obsolete]` por tipo; manter `FindResourcesByScopeAsync` -> `RequestedResources`, `GetAll`/`GetAllEnabled`).
2. `MemoryStorage`/`RealmMemoryStore`: de 4 dicionarios para **2** (`ResourceServers` com `Scope`s aninhados + `IdentityScopes`). Lookup por nome de scope resolve via RS dono.
3. Atualizar o seeding do DemoRealm.

Criterio de aceite: lookup por nome (scope, identity, resource server) resolve no novo modelo; seeding migrado.

---

## Fase 4: Client AllowedResources e ClientType

Arquivos provaveis: `RoyalIdentity/Models/Client.cs`, novo `RoyalIdentity/Models/ClientType.cs` (enum).

Passos:

1. Criar enum `ClientType` (`Public`, `Confidential`) e adicionar `Client.ClientType`. A validacao de consistencia com `RequireClientSecret`/secrets fica para o CRUD de client futuro (ver ADR-011).
2. Adicionar `AllowedIdentityScopes`, `AllowedResourceServers`, `AllowedScopes` (individuais) e `AllowAllResourceServers` (Full Scope Allowed); manter `AllowOfflineAccess` (flag).
3. Remover o `AllowedScopes` antigo (string-misto) e os `[Redesign]`.
4. Implementar a regra de permissao (scope permitido por `AllowAllResourceServers`, por `AllowedScopes`, ou pelo RS em `AllowedResourceServers`).

Criterio de aceite: client autoriza scopes por tipo; pedir scope de um RS permitido funciona sem listar cada scope; `AllowAllResourceServers` libera todos os RS sem alterar o conteudo do token; `ClientType` presente no modelo.

---

## Fase 5: Pipeline, Token e Audience

Arquivos provaveis: `ResourcesDecorator`, `ClientResourceDecorator`, `AuthorizationResourcesValidator`, `ResourcesValidator`, `ResourcesExtensions`, `DefaultTokenFactory`, `DefaultTokenClaimsService`, `DiscoveryHandler`, `UserInfoHandler`, `AuthorizationCode`/`RefreshToken`.

Passos:

1. Decorators/validators: filtrar por `AllowedResources` do client; aplicar `RequestedResources`.
2. Token: `scope` claim so com os pedidos; `aud` = `Audience` dos ResourceServers pedidos.
3. Signing-alg: aplicar a cadeia RS -> Client -> Realm (com ordem) em `ResourcesExtensions` + `IKeyManager`.
4. Discovery (`scopes_supported`) e UserInfo coerentes com o novo modelo.

Criterio de aceite: token de realms/clients diferentes reflete scopes/aud corretos; signing-alg respeita a precedencia.

---

## Fase 6: Consentimento

Arquivos provaveis: `DefaultConsentService`, `ConsentPageService`, `ConsentViewModel`/`ConsentInputModel`, `ConsentPage.razor`.

Passos:

1. ViewModel agrupado por **ResourceServer** (dados do RS + seus Scopes pedidos) + IdentityScopes + offline_access.
2. Razor: tela clara e agradavel, agrupando visualmente por ResourceServer.
3. Consentir/negar continua operando sobre os scopes efetivamente pedidos.

Criterio de aceite: a tela mostra os scopes agrupados por ResourceServer de forma clara; consentimento grava/aplica corretamente.

---

## Fase 7: Testes e Regressao

Passos:

1. Testes de modelo/store: lookup, copy, validacao, missing/disabled.
2. Testes de token: `scope` so com pedidos, `aud` a partir do RS, signing-alg pela precedencia.
3. Testes de autorizacao do client (AllowedResources) e de consent agrupado.
4. Isolamento por realm onde aplicavel.
5. `dotnet test RoyalIdentity.sln` verde.

Criterio de aceite final:

```powershell
dotnet test RoyalIdentity.sln --no-restore
```

---

## Como Marcar Progresso

- Uma fase so e concluida quando suas tarefas estiverem feitas e o criterio de aceite satisfeito.
- Ao concluir uma fase, atualizar `Progresso` no topo (`0 de 7`) e substituir um `-` por `█` na barra (barra de 7 segmentos, um por fase; mesmo caractere usado nos outros planos).

---

## Avaliacao do progresso

Apontamentos da revisao da execucao, por fase. Cada item: problema, solucao e `Status`.

`Status` pode ser: `avaliar`, `questionado`, `rejeitado`, `valido`, `corrigido`. Ao criar, usar `avaliar`.
