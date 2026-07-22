# Plan: Persistência EF dos dados de configuração do IdP (`plan-data-configuration-storage`)

## Status: EM EXECUÇÃO - Fase 4 de 7 concluída

## Progresso

`████░░░` **57%** - 4 de 7 fases

| Fase | Estado |
|---|---|
| Fase 1 - Fronteiras, projetos e modelo extensível | Concluida |
| Fase 2 - Modelo híbrido e provider SQLite | Concluida |
| Fase 3 - Adapter, lifecycle e snapshot assíncrono | Concluida |
| Fase 4 - ServerOptions, realms, clients e bridge de resources | Concluida |
| Fase 5 - Proteção e persistência de signing keys | Pendente |
| Fase 6 - PostgreSQL, migrations, runner e seeds | Pendente |
| Fase 7 - Paridade, integração e fechamento | Pendente |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluída, `%` e `X de 7`). Antes de fechar uma fase, confirme que decisões,
> critérios de aceite, testes e invariantes relacionados foram aplicados.

---

## Contexto

### Fontes verificadas

- [template-ai-implementation-plan.md](../references/template-plan/template-ai-implementation-plan.md) — shape e regras deste plano.
- [plan-data-macro.md](plan-data-macro.md) — o Plano 2 persiste Configuration antes do Operational Storage.
- [plan-data-storage-baseline.md](plan-data-storage-baseline.md) — decisões DF1-DF25, contract suite e gates dos Planos 2/3/4.
- [plan-data-storage-matrix.md](plan-data-storage-matrix.md) — catálogo das 62 operações, MPs 1/4/7/10 e ordem ServerOptions → realms/options → clients → keys.
- [ADR-013](../../adrs/ADR-013.md) — `Data.*` puro, contratos no core e adapter único em `Storage.EntityFramework`.
- [ADR-018](../../adrs/ADR-018.md) — o fake in-memory é transitório e não recebe evolução de paridade.
- [architecture.md](../foundation/architecture.md) — `Data.*` não usa Feature-Slice nem referencia o core.
- `RoyalIdentity/Contracts/Storage/*.cs` — contratos atuais de `IStorage`, realms, clients, keys e sessão.
- `RoyalIdentity/Authentication/ConfigureRealmCookieAuthenticationOptions.cs` — consumidor síncrono de `ServerOptions` e `IRealmStore.GetByPath`.
- `RoyalIdentity/Contracts/Defaults/Jobs/FirstKeyJob.cs` — o IdP cria signing keys e contém o `return` prematuro registrado como MP-4.
- `RoyalIdentity/Contracts/Defaults/RealmManager.cs` — escrita configuracional atual e borda canônica futura para normalização de domain.
- `RoyalIdentity.UserAccounts*/` e `scripts/Test-UserAccountsPostgreSql.ps1` — precedentes locais de contexts por provider, migrations e PostgreSQL 17 efêmero com Podman.
- [Applying Migrations - EF Core](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying) — scripts revisáveis são o caminho recomendado para produção; migration runner não pertence ao host.
- [ASP.NET Core Data Protection key storage](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0) — key ring compartilhado/persistente e proteção em repouso devem ser configurados pelo consumidor.
- [Options pattern in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0) — named options são cacheadas e exigem remoção via `IOptionsMonitorCache<TOptions>` para serem recriadas.
- [AesGcm - .NET](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm?view=net-10.0) — primitiva autenticada usada pela implementação AES.

### Estado atual do código (verificado em 2026-07-22)

- **Não existe storage EF do core:** a solução não contém `RoyalIdentity.Data.Configuration`, `RoyalIdentity.Storage.EntityFramework`, providers ou runner de migrations.
- **Gateway único:** `IStorage` agrega Configuration e Operational; `ServerOptions` é propriedade síncrona e stores realm-bound partem de `Realm`.
- **Outros consumidores síncronos de `ServerOptions`:** além das cookie options, `DefaultEventDispatcher` lê `storage.ServerOptions` no construtor, `RealmManager.CreateAsync` usa a propriedade ao criar `RealmOptions` e `CheckSessionResult` resolve `ServerOptions` diretamente da DI.
- **Writes configuracionais ainda estão no contrato:** `IRealmStore.SaveAsync/DeleteAsync` e `IKeyStore.AddKeyAsync` coexistem com o `IClientStore` somente leitura.
- **Configuração de cookie faz lookup síncrono:** `ConfigureRealmCookieAuthenticationOptions.Configure` injeta `IStorage` e chama `storage.Realms.GetByPath`.
- **Lifecycle atual não possui recursos reais:** `StorageProvider`/`IStorageSession` devolvem o singleton `MemoryStorage` e `Dispose` é no-op.
- **O IdP escreve signing keys no startup:** `FirstKeyJob` chama `IKeyManager.CreateSigningCredentialsAsync`; ao encontrar a primeira key válida executa `return`, deixando realms posteriores sem provisão.
- **O host padrão usa o fake:** `RoyalIdentity.Server/HostServices.cs` chama `AddInMemoryStorage()`; esta composição permanecerá padrão neste plano.
- **Configuração é live reference no fake:** `MemoryStorage` compartilha instâncias mutáveis de `ServerOptions`, `Realm`, `Client` e `KeyParameters`; o alvo exige materialização independente.
- **Contract suite existe:** `Tests.Storage` possui harness provider-neutral e 101 cenários verdes contra o fake; os aceites exclusivos dos providers EF estão tabelados na matriz.
- **Resources/scopes estão bloqueados:** a DF22 do baseline impede persistir o shape atual; o adapter deve fornecer somente bridge volátil.

### Lacunas, conflitos e restrições

- **Adapter parcial até o Plano 3:** o Plano 2 não pode virar o `IStorage` padrão de produção enquanto as operações Operational não existirem; qualquer composição híbrida é somente opt-in/teste.
- **`IStorage` não representa uma família parcial:** seus onze membros misturam Configuration e Operational; registrar uma implementação EF parcial deixaria operações públicas sem semântica válida. Neste plano, a composição completa existe apenas no harness de testes, combinando Configuration EF com Operational in-memory.
- **Contratos de escrita versus runtime somente leitura:** os writes atuais precisam existir para compatibilidade e contract tests, mas não serão o caminho administrativo nem serão chamados pelo host EF; o redesign desses contratos pertence à API administrativa.
- **DbContexts combináveis:** cada família possui seu context padrão, mas queries/stores não podem exigir herança do context concreto, pois terceiros devem poder aplicar Configuration e Operational ao mesmo `DbContext`.
- **Options não pertencem ao projeto puro:** `Data.Configuration` não pode referenciar `ServerOptions`, `RealmOptions`, `Client`, `Realm` ou `KeyParameters`; serialização e materialização desses tipos pertencem ao adapter.
- **Sem auto-migrate no host:** nenhum `EnsureCreated`, `Migrate` ou `MigrateAsync` pode ser chamado por `RoyalIdentity.Server` ou pelas extensões normais do IdP.
- **Sem concorrência administrativa neste corte:** o IdP não modifica Configuration; a futura API administrativa terá modelo de comandos/concorrência próprio, semelhante ao módulo `UserAccounts`.

### Superfícies impactadas a mapear

- `RoyalIdentity.Data.Configuration` — entidades puras, context padrão, mappings neutros e queries.
- `RoyalIdentity.Storage.EntityFramework` — adaptação core ↔ dados, materialização, snapshot, stores e proteção de key material.
- `RoyalIdentity.Storage.EntityFramework.Sqlite` / `.PostgreSql` — refinamentos, contexts, migrations e registration extensions por provider.
- `RoyalIdentity.Migrations` — executável separado para aplicar migrations e seed opcional.
- `RoyalIdentity` — remoção do lookup síncrono, snapshot para cookies e substituição do job escritor por validação somente leitura.
- `Tests.Storage` / `Tests.Architecture` — fixtures EF, aceites por provider e regras de dependência.
- `scripts/` — geração/verificação de SQL e PostgreSQL efêmero opt-in.

---

## Objetivo

1. Persistir `ServerOptions`, realms/options, clients e signing keys em SQLite e PostgreSQL, preservando as semânticas fechadas no baseline.
2. Manter `Data.Configuration` puro e permitir que um consumidor aplique seus mappings em um `DbContext` próprio, inclusive combinado futuramente com Operational.
3. Entregar portas Configuration EF scoped, snapshot com bootstrap assíncrono e nenhum I/O síncrono oculto, sem registrar um `IStorage` produtivo parcial.
4. Proteger o material das signing keys por uma estratégia substituível (`Plain`, ASP.NET Data Protection ou AES-GCM).
5. Entregar migrations versionadas, SQL revisável, runner separado e seed opcional sem auto-migrate no host.
6. Reutilizar a contract suite contra SQLite e PostgreSQL e deixar um gate verificável para os Planos 3 e 4.

## Fora de escopo

- Dados Operational: authorize parameters, tokens, codes, consents e sessões — destino: `plan-data-operational-storage.md`.
- Persistência de resources/scopes — bloqueada pela DF22 do baseline até o redesign.
- API/UI administrativa e seu modelo de escrita/concorrência — destino: `plan-admin-api-ui.md` ou plano específico.
- KMS, integração com key vaults externos e rotação administrativa de keys — destino: ADR/plano de KMS.
- Cache geral sobre stores EF — destino: `plan-data-caching.md`; o snapshot de cookies existe apenas para eliminar I/O síncrono.
- Troca do backing padrão do host e dos testes HTTP — destino: Planos 3/4.
- Projeto Aspire e containerização do runner — registrados no backlog; não criar neste plano.
- Unificação concreta dos contexts Configuration e Operational — o plano entrega a extensão, não um context combinado.

---

## Decisões fechadas

