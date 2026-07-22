# Matriz: baseline dos contratos de storage do IdP

## Estado

Inventário estático produzido na Fase 1 de
[plan-data-storage-baseline.md](plan-data-storage-baseline.md), em 2026-07-21.

Esta versão descreve o código existente; não transforma automaticamente comportamento do fake em contrato durável.
As tabelas do inventário (Fases 1-2) registram a "classe inicial", incluindo os `avaliar` da época; **todas as
classificações foram fechadas na seção "Paridade final e ordem de migração — Fase 5"**, que prevalece sobre a
classe inicial em caso de divergência. As classificações `preservar`, `descartar` e `substituir` possuem fonte
normativa ou decisão fechada.

## Escopo e contagem

| Grupo | Superfícies | Membros inventariados |
|---|---:|---:|
| Gateway | `IStorage` | 11 |
| Configuração | `IRealmStore`, `IClientStore`, `IResourceStore`, `ResourceStoreExtensions`, `IKeyStore` | 19 |
| Operacional | `IAccessTokenStore`, `IRefreshTokenStore`, `IAuthorizationCodeStore`, `IUserConsentStore`, `IUserSessionStore`, `IAuthorizeParametersStore` | 24 |
| Infraestrutura adjacente | `IMessageStore`, `IReplayCache`, `IStorageProvider`, `IStorageSession` e `IDisposable.Dispose` herdado | 8 |
| **Total de operações/propriedades** | **15 contratos + 1 extensão** | **62** |
| Tipo público de resultado | `ResourceResolution` | 7 membros de suporte |

Ficam fora do catálogo `IUserDirectory`, `ISubjectStore` e demais portas de conta: pertencem à família
`RoyalIdentity.UserAccounts` (ADR-013/015 e DF8). Métodos privados das implementações também não contam como
superfície contratual.

## Legenda

- **Owner:** cada linha possui exatamente um entre `Configuration`, `Operational` e `Adapter/Infrastructure`. As
  superfícies explicitamente excluídas são `fora do storage`. `IStorage` é o gateway contratual, mas cada accessor é
  atribuído ao owner dos dados que expõe; a implementação do gateway pertence ao adapter.
- **Gateway:** neste documento, é exclusivamente o ponto de entrada contratual `IStorage`, que agrega propriedades e
  fábricas de facades (`Realms`, `AuthorizeParameters` e `Get*Store(realm)`) sem possuir os dados nem sua semântica de
  persistência. O termo não significa API gateway, gateway de integração externa, repository, Unit of Work ou a
  `.Integration` de um módulo. Cada membro de `IStorage` continua atribuído ao owner dos dados que expõe, e a
  implementação concreta desse gateway é responsabilidade do adapter de storage.
- **Lifecycle:** `Configuration` é estado durável de baixa rotatividade; `Operational` é estado transitório/de alta
  rotatividade, sujeito a expiração, revogação e purge; `Adapter/Infrastructure` tem lifetime técnico e não é dado de
  `Data.*`. Essa correspondência é normativa para todas as linhas e não depende do backing atual.
- **Binding:** `global`, `realm` ou `n/a`. Nos stores realm-bound, o fake resolve um `RealmMemoryStore` pelo `Realm.Id`;
  realm desconhecido lança `ArgumentException` ao obter o store.
- **Mutabilidade:** salvo indicação diferente, o fake guarda e devolve a mesma instância mutável. Isso é
  `descartar`: DF17 exige persistência explícita e permite nova materialização a cada leitura.
- **CT:** salvo indicação diferente, o fake aceita mas ignora o `CancellationToken`. Isso não é requisito do alvo;
  DF23 exige propagação em I/O real e não exige simulação de I/O no fake.
- **Ordem:** `conjunto` significa que nenhuma ordem é contratual, conforme DF24.
- **Cobertura:** `direta` exercita a operação/semântica do store; `fluxo` a alcança incidentalmente; `lacuna` significa
  que não foi encontrado teste relevante.
- **Classe:** `preservar`, `descartar`, `substituir` ou `avaliar` (classificação final da Fase 5).
- **Destino:** P2 = `plan-data-configuration-storage`; P3 = futuro `plan-data-operational-storage`; P4 = futuro
  `plan-data-test-migration`; `baseline` = contract tests deste plano; `adjacente` = fora de `Data.*`.

## Gateway `IStorage`

Implementação: `MemoryStorage`. `ServerOptions`, realms e authorize parameters usam estado global; os outros getters
criam stores ligados aos dicionários do `RealmMemoryStore` correspondente. `MemoryStorage` é singleton; a composição
registra `IStorage` transient apontando para essa mesma instância.

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| ST-01 | `ServerOptions { get; }` | Configuration / global / instância estática compartilhada | Retorna a mesma instância mutável. | `ConfigureRealmCookieAuthenticationOptions`, `RealmManager`, `DefaultEventDispatcher` | fluxo: testes de realm/options | ADR-013; live reference `descartar` (DF17); P2/baseline |
| ST-02 | `Realms { get; }` | Configuration / global / `realms` + `realmMemoryStore` | Cria nova facade `RealmStore` sobre os mesmos backings. | todos os callers de `IRealmStore` | direta: `RealmIsolationTests` | ADR-013; facade `preservar`; P2/baseline |
| ST-03 | `AuthorizeParameters { get; }` | Operational / global / `authorizeParameters` | Cria nova facade sobre o mesmo dictionary global. | responses/page services do authorize flow | fluxo: login/consent callbacks | DF14; facade `preservar`; P3/baseline |
| ST-04 | `GetAccessTokenStore(Realm)` | Operational / realm / `AccessTokens` | Nova facade; realm inexistente falha antes da operação. | token factory/validator, refresh e revocation handlers | direta: `RealmIsolationTests`; fluxo: token/revocation | DF6; binding `preservar`; P3/baseline |
| ST-05 | `GetRefreshTokenStore(Realm)` | Operational / realm / `RefreshTokens` | Idem. | token factory, `LoadRefreshToken`, refresh/revocation/session revocation | direta/fluxo: `RealmIsolationTests`, `RefreshTokenTests`, `SessionLifecycleTests` | DF6; binding `preservar`; P3/baseline |
| ST-06 | `GetAuthorizationCodeStore(Realm)` | Operational / realm / `AuthorizationCodes` | Idem. | `DefaultCodeFactory`, `LoadCode` | direta/fluxo: `RealmIsolationTests`, `CodeTokenTests` | DF6; binding `preservar`; P3/baseline |
| ST-07 | `GetUserConsentStore(Realm)` | Operational / realm / `UserConsents` | Idem. | `DefaultConsentService` | direta/fluxo: `RealmIsolationTests` e authorize/consent | DF6; binding `preservar`; P3/baseline |
| ST-08 | `GetKeyStore(Realm)` | Configuration / realm / `KeyParameters` | Idem. | `DefaultKeyManager` | fluxo: signing/JWKS/key jobs | DF6/DF9; binding `preservar`; P2/baseline |
| ST-09 | `GetClientStore(Realm)` | Configuration / realm / `Clients` | Idem. | middleware, validators, secret evaluators, sign-out | direta: `RealmIsolationTests`; fluxo: endpoints | DF6; binding `preservar`; P2/baseline |
| ST-10 | `GetResourceStore(Realm)` | Configuration / realm / resources/scopes | Nova facade e novos índices derivados do estado corrente. | discovery, client/resource decorator, code/refresh handlers | direta: `ResourceStoreTests`, `RealmIsolationTests` | DF6/DF22; binding `preservar`, persistência bloqueada; baseline/redesign |
| ST-11 | `GetUserSessionStore(Realm)` | Operational / realm / `UserSessions` | Nova facade ligada ao realm. | session service, code/sign-out/end-session/session revocation | direta/fluxo: suites de sessão/logout | ADR-014/DF6; binding `preservar`; P3/baseline |

## Configuração

### `IRealmStore`

Implementação: `RealmStore`. Comparações atuais de id usam a chave do dictionary; path/domain usam `==`. As leituras
retornam live references e, exceto a enumeração, ignoram CT.

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| RL-01 | `GetByPathAsync(path, ct)` | Configuration / global / scan de `realms.Values` | Primeiro match exato ou `null`; ordem incidental se houver duplicidade. | realm discovery; `RealmManager.CreateAsync` | fluxo: realm discovery/options | isolamento/product; ausência/comparador `avaliar` (DF18/DF25); P2/baseline |
| RL-02 | `GetByPath(path)` | Configuration / global / mesmo scan | Versão síncrona do lookup anterior. | cookie authentication options | fluxo: autenticação/cookie | `substituir` por source async + snapshot síncrono sem I/O (DF23; DF7 do P2); P2 |
| RL-03 | `GetByIdAsync(id, ct)` | Configuration / global / chave de `realms` | Match exato ou `null`. | `KeyCacheEntry`, `DefaultSignOutManager`, `RealmManager` | direta/fluxo: `RealmIsolationTests`, realm/session | isolamento/product; ausência/comparador `avaliar`; P2/baseline |
| RL-04 | `GetByDomainAsync(domain, ct)` | Configuration / global / scan de `realms.Values` | Primeiro match exato ou `null`; ordem incidental se duplicado. | `RealmManager.CreateAsync` (unicidade) | fluxo: criação de realm; sem teste direto localizado | unicidade é premissa do caller; comparador/ausência `avaliar`; P2/baseline |
| RL-05 | `GetAllAsync(ct)` | Configuration / global / `realms.Values` | Conjunto de live references; observa cancelamento entre itens e encerra normalmente. | `FirstKeyJob` | fluxo: inicialização de keys | DF24: ordem incidental `descartar`; cancelamento/resultado `avaliar`; P2/baseline |
| RL-06 | `SaveAsync(realm, ct)` | Configuration / global / `AddOrUpdate` + `TryAdd` do backing do realm | Upsert/replace do `Realm`; criar inicializa backing vazio, atualizar preserva o backing existente. | `RealmManager`; setup de testes/options | direta/fluxo: realm/options/isolation | duplicate-write `avaliar` por operação (DF16); live reference `descartar`; P2/baseline |
| RL-07 | `DeleteAsync(realmId, ct)` | Configuration / global / realm + backing operacional | `false` se ausente/interno; para realm comum remove fisicamente realm e todo `RealmMemoryStore`. É uma operação Configuration com efeito cross-store Operational e não alcança `UserAccounts`. | somente testes; nenhum caller de produção ou `IRealmManager.DeleteAsync` | direta: dois cenários em `RealmIsolationTests` | proteção de internal `preservar`; hard delete config `substituir` por tombstone + purge Operational (DF20); seam cross-family pendente; P2/P3/plano admin |

### `IClientStore`

Implementação: `ClientStore`, sobre `RealmMemoryStore.Clients`.

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| CL-01 | `FindClientByIdAsync(clientId, ct)` | Configuration / realm / chave de `Clients` | Match exato ou `null`, mesmo que disabled. | `DefaultSignOutManager` | direta: `RealmIsolationTests`; fluxo: sign-out | distinção any/enabled expressa na API; comparador/ausência `avaliar`; P2/baseline |
| CL-02 | `FindEnabledClientByIdAsync(clientId, ct)` | Configuration / realm / chave de `Clients` | Retorna somente se encontrado e `Enabled`; caso contrário `null`. | `LoadClient`, CORS middleware, token validator, secret evaluators | direta: `RealmIsolationTests`; fluxo: endpoints | regra enabled `preservar` pelo uso de segurança; comparador `avaliar`; P2/baseline |

### `IResourceStore` e extensão

