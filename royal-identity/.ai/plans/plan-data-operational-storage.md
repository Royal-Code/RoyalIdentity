# Plan: Persistência EF dos dados operacionais do IdP (`plan-data-operational-storage`)

## Status: EM EXECUÇÃO - Q1-Q12 fechadas; Fases 1-5 concluídas

## Progresso

`█████░░░` **62,5%** - 5 de 8 fases

| Fase | Estado |
|---|---|
| Fase 1 - contratos, fronteiras e modelo Operational | Concluida |
| Fase 2 - access tokens e consents sobre SQLite | Concluida |
| Fase 3 - sessões SSO sobre SQLite | Concluida |
| Fase 4 - authorization codes e consumo atômico | Concluida |
| Fase 5 - refresh tokens e transições condicionais | Concluida |
| Fase 6 - authorize parameters, cleanup e purge de realm | Não iniciada |
| Fase 7 - PostgreSQL, migrations, runner e gateway EF completo | Não iniciada |
| Fase 8 - paridade, fluxos e fechamento | Não iniciada |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluída, `%` e `X de N`). Exemplo de barra: `████░░░░`.
> Antes de fechar uma fase, confirme que decisões, critérios de aceite, testes e invariantes relacionados foram
> aplicados.

> **Gate de planejamento concluído:** Q1–Q12 foram respondidas e convertidas em decisões fechadas. A implementação
> permanece não iniciada até aprovação explícita. A matriz do baseline é normativa para as semânticas já resolvidas;
> este plano não as reinfere nem as altera implicitamente.

---

## Contexto

### Fontes verificadas

- [template-ai-implementation-plan.md](../references/template-plan/template-ai-implementation-plan.md) — shape e
  regras deste plano.
- [plan-data-macro.md](plan-data-macro.md) — o Plano 3 persiste a família Operational depois de Configuration e
  antes da troca do backing dos testes.
- [plan-data-storage-baseline.md](plan-data-storage-baseline.md) — decisões B-DF1–B-DF25, fronteiras e gates dos
  Planos 2/3/4.
- [plan-data-storage-matrix.md](plan-data-storage-matrix.md) — catálogo normativo das operações AT/RT/AC/CN/SS/AP,
  mudanças MP-2/MP-3/MP-5/MP-6/MP-7 e ordem de implementação por store.
- [plan-data-configuration-storage.md](plan-data-configuration-storage.md) — implementação concluída da família
  Configuration, mappings extensíveis, providers, runner e restrições de composição herdadas.
- [ADR-013](../../adrs/ADR-013.md) — `Data.*` puro, contratos no core e adapter único em
  `RoyalIdentity.Storage.EntityFramework`.
- [ADR-014](../../adrs/ADR-014.md) e [ADR-017](../../adrs/ADR-017.md) — ownership da sessão SSO no core,
  expiração, idle touch, `SecurityStamp` e revogação por subject.
- [ADR-018](../../adrs/ADR-018.md) — o fake in-memory é transitório e não deve receber nova paridade de produção.
- [architecture.md](../foundation/architecture.md) — `Data.*` não usa Feature-Slice nem referencia o core.
- [an-par-rfc-9126.md](../analisys/an-par-rfc-9126.md) — PAR permanece uma evolução separada; não deve ser
  confundido com a continuação interna de authorize parameters.
- `RoyalIdentity/Contracts/Storage/*.cs` e `RoyalIdentity/Users/Contracts/IUserSessionStore.cs` — contratos atuais.
- `RoyalIdentity/Contexts/Decorators/LoadCode.cs`, `LoadRefreshToken.cs`,
  `RoyalIdentity/Handlers/RefreshTokenHandler.cs` e `RoyalIdentity/Pipes.cs` — pontos atuais de consumo e
  concorrência.
- `RoyalIdentity/Contracts/Storage/IAccessTokenStore.cs`, `Models/Tokens/AccessToken.cs`,
  `Models/Tokens/RefreshToken.cs`, `Contracts/Models/RefreshTokenRequest.cs`, `Contracts/ITokenFactory.cs`,
  `Contracts/Defaults/DefaultTokenFactory.cs` e `Handlers/RevocationHandler.cs` — identidade por `jti`, dependência
  atual do access token anterior, factory pública, `at_hash` e comportamento de revogação.
- `RoyalIdentity/Options/RealmOptions.cs`, `Responses/HttpResults/LoginPageResult.cs`,
  `ConsentPageResult.cs`, `Endpoints/AuthorizeCallbackEndpoint.cs` e
  `Users/Defaults/DefaultAuthorizationContextResolver.cs` — gate `StoreAuthorizationParameters` e callers de AP.
- `old-is4/src/Storage/src/Stores/Serialization/ClaimLite.cs`,
  `old-is4/src/Storage/src/Models/RefreshToken.cs`,
  `old-is4/src/IdentityServer4/src/Stores/Default/DefaultGrantStore.cs`,
  `old-is4/src/IdentityServer4/src/Stores/Default/DistributedCacheAuthorizationParametersMessageStore.cs`,
  e `old-is4/src/EntityFramework.Storage/src/TokenCleanup/TokenCleanupService.cs` — precedentes históricos
  verificados para claims mínimas, JWT não persistido, snapshot de access token dentro do refresh token, digest de
  handles de grants e cleanup por expiração.
- `RoyalIdentity/Models/Tokens/*.cs`, `RoyalIdentity/Models/Consent.cs` e
  `RoyalIdentity/Users/UserSession*.cs` — grafos que precisam de round-trip completo.
- `Tests.Storage/Storage/Contracts/*.cs` — suíte provider-neutral já criada pelo baseline.
- `RoyalIdentity.Data.Configuration`, `RoyalIdentity.Storage.EntityFramework*`,
  `RoyalIdentity.Migrations` e `scripts/Test-ConfigurationPostgreSql.ps1` — precedentes locais implementados no
  Plano 2.