- **DF1 — Ownership:** `RoyalIdentity.Data.Configuration` contém somente entidades persistentes, mappings neutros, context e queries; não referencia o core. Fonte: ADR-013/architecture.
- **DF2 — Bancos independentes e combináveis:** Configuration e Operational possuem DbContexts e connection strings próprios, que podem apontar para bancos distintos ou para o mesmo banco físico. Fonte: Q1.
- **DF3 — Mappings fora do DbContext:** mappings neutros são aplicados por extensão pública de `ModelBuilder`; cada provider expõe outra extensão pública que aplica os mappings neutros e seus refinamentos. Stores aceitam `TContext : DbContext`, sem exigir o context concreto, permitindo context combinado de terceiros com o model completo do provider escolhido. Fonte: Q1 e revisão do plano.
- **DF4 — Fonte de verdade:** depois do seed inicial, o banco é autoritativo para `ServerOptions` e demais dados de Configuration; configuração do processo fornece conexão, snapshot, proteção e entradas do seed, sem merge em runtime. Fonte: Q2.
- **DF5 — Modelo híbrido:** identidades, bindings, campos consultados e coleções com unicidade são relacionais; grafos de options não consultados por campo são payloads JSON versionados. Fonte: Q3.
- **DF6 — Lifecycle EF:** `DbContext` e portas do adapter são scoped. No Plano 2, o composite test-only cria um escopo por `IStorageSession` e `Dispose()` o encerra; o mesmo modelo será usado pela composição produtiva do Plano 3. `IDbContextFactory` só pode ser usado por consumidor excepcional documentado que não comporte scope. Fonte: Q4/DF21 do baseline e DF20.
- **DF7 — Snapshot sem I/O síncrono e sem ciclo de bootstrap:** cookies e demais consumidores síncronos leem `IConfigurationSnapshot`, que nunca expõe seu grafo mutável interno e devolve cópias defensivas. A carga inicial e o refresh são assíncronos por `IConfigurationSnapshotSource.LoadAsync(ct)`, sem depender de `IStorage.ServerOptions`; fake e adapter EF fornecem implementações próprias da source. O intervalo periódico é configuração obrigatória e validada em toda composição que registre o loader. Após publicação bem-sucedida, o loader usa `IOptionsMonitorCache<CookieAuthenticationOptions>.TryRemove` para invalidar somente o scheme default e a união dos schemes de realm dos snapshots anterior/novo; cookie schemes alheios ao RoyalIdentity não são removidos. A política de falha posterior é DF26. Fonte: Q5/MP-1/DF23, código atual e documentação de options do ASP.NET Core.
- **DF8 — Proteção substituível de signing keys:** `IKeyMaterialProtector` possui implementações `Plain`, `AspNetDataProtection` e `Aes`; configuração de cada uma pertence ao consumidor. Fonte: Q6.
- **DF9 — AES autenticado:** a implementação AES usa AES-GCM, nonce aleatório por escrita, tag de autenticação e envelope versionado; a options fornece a chave e não define como ela é obtida. Fonte: Q6 e documentação `AesGcm`.
- **DF10 — Plain explícito:** `Plain` nunca é escolhido implicitamente e deve produzir warning de segurança; é opção consciente para desenvolvimento/teste ou cenário aceito pelo operador. Fonte: Q6.
- **DF11 — Migrations fora do host:** migrations nunca rodam no `RoyalIdentity.Server`; são aplicadas pelo runner, testes ou SQL manual. Fonte: Q7/Q10.
- **DF12 — Seed opcional:** o runner pode, após migrar, executar seed idempotente de produto; demo é opt-in separado. Migrations não embutem dados de ambiente. Fonte: Q7.
- **DF13 — Runtime Configuration somente leitura:** o IdP lê Configuration; futuras modificações administrativas usam outra camada de acesso, semelhante ao `UserAccounts`. O destino dos writes legados é DF28. Fonte: Q8/Q11.
- **DF14 — Clients sem write facade nova:** `IClientStore` continua somente leitura; runner/test fixture escrevem pelo data layer, não por uma API pública administrativa antecipada. Fonte: Q8.
- **DF15 — Bridge volátil de resources:** `Storage.EntityFramework` fornece bridge interna, realm-bound e em memória para RS-02..RS-05; cada binding de realm recebe os standard identity scopes da configuração de produto do bridge e demo continua opt-in. Nenhuma tabela de resource/scope é criada. Fonte: Q9/DF22 do baseline.
- **DF16 — Estratégia de rollout:** migrations versionadas e SQL revisável; `MigrateAsync` somente no runner e testes. Produção usa preferencialmente scripts SQL revisados. Fonte: Q7/Q10 e documentação EF.
- **DF17 — Concorrência administrativa diferida:** este plano preserva constraints/atomicidade dos métodos existentes, mas não introduz token de concorrência no core; a API administrativa definirá seu próprio controle otimista. Fonte: Q11.
- **DF18 — Schemas:** PostgreSQL usa schemas `configuration` e, futuramente, `operational`; SQLite usa os mesmos nomes de tabela sem schema. Fonte: Q12.
- **DF19 — Signing key não é escrita pelo host:** remover `FirstKeyJob` da composição padrão; seed/administrador/KMS provisiona e rotaciona. A reação a realm habilitado sem key atual é DF27. Esta decisão supera a MP-4 da matriz do baseline (que previa corrigir o job), já anotada como superada pela elaboração deste plano. Fonte: Q13.
- **DF20 — Host padrão permanece in-memory e não há `IStorage` parcial de produção:** o adapter Configuration EF é opt-in neste plano e registra somente suas portas de Configuration. A fixture da contract suite pode montar um composite test-only com Operational in-memory; a implementação/registration produtiva de `IStorage`, `IStorageProvider` e `IStorageSession` EF aguarda o Plano 3, quando todos os membros do gateway tiverem backing definido. Fonte: Q14, DF21 do baseline e revisão do plano.
- **DF21 — Runner geral:** `RoyalIdentity.Migrations` começa com Configuration e será estendido pelo Plano 3 para Operational; ele aceita uma ou duas conexões e é o candidato futuro a container no Aspire. Fonte: Q15.
- **DF22 — Exclusão lógica:** deletar realm não interno grava tombstone permanente, oculta-o de lookups normais, preserva configuração e reserva path/domain; realm interno ou ausente retorna `false`. Purge Operational e coordenação cross-family ficam fora. Fonte: DF20/MP-7 do baseline.
- **DF23 — Comparadores e domain:** identificadores usam semântica Ordinal; domain entra em lowercase na borda de escrita/consulta e o adapter rejeita `SaveAsync` direto com domain não canônico. Fonte: DF18/MP-10 do baseline.
- **DF24 — Keys create-only:** `(RealmId, KeyId)` é único; duplicidade falha visivelmente e nunca sobrescreve. Listagens e ausência seguem KY-01..KY-05 da matriz. Fonte: DF16/DF19/DF25 do baseline.
- **DF25 — Materialização:** toda leitura devolve grafo independente e completo; nenhuma alteração posterior ao objeto materializado persiste sem operação explícita. Fonte: DF17 do baseline.
- **DF26 — Last-known-good indefinido no snapshot:** quando a carga inicial foi válida e um refresh periódico falha, o snapshot mantém o último estado válido indefinidamente, registrando erro e idade observável sem payload sensível; não há limite de staleness nem fail-closed do snapshot — disponibilidade prevalece, e se o banco estiver indisponível em pontos essenciais o sistema degrada por si nos caminhos que dependem dele. A política vale para os consumidores do snapshot (configuração de cookies); os stores continuam lendo o banco ao vivo. Fonte: resposta humana Q16 (opção B).
- **DF27 — Startup falha sem signing key utilizável:** realm habilitado sem signing key atual que possa ser desprotegida, materializada e selecionada para `Realm.Options.Keys.MainSigningCredentialsAlgorithm` impede o início do host — a validação somente leitura roda na inicialização e falha `StartAsync`. Encontrar apenas um id atual não atende a validação. Limite documentado da decisão: realm criado/habilitado em runtime após o boot não é coberto por essa validação e falha na primeira assinatura, até a camada administrativa prover key no próprio fluxo; a primeira key de cada realm vem do seed inicial/inicialização (DF28). Fonte: resposta humana Q17 (opção A), `DefaultKeyManager.GetSigningCredentialsAsync(Realm, ct)` e revisão do plano.
- **DF28 — Writes legados preservados agora, com remoção planejada:** `IRealmStore.SaveAsync/DeleteAsync` e `IKeyStore.AddKeyAsync` permanecem no contrato e são implementados neste plano somente por compatibilidade/contract tests; o host EF não os chama e pode operar com credencial SELECT-only. **Direção fechada:** esses membros serão removidos do contrato do core e os pontos onde o IdP escreve configuração serão migrados — a escrita administrativa ficará no futuro módulo administrativo, e a primeira key (de realm ou qualquer outra) pertence ao seed inicial/inicialização (runner). A remoção acompanha o plano administrativo/KMS e inclui reestruturar as fixtures da contract suite para semear pelo data layer. A opção de lançar `NotSupportedException` no adapter foi rejeitada porque quebraria a reutilização da suíte (o harness cria realms via `SaveAsync`). Fonte: resposta humana Q18 (opção A, com remoção planejada).

---

## Histórico de decisões

| Pergunta | Resposta humana | Conclusão aplicada |
|---|---|---|
| Q1 — bancos e contexts | A, com mappings externos e possibilidade de context único customizado | DF2/DF3 |
| Q2 — `ServerOptions` | A | DF4 |
| Q3 — mapping | A, híbrido | DF5 |
| Q4 — lifecycle | A; factory somente em exceções | DF6 |
| Q5 — cookie/options | A; refresh periódico configurável | DF7 |
| Q6 — key material | A, com Plain/Data Protection/AES básicos | DF8-DF10 |
| Q7 — migration/seed | runner separado; host nunca migra; seed opcional; Aspire futuro | DF11/DF12 |
| Q8 — writes de clients/config | somente leitura no IdP; admin futura usa camada própria | DF13/DF14 |
| Q9 — resources | A | DF15 |
| Q10 — aplicação de migrations | A, conforme Q7 | DF16 |
| Q11 — concorrência | Configuration não é escrita pelo IdP; tratar no plano administrativo | DF17 |
| Q12 — schema | A, `configuration`/`operational` | DF18 |
| Q13 — signing keys | A, remover escrita do host | DF19 |
| Q14 — ativação | A, host padrão continua in-memory | DF20 |
| Q15 — runner | A, runner geral | DF21 |
| Q16 — falha do refresh periódico | B; preservar o funcionamento do sistema é mais importante — se o banco falhar em pontos importantes, o sistema para por conta própria | DF26 |
| Q17 — realm habilitado sem signing key | A, falhar `StartAsync` | DF27 |
| Q18 — writes legados de realm/key | A, com remoção planejada: escrita ficará no módulo administrativo; primeira chave (de realm ou outra) fica no seed inicial/inicialização | DF28 |

