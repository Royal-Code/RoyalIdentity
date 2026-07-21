# Matriz: baseline dos contratos de storage do IdP

## Estado

Inventário estático produzido na Fase 1 de
[plan-data-storage-baseline.md](plan-data-storage-baseline.md), em 2026-07-21.

Esta versão descreve o código existente; não transforma automaticamente comportamento do fake em contrato durável.
As classificações marcadas `avaliar` serão fechadas até a Fase 5, conforme DF3. As classificações `preservar`,
`descartar` e `substituir` abaixo já possuem fonte normativa ou decisão fechada.

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

- **Owner:** `Configuration`, `Operational`, `Infrastructure` ou `Gateway`. A confirmação exaustiva de ownership e
  dependências cross-store pertence à Fase 2.
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
- **Destino:** P2 = futuro `plan-data-configuration-storage`; P3 = futuro `plan-data-operational-storage`; P4 = futuro
  `plan-data-test-migration`; `baseline` = contract tests deste plano; `adjacente` = fora de `Data.*`.

## Gateway `IStorage`

Implementação: `MemoryStorage`. `ServerOptions`, realms e authorize parameters usam estado global; os outros getters
criam stores ligados aos dicionários do `RealmMemoryStore` correspondente. `MemoryStorage` é singleton; a composição
registra `IStorage` transient apontando para essa mesma instância.

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| ST-01 | `ServerOptions { get; }` | Gateway / global / instância estática compartilhada | Retorna a mesma instância mutável. | `ConfigureRealmCookieAuthenticationOptions`, `RealmManager`, `DefaultEventDispatcher` | fluxo: testes de realm/options | ADR-013; live reference `descartar` (DF17); P2/baseline |
| ST-02 | `Realms { get; }` | Gateway / global / `realms` + `realmMemoryStore` | Cria nova facade `RealmStore` sobre os mesmos backings. | todos os callers de `IRealmStore` | direta: `RealmIsolationTests` | ADR-013; facade `preservar`; P2/baseline |
| ST-03 | `AuthorizeParameters { get; }` | Gateway / global / `authorizeParameters` | Cria nova facade sobre o mesmo dictionary global. | responses/page services do authorize flow | fluxo: login/consent callbacks | DF14; facade `preservar`; P3/baseline |
| ST-04 | `GetAccessTokenStore(Realm)` | Gateway / realm / `AccessTokens` | Nova facade; realm inexistente falha antes da operação. | token factory/validator, refresh e revocation handlers | direta: `RealmIsolationTests`; fluxo: token/revocation | DF6; binding `preservar`; P3/baseline |
| ST-05 | `GetRefreshTokenStore(Realm)` | Gateway / realm / `RefreshTokens` | Idem. | token factory, `LoadRefreshToken`, refresh/revocation/session revocation | direta/fluxo: `RealmIsolationTests`, `RefreshTokenTests`, `SessionLifecycleTests` | DF6; binding `preservar`; P3/baseline |
| ST-06 | `GetAuthorizationCodeStore(Realm)` | Gateway / realm / `AuthorizationCodes` | Idem. | `DefaultCodeFactory`, `LoadCode` | direta/fluxo: `RealmIsolationTests`, `CodeTokenTests` | DF6; binding `preservar`; P3/baseline |
| ST-07 | `GetUserConsentStore(Realm)` | Gateway / realm / `UserConsents` | Idem. | `DefaultConsentService` | direta/fluxo: `RealmIsolationTests` e authorize/consent | DF6; binding `preservar`; P3/baseline |
| ST-08 | `GetKeyStore(Realm)` | Gateway / realm / `KeyParameters` | Idem. | `DefaultKeyManager` | fluxo: signing/JWKS/key jobs | DF6/DF9; binding `preservar`; P2/baseline |
| ST-09 | `GetClientStore(Realm)` | Gateway / realm / `Clients` | Idem. | middleware, validators, secret evaluators, sign-out | direta: `RealmIsolationTests`; fluxo: endpoints | DF6; binding `preservar`; P2/baseline |
| ST-10 | `GetResourceStore(Realm)` | Gateway / realm / resources/scopes | Nova facade e novos índices derivados do estado corrente. | discovery, client/resource decorator, code/refresh handlers | direta: `ResourceStoreTests`, `RealmIsolationTests` | DF6/DF22; binding `preservar`, persistência bloqueada; baseline/redesign |
| ST-11 | `GetUserSessionStore(Realm)` | Gateway / realm / `UserSessions` | Nova facade ligada ao realm. | session service, code/sign-out/end-session/session revocation | direta/fluxo: suites de sessão/logout | ADR-014/DF6; binding `preservar`; P3/baseline |