Implementação: `ResourceStore`. O construtor cria índices `StringComparer.Ordinal`; lança para scope duplicado, URI
duplicada e URI inválida. O shape está bloqueado para persistência por DF22.

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| RS-01 | `GetAllResourcesAsync(ct)` | Configuration / realm / identity scopes + resource servers | Retorna todos como conjuntos; models são live references. | nenhum caller localizado | lacuna | ordem/live reference `descartar` (DF17/DF24); restante `avaliar`; baseline/redesign |
| RS-02 | `GetAllEnabledResourcesAsync(ct)` | Configuration / realm / mesmos backings | Filtra identity scopes, servers e scopes enabled; clona cada server para filtrar scopes; conjuntos. | `DiscoveryHandler`, `ClientResourceDecorator` | direta: `ResourceStoreTests`; fluxo: discovery/authorize | filtro enabled `preservar` (ADR-010 e consumers); ordem `descartar`; baseline/redesign |
| RS-03 | `FindResourcesByScopeAsync(scopes, onlyEnabled, ct)` | Configuration / realm / índices por nome | Delega ao lookup combinado sem resource URI. | `UserInfoHandler`; `Tests.Host/HostEndpoints`; setup e cenários de testes | direta: `ResourceStoreTests`; fluxo: userinfo/code/signing/isolation | semântica atual coberta por ADR-010; API/shape `avaliar`, persistência bloqueada; baseline/redesign |
| RS-04 | `FindRequestedResourcesAsync(scopes, uris, onlyEnabled, ct)` | Configuration / realm / índices Ordinal | Resolve identity/resource scopes; trata `offline_access`; registra scopes ausentes e targets inválidos/disabled; resource URI deve ser absoluta HTTPS sem fragment, com exceção HTTP localhost; deduplica owners. | `RefreshTokenHandler`, `ProtectedResourceMetadataHandler`, `ResourcesDecorator`, `ClientResourceDecorator` e RS-05 | direta: `ResourceStoreTests`; fluxo: token grants/metadata | validação atual `preservar` enquanto o contrato existir (ADR-010/012); shape bloqueado DF22; baseline/redesign |
| RS-05 | `ResolveAuthorizedSubsetAsync(...)` | Configuration / realm por receiver / sem backing próprio | Garante subset de resource indicators, downscope coerente e mapeia falhas para `invalid_target`/`invalid_scope`; pode chamar RS-04 até três vezes. | `AuthorizationCodeHandler`, `RefreshTokenHandler` | direta/fluxo: `CodeTokenTests`, `RefreshTokenTests`, `ResourceStoreTests` | semântica `preservar` (ADR-012/RFC 8707 já adotada); baseline/redesign |

### `IKeyStore`

Implementação: `KeyStore`, sobre `RealmMemoryStore.KeyParameters`.

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| KY-01 | `AddKeyAsync(key, ct)` | Configuration / realm / chave `KeyId` | Indexação substitui qualquer valor de mesmo id. | `DefaultKeyManager`/`FirstKeyJob`; setup de signing | fluxo: `SigningAlgorithmTests` e jobs | política duplicate-write `avaliar` (DF16); live reference `descartar`; P2/baseline |
| KY-02 | `ListAllCurrentKeysIdsAsync(now, ct)` | Configuration / realm / values | Inclui `NotBefore <= now && Expires >= now`; default UTC now; ordena por `Created`. | `DefaultKeyManager` | fluxo: key/signing/JWKS; sem bordas diretas localizadas | disponibilidade e ordem cronológica `preservar` (product); bordas de tempo a fechar DF19; P2/baseline |
| KY-03 | `ListAllKeysIdsAsync(now, ct)` | Configuration / realm / values | Exclui apenas keys futuras (`NotBefore > now`); inclui expiradas; ordena por `Created`. | `DefaultKeyManager` para validação | fluxo: key/signing/JWKS | retenção histórica `preservar` (product); P2/baseline |
| KY-04 | `GetKeyAsync(keyId, ct)` | Configuration / realm / chave de key | Retorna live reference; ausente lança `ArgumentException`, único outlier entre lookups simples. | `DefaultKeyManager` | fluxo: signing; lacuna para ausência | histórico é normativo; exceção de ausência `avaliar` explicitamente (DF25); P2/baseline |
| KY-05 | `GetKeysAsync(keyIds, ct)` | Configuration / realm / KY-04 repetido | Preserva ordem de entrada; qualquer key ausente propaga `ArgumentException`. | `DefaultKeyManager` | fluxo: signing/validation; sem teste direto localizado | ordem pode decorrer da correspondência solicitada; ausência/partial result `avaliar`; P2/baseline |

## Operacional

### `IAccessTokenStore`

Implementação: `AccessTokenStore`, sobre `RealmMemoryStore.AccessTokens`.

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| AT-01 | `StoreAsync(token, ct)` | Operational / realm / chave `token.Id` | `TryAdd`; colisão é ignorada e o id é retornado como se tivesse salvo. | `DefaultTokenFactory` | fluxo: token issuance; isolation direto | duplicate-write/retorno `avaliar` (DF16); live reference `descartar`; P3/baseline |
| AT-02 | `GetAsync(jti, ct)` | Operational / realm / chave de token | Match exato ou `null`; não filtra expiração/tipo. | `DefaultTokenValidator` | fluxo: validation; isolation direto | ausência/comparador/expiração `avaliar` (DF18/19/25); P3/baseline |
| AT-03 | `RemoveAsync(jti, ct)` | Operational / realm / chave de token | Remove se existe; ausência é no-op. | `RefreshTokenHandler`, `RevocationHandler` | fluxo: refresh/revocation; isolation direto | idempotência de remoção `avaliar` (DF16/25); P3/baseline |
| AT-04 | `RemoveReferenceTokensAsync(subjectId, clientId, ct)` | Operational / realm / scan snapshot | Remove tokens do tipo Reference com subject/client exatos; ausência é no-op. | `RevocationHandler` | fluxo limitado; sem contrato direto localizado | revogação realm-scoped é normativa; filtro/idempotência `avaliar`; P3/baseline |

### `IRefreshTokenStore`

Implementação: `RefreshTokenStore`, sobre `RealmMemoryStore.RefreshTokens`.

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| RT-01 | `StoreAsync(token, ct)` | Operational / realm / chave `token.Token` | `TryAdd`; resultado e colisão são ignorados. | `DefaultTokenFactory` | fluxo: `RefreshTokenTests`; isolation direto | duplicate-write `avaliar` (DF16); live reference `descartar`; P3/baseline |
| RT-02 | `GetAsync(token, ct)` | Operational / realm / chave de token | Match exato ou `null`; não filtra expiração/consumo. | `LoadRefreshToken`, refresh/revocation handlers | direta/fluxo: `RefreshTokenTests`, `RealmIsolationTests` | tolerância de consumo é normativa, mas ausência/expiração são `avaliar`; P3/baseline |
| RT-03 | `UpdateAsync(token, ct)` | Operational / realm / `TryUpdate(token.Token, token, token)` | No padrão get→mutate→update, compara e grava a mesma referência; o CAS sucede trivialmente e o resultado é ignorado. Não protege concorrência. | `RefreshTokenHandler` | fluxo: refresh; sem concorrência real | `substituir` por transição condicional/atômica (DF15); P3 |
| RT-04 | `RemoveAsync(token, ct)` | Operational / realm / chave de token | Remove se existe; ausência é no-op. | `RevocationHandler` | fluxo: revocation/refresh | idempotência `avaliar` (DF16/25); P3/baseline |
| RT-05 | `RemoveBySubjectAsync(subjectId, ct)` | Operational / realm / scan snapshot | Remove todos com subject exato e retorna a contagem; repetição retorna zero. | `DefaultSessionRevocationService` | direta/fluxo: `SessionLifecycleTests` | seam de revogação `preservar` (ADR-017); comparação/contagem `avaliar`; P3/baseline |

### `IAuthorizationCodeStore`

Implementação: `AuthorizationCodeStore`, sobre `RealmMemoryStore.AuthorizationCodes`.

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| AC-01 | `StoreAuthorizationCodeAsync(code, ct)` | Operational / realm / chave `code.Code` | Indexação substitui valor de mesmo código e retorna o código. | `DefaultCodeFactory` | fluxo: `CodeTokenTests`; isolation direto | duplicate-write `avaliar` (DF16); live reference `descartar`; P3/baseline |
| AC-02 | `GetAuthorizationCodeAsync(code, ct)` | Operational / realm / chave de code | Match exato ou `null`; não consome nem filtra expiração. | `LoadCode` | fluxo: `CodeTokenTests`; isolation direto | get+remove como consumo `substituir` por operação atômica (DF15); P3 |
| AC-03 | `RemoveAuthorizationCodeAsync(code, ct)` | Operational / realm / chave de code | Remove se existe; ausência é no-op. Separado de AC-02. | `LoadCode`, depois do get | fluxo: `CodeTokenTests` | separação do consumo `substituir` (DF15); remoção administrativa isolada `avaliar`; P3 |

### `IUserConsentStore`

Implementação: `UserConsentStore`; chave atual é concatenação `subjectId + "." + clientId`.

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| CN-01 | `StoreUserConsentAsync(consent, ct)` | Operational / realm / chave concatenada subject+client | `AddOrUpdate`/upsert e live reference; concatenação pode colidir se os componentes aceitarem ponto. | `DefaultConsentService` | fluxo/direta: consent + `RealmIsolationTests` | identidade realm+subject+client+scope `preservar` (product); encoding/upsert `avaliar`; P3/baseline |
| CN-02 | `GetUserConsentAsync(subjectId, clientId, ct)` | Operational / realm / chave concatenada | Match ou `null`; não filtra lifetime/expiração. | `DefaultConsentService` | fluxo/direta: consent + isolation | ausência/comparadores/expiração `avaliar`; P3/baseline |
| CN-03 | `RemoveUserConsentAsync(subjectId, clientId, ct)` | Operational / realm / chave concatenada | Remove se existe; ausência é no-op. | `DefaultConsentService` | fluxo: consent; isolation | idempotência/retorno `avaliar` (DF16/25); P3/baseline |

### `IUserSessionStore`

Implementação: `UserSessionStore`, sobre `RealmMemoryStore.UserSessions`. A interface vive em `Users/Contracts`, mas
é parte de `IStorage` e pertence ao operacional (ADR-014).

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| SS-01 | `CreateAsync(session, ct)` | Operational / realm / chave `session.Id` | Indexação substitui sessão de mesmo id e retorna a mesma instância. | `DefaultUserSessionService` | direta/fluxo: `DefaultUserSessionServiceTests`, characterization/isolation | persistência de sessão `preservar`; duplicate-write `avaliar`; live reference `descartar`; P3/baseline |
| SS-02 | `FindByIdAsync(sessionId, ct)` | Operational / realm / chave de sid | Match exato ou `null`; não filtra atividade/expiração. | session service, code/sign-out/end-session | ampla: suites de sessão/logout | lookup puro `preservar` (ADR-014); expiração/ausência/comparador `avaliar`; P3/baseline |
| SS-03 | `RecordClientAsync(sessionId, clientId, ct)` | Operational / realm / sessão + lista de clients | Ausente é no-op; deduplica client exato, preserva `FirstSeenAt` e atualiza `LastSeenAt` pelo relógio. | `DefaultUserSessionService` | direta: session service/characterization | deduplicação `preservar` (ADR-014); ausência/relógio/comparador `avaliar`; P3/baseline |
| SS-04 | `EndAsync(sessionId, ct)` | Operational / realm / sessão | Ausente retorna `null`; existente recebe `IsActive=false` e é retornado; repetição retorna inativo. | `DefaultUserSessionService`, `EndSessionHandler`, `DefaultSignOutManager` | ampla: session/logout/end-session | encerramento explícito `preservar` (ADR-014/017); idempotência/retorno `avaliar`; P3/baseline |
| SS-05 | `TouchAsync(sessionId, lastSeenAt, expiresAt, ct)` | Operational / realm / sessão | Ausente é no-op; existente recebe os dois tempos informados. | `DefaultUserSessionService` | direta: idle-touch em session tests | touch/throttle no caller `preservar` (ADR-017); ausência `avaliar`; P3/baseline |
| SS-06 | `EndSessionsForSubjectAsync(subjectId, exceptSid, ct)` | Operational / realm / scan snapshot | Encerra apenas ativas do subject exato, preserva sid opcional e retorna contagem; repetição retorna zero. | `DefaultSessionRevocationService` | direta/fluxo: `SessionLifecycleTests` | revogação idempotente `preservar` (ADR-017); comparação/contagem `avaliar`; P3/baseline |

