# Plan: Composição persistente do host e migração dos testes (`plan-data-test-migration`)

## Status: RASCUNHO - inventário verificado em 2026-07-26; Q1-Q10 abertas; implementação não iniciada

## Progresso

`░░░░░░░░░` **0%** - 0 de 9 fases

| Fase | Estado |
|---|---|
| Fase 1 - contrato de configuração e composição | Pendente |
| Fase 2 - provisionamento externo das três famílias | Pendente |
| Fase 3 - composição real e fail-fast do Server | Pendente |
| Fase 4 - fixture SQLite unificada, handles e seeds | Pendente |
| Fase 5 - migração de login, profile, authorize e token | Pendente |
| Fase 6 - migração dos fluxos restantes e troca do default | Pendente |
| Fase 7 - desacoplamento dos contratos de teste do fake | Pendente |
| Fase 8 - remoção da transição e exclusão do fake | Pendente |
| Fase 9 - PostgreSQL, regressão final e fechamento documental | Pendente |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluída, `%` e `X de N`). Exemplo de barra: `████░░░░░`.
> Antes de fechar uma fase, confirme que decisões, critérios de aceite, testes e invariantes relacionados foram
> aplicados.

> **Gate de planejamento:** nenhuma fase que dependa de Q1-Q10 pode ser iniciada enquanto a pergunta correspondente
> estiver aberta. Respostas humanas devem ser convertidas em novas decisões `DF<n>` e registradas em
> `Histórico de decisões` antes do primeiro edit da fase.

---

## Contexto

### Fontes verificadas

- [template-ai-implementation-plan.md](../references/template-plan/template-ai-implementation-plan.md) — shape e
  regras de manutenção deste plano.
- [plans-roadmap-02.md](plans-roadmap-02.md) — identifica o Plano 4 como o próximo plano e inclui a troca do backing
  padrão do host/testes e a remoção do fallback junto do fake.
- [plan-data-macro.md](plan-data-macro.md) — define a migração por grupos para EF/SQLite + `UserAccounts`.
- [plan-data-storage-matrix.md](plan-data-storage-matrix.md) — contém as semânticas normativas, os destinos dos
  acessos diretos ao fake e o gate para a troca do backing.
- [plan-data-configuration-storage.md](plan-data-configuration-storage.md) — entrega Configuration EF, snapshot,
  protectors, migrations e seed externo.
- [plan-data-operational-storage.md](plan-data-operational-storage.md) — entrega Operational EF, cleanup, gateway
  completo e sua decisão 39, que limita o fallback ao período em que o default ainda é in-memory.
- [plan-users-accounts-sqlite-hardening.md](plan-users-accounts-sqlite-hardening.md) — entrega migrations dos
  providers de `UserAccounts`, concorrência real e o seed test-only reutilizável.
- [ADR-013](../../adrs/ADR-013.md) — fixa storages como facades, `Data.*` puro e adapters separados.
- [ADR-014](../../adrs/ADR-014.md), [ADR-015](../../adrs/ADR-015.md) e
  [ADR-017](../../adrs/ADR-017.md) — fixam a borda core-owned de usuários/sessão, a família independente
  `UserAccounts` e o ciclo de segurança.
- [ADR-018](../../adrs/ADR-018.md) — torna o fake transitório, proíbe ampliar sua paridade e determina sua remoção
  quando a migração ocorrer.
- [product.md](../foundation/product.md), [tech.md](../foundation/tech.md),
  [structure.md](../foundation/structure.md) e [architecture.md](../foundation/architecture.md) — objetivos,
  invariantes, dependências e fronteiras a preservar; parte do texto ainda descreve o default in-memory e deve ser
  atualizada no fechamento.
- `RoyalIdentity.Server/HostServices.cs`, `RoyalIdentity.Server/Program.cs`,
  `RoyalIdentity.Server/RoyalIdentity.Server.csproj` e `RoyalIdentity.Server/appsettings.json` — composição atual do
  host oficial.
- `RoyalIdentity.Storage.EntityFramework/Extensions/*.cs` — extensões prontas para Configuration, snapshot,
  Operational, cleanup, proteção e gateway `IStorage`.
- `RoyalIdentity.Migrations/*` — runner externo atual para Configuration + Operational e seeds de produto/demo.
- `RoyalIdentity.UserAccounts.Integration/*`, `RoyalIdentity.UserAccounts.Sqlite/*` e
  `RoyalIdentity.UserAccounts.PostgreSql/*` — adapter e providers reais da borda de contas.
- `Tests.Integration/Prepare/AppFactory.cs`, `EntityFrameworkStorageAppFactory.cs`,
  `UserAccountsAppFactory.cs` e `CharacterizationSeed.cs` — default fake e duas composições opt-in parciais.
- `Tests.Storage/Storage/Support/InMemoryStorageHarness.cs`,
  `Tests.UserAccounts/UserDirectoryContractTests.cs` e
  `Tests.Architecture/ModuleBoundaryTests.cs` — consumidores restantes do projeto fake fora da suíte HTTP.
- `RoyalIdentity/Contracts/Defaults/DefaultAuthorizationCodeConsumer.cs`,
  `DefaultRefreshTokenConsumer.cs`, `ISingleUseAuthorizationCodeStore.cs` e
  `IVersionedRefreshTokenStore.cs` — capability detection e fallbacks transitórios.

### Estado atual do código (verificado em 2026-07-26)

- **O host oficial ainda é in-memory:** `RoyalIdentity.Server.HostServices.AddHostServices()` chama
  `AddInMemoryStorage()`, e o projeto referencia apenas Razor, core e `RoyalIdentity.Storage.InMemory`.
- **O host não possui contrato de persistência configurável:** `Program` não passa configuração/ambiente para
  `AddHostServices`, e `appsettings.json` não contém provider, conexões, snapshot, cleanup ou proteção.
- **O gateway EF do core está completo:** `AddEntityFrameworkStorage()` compõe Configuration + Operational e recusa
  uma composição sem `AddEntityFrameworkOperationalCleanup(...)` explícito.
- **Configuration possui bootstrap fail-closed parcial:** o snapshot inicial, `server_options` e signing keys
  utilizáveis já são validados antes do tráfego; Operational e `UserAccounts` ainda não possuem verificação de
  schema/readiness equivalente no host.
- **O provisionamento do core é externo:** `RoyalIdentity.Migrations` migra Configuration e Operational,
  sequencialmente e sem transação conjunta; o Server não referencia o runner.
- **`UserAccounts` está pronto para runtime:** há registro SQLite de arquivo, SQLite in-memory test-only,
  PostgreSQL e `AddUserAccountsForRoyalIdentity()`. Seus providers possuem migrations, mas não há comando
  operacional integrado ao runner do core.
- **As factories opt-in são complementares, não cumulativas:** `EntityFrameworkStorageAppFactory` torna o core real
  e mantém contas fake; `UserAccountsAppFactory` torna contas reais e mantém o core fake.
- **O default HTTP continua fake:** 29 classes usam `IClassFixture<AppFactory>`, e `AppFactory` herda
  `AddInMemoryStorage()` de `Tests.Host`.
- **A suíte HTTP conhece detalhes do fake:** `rg -o` encontrou 381 ocorrências de `MemoryStorage` em 36 arquivos,
  64 ocorrências de getters de `RealmMemoryStore`, 265 usos de `MemoryStorage.DemoRealm` e 28 usos do subject
  estático de Alice.
- **Há mutações não portáveis nos testes:** `CharacterizationSeed` e testes de refresh/signing alteram live
  references de contas, varrem sessões por subject, limpam tokens diretamente e escrevem clients/resources nos
  dictionaries do fake.
- **O seed compartilhado ainda importa o fake:** `Tests.UserAccounts/UserAccountsModuleSeed.cs` obtém os subjects
  determinísticos de Alice/Bob em `MemoryStorage`.
