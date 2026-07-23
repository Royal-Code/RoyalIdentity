# Análise — Revisão do plano `plan-data-configuration-storage` (Persistência EF de Configuration)

> **Status:** revisão pós-implementação das 7 fases; não é ADR nem altera decisões. Registra a verificação do
> que foi entregue contra o [plano](../plans/plan-data-configuration-storage.md), suas decisões (DF1-DF28),
> invariantes e critérios de aceite.
>
> **Objetivo:** confirmar, com evidência no código, que a implementação é fiel ao plano; apontar pontos fortes,
> observações e o gate remanescente para o Plano 3.
>
> **Escopo:** `RoyalIdentity.Data.Configuration`, `RoyalIdentity.Storage.EntityFramework` (+ `.Sqlite`/
> `.PostgreSql`), `RoyalIdentity.Migrations`, a infra de snapshot em `RoyalIdentity/Configuration/`, os
> consumidores migrados no core, os testes (`Tests.Storage`/`Tests.Architecture`/`Tests.Integration`) e os
> scripts SQL/Podman.
>
> **Método:** leitura direta dos artefatos, checagens dirigidas (grep de invariantes), build da solução, suíte
> completa e `dotnet ef migrations has-pending-model-changes` nos dois providers. Data: 2026-07-22.

---

## 1. Veredito

A implementação está **completa, fiel ao plano e de alta qualidade**. As sete fases foram entregues; as decisões
fechadas (DF1-DF28) e os invariantes verificados por código estão respeitados. A suíte completa passa, os dois
providers reportam **zero mudanças de model pendentes**, e não há registro produtivo de gateway parcial nem
qualquer caminho de migration/`EnsureCreated` no host ou no core.

Não foram encontrados defeitos bloqueantes. A revisão identificou uma lacuna operacional no seed `product`,
corrigida após a primeira versão deste documento: redirects do `server_admin` agora são entradas explícitas e
obrigatórias, sem localhost embutido. As demais observações da seção 6 são limitações documentadas, otimizações
ou diferenças deliberadas pelo plano.

---

## 2. Cobertura por fase

### Fase 1 — Fronteiras, projetos e modelo extensível
Topologia criada com as referências permitidas; `RoyalIdentity.Data.Configuration` é puro (sem core/adapter/
ASP.NET). O `ConfigurationDbContext` delega o mapping a um hook virtual; a extensão neutra e as extensões por
provider são o único seam de mappings. Guardas em `Tests.Architecture`
(`ConfigurationStorageBoundaryTests`/`ConfigurationModelExtensibilityTests`) fixam o grafo de dependências e a
equivalência de contexts padrão/customizados. **Conforme.**

### Fase 2 — Modelo híbrido e provider SQLite
`ClientEntity` carrega o inventário escalar completo do `Client` (enums/`TimeSpan` como primitivos, pois o
projeto não referencia o core); coleções em `client_string_values` por `ClientStringValueKinds`. Mapping neutro
com uniques de `path`/`domain` **sem filtro** (reserva de tombstone) e refinamento SQLite com colação `BINARY`
explícita nas colunas identificadoras (Ordinal não depende do default). Serialização JSON versionada de
`ServerOptions`/`RealmOptions` sem a referência circular (`RealmOptionsPayloadSerializer` remove `ServerOptions`
e o reconstrói no `Deserialize`); o `GetOnlyCollectionModifier` dá às coleções get-only semântica *clear-then-add*
(fidelidade real, inclusive remoções). `ClientMaterializer` reconstrói comparers (CORS `OrdinalIgnoreCase`); há
guarda de cobertura de propriedades de `Client`. Migration inicial SQLite cria **apenas** as 7 tabelas
Configuration. **Conforme.**

### Fase 3 — Adapter, lifecycle e snapshot assíncrono
Snapshot core-owned com bootstrap/refresh assíncrono: `IConfigurationSnapshot` (leitura síncrona, cópias
defensivas), `IConfigurationSnapshotSource.LoadAsync`, holder com publish atômico, `IConfigurationSnapshotRefresher`
(`RefreshAsync` fail-closed; `TryRefreshAsync` last-known-good/DF26) e `ConfigurationSnapshotHostedService`
(carga inicial no `StartAsync` falha o startup; loop periódico com `TimeProvider`). Cópia defensiva viabilizada
pelo copy ctor de `ServerOptions` (+ `ServerUIOptions`). Os quatro consumidores síncronos foram migrados; o
`IRealmStore.GetByPath` **síncrono** foi removido (interface + fake). Invalidação de named options cobre o scheme
default + união dos schemes de realm, sem tocar schemes externos. `RealmManager` dispara reload após writes de
realm (DF7/DF28), o que fecha a lacuna de realms criados em runtime — validada por `RealmConfigurationSnapshotTests`.
**Conforme.**