### `IAuthorizeParametersStore`

Implementação: `AuthorizeParametersStore`, sobre dictionary global de `NameValueCollection`.

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| AP-01 | `WriteAsync(parameters, ct)` | Operational / global / random handle de 16 bytes (`CryptoRandom.CreateUniqueId(16)`, Base64Url, ~22 chars) | `TryAdd` de live reference e retorna o handle; colisão, embora improvável, é ignorada. | `LoginPageResult`, `ConsentPageResult` | fluxo: authorize/login/consent | estado server-side `preservar` (DF14); handle/duplicidade/mutabilidade `avaliar`; P3/baseline |
| AP-02 | `ReadAsync(id, ct)` | Operational / global / chave de handle | Match ou `null`; leitura não consome e retorna a mesma collection. | `DefaultAuthorizationContextResolver`, callback | fluxo: authorize/login/consent | leitura repetida requerida pelo fluxo atual; ausência `avaliar`; live reference `descartar`; P3/baseline |
| AP-03 | `DeleteAsync(id, ct)` | Operational / global / chave de handle | Remove se existe; ausência é no-op. | authorize callback após leitura | fluxo: authorize/login/consent | cleanup `preservar` no fluxo; atomicidade/idempotência/expiração `avaliar`; P3/baseline |

## Infraestrutura adjacente

### `IMessageStore`

Implementação atual: `ProtectedDataMessageStore`. Não há backing de registros: o identificador contém o payload
serializado e protegido por ASP.NET Data Protection.

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| MS-01 | `WriteAsync<T>(message, ct)` | Adapter/Infrastructure / n/a / Data Protection | Serializa JSON, protege bytes e devolve ciphertext Base64Url completo; CT sem efeito. | end-session handler; login/consent/end-session page services | fluxo: `EndSessionTests`, back-channel logout | classificação infra `preservar` (DF14); formato/mutabilidade `avaliar`; adjacente/baseline |
| MS-02 | `ReadAsync<T>(id, ct)` | Adapter/Infrastructure / n/a / Data Protection | Base64Url decode→unprotect→deserialize; qualquer falha é logada e retorna `null`; leitura repetível. | sign-out e page services | fluxo: end-session/logout; sem teste direto de tamper | fail-closed como ausência `avaliar`; adjacente/baseline |
| MS-03 | `DeleteAsync(id, ct)` | Adapter/Infrastructure / n/a / sem backing | No-op incondicional. | page/flow cleanup quando aplicável | lacuna direta | no-op é específico da implementação e `avaliar`; adjacente/baseline |

### `IReplayCache`

Implementações: `DefaultReplayNoCache` (default DI) e `DefaultReplayDistributedCache` (opcional).

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| RC-01 | `AddAsync(purpose, handle, expiration)` | Adapter/Infrastructure / global / no-op ou `IDistributedCache` | Default não grava. Distribuído grava bytes vazios em `Prefix + purpose + handle`, sem delimitador, com expiração absoluta; API não recebe CT. | `PrivateKeyJwtSecretEvaluator` | lacuna relevante | proteção contra replay é regra de segurança; API/backing/atomicidade `avaliar`; adjacente/baseline |
| RC-02 | `ExistsAsync(purpose, handle)` | Adapter/Infrastructure / global / constante false ou cache | Default sempre `false`; distribuído faz `GetAsync`. Check+add do caller não é atômico. | `PrivateKeyJwtSecretEvaluator` | lacuna relevante | implementação default não oferece proteção; `substituir`/decidir operação atômica em plano próprio; adjacente |

### `IStorageProvider` e `IStorageSession`

`StorageProvider` implementa ambas as interfaces e é singleton.

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| SP-01 | `IStorageProvider.CreateSession()` | Adapter/Infrastructure / n/a / próprio singleton | Retorna `this` como `IStorageSession`; não abre conexão, contexto ou transação. | `KeyCacheEntry` | lacuna direta | lifetime seam `preservar`, não UoW global (DF21); implementação `substituir` no adapter EF; P2/adapter |
| SP-02 | `IStorageSession.GetStorage()` | Adapter/Infrastructure / n/a / `MemoryStorage` singleton | Retorna sempre o mesmo `IStorage`. | `KeyCacheEntry` | lacuna direta | acesso dentro do lifetime `preservar` (DF21); implementação `substituir`; P2/adapter |
| SP-03 | `IDisposable.Dispose()` | Adapter/Infrastructure / n/a / no-op | Não libera recurso. | `using` em `KeyCacheEntry` | lacuna direta | disposal/lifetime `preservar` (DF21); no-op do fake `descartar`; P2/adapter |

## Tipo de suporte `ResourceResolution`

Esses membros não persistem dados, mas fazem parte da superfície pública introduzida pela extensão RS-05.

| ID | Membro público | Owner / lifecycle | Semântica atual | Consumidores/cobertura | Fonte, classe inicial e destino |
|---|---|---|---|---|---|
| RR-01 | `Resources { get; }` | Configuration / resultado transitório | Resultado resolvido quando há sucesso; `null` em falha. | code/refresh handlers; tests desses grants | ADR-012; `preservar` enquanto RS-05 existir; baseline/redesign |
| RR-02 | `Error { get; }` | Configuration / resultado transitório | `null` em sucesso; erro OAuth em falha. | idem | ADR-012; `preservar`; baseline/redesign |
| RR-03 | `ErrorDescription { get; }` | Configuration / resultado transitório | Descrição estável criada pela extensão. | idem | texto exato `avaliar`; baseline/redesign |
| RR-04 | `Detail { get; }` | Configuration / resultado transitório | Lista/razão opcional para diagnóstico. | idem | conteúdo/formato `avaliar`; baseline/redesign |
| RR-05 | `IsSuccess { get; }` | Configuration / resultado transitório | Verdadeiro quando `Error is null`. | handlers/testes | coerência do resultado `preservar`; baseline/redesign |
| RR-06 | `Ok(resources)` | Configuration / resultado transitório | Constrói sucesso sem campos de erro. | RS-05 | coerência do resultado `preservar`; baseline/redesign |
| RR-07 | `Fail(error, description, detail)` | Configuration / resultado transitório | Constrói falha sem resources. | RS-05 | coerência do resultado `preservar`; baseline/redesign |

## Backings do fake e dependências

| Backing | Store(s) | Escopo | Observação verificada |
|---|---|---|---|
| `MemoryStorage.serverOptions` | ST-01 | global estático | Instância mutável compartilhada. |
| `MemoryStorage.realms` | ST-02, `IRealmStore` | global estático | Registro configuracional dos realms. |
| `MemoryStorage.realmMemoryStore` | ST-04..ST-11 | por `Realm.Id` | Contém todos os dictionaries config/operational do core; é removido inteiro por RL-07. |
| `MemoryStorage.authorizeParameters` | ST-03, AP-* | global estático | Não está particionado por realm; o handle aleatório é a fronteira atual. |
| `RealmMemoryStore.Clients` | `IClientStore` | realm | Configuração. |
| `RealmMemoryStore.IdentityScopes` / `ResourceServers` | `IResourceStore` | realm | Configuração com shape bloqueado por DF22. |
| `RealmMemoryStore.KeyParameters` | `IKeyStore` | realm | Configuração temporária até futuro KMS. |
| `RealmMemoryStore.AccessTokens` | `IAccessTokenStore` | realm | Operacional. |
| `RealmMemoryStore.RefreshTokens` | `IRefreshTokenStore` | realm | Operacional. |
| `RealmMemoryStore.AuthorizationCodes` | `IAuthorizationCodeStore` | realm | Operacional. |
| `RealmMemoryStore.UserConsents` | `IUserConsentStore` | realm | Operacional. |
| `RealmMemoryStore.UserSessions` | `IUserSessionStore` | realm | Operacional. |
| ASP.NET Data Protection | `IMessageStore` | infraestrutura | Ciphertext é o próprio handle; não há registro server-side. |
| `IDistributedCache` opcional | `IReplayCache` | infraestrutura | Default efetivo continua sendo o no-cache. |
| Persistência de `UserAccounts` | nenhuma superfície deste catálogo | família separada | RL-07 não a alcança; integração futura deve respeitar ADR-013/015 e DF20. |

## Classificação de ownership e lifecycle — Fase 2

A classificação usa o owner dos dados/efeito principal, não o projeto onde o contrato está declarado. Por isso os
accessors de `IStorage` não recebem um owner artificial chamado “Gateway”: cada um segue a família que expõe. O
adapter implementa o gateway, mas não se torna owner dos registros.

| Owner único | IDs | Lifecycle | Destino arquitetural |
|---|---|---|---|
| Configuration | ST-01, ST-02, ST-08..ST-10; RL-01..RL-07; CL-01..CL-02; RS-01..RS-05; KY-01..KY-05 | Durável, baixa rotatividade; tombstones configuracionais sobrevivem à exclusão lógica. Keys ficam aqui temporariamente até existir KMS. | `RoyalIdentity.Data.Configuration`, adaptado somente por `RoyalIdentity.Storage.EntityFramework`; resources permanecem bloqueados por DF22. |
| Operational | ST-03..ST-07, ST-11; AT-01..AT-04; RT-01..RT-05; AC-01..AC-03; CN-01..CN-03; SS-01..SS-06; AP-01..AP-03 | Transitório/alta rotatividade; possui consumo, revogação, expiração, retenção e purge próprios por tipo. | `RoyalIdentity.Data.Operational`, adaptado somente por `RoyalIdentity.Storage.EntityFramework`. |
| Adapter/Infrastructure | MS-01..MS-03; RC-01..RC-02; SP-01..SP-03 | Lifetime técnico, criptográfico, cache ou de acesso ao adapter; não constitui registro de `Data.*`. | Implementações/decorators de infraestrutura adjacente e lifecycle do `Storage.EntityFramework`. |
| Configuration (resultado não persistido) | RR-01..RR-07 | Objetos transitórios de resposta da resolução de configuração; não são entidades. | Construídos pelo adapter/core a partir do resource store; bloqueados com RS-05 pelo redesign. |
| fora do storage | `IUserDirectory`, `ISubjectStore`, `ILocalUserAuthenticator`, `IUserClaimsProvider`, `IUserSecurityStateProvider` e tipos de conta | Lifecycle próprio do módulo de contas. Nenhuma linha contratual incluída na contagem 62 pertence aqui. | `RoyalIdentity.UserAccounts` e sua `.Integration`; nunca `Data.Configuration`/`Data.Operational`. |

Contagem de controle das 62 linhas contratuais: 24 `Configuration`, 30 `Operational` e 8
`Adapter/Infrastructure`. Não existe linha com owner `Gateway`, owner composto ou `a definir`. Os sete membros RR são
suporte público de Configuration e ficam fora da contagem 62, como já indicado no inventário.

## Dependências cross-store

“Direta” abaixo significa que a própria operação consulta/muta outro store. “Orquestrada” significa que o caller
combina stores, sem transferir a regra de negócio ou a transação para uma facade individual. Isso evita interpretar
referências como `ClientId`/`SubjectId` dentro dos models como ownership ou FK obrigatória entre famílias.