---

## Design alvo

### Contratos e bordas

- `ConfigurationModelBuilderExtensions.ApplyRoyalIdentityConfigurationMappings(ModelBuilder, ConfigurationModelOptions)`: aplica somente mappings neutros sem depender do context padrão.
- `ApplyRoyalIdentityConfigurationSqliteMappings(...)` / `ApplyRoyalIdentityConfigurationPostgreSqlMappings(...)`: extensões públicas dos projetos de provider; aplicam o mapping neutro e os refinamentos completos (`schema`, tipos JSON, collations e índices) do provider escolhido. Contexts padrão e customizados chamam a extensão correspondente.
- `ConfigurationDbContext`: context-base provider-neutral em `Data.Configuration`; expõe `DbSet` e seu `OnModelCreating` delega a um hook virtual que, por padrão, chama a extensão neutra. Os contexts de provider sobrescrevem somente esse hook para chamar a extensão pública completa do provider, sem colocar mappings dentro do context.
- `AddEntityFrameworkConfigurationStorage<TContext>(...) where TContext : DbContext`: registra portas e stores scoped de Configuration usando `Set<TEntity>()`; não exige `ConfigurationDbContext` e não registra um `IStorage` parcial.
- `IConfigurationSnapshotSource.LoadAsync(CancellationToken)`: contrato core-owned de bootstrap/refresh assíncrono; retorna todo o estado necessário ao snapshot em um grafo independente. Há implementação in-memory e implementação EF, nenhuma delas lê `IStorage.ServerOptions`.
- `IConfigurationSnapshot`: contrato core-owned, singleton e síncrono sobre estado já publicado; expõe `ServerOptions` e busca de realm por path sem I/O e sem entregar referências ao grafo mutável interno.
- `ConfigurationSnapshotRefreshOptions.RefreshInterval`: obrigatório e positivo na composição EF; não recebe default oculto.
- `ConfigurationSnapshotHostedService`: dirigido por `IConfigurationSnapshotSource`; carrega antes do servidor aceitar tráfego e renova periodicamente com um scope novo. Depois de publicar um snapshot válido, chama `TryRemove` para o scheme default e para a união dos schemes de realm dos snapshots anterior/novo, sem limpar schemes externos; refresh falho mantém o último snapshot e o cache de options correspondentes indefinidamente, registrando erro/idade sem dados sensíveis (DF26).
- `IKeyMaterialProtector`: protege/desprotege somente o payload de `KeyParameters.Key`; metadata permanece consultável. Implementações básicas são escolhidas explicitamente na registration extension.
- `IStorage`/`IStorageProvider`/`IStorageSession`: nenhuma implementação parcial é registrada para produção no Plano 2. A fixture de contrato monta um composite test-only (Configuration EF + Operational in-memory) e comprova scope/disposal real; a composição EF produtiva desses contratos fica no Plano 3.
- `RoyalIdentity.Migrations`: executável sem referência do host; seleciona provider e conexão por configuração/argumentos, aplica migrations e executa seed somente quando solicitado.
- `IClientStore`: permanece somente leitura; nenhuma operação de create/update/delete entra no core.
- Writes legados de realm/key: implementados por compatibilidade/contract tests (DF28); nunca viram serviço administrativo, e sua remoção do contrato é direção fechada para o plano administrativo/KMS.

### Modelo, dados e persistência

```text
configuration.server_options
  id smallint PK, check id = 1
  payload_version int not null
  payload_json json/jsonb not null
  updated_at_utc timestamp not null

configuration.realms
  id string PK
  path string not null
  domain string not null (lowercase canônico)
  display_name string not null
  enabled bool not null
  internal bool not null
  options_version int not null
  options_json json/jsonb not null
  deleted_at_utc timestamp null
  unique Ordinal(path), incluindo tombstones
  unique Ordinal(domain), incluindo tombstones

configuration.clients
  realm_id string FK realms(id)
  client_id string
  escalares do Client em colunas tipadas
  PK (realm_id, client_id), comparação Ordinal

configuration.client_string_values
  realm_id, client_id, kind, value, comparison_key
  FK (realm_id, client_id)
  PK/unique (realm_id, client_id, kind, comparison_key)
  cobre URIs, grant/response types, scopes/resources, algoritmos e restrictions

configuration.client_claims
  realm_id, client_id, ordinal/id, type, value, value_type, issuer, original_issuer
  FK (realm_id, client_id)

configuration.client_secrets
  realm_id, client_id, ordinal/id, type, value, description, expiration_utc
  FK (realm_id, client_id)

configuration.signing_keys
  realm_id string FK realms(id)
  key_id string
  name, security_algorithm, serialization_format, encoding
  created_utc, not_before_utc, expires_utc
  protector_id string not null
  protected_material text/blob not null
  PK (realm_id, key_id), create-only
  index (realm_id, created_utc)
```

- PostgreSQL usa `jsonb`; SQLite usa `TEXT` com validação JSON quando suportada pelo provider.
- O payload de `RealmOptions` não serializa a referência `ServerOptions`; o adapter a reconstrói a partir do snapshot autoritativo ao materializar.
- Payloads JSON possuem versão explícita e opções de serialização determinísticas; falha de payload/version é erro de configuração e nunca retorna objeto parcial.
- Coleções de `Client` são reconstruídas integralmente; comparadores próprios, como CORS case-insensitive, são restabelecidos pelo adapter, não herdados da collation.
- Tombstones conservam realms, options, clients e keys; todos os lookups normais filtram `deleted_at_utc` antes de materializar/bindar stores.
- `resources/scopes` não aparecem no model EF; o bridge cria standard identity scopes por realm em memória e some no redesign.

### Arquitetura alvo

```text
RoyalIdentity.Data.Configuration/
  ConfigurationDbContext.cs
  Entities/
  Mappings/
  Queries/
  ConfigurationModelBuilderExtensions.cs
  (EF Core only; NO RoyalIdentity reference)

RoyalIdentity.Storage.EntityFramework/
  Configuration/
    Materialization/
    Stores/
    Snapshot/
    Resources/
  Security/KeyMaterial/
  StorageProvider.cs / StorageSession.cs
  Extensions/
  (references RoyalIdentity + Data.Configuration)

RoyalIdentity.Storage.EntityFramework.Sqlite/
  ConfigurationSqliteDbContext.cs
  public provider mapping extension + design-time factory + Migrations/

RoyalIdentity.Storage.EntityFramework.PostgreSql/
  ConfigurationPostgreSqlDbContext.cs
  public provider mapping extension + design-time factory + Migrations/

RoyalIdentity.Migrations/
  provider selection + migration execution + optional product/demo seed

Tests.Storage/
  existing provider-neutral contracts
  SQLite fixture (default)
  PostgreSQL fixture (opt-in)
  protector/snapshot/migration/seed acceptance tests
```

Um context customizado pode combinar famílias sem herdar dos contexts padrão:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
	base.OnModelCreating(modelBuilder);
	modelBuilder.ApplyRoyalIdentityConfigurationPostgreSqlMappings(configurationOptions);
	modelBuilder.ApplyRoyalIdentityOperationalPostgreSqlMappings(operationalOptions); // futuro Plano 3
}
```

Para SQLite, o mesmo context chama as duas extensões `...SqliteMappings`. Misturar extensões de providers no
mesmo model é inválido; cada model escolhe exatamente um provider e recebe tanto os mappings neutros quanto
os refinamentos desse provider.

### Segurança, concorrência e confiabilidade

- `PlainKeyMaterialProtector` grava o payload sem cifra somente após opt-in explícito e warning; testes verificam que ele não é default.
- `AspNetDataProtectionKeyMaterialProtector` usa purpose estável/versionado; o consumidor configura key ring, compartilhamento e proteção em repouso.
- `AesKeyMaterialProtector` exige chave válida na options, usa AES-GCM, nonce aleatório e tag; ciphertext adulterado falha fechado.
- Nenhum protector, store, runner ou log escreve key material, chave AES, connection string completa ou client secret.
- Startup falha antes de servir requests quando a carga inicial de Configuration falha ou não existe `ServerOptions`; realm habilitado sem signing key falha `StartAsync` (DF27).
- Falha de refresh periódico mantém last-known-good indefinidamente (DF26); telemetria registra falha e idade sem expor payload.
- O snapshot publica um grafo completo de uma vez, mantém o grafo interno inacessível e entrega cópias defensivas de `ServerOptions`/`Realm`; mutar um objeto retornado não altera leituras posteriores.
- Todo refresh publicado invalida as named options RoyalIdentity já cacheadas (default + união dos realms anterior/novo); falha de refresh não invalida nem mistura options novas com o último snapshot válido, e schemes externos não são afetados.
- Toda query é realm-bound por coluna/FK, inclusive quando ids iguais existem em realms diferentes.
- Todo I/O EF propaga `CancellationToken`; nenhum método síncrono abre conexão ou executa query.
- Constraints de unicidade são a última linha de defesa para domain/path/client/key; não se depende de check-then-write isolado.
- Concorrência de comandos administrativos não é simulada no facade legado; fica bloqueada ao plano da API administrativa.

### Compatibilidade, migração e rollout

- `RoyalIdentity.Server` continua registrando `AddInMemoryStorage`; nenhum banco ou runner vira pré-requisito do host padrão neste plano.
- A fixture EF executa somente os contratos Configuration e infrastructure pertencentes ao Plano 2 por um composite test-only com Operational in-memory; nenhuma registration pública oferece `IStorage` parcialmente funcional antes do Plano 3.
- `IRealmStore.GetByPath` síncrono é removido somente depois de `ConfigureRealmCookieAuthenticationOptions` usar o snapshot.
- `FirstKeyJob` deixa a composição padrão; fixtures in-memory e seed do runner passam a criar keys antes do startup.
- Migrations são checked-in nos providers. O runner é idempotente por histórico EF; seed é idempotente por chaves naturais.
- Scripts SQL ficam versionados em `scripts/sql/configuration/{sqlite,postgresql}/`; PostgreSQL deve possuir script idempotente revisável.
- O runner retorna exit code não zero em migration/seed inválido e nunca é chamado implicitamente por `AddHostServices` ou `AddOpenIdConnectProviderServices`.
- A futura composição Aspire executará o runner como workload/container separado e fará hosts dependerem de sua conclusão; somente o backlog é atualizado agora.

---

## Ordem de execução

1. **Fase 1 (fronteiras e projetos)** — cria os seams antes do schema.
2. **Fase 2 (modelo e SQLite)** — materializa o modelo mais barato de testar.
3. **Fase 3 (adapter e snapshot)** — elimina sync I/O e implementa lifetime real.
4. **Fase 4 (configuração principal)** — entrega ServerOptions, realms e clients sobre a infraestrutura estável.
5. **Fase 5 (keys)** — adiciona a superfície de maior sensibilidade após persistência e snapshot estarem cobertos.
6. **Fase 6 (PostgreSQL e operação)** — valida o alvo produtivo e entrega migrations/runner/scripts/seeds.
7. **Fase 7 (paridade e handoff)** — executa gates amplos e prepara Planos 3/4.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
dotnet test Tests.Architecture
dotnet test Tests.Storage
dotnet test RoyalIdentity.sln
```