- **`Tests.Storage` ainda executa contratos contra o fake:** existem 11 especializações `InMemory`; o contrato de
  `IStorageSession` ainda não tem twin EF, e há composições parciais Configuration EF + Operational fake.
- **`Tests.UserAccounts` ainda protege paridade com o fake:** `UserDirectoryContractTests` possui uma especialização
  `InMemory`, enquanto até a variante SQLite usa realms estáticos do fake.
- **Atomicidade ainda é capability opcional:** os consumers fazem cast em runtime; na ausência da capability,
  authorization code usa get-then-remove e refresh token usa `UpdateAsync` não condicional.
- **O worktree estava limpo no início do inventário:** nenhuma alteração de código ou teste foi executada para criar
  este rascunho.

### Lacunas, conflitos e restrições

- **Não existe composição integral de referência:** nenhum host combina Configuration EF + Operational EF +
  `UserAccounts` real.
- **O Server não pode apenas trocar uma chamada de DI:** faltam contrato de configuração, proteção compatível com o
  provisionamento, cleanup explícito, rota de migration de `UserAccounts` e comportamento de startup inválido.
- **O macro-plano contém uma alternativa superada:** ele admite manter `MemoryStorage` em testes específicos “se
  ainda houver valor”; ADR-018 e o roadmap atualizado determinam exclusão integral no corte final.
- **A retirada contratual precisa ser coordenada:** tornar atomicidade obrigatória antes de retirar o fake exigiria
  implementar capabilities novas no próprio fake, contrariando ADR-018.
- **Resources/scopes continuam voláteis:** testes devem usar a bridge/hook test-only existente; este plano não pode
  inventar tabelas ou uma write facade pública para contornar setup.
- **Writes de Configuration não atualizam o snapshot sozinhos:** helpers que persistirem clients precisam publicar
  refresh explícito antes de emitir requests.
- **Seeds têm owners distintos:** seed de produto/demo de Configuration é operacional; Alice/Bob são seed test-only
  de `UserAccounts`; código de produção não pode referenciar `Tests.*`.
- **Não existe dado durável a migrar do fake:** o rollout troca composição e provisiona bancos vazios/externos; não
  há exportação de dictionaries in-memory.
- **Documentos de fundação estão defasados:** ainda descrevem o fake como implementação/default de referência e
  precisam ser corrigidos somente após o corte real.

### Superfícies impactadas a mapear

- `RoyalIdentity.Server` — configuração pública do host, dependências de providers, Data Protection e startup.
- `RoyalIdentity.Migrations` e/ou runner próprio de `UserAccounts` — migrations, seeds e resultado operacional por
  família.
- `RoyalIdentity.Storage.EntityFramework*` — registro dos contexts/providers, gateway, protectors e readiness.
- `RoyalIdentity.UserAccounts.*` — provider selecionado, options por realm, migration externa e adapter.
- `RoyalIdentity/Contracts/Storage` e `RoyalIdentity/Contracts/Defaults` — shape final dos contratos atômicos.
- `Tests.Host` e `Tests.Integration` — composição HTTP default, handles, seeds e cenários por grupo.
- `Tests.Storage` — harnesses EF, `IStorageSession`, shape dos contratos e remoção das variantes fake.
- `Tests.UserAccounts` — seed determinístico e remoção da especialização in-memory.
- `Tests.Architecture` — novo grafo permitido para Server e guard contra reintrodução do fake.
- `RoyalIdentity.Storage.InMemory` e `RoyalIdentity.sln` — exclusão coordenada do projeto.
- `.ai/foundation`, `.ai/plans`, `adrs/ADR-018.md`, `.ai/backlogs/backlog-001.md` e
  `RoyalIdentity.Migrations/README.md` — estado arquitetural e instruções operacionais finais.

---

## Objetivo

1. Compor `RoyalIdentity.Server` sobre Configuration + Operational EF e `UserAccounts` real, sem fallback
   in-memory e sem migration/seed dentro do processo web.
2. Entregar provisionamento externo e validação fail-fast suficientes para iniciar o host com as três famílias
   configuradas de forma consistente.
3. Tornar uma composição SQLite/EF + `UserAccounts` o default de `Tests.Integration`, com handles e seeds neutros ao
   provider.
4. Remover os caminhos não atômicos, as capabilities opcionais conforme a decisão Q9 e todo o projeto
   `RoyalIdentity.Storage.InMemory`.
5. Preservar as semânticas fechadas na matriz, a paridade PostgreSQL exigida por Q10 e a suíte completa verde.

## Fora de escopo

- Persistir ou redesenhar resources/scopes — destino: plano específico após a decisão 22 do baseline.
- Alterar semânticas de stores fechadas em `plan-data-storage-matrix.md`.
- Adicionar cache aos stores EF — destino: `plan-data-caching.md`.
- Implementar auditoria durável, outbox ou inbox — destino: `plan-data-audit-outbox.md`.
- Criar API/UI administrativa, write model geral ou coordenação cross-family de exclusão de realm.
- Criar/rotacionar signing keys em runtime ou reproteger material existente — destino: plano de KMS.
- Introduzir transação distribuída entre Configuration, Operational e `UserAccounts`.
- Persistir options de `UserAccounts` por realm, caso Q7 preserve temporariamente o resolver atual.
- Implementar Aspire/deployment orchestration além do contrato executável de provisionamento — destino:
  `.ai/backlogs/backlog-001.md`.
- Migrar estado do fake para banco; o fake não é uma fonte durável.

---

## Perguntas ao humano

- **Q1 — Seleção de provider no host oficial:** como o Server deve selecionar os providers das três famílias?
  - **Opções:**
    - **A)** Uma opção runtime `Sqlite|PostgreSql` aplicada a Configuration, Operational e `UserAccounts`.
    - **B)** Uma opção runtime compartilhada por Configuration/Operational e outra independente para
      `UserAccounts`.
    - **C)** Uma opção runtime independente para cada família, ampliando a limitação atual do runner.
  - **Impacto se não decidir:** bloqueia referências de projeto, binding de options e matriz de startup.
  - **Status:** Aberta.

- **Q2 — Contrato das conexões:** como a configuração representa bancos que podem coincidir fisicamente?
  - **Opções:**
    - **A)** Três conexões sempre explícitas: Configuration, Operational e `UserAccounts`.
    - **B)** Uma conexão default obrigatória com overrides opcionais por família.
  - **Impacto se não decidir:** bloqueia o contrato público de configuração e sua validação.
  - **Status:** Aberta.

- **Q3 — Proteção oficial do primeiro host persistente:** quais opções independentes o Server registra para cada
  domínio?
  - **Eixos independentes; responder uma opção em cada eixo:**
    - **Signing keys — K-A)** Data Protection como protector oficial; Plain somente em development opt-in; AES fica
      para hosts customizados.
    - **Signing keys — K-B)** Seletor explícito Data Protection/AES/Plain; sem default silencioso e com Plain restrito
      a development.
    - **Operational — O-A)** Registrar somente um profile Data Protection como `default`.
    - **Operational — O-B)** Registrar por configuração um catálogo versionado de profiles Data Protection/AES/Plain,
      mantendo Plain explicitamente inseguro.
    - **ASP.NET Data Protection — D-A)** Compartilhar o provider/key ring configurado entre cookies, mensagens e os
      protectors storage que selecionarem Data Protection.
    - **ASP.NET Data Protection — D-B)** Separar providers/key rings de runtime web e storage, com nomes/purposes
      explícitos e provisionamento correspondente.
  - **Impacto se não decidir:** bloqueia compatibilidade runner-host para signing keys, catálogo Operational e
    lifecycle de cookies/mensagens.
  - **Status:** Aberta.

- **Q4 — Provisionamento de `UserAccounts`:** qual é a superfície operacional de migrations da terceira família?
  - **Opções:**
    - **A)** Estender `RoyalIdentity.Migrations` com `UserAccounts` como terceira família independente e relatório
      próprio.
    - **B)** Criar um runner/comando separado pertencente à família `UserAccounts`, documentando a ordem entre os
      dois executáveis.
  - **Impacto se não decidir:** o Server real não possui uma rota suportada para preparar o schema de contas.
  - **Status:** Aberta.