- [RFC 6749, §§4.1.2 e 10.5](https://www.rfc-editor.org/rfc/rfc6749.html#section-4.1.2) — authorization code
  curto, vinculado ao client/redirect e de uso único.
- [RFC 9700, §4.14](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.14) — confidencialidade, vínculo ao client
  e detecção de replay de refresh tokens.
- [ExecuteUpdate/ExecuteDelete — EF Core](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete)
  — operações em lote, contagem de linhas afetadas, ausência de concorrência automática e limite de retorno do
  registro alterado.
- [Transactions — EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions) — atomicidade de
  múltiplos comandos e limitações de transações entre contexts/providers.
- [Custom Migrations History Table — EF Core](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/history-table)
  — separação explícita da linha evolutiva Operational quando duas famílias usam o mesmo banco.
- [Unmapped JSON members — System.Text.Json](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members)
  — compatibilidade de payload Configuration aditivo com propriedades ausentes/desconhecidas.

### Estado atual do código (verificado em 2026-07-23)

- **Configuration EF está concluído:** existem `RoyalIdentity.Data.Configuration`,
  `RoyalIdentity.Storage.EntityFramework`, providers SQLite/PostgreSQL, migrations, SQL e runner. As portas
  Configuration são registráveis, mas não existe um `IStorage` EF produtivo parcial.
- **Operational EF ainda não existe:** não há `RoyalIdentity.Data.Operational`, entidades, mappings, contexts,
  migrations ou stores EF de tokens, codes, consents, sessões e authorize parameters.
- **O gateway mistura famílias:** `IStorage` expõe Configuration e Operational. A propriedade
  `AuthorizeParameters` ainda é global; os demais stores operacionais são realm-bound.
- **Authorization code usa get seguido de remove:** `LoadCode` lê, verifica client/redirect, remove o code e só
  depois valida expiração; `PkceMatchValidator` roda em seguida. Duas requests concorrentes podem ler o mesmo code
  antes de qualquer remoção.
- **Refresh token usa mutação + update genérico:** `LoadRefreshToken` lê e valida o token; o handler cria/renova
  access token antes de marcar `ConsumedTime` e chama `UpdateAsync`. O contrato não distingue primeira transição,
  repetição tolerada e conflito concorrente.
- **A política de claims no refresh está no lugar errado:** `Client.UpdateAccessTokenClaimsOnRefresh` é um `bool`
  com default `false`, persistido também na coluna Configuration `update_access_token_claims_on_refresh`. O alvo
  decidido remove a opção do client e torna a política exclusiva do realm, com `Current` como default.
- **A tolerância pós-consumo é comportamento de produto existente:** `RefreshTokenPostConsumedTimeTolerance`
  aceita repetição por uma janela, e `TimeSpan.MaxValue` permite o token reutilizável. O baseline exige que essa
  política não seja misturada acidentalmente com a primitiva atômica.
- **Authorize parameters não possui realm nem TTL:** login/consent escrevem uma `NameValueCollection`; resolvers
  podem lê-la repetidamente; o callback lê e apaga. O fake usa um dicionário global.
- **Sessões já possuem semântica rica no core:** `UserSession` tem `sid`, subject, método/idp, timestamps,
  `SecurityStamp`, estado ativo, expiração e clients deduplicados; o store possui touch e revogação em massa por
  subject.
- **A suíte provider-neutral já caracteriza o alvo preservado:** há cenários para realm isolation, comparadores,
  ausência, expiração lógica, idempotência, contagens efetivas e rematerialização. Os aceites de atomicidade, TTL,
  colisão e cleanup continuam reservados ao provider EF, não ao fake.
- **O host padrão continua in-memory:** `RoyalIdentity.Server` ainda chama `AddInMemoryStorage()`. A troca de backing
  dos hosts/testes pertence ao Plano 4.
- **O runner migra apenas Configuration:** `RoyalIdentity.Migrations` aceita somente provider/conexão/seed da
  família Configuration.
- **Resources continuam voláteis:** o bridge implementado no Plano 2 permanece necessário até o redesign B-DF22.

### Lacunas, conflitos e restrições

- O modelo híbrido, a tabela compartilhada `operation.protocol_artifacts` e as FKs exclusivas de ownership
  estrutural foram decididos em Q1. Handles e predicados de cleanup foram fechados em Q2/Q7.
- Authorization codes, refresh tokens, reference access tokens e authorize parameters persistem somente os digests
  definidos na DF38; valores bearer brutos não entram no banco.
- MP-2 e MP-3 usam os contratos atômicos das DF11/DF12 e as capability interfaces/fallback transitório da DF39.
- O fake permanece default até o Plano 4 sem ganhar paridade: o core usa o fallback legado somente quando a
  capability não existe; o adapter EF é obrigado a fornecê-la.
- A janela de authorize parameters usa `AuthorizationInteractionLifetime` inteiro em segundos, default 600,
  conforme DF16/DF40.
- Cleanup não possui grace histórica, segue os predicados por lifecycle da DF17 e suporta os modos
  `Hosted`/`External`.
- O comportamento atual de refresh não possui equivalência simples com o novo default `Current`:
  `UpdateAccessTokenClaimsOnRefresh=false` renova o access token anterior no caminho comum sem resource subset,
  preservando claims/audiences como um snapshot; quando há subset, mesmo `false` reexecuta a emissão. A mudança para
  `Current` é intencional, afeta o caminho default comum e entra somente junto da refatoração completa da Fase 5.
- `RefreshToken.AccessTokenId` sobrecarrega o claim `jti` com o id do access token anterior, enquanto
  `RefreshTokenRequest.AccessToken` é input público obrigatório de `ITokenFactory`. O alvo remove a primeira
  dependência, mas mantém o segundo apenas como fonte em memória da emissão, conforme DF41.
- `RefreshTokenHandler` calcula hoje o `at_hash` do identity token sobre o access token anterior. A Fase 5 corrige
  essa não conformidade para o token novo efetivamente devolvido, conforme DF42.
- `RealmOptions.StoreAuthorizationParameters=false` desvia login/consent para query string e não usa AP. Binding,
  digest, TTL e fail-closed do fluxo aplicam-se quando a opção está ligada; cleanup periódico continua removendo
  linhas históricas mesmo se o realm mudar depois para `false`, conforme DF16/DF17.
- A opção de janela de authorize interaction entra em `RealmOptions.Authentication`, portanto altera o grafo
  Configuration concluído no Plano 2. A mudança é aditiva no payload JSON e não requer migration relacional, mas
  precisa provar leitura de payload v1 anterior sem a propriedade e round-trip novo sem bump de versão.
- EF usa `__EFMigrationsHistory` por default. Como Configuration e Operational podem compartilhar o mesmo banco,
  as duas famílias precisam configurar sua history explicitamente. PostgreSQL usa
  `configuration.__EFMigrationsHistory` e `operation.__EFMigrationsHistory`; SQLite, que não possui schemas, usa
  `__ConfigurationMigrationsHistory` e `__OperationalMigrationsHistory`. O schema das entidades PostgreSQL não
  configura automaticamente o schema da history table.
- Configuration e Operational podem usar bancos distintos. Portanto, Operational não pode depender de FK ou
  transação com `configuration.realms`/`configuration.clients`; realm/client são vínculos lógicos por valor.
- Dentro da própria família Operational, somente `user_session_clients → user_sessions` usa FK de ownership.
  Demais referências, inclusive `protocol_artifacts.session_id`, são vínculos lógicos; `session_id` pode ser ausente
  em client credentials e `refresh_token.access_token_id` deixa de existir como dependência entre lifecycles.
- O fluxo atual persiste também access tokens JWT e o refresh recupera o access token original por
  `AccessTokenId`. O alvo decidido elimina essa dependência: JWT persistence vira opção por realm; reference tokens
  continuam obrigatoriamente persistidos; refresh usa claims atuais ou snapshot próprio conforme a opção do realm.
- Remover `Client.UpdateAccessTokenClaimsOnRefresh` afeta o schema relacional Configuration já entregue pelo Plano
  2. As migrations existentes não são reescritas: este plano adiciona migrations SQLite/PostgreSQL que removem
  `update_access_token_claims_on_refresh`, além da evolução aditiva do payload v1 de `RealmOptions`.
- O purge Operational após tombstone de realm é requisito fechado, mas o seam de orquestração cross-family foi
  deliberadamente adiado.
- O comportamento atual de repetição tolerada de refresh token precisa ser confrontado explicitamente com o
  hardening de replay da RFC 9700, sem expandir o plano por acidente.

### Superfícies impactadas a mapear

- `RoyalIdentity.Data.Operational` — entidades puras, context padrão, mappings neutros e operações de manutenção.
- `RoyalIdentity.Storage.EntityFramework` — materialização, stores operacionais, transições atômicas, gateway
  completo e lifecycle.
- `RoyalIdentity.Storage.EntityFramework.Sqlite` / `.PostgreSql` — mappings, contexts, migrations e registration
  extensions por provider.
- `RoyalIdentity` — MP-2, MP-3, MP-5, options e consumidores dos fluxos de code/refresh/authorize parameters.
- `RoyalIdentity/Models/Tokens/RefreshToken.cs`, `Contracts/Models/RefreshTokenRequest.cs`, `ITokenFactory` e
  handlers de authorization-code/refresh — remoção pública de `AccessTokenId`/claim `jti`, preservação de
  `RefreshTokenRequest.AccessToken` somente como fonte em memória e correção de `at_hash`.
- `RoyalIdentity.Storage.InMemory` — somente adaptação transitória mínima exigida por mudanças de shape; sem TTL,
  particionamento ou aceites de atomicidade.
- `RoyalIdentity.Migrations` — segunda família, uma ou duas conexões, sem auto-migrate no host.
- `Tests.Storage` — harness EF completo, contratos existentes e aceites exclusivos de Operational.
- `Tests.Identity` / `Tests.Integration` — regressões de consumo e emissão afetadas pelos novos contratos, ainda
  sobre a composição atual quando o Plano 4 não for pré-requisito.
- `scripts/` — SQL revisável e PostgreSQL 17 efêmero com porta dinâmica não padrão.

---

## Objetivo

1. Persistir access tokens, refresh tokens, authorization codes, consents, sessões SSO e authorize parameters em
   SQLite e PostgreSQL, com isolamento obrigatório por realm.
2. Implementar exatamente as semânticas AT/RT/AC/CN/SS/AP fechadas na matriz, incluindo create-only/upsert/no-op,
   materialização independente, ausência, comparadores e expiração lógica.
3. Substituir o consumo get+remove de authorization code por uma operação single-use atômica e o update trivial de
   refresh token por transições condicionais observáveis.
4. Tornar authorize parameters realm-bound, com TTL absoluto, leitura fail-closed, handle não adivinhável,
   regeneração em colisão e cleanup.
5. Entregar cleanup físico por tipo e purge Operational por realm sem apagar o tombstone/configuração.
6. Completar o gateway EF (`IStorage`, `IStorageProvider`, `IStorageSession`) como composição opt-in de
   Configuration + Operational, preservando contexts/bancos independentes e sem transação global.
7. Aplicar por realm a proteção de payload, a persistência de JWT e a origem das claims no refresh; remover a opção
   equivalente do client sem manter duas precedências concorrentes.
8. Estender migrations, SQL e runner separado; comprovar paridade SQLite/PostgreSQL e concorrência real antes do
   Plano 4.

## Fora de escopo

- Trocar o backing padrão de `RoyalIdentity.Server`, `Tests.Integration`, `Tests.Identity` ou demais testes HTTP —
  destino: `plan-data-test-migration.md` (Plano 4).
- Persistir/redesenhar resources e scopes — bloqueado por B-DF22.
- API/UI administrativa, writes de Configuration e coordenação completa da exclusão de realm —
  destino: plano administrativo/ADR própria.
- UserAccounts e seus dados — família independente, acessível somente pela `.Integration`.
- PAR/RFC 9126, `PersistentDataMessageStore`, `IAuthorizationRequestStore` e endpoint de PAR —
  destino: análise/backlog próprios.
- Persistir `IMessageStore` ou redesenhar `IReplayCache`.
- Cache geral sobre stores Operational — destino: plano de caching.
- Auditoria durável, outbox ou histórico forense de tokens/sessões.
- Sender-constrained tokens, DPoP/mTLS, famílias de refresh token e revogação automática por replay.
- Executar migrations ou seed automaticamente no host.
- Fornecer um `DbContext` combinado concreto; somente os mappings públicos que permitem ao consumidor criá-lo.
- Adicionar lookup de sessão por subject (MP-9), explicitamente diferido pela DF43 enquanto não houver caller;
  revogação usa a operação em massa já existente.

---

### Convenção de referências de decisão

- `DFn` identifica uma decisão deste plano Operational.
- `B-DFn` identifica uma decisão de [plan-data-storage-baseline.md](plan-data-storage-baseline.md).
- `C-DFn` identifica uma decisão de [plan-data-configuration-storage.md](plan-data-configuration-storage.md).
- Referências a `DFn` sem prefixo nunca apontam implicitamente para outro plano.

## Decisões fechadas

- **DF1 — Ownership:** `RoyalIdentity.Data.Operational` contém entidades persistentes, context padrão, mappings
  neutros e operações de dados sem referência ao core. Somente `RoyalIdentity.Storage.EntityFramework` adapta
  modelos/contratos do IdP. Fonte: ADR-013/architecture.
- **DF2 — Separação combinável:** Configuration e Operational possuem DbContexts e conexões próprias, podendo
  apontar para bancos distintos ou para o mesmo banco. Mappings ficam em extensões públicas, permitindo context
  combinado de terceiros sem fornecê-lo no produto. O gateway padrão cria um DbContext e uma instância de
  `DbConnection` por família mesmo quando as connection strings apontam para o mesmo banco; o pooling continua a
  cargo do provider, sem compartilhamento explícito de conexão/transação. Fonte: C-DF2/C-DF3 e documentação
  EF de transações cross-context.
- **DF3 — Lifecycle:** DbContexts, factories e stores são scoped. `IStorageProvider.CreateSession()` cria um scope
  real e `IStorageSession.Dispose()` o encerra; sessão não é transação global. Fonte: C-DF6/C-DF20 e B-DF21.
- **DF4 — Providers e schemas:** SQLite e PostgreSQL são obrigatórios. PostgreSQL usa schema `operation`; SQLite
  usa os mesmos nomes de tabela sem schema. Fonte: Plano 2 Q12/C-DF18 e macro-plan.
- **DF5 — Isolamento:** toda PK/unique/query/mutação Operational inclui `RealmId` lógica ou fisicamente. Ids iguais
  em realms diferentes coexistem. Fonte: B-DF6.
- **DF6 — Vínculos cross-family são lógicos:** não há FK Operational → Configuration nem transação entre famílias,
  pois os bancos podem ser distintos. `realmId`/`clientId` preservam comparação Ordinal. Fonte: DF2 deste plano,
  B-DF18/B-DF21.
- **DF7 — Criação:** access token, refresh token, authorization code e sessão são create-only; duplicidade falha
  visivelmente e nunca sobrescreve. Consent é upsert pela chave `(realm, subject, client)`. Fonte:
  AT-01/RT-01/AC-01/SS-01/CN-01.
- **DF8 — Leituras e ausência:** lookups retornam `null` quando ausentes. Access token, refresh token, code, consent
  e sessão continuam materializáveis mesmo expirados/consumidos/inativos; somente authorize parameters filtra
  expirado e retorna `null`. Fonte: B-DF19/B-DF25 e matriz.
- **DF9 — Materialização:** toda leitura produz grafo independente; mutar o objeto devolvido não persiste sem
  operação explícita. Toda coleção e todo dado pertencente ao contrato Operational deve sobreviver ao round-trip;
  claims seguem o contrato mínimo da DF34 e credenciais de configuração seguem a exclusão da DF44. Fora dessas
  duas exclusões nomeadas, nenhuma perda é aceitável — e cada uma exige versão nova de payload para ser
  revertida. Fonte: B-DF17 e Q1 Parte 1.
- **DF10 — Comparadores:** identificadores operacionais, subject, client, sid, scope e handles usam comparação
  Ordinal/case-sensitive. Nenhuma collation default do provider redefine a semântica. Fonte: B-DF18.
- **DF11 — Authorization code:** o consumo no fluxo do token é single-use e atômico; recebe handle, client e
  redirect URI esperados, remove e retorna o code para apenas um concorrente. `null` cobre ausente, já consumido ou
  vínculo inválido sem revelar a causa. Expiração e PKCE continuam no pipeline depois do consumo; portanto, uma
  tentativa que obteve o code mas falhou nessas validações não o torna reutilizável. A remoção administrativa
  continua idempotente. A descrição observável do erro de redirect mismatch muda de `Invalid redirect_uri` para a
  mesma descrição genérica de code inválido; o código OAuth continua `invalid_grant`, sem oracle de vínculo.
  Fonte: B-DF15/MP-2 e Q4=A.
- **DF12 — Refresh token:** a materialização inclui `state_version`. `TryConsume` usa estado + versão esperada e
  distingue transição vencedora, conflito concorrente e token já consumido; qualquer atualização posterior do
  refresh reutilizável também exige a versão esperada. A tolerância é política separada: conflito nunca é sucesso e
  somente estado consumido rematerializado pode ser submetido à tolerância pelo caller. Fonte: B-DF15/MP-3 e Q5=A.
- **DF13 — Access token:** reference access tokens são sempre persistidos e seguem AT-01..AT-04; remoção em massa
  usa tipo + subject + client Ordinal e é idempotente. JWTs seguem `JwtAccessTokenPersistenceMode` do realm
  (`None`, `Metadata` ou `Full`, default `None`); somente `Full` conserva o compact JWT. Persistir/remover um JWT não
  constitui revogação efetiva, pois validação stateless não consulta o store. Refresh não depende de uma linha do
  access token anterior. Todo access-token artifact usa `SHA-256(jti)` como lookup digest; no modelo atual o bearer
  de reference token coincide com `jti`, enquanto o compact JWT nunca é lookup key. `RevocationHandler` recebe o
  token bruto: reference encontra seu `jti`; JWT bruto não encontra metadata/full e continua sem revogação stateful.
  O `jti` bruto não precisa de coluna projetada: o adapter rematerializa `Id`/`Token` a partir do argumento de lookup;
  isso evita reexpor o bearer reference em claro. Fonte: AT-01..AT-04, `IAccessTokenStore`, comparação com IS4 e
  decisão humana.
- **DF14 — Consent:** scopes permanecem dentro do consent, preservando casing; ausência/removal são
  null/idempotente e upsert torna a última escrita efetiva. Fonte: CN-01..CN-03.
- **DF15 — Sessão:** create-only; record-client deduplica por client Ordinal, preserva `FirstSeenAt` e atualiza
  `LastSeenAt` via `TimeProvider`; ausência em record/touch é no-op; end e revogação por subject são idempotentes e
  contam apenas mudanças efetivas. Fonte: SS-01..SS-06/ADR-017.
- **DF16 — Authorize parameters:** o accessor passa a ser realm-bound; write grava expiração absoluta calculada
  pelo `TimeProvider`; read é repetível dentro da janela e fail-closed depois; delete é idempotente; handle possui
  ao menos 128 bits de entropia e colisão é regenerada internamente. O lifetime por realm é
  `AuthenticationOptions.AuthorizationInteractionLifetime`, inteiro em segundos, com default `600` e validação
  `> 0`. A expiração persistida é `now + lifetime seconds`; alterar a opção não reinterpreta registros existentes.
  O fluxo de write/read/delete usa essas regras somente quando `RealmOptions.StoreAuthorizationParameters=true`;
  com `false`, login/consent mantêm os parâmetros na query string e resolver/callback não consultam o store. Cleanup
  periódico continua elegível para linhas criadas anteriormente, independentemente do valor atual da opção. Fonte:
  MP-5, Q6=A, Q12=B e comportamento atual de login/consent/callback.
- **DF17 — Cleanup separado:** validação lógica não depende da execução do purge. Cleanup físico é por tipo, em
  batches e idempotente, sem grace de retenção histórica. Authorization code consumido é removido pela própria
  operação atômica; code abandonado, access token e authorize parameters tornam-se elegíveis ao expirar. Refresh
  token torna-se elegível ao expirar ou, se consumido, quando terminar sua tolerância pós-consumo; tolerância
  infinita conserva-o até expiração ou remoção explícita. Consent com expiração torna-se elegível ao expirar;
  consent sem expiração permanece até remoção explícita/purge do realm. Sessão torna-se elegível ao expirar ou
  atingir estado terminal por end/revogação. Lazy cleanup de AP pode ocorrer na leitura. Refresh conserva o
  grant/snapshot que seu modo exige e nunca prolonga a retenção da linha do access token anterior. A manutenção é
  reutilizável e suporta dois modos de execução explicitamente selecionados: hosted worker ou comando/job externo;
  exatamente um modo fica habilitado, nunca dois schedulers concorrentes pela configuração do produto. Fonte:
  B-DF19/MP-6, DF31/DF32, Q7=A e Q8=C.
- **DF18 — Realm deletion:** Configuration conserva tombstone/path/domain e Operational apaga fisicamente seus
  dados. Esta fase entrega o purge isolado por uma porta de manutenção em `Storage.EntityFramework`, expressa em
  primitivas e fora de `IStorage`; coordenação com Configuration/UserAccounts continua futura. Fonte: B-DF20/MP-7 e
  Q9=A.
- **DF19 — I/O:** todo acesso EF é assíncrono, propaga `CancellationToken` até o provider e não abre conexão em API
  síncrona. Fonte: B-DF23.
- **DF20 — Ordenação:** nenhuma listagem Operational recebe order implícito; só se adiciona ordem quando existir
  significado de negócio. Fonte: B-DF24.
- **DF21 — Gateway completo, mas opt-in:** após Operational, `Storage.EntityFramework` pode compor
  Configuration + Operational em `IStorage`/`IStorageProvider`/`IStorageSession`. O host padrão continua in-memory
  até o Plano 4. Fonte: C-DF20 e macro-plan.
- **DF22 — Configuration no gateway:** `IStorage.ServerOptions` usa o snapshot já implementado e não faz I/O
  síncrono; realms/clients/keys vêm das portas Configuration; resources usam o bridge volátil B-DF22. Fonte:
  Plano 2.
- **DF23 — Migrations:** o host nunca executa `EnsureCreated`, `Migrate` ou seed. Migrations ficam nos providers,
  SQL é versionado e o runner geral aceita Configuration, Operational ou ambas. Cada família configura
  `MigrationsHistoryTable` explicitamente em todos os pontos que constroem options para migrations:
  `configuration.__EFMigrationsHistory` e `operation.__EFMigrationsHistory` no PostgreSQL;
  `__ConfigurationMigrationsHistory` e `__OperationalMigrationsHistory` no SQLite. Em banco já migrado pelo Plano 2,
  o runner executa um bootstrap idempotente **antes** de qualquer `MigrateAsync`: se a history legada default existe
  e a nova de Configuration não existe, move/renomeia a tabela preservando todas as linhas; se ambas existem, valida
  e falha fechado diante de ambiguidade, sem merge/delete automático; banco vazio cria diretamente as histories
  novas. SQL manual versionado oferece a mesma transição. Fonte: C-DF11/C-DF16/C-DF21 e documentação EF de
  migrations history.
- **DF24 — Sem seed Operational:** dados operacionais nascem dos fluxos; runner não cria tokens, codes, consents ou
  sessões demo. Fixtures escrevem pelo data layer/store apropriado. Fonte: natureza da família e macro-plan.
- **DF25 — Fake transitório:** não adicionar TTL, particionamento, payload protection, cleanup ou testes de
  concorrência ao fake. Mudanças mínimas de shape não o tornam referência de paridade. Fonte: ADR-018/matriz.
- **DF26 — PAR e messages separados:** AP representa continuação interna multi-read; não é store de PAR nem
  `IMessageStore`. Fonte: análise PAR e B-DF14.
- **DF27 — Relógio:** timestamps de expiração, consumo, sessão e cleanup usam `TimeProvider` da composição, em UTC;
  nenhum store chama relógio de parede diretamente. Fonte: matriz/ADR-017.
- **DF28 — Logs:** handles bearer, payloads, claims, subjects, connection strings e material de proteção não
  aparecem em logs/erros. Telemetria usa tipo, resultado agregado e contagens. Fonte: requisitos OAuth e
  precedentes de segurança do Plano 2.
- **DF29 — Evolução aditiva de RealmOptions:** a janela de authorize interaction,
  `OperationalStorageOptions` e `RefreshTokenOptions` alteram a família Configuration, mas não seu schema relacional.
  Propriedades ausentes no payload v1 materializam os defaults fechados; payload novo faz round-trip e
  `RealmOptionsPayloadSerializer.CurrentVersion` permanece `1`. Qualquer mudança que não satisfaça esses testes
  exige reabrir a decisão de versão antes de implementar. Fonte: Q6/Q12, DF30–DF32, C-DF5/C-DF25 e
  comportamento de membros JSON ausentes.
- **DF30 — Proteção Operational por realm:** `RealmOptions.OperationalStorage.PayloadProtectionProfile` seleciona
  por id um profile registrado pelo host; o default serializável é o id `default`, não um algoritmo/protector
  implícito. Secrets, chaves e key-ring paths nunca entram em Configuration. O envelope versionado persiste
  `protector_id`; novas escritas usam o profile atual e leitores anteriores continuam registrados para abrir linhas
  legadas. `Plain` exige registration + seleção explícitas e warning; profile ausente falha fechado, sem fallback.
  Campos relacionais consultáveis ficam em claro; o contexto autenticado inclui `realm_id`, artifact/record type,
  lookup key e payload version. Fonte: Q3=A e decisão humana.
- **DF31 — Persistência JWT por realm:** `RealmOptions.OperationalStorage.JwtAccessTokenPersistence` possui
  `None`, `Metadata` e `Full`, com default `None`. A opção afeta somente novas emissões e nunca desabilita a
  persistência obrigatória de reference tokens. `Metadata` não grava o compact JWT; `Full` grava o grafo completo e
  o compact JWT dentro do payload protegido conforme DF30. Fonte: decisão humana e precedente IS4.
- **DF32 — Claims no refresh por realm:** `RealmOptions.RefreshTokens.ClaimsMode` possui `Current` e `Snapshot`,
  com default `Current`. `Current` conserva somente o grant mínimo (subject/session/client/scopes/resources e
  vínculos futuros aplicáveis), revalida conta/sessão/configuração e reexecuta a emissão/consulta ao UserAccounts;
  nunca amplia o grant original. `Snapshot` conserva no próprio refresh payload os dados necessários para reproduzir
  as claims, sem depender da linha do access token. `Client.UpdateAccessTokenClaimsOnRefresh` é removido sem override
  por client. Fonte: decisão humana, `UserSession`, `DefaultProfileService` e precedente IS4.
- **DF33 — Remoção da opção do client:** remover `Client.UpdateAccessTokenClaimsOnRefresh`, a propriedade
  `ClientEntity`, mapping/materializers e a coluna Configuration `update_access_token_claims_on_refresh`. Migrations
  Configuration novas de SQLite/PostgreSQL removem a coluna; migrations já publicadas do Plano 2 não são reescritas.
  Não se infere configuração de realm a partir de clients possivelmente divergentes: payload v1 ausente adota
  `ClaimsMode.Current`. Hoje o default `false` renova claims/audiences do access token anterior quando não há
  resource subset e reemite quando há subset; portanto, `Current` muda intencionalmente o caminho default comum, não
  apenas clients divergentes. A propriedade/mapping e a migration SQLite são removidos somente na Fase 5 junto da
  nova emissão; PostgreSQL recebe a migration equivalente na Fase 7. Fonte: decisão humana e código atual.
- **DF34 — Claim payload mínimo:** o contrato persistido de claim contém somente `Type`, `Value` e `ValueType`, como
  o `ClaimLite` do IS4. `Issuer`, `OriginalIssuer` e `Claim.Properties` não possuem semântica Operational; o
  materializador cria a claim com issuer canônico/default. Metadados `Properties` de outros modelos continuam
  preservados. Uma futura dependência de metadata de claim exige nova versão explícita, nunca perda silenciosa.
  Fonte: Q1 Parte 1 e comparação com IS4.
- **DF35 — Integridade relacional interna:** FKs existem somente para ownership estrutural com lifecycle comum.
  `user_session_clients` referencia `user_sessions` no mesmo realm; referências entre agregados/lifecycles, como
  `protocol_artifacts.session_id`, são vínculos lógicos indexados. Não há cascades/restrict entre artifacts,
  consents e sessões que acoplem escrita, retenção ou cleanup independentes. Fonte: Q1 Parte 2=A.
- **DF36 — Topologia física:** `operation.protocol_artifacts` é a tabela compartilhada e discriminada para reference
  access tokens, refresh tokens, authorization codes e JWT metadata/full quando habilitado pelo realm. Consents,
  user sessions, session-clients e authorize parameters permanecem em tabelas próprias: cinco tabelas Operational
  de negócio no total. Stores continuam tipados; `artifact_type` integra toda PK/query/operação atômica e índices
  específicos/condicionais evitam write amplification indevida. Novo artifact com uma chave principal, realm,
  expiração e lifecycle compatível pode registrar discriminator/codec/version/policy sem nova tabela; necessidades
  de campos consultáveis, múltiplas chaves ou relações próprias exigem evolução relacional explícita. Fonte:
  Q1 Parte 3=A e comparação de performance/IS4.
- **DF37 — Escopo de replay do refresh:** este plano preserva
  `RefreshTokenPostConsumedTimeTolerance` e torna atômicas/observáveis somente as transições de estado. Famílias de
  refresh token, detecção e revogação automática por replay e sender constraints ficam para um hardening próprio;
  a tolerância existente não é descrita como mecanismo de detecção de replay. Fonte: Q10=A.
- **DF38 — Handles por digest:** authorization code, refresh token, authorize-parameters handle e o bearer handle
  de reference access token são localizados por digest SHA-256 determinístico com separação de domínio por
  `artifact_type`; o valor bruto não é persistido. Para `access_token`, a entrada é sempre o `jti`, conforme DF13;
  o bearer reference coincide hoje com ele, e JWT metadata/full usa o mesmo domínio de `jti`, nunca o compact JWT.
  O `jti`/bearer bruto também não recebe coluna projetada; o argumento de lookup rematerializa o identificador.
  `realm_id` e tipo integram a identidade/consulta, portanto digests iguais em realms ou tipos distintos não colidem
  semanticamente. O desenho mantém o precedente do IS4 para grants e o estende a authorize parameters; HMAC não é
  introduzido porque os handles gerados têm alta entropia e não justificam distribuição/rotação de uma chave no
  caminho crítico. Fonte: Q2=A, `IAccessTokenStore` e comparação com IS4.
- **DF39 — Compatibilidade transitória por capability:** MP-2 e MP-3 são expostos por capability interfaces
  distintas dos contratos CRUD legados e implementadas obrigatoriamente pelo adapter EF. Enquanto o host default
  permanecer in-memory, o core detecta a ausência da capability e usa explicitamente o fluxo legado não atômico; o
  fake não implementa locks, CAS, TTL ou nova paridade. A composição EF falha na validação se alguma capability
  estiver ausente e nunca pode alcançar o fallback. O fallback e a detecção desaparecem no Plano 4 junto da troca do
  backing default. Fonte: Q11=A e ADR-018.
- **DF40 — Unidade do lifetime de interação:** `AuthorizationInteractionLifetime` é um `int` em segundos dentro de
  `AuthenticationOptions`, seguindo os lifetimes atuais de `Client`. O default é `600`, seu copy constructor e
  payload Configuration preservam o valor, e zero/negativo falha na validação. Payload RealmOptions v1 anterior
  materializa `600` sem bump de versão ou migration relacional. Fonte: Q12=B, Q6=A e DF29.
- **DF41 — Refresh token sem identidade do access token anterior:** remover do modelo público
  `RefreshToken.AccessTokenId`, o parâmetro `accessTokenId` do construtor e o claim `jti` que hoje representa o
  access token anterior. O handle do refresh token continua sua própria identidade; não se cria outro `jti` para
  substituir a sobrecarga removida. `RefreshTokenRequest.AccessToken` e a assinatura de
  `ITokenFactory.CreateRefreshTokenAsync` permanecem: o objeto recém-emitido é somente fonte em memória dos
  scopes/resource URIs autorizados e, em `Snapshot`, das claims a copiar para o payload próprio. Nenhum identificador
  ou lookup do access token é persistido; refresh reutilizável deixa de reescrever claim `jti`. Fonte: modelos,
  factory/handlers atuais e DF32.
- **DF42 — `at_hash` no refresh:** quando o refresh emitir identity token, `AccessTokenToHash` recebe
  `newAccessToken.Token`, exatamente o access token devolvido na mesma resposta, nunca o token anterior. O cálculo
  independe de persistir o JWT e é coberto por regressão que usa valores antigo/novo distintos. Fonte:
  `RefreshTokenHandler`, semântica OIDC de `at_hash` e DF31/DF32.
- **DF43 — MP-9 explicitamente diferida:** lookup de sessão por subject não entra neste plano porque nenhum caller
  foi comprovado; revogação continua pela operação em massa existente e testes capturam o `sid` do fluxo. A matriz
  registra MP-9 como diferida/não requerida pelo P3, reabrível somente junto de um caller futuro. Fonte: inventário
  da matriz e escopo decidido deste plano.
- **DF44 — Credenciais de configuração fora do payload Operational:** `ResourceServer.Secrets`, alcançável pelo
  grafo `RequestedResources` de um authorization code, não entra no payload persistido. São credenciais de
  autenticação do resource server — configuração, não decisão de autorização —, não possuem nenhum leitor no
  caminho de code exchange e replicá-las em cada linha operacional de vida curta espalharia segredo sem ganho
  funcional. É uma exclusão nomeada da DF9, no mesmo formato da DF34: a coleção rematerializa vazia, o valor
  nunca aparece no JSON e uma futura dependência exige versão explícita de payload, nunca perda silenciosa. A
  exclusão é comprovada por teste que popula o segredo, verifica sua ausência no payload e a coleção vazia após
  o round-trip. Fonte: revisão da Fase 1, inventário de leitores de `ResourceServer.Secrets` no core e DF28.
- **DF45 — Coluna consultável é fonte única:** todo valor projetado em coluna relacional — realm, client,
  redirect URI, tipo de access token, sessão do code e timestamps — não é repetido no payload; a materialização
  o recebe da linha. Lookups, o consumo condicional da DF11 e os predicados de cleanup da DF17 avaliam as
  colunas, então uma segunda cópia no payload permitiria ao banco validar um client/redirect/expiração enquanto
  o objeto rematerializado carrega outro. Colunas que são projeção pura de conteúdo do payload — `subject_id` e
  o `session_id` de tokens, ambos derivados das claims — são a exceção: existem para índice e remoção por
  subject, são escritas da mesma origem e nunca são lidas de volta para o modelo. O lifetime deriva de
  `created_at_utc`/`expires_at_utc` e expiração anterior à criação falha fechado. Fonte: revisão da Fase 1,
  DF11/DF17/DF36.
- **DF46 — Capabilities atômicas por construção no adapter EF:** o aceite "a registration EF valida a presença
  das capabilities" é satisfeito em tempo de compilação: `IOperationalStoreFactory` devolve
  `IOperationalAuthorizationCodeStore`/`IOperationalRefreshTokenStore`, que compõem o contrato CRUD com MP-2/MP-3.
  Um store EF sem a capability não pode sequer ser devolvido pela factory, portanto o fallback transitório da
  DF39 é inalcançável a partir do adapter por construção — garantia mais forte que uma checagem de runtime, que
  poderia ser esquecida. Somente o fake, que nunca implementa as capabilities, alcança o fallback. Fonte:
  revisão da Fase 1 e DF39.

---

## Histórico de decisões

> Ao responder Q1–Q12, registrar aqui as opções consideradas, a resposta, as considerações verificadas e a
> conclusão antes de remover a pergunta de `Perguntas ao humano`.

### 2026-07-23 — Q1, Q3 e políticas correlatas de token/refresh

- **Q1 Parte 1:** escolhida **A**, modelo híbrido. Campos usados por lookup/revogação/cleanup ficam relacionais;
  grafos não consultados ficam em payload versionado. `ClaimPayload` foi reduzido a `Type`/`Value`/`ValueType`,
  alinhado ao IS4 e ao comportamento atual do core/UserAccounts.
- **Q1 Parte 2:** escolhida **A**. FKs somente para ownership estrutural; vínculos entre aggregates/lifecycles
  permanecem lógicos e indexados.
- **Q1 Parte 3:** escolhida **A**. `operation.protocol_artifacts` compartilha access/reference, refresh, code e JWT
  opcional; consent, session, session-client e authorize parameters permanecem separados.
- **Q3:** escolhida **A**, refinada para seleção por realm. O realm guarda somente um profile id; o host registra
  Data Protection/AES/Plain e seus secrets. Envelope guarda o protector usado, `Plain` nunca é fallback e profile
  ausente falha fechado.
- **Persistência JWT:** decisão nova fechada por realm: `None`/`Metadata`/`Full`, default `None`; reference tokens
  permanecem sempre persistidos. Armazenar JWT não é tratado como revogação.
- **Claims no refresh:** decisão nova fechada por realm: `Current`/`Snapshot`, default `Current`. `Current` reexecuta
  a emissão com claims atuais sem ampliar scopes/resources do grant; `Snapshot` guarda os dados no próprio refresh
  token. Nenhum modo depende da linha do access token anterior.
- **Client:** `Client.UpdateAccessTokenClaimsOnRefresh` é removido, sem override/precedência por client. A coluna
  Configuration correspondente recebe migrations novas; o novo default `Current` é intencional.

### 2026-07-24 — Q2 e Q4-Q12

- **Q4:** escolhida **A**. O consume de authorization code inclui handle, client e redirect esperados na operação
  atômica; retorna o code ou `null`, sem diferenciar externamente ausência, consumo anterior ou vínculo inválido.
- **Q5:** escolhida **A**. Refresh token passa a ter estado + versão; consumo distingue vencedor, conflito e estado
  já consumido, e updates posteriores também exigem a versão esperada.
- **Q6:** escolhida **A**. Authorize parameters têm lifetime default de 10 minutos por realm, posteriormente
  expresso como `600` segundos pela Q12.
- **Q8:** escolhida **C**. A mesma manutenção suporta hosted worker e job/comando externo, com um único modo
  explicitamente habilitado em cada composição.
- **Q9:** escolhida **A**. Purge Operational por realm é exposto por porta de manutenção no adapter EF, fora do
  core e de `IStorage`.
- **Q10:** escolhida **A**. A tolerância pós-consumo existente é preservada; famílias, revogação de replay e sender
  constraints permanecem fora deste plano.
- **Q2:** escolhida **A** após comparação com o IS4. Handles bearer/opaques usam digest SHA-256 separado por tipo;
  realm e tipo integram a identidade, e authorize parameters passam a seguir a mesma proteção dos grants.
- **Q7:** escolhida **A** após comparação com o IS4. Não existe grace histórica: cada tipo é elegível ao deixar de
  ser semanticamente observável, com refresh respeitando sua tolerância pós-consumo e consents sem expiry
  preservados até remoção explícita/purge.
- **Q11:** escolhida **A**. EF implementa capabilities atômicas obrigatórias; o core mantém fallback legado apenas
  para o fake default até o Plano 4, sem ampliar a implementação in-memory.
- **Q12:** escolhida **B**. `AuthenticationOptions.AuthorizationInteractionLifetime` usa `int` em segundos, default
  `600`, seguindo os lifetimes atuais de `Client`.

### 2026-07-24 — validação da revisão independente

- **Sequenciamento da flag:** achado confirmado. A remoção de
  `Client.UpdateAccessTokenClaimsOnRefresh`/mapping/coluna saiu das Fases 1/2 e foi movida para a Fase 5 junto da
  ativação efetiva de `Current`/`Snapshot`; a migration PostgreSQL equivalente permanece na Fase 7.
- **Mudança do default:** confirmada com nuance. `false` sem resource subset renova claims/audiences antigas
  (snapshot-like), enquanto `false` com subset já reemite. `Current` continua decidido como novo default, agora
  documentado como mudança intencional do caminho comum.
- **Lookup de access token:** corrigido para digest sempre derivado de `jti`; bearer reference coincide hoje com
  `jti`, e compact JWT nunca é chave nem mecanismo de revogação.
- **Superfícies de refresh:** `RefreshToken.AccessTokenId`, parâmetro/claim `jti` e reescrita no token reutilizável
  são removidos; `RefreshTokenRequest.AccessToken` permanece somente como fonte em memória.
- **`at_hash`:** elevado a decisão/aceite de conformidade sobre o novo access token devolvido.
- **Gate de AP:** TTL/digest/store realm-bound aplicam-se apenas com `StoreAuthorizationParameters=true`; o resolver
  passa a obter o `Realm` atual e o modo `false` continua por query string.
- **Ajustes de execução:** foram nomeados o teste de migrations history, os testes da flag e as implementações
  test-only de `IStorage` afetadas por MP-5.
- **Governança:** referências externas usam `B-DF`/`C-DF`; MP-9 foi fechada na matriz como diferida sem caller; a
  descrição genérica de redirect mismatch foi registrada como mudança observável da DF11.

---

## Design alvo

O design abaixo materializa as decisões Q1–Q12 já fechadas; nenhuma alternativa de pergunta permanece pendente.

### Contratos e bordas

- `IOperationalStoreFactory` é a entrada scoped do adapter para criar stores realm-bound de access token, refresh
  token, authorization code, consent, sessão e authorize parameters.
- `IStorage.AuthorizeParameters` é substituído por accessor realm-bound, alinhado aos demais stores. O nome exato
  deve seguir o padrão existente (`GetAuthorizeParametersStore(Realm)`). Login, consent, resolver e callback só o
  acessam quando `StoreAuthorizationParameters=true`; o resolver obtém o `Realm` atual, não apenas seu path.
- MP-2 entra como capability de consumo condicional de authorization code conforme DF11/DF39.
- MP-3 entra como capability de transição versionada de refresh token conforme DF12/DF39.
- A janela de authorize interaction vive em `RealmOptions.Authentication` como
  `int AuthorizationInteractionLifetime`, em segundos e com default `600`, conforme DF16/DF40.
  Essa é uma alteração aditiva da família Configuration: mantém payload version `1`, não gera migration relacional
  e exige teste de payload v1 anterior sem a propriedade além do round-trip novo (DF29).
- `RealmOptions.OperationalStorage` contém `PayloadProtectionProfile` e `JwtAccessTokenPersistence`; ambos são
  exclusivos do realm. O profile é apenas um identificador público resolvido pelo adapter/host, nunca material
  criptográfico.
- `RealmOptions.RefreshTokens.ClaimsMode` é a única política de origem das claims no refresh.
  `Client.UpdateAccessTokenClaimsOnRefresh` e sua coluna Configuration são removidos na Fase 5, junto da troca do
  handler para `Current`/`Snapshot`, conforme DF33; não existe override por client.
- `RefreshToken` perde `AccessTokenId`, o parâmetro correspondente e o claim `jti`; `RefreshTokenRequest.AccessToken`
  permanece somente como fonte em memória para montar o grant/snapshot, sem lookup ou id persistido, conforme DF41.
- O adapter expõe registration de profiles Operational nomeados. Exatamente um profile selecionado é usado para
  novas escritas de cada realm; o `protector_id` do envelope seleciona leitores legados.
- A manutenção de cleanup/purge não vira CRUD administrativo nem transação cross-family. O adapter expõe a porta
  definida na DF18; hosted worker e job/comando externo reutilizam a mesma implementação conforme DF17.
- O gateway EF completo compõe:
  - `IConfigurationSnapshot` para `ServerOptions`;
  - `IConfigurationStoreFactory` para realms, clients, keys e resources bridge;
  - `IOperationalStoreFactory` para dados operacionais;
  - um scope próprio por `IStorageSession`.
- Nenhum store aceita `Realm` vivo no projeto puro. O adapter extrai `realm.Id` e instancia uma porta ligada ao
  valor.

### Modelo, dados e persistência

O modelo físico aprovado usa `operation.protocol_artifacts` para access/reference, refresh, authorization code e
JWT opcional. `artifact_type` integra toda PK/query/operação; as projeções por tipo são:

```text
operation.protocol_artifacts [artifact_type = access_token]
  realm_id + artifact_type + lookup_digest PK [DF38; SHA-256(jti)]
  subject_id, client_id, session_id
  access_token_type
  created_at_utc, expires_at_utc
  payload_version + protected_payload      [DF30/DF31; ausente para JWT mode None]

operation.protocol_artifacts [artifact_type = refresh_token]
  realm_id + artifact_type + lookup_digest PK [DF38]
  subject_id, client_id, session_id
  created_at_utc, expires_at_utc, consumed_at_utc
  state_version                            [DF12]
  claims_mode
  payload_version + protected_payload      [grant mínimo Current ou snapshot próprio]
  sem access_token_id/claim jti             [DF41]
  index (realm_id, subject_id)

operation.protocol_artifacts [artifact_type = authorization_code]
  realm_id + artifact_type + lookup_digest PK [DF38]
  client_id, redirect_uri
  created_at_utc, expires_at_utc
  payload_version + protected_payload

operation.consents
  realm_id + subject_id + client_id        PK
  created_at_utc, expires_at_utc
  payload_version + protected_payload/scopes

operation.user_sessions
  realm_id + session_id                    PK
  subject_id
  authentication_method, identity_provider
  started_at_utc, last_seen_at_utc, expires_at_utc, ended_at_utc
  security_stamp, is_active
  payload_version + protected_payload      [somente se necessário]
  index (realm_id, subject_id, is_active)

operation.user_session_clients
  realm_id + session_id + client_id        PK
  first_seen_at_utc, last_seen_at_utc
  FK para user_sessions no mesmo realm

operation.authorize_parameters
  realm_id + handle_digest                 PK [DF38]
  created_at_utc, expires_at_utc
  payload_version + protected_payload
  index (realm_id, expires_at_utc)
```

- `expires_at_utc` é persistido para não recalcular validade com configuração alterada.
- `ended_at_utc` registra o estado terminal da sessão para cleanup imediato e indexável; end/revogação repetidos
  preservam o primeiro timestamp terminal.
- Não há FK para realm/client/subject em outras famílias.
- A única FK interna é `user_session_clients → user_sessions` no mesmo realm. Demais identificadores correlatos são
  vínculos lógicos indexados; nenhuma relação adicional é inferida apenas porque duas colunas carregam o mesmo valor.
- Reference access tokens sempre produzem artifact persistido. JWT produz nenhuma linha, metadata sem compact JWT ou
  payload completo conforme DF31; a policy efetiva é capturada na escrita e mudança posterior do realm não
  reinterpreta artifacts existentes. O lookup digest de qualquer access-token artifact deriva de `jti`;
  reference bearer coincide com ele no modelo atual, enquanto o compact JWT não participa da chave. `jti`/bearer
  não fica em coluna: `GetAsync(jti)` fornece o valor necessário para rematerializar `AccessToken.Id`/`Token`.
- Refresh não possui `AccessTokenId` nem claim `jti` do access token anterior. `Current` conserva o grant mínimo e
  reconsulta sessão/UserAccounts/configuração; `Snapshot` conserva seu próprio snapshot. Ambos preservam
  subject/client/scopes/resources originalmente autorizados e nunca ampliam o grant a partir de configuração atual.
- Nenhuma linha de authorize parameters é criada quando `StoreAuthorizationParameters=false`; nesse modo,
  login/consent/callback/resolver usam apenas a query string. TTL, fail-closed e lazy/periodic cleanup valem para
  registros criados quando a opção estava ligada.
- Índices de cleanup seguem os predicados da DF17: `protocol_artifacts` por
  `(artifact_type, expires_at_utc)` e refresh também por `(artifact_type, consumed_at_utc)`; consents/AP por
  `expires_at_utc`; sessions por `expires_at_utc` e `ended_at_utc`. Cleanup realm-bound/purge começa por `realm_id`.
  Os model tests comprovam a forma escolhida antes da migration SQLite inicial.
- Claims persistem somente `Type`, `Value` e `ValueType` conforme DF34; `Issuer`, `OriginalIssuer` e
  `Claim.Properties` são deliberadamente fora do contrato, enquanto properties próprias de code/outros modelos
  continuam no payload.
- Payloads possuem envelope/versionamento explícito, profile por realm e falham fechado quando ilegíveis.
- A infraestrutura criptográfica genérica comprovada em Configuration é extraída/reusada, mas contratos,
  purposes e contexto autenticado Operational permanecem próprios; `IKeyMaterialProtector` não é reutilizado como
  interface de payload operacional.
- Collections retornam comparadores equivalentes aos modelos do core; não herdam comportamento da collation.

### Arquitetura alvo

```text
RoyalIdentity.Data.Operational/
  OperationalDbContext.cs
  OperationalModelOptions.cs
  OperationalModelBuilderExtensions.cs
  Entities/
  Maintenance/
  (EF Core only; NO RoyalIdentity reference)

RoyalIdentity.Storage.EntityFramework/
  Operational/
    Materialization/
    Stores/
    Atomic/
    Cleanup/
  Storage/
    EntityFrameworkStorage.cs
    EntityFrameworkStorageProvider.cs
    EntityFrameworkStorageSession.cs
  Extensions/
  (references RoyalIdentity + Data.Configuration + Data.Operational)

RoyalIdentity.Storage.EntityFramework.Sqlite/
  OperationalSqliteDbContext.cs
  public Operational SQLite mapping extension
  design-time factory + Operational Migrations/

RoyalIdentity.Storage.EntityFramework.PostgreSql/
  OperationalPostgreSqlDbContext.cs
  public Operational PostgreSQL mapping extension
  design-time factory + Operational Migrations/

RoyalIdentity.Migrations/
  Configuration and/or Operational selection
  one or two provider/connection pairs
  no Operational seed

Tests.Storage/
  Operational/Support/
  Operational provider acceptances
  complete EF StorageContractHarness
```

Context combinado de terceiro:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
	base.OnModelCreating(modelBuilder);
	modelBuilder.ApplyRoyalIdentityConfigurationPostgreSqlMappings(configurationOptions);
	modelBuilder.ApplyRoyalIdentityOperationalPostgreSqlMappings(operationalOptions);
}
```

O context combinado usa um provider e uma conexão; contexts separados podem usar providers/conexões diferentes.
Nenhum código do produto coordena commit entre ambos.

### Segurança, concorrência e confiabilidade

- Create-only usa constraint única como autoridade; check prévio pode melhorar erro, mas não substitui a constraint.
- Upsert de consent precisa ser um resultado indivisível por chave composta; concorrentes não criam duplicatas.
- Code consumption precisa alterar/remover exatamente uma linha e retornar sucesso para no máximo um concorrente.
- Refresh consumption usa predicado de estado/versão e contagem de linhas afetadas; zero linhas é resultado de
  conflito/estado alterado, não sucesso silencioso.
- `ExecuteUpdateAsync`/`ExecuteDeleteAsync` não fornecem concorrência automática nem retornam o registro anterior.
  Cada provider pode usar transação curta, SQL com `RETURNING` ou combinação equivalente; a semântica pública e os
  testes são provider-neutral.
- Não misturar entidades tracked com operações set-based no mesmo context sem limpar/recarregar o tracker.
- Transações explícitas ficam limitadas à operação Operational que precisa delas. Nada envolve Configuration,
  UserAccounts, dispatch de eventos ou chamadas externas.
- Os testes concorrentes usam scopes/DbContexts/conexões independentes e barreira de início; concorrência simulada
  dentro do mesmo `DbContext` não é aceite válido.
- O profile de proteção efetivo é resolvido a partir do snapshot do realm antes da operação. Profile ausente,
  envelope adulterado, protector antigo indisponível ou incompatibilidade de contexto falham fechado; nenhuma dessas
  falhas tenta `Plain`.
- `JwtAccessTokenPersistence=Full` combinado com profile `Plain` exige opt-ins independentes e warning explícito,
  pois grava um bearer reutilizável em texto claro. O compact JWT nunca entra em coluna consultável, PK ou log.
- Revogação por token bruto encontra reference token porque seu bearer coincide com `jti`; JWT metadata/full não é
  encontrado pelo compact JWT e não simula revogação stateful.
- Identity token emitido em refresh calcula `at_hash` exclusivamente sobre `newAccessToken.Token`, o token devolvido
  na mesma resposta, conforme DF42.

**Cleanup e purge:**

- Toda operação de cleanup recebe `now`, batch size e `CancellationToken`; produção fornece `now` por
  `TimeProvider`.
- A implementação de manutenção é independente do scheduler. `CleanupExecutionMode` seleciona explicitamente
  `Hosted` ou `External`; no primeiro modo registra um hosted worker configurável, no segundo expõe o mesmo serviço
  ao comando/job externo sem registrar worker. Configuração ausente, inválida ou que tente ativar ambos falha na
  validação.
- Cada batch retorna contagens por tipo, não os registros removidos.
- AP expirado é ausente na leitura mesmo que o purge nunca rode; uma leitura pode remover preguiçosamente sem
  transformar falha de cleanup em falha do protocolo.
- Access token/code/consent/session/refresh seguem a elegibilidade da DF17 e nunca são filtrados antes disso por um
  lookup comum. Refresh não prolonga a retenção do access token anterior; a linha JWT/reference segue seu próprio
  expiry/estado, sem grace histórica.
- Purge de realm remove todas as tabelas Operational e children no realm alvo, é repetível e não observa outro
  realm com ids colidentes.
- Cleanup e purge não registram handles, subjects ou payloads.

### Compatibilidade, migração e rollout

- `RoyalIdentity.Server` continua com `AddInMemoryStorage()` neste plano.
- A registration EF completa é opt-in e exige as duas famílias; não existe modo produtivo “Operational EF +
  Configuration ausente”.
- O gateway padrão resolve um DbContext/conexão por família. Connection strings iguais podem reutilizar o pool do
  provider, mas o produto não compartilha instância de `DbConnection` ou `DbTransaction`; context combinado de
  terceiro permanece opt-in.
- A fixture EF completa substitui o composite test-only do Plano 2 para a contract suite, mas os testes HTTP só
  migram no Plano 4.
- O fake recebe apenas o mínimo necessário para compilar o accessor realm-bound de AP; não implementa as
  capabilities da DF39, seu dicionário continua global, sem TTL, e não executa os aceites EF.
- Migrations Configuration e Operational mantêm assemblies/contexts próprios e usam a topologia exata da DF23. No
  PostgreSQL, o mesmo nome `__EFMigrationsHistory` é isolado pelos schemas `configuration`/`operation`; no SQLite,
  os nomes são distintos por não existir schema. Quando duas famílias usam o mesmo banco, o runner primeiro conclui
  o bootstrap da history legada e depois aplica ambas sequencialmente, sem transação distribuída e sem misturar suas
  linhas evolutivas.
- O bootstrap de history é infraestrutura do runner, não migration de domínio: ele roda por conexão/família antes
  de o EF consultar sua history. Scripts manuais equivalentes ficam em
  `scripts/sql/migration-history/{sqlite,postgresql}/`.
- Migrations Configuration já geradas não são reescritas; design-time factories, runner e scripts idempotentes são
  reconfigurados/regenerados para a nova history. O bootstrap preserva os migration ids existentes e não cria uma
  migration de domínio artificial apenas para mover metadata do EF.
- Novas migrations Configuration removem `update_access_token_claims_on_refresh` depois de o core/adapter passarem a
  usar exclusivamente `RealmOptions.RefreshTokens.ClaimsMode`. Não há backfill client → realm: payload v1 sem a
  opção materializa `Current`. Testes cobrem clients legados com `true`/`false` e documentam a mudança intencional.
- Alterar o profile de proteção, o modo JWT ou o modo de claims do refresh afeta somente novas escritas/emissões.
  Envelopes antigos conservam seu `protector_id`; refresh tokens já emitidos conservam seu `claims_mode`; artifacts
  JWT existentes seguem legíveis/limpáveis sem reinterpretar a configuração atual.
- SQL fica em `scripts/sql/operational/{sqlite,postgresql}/`.
- O runner aceita executar somente Configuration, somente Operational ou ambas. Seed continua exclusivo de
  Configuration.

---

## Ordem de execução

1. **Fase 1 (fronteiras/modelo/contracts)** — fecha primeiro os shapes públicos e o model extensível.
2. **Fase 2 (access tokens/consents)** — inicia pelos stores de menor risco na ordem normativa.
3. **Fase 3 (sessões)** — entrega o agregado mutável e a revogação por subject.
4. **Fase 4 (authorization codes)** — introduz a primeira primitiva de consumo atômico.
5. **Fase 5 (refresh tokens)** — trata a máquina de estado e concorrência de maior risco.
6. **Fase 6 (AP/cleanup/purge)** — fecha TTL, manutenção e remoção por realm.
7. **Fase 7 (PostgreSQL/operação/gateway)** — valida o alvo produtivo e completa runner/composição.
8. **Fase 8 (paridade/handoff)** — prova contratos, fluxos e gates do Plano 4.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
dotnet test Tests.Architecture
dotnet test Tests.Storage
dotnet test Tests.Identity
dotnet test Tests.Integration
dotnet test RoyalIdentity.sln
```