---

## Fase 1 - Fronteiras, projetos e modelo extensível

**Depende de:** DF1-DF6, DF18, ADR-013 e architecture foundation.

**Escopo:** solução, novos projetos, referências, contratos de mapping/registration e testes de arquitetura.

**O que/como:** criar a topologia sem implementar stores de domínio. O context padrão deve ser conveniente, mas toda configuração e todo consumo devem funcionar com `TContext : DbContext` e `Set<TEntity>()`.

**Tarefas:**

- [x] Criar `RoyalIdentity.Data.Configuration`, `RoyalIdentity.Storage.EntityFramework`, `.Sqlite`, `.PostgreSql` e `RoyalIdentity.Migrations` em `src` da solução.
- [x] Adicionar somente referências permitidas: `Data.Configuration` → EF Core; adapter → core + Data; providers → adapter/Data + provider EF; runner → providers.
- [x] Criar entidades puras iniciais e `ConfigurationDbContext` sem referência a tipos do core, com hook virtual que apenas seleciona a extensão externa de mapping.
- [x] Criar `ConfigurationModelBuilderExtensions` neutra, opções de model/schema fora do DbContext e extensões públicas de mapping por provider.
- [x] Fazer stores/queries futuros dependerem de `TContext : DbContext`/`Set<TEntity>()`, não do context concreto.
- [x] Criar registration extensions genéricas que permitam substituir o context padrão por context customizado.
- [x] Adicionar testes em `Tests.Architecture` para bloquear core em `Data.Configuration`, provider no Data e dependências invertidas.
- [x] Adicionar `CombinedTestDbContext` sem herdar de `ConfigurationDbContext` e testar que as extensões SQLite e PostgreSQL produzem, separadamente, o model neutro mais os refinamentos iniciais do provider escolhido.
- [x] Testar a registration genérica com context customizado: lifetime scoped, accessor sobre a mesma instância, conexão fechada sem trabalho implícito e ausência de `IStorage`/`IStorageProvider`/`IStorageSession` parciais.

**Critérios de aceite:** projetos compilam; `Data.Configuration` não referencia `RoyalIdentity`; um context arbitrário aplica o model neutro e os refinamentos presentes nesta fase (`jsonb`/schema no PostgreSQL, `TEXT`/sem schema no SQLite); as extensões públicas são o único seam pelo qual collations/índices completos serão adicionados nas Fases 2/6, sem depender dos contexts concretos; provider-specific code não entra no projeto puro; a registration genérica usa o context customizado scoped, não registra gateway parcial e não abre conexão nem executa migration implicitamente.

**Testes:** `dotnet build RoyalIdentity.sln`; `dotnet test Tests.Architecture`.

### Resultado da Fase 1

**Concluída em 2026-07-22.** Topologia criada com comandos `dotnet` (new/sln/add), projetos no solution
folder `src`:

- **`RoyalIdentity.Data.Configuration`** (puro): remove o `FrameworkReference` de ASP.NET herdado do
  `Directory.Build.props` (precedente do módulo `UserAccounts`) e referencia somente
  `Microsoft.EntityFrameworkCore.Relational 10.0.10`. Contém as 7 entidades iniciais do schema do design
  (`ServerOptionsEntity`, `RealmEntity`, `ClientEntity`, `ClientStringValueEntity`, `ClientClaimEntity`,
  `ClientSecretEntity`, `SigningKeyEntity` — `ClientEntity` com identidade + primeiros escalares; o
  inventário completo de `Client` é da Fase 2), `ConfigurationModelOptions` (schema fora do context),
  `ConfigurationModelBuilderExtensions.ApplyRoyalIdentityConfigurationMappings` (tabelas/colunas snake_case,
  PKs compostas, FKs, check constraint do singleton de server options, índice `(realm_id, created_utc)` de
  keys; uniques/comparadores/serialização ficam na Fase 2) e `ConfigurationDbContext` com `OnModelCreating`
  **sealed** delegando ao hook virtual `ApplyConfigurationModel` (default = extensão neutra).
- **`RoyalIdentity.Storage.EntityFramework`** (adapter): referencia core + Data; entrega o seam scoped
  `IConfigurationDbContextAccessor`/`ConfigurationDbContextAccessor<TContext>` e
  `AddEntityFrameworkConfigurationStorage<TContext>() where TContext : DbContext`, que registra somente as
  portas de Configuration — sem `IStorage`/`IStorageProvider`/`IStorageSession` (DF20) e sem auto-migrate
  (DF11); stores/snapshot das próximas fases usam esse seam via `Set<TEntity>()`.
- **`.Sqlite`/`.PostgreSql`** (providers): referenciam adapter + Data + provider EF
  (`Microsoft.EntityFrameworkCore.Sqlite 10.0.10` com bundle SQLitePCLRaw 3.0.3;
  `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0` — mesmas versões resolvidas pela família `UserAccounts`) e
  expõem as extensões públicas `ApplyRoyalIdentityConfigurationSqliteMappings` (sem schema, payloads TEXT) e
  `ApplyRoyalIdentityConfigurationPostgreSqlMappings` (schema `configuration`, payloads `jsonb`), compondo a
  extensão neutra (DF3/DF18). Contexts de provider ficam para as Fases 2/6.
- **`RoyalIdentity.Migrations`** (runner): console separado, sem SDK web e sem referência ao host, referencia os
  dois providers; a dependência transitiva de runtime em `Microsoft.AspNetCore.App` via adapter/core foi aceita
  na revisão da Fase 1. O `Program` stub recusa execução implícita (mensagem + exit code 64) até a Fase 6.
- **`Tests.Architecture`**: +18 testes — `ConfigurationStorageBoundaryTests` (pureza do Data por assembly
  refs e csproj; adapter → core+Data; providers → adapter+Data sem bind direto do core; runner → providers;
  host e core sem referência à família nova) com o helper `ProjectReferenceReader`, e
  `ConfigurationModelExtensibilityTests` (contexts combinados `CombinedSqliteDbContext`/
  `CombinedPostgreSqlDbContext` que **não** herdam de `ConfigurationDbContext` e obtêm o model neutro + os
  refinamentos iniciais do provider escolhido: mesmas tabelas nos dois providers, schema/jsonb no PostgreSQL,
  sem schema/TEXT no SQLite; o context padrão aplica o model neutro), além de
  `ConfigurationStorageRegistrationTests` (context customizado e accessor na mesma instância scoped, scopes
  independentes, conexão fechada, fluent return/null guard e ausência do gateway parcial de DF20).

Critérios de aceite verificados: solução compila (0 erros; nenhum warning novo nos projetos criados);
`Data.Configuration` sem `ProjectReference` algum e sem bind de core/adapter/ASP.NET; contexts arbitrários
aplicam o model neutro e os refinamentos iniciais de cada provider pelas extensões públicas; collations,
índices e demais refinamentos completos permanecem critérios explícitos das Fases 2/6; nenhum código
provider-specific no projeto puro; a registration genérica usa o context customizado scoped, não registra
gateway parcial e não abre conexão nem executa migration.

Validação: `dotnet build RoyalIdentity.sln` — êxito; `dotnet test Tests.Architecture` — 33 aprovados
(15 preexistentes + 18 novos); solução completa — 683 aprovados, 0 falhas, 1 ignorado (PostgreSQL opt-in
preexistente de `Tests.UserAccounts`).

---

## Fase 2 - Modelo híbrido e provider SQLite

**Depende de:** Fase 1, DF4/DF5/DF18/DF22-DF25.

**Escopo:** entidades, mappings, serialização de payloads e provider SQLite.

**O que/como:** implementar o schema híbrido descrito no design; manter todos os tipos core fora do Data e testar materialização no adapter com banco SQLite real em memória.

**Tarefas:**

- [x] Implementar entidades/tabelas de server options, realms, clients, client values/claims/secrets e signing keys.
- [x] Implementar PKs, FKs, unique indexes e comparadores/collations explícitos necessários para semântica Ordinal.
- [x] Implementar payload JSON versionado para `ServerOptions`/`RealmOptions`, sem serializar a referência circular de server options.
- [x] Mapear todos os escalares e coleções atuais de `Client`; adicionar teste que falha quando nova propriedade pública do modelo não possui decisão de persistência/materialização.
- [x] Implementar `ConfigurationSqliteDbContext`, refinamentos SQLite e design-time factory.
- [x] Fazer o context SQLite padrão e um context customizado chamarem a mesma extensão pública `ApplyRoyalIdentityConfigurationSqliteMappings`.
- [x] Criar a migration inicial SQLite sem `EnsureCreated`.
- [x] Testar schema `EnsureDeleted` → `MigrateAsync` → consulta do histórico de migrations.
- [x] Testar round-trip com objetos materializados independentes, dois realms com ids de client/key iguais e payload JSON inválido/version desconhecida fail-closed.