| Operações | Dependência observada ou alvo | Cruza Configuration×Operational? | Consequência para os planos seguintes |
|---|---|---|---|
| ST-01, ST-02; RL-01..RL-06 | Somente estado Configuration global. RL-06 cria também o container operacional vazio no fake, mas isso é inicialização do backing, não semântica pública de `SaveAsync`. | Não como contrato; o acoplamento de inicialização do fake deve ser descartado. | P2 persiste configuração; readiness Operational é responsabilidade de composição/adapter, sem ampliar `SaveAsync`. |
| ST-08..ST-10; CL-*, RS-*, KY-* | O getter valida a existência do realm; depois, cada operação permanece dentro de Configuration. `FirstKeyJob` encadeia realms→keys e discovery/client flows encadeiam realms/clients/resources. | Não; são dependências internas à mesma família. | Podem compartilhar unidade transacional somente quando uma operação explícita do mesmo store assim exigir; DF21 não cria UoW global. |
| ST-03; AP-* | Estado Operational global no contrato atual, sem lookup direto de realm/configuração. O authorize flow liga o handle ao contexto da request fora do store. | Não diretamente. | P3 deve decidir particionamento/expiração sem inferir realm binding inexistente hoje. |
| ST-04..ST-07, ST-11 | A obtenção de cada store Operational exige um `Realm` já resolvido e um backing cadastrado para seu `Realm.Id`. | **Sim, no binding:** Configuration identifica o realm; Operational contém os registros. | O adapter deve preservar isolamento por realm sem expor entidade Configuration ao projeto Operational nem pressupor transação distribuída. |
| AT-*, RT-*, AC-*, CN-*, SS-* | Após o binding, nenhuma implementação atual consulta outro store. `ClientId`, `SubjectId`, `SessionId` e `RealmId` são valores de correlação, não navegações para conta/configuração. | Não dentro da operação; pode existir na orquestração do caller. | Queries continuam realm-scoped. Nenhuma FK ou referência a entidade do core/`UserAccounts` é decidida neste baseline. |
| AC-02/AC-03 + RS-05 + AT-/RT-* | Code/refresh handlers combinam consumo Operational, resolução Configuration de resources e emissão Operational. | **Sim, orquestrada.** | Atomicidade de consumo fica na operação condicional Operational (DF15); não há transação Configuration×Operational. A configuração é lida para validar/projetar. |
| RT-02/RT-04 + AT-03/AT-04 | `RevocationHandler` combina refresh e access tokens. | Não; somente Operational. | P3 define idempotência e consistência dentro das operações explícitas. |
| SS-06 + RT-05 | `DefaultSessionRevocationService` encerra sessões e remove refresh tokens do subject. | Não; somente Operational, mas são dois stores. | Orquestração continua explícita; DF21 não promete transação global. |
| CL-01 + SS-* + MS-* | Sign-out resolve client Configuration, lê/muta sessão Operational e usa message infrastructure. | **Sim, orquestrada**, mais infraestrutura adjacente. | Falha/retomada do fluxo não deve ser escondida em `IStorageSession`; sem UoW distribuída. |
| RL-07 | Hoje remove realm Configuration e o container com todos os stores Operational do fake. Alvo: tombstone Configuration + purge Operational. Depois, a família `UserAccounts` deve remover suas próprias contas. | **Sim, direta hoje e coordenada no alvo.** | O contrato permanece owner de Configuration; P2 implementa tombstone, P3 implementa purge. O seam administrativo cross-family e a semântica de conclusão exigem decisão futura própria. |
| SP-01..SP-03 + RL-03 | `KeyCacheEntry` abre um lifetime de adapter e relê realm Configuration para calcular TTL. | Não entre famílias de dados; cruza Infrastructure→Configuration. | O adapter EF pode possuir contexts/scopes descartáveis, mas não uma transação implícita entre `Data.*` (DF21). |
| MS-*, RC-* | Não dependem de `Data.Configuration` nem `Data.Operational`; seus callers podem participar de fluxos que usam ambos. | Não na operação. | Permanecem infraestrutura adjacente (DF14); não criar tabelas em `Data.*` por efeito deste plano. |

As demais linhas são operações locais do mesmo store e não têm dependência cross-store adicional além do binding já
descrito. Em particular, o store não valida a existência de client ou conta ao persistir um identificador recebido;
essa validação, quando necessária, pertence aos callers e não autoriza acoplamento entre projetos de dados.

## Tipos do core e responsabilidade de mapping

Os tipos abaixo atravessam as facades atuais, mas **não** podem ser usados como entidades em `Data.*`, pois esses
projetos não referenciam o core. `RoyalIdentity.Storage.EntityFramework` materializa/projeta entre os modelos puros de
persistência e os tipos do contrato. Esta lista identifica raízes e objetos de resultado; não define schema, tabela,
chave, navegação ou estratégia de serialização.

| Owner | Tipos do core/BCL vistos pelas facades | Responsabilidade futura |
|---|---|---|
| Configuration | `ServerOptions`; `Realm` e seu grafo `RealmOptions`/routes; `Client` e seu grafo de secrets, claims, URIs, grants e restrições; `KeyParameters` | P2 cria entidades puras próprias e o adapter faz mapping nos dois sentidos. Referências entre esses grafos não autorizam `Data.Configuration` a referenciar assemblies do core. |
| Configuration — bloqueado | `ResourceServer`, `IdentityScope`, `Scope`, `ProtectedResource`, containers `AllScopes`/`RequestedResources` e `ResourceResolution` | Os containers/resultados são construídos, não persistidos. Entidades para resources/scopes não serão desenhadas nem implementadas até o redesign fechar o modelo (DF11/DF22). |
| Operational | `AccessToken`, `RefreshToken`, `AuthorizationCode` e o grafo comum de token/claims/audiences/scopes; `Consent`/`ConsentedScope`; `UserSession`/`UserSessionClient`; `NameValueCollection` de authorize parameters | P3 define modelos puros e mappings. Claims/collections/tempos exigem projeção explícita, mas o formato físico permanece fora deste baseline. |
| Adapter/Infrastructure | `Message<T>`, `IStorage`, `IStorageSession`; primitivas de replay (`purpose`, `handle`, `expiration`) | Não gerar entidades de `Data.*`. Message store, replay cache e lifetime são implementações/decorators adjacentes. |
| fora do storage | `Subject` e tipos/agregados/credenciais/propriedades de `RoyalIdentity.UserAccounts` | Não mapear no `Storage.EntityFramework`; somente a `.Integration` do módulo traduz a borda core-owned para o módulo. |

Os identificadores `SubjectId` presentes em consent, sessão e tokens são valores protocolares de correlação. Eles não
transferem ownership da conta ao operacional, não autorizam navegação EF para `UserAccounts` e não implicam FK entre
DbContexts. A mesma regra vale para `ClientId` e `RealmId` entre Operational e Configuration: integridade cross-family
é tratada por validação/orquestração, não por um modelo compartilhado decidido aqui.

## Bloqueios e fronteiras

### Resources/scopes

RS-01..RS-05 e RR-01..RR-07 têm owner Configuration, mas destino `baseline/redesign`, não P2 implementável. O
inventário atual pode gerar contract tests do comportamento vigente; não pode gerar entidade, mapping ou migration.
O desbloqueio exige fechar o redesign de `Client.AllowedScopes`/`AllowOfflineAccess` e da hierarquia de resource
servers, protected resources, scopes e identity scopes (DF11/DF22). Realms/options, clients e keys não ficam
bloqueados junto com esse grafo.

### Exclusão de realm

RL-07 já é executável e testado no fake, mas não tem caller de produção. Seu owner único é Configuration; o efeito
cross-family não muda esse ownership. O alvo fechado por DF20 é:

1. recusar realm interno e tratar ausência conforme a semântica final ainda a fechar em DF25;
2. criar tombstone Configuration permanente, invisível a lookups normais, reservando path e domain;
3. impedir que o realm excluído atenda novas requests;
4. purgar fisicamente os dados Operational do realm;
5. solicitar que `UserAccounts` remova posteriormente os dados que ele próprio possui.

Este baseline **não** escolhe evento, saga, chamada direta ou outro seam, não define uma transação distribuída e não
considera a limpeza de `UserAccounts` concluída pelo simples sucesso de RL-07. Essa decisão acompanha a futura
arquitetura/API administrativa de exclusão de realm e pode exigir ADR própria.

### Exclusão explícita de `UserAccounts`

Nenhuma linha ST/RL/CL/RS/KY/AT/RT/AC/CN/SS/AP/MS/RC/SP/RR possui owner `UserAccounts`. Nenhuma entidade de conta,
credencial, propriedade, email, telefone, token de ação ou options do módulo entra em `Data.Configuration` ou
`Data.Operational`. Apenas ids escalares e a coordenação futura de exclusão cruzam a fronteira. Isso preserva
ADR-013/015 e DF8.

## Consumidores e cobertura por área

| Área | Consumidores principais | Testes atuais relevantes | Lacunas observadas |
|---|---|---|---|
| Realm/binding | realm discovery, cookie options, `RealmManager`, `FirstKeyJob`, `KeyCacheEntry` | `RealmIsolationTests`, testes de realm options | lookup de domain e provider/session sem teste direto; não há exclusão em produção |
| Clients | client loader, CORS, validators, secret evaluators, sign-out | `RealmIsolationTests` e fluxos OIDC | comparador/ausência/disabled ainda não formam contrato provider-neutral |
| Resources | discovery, client decorator, code/refresh grants | `ResourceStoreTests`, `CodeTokenTests`, `RefreshTokenTests` | `GetAllResourcesAsync` sem caller/test; shape bloqueado |
| Keys | `DefaultKeyManager`, first-key job, signing/validation | signing/JWKS/jobs incidentalmente | ausência, ordem e bordas `NotBefore`/`Expires` sem testes diretos mapeados |
| Tokens/codes/consent | factories, loaders, validators, handlers e consent service | token/code/refresh/revocation/realm isolation | concorrência atômica ausente; duplicidade e expiração desiguais |
| Sessões | session service, code, end-session, sign-out, revocation | `DefaultUserSessionServiceTests`, `SessionLifecycleTests`, `UserSessionCharacterizationTests`, `BackChannelLogoutCharacterizationTests`, active-rule/end-session | faltam cenários provider-neutral por método e identidade materializada |
| Authorize parameters | page results, resolver e callback | fluxo de login/consent | sem teste direto de store; globalidade, expiração e colisão não decididas |
| Mensagens | handlers e page services de login/consent/logout | end-session/back-channel logout | sem teste direto de tamper/delete |
| Replay | `PrivateKeyJwtSecretEvaluator` | nenhum teste relevante localizado | default sem proteção e check+add não atômico |

## Evidência de inventário

Buscas executadas a partir da raiz do repositório:

```powershell
rg --files RoyalIdentity/Contracts/Storage
rg --files RoyalIdentity/Users/Contracts -g "IUserSessionStore.cs"

rg -n "public interface I(Storage|RealmStore|ClientStore|ResourceStore|KeyStore|AccessTokenStore|RefreshTokenStore|AuthorizationCodeStore|UserConsentStore|AuthorizeParametersStore|MessageStore|ReplayCache|StorageProvider|StorageSession|UserSessionStore)" RoyalIdentity

rg -n "ServerOptions|\.Realms|AuthorizeParameters|GetAccessTokenStore|GetRefreshTokenStore|GetAuthorizationCodeStore|GetUserConsentStore|GetKeyStore|GetClientStore|GetResourceStore|GetUserSessionStore|CreateSession\(|GetStorage\(" RoyalIdentity RoyalIdentity.Server Tests.Integration Tests.Identity

rg -n "GetByPathAsync|GetByPath\(|GetByIdAsync|GetByDomainAsync|GetAllAsync|SaveAsync|DeleteAsync|FindClientByIdAsync|FindEnabledClientByIdAsync|GetAllResourcesAsync|GetAllEnabledResourcesAsync|FindResourcesByScopeAsync|FindRequestedResourcesAsync|ResolveAuthorizedSubsetAsync|AddKeyAsync|ListAllCurrentKeysIdsAsync|ListAllKeysIdsAsync|GetKeyAsync|GetKeysAsync" RoyalIdentity RoyalIdentity.Server Tests.Integration

rg -n "GetRealmMemoryStore|GetDemoRealmStore|GetServerRealmStore" Tests.Integration Tests.UserAccounts -g "*.cs"
```