- **Q5 — Readiness de schema no startup:** até onde o host deve validar sem aplicar migrations?
  - **Opções:**
    - **A)** Falhar o startup quando houver migration pendente ou schema/conectividade inválidos em qualquer uma das
      três famílias.
    - **B)** Validar somente conectividade e operações mínimas exigidas pelo bootstrap, permitindo migrations
      pendentes compatíveis.
    - **C)** Confiar exclusivamente no provisionamento externo; manter apenas as validações Configuration/signing
      já existentes.
  - **Impacto se não decidir:** bloqueia o comportamento de falha e os testes negativos do host.
  - **Status:** Aberta.

- **Q6 — Experiência local/demo após retirar o fake:** o Server oficial deve manter um perfil demo funcional?
  - **Opções:**
    - **A)** Manter profile demo opt-in, provisionado externamente com Configuration demo, resource bridge e contas
      demo próprias da composição, sem depender de `Tests.*`.
    - **B)** Remover o demo do Server oficial; exigir configuração/provisionamento do operador e conservar Alice/Bob
      somente nos testes.
  - **Impacto se não decidir:** bloqueia appsettings de development, seed de contas e documentação de `dotnet run`.
  - **Status:** Aberta.

- **Q7 — Options por realm de `UserAccounts` neste corte:** qual fonte o host usa?
  - **Opções:**
    - **A)** Aceitar temporariamente `DefaultUserAccountsRealmOptionsResolver` e diferir configuração/persistência
      por realm.
    - **B)** Entregar neste plano uma fonte configurável por realm, sem criar dependência do módulo puro no core.
  - **Impacto se não decidir:** bloqueia o fechamento de escopo/configuração; o resolver atual permite o registro
    técnico, mas não policies distintas por realm.
  - **Status:** Aberta.

- **Q8 — Fonte única da composição usada pelos testes HTTP:** como evitar drift entre Server e `Tests.Host`?
  - **Opções:**
    - **A)** Manter `Tests.Host`, mas fazê-lo consumir o mesmo entry point reutilizável de persistência do Server e
      acrescentar apenas endpoints/componentes de teste.
    - **B)** Basear `WebApplicationFactory` diretamente em `RoyalIdentity.Server.Program` e acrescentar as
      superfícies test-only pela factory.
  - **Impacto se não decidir:** bloqueia a factory unificada e o novo grafo de referências.
  - **Status:** Aberta.

- **Q9 — Shape final dos contratos atômicos:** como eliminar as capabilities opcionais?
  - **Opções:**
    - **A)** Incorporar consumo single-use e transição versionada nos contratos base, remover as interfaces de
      capability redundantes e remover `IRefreshTokenStore.UpdateAsync`.
    - **B)** Manter interfaces de capability separadas, mas alterar/adicionar accessors realm-bound em `IStorage`
      para retorná-las obrigatoriamente; os consumers usam esses accessors sem cast, detecção ou fallback, e
      `UpdateAsync` legado é removido.
  - **Impacto se não decidir:** bloqueia a quebra pública coordenada e a exclusão do fake.
  - **Status:** Aberta.

- **Q10 — Gate PostgreSQL e CI:** qual evidência é obrigatória antes de excluir o fake?
  - **Opções:**
    - **A)** Exigir um fluxo OIDC completo opt-in sobre PostgreSQL 17 real no fechamento, sem torná-lo job default.
    - **B)** Tornar o fluxo OIDC completo sobre PostgreSQL um job obrigatório de CI.
    - **C)** Manter os contratos/concorrência PostgreSQL já existentes e exigir apenas provisionamento + startup
      smoke do Server real.
  - **Impacto se não decidir:** bloqueia os critérios finais, o custo de CI e a definição de paridade do host.
  - **Status:** Aberta.

---

## Decisões fechadas

- **DF1 — Escopo do Plano 4:** este plano troca o backing padrão do Server e dos testes, remove o fallback
  transitório e aposenta o fake; não redesenha stores. Fonte: roadmap 02, macro-plano e Plano 3.
- **DF2 — Matriz normativa:** ownership, realm scope, comparadores, duplicidade, ausência, expiração, atomicidade e
  ordem dos stores vêm de `plan-data-storage-matrix.md` e não são reinferidos. Fonte: baseline concluído.
- **DF3 — Fronteira de persistência:** Configuration + Operational entram no core somente por
  `RoyalIdentity.Storage.EntityFramework`; `UserAccounts` mantém persistence própria e cruza a borda somente por
  `.Integration`. Fonte: ADR-013 e ADR-015.
- **DF4 — Gateway produtivo completo:** o Server usa Configuration + Operational completos por
  `AddEntityFrameworkStorage()`; não existe composição produtiva parcial do `IStorage`. Fonte: decisões 21/22 do
  Plano 3.
- **DF5 — Alvo default dos testes:** SQLite/EF para o core + `UserAccounts` SQLite é o backing único de regressão;
  seeds e handles são test-only e neutros ao fake. Fonte: ADR-018 e macro-plano.
- **DF6 — Exclusão integral do fake:** `RoyalIdentity.Storage.InMemory`, suas facades, variantes contratuais e
  referências de projeto são removidos; não permanece uma suíte específica do fake. Fonte: ADR-018 e roadmap 02,
  posteriores à alternativa condicional do macro-plano.
- **DF7 — Timing do fallback:** capability detection e caminhos não atômicos só desaparecem depois que o default
  real estiver verde; o fake não recebe CAS, locks ou feature parity para viabilizar a remoção. Fonte: decisão 39
  do Plano 3 e ADR-018.
- **DF8 — Provisionamento fora do host:** o processo web nunca chama `EnsureCreated`, `Migrate`, `MigrateAsync` ou
  seed. Fonte: decisão 23 do Plano 3.
- **DF9 — Independência das famílias:** Configuration, Operational e `UserAccounts` preservam contexts, conexões,
  migrations e ownership independentes mesmo quando compartilham banco físico; não há transação global. Fonte:
  ADR-013 e Planos 0/2/3.
- **DF10 — Cleanup explícito:** toda composição EF completa escolhe `Hosted` ou `External`; ausência ou duplicidade
  falha. Não há default silencioso. Fonte: decisão 17 do Plano 3 e implementação atual.
- **DF11 — Proteção fail-closed:** Plain exige registro + seleção explícitos e warning; profile/protector ausente
  falha, sem fallback; segredos não entram em Configuration persistida. Fonte: Planos 2/3.
- **DF12 — Signing keys são externas ao host:** provisionamento cria material utilizável; o Server valida e usa,
  mas não cria nem rotaciona keys. Fonte: decisões 19/27/28 do Plano 2.
- **DF13 — Resources/scopes permanecem bridge:** o Plano 4 usa `IConfigurationResourceSource`/hook de composição e
  não adiciona persistência ou write facade pública. Fonte: decisão 22 do baseline e Plano 2.
- **DF14 — Seeds separados por owner:** produto/demo de Configuration pertencem ao provisionamento; Alice/Bob e
  dados de cenário pertencem às fixtures de `UserAccounts`; código produtivo não referencia `Tests.*`. Fonte:
  matriz, ADR-018 e Planos 0/2.
- **DF15 — Server não referencia o runner:** `RoyalIdentity.Server` pode referenciar adapters/providers
  selecionados, mas nunca `RoyalIdentity.Migrations` nem projetos `Data.*` diretamente. Fonte: architecture.md,
  ADR-013 e decisão 23 do Plano 3.
- **DF16 — Cobertura provider real prévia:** contratos, migrations, concorrência e gateway EF já estão verdes em
  SQLite e possuem aceites PostgreSQL opt-in; este plano migra consumidores e composição, sem repetir o design dos
  providers. Fonte: encerramento dos Planos 2/3.

---