**Critérios de aceite:** migration SQLite cria somente tabelas Configuration; todos os campos de `Client` possuem round-trip; alteração de objeto carregado não muda banco sem save; indexes Ordinal não dependem da collation default; não há tabelas de resources/Operational.

**Testes:** `dotnet test Tests.Storage --filter "FullyQualifiedName~Sqlite|FullyQualifiedName~ConfigurationModel|FullyQualifiedName~Materialization"`.

### Resultado da Fase 2

**Concluída em 2026-07-22.** Modelo híbrido completo e provider SQLite validado com banco real em memória; todos
os tipos core permanecem fora do projeto puro (materialização/serialização vivem no adapter).

- **Entidades (`Data.Configuration`, puro):** `ClientEntity` recebeu o inventário completo de escalares do core
  `Client` (35 colunas), com enums (`ClientType`, `TokenExpiration`) e `TimeSpan` armazenados como primitivos
  (`int`/`long ticks`) — o projeto não referencia o core, então a conversão fica no adapter. Novo
  `ClientStringValueKinds` (vocabulário de persistência das 13 coleções de string do client). Demais entidades
  (server options, realms, client values/claims/secrets, signing keys) já vinham da Fase 1.
- **Mappings neutros:** colunas snake_case de todos os escalares de `ClientEntity`; unique indexes
  `ux_realms_path`/`ux_realms_domain` **sem filtro** (path/domain reservados inclusive para tombstones — DF22);
  PKs/FKs compostas e check constraint do singleton mantidos.
- **Refinamento SQLite (`.Sqlite`):** colação `BINARY` explícita em todas as colunas identificadoras (id, path,
  domain, realm_id, client_id, kind, comparison_key, key_id) — semântica Ordinal não depende do default do
  provider (DF23); payloads como `TEXT`.
- **Materialização e serialização (adapter):** `ServerOptionsPayloadSerializer`/`RealmOptionsPayloadSerializer`
  produzem payload JSON **versionado** (v1) e determinístico; `RealmOptions` **não** serializa a referência
  circular `ServerOptions` (removida por modifier), reconstruída a partir do grafo autoritativo no
  `Deserialize(..., ServerOptions)` (via `CreateObject`). `GetOnlyCollectionModifier` dá às coleções get-only
  das options semântica *clear-then-add* (fidelidade total — inclusive **remover** um item default, p.ex.
  restringir `Keys.SigningCredentialsAlgorithms` — e preservação de comparers como o CORS case-insensitive);
  coleção persistida como `null` falha fechado em vez de conservar defaults. `ConfigurationObjectJsonConverter`
  materializa valores de `Discovery.CustomEntries` nos tipos CLR naturais de JSON, preservando inclusive strings
  relativas `~/...` usadas pelo discovery handler, em vez de devolvê-las como `JsonElement`.
  `ClientMaterializer` mapeia `Client` ↔ (root + string values por kind + claims + secrets) reconstruindo o
  comparer `OrdinalIgnoreCase` de `AllowedCorsOrigins` (comparison key em lowercase); antes de materializar,
  rejeita root fora do realm solicitado, satélites de outro `(realm, client)`, kind desconhecido e enums inválidos
  via `ConfigurationMaterializationException`, sem incluir ids/valores nas mensagens. Falha de versão/JSON é
  fail-closed via `ConfigurationPayloadException` (mensagem sem payload — invariante 10).
- **Provider SQLite:** `ConfigurationSqliteDbContext` (sobrescreve só o hook `ApplyConfigurationModel` para
  chamar a extensão pública), `ConfigurationSqliteDesignTimeDbContextFactory` e a migration inicial
  `InitialConfiguration` gerada por `dotnet ef` (sem `EnsureCreated`) — cria **apenas** as 7 tabelas de
  Configuration, com a colação e os índices esperados. Pacote `Microsoft.EntityFrameworkCore.Design`
  (`PrivateAssets=all`) adicionado ao provider para a tooling.
- **Testes (`Tests.Storage/Configuration/`, +26):** `SqliteConfigurationMigrationTests` (EnsureDeleted →
  MigrateAsync → histórico; só as 7 tabelas; check constraint do singleton), `ConfigurationMaterializationClientTests`
  (round-trip de client totalmente preenchido; independência do grafo materializado — DF25; isolamento de mesmo
  client id em dois realms), `ConfigurationModelPayloadTests` (round-trip estável e fiel; sem ref circular;
  fail-closed de versão/JSON), `ConfigurationModelClientCoverageTests` (guarda: toda propriedade pública de
  `Client` tem decisão de persistência documentada) e `SqliteConfigurationSigningKeyTests` (mesmo key id em dois
  realms; PK create-only rejeita duplicata — DF24). Os testes de payload também cobrem valores primitivos/nested
  de `CustomEntries` e rejeição de coleção get-only `null` nos grafos server/realm; os testes do materializador
  cobrem fail-closed para projeções cross-realm/cross-client, kind desconhecido e enums inválidos. Helper
  `SqliteConfigurationDatabase` cria banco real `:memory:` migrado sobre conexão compartilhada.

Critérios de aceite verificados: a migration cria somente tabelas Configuration; todos os campos de `Client`
têm round-trip; alterar objeto carregado não muda o banco sem save; indexes Ordinal não dependem da collation
default (BINARY explícito); nenhuma tabela de resources/Operational. Provider-specific fora do projeto puro
mantido; o context SQLite e um customizado usam a mesma extensão pública (validado desde a Fase 1 por
`ConfigurationModelExtensibilityTests`).

Validação: `dotnet build RoyalIdentity.sln` — êxito (0 erros; nenhum warning novo nos projetos criados);
`dotnet test Tests.Storage` — 127 aprovados (101 preexistentes + 26 novos); `dotnet test Tests.Architecture` —
33 aprovados (sem regressão de fronteira); solução completa — 709 aprovados, 0 falhas, 1 ignorado (PostgreSQL
opt-in preexistente de `Tests.UserAccounts`).

---

## Fase 3 - Adapter, lifecycle e snapshot assíncrono

**Depende de:** Fases 1-2, DF6/DF7/DF20/DF26, MP-1 e DF21/DF23 do baseline.

**Escopo:** adapter EF, DI, composite/session test-only, snapshot e consumidores síncronos de configuração.

**O que/como:** tornar as portas Configuration scoped e remover qualquer consulta síncrona ao banco. O snapshot é uma projeção técnica mínima para APIs síncronas, não o cache geral do Plano 5. A source assíncrona faz o bootstrap sem depender da propriedade síncrona de `IStorage`.

**Tarefas:**

- [x] Registrar somente as portas Configuration EF scoped; impedir por teste de DI que a registration pública do Plano 2 forneça `IStorage`, `IStorageProvider` ou `IStorageSession` parciais.
- [x] Criar composite exclusivamente test-only, combinando Configuration EF com Operational in-memory, para reutilizar os contratos P2 que ainda recebem `IStorage`.
- [x] No composite test-only, implementar `IStorageProvider` que cria scope e `IStorageSession` owner desse scope; comprovar que `Dispose` libera uma dependência scoped, sem cristalizar comportamento após disposal.
- [x] Introduzir `IConfigurationSnapshotSource.LoadAsync(ct)`, `IConfigurationSnapshot` e `ConfigurationSnapshotRefreshOptions` com intervalo obrigatório na composição EF.
- [x] Carregar snapshot inicial antes do tráfego e realizar refresh periódico com scope novo e `CancellationToken`.
- [x] Implementar a política last-known-good indefinida de DF26 e publicar estado/idade observável sem payload sensível.
- [x] Implementar sources EF e in-memory que materializam grafos independentes sem ler `IStorage.ServerOptions`; registrar a source in-memory no host padrão.
- [x] Migrar `ConfigureRealmCookieAuthenticationOptions`, `DefaultEventDispatcher`, `RealmManager` e `CheckSessionResult` para a leitura síncrona do snapshot já carregado no ponto de uso, sem capturar uma cópia indefinidamente; manter `IStorage.ServerOptions` apenas como contrato legado até a composição completa do Plano 3.
- [x] Remover `IRealmStore.GetByPath` síncrono e suas implementações após migrar todos os callers.
- [x] Publicar snapshots atomicamente e devolver cópias defensivas; provar que mutar `ServerOptions`/`Realm` obtido do snapshot não altera a próxima leitura.
- [x] Após cada refresh bem-sucedido, usar `TryRemove` no cache de `CookieAuthenticationOptions` para o scheme default e a união dos schemes de realm anterior/novo; após falha, preservar snapshot/options e nunca remover scheme externo ao RoyalIdentity.
- [x] Fazer writes legados usados por testes solicitarem reload assíncrono; publicar/invalidate somente se a nova carga completa for válida.
- [x] Criar testes de ausência de sync I/O, bootstrap sem ciclo, refresh periódico configurado, falha inicial, last-known-good, invalidação de named options e disposal real do composite test-only.

**Critérios de aceite:** cookie options e os outros consumidores inventariados não leem `IStorage.ServerOptions`; nenhuma source depende de `IStorage` para obter o estado inicial; nenhum método síncrono consulta EF; `GetByPath` síncrono não existe; a registration EF pública não resolve gateway parcial; cada sessão do composite test-only cria/libera scope próprio; configuração sem intervalo falha na validação; a mesma named option RoyalIdentity já materializada é recriada com novos valores após refresh, permanece coerente após refresh falho e um scheme externo permanece cacheado; DF26 está aplicada e testada; host padrão continua in-memory e funcional com o snapshot.

**Testes:** `dotnet test Tests.Storage --filter "FullyQualifiedName~StorageSession|FullyQualifiedName~ConfigurationSnapshot|FullyQualifiedName~Cookie|FullyQualifiedName~Registration"`; `dotnet test Tests.Integration --filter "FullyQualifiedName~Realm|FullyQualifiedName~CheckSession|FullyQualifiedName~EventDispatcher"`.