### Fase 4 — ServerOptions, realms, clients e bridge de resources
Seam Configuration-only via `IConfigurationStoreFactory` (scoped, somente leitura) sem `IStorage`/`IStorageProvider`/
`IStorageSession`. `EntityFrameworkRealmStore`: lookups async/canceláveis sobre `LiveRealms()` (filtra
`DeletedAtUtc == null`), tombstone permanente em `DeleteAsync` (internal/ausente → `false`, sem apagar linha),
recusa de ressurreição, proteção de identidade de realm interno e rejeição de domain não canônico
(`EnsureCanonicalDomain`). `ConfigurationServerOptionsReader` é fail-closed e desserializa a cada leitura
(independência). `EntityFrameworkClientStore` é read-only, realm-bound, sempre filtra tombstone e aplica
`Enabled` somente em `FindEnabledClientByIdAsync`; ambos os lookups materializam raiz + satélites. Bridge de
resources (`DefaultConfigurationResourceSource`/`ConfigurationResourceBridgeOptions`)
é volátil, sem tabela e sem referência a `Storage.InMemory`; demo é opt-in por
`AddEntityFrameworkConfigurationDemoResources(realmId)`. **Conforme.**

### Fase 5 — Proteção e persistência de signing keys
`IKeyMaterialProtector` com `KeyMaterialEnvelope` v1 (identificador em coluna própria, payload prefixado por
versão, `ToString` **redige** o payload). `PlainKeyMaterialProtector` só entra por `AddPlainKeyMaterialProtector()`
e emite warning sem material; as três extensões fazem `RemoveAll<IKeyMaterialProtector>()` antes de adicionar, e
`KeyMaterialProtectorResolver.GetForWrite()` exige exatamente um — **Plain nunca é default**. As implementações
são selecionáveis no provisionamento; migração/reproteção de material já persistido entre protectors permanece
fora do Plano 2, junto ao futuro KMS. `AesKeyMaterialProtector`
valida chave 16/24/32 bytes, copia e zera a chave, usa nonce aleatório de 96 bits + tag de 128 bits, e falha
fechado em payload/tag adulterado (`AesGcm.Decrypt`). `EntityFrameworkKeyStore` é create-only pela PK
`(realm_id, key_id)`, realm-bound + tombstone-filtered, com KY-02/03 em bordas inclusivas e ordem por `Created`,
KY-04/05 lançando `ArgumentException`, validação de enums fail-closed e materialização independente.
`SigningKeyStartupValidator` (um `IHostedService` próprio, porque `IServerJob` isola falhas) percorre o caminho
completo de `IKeyManager.GetSigningCredentialsAsync` para cada realm habilitado e falha o `StartAsync` sem expor
material. `FirstKeyJob` saiu da composição. **Conforme (DF8-DF10/DF19/DF24/DF27).**

### Fase 6 — PostgreSQL, migrations, runner e seeds
`ConfigurationPostgreSqlDbContext` + factory aplicam a mesma extensão pública; a migration usa schema
`configuration`, `jsonb` e colação `C` nos identificadores (equivalente ao `BINARY`). **Ambos os providers
reportam zero model changes pendentes** (verificado por `has-pending-model-changes`). `RoyalIdentity.Migrations`
exige provider/conexão explícitos, aceita conexão direta ou por variável de ambiente e, para `product`/`all`,
ao menos um `--server-admin-redirect-uri` absoluto e repetível, com exit codes `0`
(sucesso/ajuda), `64` (uso inválido) e `1` (falha), sanitizando a mensagem. `ConfigurationSeed` é transacional e
idempotente (product/demo/all), não persiste standard scopes, e para cada realm habilitado **valida** as keys
correntes (desproteção + materialização + `CreateSigningCredentials`) antes de criar uma nova apenas quando falta
o algoritmo principal — coerente com o startup fail-fast. Scripts SQL versionados para os dois providers e
`scripts/Test-ConfigurationPostgreSql.ps1` (porta dinâmica ≠ 5432, limpeza em `finally`). **Conforme.**