## Histórico de decisões

**Pré-plano (direção do fake):**

- **Alternativa anterior — manter fake em testes específicos:** o macro-plano admitia essa possibilidade
  condicional.
  - **SUPERSEDED por ADR-018 e roadmap 02:** o estado final remove `RoyalIdentity.Storage.InMemory` e o lado fake dos
    contract tests; somente doubles locais, focados e não registrados como backing do produto podem permanecer.

**Pré-plano (papel de referência da borda):**

- **Alternativa anterior — fake como referência durável:** ADR-015/ADR-017 ainda descrevem paridade fake × módulo.
  - **SUPERSEDED por ADR-018:** a referência executável passa a ser `UserAccounts` SQLite e não se adiciona
    comportamento novo ao fake.

---

## Design alvo

### Contratos e bordas

- `RoyalIdentity.Server`: possui o binding/validation da configuração do host e registra adapters/providers
  concretos conforme Q1-Q7; não contém acesso direto a entidades `Data.*`.
- `IStorage`/`IStorageProvider`: são fornecidos exclusivamente pelo gateway EF completo; exatamente uma composição
  fica resolvível.
- `IUserDirectory` e portas realm-bound de conta: são fornecidos por
  `RoyalIdentity.UserAccounts.Integration`; o core continua sem referência ao módulo.
- Runner(s) de migration: recebem provider e conexão por família, aplicam migrations explicitamente e retornam
  resultado independente por família; a forma executável depende de Q4.
- Readiness do host: é somente leitura e nunca aplica schema/seed; a profundidade depende de Q5.
- Configuração de cleanup: seleciona exatamente um modo `Hosted|External`, sem default.
- Proteção de signing keys e payload Operational: usa profiles registrados e material externo compatível com o
  provisionamento; o catálogo oficial depende de Q3.
- Contrato atômico de authorization code/refresh token: torna o caminho seguro uma dependência de compilação,
  conforme Q9; nenhum consumer detecta capability opcional.
- Fixture HTTP: expõe handles imutáveis de realm/client/resource/subject/session e operações test-only de setup,
  nunca `MemoryStorage`, dictionaries ou live references.
- `IConfigurationSnapshotRefresher`: é chamado pela fixture depois de writes de Configuration e antes do request
  que consome os dados.
- `IConfigurationResourceSource`: continua sendo a rota volátil explícita de resources/scopes em host/fixtures.

### Modelo, dados e persistência

Este plano não cria um quarto modelo nem altera semânticas relacionais. A composição final preserva:

```text
ConfigurationDbContext
  server_options, realms, clients, signing_keys
  migration history exclusiva da família Configuration

OperationalDbContext
  access/refresh tokens, authorization codes, consents, sessions, authorize parameters
  migration history exclusiva da família Operational

UserAccountsDbContext
  accounts, credentials, claims/properties, action tokens e estado de segurança
  migrations e history pertencentes ao provider UserAccounts

IConfigurationResourceSource
  resources/scopes voláteis; fora dos três schemas neste plano
```

Quando as três conexões apontarem para o mesmo banco, nomes de tabelas/history e mappings devem continuar sem
colisão. Cada migration reporta sucesso/falha próprio; falha intermediária não é apresentada como rollback conjunto.

### Arquitetura alvo

```text
RoyalIdentity.Server/
  binding, validação, composição web e startup
  -> RoyalIdentity.Storage.EntityFramework
  -> provider EF Sqlite/PostgreSql selecionado
  -> RoyalIdentity.UserAccounts.Integration
  -> provider UserAccounts Sqlite/PostgreSql selecionado
  -X-> RoyalIdentity.Migrations
  -X-> RoyalIdentity.Storage.InMemory
  -X-> RoyalIdentity.Data.*

RoyalIdentity.Migrations/ ou runner UserAccounts decidido em Q4
  aplica schemas e seeds fora do processo web
  preserva ownership e resultado por família

Tests.Host + Tests.Integration/
  reutilizam a composição definida em Q8
  usam SQLite isolado, handles e seeds test-only
  -X-> MemoryStorage/RealmMemoryStore

RoyalIdentity/
  mantém contratos e consumers core-owned
  -X-> providers, Server, UserAccounts ou fake
```

### Segurança, concorrência e confiabilidade

- Toda consulta/mutação permanece realm-scoped; handles de teste carregam `realmId` explicitamente.
- Authorization code continua single-use e refresh token continua usando transição condicional + tolerância
  pós-consumo definida; remover o fallback não altera essas semânticas.
- O host falha fechado para configuração, protector/profile ou signing key inválidos; mensagens não exibem
  connection strings, keys, bearer tokens ou payloads.
- Key ring/application name do host e do provisionamento precisam ser compatíveis quando Data Protection for
  selecionado.
- Nenhuma secret é persistida no payload Configuration nem incluída em argumento de teste/log quando uma variável
  de ambiente pode ser usada.
- Setup test-only não usa variável de ambiente global compartilhada entre fixtures paralelas; cada fixture possui
  material/arquivo isolado ou provider em memória com lifetime próprio.
- Writes de Configuration publicam snapshot antes do request; setup de cenário é serializado quando SQLite não
  suportar concorrência segura.
- Cleanup é explícito e idempotente; modo `External` não dispensa readiness do schema conforme a resposta Q5.
- `UserAccounts` conserva optimistic concurrency/retry e action-token conditional update; o plano não bypassa seus
  casos de uso com mutação de entidades vivas.
- O processo web não escreve schema nem seed, inclusive em development.

### Compatibilidade, migração e rollout

- Primeiro entregar provisionamento e composição real do Server; depois criar a fixture conjunta e migrar os grupos
  de testes.
- Manter o fake apenas como suporte temporário durante Fases 1-7, sem adicionar capabilities ou comportamento.
- Trocar `AppFactory` para a composição real somente após todos os grupos HTTP estarem verdes nessa factory.
- Preparar `Tests.Storage`, `Tests.UserAccounts` e guards arquiteturais para viver sem o fake antes da quebra pública.
- Aplicar Q9, remover fallbacks e excluir o projeto fake no mesmo corte compilável da Fase 8.
- Não há dual-write, import/export de dictionaries nem compatibilidade de dados com processos in-memory anteriores.
- Hosts com banco existente devem executar o(s) runner(s) antes do novo binário; o Server nunca corrige schema.
- O fechamento PostgreSQL/CI segue Q10 e ocorre antes de marcar o plano como concluído.

---

## Ordem de execução

1. **Fase 1 (contrato de configuração e composição)** — fecha Q1-Q3/Q5-Q8 e cria a superfície validada que as
   demais fases consomem.
2. **Fase 2 (provisionamento externo)** — prepara os três schemas e seeds sem permitir migration no processo web.
3. **Fase 3 (Server real)** — troca o host oficial somente depois de existir configuração e provisionamento.
4. **Fase 4 (fixture SQLite unificada)** — reproduz a composição integral com lifetime e dados controlados.
5. **Fase 5 (primeiros grupos HTTP)** — migra setup de conta/configuração e os fluxos login/authorize/token.
6. **Fase 6 (fluxos restantes e default)** — elimina acessos diretos do HTTP ao fake e vira `AppFactory`.
7. **Fase 7 (contratos de teste)** — retira os últimos consumidores do fake sem ainda alterar os contratos públicos.
8. **Fase 8 (remoção da transição)** — aplica Q9, apaga fallbacks e exclui o fake num único corte compilável.
9. **Fase 9 (paridade e fechamento)** — executa Q10, regressão completa e atualiza a documentação normativa.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

---

## Fase 1 - contrato de configuração e composição

**Depende de:** Q1, Q2, Q3, Q5, Q6, Q7, Q8, DF3, DF4, DF9-DF12 e DF15.

**Escopo:** `RoyalIdentity.Server`, options/validators de composição, `Tests.Architecture` e eventual ADR exigida
pelas respostas.