## Configuração

### `IRealmStore`

Implementação: `RealmStore`. Comparações atuais de id usam a chave do dictionary; path/domain usam `==`. As leituras
retornam live references e, exceto a enumeração, ignoram CT.

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| RL-01 | `GetByPathAsync(path, ct)` | Configuration / global / scan de `realms.Values` | Primeiro match exato ou `null`; ordem incidental se houver duplicidade. | realm discovery; `RealmManager.CreateAsync` | fluxo: realm discovery/options | isolamento/product; ausência/comparador `avaliar` (DF18/DF25); P2/baseline |
| RL-02 | `GetByPath(path)` | Configuration / global / mesmo scan | Versão síncrona do lookup anterior. | cookie authentication options | fluxo: autenticação/cookie | `substituir` por API async (DF23); P2 |
| RL-03 | `GetByIdAsync(id, ct)` | Configuration / global / chave de `realms` | Match exato ou `null`. | `KeyCacheEntry`, `DefaultSignOutManager`, `RealmManager` | direta/fluxo: `RealmIsolationTests`, realm/session | isolamento/product; ausência/comparador `avaliar`; P2/baseline |
| RL-04 | `GetByDomainAsync(domain, ct)` | Configuration / global / scan de `realms.Values` | Primeiro match exato ou `null`; ordem incidental se duplicado. | `RealmManager.CreateAsync` (unicidade) | fluxo: criação de realm; sem teste direto localizado | unicidade é premissa do caller; comparador/ausência `avaliar`; P2/baseline |
| RL-05 | `GetAllAsync(ct)` | Configuration / global / `realms.Values` | Conjunto de live references; observa cancelamento entre itens e encerra normalmente. | `FirstKeyJob` | fluxo: inicialização de keys | DF24: ordem incidental `descartar`; cancelamento/resultado `avaliar`; P2/baseline |
| RL-06 | `SaveAsync(realm, ct)` | Configuration / global / `AddOrUpdate` + `TryAdd` do backing do realm | Upsert/replace do `Realm`; criar inicializa backing vazio, atualizar preserva o backing existente. | `RealmManager`; setup de testes/options | direta/fluxo: realm/options/isolation | duplicate-write `avaliar` por operação (DF16); live reference `descartar`; P2/baseline |
| RL-07 | `DeleteAsync(realmId, ct)` | Configuration + efeito Operational / global | `false` se ausente/interno; para realm comum remove fisicamente realm e todo `RealmMemoryStore`. Não alcança `UserAccounts`. | somente testes; nenhum caller de produção ou `IRealmManager.DeleteAsync` | direta: dois cenários em `RealmIsolationTests` | proteção de internal `preservar`; hard delete config `substituir` por tombstone + purge Operational (DF20); seam cross-family pendente; P2/P3/plano admin |

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
| RS-05 | `ResolveAuthorizedSubsetAsync(...)` | Configuration sem estado próprio / realm por receiver | Garante subset de resource indicators, downscope coerente e mapeia falhas para `invalid_target`/`invalid_scope`; pode chamar RS-04 até três vezes. | `AuthorizationCodeHandler`, `RefreshTokenHandler` | direta/fluxo: `CodeTokenTests`, `RefreshTokenTests`, `ResourceStoreTests` | semântica `preservar` (ADR-012/RFC 8707 já adotada); baseline/redesign |

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
| MS-01 | `WriteAsync<T>(message, ct)` | Infrastructure / n/a / Data Protection | Serializa JSON, protege bytes e devolve ciphertext Base64Url completo; CT sem efeito. | end-session handler; login/consent/end-session page services | fluxo: `EndSessionTests`, back-channel logout | classificação infra `preservar` (DF14); formato/mutabilidade `avaliar`; adjacente/baseline |
| MS-02 | `ReadAsync<T>(id, ct)` | Infrastructure / n/a / Data Protection | Base64Url decode→unprotect→deserialize; qualquer falha é logada e retorna `null`; leitura repetível. | sign-out e page services | fluxo: end-session/logout; sem teste direto de tamper | fail-closed como ausência `avaliar`; adjacente/baseline |
| MS-03 | `DeleteAsync(id, ct)` | Infrastructure / n/a / sem backing | No-op incondicional. | page/flow cleanup quando aplicável | lacuna direta | no-op é específico da implementação e `avaliar`; adjacente/baseline |

### `IReplayCache`