### Fase 7 — Paridade, integração e fechamento
A base compartilhada do harness executa os mesmos cenários provider-neutral em SQLite e PostgreSQL (fixture PG
com database isolado por cenário e `DROP DATABASE ... WITH (FORCE)`). Model restrito às 7 entidades; nenhuma
tabela/store Operational; resources continuam voláteis. Buscas confirmam ausência de `EnsureCreated`/`Migrate`/
`MigrateAsync` no host/core; `HostServices` usa somente `AddInMemoryStorage`. **Conforme.**

---

## 3. Invariantes e decisões — verificação dirigida

| Item | Evidência | Resultado |
|---|---|---|
| DF11 — host/core não migram | grep `EnsureCreated`/`Migrate`/`MigrateAsync` em `RoyalIdentity`/`.Server`/adapter: **vazio**; ocorrências só em `RoyalIdentity.Migrations` + `Tests.Storage` | OK |
| DF20 — sem `IStorage`/`IStorageProvider`/`IStorageSession` parcial | adapter só registra `IConfigurationStoreFactory`/`IRealmStore`/snapshot source; guard `ConfigurationStorageRegistrationTests` | OK |
| Invariante 16 — host in-memory | `HostServices.cs` → apenas `AddInMemoryStorage()` | OK |
| DF22 — tombstone reserva path/domain | uniques sem filtro; `DeleteAsync` grava `DeletedAtUtc`, mantém linha; internal/ausente → `false` | OK |
| DF23 — domain lowercase | `RealmManager` normaliza (`ToLowerInvariant`) antes de consulta/escrita; EF store rejeita não canônico | OK |
| DF24 — keys create-only | PK `(realm_id, key_id)` via `db.Add`; sem sobrescrita | OK |
| DF25 — materialização independente | readers desserializam por chamada; cópias defensivas de `ServerOptions`/`Realm` | OK |
| DF10 — Plain nunca default | `RemoveAll` + `GetForWrite` exige exatamente 1; guard de arquitetura | OK |
| DF27 — startup falha sem key utilizável | `SigningKeyStartupValidator` percorre `GetSigningCredentialsAsync` e falha `StartAsync` | OK |
| Invariante 10 — nada sensível em log/erro | `KeyMaterialEnvelope.ToString` redige; grep de logging sensível **vazio**; runner sanitiza | OK |
| DF18 — schemas/colação | PostgreSQL `configuration`/`jsonb`/`C`; SQLite sem schema/`TEXT`/`BINARY` | OK |
| Paridade migration↔model | `has-pending-model-changes`: sem mudanças em SQLite e PostgreSQL | OK |

---

## 4. Pontos fortes

- **Segurança de key material sólida:** AES-GCM autenticado com nonce por escrita, zeragem de buffers, tamper
  fail-closed; envelope versionado e redigido; seleção explícita de protector (nunca implícita); validação de
  usabilidade real no startup e no seed (não apenas presença de id).
- **Fronteiras preservadas com testes que as fixam:** pureza do `Data.Configuration`, ausência de gateway
  parcial e equivalência de contexts são asseguradas por `Tests.Architecture`, não só por convenção.
- **Snapshot correto e defensivo:** publish atômico, cópias defensivas do grafo inteiro (inclusive
  `DiscoveryOptions.CustomEntries`), last-known-good observável, invalidação cirúrgica de named options e reload
  disparado por writes — sem I/O síncrono e sem ciclo de bootstrap.
- **Fidelidade de payload não trivial resolvida:** o `GetOnlyCollectionModifier` evita a perda silenciosa de
  coleções get-only (risco real com `System.Text.Json`), preservando remoções e comparers.
- **Operação desacoplada do host:** runner dedicado com exit codes e sanitização, seeds idempotentes/
  transacionais, SQL revisável versionado e ensaio real contra PostgreSQL 17.

---

## 5. Aderência ao processo do plano

O plano foi mantido como registro vivo: status `CONCLUÍDO 7/7`, tarefas marcadas, "Resultado da Fase" preenchido
em todas com contagens de teste e comandos. As contagens originais conferiam antes do ajuste pós-revisão; a
execução atual possui dois testes adicionais no projeto Storage (808/9). Os
riscos endereçados foram promovidos a *Mitigado*. O handoff aponta corretamente os planos sucessores
(`plan-data-operational-storage`, `plan-data-test-migration`, administrativo/KMS).

---

## 6. Observações (menores, sem ação obrigatória)

1. **Leitura repetida de `ServerOptions`:** `ConfigurationServerOptionsReader.ReadAsync` desserializa o singleton
   a cada chamada. Em um fluxo que materialize vários realms/clients no mesmo escopo, isso repete leitura +
   desserialização. É *caminho frio* (Configuration); uma memoização por escopo pode ser otimização futura sem
   violar DF25, desde que cada materialização continue recebendo uma cópia independente.