---

## Fase 1 - contratos, fronteiras e modelo Operational

**Depende de:** aprovação explícita deste plano para iniciar.

**Escopo:** criar o projeto puro, mappings neutros, modelo completo, seams do adapter e mudanças públicas antes de
implementar stores.

**O que/como:**

- Adicionar `RoyalIdentity.Data.Operational` à solução, referenciando somente EF Core/BCL.
- Criar `OperationalDbContext`, `OperationalModelOptions` e extensão pública de mapping fora do context.
- Modelar `protocol_artifacts`, consents, sessions/session-clients e authorize parameters conforme DF34–DF36, com
  digests/índices finais conforme DF17/DF38 e sem FK cross-family.
- Criar accessor genérico `TContext : DbContext` no adapter; stores não dependem do context concreto.
- Implementar os contratos MP-2 e MP-3 conforme DF11/DF12 e a compatibilidade transitória da DF39.
- Adicionar `AuthorizationInteractionLifetime` como `int` em segundos, default `600`, conforme DF16/DF40,
  incluindo copy constructor e cobertura do serializer de Configuration; tratar a mudança como aditiva sobre
  payload v1, sem migration relacional/bump automático.
- Adicionar `OperationalStorageOptions` e `RefreshTokenOptions` a `RealmOptions`, com defaults/clone/serialização
  definidos pelas DF29–DF32.