Resultados resumidos:

- 15 contratos incluídos, uma extensão semântica e 62 operações/propriedades contratuais inventariadas; os sete
  membros públicos de `ResourceResolution` foram catalogados separadamente.
- Todos os getters realm-bound foram ligados ao dictionary correspondente em `RealmMemoryStore`; `ServerOptions`,
  realms e authorize parameters foram identificados como globais no fake.
- Foram confirmadas 56 referências diretas aos getters de setup do fake: 55 em 15 arquivos de `Tests.Integration` e
  uma em `Tests.UserAccounts/UserDirectoryContractTests.cs`. A classificação individual dessas ocorrências pertence
  à Fase 4.
- Não foram encontrados caller de produção para `IRealmStore.DeleteAsync`, caller para `GetAllResourcesAsync`, nem
  testes diretos relevantes para replay cache e `IStorageProvider`/`IStorageSession`.
- A matriz não marca como `preservar` comportamento observado sem citar ADR, foundation, produto ou decisão DF. Tudo
  que ainda depende de decisão por store/campo/método permanece explicitamente `avaliar`.

## Contract tests provider-neutral — Fase 3

Suíte criada em `Tests.Storage` (DF13), executada em 2026-07-21 com 78 cenários verdes contra a fixture
`MemoryStorage` (revisada no mesmo dia após análise externa — ver as regras e a tabela de aceites abaixo).
Estrutura conforme o design alvo do plano:

- `Tests.Storage/Storage/Support/` — `StorageContractHarness` (abstração provider-neutral com dois realms
  isolados, realm interno, `FakeClock` e hooks test-only de seed de clients/resources) e
  `InMemoryStorageHarness` (única classe autorizada a tocar `MemoryStorage`/`RealmMemoryStore`; usa
  `AddInMemoryStorage`). Lifecycle isolado por teste: cada cenário cria e descarta seu próprio harness.
- `Tests.Storage/Storage/Contracts/` — um grupo de contrato por store, nomes por comportamento; cada provider
  futuro adiciona apenas uma classe aninhada por grupo (`InMemory` hoje; `Sqlite`/`PostgreSql` nos Planos 2/3).

| Grupo de contrato | Linhas cobertas | Cenários |
|---|---|---:|
| `RealmStoreContractTests` | RL-01, RL-03..RL-07 (RL-06 update preserva dados; RL-07 por efeito observável de DF20) | 8 |
| `ClientStoreContractTests` | CL-01, CL-02, DF6 com client id colidente | 6 |
| `KeyStoreContractTests` | KY-01..KY-05, DF6 com key id colidente; janelas atuais/históricas e ordem por `Created` | 7 |
| `AccessTokenStoreContractTests` | AT-01..AT-04, DF6 com jti colidente | 7 |
| `RefreshTokenStoreContractTests` | RT-01, RT-02, RT-04, RT-05, DF6 (RT-03 deliberadamente sem cenário — aceite P3, ver tabela abaixo) | 6 |
| `AuthorizationCodeStoreContractTests` | AC-01..AC-03, DF6 com handle colidente | 6 |
| `UserConsentStoreContractTests` | CN-01..CN-03, DF6/invariante 6 com par subject+client colidente | 5 |
| `UserSessionStoreContractTests` | SS-01..SS-06, DF6 com sid colidente; dedup de client com relógio controlado | 11 |
| `AuthorizeParametersStoreContractTests` | AP-01..AP-03; leitura não consome; handles distintos | 5 |
| `ResourceStoreContractTests` | RS-02 (filtro enabled), RS-03 + DF6, RS-04 (resolução scope+resource, scope ausente/disabled, invalid targets de URI desconhecida/server disabled/malformada/HTTP não-loopback e HTTP localhost aceito) e RS-05 (subset não autorizado → `invalid_target`, conjunto completo sem indicators, downscope coerente) | 13 |
| `StorageSessionContractTests` | SP-01..SP-03 (lifetime seam de DF21; nada é assertado pós-dispose) | 2 |
| `MessageStoreContractTests` | MS-01..MS-03 (roundtrip do id; delete de id escrito completa; id desconhecido não assertado — `avaliar`/DF25) — fixture `ProtectedData` | 2 |

Regras aplicadas na suíte:

- Nenhum cenário referencia `ConcurrentDictionary`, `RealmMemoryStore` ou getters de setup do fake; o único
  ponto provider-specific é a classe aninhada de fixture (verificado por busca em `Storage/Contracts`).
- Nenhum cenário depende de live reference, identidade de objeto, ordem incidental, collation ou relógio real
  (DF17/DF18/DF24; tempo via `FakeClock`). Em particular, o update de realm (RL-06) salva uma instância nova
  com o mesmo id — a asserção não é satisfeita por mutação da referência já retida pelo backing — e RT-03
  ficou sem cenário porque o fake não consegue falsificar persistência explícita (ver aceites abaixo).
- A indisponibilidade pós-exclusão aceita o `ArgumentException` do fake como sinal de binding recusado ou
  uma leitura vazia após o purge do provider EF; qualquer outra exceção de infraestrutura reprova o teste.
  O contrato observável é não expor dados do realm excluído, não exigir consulta síncrona ao banco no accessor.
- Comportamentos `avaliar` exercitados por serem load-bearing para consumidores atuais (lookups ausentes →
  `null`, remoções idempotentes, upsert de consent, no-ops de sessão ausente) estão anotados nos cenários com
  a linha da matriz e a decisão pendente (DF16/DF19/DF25); a Fase 5 pode ajustá-los sem quebrar o desenho.
- `IReplayCache` (RC-01/RC-02) não recebeu contract test: o default `DefaultReplayNoCache` não oferece
  proteção e um teste cristalizaria o no-op; a operação atômica check+add permanece requisito de plano
  próprio, como já registrado.

### Testes de aceite futuros registrados (sem parity no fake — ADR-018)

| Requisito | Linha/decisão | Plano destino | Observação |
|---|---|---|---|
| Path/domain de realm excluído permanecem reservados pelo tombstone | RL-07 / DF20 | Plano 2 | Fake remove fisicamente e permite recriação; o provider EF de configuração deve adicionar o cenário de reserva. |
| Tombstone Configuration invisível a lookups normais | RL-07 / DF20 | Plano 2 | A suíte atual já cobre o efeito observável comum; a inspeção do tombstone é teste do provider. |
| Consumo atômico single-use de authorization code | AC-02/AC-03 / DF15 | Plano 3 | Concorrência get+remove não é exercida contra o fake. |
| Persistência do update e transição condicional/atômica de refresh token | RT-03 / DF15, DF17 | Plano 3 | O backing por live reference do fake não falsifica persistência explícita (a mutação fica visível antes do update e o CAS por referência rejeita instância rematerializada); RT-03 fica sem cenário na suíte e vira aceite do provider EF. |
| Propagação de `CancellationToken` em todo I/O real | DF23 | Planos 2/3 | O fake não simula cancelamento de I/O inexistente; os providers EF devem encaminhar `ct` a queries, enumerações e `SaveChangesAsync`, com cenário de aceite próprio. |
| Disposal real de `IStorageSession` (context/conexão) | SP-03 / DF21 | Plano 2 | Nada é assertado pós-dispose contra o fake no-op. |
| Check+add atômico de replay | RC-01/RC-02 | plano próprio | Sem teste contra o default no-cache. |
| Reject de duplicidade nas escritas create-only (índice único; falha visível, nunca overwrite ou sucesso silencioso) | KY-01 / AT-01, RT-01, AC-01, SS-01 / DF16 | Plano 2 (keys) / Plano 3 (operacionais) | O fake sobrescreve ou ignora silenciosamente (`descartar`); sem cenário na suíte. |
| Regeneração interna do handle de authorize parameters em colisão | AP-01 / DF16 | Plano 3 | Handle é gerado pelo store; nunca sobrescrever nem falhar por azar de geração. |
| Authorize parameters realm-bound + TTL absoluto + leitura fail-closed de expirado + purge de abandonados | ST-03, AP-01..AP-03 / DF6, DF19 (MP-5) | Plano 3 | Fake permanece global e sem TTL (ADR-018); semânticas fechadas na seção de fechamento de AP. |
| Normalização lowercase de `Realm.Domain` (escrita + consulta), recusa de `SaveAsync` direto não canônico e unicidade sobre o valor normalizado | RL-04 / DF18 (MP-10) | Plano 2 | Comportamento novo; o store compara Ordinal (coberto na suíte); manager, adapter EF e consultas têm aceites próprios. |
| Índices únicos de path/domain de realm | RL-01/RL-04 / DF16 | Plano 2 | Hoje a unicidade é premissa do caller (`RealmManager`); vira constraint do provider. |

## Seeds, dados globais e acessos diretos — Fase 4

Inventário produzido em 2026-07-21 por inspeção de `MemoryStorage`/`RealmMemoryStore`, das composições de host
(`RoyalIdentity.Server`, `Tests.Host`, `AppFactory`/`UserAccountsAppFactory`) e das 56 ocorrências de getters
diretos do fake. Nenhum código foi alterado nesta fase (DF1); as linhas abaixo são entradas executáveis dos
Planos 2/3/4.

### Seeds estáticos e globais do fake

| Seed | Onde nasce | Conteúdo | Owner/finalidade | Destino |
|---|---|---|---|---|
| `ServerOptions` global | `MemoryStorage.serverOptions` (static) | instância única compartilhada por todas as `RealmOptions` | Configuration; base de opções do servidor | P2 persiste/materializa; deixa de ser instância mutável compartilhada (DF17) |
| Realms internos | statics `ServerRealm`/`AccountRealm`/`AdminRealm` | 3 realms `Internal = true` | produto: realms internos obrigatórios e não removíveis | seed de produto na composição/migração do P2 |
| Realm demo | static `DemoRealm` (+ branding no static ctor) | realm comum `demo` | dev/demo do host | seed de dev/demo da composição do host; nunca requisito do provider |
| Clients iniciais | ctor de `RealmMemoryStore(realm, isServer)` | server → `server_admin`; account/admin/demo → `demo_client`+`demo_consent_client` | `server_admin` é produto/host; demo clients são dev/demo. **Acidente:** o parâmetro binário `isServer` faz account/admin receberem os clients de demo | P2/host seed; o vazamento de demo clients para account/admin **não é comportamento a preservar** |
| Identity scopes padrão | property initializer de `RealmMemoryStore.IdentityScopes` | openid, profile, email, address, phone em **todo** realm, inclusive os criados por `SaveAsync` | openid é exigência de produto; o conjunto padrão é candidato a seed de criação de realm | decisão do P2/redesign: o que a criação de realm semeia é regra explícita, não initializer escondido |
| Resource server demo | property initializer de `RealmMemoryStore.ResourceServers` | `apiserver` (api, api:read, api:write; URI `https://api.demo.local/apiserver`) em **todo** realm | dev/demo. **Acidente:** realms novos também o recebem | seed demo da composição; bloqueado por DF22 para persistência |
| Contas demo | `DemoUsers()` no ctor (somente demo realm) + constantes `AliceSubjectId`/`BobSubjectId` | alice/bob com hash de senha | fora do storage do IdP (família `UserAccounts`); fake transitório ADR-018 | composição com o módulo real + `UserAccountsModuleSeed` (precedente: `UserAccountsAppFactory`) |
| Keys | nenhum seed estático | `KeyParameters` nasce vazio; `FirstKeyJob` (job de startup, `IServerJob`) percorre `Realms.GetAllAsync` criando keys — mas **encerra o job inteiro** (`return`) ao encontrar o primeiro realm que já possua key, e `RealmManager.CreateAsync` não provisiona key para realms criados em runtime; o key manager não cria on-demand (lookups retornam `null`/vazio) | produto: todo realm habilitado precisa de key de assinatura utilizável. Hoje o job só cobre todos os realms porque o backing em memória renasce vazio a cada startup | P2 remove o job escritor, persiste keys pelo `AddKeyAsync` legado somente por compatibilidade/testes, semeia uma key utilizável por realm habilitado e valida material/algoritmo no startup; criação/rotação em runtime fica para admin/KMS (DF19/DF27/DF28 do P2) |
| Authorize parameters | dictionary global vazio | estado transitório por request | Operational; sem seed | n/a |