2. **Exceção de duplicidade de key é do EF:** `AddKeyAsync` confia na PK (correto e livre de corrida), mas a
   duplicata emerge como `DbUpdateException`, com causa/detalhes internos dependentes do provider, não uma
   exceção de domínio tipada. Atende
   "falha visivelmente" (DF24); como é write legado que o host não chama, é aceitável. Uma exceção tipada seria
   um polimento se esses writes sobreviverem à camada administrativa.
3. **Simetria fake↔EF na normalização de domain:** a rejeição de domain não canônico vive no `EntityFrameworkRealmStore`;
   o `RealmStore` in-memory não a replica. Como a normalização ocorre na borda (`RealmManager`) e o fake é
   transitório (ADR-018), não se amplia sua paridade; ainda assim, consumidores diretos do fake podem observar
   a diferença e os callers atuais devem continuar normalizando na borda.
4. **Resolvido após a revisão — URLs no seed de produto:** `server_admin` não possui mais redirects localhost
   embutidos. `product`/`all` exigem ao menos um `--server-admin-redirect-uri` absoluto, repetível e escolhido pelo
   operador; a validação ocorre antes de abrir ou migrar o banco. No runner, URLs localhost permanecem apenas
   no demo.
5. **Concorrência de writes legados:** `SaveAsync`/`AddKeyAsync` não têm token de concorrência otimista. É
   **decisão explícita** (DF17: concorrência fica na API administrativa); registro aqui apenas para rastreio.
6. **Troca de protector em base existente:** `Plain`, AES e Data Protection são opções substituíveis no
   provisionamento, mas o runner usa um único protector e valida com ele todas as keys correntes. Trocar o
   protector de uma base populada exige reproteção/migração própria; essa capacidade pertence ao plano de KMS.

Nenhuma dessas observações bloqueia o fechamento do Plano 2.

---

## 7. Validação executada

- `dotnet build RoyalIdentity.sln --no-restore --no-incremental` — êxito (0 erros; 43 warnings, nenhum emitido
  pelos arquivos alterados no ajuste pós-revisão).
- `dotnet test RoyalIdentity.sln --no-build --no-restore` — **808 aprovados, 0 falhas, 9 ignorados** (8 PostgreSQL
  Configuration opt-in + 1 UserAccounts PostgreSQL opt-in). Por projeto: Pipelines 3, Identity 13, Architecture
  36, Security 116, UserAccounts 194 (+1 skip), Storage 219 (+8 skip), Integration 227.
- `dotnet ef migrations has-pending-model-changes` (SQLite e PostgreSQL) — sem mudanças pendentes.
- Checagens dirigidas: sem `EnsureCreated`/`Migrate`/`MigrateAsync` no host/core/adapter; sem logging de key
  material/segredo/connection string; host apenas `AddInMemoryStorage`; PostgreSQL com schema/`jsonb`/collation
  `C`.
- `scripts/Test-ConfigurationPostgreSql.ps1` — **8 aprovados, 0 falhas**, contra PostgreSQL 17 real na porta
  dinâmica 33135; o container efêmero foi removido no `finally`. Como `pwsh` não estava no `PATH`, o mesmo script
  foi invocado diretamente pela sessão PowerShell atual.

---

## 8. Conclusão e gate para o Plano 3

O Plano 2 pode ser considerado **fechado com confiança**. A persistência de Configuration (ServerOptions, realms/
options, clients, signing keys) existe em SQLite e PostgreSQL com paridade validada, protectors de key material
selecionáveis no provisionamento, snapshot assíncrono defensivo e caminho operacional (runner/SQL/seed)
separado do host — sem
promover qualquer gateway EF parcial a produção e sem alterar o backing padrão in-memory.

O que permanece deliberadamente aberto, como gate para as próximas etapas:

- **Operational Storage** (tokens, codes, consents, sessões, authorize parameters) — `plan-data-operational-storage.md`
  (ainda não criado); só então o `IStorage`/`IStorageProvider` EF completo pode ser composto e registrado.
- **Migração do backing padrão** de testes/host — `plan-data-test-migration.md`.
- **Escrita administrativa e concorrência** de Configuration — plano administrativo próprio (DF13/DF17/DF28), que
  também remove os writes legados de `IRealmStore`/`IKeyStore` do contrato do core.
- **Resources/scopes persistentes** — bloqueado por DF22 do baseline até o redesign; hoje na bridge volátil.
- **KMS/rotação** de keys — ADR/plano próprio.