**O que/como:** transformar as respostas em decisões fechadas; criar um único entry point de registro persistente
com configuração tipada, validação antes do tráfego e grafo de dependências permitido. Não trocar o backing do
Server antes de a superfície estar testada.

**Tarefas:**

- [ ] Registrar Q1-Q3/Q5-Q8 respondidas em `Histórico de decisões` e criar as DFs correspondentes.
- [ ] Registrar em ADR nova somente decisões arquiteturais que não estejam cobertas pelas ADRs existentes.
- [ ] Criar options tipadas para provider(s), conexões, snapshot, cleanup, protection, Data Protection e profile
  demo conforme as respostas.
- [ ] Validar presença, formato, combinações permitidas e duplicidade sem materializar secrets em mensagens.
- [ ] Alterar `AddHostServices`/entry point escolhido para receber `IConfiguration` e ambiente explicitamente.
- [ ] Encapsular a composição persistente em uma superfície reutilizável conforme Q8.
- [ ] Substituir guards que hoje proíbem qualquer referência EF no Server por guards que permitam adapters/providers
  e continuem proibindo `Data.*`, `Migrations` e dependências inversas.
- [ ] Cobrir configuração SQLite/PostgreSQL, campos ausentes, provider inválido, cleanup ausente e protection
  incompatível.

**Critérios de aceite:** as opções válidas produzem exatamente uma descrição de composição; provider, conexão,
cleanup ou protection ausentes/ambíguos falham antes de servir requests; nenhum erro contém secret; os guards
arquiteturais refletem DF3/DF15; nenhuma pergunta desta fase permanece aberta.

**Testes:**

```powershell
dotnet build RoyalIdentity.Server/RoyalIdentity.Server.csproj
dotnet test Tests.Architecture
dotnet test Tests.Integration --filter "FullyQualifiedName~HostConfiguration"
```

### Resultado da Fase 1

*a preencher*

---

## Fase 2 - provisionamento externo das três famílias

**Depende de:** Q4, Fase 1, DF8, DF9, DF12, DF14 e DF15.

**Escopo:** `RoyalIdentity.Migrations` e/ou runner da família `UserAccounts`, providers de `UserAccounts`, seeds,
scripts/README e testes de migration.

**O que/como:** entregar a rota operacional decidida em Q4 para aplicar migrations de Configuration, Operational e
`UserAccounts` fora do host. Preservar conexão, history, resultado e ownership próprios; somente criar seed demo
produtivo se Q6=A.

**Tarefas:**

- [ ] Registrar Q4 respondida e convertê-la em decisão fechada.
- [ ] Implementar a seleção explícita da família `UserAccounts` no runner escolhido sem acoplá-la ao gateway
  `IStorage`.
- [ ] Aceitar provider e conexão por família conforme Q1/Q2, preferindo secrets por variáveis de ambiente.
- [ ] Desacoplar a seleção de provider Configuration/Operational no runner atual e cobrir topologia mista somente se
  Q1=C.
- [ ] Aplicar migrations das famílias selecionadas em ordem documentada, sem transação distribuída.
- [ ] Retornar status independente por família e preservar códigos de saída não zero em falha parcial.
- [ ] Manter seed Configuration `Product|Demo` idempotente e separado de migration.
- [ ] Implementar seed demo de contas externo e composition-owned somente se Q6=A, sem referenciar `Tests.*`.
- [ ] Provar segunda execução idempotente, banco compartilhado, bancos separados e combinações de provider
  autorizadas por Q1.
- [ ] Provar que falha na terceira família não reporta rollback inexistente das anteriores.
- [ ] Atualizar a documentação do(s) runner(s) com comandos que não exponham connection strings/chaves.

**Critérios de aceite:** banco vazio pode receber os três schemas somente pelo(s) comando(s) externo(s); execução
repetida é idempotente; cada família tem resultado identificável; banco compartilhado não colide histories/tabelas;
as combinações de provider autorizadas por Q1 são aceitas e as demais são rejeitadas antes de I/O;
`RoyalIdentity.Server` não referencia nem chama o runner; seed demo segue Q6.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~MigrationRunner|FullyQualifiedName~Migration"
dotnet test Tests.UserAccounts --filter "FullyQualifiedName~Migration"
dotnet test Tests.Architecture
```

### Resultado da Fase 2

*a preencher*

---

## Fase 3 - composição real e fail-fast do Server

**Depende de:** Fases 1-2, Q1-Q7, DF3, DF4, DF8-DF13 e DF15.

**Escopo:** `RoyalIdentity.Server`, providers EF, `.Integration`/providers de `UserAccounts`, startup validators,
appsettings de exemplo e testes de host.

**O que/como:** trocar `AddInMemoryStorage()` pela composição integral. Registrar Data Protection, contexts
Configuration/Operational, snapshot, resource bridge, Operational/profiles, cleanup, protector de signing keys,
gateway completo e `UserAccounts`, nesta ordem lógica. Executar somente validações de leitura no startup conforme Q5.

**Tarefas:**

- [ ] Referenciar no Server apenas adapters/providers permitidos pela decisão Q1.
- [ ] Configurar ASP.NET Data Protection e o protector de signing keys compatível com o provisionamento, preservando
  a independência dos eixos decididos em Q3.
- [ ] Registrar contexts Configuration/Operational com histories corretas para o(s) provider(s) selecionado(s).
- [ ] Registrar snapshot source/refresh interval e resource bridge somente conforme o profile decidido em Q6.
- [ ] Registrar Operational storage, profiles e exatamente um modo de cleanup.
- [ ] Registrar o gateway `AddEntityFrameworkStorage()` completo.
- [ ] Registrar provider `UserAccounts`, resolver de options conforme Q7 e
  `AddUserAccountsForRoyalIdentity()`.
- [ ] Remover `AddInMemoryStorage()` e a referência `RoyalIdentity.Storage.InMemory` do Server.
- [ ] Implementar readiness somente leitura conforme Q5, sem `EnsureCreated`, `Migrate*` ou seed.
- [ ] Validar todos os profiles Operational selecionados pelos realms do snapshot antes do tráfego.
- [ ] Preservar bootstrap de snapshot, `SigningKeyStartupValidator` e ordem
  `UseRealmDiscovery` antes de `UseAuthentication`.
- [ ] Cobrir schema ausente/pendente conforme Q5, protector incompatível, profile ausente, key inválida e
  `IUserSecurityStateProvider` exigido por policy.

**Critérios de aceite:** o Server inicia sobre bancos previamente provisionados e resolve exatamente um `IStorage`
EF e um `IUserDirectory` de `UserAccounts`; configuração inválida falha antes de aceitar request; o projeto não
referencia InMemory/Migrations/Data; não há migration/seed no processo; resource bridge segue DF13.

**Testes:**

```powershell
dotnet build RoyalIdentity.Server/RoyalIdentity.Server.csproj
dotnet test Tests.Architecture
dotnet test Tests.Integration --filter "FullyQualifiedName~HostComposition|FullyQualifiedName~HostStartup"
```

### Resultado da Fase 3

*a preencher*

---

## Fase 4 - fixture SQLite unificada, handles e seeds

**Depende de:** Q8, Fases 2-3, DF5, DF13, DF14 e DF16.

**Escopo:** `Tests.Host`, `Tests.Integration/Prepare`, `Tests.UserAccounts/UserAccountsModuleSeed.cs`, helpers de
Configuration/Operational e resource bridge test-only.

**O que/como:** criar uma única factory integral SQLite com Configuration + Operational migrados e `UserAccounts`
real. Expor dados por handles neutros e operações explícitas de setup; não substituir um acesso ao fake por outro
static global.

**Tarefas:**

- [ ] Implementar a fonte única de composição HTTP decidida em Q8.
- [ ] Criar banco(s)/conexões SQLite isolados por lifetime de factory e aplicar migrations antes do host.
- [ ] Registrar Configuration + Operational EF, `UserAccounts` SQLite e cleanup `External` na fixture.
- [ ] Usar protectors determinísticos test-only sem variável de ambiente process-global compartilhada.
- [ ] Semear Configuration demo/teste, Alice/Bob e property scopes por owner correto.
- [ ] Mover `AliceSubjectId`/`BobSubjectId` para o seed test-only e remover seu import de InMemory.
- [ ] Expor handles imutáveis para realms internos/demo, clients, resources e subjects.
- [ ] Criar helper de client que persiste pelo seam test-only e chama `IConfigurationSnapshotRefresher`.
- [ ] Criar source/hook explícito para resources/scopes voláteis, sem nova tabela/contrato público.
- [ ] Criar operações test-only de conta via features reais do módulo para seed, claims e activate/deactivate.
- [ ] Criar setup Operational focado apenas onde a API pública não permite preparar o cenário.
- [ ] Provar smoke de discovery, login, authorize, token e sessão na composição integral.
- [ ] Garantir teardown de arquivos/conexões e ausência de contaminação entre duas factories paralelas.

**Critérios de aceite:** uma factory inicia sem resolver `MemoryStorage`; os três backings reais estão presentes;
Alice/Bob mantêm subjects determinísticos; writes de client são visíveis após refresh; resources usam a bridge;
duas fixtures não compartilham DB, env var ou estado estático; um fluxo OIDC completo passa.

**Testes:**

```powershell
dotnet test Tests.UserAccounts --filter "FullyQualifiedName~UserAccountsModuleSeed"
dotnet test Tests.Integration --filter "FullyQualifiedName~EntityFrameworkStorageOidcFlow|FullyQualifiedName~PersistentStorage"
```

### Resultado da Fase 4

*a preencher*

---

## Fase 5 - migração de login, profile, authorize e token

**Depende de:** Fase 4, DF2, DF5, DF13 e DF14.

**Escopo:** testes de login/profile/claims/active rule, authorize/code/client token, discovery/JWK/signing,
`CharacterizationSeed` e seus setups.

**O que/como:** migrar os primeiros grupos do macro-plano para a factory integral. Trocar live references por features
do módulo, getters de realm por handles e mutações de client/resource por helpers que atualizam snapshot/bridge.

**Tarefas:**

- [ ] Migrar login, user info, claims, active/lockout e caracterizações de conta.
- [ ] Substituir seed/inspeção de `MemoryUserAccount` por seed e comportamento observável do módulo.
- [ ] Substituir deactivate/claim mutation por operações reais/test-only de `UserAccounts`.
- [ ] Migrar authorize, code token, client token, discovery, JWK e signing algorithm.
- [ ] Substituir clients diretos por helper Configuration + refresh explícito.
- [ ] Substituir resources diretos pelo source volátil da fixture.
- [ ] Semear contas cross-realm por `realmId`/`SubjectId`, sem copiar objetos do demo.
- [ ] Remover dos arquivos migrados todos os getters de `RealmMemoryStore` e constantes `MemoryStorage`.
- [ ] Preservar casos negativos, issuer/realm isolation, PKCE, signing algorithm e claims emitidas.

**Critérios de aceite:** todos os grupos listados executam somente sobre a factory integral; seus arquivos não
referenciam namespace/tipos do fake; alterações de conta passam por `UserAccounts`; writes de Configuration ficam
visíveis no snapshot; nenhuma asserção depende de live reference.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~Login|FullyQualifiedName~UserInfo|FullyQualifiedName~Claims|FullyQualifiedName~ActiveRule"
dotnet test Tests.Integration --filter "FullyQualifiedName~CodeAuthorize|FullyQualifiedName~CodeToken|FullyQualifiedName~ClientToken|FullyQualifiedName~Discovery|FullyQualifiedName~Jwk|FullyQualifiedName~SigningAlgorithm"
```