**Lacuna de provisão de keys (lifecycle/orquestração) — decisão superada pelo Plano 2:** o problema factual do
`FirstKeyJob` permanece como estado de partida, mas MP-4 não é mais o design alvo. DF19/DF27/DF28 de
`plan-data-configuration-storage.md` removem o job escritor, fazem o runner/seed provisionar uma key atual,
desprotegível e compatível com o algoritmo principal para cada realm habilitado e fazem o startup falhar quando
essa condição não é atendida. A provisão de realms criados/habilitados em runtime e a rotação permanecem no
futuro fluxo administrativo/KMS.

### Composição de host e testes

- `RoyalIdentity.Server/HostServices` e `Tests.Host/HostServices` chamam `AddInMemoryStorage()` — todo o seed
  acima entra por essa única chamada; não existe seed público de produção separado do fake.
- `Tests.Integration/Prepare/AppFactory` usa o `Program` de `Tests.Host` + Data Protection persistido em disco;
  é a fixture compartilhada dos testes de fluxo HTTP.
- `Tests.Integration/Prepare/UserAccountsAppFactory` (opt-in) mantém o storage do IdP in-memory e troca a
  borda de contas para o módulo real (Sqlite in-memory) com `UserAccountsSeedHostedService` chamando
  `UserAccountsModuleSeed.SeedDefaultScopesAsync`/`SeedDefaultAccountsAsync` — **precedente do P4** para seed
  por composição, correlacionado apenas por `realmId`/`SubjectId` escalares (DF8).
- `Tests.Storage` (Fase 3) cria os realms pela própria facade (`SaveAsync`) e semeia clients/resources por
  hooks test-only do harness; keys entram pela facade contratual (`AddKeyAsync`).

### Classificação das 56 ocorrências de getters diretos do fake

Por arquivo (55 em `Tests.Integration`, 1 em `Tests.UserAccounts`):

| Arquivo | Ocorrências | Uso |
|---|---:|---|
| `Endpoints/ClientTokenTests.cs` | 14 | setup: 8 clients, 6 resource servers |
| `Realm/RealmIsolationTests.cs` | 8 | setup: clients (incl. realm B com id colidente) |
| `Endpoints/RefreshTokenTests.cs` | 7 | setup: 4 clients, 2 resource servers; 1 leitura de `ResourceServers` para derivar `AllowedResourceServers` do client |
| `Endpoints/CodeTokenTests.cs` | 7 | setup: 4 clients, 3 resource servers |
| `Endpoints/CodeAuthorizeTests.cs` | 4 | setup: 3 clients, 1 resource server |
| `Endpoints/SigningAlgorithmTests.cs` | 3 | setup: 1 resource server, 1 conta (alice em realm novo), 1 client |
| `Prepare/CharacterizationSeed.cs` | 3 | helper compartilhado: 1 seed de conta; 1 inspeção e mutação de conta (contadores/lockout e desativação por live reference); 1 inspeção de sessão por `SubjectId` |
| `Characterization/BackChannelLogoutCharacterizationTests.cs` | 2 | setup: clients |
| `Endpoints/DiscoveryTests.cs` | 1 | setup: resource server |
| `Endpoints/EndSessionTests.cs` | 1 | setup: client |
| `Characterization/PromptInteractionCharacterizationTests.cs` | 1 | setup: client |
| `Realm/EventIsolationTests.cs` | 1 | setup: client |
| `UI/LoginConsentUIFlowTests.cs` | 1 | setup: client |
| `Realm/RealmOptionsPhase4Tests.cs` | 1 | setup: client |
| `Realm/RealmOptionsPhase5Tests.cs` | 1 | setup: client |
| `Tests.UserAccounts/UserDirectoryContractTests.cs` | 1 | seed de conta do lado in-memory do contract test |

Por categoria, com destino de substituição:

| Categoria | Ocorrências | Destino |
|---|---:|---|
| Setup de clients | 36 | write facade de configuração do P2 (quando existir) ou seed test-only do provider; até lá, hook de fixture — nunca API pública criada pelo baseline (DF1) |
| Setup de resource servers | 14 | **bloqueado por DF22**: permanece hook test-only até o redesign fechar o modelo; o P4 não pode migrar esses testes para write facade inexistente |
| Setup de contas (fake) | 3 (`CharacterizationSeed.SeedUser`, `SigningAlgorithmTests.SeedAlice`, `UserDirectoryContractTests.InMemory.SeedAsync`) | composição com módulo `UserAccounts` + `UserAccountsModuleSeed` (ADR-018); o lado fake do contract test morre com o fake |
| Inspeção **e mutação** de conta | 1 (`CharacterizationSeed.GetDetails` — lê contadores de falha/lockout e, em `ActiveRuleCharacterizationTests`, **desativa a conta mutando a referência viva** retornada) | leituras viram asserção de comportamento observável (respostas do fluxo) ou superfície do módulo real; a mutação exige uma operação de atualização/desativação pelo módulo `UserAccounts` ou hook test-only da fixture — estado interno de conta não vira contrato do IdP |
| Inspeção de sessão | 1 (`CharacterizationSeed.FindSession` — scan de `UserSessions` por `SubjectId`) | o contrato não tem consulta por subject (somente `FindByIdAsync`/`EndSessionsForSubjectAsync`); substituir capturando o `sid` no próprio fluxo de teste. Se o P3 julgar necessário um lookup por subject, é mudança pública a listar na Fase 5 — não inferida aqui |
| Leitura de configuração para setup | 1 (`RefreshTokenTests` — deriva owners de resource URIs) | substituível pela facade de leitura existente (`IResourceStore.FindRequestedResourcesAsync`) |

Com uma exceção, nenhuma das 56 ocorrências é dependência semântica do backing (ninguém depende de
`ConcurrentDictionary` ou de ordem para o comportamento assertado). A exceção é a mutação por live reference
via `GetDetails` descrita acima — o único caso que exige rota própria (operação do módulo ou hook test-only)
em vez de mera troca de mecanismo de setup. Não há setup indispensável sem estratégia test-only.

### Acoplamento adicional: statics do fake como handles

Além dos getters, `Tests.Integration` usa `MemoryStorage.DemoRealm`/`ServerRealm`/`AccountRealm`/
`AliceSubjectId`/`BobSubjectId` como handles em 248 linhas contendo 260 referências, em 26 arquivos (contagem
por `MemoryStorage\.(DemoRealm|ServerRealm|AccountRealm|AdminRealm|AliceSubjectId|BobSubjectId)`; algumas
linhas contêm mais de uma referência). Não é acesso a
estado interno, mas é acoplamento de composição ao fake: o P4 deve fornecer esses handles pela fixture
(realms/subjects resolvidos da composição corrente) ao trocar o backing default.

### Dependências mínimas de seed (ordem falsificável)

1. `ServerOptions` existe antes de qualquer realm (as `RealmOptions` derivam dele).
2. Realm salvo (`IRealmStore.SaveAsync`) antes de qualquer store realm-bound — realm desconhecido ou excluído
   nunca pode expor dados (ST-04..ST-11); o fake recusa o binding, enquanto o EF pode materializar um store
   cujas consultas retornam vazio após o purge.
3. Configuração do realm antes dos fluxos que a consomem: clients (authorize/token), identity
   scopes/resource servers (discovery/grants; shape bloqueado por DF22), keys (assinatura — via
   `AddKeyAsync` ou `FirstKeyJob`).
4. Dados operacionais (tokens, codes, consents, sessões, authorize parameters) somente com o realm resolvido;
   nos fluxos HTTP exigem também client/scopes existentes. No nível do store, nenhuma operação valida
   client/conta — correlação é escalar (Fase 2).
5. Contas ficam fora desta ordem: são semeadas pela família `UserAccounts` na composição (module seed),
   correlacionadas por `SubjectId`; o storage do IdP não participa (DF8).

A fixture da Fase 3 falsifica essa ordem: o harness cria `ServerOptions`→realms→dados nessa sequência e os
cenários DF6 provam dois realms isolados com ids/handles colidentes em todos os stores realm-bound
(`SameClientId/SameKeyId/SameJti/SameHandle/SameCodeHandle/SameSubjectAndClient/SameSid…_InTwoRealms`).

### Separação de responsabilidades de seed

| Camada | Conteúdo | Hoje | Alvo |
|---|---|---|---|
| Host/produto | realms internos, `server_admin`; provisão de keys pelo `FirstKeyJob` do core | realms/client embutidos no fake via `AddInMemoryStorage`; o job é registrado por `AddOpenIdConnectProviderServices` e persiste pela facade `IKeyStore` | seed explícito de composição/migração; P2 remove o job, semeia key por realm habilitado e valida sua usabilidade no startup; admin/KMS futuro assume criação/rotação em runtime |
| Dev/demo | demo realm, demo clients, `apiserver`, alice/bob | embutido no fake | seed de dev/demo do host, opcional, fora do provider |
| Fixture compartilhada | `AppFactory`/`Tests.Host` (fluxos HTTP); `StorageContractHarness` (contract tests) | AppFactory herda todo o seed do fake; harness cria o próprio estado | P4: fixture fornece realms/handles e seed mínimo pela composição corrente |
| Cenário | clients/resources/keys/contas únicos por teste (sufixos aleatórios) | mutação direta de dictionary (56 ocorrências) | hooks test-only → facades/seed do provider conforme tabela de categorias |

### Gate para o Plano 4 (troca de backing)

O P4 só migra os testes de fluxo quando: (a) P2 entregar write facade ou seed test-only para clients e
implementar a persistência do `AddKeyAsync` já existente para keys, com a lacuna de provisão de keys
resolvida ou contornada pela fixture; (b) resources tiverem rota test-only própria enquanto DF22 vigorar;
(c) contas forem semeadas pela composição do módulo (`UserAccountsModuleSeed`), já demonstrado por
`UserAccountsAppFactory`; (d) os handles estáticos (`DemoRealm` etc.) forem substituídos por handles da
fixture. Os acessos de `CharacterizationSeed` além de seed devem ser resolvidos antes da troca do backing:
as leituras de conta viram comportamento observável, a mutação de conta ganha rota pelo módulo ou hook test-only,
e `FindSession` passa a capturar o `sid` no próprio fluxo e consultar a sessão por `FindByIdAsync`.

## Paridade final e ordem de migração — Fase 5

Fechamento produzido em 2026-07-22. Esta seção resolve todos os `avaliar` do inventário aplicando DF15-DF25;
onde houver divergência com a "classe inicial" das tabelas acima, **esta seção prevalece** (as tabelas do
inventário são o registro histórico das Fases 1-2). Nenhuma semântica foi inferida do fake: cada fechamento
cita a decisão DF e a fonte normativa ou a regra protocolar/negocial que o sustenta.

### Políticas transversais fechadas