- Manter temporariamente `Client.UpdateAccessTokenClaimsOnRefresh` e seu mapping em uso até a Fase 5; a nova opção
  de realm é aditiva nesta fase e ainda não muda o handler.
- Criar serializers/materializers versionados com round-trip completo e falha fechada.
- Extrair/reusar primitivas genéricas de envelope/proteção do Plano 2 sem reutilizar
  `IKeyMaterialProtector` como contrato Operational.
- Criar resolver de profiles Operational nomeados e capturar no envelope o protector efetivamente usado.
- Centralizar os nomes/schemas de migrations history definidos pela DF23 para que runner, design-time factories e
  fixtures não possam divergir.
- Adicionar regras de arquitetura para impedir referências proibidas.

**Tarefas:**

- [x] Atualizar solution/csproj e dependências.
- [x] Criar entidades, DbSets, mappings, constraints e índices provider-neutral.
- [x] Implementar a FK estrutural session-client → session e manter os demais vínculos lógicos conforme DF35.
- [x] Implementar `operation.protocol_artifacts` conforme DF36 e comprovar que todo store tipado inclui
  `artifact_type` em PK/query/mutação/cleanup.
- [x] Criar `IOperationalStoreFactory` e seam de `DbContext` genérico.
- [x] Alterar interfaces/consumidores somente até o ponto em que compilam; manter o comportamento nas fases por
  store.
- [x] Adaptar explicitamente `MemoryStorage`, `SessionLifecycleTests.FakeSessionStorage` e
  `SqliteConfigurationStorageHarness.ConfigurationCompositeStorage` ao accessor realm-bound de AP, sem atribuir ao
  fake TTL, particionamento real ou aceites de MP-5.
- [x] Implementar as capability interfaces e o fallback transitório da DF39 sem dar aceites novos ao fake.
- [x] Validar que `AuthorizationInteractionLifetime` é positivo, usa segundos e materializa `600` quando ausente em
  payload v1.
- [x] Criar testes de model metadata, payload version e round-trip por tipo.
- [x] Cobrir payload Configuration v1 anterior sem as novas options, defaults `Jwt=None`/`Claims=Current`, profile
  default e round-trip novo ainda em version `1`.
- [x] Testar profiles por realm: dois realms/protectors, rotação com leitor anterior, profile ausente, envelope/AAD
  adulterado e `Plain` explicitamente registrado.
- [x] Testar `ClaimPayload` mínimo e provar que properties próprias de authorization code continuam preservadas.
- [x] Provar que nenhum payload duplica coluna consultável e que a materialização toma esses valores da linha
  (DF45), inclusive falha fechada para timestamps incoerentes.
- [x] Provar a exclusão da DF44: segredo de resource server ausente do payload e coleção vazia no round-trip.
- [x] Cobrir coleção contratual omitida e explicitamente `null`, inclusive no grafo aninhado do code.
- [x] Fixar em teste a topologia das quatro histories da DF23 sem abrir conexão.
- [x] Criar teste de context combinado de prova, inicialmente com mappings neutros.

**Critérios de aceite:**

- `Data.Operational` não referencia core, adapter, providers, host ou Configuration.
- Toda entidade possui realm na chave/índice aplicável e nenhuma FK cross-family.
- Um `DbContext` arbitrário aplica o mapping sem herdar de `OperationalDbContext`.
- Contratos MP-2/MP-3 não possuem implementação default não atômica; somente o core aciona o fallback quando a
  capability está ausente, e a composição EF garante sua presença por construção conforme DF46.
- Nenhum payload duplica valor de coluna consultável; a materialização toma esses valores da linha e timestamps
  incoerentes falham fechado (DF45).
- Coleção contratual ausente ou explicitamente `null` no payload falha fechado; somente `Properties` do code e
  `Scopes` do consent distinguem ausência de vazio, por serem nulláveis por contrato.
- `ResourceServer.Secrets` não aparece no payload e rematerializa vazio (DF44).
- Realm options continuam round-trip após as novas opções; payload v1 anterior materializa
  `AuthorizationInteractionLifetime=600` e os demais defaults sem migration relacional ou bump de payload version.
- `ClaimsMode` está disponível e serializável, mas a propriedade do client e o comportamento atual permanecem
  intactos até a refatoração indivisível da Fase 5.
- Nenhum secret de protector entra no payload Configuration; profile inexistente nunca cai para `Plain`.
- Todo payload inválido/version desconhecida falha sem materialização parcial.

**Testes:**

```powershell
dotnet build RoyalIdentity.sln
dotnet test Tests.Architecture
dotnet test Tests.Storage --filter "FullyQualifiedName~OperationalModel|FullyQualifiedName~OperationalPayload|FullyQualifiedName~OperationalContractsShape"
```

### Resultado da Fase 1

**Concluída em 2026-07-24**, com os ajustes da revisão externa aplicados no mesmo dia. Build da solução e
`dotnet test RoyalIdentity.sln` verdes: 920 aprovados, 9 ignorados (PostgreSQL opt-in), 0 falhas.
`git diff --check` sem erros.

**Arquivos criados**

- `RoyalIdentity.Data.Operational/` — projeto puro (EF Core Relational apenas, sem `Microsoft.AspNetCore.App`, sem
  project reference): `OperationalDataAssemblyMarker`, `OperationalModelOptions`, `OperationalDbContext`,
  `OperationalModelBuilderExtensions` e `Entities/` (`ProtocolArtifactEntity`, `ProtocolArtifactTypes`,
  `ConsentEntity`, `UserSessionEntity`, `UserSessionClientEntity`, `AuthorizeParametersEntity`).
- `RoyalIdentity/Contracts/Storage/` — `ISingleUseAuthorizationCodeStore` (MP-2), `IVersionedRefreshTokenStore` +
  `RefreshTokenTransition`/`RefreshTokenTransitionOutcome` (MP-3), `IAuthorizationCodeConsumer` e
  `IRefreshTokenConsumer` (seams de detecção/fallback da DF39).
- `RoyalIdentity/Contracts/Defaults/` — `DefaultAuthorizationCodeConsumer`, `DefaultRefreshTokenConsumer`.
- `RoyalIdentity/Options/` — `OperationalStorageOptions`, `JwtAccessTokenPersistenceMode`, `RefreshTokenOptions`,
  `RefreshTokenClaimsMode`.
- `RoyalIdentity.Storage.EntityFramework/Migrations/StorageMigrationsHistory.cs` — topologia única das quatro
  histories da DF23 (`StorageFamily` × `StorageProviderKind`) mais a legada, consumida por runner, design-time
  factories e fixtures.
- `RoyalIdentity.Storage.EntityFramework/Operational/` — `IOperationalDbContextAccessor` +
  `OperationalDbContextAccessor<TContext>`, `Stores/` (`IOperationalStoreFactory` e os contratos compostos
  `IOperationalAuthorizationCodeStore`/`IOperationalRefreshTokenStore` da DF46), `Protection/` (contexto
  autenticado, envelope versionado, resolver, profiles AES-GCM/Data Protection/Plain, exceção fail-closed) e
  `Materialization/` (`OperationalRecordTypes`, `OperationalRecordIdentities`, `OperationalLookupDigest`, codec
  versionado, serializers de access token, refresh token, authorization code, consent e authorize parameters,
  mais os DTOs de payload).
- `RoyalIdentity.Storage.EntityFramework/Security/Cryptography/AesGcmCipher.cs` — primitiva genérica extraída,
  agora compartilhada pelo protector de key material (Configuration) e pelo profile AES-GCM Operational.
- `RoyalIdentity.Storage.EntityFramework/Extensions/OperationalServiceCollectionExtensions.cs` — registration do
  seam scoped, dos serializers e dos profiles nomeados (nenhum profile é implícito).
- Testes: `Tests.Architecture/OperationalStorageBoundaryTests.cs`,
  `Tests.Architecture/OperationalModelExtensibilityTests.cs`, `Tests.Storage/Operational/` (`OperationalModelTests`,
  `OperationalModelMigrationsHistoryTests`, `OperationalPayloadTests`, `OperationalPayloadProtectionTests`,
  `OperationalContractsShapeTests`, `Support/OperationalTestData`).