### Resultado da Fase 5

*a preencher*

---

## Fase 6 - migração dos fluxos restantes e troca do default

**Depende de:** Fase 5, DF2, DF5, DF7, DF13 e DF14.

**Escopo:** refresh/revocation, logout/session, UI/consent, realm isolation, demais caracterizações,
`AppFactory` e factories opt-in parciais.

**O que/como:** migrar os grupos restantes, eliminar preparações específicas do fake e somente então tornar a
factory integral o `AppFactory` compartilhado pelas 29 classes.

**Tarefas:**

- [ ] Migrar refresh token, claims mode e revocation sem `UpdateAsync` manual de cenário.
- [ ] Substituir limpeza global de access tokens pela remoção do JTI conhecido ou hook Operational test-only.
- [ ] Migrar end session, lifecycle de sessão, logout e revogação por subject.
- [ ] Capturar `sid` no próprio fluxo e consultar por id, sem scan de `UserSessions`.
- [ ] Migrar UI login/consent, issuer URI, eventos e isolamento por realm.
- [ ] Substituir `FakeSessionStorage` baseado em stores concretos por doubles locais de contratos ou gateway EF.
- [ ] Tornar a composição integral a implementação de `AppFactory`.
- [ ] Absorver/remover `EntityFrameworkStorageAppFactory` e `UserAccountsAppFactory` parciais.
- [ ] Remover o global using e todas as referências a `MemoryStorage`/`RealmMemoryStore` de `Tests.Integration`.
- [ ] Executar toda a suíte HTTP sobre o novo default antes de tocar nos contratos atômicos.