- **Comparadores (DF18):** todos os identificadores do catálogo comparam **Ordinal** (case-sensitive, byte a
  byte), sem normalização no store: realm id, realm path (segmento de URL — RFC 3986), client_id (RFC 6749,
  string opaca), jti, handle de refresh token, code, subject id (OIDC `sub` é case-sensitive), session id,
  nomes de scope, key id, resource URI (comparação literal — RFC 8707/ADR-012) e handle de authorize
  parameters. **Única exceção de regra:** `Realm.Domain` — DNS é case-insensitive (RFC 4343); a regra é
  normalizar para lowercase e comparar Ordinal sobre o valor normalizado. **Essa normalização é
  comportamento novo, não paridade preservada** (`substituir` — MP-10): hoje nem o modelo nem o fake
  normalizam. O `RealmManager` é a borda canônica de escrita e as bordas de leitura por domain normalizam o
  valor consultado antes de chamar o store; para que chamadas diretas ao contrato público não persistam um
  valor não canônico, o adapter EF **rejeita** `SaveAsync` quando `Domain` não estiver em lowercase. Seeds e
  migrações também entregam o valor já normalizado. O store **sempre** compara Ordinal. Nenhum
  campo depende de collation do provider; collation apenas implementa a regra (índices dos providers devem
  ser case-sensitive/binários), e a suíte cobre identificadores que diferem apenas por casing.
- **Ausência (DF25), por família de método:** lookups `Find*`/`Get*`/`Read*` de registro retornam `null`
  (RL-01/03/04, CL-01/02, AT-02, RT-02, AC-02, CN-02, SS-02, AP-02, MS-02); remoções e revogações são
  idempotentes — ausência é no-op sem erro (AT-03/04, RT-04, AC-03, CN-03, AP-03), retornam contagem
  efetiva quando o contrato a expõe (RT-05, SS-06 — repetição retorna 0) ou `false` (RL-07 para realm
  inexistente); no-ops de sessão ausente preservados (SS-03/05, `EndAsync` retorna `null`). **Exceção
  decidida:** `GetKeyAsync`/`GetKeysAsync` (KY-04/05) **lançam** em ausência — os ids vêm das próprias
  listagens do store e ausência indica inconsistência, não fluxo normal; o tipo permanece o atual
  (`ArgumentException`) como critério de paridade.
- **Duplicidade (DF16), por semântica do método:** `IRealmStore.SaveAsync` (RL-06) e
  `StoreUserConsentAsync` (CN-01) são **upsert** — é a semântica documentada/consumida do método
  (`RealmManager`, `DefaultConsentService`). Todas as demais escritas persistem artefatos de identificador
  **gerado** e são **create-only**: `AddKeyAsync` (KY-01), `StoreAsync` de access/refresh token
  (AT-01/RT-01), `StoreAuthorizationCodeAsync` (AC-01), `CreateAsync` de sessão (SS-01) e `WriteAsync` de
  authorize parameters (AP-01). Nelas, colisão de id é falha de integridade: o alvo é **reject** (índice
  único do provider); os comportamentos silenciosos do fake (`TryAdd` ignorado, overwrite por indexação)
  são `descartar` e não recebem cenário — o reject é aceite dos Planos 2/3. **Sinal observável do reject
  fechado:** a operação falha visivelmente (exceção); o contrato **não** fixa um tipo de exceção — colisão
  de identificador gerado com entropia adequada é falha de integridade/infraestrutura, não fluxo de negócio;
  a garantia contratual é **nunca sobrescrever e nunca reportar sucesso silencioso**. **Exceção — AP-01:**
  o handle é gerado pelo próprio store, então em colisão o store **regenera o handle** (retry interno) em
  vez de falhar ou sobrescrever.
- **Expiração (DF19), por tipo:** nenhuma leitura individual Operational filtra expiração ou consumo — o
  registro logicamente expirado/consumido é devolvido e a validação pertence ao consumidor (token
  validator, `LoadCode`, consent service, session service); a tolerância de refresh token **exige** ler o
  registro consumido. As únicas leituras com janela temporal contratual são KY-02/KY-03, com bordas
  **inclusivas** fechadas: key é corrente quando `NotBefore <= now && now <= Expires` (limite `null` =
  sem restrição); histórica quando `NotBefore <= now` (expirada incluída, futura excluída). Retenção
  necessária ao fluxo, expiração lógica e **remoção física (TTL/purge)** são dimensões separadas — o
  cleanup físico por tipo é requisito do P3. **Exceção fechada — authorize parameters (AP-02):** a leitura é
  fail-closed — registro logicamente expirado é ausente (`null`), porque retomar um authorize abandonado é
  risco de segurança, não feature; ver o fechamento de AP abaixo.
- **Ordem (DF24):** ordens contratuais são somente KY-02/KY-03 (cronológica por `Created`) e KY-05 (ordem
  dos ids solicitados — correspondência à requisição). Todas as demais listagens/enumerações são conjuntos.
- **Mutabilidade (DF17):** persistência somente por operação explícita; toda leitura pode materializar nova
  instância; todas as live references do fake são `descartar`.
- **Cancelamento (DF23):** operações assíncronas propagam `ct` no provider EF (aceite registrado); in-memory
  não simula cancelamento; RL-02 é a única API síncrona e é `substituir`.

### Fechamento por linha

| ID(s) | Classe final | Fechamento aplicado | Teste / justificativa |
|---|---|---|---|
| ST-01 | preservar (accessor) / descartar (instância mutável compartilhada) | DF17 | usado por toda a fixture (`CreateRealmAsync`); sem cenário dedicado |
| ST-02 | preservar | facade global de realms | `RealmStoreContractTests` |
| ST-03 | preservar (facade) / substituir (binding global → realm-bound, MP-5) | o alvo particiona authorize parameters por realm | `AuthorizeParametersStoreContractTests` + aceite P3 |
| ST-04..ST-11 | preservar | binding realm-bound obrigatório; realm desconhecido/excluído nunca expõe dados. O `ArgumentException` do fake é sinal aceito, não exigência do accessor EF síncrono; leitura vazia após purge também satisfaz o contrato | cenários DF6 `Same*_InTwoRealms` + indisponibilidade pós-delete |
| RL-01 | preservar | `null` em ausência; path Ordinal; unicidade de path vira índice único P2 (elimina a "ordem incidental de duplicidade") | `Save_NewRealm…`, `GetByPath_Unknown…` |
| RL-02 | substituir | DF23 + DF7 do P2 — método do store removido; source carrega async e consumidor síncrono lê snapshot sem I/O | mudança pública MP-1 |
| RL-03 | preservar | `null`; id Ordinal | testes de realm |
| RL-04 | preservar (`null` em ausência; Ordinal no store) / **substituir** (normalização lowercase — comportamento novo, MP-10) | normalização no `RealmManager` e nas bordas de consulta; `SaveAsync` direto rejeita domain não canônico no adapter EF; unicidade sobre o valor normalizado (aceite P2) | `Save_NewRealm…`, `GetByDomain_Unknown…`, casing → `null` no store + aceites P2 |
| RL-05 | preservar | enumeração completa como conjunto; ordem `descartar` | `GetAll_ContainsSavedRealms` |
| RL-06 | preservar (upsert) / descartar (init de backing do fake; live reference) | DF16 — upsert é a semântica do método | `Save_ExistingRealm…` |
| RL-07 | preservar (recusa interno; `false` ausente) / substituir (hard delete → tombstone + purge, DF20) | DF25 fechado: ausência → `false` idempotente | testes de delete + aceites P2/P3 |
| CL-01/CL-02 | preservar | `null`; Ordinal; disabled só na API enabled | `ClientStoreContractTests` |
| RS-01 | descartar | sem caller de produção ou teste; **candidata a remoção do contrato no redesign** (não requerida pelos Planos 2/3) | justificativa de descarte |
| RS-02..RS-05 | preservar (comportamento) — persistência bloqueada por DF22 | ADR-010/012, RFC 8707 | `ResourceStoreContractTests` (13 cenários) |
| RR-01/02/05/06/07 | preservar | coerência do resultado | via cenários RS-05 |
| RR-03/RR-04 | descartar (texto/conteúdo exato) | mensagens podem evoluir; os **códigos** de erro OAuth são o contrato (RR-02) | não assertar texto |
| KY-01 | preservar (escrita pela facade) / descartar (replace silencioso) | DF16 create-only; duplicidade → reject (aceite P2) | `AddKey_ThenGetKey…` |
| KY-02/KY-03 | preservar | bordas inclusivas fechadas (acima); ordem por `Created` | `ListAllCurrentKeysIds…`, `ListAllKeysIds…`, `KeyListings_AreOrderedByCreation` |
| KY-04 | preservar | ausência **lança** `ArgumentException` (exceção de paridade — ver política de ausência) | `GetKey_UnknownKeyId_Throws…` (novo) |
| KY-05 | preservar | resultado na ordem dos ids solicitados; ausência propaga a exceção de KY-04 | `GetKeys_ReturnsKeysInRequestedOrder` (reforçado) |
| AT-01 | preservar (emissão) / descartar (`TryAdd` silencioso) | create-only; reject é aceite P3 | `Store_ThenGetByJti…` |
| AT-02 | preservar | `null`; leitura não filtra expiração (DF19) | `Get_UnknownJti…`, `Get_ReturnsLogicallyExpiredToken` (novo) |
| AT-03/AT-04 | preservar | remoção/revogação idempotente; filtro exato Reference+subject+client Ordinal | testes existentes |
| RT-01 | preservar (emissão) / descartar (`TryAdd` silencioso) | create-only; reject aceite P3 | `Store_ThenGet…` |
| RT-02 | preservar | `null`; leitura devolve consumido/expirado — requisito da tolerância (product) | `Get_UnknownHandle…`, `Get_ReturnsExpiredAndConsumedToken` (novo) |
| RT-03 | substituir | DF15/DF17 — transição condicional/atômica + persistência por instância rematerializada | aceite P3 (MP-3) |
| RT-04/RT-05 | preservar | idempotência; contagem efetiva; Ordinal; nunca cross-realm | testes existentes |
| AC-01 | preservar (emissão) / descartar (overwrite por indexação) | create-only; reject aceite P3 | `Store_ThenGet…` |
| AC-02 | preservar (leitura) / substituir (get+remove como consumo) | `null`; sem filtro de expiração na leitura (validação no consumidor); consumo atômico é MP-2 | `Get_UnknownCode…`, `Get_ReturnsLogicallyExpiredCode` (novo) |
| AC-03 | preservar (remoção administrativa idempotente) / substituir (papel de consumo) | DF15 | testes existentes |
| CN-01 | preservar (upsert por identidade realm+subject+client) / descartar (chave concatenada `subject.client` — colisão com `.`) | DF16; provider usa chave composta real (aceite P3) | `Store_ThenGet…`, `Store_SameSubjectAndClient…` |
| CN-02 | preservar | `null`; leitura não filtra `Expiration` (consent service decide) | `Get_UnknownSubjectClientPair…`, `Get_ReturnsLogicallyExpiredConsent` (novo) |
| CN-03 | preservar | idempotente | teste existente |
| SS-01 | preservar (persistência) / descartar (overwrite por indexação) | create-only; reject aceite P3 | `Create_ThenFindById…` |
| SS-02 | preservar | `null`; leitura devolve sessão expirada/inativa (regra de validade é do service — ADR-017) | `FindById_Unknown…`, `FindById_ReturnsLogicallyExpiredSession` (novo) |
| SS-03..SS-06 | preservar | dedup por client; no-ops de ausência; `EndAsync` idempotente (repetição devolve inativa); touch explícito; revogação por subject com contagem | testes existentes + `End` reforçado |
| AP-01 | preservar (estado server-side; handle aleatório ≥128 bits não adivinhável) / substituir (colisão silenciosa → **regeneração interna do handle**; escrita grava expiração absoluta) | DF14/DF16/DF19 — fechamentos de AP abaixo | `Write_ThenRead…`, `Write_TwoEntries…` + aceites P3 |
| AP-02 | preservar (leitura repetível; `null` em ausência) / substituir (**fail-closed para expirado**: registro vencido é ausente) | DF19 por tipo; live reference `descartar` | testes existentes + aceite P3 (fake não tem TTL) |
| AP-03 | preservar (cleanup idempotente do callback) / substituir (TTL + purge de abandonados — MP-5/MP-6) | DF19 | teste existente + aceite P3 |
| MS-01/MS-02 | preservar (roundtrip; fail-closed → `null` em id ilegível — regra de segurança) / descartar (formato do id como contrato) | DF14/DF25 | roundtrip + `Read_UnreadableId_ReturnsNull` (novo) |
| MS-03 | descartar (como critério de paridade) | efeito do delete é da implementação; semântica definitiva acompanha o futuro store persistente (backlog PAR) | delete de id escrito apenas completa |
| RC-01/RC-02 | substituir | check+add atômico com proteção real — plano próprio; default no-cache documentado como sem proteção | aceite registrado |
| SP-01..SP-03 | preservar (seam DF21) / substituir (implementação no adapter EF) / descartar (dispose no-op) | DF21 | `StorageSessionContractTests` + aceite P2 |