### Resultado da Fase 3

**Concluída em 2026-07-22.** Snapshot com bootstrap/refresh assíncrono entregue; todos os consumidores
síncronos inventariados migrados; `GetByPath` síncrono removido; nenhum gateway EF parcial registrado.

- **Cópia defensiva (core):** copy ctor de `ServerOptions` (e o único faltante, `ServerUIOptions`) — completa o
  padrão de copy ctors já existente nas demais options. O snapshot assume propriedade de uma cópia integral na
  publicação, religa cada `RealmOptions.ServerOptions` ao server autoritativo interno e entrega novas cópias nas
  leituras. `DiscoveryOptions.CustomEntries` também é copiado recursivamente, sem compartilhar dicionários ou
  coleções aninhadas com a source ou com consumidores (DF7/invariante 17).
- **Contratos e infra do snapshot (core, `RoyalIdentity/Configuration/`):** `IConfigurationSnapshot` (view
  síncrona singleton: `ServerOptions`/`FindRealmByPath` devolvem cópias defensivas; `IsLoaded`, `RealmPaths`,
  `LoadedAtUtc`, `LastRefreshFailureUtc`); `IConfigurationSnapshotSource.LoadAsync(ct)`;
  `ConfigurationSnapshotData`; `ConfigurationSnapshotRefreshOptions` (intervalo obrigatório, `Validate()`);
  `PublishedConfigurationSnapshot` (imutável, publica atômico); `ConfigurationSnapshotHolder` (swap volátil,
  `Publish`/`MarkRefreshFailure`); `IConfigurationSnapshotRefresher` serializa cargas explícitas e periódicas por
  um gate assíncrono, com `RefreshAsync` fail-closed para bootstrap/writes e `TryRefreshAsync` last-known-good +
  grava falha + log sem payload (DF26), propagando cancelamento solicitado pelo caller;
  `ConfigurationSnapshotHostedService` faz a carga inicial não-guardada no `StartAsync` (falha o startup) e usa
  um loop sequencial com `PeriodicTimer`/`TimeProvider`, cancelado e aguardado no shutdown; seam público
  `AddConfigurationSnapshot()`.
- **Invalidação de named options:** após publish, o refresher chama `TryRemove` no
  `IOptionsMonitorCache<CookieAuthenticationOptions>` para o scheme default + união dos schemes de realm
  anterior/novo; schemes externos ao RoyalIdentity nunca são tocados; refresh falho não invalida nada.
- **Sources:** in-memory (`RoyalIdentity.Storage.InMemory`, lê o estado do `MemoryStorage` direto — nunca
  `IStorage.ServerOptions` — e clona; registrada no host padrão por `AddInMemoryStorage` com intervalo default
  de 5 min) e EF (`EntityFrameworkConfigurationSnapshotSource` + `RealmMaterializer`, lê `server_options`
  singleton + realms live filtrando tombstones via accessor scoped; server options ausente é fail-closed);
  `AddEntityFrameworkConfigurationSnapshotSource` registra source scoped + serializers/materializer, sem
  gateway parcial (DF20).
- **Migração dos consumidores:** `ConfigureRealmCookieAuthenticationOptions` (agora
  `snapshot.ServerOptions`/`FindRealmByPath`), `DefaultEventDispatcher` (lê `DispatchEvents` do snapshot no
  dispatch, não no ctor), `RealmManager.CreateAsync` (usa `snapshot.ServerOptions`) e `CheckSessionResult`
  (dívida inalcançável migrada; o singleton lê o snapshot em cada execução, sem reter a primeira configuração).
  `IStorage.ServerOptions` permanece como contrato legado. `RealmManager`
  dispara `snapshotRefresher.RefreshAsync` após create/update/enable/disable, para que um realm criado em
  runtime fique visível de imediato aos consumidores síncronos (writes legados solicitando reload — DF7/DF28).
- **`IRealmStore.GetByPath` síncrono removido** (interface + impl InMemory); único caller era o cookie config.
- **Testes (+23):** `Tests.Storage/Configuration/` — `ConfigurationSnapshotTests` (publish atômico, propriedade
  integral e cópias defensivas inclusive do grafo aninhado, bootstrap-throws-before-load, last-known-good +
  gravação de falha + preservação de scheme externo, falha antes do bootstrap, propagação de cancelamento,
  serialização de refreshes concorrentes e releitura do `CheckSessionResult`),
  `ConfigurationSnapshotHostedServiceTests` (carga inicial, falha inicial falha o startup, intervalo inválido,
  cancelamento e espera do refresh periódico no shutdown), `ConfigurationSnapshotSourceSqliteTests`
  (materialização EF + exclusão de tombstone + fail-closed),
  `CompositeStorageSessionTests` (session combina Config EF + Operational in-memory, scope independente por
  session, `Dispose` libera a dependência scoped e não sobrevive após disposal), com o harness `SnapshotTestHarness`
  (compõe a infra interna pelo seam público + source/cache/clock controláveis); `Tests.Architecture`
  (`ConfigurationStorageRegistrationTests` +1 guard do snapshot source scoped sem gateway parcial);
  `Tests.Integration/Realm/RealmConfigurationSnapshotTests` (snapshot carregado no startup, realm em runtime
  visível, cópia defensiva end-to-end).

Critérios de aceite verificados: cookie options e os demais consumidores não leem `IStorage.ServerOptions`;
nenhuma source depende de `IStorage`; nenhum método síncrono consulta EF; `GetByPath` síncrono não existe; a
registration EF pública não resolve gateway parcial; cada session do composite cria/libera scope próprio;
configuração sem intervalo falha na validação; named options RoyalIdentity são recriadas após refresh e
preservadas após falha, sem afetar scheme externo; DF26 aplicada e testada; host padrão continua in-memory e
funcional com o snapshot.

Validação: `dotnet build RoyalIdentity.sln` — êxito (0 erros; nenhum warning novo nos arquivos criados);
`dotnet test Tests.Storage` — 146 aprovados; `dotnet test Tests.Architecture` — 34; `dotnet test Tests.Integration`
— 226 (sem regressão após a migração dos consumidores); solução completa — 732 aprovados, 0 falhas, 1 ignorado
(PostgreSQL opt-in preexistente de `Tests.UserAccounts`).

---

## Fase 4 - ServerOptions, realms, clients e bridge de resources

**Depende de:** Fase 3, DF4/DF13-DF15/DF22/DF23/DF25 e linhas ST-01/02/09/10, RL-*, CL-*, RS-* da matriz.

**Escopo:** stores/materializers de ServerOptions, realm/options e clients; tombstone; bridge resource.

**O que/como:** implementar os stores na ordem do baseline, sempre com consultas no banco e materialização completa. Writes de realm existem somente por compatibilidade/testes; clients continuam read-only.

**Tarefas:**

- [x] Implementar leitura singleton de `ServerOptions` e falhar quando o registro autoritativo estiver ausente/inválido.
- [x] Implementar todos os lookups async de realm e aplicar DF28 ao upsert/exclusão legados (compatibilidade/contract tests; host EF não os chama).
- [x] Normalizar domain nas bordas core e rejeitar `SaveAsync` EF direto quando o valor não estiver lowercase.
- [x] Preservar tombstone, options, clients e keys; filtrar realm excluído de todos os lookups e bindings normais.
- [x] Garantir que path/domain de tombstone permaneçam reservados pelos unique indexes.
- [x] Implementar `IClientStore` somente leitura, incluindo filtro `Enabled` e materialização integral de collections/claims/secrets.
- [x] Implementar bridge volátil realm-bound de resources/scopes, sem referência do adapter ao `Storage.InMemory`.
- [x] Inicializar standard identity scopes por realm no bridge a partir de sua configuração de produto e manter recursos demo em rota opt-in separada.
- [x] Reutilizar os contract tests P2-owned contra a fixture SQLite e adicionar aceites de domain/tombstone/cancelamento.

**Critérios de aceite:** RL/CL preservam a matriz; tombstone é invisível mas reserva path/domain; internal/ausente retorna `false`; client disabled só aparece no lookup não-enabled; resources não criam tabelas; todo CT chega ao EF.

**Testes:** `dotnet test Tests.Storage --filter "FullyQualifiedName~RealmStore|FullyQualifiedName~ClientStore|FullyQualifiedName~ResourceStore|FullyQualifiedName~ServerOptions"`.

### Resultado da Fase 4

**Concluída em 2026-07-22.** As portas de leitura de Configuration foram implementadas sobre EF/SQLite,
sem registrar um `IStorage` parcial e sem antecipar a persistência do modelo instável de resources/scopes.

- **Seam Configuration-only:** `IConfigurationStoreFactory` é scoped e expõe somente leitura autoritativa de
  `ServerOptions`, `IRealmStore` e bindings de client/resource. `AddEntityFrameworkConfigurationStorage<TContext>`
  registra essas portas sobre o context escolhido pelo consumidor, mas continua sem fornecer
  `IStorage`/`IStorageProvider`/`IStorageSession`, sem abrir conexão e sem aplicar migration (DF3/DF6/DF20).
- **ServerOptions e realms:** um reader único materializa o singleton versionado e é reutilizado pela source do
  snapshot; ausência ou payload inválido falha fechado. O realm store implementa todos os lookups com consultas
  async/canceláveis e materialização independente, upsert legado, recusa de ressurreição, proteção da identidade
  de realms internos e exclusão lógica permanente via `DeletedAtUtc` (DF22/DF25/DF28).
- **Domain e tombstone:** `RealmManager.CreateAsync` normaliza domain com `ToLowerInvariant()` antes da consulta e
  escrita; o adapter rejeita `SaveAsync` direto não canônico. Path/domain continuam reservados pelos índices
  únicos sem filtro. A exclusão conserva payload de options, clients e signing keys, mas realm, client e resource
  deixam de aparecer nos lookups/bindings normais.
- **Clients:** `IClientStore` realm-bound consulta root e satélites no banco, distingue lookup comum/enabled e
  usa `ClientMaterializer` para reconstruir integralmente escalares, strings, claims, secrets e comparadores, sem
  entregar referências persistentes compartilhadas.