**Arquivos alterados**

- `IStorage.AuthorizeParameters` → `IStorage.GetAuthorizeParametersStore(Realm)` (MP-5), com `LoginPageResult`,
  `ConsentPageResult`, `AuthorizeCallbackEndpoint`, `DefaultAuthorizationContextResolver`, `MemoryStorage`,
  `SessionLifecycleTests.FakeSessionStorage`, `SqliteConfigurationStorageHarness.ConfigurationCompositeStorage` e
  `AuthorizeParametersStoreContractTests` adaptados. O resolver passou a obter o `Realm` atual
  (`TryGetCurrentRealm`) em vez de apenas o path — mudança já prevista pela Fase 6, antecipada aqui porque é o
  mínimo para compilar.
- `RealmOptions` ganhou `OperationalStorage` e `RefreshTokens` (com clone no copy constructor);
  `AuthenticationOptions` ganhou `AuthorizationInteractionLifetime` (int, segundos, default `600` em
  `Constants.Server`) e `Validate()`.
- `RefreshToken` ganhou `StateVersion` (propriedade do store, exigida pelo CAS da DF12).
- `AuthorizationCode` ganhou um construtor de rematerialização que recebe o handle bruto; o construtor de emissão
  continua gerando o code e agora delega a ele.
- `AesKeyMaterialProtector` passou a usar `AesGcmCipher` preservando formato e exceções observáveis.
- `Tests.Architecture/ConfigurationStorageBoundaryTests` reflete o adapter com três project references e proíbe
  `Data.Operational` no core e no host.

**Desvios e decisões tomadas na execução**

- **`user_sessions` não tem `payload_version`/`protected_payload`.** O design marcava o par como "somente se
  necessário"; todo campo do `UserSession` mapeia para coluna consultável e os clients são tabela filha, então
  nada da sessão é opaco.
- **`OperationalLookupDigest` não entra no digest com o realm.** O realm é parte da identidade da linha
  (PK), não do hash: assim o mesmo handle em dois realms é linha diferente, e não hash diferente do mesmo valor.
- Os seams `IAuthorizationCodeConsumer`/`IRefreshTokenConsumer` estão registrados no DI e cobertos por teste, mas
  ainda **não** são chamados por `LoadCode`/`RefreshTokenHandler` — o plano manda o comportamento mudar nas Fases
  4 e 5, e esta fase só fecha contrato e fallback.
- Os profiles de proteção usam `IOperationalPayloadProtector`, contrato próprio; `IKeyMaterialProtector` não foi
  reutilizado (DF30). Só a mecânica do AES-GCM é compartilhada.

**Ajustes aplicados após revisão externa (2026-07-24)**

- **DF45 — coluna consultável virou fonte única.** Os payloads duplicavam `ClientId`, realm, tipo, redirect URI,
  sessão do code e timestamps, e a materialização confiava na cópia do payload enquanto lookups, o consumo
  condicional da DF11 e o cleanup avaliam as colunas. Os payloads perderam esses campos; a materialização passa a
  recebê-los por `AccessTokenIdentity`/`RefreshTokenIdentity`/`AuthorizationCodeIdentity`
  (`Operational/Materialization/OperationalRecordIdentities.cs`), o lifetime deriva dos dois timestamps e
  expiração anterior à criação falha fechado. Testes novos provam que nenhuma coluna consultável aparece no JSON
  e que a materialização segue a linha mesmo quando ela diverge do que o modelo tinha na escrita.
- **Falha fechada para coleção ausente completada.** As coleções contratuais passaram a `required` e o codec usa
  `RespectNullableAnnotations`, então membro omitido **e** `null` explícito falham — antes só `null` explícito era
  detectado, por guardas manuais, e a omissão virava coleção vazia silenciosa. Cobertura por teoria removendo e
  anulando cada coleção, inclusive no grafo aninhado do authorization code. `Properties` do code e `Scopes` do
  consent seguem nulláveis por contrato, porque neles ausência é distinta de vazio.
- **DF46 — capabilities por construção.** `IOperationalStoreFactory` passou a devolver
  `IOperationalAuthorizationCodeStore`/`IOperationalRefreshTokenStore`, que compõem CRUD + MP-2/MP-3. O aceite
  "a registration EF valida a presença" é satisfeito pelo compilador: um store EF sem a capability não pode ser
  devolvido, então o fallback da DF39 é inalcançável a partir do adapter.
- **DF44 — exclusão de `ResourceServer.Secrets` formalizada.** Deixou de ser desvio e virou decisão fechada, com
  DF9 emendada, invariante e teste dedicado.