Não resta nenhuma semântica de storage marcada `avaliar` ou `a definir`: os fechamentos derivam de
DF15-DF25 e das fontes citadas. Decisões de desenho interno que pertencem aos planos executores permanecem
nomeadas como tal — MP-1 no P2 — e o valor numérico da janela de interação é uma opção de produto do P3,
não uma semântica inventada por este baseline.

### Fechamento de `IAuthorizeParametersStore` (decisões antes delegadas ao P3)

O contrato atual é global e não expressa realm, instante nem expiração. O baseline fecha a semântica alvo; o
P3 implementa sem novas decisões de comportamento:

1. **Binding:** o alvo é **realm-bound** (`substituir` o estado global). Fonte: isolamento por realm é a
   fronteira de dados do produto (product.md/DF6); o handle não adivinhável é defesa em profundidade, não
   fronteira de isolamento. Mudança contratual (MP-5): o accessor global `IStorage.AuthorizeParameters` dá
   lugar a um accessor realm-bound (ex.: `GetAuthorizeParametersStore(realm)`), com particionamento por realm
   no schema.
2. **Expiração:** a escrita grava validade absoluta = `now + janela de interação do authorize`, com a janela
   configurável por realm (nova option em `RealmOptions.Authentication`; hoje não existe essa option).
   O baseline não fixa um default numérico sem fonte: o P3 deve documentar a decisão de produto antes de
   implementar. Como referência adjacente, [RFC 9126 §2.2](https://www.rfc-editor.org/rfc/rfc9126.html#section-2.2)
   deixa o lifetime de PAR a critério do authorization
   server e cita 5–600 segundos como faixa tipicamente curta; essa faixa não é transplantada automaticamente
   para este estado interno, que atravessa login e consent. Owner do relógio: o `TimeProvider` da composição
   (mesmo padrão do session store), nunca relógio de parede do provider.
3. **Leitura:** fail-closed — `ReadAsync` de registro expirado retorna `null` (retomar authorize abandonado é
   risco, não feature). A leitura continua repetível dentro da validade (fluxo atual).
4. **Cleanup:** lazy na leitura (expirado ⇒ ausente) **e** purge físico periódico junto ao MP-6; o delete do
   callback permanece o caminho feliz (AP-03).
5. **Colisão de handle:** o handle é gerado pelo store; em colisão o store regenera internamente — nunca
   sobrescreve, nunca falha por azar de geração.

O fake não recebe TTL/particionamento (ADR-018); os itens 1-5 são testes de aceite do P3 na tabela abaixo.

### Mudanças públicas requeridas (registradas, não implementadas — DF1)

| # | Mudança | Linha/decisão | Plano |
|---|---|---|---|
| MP-1 | Remover `IRealmStore.GetByPath` síncrono. **Desenho fechado por DF7 do P2:** `IConfigurationSnapshotSource` carrega assincronamente um snapshot defensivo antes do tráfego e em refresh periódico; os consumidores síncronos leem somente o snapshot. Após publicação válida, o loader remove do `IOptionsMonitorCache<CookieAuthenticationOptions>` somente o scheme default e a união dos schemes de realm anterior/novo; falha mantém snapshot/options last-known-good. A remoção do método síncrono só ocorre após todos os callers inventariados migrarem. | RL-02 / DF23 + DF7/DF26 do P2 | P2 |
| MP-2 | Operação atômica de consumo single-use de authorization code (substitui o par get+remove no fluxo de token) | AC-02/AC-03 / DF15 | P3 |
| MP-3 | Transição condicional/atômica de refresh token (consumo/rotação; substitui o `UpdateAsync` CAS-trivial) | RT-03 / DF15 | P3 |
| MP-4 | **SUPERSEDED por DF19/DF27/DF28 do P2:** não corrigir nem manter o `FirstKeyJob`; removê-lo da composição, provisionar uma key utilizável por realm habilitado via runner/seed e falhar startup se material/algoritmo forem inválidos. Provisão/rotação em runtime pertence ao futuro admin/KMS. | lacuna Fase 4 | P2 (seed + validação); admin/KMS futuro |
| MP-5 | Authorize parameters: contrato global → realm-bound (accessor por realm), TTL absoluto gravado na escrita com janela configurável por realm (default numérico é decisão de produto documentada pelo P3), leitura fail-closed de expirado, purge de abandonados e regeneração de handle em colisão — semânticas de storage fechadas na seção "Fechamento de `IAuthorizeParametersStore`" | ST-03, AP-01..AP-03 / DF6, DF19 | P3 |
| MP-6 | Cleanup físico/TTL por tipo Operational (tokens, codes, consents, sessões), separado da leitura lógica | DF19 | P3 |
| MP-7 | Semântica de exclusão de realm: tombstone Configuration + purge Operational + reserva de path/domain (não muda assinatura de `DeleteAsync`) | RL-07 / DF20 | P2/P3; seam cross-family em ADR própria futura |
| MP-8 | Candidata (não requerida): remoção de `IResourceStore.GetAllResourcesAsync` sem caller | RS-01 | redesign de resources |
| MP-9 | Candidata (não requerida): lookup de sessão por subject — somente se o P3 decidir; os testes capturam o `sid` do fluxo | Fase 4 (`FindSession`) | P3, se necessário |
| MP-10 | Normalização lowercase de `Realm.Domain` no `RealmManager` (escrita) e nas bordas de consulta por domain; o adapter EF rejeita `SaveAsync` direto com domain não canônico; unicidade sobre o valor normalizado — comportamento novo, não paridade | RL-04 / DF18 | P2 |

Os rejects de create-only (KY-01, AT-01, RT-01, AC-01, SS-01) não mudam contrato público — são constraints
do provider (índice único; falha visível, nunca overwrite/sucesso silencioso) com testes de aceite nos
Planos 2/3; em AP-01 o equivalente é a regeneração interna do handle. Todos constam da tabela de aceites.

### Ordem de migração por store

Sem dependência circular: Operational depende de Configuration apenas pelo binding de realm; nenhuma
migração exige transação cross-família (DF21).

**Plano 2 — Configuration (nesta ordem):**

1. `ServerOptions` (base de tudo; materialização sem instância compartilhada);
2. Realms + `RealmOptions` (inclui tombstone, índices únicos de path/domain e reserva — MP-7; e MP-1);
3. Clients (inclui a write facade/seed test-only exigida pelo gate do P4);
4. Keys (persistência do `AddKeyAsync` legado + reject de duplicidade + seed/validação de DF19/DF27/DF28 do P2; MP-4 superada).

**Resources/scopes não entram no P2** (DF22): o adapter EF compõe uma ponte transitória para RS-*
(implementação em memória semeada por composição), sem persistir o shape instável; a persistência real
espera o redesign.

**Plano 3 — Operational (nesta ordem, crescendo em risco):**

1. Access tokens (CRUD mais simples; reject AT-01);
2. Consents (chave composta real; upsert CN-01);
3. Sessões (integração com service/revogação ADR-017; reject SS-01);
4. Authorization codes (consumo atômico — MP-2);
5. Refresh tokens (transição condicional — MP-3; revogação por subject);
6. Authorize parameters (MP-5: TTL + particionamento) e MP-6 (cleanup por tipo).

**Plano 4 — troca de backing:** gate da Fase 4 **mais** os seguintes: contract suite `Tests.Storage` verde
nas fixtures EF (Sqlite; PostgreSQL opt-in) para todas as linhas `preservar`, e testes de aceite dos
`substituir` (tabela de aceites) verdes nos providers. Só então os testes de fluxo migram por grupos e o
fake perde o papel de default (ADR-018 atualizada com o estado real).

### Ajustes na suíte durante a Fase 5

Aplicando o fechamento acima, a suíte foi ajustada ao alvo (sem cristalizar descartes), em três rodadas:

- **Primeira rodada:** +8 cenários (`GetByDomain_Unknown…`, `GetKey_UnknownKeyId_Throws…`, leituras de
  registros logicamente expirados/consumidos em AT/RT/AC/CN/SS, `Read_UnreadableId_ReturnsNull` do message
  store) e 2 reforçados (`GetKeys` asserta a ordem da requisição; `End` de sessão asserta idempotência).
- **Segunda rodada (revisão):** +7 cenários falsificando as regras fechadas — bordas inclusivas de keys com
  `NotBefore == now` e `Expires == now`; identificadores que diferem apenas por casing devolvem ausência
  (realm path/domain, client id, jti, handle de refresh, par subject+client de consent, sid) — crítico para
  a paridade SQLite×PostgreSQL sob DF18; e o cenário de exclusão de realm passou a semear e sondar **todos
  os oito accessors realm-bound** (ST-04..ST-11), não apenas o de authorization codes.
- **Terceira rodada (fechamento residual):** +8 cenários cobrem a borda inclusiva de KY-03 e casing Ordinal
  de key id, authorization code, authorize-parameters handle, scope/resource URI, scopes de consent e campos
  de sessão (client, subject e sid excetuado); os cenários de realm id e revogações AT/RT também foram
  reforçados. O teste pós-delete foi alinhado ao contrato observável comum: binding recusado ou leitura vazia,
  mas nunca dados do realm excluído.

Nenhum cenário existente precisou ser removido: nenhum teste asserta comportamento classificado `descartar`.
Total: 101 cenários verdes contra a fixture `MemoryStorage`.

## Handoff para as próximas fases

- **Fase 2:** confirmar exatamente um owner por linha, detalhar dependências Configuration×Operational e manter
  resources bloqueados; documentar RL-07 sem escolher o seam administrativo cross-family.
- **Fase 3 (concluída):** cenários provider-neutral criados em `Tests.Storage`; ver a seção
  "Contract tests provider-neutral — Fase 3" e a tabela de aceites futuros registrados.
- **Fase 4 (concluída):** seeds, composições e as 56 referências diretas decompostas com destino por
  categoria; ver a seção "Seeds, dados globais e acessos diretos — Fase 4" e o gate do Plano 4.
- **Fase 5 (concluída):** todos os `avaliar` resolvidos, políticas de duplicidade/comparadores/expiração/
  ausência fechadas por operação, mudanças públicas MP-1..MP-10 listadas e ordem de migração produzida; ver
  "Paridade final e ordem de migração — Fase 5". O baseline está encerrado; os consumidores deste artefato
  são `plan-data-configuration-storage.md` (P2), `plan-data-operational-storage.md` (P3) e
  `plan-data-test-migration.md` (P4).