Implementações: `DefaultReplayNoCache` (default DI) e `DefaultReplayDistributedCache` (opcional).

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| RC-01 | `AddAsync(purpose, handle, expiration)` | Infrastructure / global / no-op ou `IDistributedCache` | Default não grava. Distribuído grava bytes vazios em `Prefix + purpose + handle`, sem delimitador, com expiração absoluta; API não recebe CT. | `PrivateKeyJwtSecretEvaluator` | lacuna relevante | proteção contra replay é regra de segurança; API/backing/atomicidade `avaliar`; adjacente/baseline |
| RC-02 | `ExistsAsync(purpose, handle)` | Infrastructure / global / constante false ou cache | Default sempre `false`; distribuído faz `GetAsync`. Check+add do caller não é atômico. | `PrivateKeyJwtSecretEvaluator` | lacuna relevante | implementação default não oferece proteção; `substituir`/decidir operação atômica em plano próprio; adjacente |

### `IStorageProvider` e `IStorageSession`

`StorageProvider` implementa ambas as interfaces e é singleton.

| ID | Operação | Owner / binding / backing atual | Comportamento atual | Consumidores | Cobertura atual | Fonte, classe inicial e destino |
|---|---|---|---|---|---|---|
| SP-01 | `IStorageProvider.CreateSession()` | Infrastructure / n/a / próprio singleton | Retorna `this` como `IStorageSession`; não abre conexão, contexto ou transação. | `KeyCacheEntry` | lacuna direta | lifetime seam `preservar`, não UoW global (DF21); implementação `substituir` no adapter EF; P2/adapter |
| SP-02 | `IStorageSession.GetStorage()` | Infrastructure / n/a / `MemoryStorage` singleton | Retorna sempre o mesmo `IStorage`. | `KeyCacheEntry` | lacuna direta | acesso dentro do lifetime `preservar` (DF21); implementação `substituir`; P2/adapter |
| SP-03 | `IDisposable.Dispose()` | Infrastructure / n/a / no-op | Não libera recurso. | `using` em `KeyCacheEntry` | lacuna direta | disposal/lifetime `preservar` (DF21); no-op do fake `descartar`; P2/adapter |

## Tipo de suporte `ResourceResolution`

Esses membros não persistem dados, mas fazem parte da superfície pública introduzida pela extensão RS-05.

| ID | Membro público | Semântica atual | Consumidores/cobertura | Fonte, classe inicial e destino |
|---|---|---|---|---|
| RR-01 | `Resources { get; }` | Resultado resolvido quando há sucesso; `null` em falha. | code/refresh handlers; tests desses grants | ADR-012; `preservar` enquanto RS-05 existir; baseline/redesign |
| RR-02 | `Error { get; }` | `null` em sucesso; erro OAuth em falha. | idem | ADR-012; `preservar`; baseline/redesign |
| RR-03 | `ErrorDescription { get; }` | Descrição estável criada pela extensão. | idem | texto exato `avaliar`; baseline/redesign |
| RR-04 | `Detail { get; }` | Lista/razão opcional para diagnóstico. | idem | conteúdo/formato `avaliar`; baseline/redesign |
| RR-05 | `IsSuccess { get; }` | Verdadeiro quando `Error is null`. | handlers/testes | coerência do resultado `preservar`; baseline/redesign |
| RR-06 | `Ok(resources)` | Constrói sucesso sem campos de erro. | RS-05 | coerência do resultado `preservar`; baseline/redesign |
| RR-07 | `Fail(error, description, detail)` | Constrói falha sem resources. | RS-05 | coerência do resultado `preservar`; baseline/redesign |

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
rg --files RoyalIdentity/Contracts/Storage RoyalIdentity/Users/Contracts |
  rg "(Storage|ResourceStoreExtensions)\.cs$"

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

## Handoff para as próximas fases

- **Fase 2:** confirmar exatamente um owner por linha, detalhar dependências Configuration×Operational e manter
  resources bloqueados; documentar RL-07 sem escolher o seam administrativo cross-family.
- **Fase 3:** criar cenários provider-neutral para todas as regras preservadas e para os gaps relevantes, sem acoplar
  os testes a dictionary/live references; requisitos `substituir` ficam como aceite dos providers futuros quando o
  fake transitório não puder cumpri-los.
- **Fase 4:** decompor as 56 referências diretas ao fake por setup, inspeção ou dependência real e mapear seeds.
- **Fase 5:** resolver todos os `avaliar`, atribuir política de duplicidade, comparadores, expiração e ausência por
  operação, e produzir a ordem final de migração.