- **Indentação:** a revisão apontou os 11 arquivos novos do core como fora do padrão por usarem 4 espaços — o
  achado estava invertido. A regra do repositório é **4 espaços**, e o texto contraditório do `AGENTS.md` era o
  erro. Resolução: `AGENTS.md` passou a delegar o estilo a `.ai/rules/code-style.rules.md` ("Use 4 spaces for C#
  indentation"), `CLAUDE.md` foi corrigido, `.editorconfig` ganhou `indent_style = space` para a regra deixar de
  depender do default do editor, e os 287 arquivos `.cs` que ainda usavam tab de indentação — em todos os
  projetos, não só nos desta fase — foram convertidos. Nenhum `.cs` do repositório (fora de `old-is4/`) começa
  linha com tab.

**Comandos executados**

```powershell
dotnet build RoyalIdentity.sln
dotnet test Tests.Architecture
dotnet test Tests.Storage --filter "FullyQualifiedName~OperationalModel|FullyQualifiedName~OperationalPayload|FullyQualifiedName~OperationalContractsShape"
dotnet test RoyalIdentity.sln
git diff --check
```

---

## Fase 2 - access tokens e consents sobre SQLite

**Depende de:** Fase 1.

**Escopo:** criar refinamentos/migration SQLite e implementar AT-01..AT-04 e CN-01..CN-03.

**O que/como:**

- Criar extensão pública `ApplyRoyalIdentityOperationalSqliteMappings` e context/design-time factory.
- Gerar migration inicial Operational SQLite sobre o model completo.
- Configurar uma migrations history table exclusiva para Operational no SQLite; não reutilizar a history padrão de
  Configuration quando as famílias compartilham o arquivo/banco. Configuration passa a usar
  `__ConfigurationMigrationsHistory` e Operational usa `__OperationalMigrationsHistory`.
- Implementar o bootstrap SQLite da history Configuration legada `__EFMigrationsHistory` antes do primeiro
  `MigrateAsync` configurado com o novo nome.
- Implementar access-token store realm-bound create-only, lookup, remoção e remoção de reference tokens sobre
  `protocol_artifacts`.
- Aplicar `JwtAccessTokenPersistenceMode` na emissão: reference sempre persiste; JWT `None` não escreve,
  `Metadata` omite compact JWT e `Full` preserva o grafo completo protegido pelo profile do realm.
- Implementar consent store com upsert atômico por `(realm, subject, client)`.
- Criar harness SQLite Operational com migrations reais, nunca `EnsureCreated`.
- Reutilizar os contratos provider-neutral existentes e adicionar aceites de duplicidade/materialização/CT.

**Tarefas:**

- [x] Aplicar collation/índices SQLite compatíveis com comparação Ordinal.
- [x] Configurar e testar as histories SQLite de Configuration/Operational, inclusive no mesmo banco.
- [x] Atualizar runner, design-time factories e fixtures SQLite para consumir a mesma configuração centralizada de
  history.
- [x] Atualizar `SqliteConfigurationMigrationTests.Migrate_CreatesOnlyTheConfigurationTables` para excluir
  `__ConfigurationMigrationsHistory`/`__OperationalMigrationsHistory` conforme o banco exercitado, sem mascarar
  tabelas de negócio inesperadas.
- [x] Testar bootstrap SQLite: banco vazio; somente history legada; somente history nova; histories legada e nova
  simultâneas/ambíguas; repetição idempotente.
- [x] Implementar materialização completa de `AccessToken` e `Consent`.
- [x] Testar que `lookup_digest` de access token deriva sempre de `jti` em Reference/Metadata/Full; compact JWT
  diferente de `jti` nunca participa da PK.
- [x] Provar que não existe coluna de `jti` bruto, que o bearer reference não entra em seu payload e que
  `GetAsync(jti)` rematerializa `AccessToken.Id`/`Token` a partir do argumento; eventual `jti` dentro do compact JWT
  permanece apenas no payload protegido de `Full`.
- [x] Cobrir revogação por token bruto: reference encontra/remove seu artifact; JWT metadata/full permanece sem
  revogação stateful e a resposta RFC 7009 continua indistinguível.
- [x] Testar realms simultâneos com profiles de proteção e modos JWT diferentes sem cruzamento de policy/chaves.
- [x] Mapear expiration como dado, sem query filter.
- [x] Implementar remove em lote com contagem/efeito definido pela matriz.
- [x] Testar ids colidentes entre realms e casings diferentes.
- [x] Testar mutação do objeto materializado sem persistência implícita.
- [x] Testar `CancellationToken` pré-cancelado e propagado ao comando EF.

**Critérios de aceite:**

- Duplicidade de access-token id no mesmo realm falha e não sobrescreve; em realm diferente é aceita.
- Reference token segue AT-01..AT-04 em todos os realms. JWT `None` não cria artifact; `Metadata` não contém o
  compact JWT; `Full` faz round-trip completo. Alterar a opção não reinterpreta linhas existentes.
- Reference/Metadata/Full persistidos são localizados por `jti`; somente o bearer reference coincide com essa chave.
- SQLite registra migrations Configuration/Operational apenas nas histories nomeadas da DF23; banco legado é
  realocado antes de o EF avaliar pending migrations, sem perder/duplicar ids.
- Lookup devolve token/consent expirado até cleanup explícito.
- Reference-token removal não remove JWT nem outro subject/client/realm.
- Consent concorrente não cria duas linhas; a última operação concluída é a efetiva.
- Scopes com casing distinto sobrevivem ao round-trip.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~AccessToken|FullyQualifiedName~Consent|FullyQualifiedName~SqliteOperational"
dotnet test Tests.Architecture
```

### Resultado da Fase 2

**Concluída em 2026-07-25**, com os ajustes da revisão externa aplicados no mesmo dia. `dotnet build
RoyalIdentity.sln` e `dotnet test RoyalIdentity.sln` verdes: 985 aprovados, 9 ignorados (PostgreSQL opt-in),
0 falhas. `git diff --check` sem erros.

**Arquivos criados**

- `RoyalIdentity.Storage.EntityFramework.Sqlite/` — `SqliteOperationalModelBuilderExtensions`
  (`ApplyRoyalIdentityOperationalSqliteMappings`, com `BINARY` em todo identificador que sustenta chave, índice
  ou filtro de igualdade), `OperationalSqliteDbContext`, `OperationalSqliteDesignTimeDbContextFactory`,
  `SqliteMigrationsHistoryExtensions` (`UseConfigurationMigrationsHistory`/`UseOperationalMigrationsHistory`),
  `SqliteMigrationsHistoryBootstrap` e `OperationalMigrations/20260725030157_InitialOperational`.
- `RoyalIdentity.Storage.EntityFramework/Operational/Stores/` — `EntityFrameworkAccessTokenStore`,
  `EntityFrameworkUserConsentStore` e `EntityFrameworkOperationalStoreFactory`.
- `scripts/sql/operational/sqlite/0001_initial_operational.sql` e
  `scripts/sql/migration-history/sqlite/0001_relocate_legacy_configuration_history.sql`.
- Testes: `Tests.Storage/Operational/` — `SqliteOperationalAccessTokenTests` (17),
  `SqliteOperationalConsentTests` (10), `SqliteOperationalMigrationTests` (10) e
  `Support/` (`SqliteOperationalDatabase`, `SqliteOperationalStorageHarness`).

**Arquivos alterados**

- `AccessTokenPayloadSerializer.Serialize` recebe `persistCompactToken`, para o store expressar a diferença entre
  `Metadata` e `Full` sem que o serializer conheça a option do realm.
- `OperationalServiceCollectionExtensions` registra `IOperationalStoreFactory`.
- `ConfigurationMigrationRunner` roda o bootstrap da history legada **antes** de `MigrateAsync` e passa a
  configurar a history de Configuration pela topologia centralizada; `ConfigurationSqliteDesignTimeDbContextFactory`
  idem, e o script SQL de Configuration foi regenerado (migrations já publicadas não foram reescritas).
- `StorageContractHarness` ganhou o hook `ConfigureRealmOptions`; `ConfigurationCompositeStorage` aceita um
  `IOperationalStoreFactory` opcional e roteia a ele os stores que a fase entregou.
- `AccessTokenStoreContractTests`/`UserConsentStoreContractTests` ganharam a fixture `SqliteOperational`.
- `SqliteConfigurationDatabase`, `SqliteConfigurationStorageHarness`, `ConfigurationMigrationRunnerTests` e
  `SqliteConfigurationMigrationTests` passaram a usar a history nova.
- `ConfigurationStorageBoundaryTests` reflete o provider SQLite com três project references e reforça que nenhum
  provider referencia o core.

**Decisões tomadas na execução**

- **A fixture do harness liga `JwtAccessTokenPersistence=Full`.** Os contratos provider-neutral emitem access
  tokens JWT, e um realm em default de produto (`None`) não persistiria nenhum — não haveria linha para AT-02 ler.
  Os contratos descrevem o store, então a composição escolhe um realm que persiste; os três modos em si têm
  aceites dedicados que os setam explicitamente.
- **Reference token nunca persiste seu token string, mesmo em `Full`.** O store passa `persistCompactToken: false`
  para `Reference` em vez de confiar na comparação `Token == Id`: se o modelo divergir, o bearer continua fora do
  payload, e `Id`/`Token` sempre voltam do argumento de lookup (DF13).
- **Upsert de consent é update → insert → update.** Provider-neutral, sem SQL específico: a chave composta é a
  autoridade que impede duplicata, e o segundo update cobre o writer que perdeu a corrida do insert. O aceite
  correspondente prova a convergência para uma linha; **concorrência real de múltiplas conexões continua reservada
  às primitivas atômicas das Fases 4/5**, e a fixture SQLite compartilha uma conexão.
- **`EntityFrameworkOperationalStoreFactory` lança `NotSupportedException` nomeando a fase** para sessões, codes,
  refresh e AP. Nada em produção alcança esses membros porque o gateway EF completo só é composto na Fase 6
  (DF21), e o composite de teste só roteia ao EF o que a fase entregou.
- **O script SQL manual de bootstrap é explicitamente two-step.** SQLite não tem DDL condicional; a versão
  idempotente e à prova dos quatro estados é a automatizada (`SqliteMigrationsHistoryBootstrap`), usada pelo
  runner e coberta por teste.

**Ajustes aplicados após revisão externa (2026-07-25)**

- **`jti` de JWT não é mais aceito como bearer opaco (achado de segurança, pré-existente).** O bearer sem ponto
  vai para a validação de reference token, o store é indexado por `jti`, e `IncludeJwtId` é `true` por default —
  então quem possuísse um JWT podia ler seu `jti` do payload (não cifrado) e apresentá-lo como um segundo bearer,
  pulando a validação de assinatura. `DefaultTokenValidator.ValidateReferenceAccessTokenAsync` passou a exigir
  `AccessTokenType.Reference`, com resposta idêntica à de handle inexistente (sem oracle). O defeito **não foi
  introduzido pela Fase 2** — existe desde o fake in-memory, que persiste todo access token —, mas a Fase 2 o
  tornou explícito ao persistir JWTs em `Metadata`/`Full`, e DF13 já dizia que persistir um JWT não é revogação.
  Regressão em `Tests.Integration/Endpoints/ReferenceTokenBearerTests`: o `jti` é rejeitado exatamente como um
  handle inexistente, o próprio JWT continua aceito e um reference token legítimo continua aceito. **Verificado
  por mutação:** com a checagem desabilitada o teste falha.
- **`access_token_type` passou a falhar fechado.** O fallback `null → Jwt` na materialização foi trocado por
  validação: tipo ausente ou desconhecido é dado corrompido e levanta `OperationalPayloadException`. É o tipo que
  decide se o token pode ser apresentado como bearer opaco, então adivinhá-lo era exatamente o risco acima.
- **Aceite de concorrência de consent cumprido de verdade.** O relatório anterior alegava que isso pertencia às
  Fases 4/5 — **estava errado**: o aceite é da Fase 2, e a seção de concorrência do plano exige scopes,
  `DbContext`s e conexões independentes com barreira de início. `SqliteOperationalFileDatabase` provê banco em
  arquivo, pooling desligado, WAL e barreira assíncrona (um `Barrier` bloqueante inania as próprias
  continuations); `SqliteOperationalConsentConcurrencyTests` cobre 2 e 8 writers na mesma chave, writers em chaves
  distintas e o caso determinístico de "quem termina depois vence". **Verificado por mutação:** removendo a
  recuperação do upsert, 3 dos 4 testes falham.
- **Estado residual do consent store.** O detach virou `finally`, então cancelamento ou qualquer falha não deixa a
  linha em `Added` para ser gravada pelo próximo `SaveChanges` do mesmo scope. Coberto por teste que reutiliza o
  scope após o cancelamento.
- **Pontos do relatório mantidos:** `JwtAccessTokenPersistence=Full` na fixture (exercita o round-trip mais
  completo; os três modos têm aceites próprios) e a factory parcial com `NotSupportedException` (o gateway
  produtivo só é composto na Fase 6 — a condição é que nenhum desses caminhos sobreviva até lá).

**Comandos executados**

```powershell
dotnet build RoyalIdentity.sln
dotnet ef migrations add InitialOperational --project RoyalIdentity.Storage.EntityFramework.Sqlite --context OperationalSqliteDbContext --output-dir OperationalMigrations
dotnet ef migrations script --project RoyalIdentity.Storage.EntityFramework.Sqlite --context OperationalSqliteDbContext
dotnet test Tests.Architecture
dotnet test Tests.Storage --filter "FullyQualifiedName~AccessToken|FullyQualifiedName~Consent|FullyQualifiedName~SqliteOperational"
dotnet test RoyalIdentity.sln
git diff --check
```

---

## Fase 3 - sessões SSO sobre SQLite

**Depende de:** Fase 2.

**Escopo:** implementar SS-01..SS-06 e provar o comportamento de sessão definido pelas ADR-014/017.

**O que/como:**

- Implementar `IUserSessionStore` realm-bound sobre session + session-clients.
- Create é create-only e materializa todo o grafo.
- Record-client usa a PK composta para deduplicar e operação condicional para preservar `FirstSeenAt`/renovar
  `LastSeenAt`.
- End/touch/revogação por subject são set-based/condicionais, idempotentes e contam somente transições efetivas.
- Usar `TimeProvider` somente onde o contrato manda o store definir tempo; touch recebe timestamps do caller.

**Tarefas:**

- [x] Implementar create/find/record/end/touch/end-by-subject.
- [x] Preservar `SecurityStamp`, auth method/idp e expiração.
- [x] Garantir no-op para record/touch ausentes.
- [x] Evitar lost update entre record-client, touch e end.
- [x] Testar clients case-sensitive, timestamps e rematerialização.
- [x] Executar contratos existentes contra SQLite.

**Critérios de aceite:**

- Dois realms aceitam o mesmo sid sem interferência.
- Duplicidade de sid no mesmo realm falha visivelmente.
- Record concorrente do mesmo client deixa uma linha, mantém o primeiro `FirstSeenAt` e publica o maior/último
  `LastSeenAt` definido pela operação.
- End repetido não muda contagem/estado; revogação por subject respeita `exceptSessionId`.
- Lookup devolve sessão inativa/expirada enquanto não purgada.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~UserSession|FullyQualifiedName~SqliteOperational"
dotnet test Tests.Integration --filter "FullyQualifiedName~DefaultUserSessionService"
```

### Resultado da Fase 3

**Concluída em 2026-07-25**, com os ajustes da revisão externa aplicados no mesmo dia. `dotnet test
RoyalIdentity.sln` verde: 1022 aprovados, 9 ignorados (PostgreSQL opt-in), 0 falhas. `git diff --check` sem
erros.

**Arquivos criados**

- `RoyalIdentity.Storage.EntityFramework/Operational/Stores/EntityFrameworkUserSessionStore.cs` — SS-01..SS-06
  sobre `user_sessions` + `user_session_clients`.
- Testes: `Tests.Storage/Operational/SqliteOperationalUserSessionTests` (13 aceites) e
  `SqliteOperationalUserSessionConcurrencyTests` (4 aceites de concorrência real).

**Arquivos alterados**

- `EntityFrameworkOperationalStoreFactory` passou a receber `TimeProvider` e a devolver o session store;
  `ConfigurationCompositeStorage.GetUserSessionStore` roteia ao EF quando a factory está presente.
- `UserSessionStoreContractTests` ganhou a fixture `SqliteOperational` — os 15 contratos passaram sem alteração.
- `SqliteOperationalFileDatabase` ganhou `CountAsync`/`CountSessionClientsAsync`, reusado pelos aceites de
  concorrência.

**Decisões tomadas na execução**

- **A sessão não tem payload protegido.** Todo campo do modelo é coluna consultável e os clients são tabela
  filha, então `record-client`, `touch` e `end` são operações condicionais set-based em vez de
  read-modify-write sobre um blob — que é exatamente o que impede uma perder a alteração da outra. Há aceite
  explícito de que gravar um client não reescreve a linha da sessão, e vice-versa.
- **`ended_at_utc` é gravado condicionalmente a `IsActive`**, então repetir `End`/revogação preserva o primeiro
  instante terminal (DF15/DF17). Uma sessão **criada já inativa** também recebe o instante terminal na criação:
  sem isso, uma sessão inativa sem expiração não seria alcançada por nenhum predicado de cleanup.
- **`RecordClientAsync` checa a existência da sessão antes do upsert**, para o no-op de SS-03 não depender de
  engolir uma violação de FK. Se a sessão sumir no meio da corrida, o insert falha, o refresh subsequente não
  encontra nada e o erro sobe — estado torto é reportado, não silenciado.
- **Concorrência real coberta desde já**, com o mesmo shape da Fase 2 (banco em arquivo, pooling desligado, WAL,
  scopes/contexts/conexões independentes e barreira assíncrona): 8 records do mesmo client convergem em uma
  linha preservando `FirstSeenAt`, 6 revogações concorrentes somam exatamente uma transição efetiva, e records
  de clients distintos não se perdem. **Verificado por mutação:** removendo a recuperação do upsert, 2 dos 4
  testes falham.

**Ajustes aplicados após revisão externa (2026-07-25)**

- **`LastSeenAt` não regride mais.** `TouchClientAsync` atribuía o instante do writer incondicionalmente, então
  um writer que capturou `10:01` e commitou depois de outro que gravou `10:02` reescrevia para trás — violando o
  aceite "publica o maior/último `LastSeenAt`". A atualização passou a tomar o maior entre o valor armazenado e o
  do writer. `FirstSeenAt` permanece intocado por decisão: o aceite pede que a primeira entrada persistida seja
  preservada, e é isso que a operação garante. **Testes determinísticos** com o relógio rebobinado provam a
  não-regressão sem depender de uma corrida produzir a inversão.
- **Remoção concorrente da sessão virou no-op.** O achado está certo: a FK já garantiu que não há órfão, então a
  remoção física durante o `RecordClientAsync` lineariza como sessão ausente — exatamente o no-op de SS-03 — e não
  como falha operacional. Depois do refresh retornar zero, o store reconfere a sessão: ausente conclui em no-op,
  presente relança. **Teste determinístico:** o store lê o relógio entre o pre-check e o insert, então um
  `TimeProvider` que apaga a sessão nessa leitura reproduz a janela exata, sem depender de sorte; há ainda um
  teste de corrida real `record × delete` provando ausência de exceção e de linha órfã.
- **Comando focado corrigido no plano:** `DefaultUserSessionServiceTests` vive em `Tests.Integration`, não em
  `Tests.Identity` — o comando anterior encontrava zero testes (sem lacuna funcional, a suíte completa os cobria).
- **Pontos do relatório mantidos:** sessão sem payload protegido e `ended_at_utc` na criação de sessão inativa,
  ambos aprovados na revisão.
- **Verificado por mutação:** revertendo as duas correções, os 4 testes novos falham.

**Comandos executados**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~UserSession|FullyQualifiedName~SqliteOperational"
dotnet test Tests.Integration --filter "FullyQualifiedName~DefaultUserSessionService"
dotnet test RoyalIdentity.sln
git diff --check
```

---

## Fase 4 - authorization codes e consumo atômico

**Depende de:** Fases 1–3.

**Escopo:** implementar AC-01..AC-03, MP-2 e migrar o fluxo de token.

**O que/como:**

- Implementar create-only/get/remove administrativo no adapter SQLite.
- Implementar a operação atômica da DF11 sem filtrar expiração no lookup comum.
- Refatorar `LoadCode`/pipeline para que somente o vencedor prossiga.
- Preservar a ordem de segurança observável: vínculo client/redirect, consumo, expiração, PKCE e active-user
  conforme a DF11 e o comportamento atual documentado.
- Não introduzir status OAuth diferente para ausente/já usado/vínculo inválido.

**Tarefas:**

- [x] Adicionar aceite “dois consumers simultâneos, exatamente um sucesso”.
- [x] Usar scopes/conexões independentes no teste.
- [x] Confirmar que code expirado ainda é materializado e é consumido/rejeitado pelo pipeline conforme alvo.
- [x] Manter remove administrativo ausente idempotente.
- [x] Atualizar testes do pipeline/token endpoint.
- [x] Fixar a mudança observável de redirect mismatch: `invalid_grant` permanece, mas a descrição deixa de ser
  `Invalid redirect_uri` e passa à resposta genérica de code inválido conforme DF11.
- [x] Documentar que revogação de tokens já emitidos por reuse de code não é adicionada neste plano.

**Critérios de aceite:**

- Nunca duas requests concorrentes chegam ao handler com o mesmo code.
- Invalid client/redirect não fornece oracle mais detalhado e, por não satisfazer o predicado condicional, não
  remove o code.
- Client/redirect mismatch compartilham a descrição genérica sem alterar o código OAuth `invalid_grant`.
- Falha de PKCE ocorre depois do consume e não permite segunda tentativa com o mesmo code.
- Fake continua transitório pelo fallback da DF39; aceite de atomicidade roda somente em EF.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~AuthorizationCode|FullyQualifiedName~Atomic"
dotnet test Tests.Identity --filter "FullyQualifiedName~AuthorizationCode|FullyQualifiedName~Pkce"
dotnet test Tests.Integration --filter "FullyQualifiedName~AuthorizationCode"
```

### Resultado da Fase 4

**Concluída em 2026-07-25.** `dotnet test RoyalIdentity.sln` verde: 1054 aprovados, 9 ignorados (PostgreSQL
opt-in), 0 falhas. `git diff --check` sem erros.

**Arquivos criados**

- `RoyalIdentity.Storage.EntityFramework/Operational/Stores/EntityFrameworkAuthorizationCodeStore.cs` —
  AC-01..AC-03 mais a primitiva single-use de MP-2.
- Testes: `Tests.Storage/Operational/SqliteOperationalAuthorizationCodeTests` (13 aceites),
  `SqliteOperationalAuthorizationCodeConcurrencyTests` (5, incluindo o aceite central de atomicidade) e
  `Tests.Integration/Endpoints/CodeSingleUseTests` (5 regressões de fluxo).

**Arquivos alterados**

- `LoadCode` passou a consumir pelo seam `IAuthorizationCodeConsumer` em vez de `get` + checagens manuais +
  `remove`. É a **primeira mudança de comportamento de fluxo** do plano.
- `EntityFrameworkOperationalStoreFactory` devolve o code store; `ConfigurationCompositeStorage` o roteia.
- `AuthorizationCodeStoreContractTests` ganhou a fixture `SqliteOperational` — os 8 contratos passaram sem
  alteração.

**Mudança observável (DF11)**

Redirect mismatch deixou de responder `Invalid redirect_uri`. Ausente, já consumido, client divergente e
redirect divergente agora compartilham **a mesma** descrição genérica de code inválido; o código OAuth continua
`invalid_grant`. `CodeSingleUseTests.EveryRefusedExchange_AnswersIdentically` compara as quatro respostas
(`error` + `error_description`) e exige que sejam semanticamente idênticas, para que nenhuma vire oracle.

**Decisões tomadas na execução**

- **O consumo atômico é read → conditional delete, e o delete é o ponto de decisão.** O predicado inclui o
  binding e a contagem de linhas afetadas decide o vencedor: quem lê a mesma linha mas perde o delete vê zero e
  devolve `null`, indistinguível de ausente. Não precisa de transação explícita nem de SQL de provider.
- **Expiração fica fora do predicado de propósito.** Um code expirado é consumido e só então rejeitado pelo
  pipeline — se a expiração fizesse parte da condição, uma tentativa perdedora poderia retentar o mesmo code.
- **Binding divergente não consome.** Como o predicado não casa, nenhuma linha é removida — então uma requisição
  inválida não consegue negar a legítima. Coberto em aceite de storage e em corrida real (6 impostores
  simultâneos não impedem o consumidor legítimo).
- **PKCE continua depois do consumo.** Vencer o code e falhar PKCE não o torna reutilizável; há regressão de
  fluxo com verifier errado seguido do verifier correto, e a segunda tentativa falha.
- **Revogação de tokens já emitidos por reuse de code não foi adicionada** — permanece fora deste plano, junto
  do hardening de replay da DF37.
- **Verificado por mutação:** ignorando a contagem de linhas do delete, os aceites de atomicidade falham;
  distinguindo o redirect mismatch no fallback, 2 regressões de fluxo falham.

**Ajustes aplicados após revisão externa (2026-07-25)**

- **A afirmação de cobertura no relatório era maior que o teste.** O teste comparava três respostas (inexistente,
  client divergente, redirect divergente) e não a de code já consumido, e comparava campos desserializados — não
  bytes. Corrigido nos dois sentidos: `EveryRefusedExchange_AnswersIdentically` passou a incluir o caso já
  consumido, e o texto acima agora diz "semanticamente idênticas", que é o requisito real.
- **Regressão de fluxo para code expirado.** O aceite de storage já provava consumo do expirado, mas faltava a
  sequência ponta a ponta. `AnExpiredCode_IsRejectedAsExpired_AndConsumedAllTheSame` prova que a primeira troca
  responde "expired" e a segunda cai na recusa genérica — ou seja, o code foi consumido mesmo expirado.
- **A prova por mutação da atomicidade virou determinística.** A barreira liberava antes de
  `ConsumeAuthorizationCodeAsync`, então nada garantia que todos os consumers ficassem entre o `SELECT` interno e
  o `DELETE`; uma mutação podia escapar por agendamento. `ReadBeforeWriteInterceptor` (um `DbCommandInterceptor`)
  segura cada participante após seu `SELECT` até que todos tenham lido, e só então libera os `DELETE`s. O novo
  aceite ainda afirma `Interleaved`, então a janela é pré-condição do teste e não resultado de timing.
  **Confirmado:** com a mutação, o teste falha nas 3 execuções seguidas.

**Comandos executados**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~AuthorizationCode|FullyQualifiedName~Atomic"
dotnet test Tests.Integration --filter "FullyQualifiedName~Code"
dotnet test RoyalIdentity.sln
git diff --check
```

---

## Fase 5 - refresh tokens e transições condicionais

**Depende de:** Fase 4.

**Escopo:** implementar RT-01..RT-05, MP-3 e reorganizar o handler para não emitir antes da transição exigida.

**O que/como:**

- Implementar create-only/get/remove/remove-by-subject.
- Persistir versão/estado conforme DF12 e realizar CAS por predicado + linhas afetadas.
- Remover `Client.UpdateAccessTokenClaimsOnRefresh` somente agora, junto da ativação efetiva de
  `RealmOptions.RefreshTokens.ClaimsMode`; remover também `ClientEntity`, mapping/materializers e gerar a migration
  Configuration SQLite que descarta `update_access_token_claims_on_refresh`, sem editar migrations anteriores.
- Mover a primeira transição para antes de efeitos de emissão que não podem ser revertidos.
- Separar claramente:
  1. lookup/materialização;
  2. validações de expiração/client/offline access/active user;
  3. transição atômica;
  4. política de tolerância;
  5. emissão e eventual atualização do token reutilizável.
- Remover `RefreshToken.AccessTokenId`, o parâmetro de construtor e o claim `jti` sobrecarregado; eliminar a busca
  obrigatória do access token anterior e a reescrita de `jti` no refresh reutilizável, conforme DF41.
- Manter `RefreshTokenRequest.AccessToken` apenas como fonte em memória do grant/snapshot recém-emitido; a assinatura
  de `ITokenFactory.CreateRefreshTokenAsync` não muda.
- No modo `Current`, persistir no refresh somente o grant mínimo, reconstruir o principal protocolar a partir da
  sessão e reexecutar resolução de resources + emissão + `IUserClaimsProvider`. Scopes/resources atuais só podem
  restringir o conjunto originalmente autorizado, nunca ampliá-lo.
- No modo `Snapshot`, persistir dentro do refresh payload o snapshot necessário para reproduzir as claims; ainda
  validar account/session/client/expiração/consumo e nunca depender de uma linha do access token.
- Capturar `claims_mode` no refresh token emitido para que mudança posterior da opção do realm não altere a semântica
  de tokens existentes.
- Calcular `at_hash`, quando aplicável, sobre o novo access token devolvido na resposta; o compact JWT anterior não
  é requisito de refresh. Esta é a correção de conformidade da DF42, não apenas um detalhe de persistência.
- Preservar exatamente os resultados atuais de tolerância sem chamar repetição tolerada de detecção de replay;
  famílias/revogação automática por replay/sender constraints permanecem diferidas conforme DF37.

**Tarefas:**

- [x] Criar o tipo de resultado mínimo da DF12.
- [x] Implementar concorrência com contexts independentes.
- [x] Tratar conflito sem retry cego de efeitos externos/emissão.
- [x] Modelar o grant mínimo de `Current` com subject/session/client/scopes/resource URIs e contexto protocolar
  necessário (`auth_time`, `idp`, `amr` e futuro `acr` quando suportado), sem snapshot de profile claims.
- [x] Modelar o snapshot próprio de `Snapshot` com `ClaimPayload` mínimo e sem compact JWT anterior.
- [x] Remover `AccessTokenId` de modelo/construtor/payload/handler, manter `RefreshTokenRequest.AccessToken` somente
  como input em memória e cobrir refresh após cleanup do access token anterior.
- [x] Atualizar `DefaultTokenFactory`, `StorageContractTests.NewRefreshToken` e
  `SessionLifecycleTests.SeedRefreshToken` para o construtor sem `accessTokenId`; provar que nenhum fixture reinsere
  o claim `jti` removido.
- [x] Remover a flag do client em core/entity/mapping/materializers; atualizar
  `ConfigurationModelClientCoverageTests`, `ConfigurationMaterializationClientTests` e `ConfigurationTestData`.
- [x] Gerar/testar a migration Configuration SQLite de drop da coluna legada na mesma mudança que ativa
  `Current`/`Snapshot`.
- [x] Testar que `Current` reflete remoção/adição de claims do UserAccounts somente dentro do grant original.
- [x] Testar que `Snapshot` preserva as claims emitidas e que ambos os modos continuam aplicando active-user/session.
- [x] Testar que dois realms com modos diferentes não compartilham policy.
- [x] Testar explicitamente a mudança do caminho default legado (`false`, sem resource subset, equivalente a
  snapshot) para `ClaimsMode.Current`, além do caminho legado com subset que já reemitia.
- [x] Testar que identity token de refresh contém `at_hash` calculado sobre o novo access token da resposta usando
  tokens antigo/novo distintos, inclusive com `JwtAccessTokenPersistence=None`.
- [x] Testar expired/consumed lookup, tolerância zero, finita e infinita.
- [x] Testar revogação ordinal por subject e isolamento por realm.
- [x] Garantir que handle/payload nunca entra em log.

**Critérios de aceite:**

- Exatamente uma request observa a transição inicial `null → ConsumedTime`.
- Conflito não é convertido em sucesso silencioso.
- Eventual repetição tolerada usa estado consumido rematerializado depois do
  conflito; a perda do CAS isoladamente nunca autoriza emissão.
- A transição condicional nunca usa como estado esperado a mesma instância já mutada que será gravada; uma instância
  rematerializada ou versão persistida anterior deve falsificar o CAS trivial.
- Tolerância finita usa o timestamp persistido e `TimeProvider`, sem relógio do banco/processo divergente.
- Remove-by-subject retorna contagem efetiva e repetição retorna zero.
- Falha anterior à transição não cria token novo; falha posterior tem comportamento documentado e testado.
- `Current` é o default para payload RealmOptions v1 sem a nova propriedade; nenhuma opção do client interfere.
- Nenhum refresh válido exige que a linha do access token anterior ainda exista.
- `RefreshToken` não expõe/persiste `AccessTokenId` ou claim `jti` do access token anterior; a factory ainda recebe o
  access token recém-emitido somente como fonte em memória.
- `at_hash` do identity token corresponde ao access token novo devolvido na mesma resposta e difere do hash do
  token anterior.
- A flag do client, seus testes de mapping/materialização e sua coluna SQLite desaparecem apenas junto da nova
  semântica, sem intervalo intermediário em que o handler fique sem política efetiva.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~RefreshToken|FullyQualifiedName~Atomic|FullyQualifiedName~ConfigurationModelClient|FullyQualifiedName~ConfigurationMaterializationClient"
dotnet test Tests.Identity --filter "FullyQualifiedName~RefreshToken|FullyQualifiedName~AtHash"
dotnet test Tests.Integration --filter "FullyQualifiedName~SessionRevocation"
```

### Resultado da Fase 5

**Concluída em 2026-07-25**, com os ajustes da revisão externa aplicados no mesmo dia. `dotnet test
RoyalIdentity.sln` verde: 1113 aprovados, 9 ignorados (PostgreSQL opt-in), 0 falhas. `git diff --check` sem
erros.

**Arquivos criados**

- `RoyalIdentity.Storage.EntityFramework/Operational/Stores/EntityFrameworkRefreshTokenStore.cs` — RT-01..RT-05
  mais as transições condicionais de MP-3.
- `RoyalIdentity.Storage.EntityFramework.Sqlite/Migrations/20260725171141_DropUpdateAccessTokenClaimsOnRefresh` e
  `scripts/sql/configuration/sqlite/0002_drop_update_access_token_claims_on_refresh.sql`.
- Testes: `Tests.Storage/Operational/SqliteOperationalRefreshTokenTests` (17),
  `SqliteOperationalRefreshTokenConcurrencyTests` (5) e
  `Tests.Integration/Endpoints/RefreshTokenClaimsModeTests` (18).

**Arquivos alterados**

- `RefreshToken` perdeu `AccessTokenId`, o parâmetro de construtor e o claim `jti`; ganhou `ClaimsMode` e,
  para `Snapshot`, snapshots distintos de access token (`Claims`) e identity token (`IdentityTokenClaims`).
- `DefaultTokenFactory` captura o `ClaimsMode` do realm na emissão e escolhe o que persistir: `Current` guarda só
  o contexto protocolar (`sub`/`sid`/`auth_time`/`idp`/`amr`); `Snapshot` guarda separadamente as claims emitidas
  para cada tipo de token, removendo de cada coleção apenas as claims que a nova instância reemite.
- O payload protegido de refresh passou à versão 2 para carregar `IdentityTokenClaims`. A versão 1 defeituosa
  falha fechada em vez de ser reinterpretada misturando claims de access token e identity token.
- `RefreshTokenHandler` reescrito na ordem da fase: transição atômica **antes** de qualquer emissão, tolerância
  depois e sobre o estado rematerializado, emissão por último.
- `Client.UpdateAccessTokenClaimsOnRefresh` removido de core, `ClientEntity`, mapping, materializers e fixtures.
- `EntityFrameworkOperationalStoreFactory` devolve o refresh store; `ConfigurationCompositeStorage` o roteia.
- `RefreshTokenStoreContractTests` ganhou a fixture `SqliteOperational`; `StorageContractTests.NewRefreshToken`,
  `OperationalTestData` e `SessionLifecycleTests.SeedRefreshToken` usam o construtor sem `accessTokenId`.

**Mudanças observáveis**

- **A política de claims do refresh passou a ser exclusivamente do realm.** O default é `Current`, o que muda o
  caminho comum: antes, `UpdateAccessTokenClaimsOnRefresh=false` sem resource subset renovava as claims antigas
  (comportamento snapshot-like). Mudança intencional da DF32/DF33.
- **`at_hash`** do identity token emitido no refresh agora cobre o access token devolvido na mesma resposta
  (DF42). Antes cobria o token anterior — o id_token não correspondia ao que o cliente recebia.

**Decisões tomadas na execução**

- **A transição vem antes da emissão.** O handler não cria nada até vencer o token; conflito e
  `AlreadyConsumed` só então passam pela tolerância, e sempre sobre `transition.Current` — o estado
  rematerializado —, nunca sobre a instância que a request já mutou.
- **O handler segue com a instância rematerializada.** `TryWinTheTokenAsync` devolve o token efetivo
  (`transition.Current` tanto no sucesso quanto na repetição tolerada) e o resto do handler trabalha só com ele.
- **`TryUpdateAsync` sincroniza a versão na instância do caller** após sucesso, para uma transição seguinte usar
  a versão nova em vez da obsoleta. Isso apareceu num teste que precisou capturar a versão antiga antes de
  provocar o conflito — o comportamento é intencional e está documentado.
- **`Snapshot` reemite a partir das claims do próprio refresh**, sem consultar o claims provider, mas os scopes
  vêm do que a renovação resolveu: uma request que estreita o grant estreita o token.
- **`UpdateAsync` (contrato CRUD legado) delega ao `TryUpdateAsync`** com a versão da própria instância, para o
  adapter não ter dois caminhos de escrita — o CAS continua sendo o único jeito de gravar.
- **Verificado por mutação:** apontando `at_hash` para outro token, a regressão de fluxo falha.

**Ajustes aplicados após revisão externa (2026-07-25)**

- **O handler descartava `transition.Current` e atualizava com versão obsoleta (defeito funcional).** Achado
  correto e sério: `TryWinTheTokenAsync` devolvia `bool`, então o handler seguia com a instância carregada antes
  do consumo. Sob EF, o `TryUpdateAsync` do refresh reutilizável comparava a versão pré-transição contra a que o
  consumo já incrementara — **conflito garantido**, e o token reutilizável nunca era atualizado. O método passou
  a devolver o token efetivo e o handler reatribui `refreshToken` a ele. O defeito estava mascarado porque os
  testes de fluxo usam o fake, cujo fallback muta a própria instância do caller — exatamente como a revisão
  apontou. Cobertura nova em `Tests.Storage`: consumo→update pelo seam `DefaultRefreshTokenConsumer` + store EF
  (sucesso com a instância rematerializada, conflito com a obsoleta).
- **`Snapshot` não valia para o identity token.** Também correto: `CreateIdentityTokenAsync` reconsultava o
  profile service, então o id_token trazia claims atuais ao lado de um access token montado do snapshot — duas
  visões do usuário na mesma resposta. `IdentityTokenRequest` ganhou `SnapshotClaims`, um seam explícito que o
  handler preenche só em `Snapshot`. **Verificado por mutação:** sem o seam, o novo teste falha.
- **A primeira correção de `SnapshotClaims` ainda misturava a proveniência dos tokens.** O refresh guardava só
  as claims do access token e o handler tentava derivar dali o identity token por exclusão. Isso deixava
  `client_id` e claims arbitrárias do client entrarem no `id_token`. `RefreshToken` agora mantém coleções
  distintas, a emissão inicial cria o identity token antes de persistir o refresh e a rotação captura novamente
  os dois conjuntos. O aceite prova simultaneamente que uma claim de perfil removida depois do grant permanece
  nos dois tokens e que `client_id`/claim exclusiva do access token não entram no identity token.
- **A tolerância rodava duas vezes.** `LoadRefreshToken` ainda a avaliava sobre o estado pré-transição — o que
  também contradizia o texto deste relatório. A validação antecipada saiu do decorator, que agora cobre lookup,
  expiração, client e offline access; consumo e tolerância ficam juntos no handler, sobre o estado
  rematerializado. Decidir sobre um estado que pode envelhecer até o consumo aceitaria ou rejeitaria pelo motivo
  errado.
- **`UpdateAsync` engolia conflito.** Passou a lançar quando a transição não é `Succeeded`: reportar sucesso num
  conflito reintroduziria pela API legada exatamente o lost update que MP-3 existe para impedir.
- **Testes que prometiam mais do que cobriam.** `CurrentMode_ReflectsClaimsAddedAfterTheGrant` não alterava
  claim alguma. Foi reescrito para de fato adicionar uma claim entre o grant e a renovação, e ganhou par:
  `Snapshot` ignora a claim nova **em ambos os tokens**. `Current` também cobre remoção de claim em ambos, e
  `Snapshot` cobre preservação da claim original depois de sua remoção no UserAccounts. Somaram-se ainda
  `TryUpdate` que comprova persistência efetiva e `UpdateAsync` que comprova o lançamento em conflito.
- **As lacunas de cobertura foram fechadas:** há aceites ponta a ponta para tolerância zero, finita dentro e fora
  da janela (usando o timestamp persistido) e infinita; active-user e sessão encerrada nos dois modes; dois realms
  simultâneos com policies diferentes; e `invalid_target` posterior ao CAS mantendo o refresh consumido. O
  comando focado de `Tests.Identity` previsto na fase continua sem casos porque essa cobertura de fluxo vive em
  `Tests.Integration`.

---

## Fase 6 - authorize parameters, cleanup e purge de realm

**Depende de:** Fases 2–5.

**Escopo:** implementar MP-5/MP-6/parte Operational de MP-7 e completar o gateway SQLite.

**O que/como:**

- Tornar AP realm-bound em todos os callers.
- Preservar `StoreAuthorizationParameters` como gate: `true` usa o store realm-bound; `false` conserva o fluxo por
  query string sem write/read/delete no store.
- Gerar handle com ao menos 128 bits, persistir somente seu digest conforme DF38 e regenerar colisões por generator
  injetável/testável.
- Gravar `CreatedAt`/`ExpiresAt` absolutos; read repetível dentro da validade e `null` depois.
- Implementar cleanup em batches por tipo conforme a elegibilidade e os modos de execução da DF17.
- Limpar access-token artifacts por seu próprio expiry/estado, sem `NOT EXISTS` contra refresh tokens; o
  refresh já conserva grant/snapshot suficiente segundo DF32.
- Implementar purge por realm pela porta de manutenção da DF18.
- Compor um `IStorage` EF completo sobre Configuration EF + Operational SQLite + resources bridge.

**Tarefas:**

- [ ] Migrar login, consent, resolver e callback para o accessor realm-bound.
- [ ] Alterar `DefaultAuthorizationContextResolver` para obter `httpContext.GetCurrentRealm()` (não apenas
  `GetRealmPath()`), respeitar `StoreAuthorizationParameters` e passar o realm ao accessor.
- [ ] Cobrir `StoreAuthorizationParameters=true/false`: somente `true` cria/responde handle e aplica TTL;
  `false` mantém query string e não invoca o store.
- [ ] Comprovar que mudar o realm de `true` para `false` não impede o cleanup periódico de AP já expirado.
- [ ] Cobrir clone/round-trip de `NameValueCollection`, inclusive chaves repetidas se suportadas pelo shape atual.
- [ ] Injetar handle generator em teste para forçar colisão.
- [ ] Criar opções de cleanup validadas (modo `Hosted`/`External`, intervalo e batch); a elegibilidade não possui
  grace configurável.
- [ ] Comprovar que `Hosted` registra um único worker, `External` não registra worker e ambos reutilizam a mesma
  manutenção.
- [ ] Implementar e testar índices alinhados aos predicados reais de cleanup global/realm-bound da DF17.
- [ ] Implementar lazy AP cleanup sem transformar delete falho em retorno de payload expirado.
- [ ] Semear todas as tabelas em dois realms, purgar um e provar isolamento.
- [ ] Criar `EntityFrameworkStorage`/provider/session e testes de scope/disposal com dois DbContexts.

**Critérios de aceite:**

- Handle de AP em realm A não resolve em realm B.
- Com `StoreAuthorizationParameters=false`, login/consent/resolver/callback não acessam AP e o fluxo por query
  string continua funcional.
- Alterar o TTL do realm não muda a expiração já gravada.
- Colisão nunca sobrescreve nem escapa como falha aleatória.
- Cleanup nunca remove refresh token ainda observável pela tolerância escolhida.
- Code consumido desaparece na operação atômica; code abandonado, AT e AP expiram sem grace histórica.
- Consent sem expiry não é removido por cleanup; sessão terminada/expirada torna-se elegível e preserva o primeiro
  `ended_at_utc`.
- Cleanup do access token anterior não invalida refresh token ainda observável.
- Purge é idempotente, abrange todas as tabelas Operational e não toca Configuration/UserAccounts.
- `IStorageSession` descarta ambos os contexts, sem commit/transação global.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~AuthorizeParameters|FullyQualifiedName~Cleanup|FullyQualifiedName~PurgeRealm|FullyQualifiedName~StorageSession"
dotnet test Tests.Identity --filter "FullyQualifiedName~AuthorizationContext"
dotnet test Tests.Integration --filter "FullyQualifiedName~Login|FullyQualifiedName~Consent|FullyQualifiedName~AuthorizeCallback"
```

### Resultado da Fase 6

*a preencher*

---

## Fase 7 - PostgreSQL, migrations, runner e gateway EF completo

**Depende de:** Fase 6.

**Escopo:** implementar refinamentos PostgreSQL, migrations/SQL, estender o runner e validar o gateway completo no
provider produtivo.

**O que/como:**

- Criar extensão pública `ApplyRoyalIdentityOperationalPostgreSqlMappings`, context e design-time factory.
- Usar schema `operation` e tipos/refinamentos equivalentes ao SQLite.
- Configurar explicitamente `configuration.__EFMigrationsHistory` e `operation.__EFMigrationsHistory`; o schema
  das entidades não é considerado configuração implícita suficiente.
- Implementar o bootstrap PostgreSQL que move a history Configuration legada do schema default para
  `configuration` antes de qualquer `MigrateAsync`.
- Gerar migration inicial Operational PostgreSQL e scripts SQL revisáveis/idempotentes conforme o padrão P2.
- Gerar nova migration Configuration PostgreSQL que remove `update_access_token_claims_on_refresh`, equivalente à
  SQLite e sem editar migrations anteriores.
- Estender `RoyalIdentity.Migrations` para selecionar famílias e aceitar uma ou duas conexões.
- Quando ambas apontam ao mesmo banco, aplicar Configuration e Operational sequencialmente; quando distintas,
  reportar falha por família sem sugerir atomicidade conjunta.
- Criar fixture PostgreSQL opt-in e script Podman com PostgreSQL 17 e porta host dinâmica diferente de 5432.
- Registrar gateway EF completo por provider como opt-in, nunca no host padrão.

**Tarefas:**

- [ ] Testar context separado e context combinado PostgreSQL.
- [ ] Testar histories distintas ao executar Configuration + Operational no mesmo banco SQLite e PostgreSQL.
- [ ] Atualizar runner, design-time factories e fixtures PostgreSQL para consumir a mesma configuração centralizada
  de history.
- [ ] Testar bootstrap PostgreSQL: banco vazio; somente history legada; somente history nova; ambas presentes/
  ambíguas; repetição idempotente e preservação integral dos migration ids.
- [ ] Implementar estratégia provider-specific de MP-2/MP-3 com a mesma semântica SQLite.
- [ ] Testar migration from-empty, pending model changes e SQL versionado.
- [ ] Testar upgrade Configuration do schema do Plano 2, preservando clients e removendo somente a coluna obsoleta.
- [ ] Regenerar/revalidar os scripts idempotentes Configuration contra a nova history e versionar os scripts de
  bootstrap sem alterar migrations de domínio já geradas.
- [ ] Testar runner: Configuration-only, Operational-only, ambas/mesmo banco e ambas/bancos distintos.
- [ ] Confirmar que o gateway com mesmo banco mantém dois DbContexts/conexões sem compartilhar transação.
- [ ] Garantir que Operational rejeita seed.
- [ ] Criar/estender script de PostgreSQL efêmero reutilizando o precedente local.
- [ ] Validar logs/erros redigidos.

**Critérios de aceite:**

- SQLite/PostgreSQL concordam em casing, duplicidade, ausência, TTL, contagens e concorrência.
- SQLite/PostgreSQL concordam nos três modos JWT, nos dois modos de claims do refresh e na seleção de protector por
  realm.
- Migrations não dependem da ordem de conexão entre famílias além da aplicação explícita do runner.
- PostgreSQL usa `configuration.__EFMigrationsHistory`/`operation.__EFMigrationsHistory`; SQLite usa
  `__ConfigurationMigrationsHistory`/`__OperationalMigrationsHistory`.
- History Configuration legada é realocada antes de o EF consultar pending migrations; ambiguidade falha fechado e
  nunca causa reaplicação de migrations ou merge/delete silencioso.
- SQL manual cria o mesmo model sem rodar host.
- Gateway completo resolve todos os membros de `IStorage`.
- Nenhuma extension de runtime chama migrate/seed.
- PostgreSQL 17 real passa contratos e aceites atômicos.

**Testes:**

```powershell
dotnet test Tests.Storage
dotnet test Tests.Storage --filter "FullyQualifiedName~OperationalMigrationRunner"
./scripts/Test-OperationalPostgreSql.ps1
```

### Resultado da Fase 7

*a preencher*

---

## Fase 8 - paridade, fluxos e fechamento

**Depende de:** Fase 7.

**Escopo:** executar gates completos, eliminar fallback acidental do adapter EF e preparar o Plano 4.

**O que/como:**

- Executar toda a contract suite contra SQLite e PostgreSQL opt-in.
- Executar os aceites exclusivos do P3 listados na matriz:
  - duplicidade create-only;
  - code single-use concorrente;
  - refresh transition concorrente;
  - AP realm/TTL/expiração/colisão;
  - cleanup e purge por realm;
  - profiles de proteção por realm, rotação/leitor anterior e falha fechada;
  - JWT `None`/`Metadata`/`Full` e refresh `Current`/`Snapshot`;
  - CT e disposal real.
- Exercitar ao menos um fluxo OIDC completo opt-in sobre o gateway EF sem mudar o default dos testes.
- Procurar acesso global a AP, get+remove como consumo de code, generic update de refresh no fluxo e
  `EnsureCreated`/`Migrate` no host.
- Atualizar matriz, macro, AGENTS/backlog apenas com resultados reais e diferidos confirmados.
- Produzir handoff do Plano 4 com grupos de testes e seeds necessários à troca de backing.

**Tarefas:**

- [ ] Rodar build/test focal e solução completa.
- [ ] Rodar PostgreSQL real.
- [ ] Inspecionar migrations e SQL por secrets/dados demo.
- [ ] Confirmar que o adapter EF nunca usa o fallback da DF39.
- [ ] Confirmar que fake não recebeu paridade Operational nova.
- [ ] Confirmar que `UpdateAccessTokenClaimsOnRefresh` e sua coluna não permanecem em core, entities,
  materializers, mappings, migrations novas ou código de runtime.
- [ ] Confirmar que MP-9 permanece explicitamente diferida na matriz, sem lookup por subject adicionado sem caller.
- [ ] Registrar contagem final de contratos/aceites e arquivos.
- [ ] Executar `git diff --check`.

**Critérios de aceite:**

- Todos os contratos preservados e aceites substitutos verdes em ambos os providers.
- Code/refresh concorrentes possuem resultado determinístico e falsificável.
- Full gateway EF é utilizável opt-in e não é default do host.
- Runner/SQL operam uma ou duas famílias sem auto-migrate.
- Matriz não contém MP-2/3/5/6/parte Operational de MP-7 pendentes.
- MP-9 está fechada como diferida/não requerida pelo P3, e não como candidata ainda aguardando decisão.
- O Plano 4 pode migrar testes sem redesenhar persistence contracts.

**Testes:**

```powershell
dotnet build RoyalIdentity.sln
dotnet test Tests.Architecture
dotnet test Tests.Storage
dotnet test Tests.Identity
dotnet test Tests.Integration
dotnet test RoyalIdentity.sln
./scripts/Test-OperationalPostgreSql.ps1
git diff --check
```

### Resultado da Fase 8

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(ões) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| Persistir Operational | 1–7 | DF1–DF10/DF23/DF29–DF46 | schema/model/materialização/histories equivalentes | contracts SQLite/PostgreSQL |
| Payload sem duplicar coluna nem credencial | 1–7 | DF9/DF34/DF44/DF45 | nenhuma coluna consultável repetida no payload, exclusões nomeadas comprovadas e falha fechada para membro ausente/nulo | `OperationalPayloadTests` |
| Access token + consent | 2, 6, 7 | DF7/DF8/DF13/DF14/DF17/DF30/DF31/DF38 | AT/CN completos, digests, modos JWT, profiles e cleanup independente | contract + provider acceptances |
| Sessões | 3, 7 | DF15/DF27/DF43 | SS-01..SS-06, touch/revogação concorrentes e MP-9 diferida | session contracts + ADR-017 regressions |
| Code single-use | 1, 4, 7 | DF11/DF39/DF46 | um vencedor concorrente e fallback restrito ao fake, inalcançável no adapter | atomic code acceptance + `OperationalContractsShapeTests` |
| Refresh conditional | 1, 5, 7 | DF12/DF32–DF34/DF37/DF39/DF41/DF42 | transição/versão/tolerância, Current/Snapshot, sem `AccessTokenId` e `at_hash` correto | atomic refresh + claims-mode/at-hash acceptance |
| AP realm-bound + TTL | 1, 6, 7 | DF16/DF29/DF30/DF38/DF40 | gate `StoreAuthorizationParameters`, realm, digest, expiração em segundos, colisão e fail-closed | AP acceptances |
| Cleanup/purge | 6, 7 | DF17/DF18 | batch por tipo, um modo de execução e purge isolado | cleanup/purge acceptances |
| Gateway/lifecycle | 6–8 | DF3/DF21/DF22 | todos os membros, scopes reais, sem UoW global | StorageSession/full harness |
| Migrations/operação | 2, 5, 7, 8 | DF4/DF23/DF24/DF33 | histories por família, drop da opção do client junto da nova semântica, runner/SQL separados e uma/duas conexões | migration/runner/Podman |
| Handoff Plano 4 | 8 | DF21/DF25 | EF completo sem alterar default | solução + OIDC opt-in |

---

## Invariantes a preservar

1. `RoyalIdentity.Data.Operational` nunca referencia core, Configuration, adapter, provider, host ou UI.
2. Somente `RoyalIdentity.Storage.EntityFramework` traduz entidades Operational para modelos do core.
3. Toda operação é realm-bound; nenhuma chave/consulta/mutação cruza realm.
4. Configuration e Operational podem residir em bancos diferentes e nunca exigem FK/transação conjunta.
5. O gateway padrão usa um DbContext/conexão por família mesmo no mesmo banco; não compartilha conexão/transação.
6. Authorization code é single-use sob concorrência real.
7. Refresh transition nunca usa o CAS trivial “valor esperado = mesma instância mutada”.
8. A tolerância pós-consumo não é confundida com a primitiva de concorrência.
9. Artifacts efetivamente persistidos de access/refresh/code, consent e session expirados continuam legíveis até
   purge; AP expirado falha fechado.
10. Refresh nunca depende da linha do access token anterior; cleanup desse access token não invalida o grant.
11. Create-only nunca sobrescreve; consent upsert nunca duplica.
12. Removals/no-ops/counts seguem exatamente a matriz.
13. Materialização é independente e completa; nenhuma referência viva do EF escapa.
14. Comparadores são Ordinal, não defaults de collation.
15. Sessão preserva `SecurityStamp`, expiração, clients e semântica ADR-017.
16. Cleanup não é requisito para correção lógica de expiração.
17. Purge de realm não apaga tombstone/configuração nem chama UserAccounts.
18. `IStorageSession` é lifetime, não UoW global.
19. Todo I/O EF é async e propaga `CancellationToken`.
20. Host não executa migrations/seed e permanece in-memory por default neste plano.
21. PostgreSQL usa histories `configuration.__EFMigrationsHistory`/`operation.__EFMigrationsHistory`; SQLite usa
    `__ConfigurationMigrationsHistory`/`__OperationalMigrationsHistory`, com bootstrap legado antes do EF.
22. Resources/scopes continuam no bridge volátil.
23. Fake não ganha TTL, protection, cleanup ou paridade atômica.
24. Handles bearer, payloads, claims e subjects não aparecem em logs.
25. PAR, messages e replay cache não são assimilados por AP.
26. Reference access token é sempre persistido; JWT segue exclusivamente o modo do realm e `None` é o default.
27. Profile de proteção é selecionado por realm, não contém secrets e nunca cai silenciosamente para `Plain`.
28. Refresh `Current` nunca amplia scopes/resources do grant original; `Snapshot` nunca dispensa validação de
    account/session/client.
29. Claims Operational persistem apenas `Type`/`Value`/`ValueType`; qualquer expansão exige versão explícita.
29a. `ResourceServer.Secrets` nunca entra em payload Operational; a coleção rematerializa vazia e reverter exige
    versão explícita de payload (DF44).
29b. Valor projetado em coluna consultável não é duplicado no payload; a materialização o toma da linha, e o
    lifetime deriva dos dois timestamps autoritativos (DF45).
29c. A factory EF só devolve stores que carregam MP-2/MP-3; o fallback da DF39 é inalcançável a partir do
    adapter (DF46).
30. Não existe configuração ou override de origem das claims do refresh no `Client`.
31. Access/reference, refresh, authorization code e JWT opcional usam `operation.protocol_artifacts`; nenhum store
    tipado consulta ou muta uma linha sem fixar `artifact_type`.
32. A única FK interna é session-client → session no mesmo realm; demais relações Operational são vínculos lógicos.
33. Cleanup usa exatamente um modo configurado, `Hosted` ou `External`, sobre a mesma manutenção reutilizável.
34. Handles bearer/opaques cobertos pela DF38 nunca são persistidos em forma bruta; lookup usa digest separado por
    tipo e realm.
35. Cleanup não conserva grace histórica nem remove dado ainda observável pela tolerância/lifecycle da DF17.
36. O fake não implementa as capabilities atômicas; somente o core pode usar o fallback legado, e a composição EF
    falha se alguma capability da DF39 estiver ausente.
37. `AuthorizationInteractionLifetime` usa segundos, é sempre positivo e materializa `600` quando ausente no
    payload Configuration v1.
38. Access-token artifact usa digest de `jti`, sem coluna bruta; compact JWT nunca é chave de lookup ou falsa
    revogação stateful.
39. Refresh token não carrega `AccessTokenId` nem claim `jti` do access token anterior.
40. `at_hash` emitido em refresh corresponde ao access token novo devolvido na mesma resposta.
41. AP só é acessado quando `StoreAuthorizationParameters=true`; o modo `false` permanece integralmente em query
    string.
42. A remoção da flag do client e sua coluna ocorre junto da ativação de `Current`/`Snapshot` na Fase 5, nunca antes.

---

## Critérios globais de conclusão

- Q1–Q12 respondidas, convertidas em DFs e removidas como bloqueio antes da Fase 1.
- Oito fases concluídas com resultado, arquivos, desvios e comandos registrados.
- `RoyalIdentity.Data.Operational` puro e mappings aplicáveis a context customizado/combined.
- Todos os stores Operational possuem migrations SQLite/PostgreSQL e paridade comprovada.
- MP-2/MP-3 são atômicos sob requests concorrentes com DbContexts independentes.
- MP-5/MP-6 e o purge Operational de MP-7 estão completos.
- Gateway EF completo resolve todos os membros sem I/O síncrono oculto e sem transação cross-family.
- Runner/SQL suportam uma ou duas famílias, com a topologia exata de histories da DF23 e bootstrap legado seguro;
  host não migra.
- PostgreSQL 17 real validado ou a fase permanece incompleta.
- `dotnet build RoyalIdentity.sln` e `dotnet test RoyalIdentity.sln` verdes.
- `git diff --check` sem erros.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| Payload perde dado contratual | serializer não cobre grafo completo | token/consent/code muda ao ler | DF34 + versionamento/round-trip por modelo | Aberto |
| Option aditiva quebra payload Configuration v1 | serializer/default não materializa ausência | realms existentes falham ou mudam de comportamento | DF29 + fixture de payload v1 anterior sem a propriedade | Aberto |
| Bearer handle vaza pelo banco | valor bruto vira PK/log | credencial reutilizável | digest DF38 + redaction DF28 | Aberto |
| Protection/profile inviabiliza leitura | profile ausente, rotação remove leitor ou AAD diverge | outage por realm | DF30 + envelope/protector id + testes multi-profile/rotação | Aberto |
| Code é consumido duas vezes | get/remove ou transação fraca | emissão duplicada | MP-2 + teste com connections independentes | Aberto |
| Invalid request consome code | contrato atômico ignora vínculo/ordem | DoS contra fluxo legítimo | predicado client/redirect da DF11 + pipeline tests | Aberto |
| Client depende da descrição de redirect mismatch | `error_description` deixa de ser `Invalid redirect_uri` | integração observa texto diferente embora continue `invalid_grant` | mudança explícita DF11 + teste/documentação | Aberto |
| Refresh emite antes de ganhar CAS | handler mantém ordem atual | tokens órfãos/duplicados | reorganizar Fase 5 | Aberto |
| Refresh tolerance mascara replay | `TimeSpan.MaxValue`/janela ampla | divergência RFC 9700 | DF37 + backlog explícito | Aberto |
| Lost update no refresh reutilizável | concorrência muda estado/payload após consumo | grant/token subsequente incorreto | versão condicional DF12 | Aberto |
| Session client/touch perdem update | JSON/replace do agregado concorrente | logout/idle incorretos | tabela filha + operações condicionais | Aberto |
| Cleanup remove dado observável | eligibility ignora tolerância/lifecycle | refresh/diagnóstico quebrado | predicados DF17 + fake clock tests | Aberto |
| Refresh volta a depender do access token | handler conserva `AccessTokenId`/lookup legado | cleanup invalida refresh válido | DF41 + teste AT removido antes do refresh | Aberto |
| `at_hash` referencia token anterior | handler usa `accessToken.Token` em vez do token novo | identity token não corresponde à resposta | DF42 + regressão com tokens distintos | Aberto |
| Current amplia o grant | emissão usa permissões atuais do client em vez do grant persistido | elevação de privilégio | interseção scopes/resources + testes negativos DF32 | Aberto |
| Snapshot conserva claim revogada | modo escolhido reutiliza profile claim antiga | autorização obsoleta até expiração | default Current + escolha explícita/TTL do realm | Aberto |
| Realm seleciona Full + Plain | JWT bearer fica legível no banco | reutilização após leak | opt-ins independentes + warning + DF30/DF31 | Aberto |
| Drop da opção do client muda o caminho default | `false` sem resource subset hoje renova claims antigas | comportamento global muda para Current | sequenciamento indivisível da Fase 5 + DF33 + regressão explícita | Aberto |
| Migration da flag precede o novo handler | propriedade/coluna some antes de Current/Snapshot funcionar | build intermediário ou regressão de refresh | drop somente na Fase 5 junto do handler; PostgreSQL na Fase 7 | Aberto |
| AP ignora `StoreAuthorizationParameters=false` | caller realm-bound acessa store incondicionalmente | query-string mode quebra ou cria estado indevido | gate DF16 + testes true/false | Aberto |
| Lookup de access token mistura bearer/JWT | digest deriva do token bruto em vez de `jti` | metadata/full fica inalcançável ou colide semanticamente | DF13/DF38 + contratos por jti | Aberto |
| Cleanup nunca roda | modo externo sem scheduler | crescimento ilimitado | seleção explícita DF17 + health/observabilidade operacional | Aberto |
| Dois workers disputam cleanup | múltiplos nós | locks/carga | batches idempotentes e índices de expiry | Aberto |
| Purge cruza realm | filtro incompleto/cascade | incidente multi-tenant | realm em PK/FK + cenário abrangente | Aberto |
| Combined context diverge | mapping provider fica no context concreto | customização de terceiro quebra | extensões públicas + model tests | Aberto |
| SQLite passa, PostgreSQL falha | estratégia atômica/provider difere | falso sinal de produção | aceites reais PostgreSQL 17 | Aberto |
| Fake aparenta garantia EF | fallback não documentado | testes/default escondem corrida | DF39 + assert de que EF não usa fallback | Aberto |
| Runner sugere atomicidade conjunta | duas conexões falham parcialmente | operação confusa | resultado por família + sem transação distribuída | Aberto |
| Histories de migrations se misturam | providers dependem da history default ou configuram somente Operational | diagnóstico/rollback/scripts acoplados | topologia explícita da DF23 para ambas as famílias + teste same-database | Aberto |
| Mudança da history reaplica migrations Configuration | EF consulta o novo local antes de realocar a history legada | tentativa de recriar tabelas/indisponibilidade | bootstrap pré-`MigrateAsync`, preservação de ids, casos legado/novo/ambíguo e SQL manual | Aberto |
| SQL diverge da migration | model muda sem regenerar | deploy manual incompleto | pending-model/script tests | Aberto |

---

## Diferidos e backlog

- Troca do backing padrão dos testes/host e remoção do fallback transitório — `plan-data-test-migration.md`.
- Persistência/redesign de resources/scopes — plano específico após B-DF22.
- Coordenação idempotente de tombstone Configuration + purge Operational + UserAccounts — ADR/plano administrativo.
- API administrativa e write model — plano próprio.
- PAR/RFC 9126 e eventual `IAuthorizationRequestStore`/`IPushedAuthorizationRequestStore` —
  [an-par-rfc-9126.md](../analisys/an-par-rfc-9126.md) e backlog.
- Persistent `IMessageStore` e redesign atômico de `IReplayCache`.
- Cache Operational.
- Auditoria/outbox/forense durável.
- Refresh-token families, replay revocation e sender constraint conforme DF37.
- Aspire e agendamento/container de migrations/maintenance.
- Lookup de sessão por subject (MP-9), diferido pela DF43 até existir caller comprovado.

---

## Referências

- [template-ai-implementation-plan.md](../references/template-plan/template-ai-implementation-plan.md).
- [plan-data-macro.md](plan-data-macro.md).
- [plan-data-storage-baseline.md](plan-data-storage-baseline.md).
- [plan-data-storage-matrix.md](plan-data-storage-matrix.md).
- [plan-data-configuration-storage.md](plan-data-configuration-storage.md).
- [plans-roadmap-02.md](plans-roadmap-02.md).
- [ADR-013](../../adrs/ADR-013.md).
- [ADR-014](../../adrs/ADR-014.md).
- [ADR-017](../../adrs/ADR-017.md).
- [ADR-018](../../adrs/ADR-018.md).
- [product.md](../foundation/product.md).
- [tech.md](../foundation/tech.md).
- [structure.md](../foundation/structure.md).
- [architecture.md](../foundation/architecture.md).
- [code-style.rules.md](../rules/code-style.rules.md).
- [an-par-rfc-9126.md](../analisys/an-par-rfc-9126.md).
- [RFC 6749](https://www.rfc-editor.org/rfc/rfc6749.html).
- [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700.html).
- [EF Core — ExecuteUpdate/ExecuteDelete](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete).
- [EF Core — Transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions).
- [EF Core — Custom Migrations History Table](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/history-table).
- [System.Text.Json — unmapped members](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members).
- `RoyalIdentity/Contracts/Storage/*.cs`.
- `RoyalIdentity/Users/Contracts/IUserSessionStore.cs`.
- `RoyalIdentity/Contexts/Decorators/LoadCode.cs`.
- `RoyalIdentity/Contexts/Decorators/LoadRefreshToken.cs`.
- `RoyalIdentity/Handlers/RefreshTokenHandler.cs`.
- `Tests.Storage/Storage/Contracts/*.cs`.