- **Resources/scopes voláteis:** `IConfigurationResourceSource` + `ConfigurationResourceBridgeOptions` alimentam
  uma bridge realm-bound sem referência a `Storage.InMemory` e sem novas tabelas. Os cinco standard identity
  scopes são configuração padrão por realm; `AddEntityFrameworkConfigurationDemoResources(realmId)` é um opt-in
  separado e limitado ao realm informado. Cada leitura entrega cópias profundas e valida nomes/URIs Ordinal.
- **Contract suite e aceites (+38 em `Tests.Storage`, +1 em `Tests.Integration`):** a fixture
  `SqliteConfigurationStorageHarness` reutiliza sem alteração os 31 cenários RL/CL/RS provider-neutral por um
  composite exclusivamente test-only; mais 7 aceites cobrem singleton/independência, client completo,
  tombstone/reserva/ressurreição, domain, cancelamento, cópia da bridge e standard/demo. O teste de integração
  comprova a normalização do manager; o guard de arquitetura verifica factory/realm store scoped e ausência do
  gateway parcial.

Critérios de aceite verificados: RL/CL preservam a matriz; tombstone é invisível e reserva path/domain;
internal/ausente retorna `false`; client disabled aparece apenas no lookup não-enabled; resources permanecem
fora do model/migration; todas as operações EF assíncronas recebem `CancellationToken`; host padrão não mudou.

Validação: `dotnet build RoyalIdentity.sln` — êxito; `dotnet test Tests.Storage` — 184 aprovados;
`dotnet test Tests.Architecture` — 34; `dotnet test Tests.Integration` — 227; solução completa — 771 aprovados,
0 falhas, 1 ignorado (PostgreSQL opt-in preexistente de `Tests.UserAccounts`).

---

## Fase 5 - Proteção e persistência de signing keys

**Depende de:** Fases 2-4, DF8-DF10/DF19/DF24/DF27/DF28, MP-4 superada e KY-01..KY-05.

**Escopo:** key protectors, key store, seed de testes, startup validator e remoção do job escritor.

**O que/como:** persistir metadata relacional e material opaco protegido. O IdP somente lê e valida disponibilidade; criação/rotação sai do processo host.

**Tarefas:**

- [ ] Definir `IKeyMaterialProtector` assíncrono e envelope versionado com identificador do protector.
- [ ] Implementar `PlainKeyMaterialProtector` com opt-in explícito e warning sem material sensível.
- [ ] Implementar `AspNetDataProtectionKeyMaterialProtector` com purpose estável/versionado e documentação sobre key ring persistente/compartilhado.
- [ ] Implementar `AesKeyMaterialProtectorOptions` e AES-GCM com validação de chave, nonce aleatório e autenticação.
- [ ] Implementar `IKeyStore` EF com insert create-only, listagens temporais/ordem e exceções de ausência definidas na matriz.
- [ ] Testar ciphertext diferente para o mesmo plaintext em duas proteções AES e falha em envelope/tag adulterado.
- [ ] Testar que Data Protection round-trip funciona entre instâncias que compartilham o mesmo provider e falha fechado com provider incompatível.
- [ ] Remover `FirstKeyJob` da composição padrão e aplicar DF27 por `IKeyManager.GetSigningCredentialsAsync(realm, ct)` ou caminho equivalente completo: falhar `StartAsync` se cada realm habilitado não possuir key atual desprotegível, materializável e com o algoritmo principal configurado.
- [ ] Testar startup com key ausente, ciphertext corrompido, protector incompatível e key atual de algoritmo diferente; todos falham antes de servir requests.
- [ ] Atualizar seeds/fixtures in-memory para fornecer uma key utilizável por realm habilitado antes do startup, sem reintroduzir job escritor.
- [ ] Garantir que logs/exceções/snapshots não contenham key material nem chave AES.

**Critérios de aceite:** nenhuma key nova é criada pelo host; cada realm habilitado sem key atual utilizável para seu algoritmo principal falha o startup (DF27); existência de id sem materialização válida não produz falso positivo; duplicidade não sobrescreve; KY-02/03 respeitam bordas inclusivas e ordem; os três protectors possuem testes; `Plain` nunca é default.

**Testes:** `dotnet test Tests.Storage --filter "FullyQualifiedName~KeyStore|FullyQualifiedName~KeyMaterial|FullyQualifiedName~SigningKey"`; `dotnet test Tests.Security`.

### Resultado da Fase 5

*a preencher*

---

## Fase 6 - PostgreSQL, migrations, runner e seeds

**Depende de:** Fases 2-5, DF11/DF12/DF16/DF18/DF21/DF27/DF28.

**Escopo:** provider PostgreSQL, migrations dos dois providers, runner, scripts SQL, seed e Podman opt-in.

**O que/como:** entregar o caminho operacional sem acoplar o host. O runner geral executa Configuration agora e recebe extensão Operational no Plano 3.

**Tarefas:**

- [ ] Implementar `ConfigurationPostgreSqlDbContext`, schema `configuration`, `jsonb`, collations/índices e design-time factory.
- [ ] Fazer o context PostgreSQL padrão e um context customizado chamarem a mesma extensão pública `ApplyRoyalIdentityConfigurationPostgreSqlMappings`.
- [ ] Criar migration PostgreSQL equivalente ao model SQLite e conferir snapshot/model diff.
- [ ] Implementar `RoyalIdentity.Migrations` com seleção explícita de provider/conexão, migrate e seed opcional.
- [ ] Fazer o runner aceitar futuramente conexões Configuration/Operational separadas sem exigir que sejam bancos diferentes.
- [ ] Implementar seed de produto idempotente para `ServerOptions`, realms internos, `server_admin` e uma signing key protegida/utilizável para cada realm habilitado; não tentar persistir standard scopes.
- [ ] Implementar seed demo separado e opt-in, incluindo key própria se o realm demo nascer habilitado; não copiar URLs/segredos demo para seed de produto.
- [ ] Fazer segunda execução de migrate/seed resultar em zero duplicatas e estado equivalente.
- [ ] Gerar e versionar SQL SQLite e PostgreSQL; gerar PostgreSQL idempotente e validar que não há `EnsureCreated`.
- [ ] Criar `scripts/Test-ConfigurationPostgreSql.ps1` seguindo o precedente: verificar Podman, iniciar machine se necessário, usar PostgreSQL 17 efêmero em porta dinâmica não padrão e limpar em `finally`.
- [ ] Executar runner/testes contra PostgreSQL real e verificar schema, migrations, seed, indexes Ordinal, JSONB e protectors compatíveis.
- [ ] Documentar exemplos sem connection strings reais e garantir exit code não zero em provider/configuração/migration/seed inválidos.

**Critérios de aceite:** runner é o único executável que aplica migration fora de testes; host não contém chamada de migration; SQLite/PostgreSQL chegam ao mesmo comportamento; SQL revisável existe; seed é opcional/idempotente e todo realm habilitado semeado passa pela validação completa de DF27; teste PostgreSQL não conflita com porta 5432 e sempre limpa o container.

**Testes:** `dotnet test Tests.Storage --filter "FullyQualifiedName~Migration|FullyQualifiedName~Seed"`; `pwsh -File scripts/Test-ConfigurationPostgreSql.ps1` (opt-in, obrigatório antes de concluir a fase quando Podman estiver disponível).

### Resultado da Fase 6

*a preencher*

---

## Fase 7 - Paridade, integração e fechamento

**Depende de:** Fases 1-6 e gate do Plano 4 na matriz.

**Escopo:** contract suite completa P2, testes amplos, documentação, roadmap e handoff.

**O que/como:** provar a paridade Configuration sem promover o adapter parcial a host padrão. Atualizar os documentos com resultados reais e lacunas remanescentes.

**Tarefas:**

- [ ] Executar todos os IDs P2 da matriz contra SQLite e os mesmos cenários contra PostgreSQL opt-in.
- [ ] Cobrir create-only de keys, tombstone/reserva, domain lowercase, CT, materialização independente, snapshot e disposal real.
- [ ] Verificar que resources continuam bridge volátil e que nenhuma superfície Operational ganhou persistência acidental.
- [ ] Executar testes de arquitetura para todos os novos projetos e os contexts customizados SQLite/PostgreSQL com refinamentos completos.
- [ ] Executar solução completa e registrar contagens/skips/limitações no resultado da fase.
- [ ] Confirmar por busca que `RoyalIdentity.Server`/core não chamam `EnsureCreated`, `Migrate` ou `MigrateAsync`.
- [ ] Confirmar que o host padrão continua `AddInMemoryStorage` e não requer banco.
- [ ] Atualizar macro-plano, roadmap, AGENTS e backlog com o estado real e o gate do Plano 3; preservar na matriz do baseline a anotação já feita de que MP-4 foi superada por DF19/DF27/DF28.
- [ ] Registrar no handoff que a ativação produtiva do gateway aguarda Operational, API administrativa e migração do backing padrão.

**Critérios de aceite:** todos os critérios globais estão atendidos; contract suite Configuration verde em SQLite e PostgreSQL real validado; host padrão continua executável sem DB; zero perguntas/semânticas Configuration abertas; documentação não afirma que o adapter parcial é produção completa.

**Testes:** `dotnet build RoyalIdentity.sln`; `dotnet test RoyalIdentity.sln`; `pwsh -File scripts/Test-ConfigurationPostgreSql.ps1`; `git diff --check`.