**Critérios de aceite:** as 29 classes antes ligadas a `AppFactory` executam sobre EF + `UserAccounts`; não existem
factories parciais; `Tests.Integration` não contém uso de `MemoryStorage`, getters do fake ou mutação de dictionary;
todos os fluxos e caracterizações permanecem verdes; os fallbacks ainda não foram ampliados nem acionados pelo EF.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~RefreshToken|FullyQualifiedName~Revocation"
dotnet test Tests.Integration --filter "FullyQualifiedName~EndSession|FullyQualifiedName~UserSession|FullyQualifiedName~SessionLifecycle|FullyQualifiedName~DefaultUserSession"
dotnet test Tests.Integration --filter "FullyQualifiedName~Realm|FullyQualifiedName~IssuerUri|FullyQualifiedName~LoginConsent"
dotnet test Tests.Integration
```

### Resultado da Fase 6

*a preencher*

---

## Fase 7 - desacoplamento dos contratos de teste do fake

**Depende de:** Fase 6, DF5-DF7 e DF16.

**Escopo:** `Tests.Storage`, `Tests.UserAccounts`, `Tests.Architecture`, `Tests.Host` e referências de projeto
restantes.

**O que/como:** retirar todos os consumidores que ainda obrigariam o projeto fake a compilar depois da quebra Q9.
Preservar cobertura sem transformar doubles locais em outro backing geral.

**Tarefas:**

- [ ] Adicionar a variante EF completa de `StorageSessionContractTests`.
- [ ] Substituir `CompositeStorageSessionTests` Configuration EF + Operational fake pelo gateway EF completo.
- [ ] Remover fallbacks de stores fake do harness SQLite Configuration; usar gateway EF ou doubles locais focados.
- [ ] Remover as 11 variantes `InMemory` e `InMemoryStorageHarness` de `Tests.Storage`.
- [ ] Substituir em `OperationalContractsShapeTests` os casos que usam o fake por doubles locais de caracterização
  do contrato ainda transitório; registrar as asserções que serão removidas/reformuladas na Fase 8, sem antecipar
  Q9.
- [ ] Remover a especialização `InMemory` de `UserDirectoryContractTests`.
- [ ] Tornar os realms da variante `UserAccountsSqlite` independentes de `MemoryStorage`.
- [ ] Remover referências ao fake de `Tests.Storage`, `Tests.UserAccounts` e `Tests.Host`.
- [ ] Substituir o teste arquitetural do grafo do fake por allowlist genérica de dependências, sem conservar o nome
  literal do projeto removido.
- [ ] Mapear cada teste concreto removido para cobertura EF/módulo equivalente e registrar qualquer perda real.

**Critérios de aceite:** somente o próprio projeto `RoyalIdentity.Storage.InMemory` e a entrada na solução permanecem;
nenhum projeto de produção/teste o referencia; contratos de core e `UserDirectory` rodam sobre providers reais;
`IStorageSession` possui cobertura EF; não houve perda de cenário sem substituição registrada.

**Testes:**

```powershell
dotnet test Tests.UserAccounts --filter "FullyQualifiedName~UserDirectoryContractTests|FullyQualifiedName~UserAccountsModuleSeed"
dotnet test Tests.Storage --filter "FullyQualifiedName~Contracts|FullyQualifiedName~StorageSession|FullyQualifiedName~StorageGateway|FullyQualifiedName~OperationalContractsShape"
dotnet test Tests.Architecture
```

### Resultado da Fase 7

*a preencher*

---

## Fase 8 - remoção da transição e exclusão do fake

**Depende de:** Q9, Fase 7, DF6 e DF7.

**Escopo:** contratos/consumers do core, adapter EF Operational, shape tests, solução e
`RoyalIdentity.Storage.InMemory`.

**O que/como:** aplicar a quebra pública escolhida em Q9 e excluir o fake no mesmo corte. O código intermediário não
precisa suportar uma implementação sem atomicidade; a branch deve voltar a compilar e testar antes de encerrar a
fase.

**Tarefas:**

- [ ] Registrar Q9 respondida e convertê-la em decisão fechada.
- [ ] Tornar consumo de authorization code single-use uma dependência obrigatória de compilação.
- [ ] Tornar transição de refresh token versionada/condicional uma dependência obrigatória de compilação.
- [ ] Remover casts, capability detection, logging de fallback e get-then-remove do
  `DefaultAuthorizationCodeConsumer`.
- [ ] Remover casts, fallback não condicional e `IRefreshTokenStore.UpdateAsync`.
- [ ] Remover interfaces/composites redundantes conforme a opção Q9.
- [ ] Atualizar adapter EF, mocks/doubles locais e testes de shape para o contrato final.
- [ ] Preservar testes concorrentes de code single-use e refresh transition/tolerance.
- [ ] Remover `AddInMemoryStorage`, extensões, facades e todos os arquivos de
  `RoyalIdentity.Storage.InMemory`.
- [ ] Remover o projeto da solução, props/referências e guards históricos restantes.
- [ ] Executar busca estática em código/projetos/solução para provar ausência do fake e dos fallbacks.

**Critérios de aceite:** nenhum consumer possui ramo não atômico; `IRefreshTokenStore.UpdateAsync` não existe; a
composição EF satisfaz os contratos em compile time; nenhuma referência, símbolo ou projeto InMemory permanece em
código/csproj/solução; concorrência e tolerância mantêm as semânticas fechadas.

**Testes:**

```powershell
dotnet build RoyalIdentity.sln
dotnet test Tests.Identity
dotnet test Tests.Storage
dotnet test Tests.Integration
dotnet test Tests.UserAccounts
dotnet test Tests.Architecture
rg -n "RoyalIdentity\.Storage\.InMemory|AddInMemoryStorage|MemoryStorage|RealmMemoryStore" . -g "*.cs" -g "*.csproj" -g "*.sln" -g "!old-is4/**"
rg -n "ISingleUseAuthorizationCodeStore|IVersionedRefreshTokenStore|fallback|UpdateAsync" RoyalIdentity/Contracts RoyalIdentity.Storage.EntityFramework Tests.Storage
```

Para as duas buscas `rg`, o resultado esperado deve ser documentado na fase: zero para símbolos removidos; menções
legítimas a `UpdateAsync` não relacionadas a refresh ou a texto histórico devem ser classificadas explicitamente.

### Resultado da Fase 8

*a preencher*

---

## Fase 9 - PostgreSQL, regressão final e fechamento documental

**Depende de:** Q10, Fase 8, DF1-DF16.

**Escopo:** aceites PostgreSQL, suíte completa, `ADR-018`, foundations, roadmap/macro, backlog, README de migrations,
`AGENTS.md` e este plano.

**O que/como:** executar a evidência PostgreSQL decidida em Q10, fechar regressão de solução e alinhar toda
documentação ao estado real. Não marcar conclusão com teste obrigatório não executado.

**Tarefas:**

- [ ] Registrar Q10 respondida e convertê-la em decisão fechada.
- [ ] Provisionar PostgreSQL 17 efêmero com porta dinâmica não padrão e as três famílias.
- [ ] Executar startup/fluxo OIDC PostgreSQL conforme Q10 e registrar comando, contagens e skips.
- [ ] Executar novamente contratos, migrations, concorrência e gateway SQLite/PostgreSQL afetados.
- [ ] Executar `dotnet build` e `dotnet test` da solução completa.
- [ ] Atualizar ADR-018 com a conclusão da migração e a remoção efetiva do fake, sem adicionar design à ADR.
- [ ] Atualizar `product.md`, `tech.md`, `structure.md` e `architecture.md` onde ainda descrevem o default antigo.
- [ ] Atualizar `plans-roadmap-02.md`, `plan-data-macro.md`, backlog e `AGENTS.md` com Plano 4 concluído e próximo
  item real.
- [ ] Atualizar README/scripts do provisionamento com provider, três famílias, protection, cleanup e experiência
  demo decididos.
- [ ] Confirmar que documentação histórica não é apresentada como instrução vigente.
- [ ] Preencher `Resultado da Fase` de todas as fases, riscos, desvios e pendências.
- [ ] Remover `Perguntas ao humano`, marcar o plano `CONCLUIDO` e atualizar a barra somente após todos os gates.

**Critérios de aceite:** evidência PostgreSQL cumpre Q10; solução completa verde; nenhuma instrução vigente indica
InMemory como default; ADR-018 registra a consequência já realizada; documentação operacional permite provisionar e
iniciar o host sem secret em linha de comando; todas as perguntas estão fechadas.

**Testes:**

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

Executar também o script PostgreSQL definido/atualizado pela fase e registrar o comando exato em
`Resultado da Fase 9`; não inserir aqui um nome de script que ainda dependa da resposta Q10.

### Resultado da Fase 9

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(es) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| Objetivo 1 — Server integral sem fake | 1-3 | DF3, DF4, DF8-DF13, DF15; Q1-Q3/Q5-Q7 | exatamente um gateway EF e um `IUserDirectory` real; zero migration no web | `HostConfiguration`, `HostComposition`, `HostStartup`, `Tests.Architecture` |
| Objetivo 2 — provisionamento/readiness | 1-3 | DF8, DF9, DF11, DF12, DF15; Q2-Q6 | três schemas externos, resultado por família, falha segura | testes de migration/runner e startup negativo |
| Objetivo 3 — testes default reais | 4-6 | DF2, DF5, DF13, DF14, DF16; Q8 | 29 consumers sobre factory integral; zero uso do fake em `Tests.Integration` | grupos HTTP + `dotnet test Tests.Integration` |
| Objetivo 4 — remover transição/fake | 7-8 | DF6, DF7; Q9 | atomicidade obrigatória; zero fallback/projeto/referência fake | `Tests.Identity`, `Tests.Storage`, concorrência e buscas `rg` |
| Objetivo 5 — paridade e fechamento | 9 | DF1, DF2, DF16; Q10 | gate PostgreSQL cumprido, solução/doc normativa verde | script PostgreSQL decidido + `dotnet test RoyalIdentity.sln` |

---

## Invariantes a preservar

1. Todo dado de client, key, conta, sessão, token, consent ou configuração permanece realm-scoped.
2. `RoyalIdentity` não referencia providers, Server, `UserAccounts` ou qualquer fake.
3. `RoyalIdentity.Pipelines` permanece sem dependência de domínio.
4. `Data.*` permanece puro e só é adaptado por `RoyalIdentity.Storage.EntityFramework`.
5. `UserAccounts` puro não referencia o core; somente `.Integration` conhece os dois lados.
6. O Server não referencia `Data.*`, `RoyalIdentity.Migrations` ou `Tests.*`.
7. O processo web nunca aplica migration, `EnsureCreated` ou seed.
8. Configuration, Operational e `UserAccounts` mantêm migrations/histories/resultado próprios.
9. Não há transação global nem promessa de rollback conjunto entre famílias.
10. Cleanup possui exatamente um modo explícito.
11. Plain nunca é default e proteção ausente/incompatível falha fechado.
12. Signing keys persistidas permanecem desprotegíveis pelo host depois do provisionamento.
13. Resources/scopes permanecem voláteis por DF13.
14. Authorization codes são single-use sob concorrência real.
15. Refresh transitions são condicionais e preservam a tolerância pós-consumo vigente.
16. Nenhuma nova paridade é adicionada ao fake durante sua janela restante.
17. Setup de conta usa o módulo ou seam test-only; nunca live reference de entidade.
18. Write de Configuration usado por teste é seguido de refresh explícito do snapshot.
19. Fixtures não compartilham DB, secret, env var mutável ou handle estático.
20. `UseRealmDiscovery` continua antes de `UseAuthentication`.
21. Validators continuam sinalizando falhas esperadas por `context.Response`, sem lançar por erro de protocolo.
22. A fase de exclusão não remove cobertura sem mapear seu provider/teste substituto.

---

## Critérios globais de conclusão

- Q1-Q10 foram respondidas, convertidas em DFs e removidas da seção de perguntas.
- `RoyalIdentity.Server` inicia sobre a composição persistente decidida e não oferece fallback in-memory.
- As três famílias são provisionáveis fora do host e validadas conforme Q5.
- `Tests.Integration` roda integralmente sobre EF/SQLite + `UserAccounts`.
- Nenhum código de teste resolve `MemoryStorage`, `RealmMemoryStore` ou stores concretos do fake.
- Nenhum consumer detecta capability atômica opcional ou executa fallback não atômico.
- `RoyalIdentity.Storage.InMemory` não existe na solução nem no grafo de projetos.
- Resources/scopes continuam pela bridge e nenhuma semântica fechada na matriz foi reaberta.
- O gate PostgreSQL decidido em Q10 está verde e registrado.
- Foundations, ADR-018, roadmap, backlog, README e `AGENTS.md` refletem o estado final.
- `dotnet build RoyalIdentity.sln` está verde.
- `dotnet test RoyalIdentity.sln` está verde.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| Runner e host usam proteção incompatível | key ring, application name ou AES key divergem | signing keys/payloads ficam ilegíveis e host não sobe | contrato único Q3, teste cross-process/instância e fail-fast | Aberto |
| Host sobe com schema Operational/UserAccounts inválido | cleanup External não toca o banco no startup | falha tardia em token/login | decisão Q5 + readiness somente leitura | Aberto |
| Demo deixa de autenticar | Configuration demo existe sem accounts/resources | `dotnet run` parece funcional, mas login/authorize falha | decidir Q6 e testar/documentar o profile inteiro | Aberto |
| Histories colidem em banco compartilhado | terceira família usa nome/schema incompatível | migrations são ignoradas ou reaplicadas | teste same-database e history explícita por owner | Aberto |
| Snapshot não reflete setup | helper grava client e não publica refresh | testes falham ou exercitam dados antigos | helper único + `IConfigurationSnapshotRefresher` obrigatório | Aberto |
| Estado global contamina fixtures | env AES estática, arquivo ou connection compartilhada | flakiness/paralelismo inseguro | material/lifetime por fixture e teste com duas factories | Aberto |
| Live references são reproduzidas em outro seam | setup altera entidade EF diretamente | teste deixa de representar comportamento real | features do módulo ou hook test-only explícito | Aberto |
| Resource bridge é “resolvida” com persistência acidental | cenário precisa adicionar/remover resource | quebra DF13 e antecipa redesign | source volátil da fixture + guard de arquitetura | Aberto |
| Limpeza direta de Operational vira API pública genérica | teste chama `Clear()` por conveniência | contrato de produto cresce por setup | remover handle conhecido ou hook test-only focado | Aberto |
| Quebra atômica ocorre cedo demais | Q9 é aplicada com fake ainda referenciado | fake precisa ganhar paridade ou solução não compila | Fases 6-8 e corte coordenado DF7 | Aberto |
| Testes concretos do fake somem sem equivalente | variante/harness é apagado sem mapa | perda silenciosa de regressão | inventário por cenário na Fase 7 | Aberto |
| SQLite mascara diferença produtiva | fluxo só é executado no default SQLite | regressão de provider chega a produção | gate Q10 e contratos PostgreSQL reais | Aberto |
| Cleanup Hosted disputa entre réplicas | várias instâncias escolhem Hosted | carga/locks apesar de batches idempotentes | configuração explícita, documentar External para cluster | Aberto |
| Secrets aparecem em CLI/log | conexão/key é passada como argumento ou exception | exposição operacional | env/secret store, sanitização e testes negativos | Aberto |
| Foundations continuam instruindo fake | docs não são atualizadas no fechamento | nova implementação reintroduz padrão removido | Fase 9 documental obrigatória | Aberto |

---

## Diferidos e backlog

- Persistência/redesign de resources e scopes — destino: plano específico após a decisão 22 do baseline.
- Cache de Configuration/Operational — destino: `plan-data-caching.md`.
- Auditoria durável e outbox seletivo — destino: `plan-data-audit-outbox.md`.
- Reproteção/rotação de signing keys e KMS — destino: plano KMS.
- Coordenação idempotente de tombstone Configuration + purge Operational + `UserAccounts` — destino: plano
  administrativo/ADR própria.
- Orquestração Aspire e deployment workloads — destino: `.ai/backlogs/backlog-001.md`.
- Lock distribuído para cleanup Hosted — destino: backlog operacional, se métricas demonstrarem necessidade.
- Persistência/admin de `UserAccountsRealmOptions` — destino: backlog/plano administrativo se Q7=A.
- API/UI administrativa e gerenciamento de realms/clients/users — destino: roadmap administrativo.

---

## Referências

- [plans-roadmap-02.md](plans-roadmap-02.md).
- [plan-data-macro.md](plan-data-macro.md).
- [plan-data-storage-matrix.md](plan-data-storage-matrix.md).
- [plan-data-configuration-storage.md](plan-data-configuration-storage.md).
- [plan-data-operational-storage.md](plan-data-operational-storage.md).
- [plan-users-accounts-sqlite-hardening.md](plan-users-accounts-sqlite-hardening.md).
- [ADR-013](../../adrs/ADR-013.md).
- [ADR-014](../../adrs/ADR-014.md).
- [ADR-015](../../adrs/ADR-015.md).
- [ADR-017](../../adrs/ADR-017.md).
- [ADR-018](../../adrs/ADR-018.md).
- [architecture.md](../foundation/architecture.md).
- `RoyalIdentity.Server/HostServices.cs`.
- `RoyalIdentity.Storage.EntityFramework/Extensions/ServiceCollectionExtensions.cs`.
- `RoyalIdentity.Storage.EntityFramework/Extensions/OperationalServiceCollectionExtensions.cs`.
- `RoyalIdentity.Migrations/StorageMigrationRunner.cs`.
- `RoyalIdentity.UserAccounts.Integration/UserAccountsIntegrationExtensions.cs`.
- `Tests.Integration/Prepare/EntityFrameworkStorageAppFactory.cs`.
- `Tests.Integration/Prepare/UserAccountsAppFactory.cs`.
- `Tests.Integration/Prepare/CharacterizationSeed.cs`.
- `Tests.Storage/Storage/Support/InMemoryStorageHarness.cs`.
- `Tests.UserAccounts/UserDirectoryContractTests.cs`.
- `RoyalIdentity/Contracts/Defaults/DefaultAuthorizationCodeConsumer.cs`.
- `RoyalIdentity/Contracts/Defaults/DefaultRefreshTokenConsumer.cs`.