### Resultado da Fase 7

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(es) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| Persistir Configuration | 2, 4, 5, 6 | DF4/DF5/DF18/DF22-DF25/DF28 | schema e semânticas P2 equivalentes nos providers; writes legados limitados à compatibilidade | `Tests.Storage`; script PostgreSQL |
| Context extensível/combinável | 1, 2, 6 | DF1-DF3 | context arbitrário aplica mappings neutros e refinamentos completos do provider | `Tests.Architecture`; model tests SQLite/PostgreSQL |
| Lifecycle sem sync I/O | 3 | DF6/DF7/DF20/DF26 | source assíncrona sem ciclo; snapshot defensivo; named options invalidadas; nenhum gateway parcial | filtros StorageSession/Snapshot/Cookie/Registration |
| Runtime somente leitura | 3-7 | DF13/DF14/DF19/DF20/DF28 | host não grava Configuration nem ativa EF por padrão | startup/key tests; buscas de composição |
| Key material protegido e utilizável | 5, 6 | DF8-DF10/DF24/DF27/DF28 | três protectors; tamper fail-closed; startup valida material e algoritmo; seed por realm habilitado | KeyMaterial/KeyStore/SigningKey/Seed tests |
| Migrations operáveis | 6, 7 | DF11/DF12/DF16/DF21 | runner/SQL separados; seed idempotente; host sem migrate | migration/seed tests; script PostgreSQL |
| Preparar Planos 3/4 | 7 | DF15/DF17/DF20/DF28 | gate documentado sem persistir Operational/resources nem registrar `IStorage` parcial | solução completa + revisão da matriz |

---

## Invariantes a preservar

1. `RoyalIdentity.Data.Configuration` nunca referencia o core, adapter, provider, host ou UI.
2. Somente `RoyalIdentity.Storage.EntityFramework` traduz entidades puras para modelos/contratos do core.
3. Toda leitura de client/key é realm-bound e não observa registro de outro realm com o mesmo identificador.
4. Realm excluído é tombstone permanente; path/domain não podem ser reutilizados e configuração não é apagada fisicamente.
5. Realms internos continuam não removíveis e suas identidades imutáveis não são flexibilizadas.
6. Domain é lowercase canônico; os demais identificadores seguem comparador Ordinal definido na matriz.
7. Material persistido é independente de referência de objeto e só muda por escrita explícita.
8. `IClientStore` continua somente leitura; API administrativa não é antecipada pelo facade do IdP.
9. O host do IdP não executa migrations nem seed e não escreve signing keys.
10. Key material, chaves AES, client secrets e connection strings não aparecem em logs ou erros.
11. Signing keys históricas necessárias à validação permanecem consultáveis conforme KY-03.
12. Resources/scopes não recebem tabela ou nova feature parity no fake.
13. Operational e UserAccounts permanecem fora do schema Configuration.
14. `IStorageSession` não é transação global entre famílias/bancos.
15. Todo I/O EF é assíncrono e propaga `CancellationToken`; APIs síncronas não escondem banco.
16. `RoyalIdentity.Server` permanece in-memory por padrão até decisão dos Planos 3/4.
17. O snapshot não expõe seu grafo interno mutável; cada publicação é atômica e somente as named options RoyalIdentity afetadas são invalidadas após publicação válida.
18. O Plano 2 não registra `IStorage`, `IStorageProvider` ou `IStorageSession` produtivos com membros Operational indisponíveis.
19. Todo realm habilitado criado por seed possui signing key atual, desprotegível e compatível com o algoritmo principal configurado.

---

## Critérios globais de conclusão

- Sete fases concluídas, com resultados, arquivos, desvios e comandos registrados.
- `ServerOptions`, realms/options, clients e keys possuem migrations SQLite/PostgreSQL e paridade P2 comprovada.
- Mappings neutros e refinamentos SQLite/PostgreSQL podem ser aplicados a contexts customizados sem herdar de `ConfigurationDbContext`.
- Snapshot possui source assíncrona sem ciclo, remove todos os acessos síncronos inventariados a configuração persistida, protege o grafo interno e invalida named options após refresh válido.
- Nenhum gateway EF parcial é registrado em produção; o composite test-only comprova scope/disposal real para os contratos P2.
- Signing keys não são criadas pelo host e seu material usa protector explicitamente selecionado.
- Cada realm habilitado semeado possui key utilizável, e startup rejeita material ausente, corrompido ou incompatível com o algoritmo principal.
- Runner e SQL manual existem; nenhum host aplica migrations.
- PostgreSQL 17 real foi validado ou a impossibilidade externa foi registrada sem marcar a fase como concluída.
- `dotnet build RoyalIdentity.sln` e `dotnet test RoyalIdentity.sln` verdes.
- `git diff --check` sem erros.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| Payload JSON perde opção nova | propriedade pública de options/client não entra no round-trip | configuração silenciosamente alterada | teste de cobertura de propriedades de `Client` + payload versionado + `GetOnlyCollectionModifier` (clear-then-add fiel a remoções em coleções get-only das options) | Mitigado (Fase 2) |
| Context combinado é apenas teórico | store exige `ConfigurationDbContext` concreto ou perde refinamentos do provider | terceiros não conseguem unificar contexts com model equivalente | registration genérica + `CombinedTestDbContext` SQLite na Fase 2 e PostgreSQL na Fase 6 | Aberto |
| Snapshot fica obsoleto | refresh falha ou intervalo é excessivo | configuração antiga continua ativa | intervalo obrigatório (`Validate()`), idade observável (`LoadedAtUtc`/`LastRefreshFailureUtc`) e last-known-good explícito (`TryRefreshAsync`) | Mitigado (Fase 3) |
| Snapshot expõe mutabilidade | caller altera `ServerOptions`/`Realm` retornado | configuração publicada muda fora do refresh | grafo interno inacessível, cópia defensiva (copy ctor de `ServerOptions`/clone de `Realm`) e teste de mutação | Mitigado (Fase 3) |
| Cookie mantém named options antigas | snapshot renova após scheme já materializado | autenticação continua com cookie/rotas anteriores | `TryRemove` do default e união dos schemes anterior/novo após publicação válida; testado mesmo nome e preservação de scheme externo | Mitigado (Fase 3) |
| Data Protection não compartilha key ring | nós usam rings distintos ou efêmeros | signing key persistida não pode ser aberta | documentação/acceptance multi-instância; configuração pertence ao consumidor | Aberto |
| Chave AES fraca/exposta | options recebe tamanho inválido ou segredo aparece em log | perda de confidencialidade das signing keys | validação AES-GCM; redaction; testes negativos | Aberto |
| Plain vira default acidental | DI resolve Plain sem opt-in | segredo em texto claro | sem default + warning + teste de registration | Aberto |
| Remoção do `FirstKeyJob` quebra dev/test | fixture não semeia key antes do startup | servidor não inicia | seed explícito in-memory/runner e startup test | Aberto |
| Validação aceita key inutilizável | startup verifica apenas id/período e ignora decrypt/algoritmo | primeira assinatura falha após o host iniciar | validar pelo caminho completo do key manager; testes de ciphertext, protector e algoritmo | Aberto |
| Adapter parcial é usado como produção | consumidor ativa P2 antes do Operational | operações OAuth sem backing durável/coerente | não registrar no host padrão; documentação e guard de composição | Aberto |
| Collation diverge SQLite/PostgreSQL | índice usa default do provider | casing produz lookup/unique diferente | collation/comparação explícita + mesmos ids em dois casings/providers | Aberto |
| SQL versionado diverge das migrations | model muda sem regenerar script | implantação manual incompleta | teste pending model changes e verificação de scripts na Fase 6 | Aberto |
| Seed mistura produto e demo | runner padrão insere URLs/segredos locais | configuração insegura em produção | perfis separados; demo exige opt-in explícito | Aberto |
| Admin futura reutiliza writes legados | facade parece CRUD disponível | concorrência/validação insuficientes | DF13/DF17; camada administrativa própria e redesign contratual | Mitigado |

---

## Diferidos e backlog

- Persistência Operational e conclusão do gateway EF — destino: `plan-data-operational-storage.md`.
- Troca do backing padrão dos testes/host — destino: `plan-data-test-migration.md`.
- API administrativa, write model e concorrência otimista — destino: plano administrativo próprio.
- KMS, rotação e integrações com key vaults — destino: ADR/plano de KMS.
- Persistência/redesign de resources/scopes — destino: plano específico já bloqueado pela DF22 do baseline.
- Cache geral e invalidação administrativa — destino: `plan-data-caching.md`.
- Aspire com runner de migrations como container/workload separado — destino: backlog `Aspire e orquestração de ambiente`.
- Coordenação idempotente de exclusão de realm entre Configuration, Operational e UserAccounts — destino: ADR/plano do fluxo administrativo.
- Remoção dos writes legados de `IRealmStore`/`IKeyStore` após introdução da camada administrativa/KMS —
  **direção fechada em DF28**, não apenas possibilidade: inclui migrar os pontos onde o IdP escreve
  configuração, mover a escrita para o módulo administrativo, manter a primeira key no seed
  inicial/inicialização e reestruturar as fixtures da contract suite para semear pelo data layer — destino:
  planos correspondentes.

---

## Referências

- [template-ai-implementation-plan.md](../references/template-plan/template-ai-implementation-plan.md).
- [plan-data-macro.md](plan-data-macro.md).
- [plan-data-storage-baseline.md](plan-data-storage-baseline.md).
- [plan-data-storage-matrix.md](plan-data-storage-matrix.md).
- [plans-roadmap-02.md](plans-roadmap-02.md).
- [ADR-013](../../adrs/ADR-013.md).
- [ADR-018](../../adrs/ADR-018.md).
- [product.md](../foundation/product.md).
- [tech.md](../foundation/tech.md).
- [structure.md](../foundation/structure.md).
- [architecture.md](../foundation/architecture.md).
- [code-style.rules.md](../rules/code-style.rules.md).
- `RoyalIdentity/Contracts/Storage/*.cs`.
- `RoyalIdentity/Authentication/ConfigureRealmCookieAuthenticationOptions.cs`.
- `RoyalIdentity/Contracts/Defaults/DefaultEventDispatcher.cs`.
- `RoyalIdentity/Contracts/Defaults/Jobs/FirstKeyJob.cs`.
- `RoyalIdentity/Contracts/Defaults/DefaultKeyManager.cs`.
- `RoyalIdentity/Contracts/Defaults/RealmManager.cs`.
- `RoyalIdentity/Responses/HttpResults/CheckSessionResult.cs`.
- `Tests.Storage/Storage/Contracts/*.cs`.
- `scripts/Test-UserAccountsPostgreSql.ps1`.
- [Applying Migrations - EF Core](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).
- [ASP.NET Core Data Protection key storage](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0).
- [Options pattern in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0).
- [AesGcm - .NET](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm?view=net-10.0).
