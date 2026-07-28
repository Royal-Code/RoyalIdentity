# Plan: Composição persistente do host e migração dos testes (`plan-data-test-migration`)

## Status: EM ANDAMENTO - Fases 1-3 concluídas em 2026-07-27; Fase 4 pendente

## Progresso

`███░░░░░░` **33%** - 3 de 9 fases

| Fase | Estado |
|---|---|
| Fase 1 - contrato de configuração e composição | Concluida |
| Fase 2 - provisionamento externo das três famílias | Concluida |
| Fase 3 - composições reais e fail-fast do Server/Demo | Concluida |
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

> **Evidência numérica:** todo número medido durante a execução deve ser registrado com o comando exato que o
> produziu, no mesmo item ou no `Resultado da Fase`; contagem sem comando não fecha critério de aceite.

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
- [ADR.md](../../ADR.md) — define que uma decisão aceita não é reescrita; consequências posteriores entram em
  nova seção de revisão, e mantém o índice das ADRs.
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
- `RoyalIdentity/Contracts/Defaults/DefaultAuthorizationCodeConsumer.cs` e
  `DefaultRefreshTokenConsumer.cs` — capability detection e fallbacks transitórios.
- `RoyalIdentity/Contracts/Storage/ISingleUseAuthorizationCodeStore.cs` e
  `IVersionedRefreshTokenStore.cs` — interfaces das capabilities atômicas.

### Estado atual do código (verificado em 2026-07-26)

- **O host oficial ainda é in-memory:** `RoyalIdentity.Server.HostServices.AddHostServices()` chama
  `AddInMemoryStorage()`, e o projeto referencia apenas Razor, core e `RoyalIdentity.Storage.InMemory`.
- **O host não possui contrato de persistência configurável:** `Program` não passa configuração/ambiente para
  `AddHostServices`, e `appsettings.json` não contém conexões, snapshot, cleanup ou proteção.
- **Ainda não existe `RoyalIdentity.Demo`:** a experiência demo está embutida nos dados do fake e não há
  composition root SQLite executável independente.
- **O gateway EF do core está completo:** `AddEntityFrameworkStorage()` compõe Configuration + Operational e recusa
  uma composição sem `AddEntityFrameworkOperationalCleanup(...)` explícito.
- **Configuration possui bootstrap funcional fail-closed:** snapshot inicial, `server_options` e signing keys
  utilizáveis são validados antes do tráfego. Isso não é inspeção de migrations; Operational e `UserAccounts` são
  acessados conforme os fluxos que os consomem.
- **O provisionamento do core é externo:** `RoyalIdentity.Migrations` migra Configuration e Operational,
  sequencialmente e sem transação conjunta; o Server não referencia o runner.
- **`UserAccounts` está pronto para runtime:** há registro SQLite de arquivo, SQLite in-memory test-only,
  PostgreSQL e `AddUserAccountsForRoyalIdentity()`. Seus providers possuem migrations, mas não há comando
  operacional integrado ao runner do core.
- **As factories opt-in são complementares, não cumulativas:** `EntityFrameworkStorageAppFactory` torna o core real
  e mantém contas fake; `UserAccountsAppFactory` torna contas reais e mantém o core fake.
- **O default HTTP continua fake:** 29 classes usam `IClassFixture<AppFactory>` — medição:
  `(rg -l "IClassFixture<AppFactory>" Tests.Integration -g "*.cs" | Measure-Object -Line).Lines` —, e `AppFactory`
  herda `AddInMemoryStorage()` de `Tests.Host`.
- **A suíte HTTP conhece detalhes do fake:** `MemoryStorage` possui 381 ocorrências em 36 arquivos;
  medições: `(rg -o "MemoryStorage" Tests.Integration -g "*.cs" | Measure-Object -Line).Lines` e
  `(rg -l "MemoryStorage" Tests.Integration -g "*.cs" | Measure-Object -Line).Lines`.
  `MemoryStorage.DemoRealm` possui 265 ocorrências — medição:
  `(rg -o "MemoryStorage\.DemoRealm" Tests.Integration -g "*.cs" | Measure-Object -Line).Lines` —; e o subject
  estático de Alice, 28 — medição:
  `(rg -o "MemoryStorage\.AliceSubjectId" Tests.Integration -g "*.cs" | Measure-Object -Line).Lines`. A busca
  `rg -o "GetRealmMemoryStore|GetDemoRealmStore|GetServerRealmStore" Tests.Integration -g "*.cs"` retorna
  exatamente 64 ocorrências: 16 de `GetRealmMemoryStore`, 47 de `GetDemoRealmStore` e 1 de
  `GetServerRealmStore`, incluindo as indireções definidas em `MemoryStorage.Storage.cs`.
- **A referência ao fake tem quatro consumidores diretos:** o comando
  `rg -l "RoyalIdentity.Storage.InMemory" -g "*.csproj"` retorna `RoyalIdentity.Server`, `Tests.Host`,
  `Tests.Storage` e `Tests.UserAccounts`; `Tests.Integration` recebe a dependência transitivamente de `Tests.Host`.
- **`Tests.Integration` já referencia o runner diretamente:** seu `.csproj` possui referência a
  `RoyalIdentity.Migrations`; essa relação não precisa ser criada para a fixture persistente.
- **O registro in-memory possui seis descriptors:** `AddInMemoryStorage()` registra `MemoryStorage`, `IStorage`,
  `IStorageProvider`, `IConfigurationSnapshotSource`, `ConfigurationSnapshotRefreshOptions` e `IUserDirectory`;
  não registra `IReplayCache` nem `IMessageStore`.
- **A integração de contas já suporta substituição explícita:** `AddUserAccountsForRoyalIdentity()` usa
  `Replace` e funciona sem um registro anterior do fake.
- **Há mutações não portáveis nos testes:** `CharacterizationSeed` e testes de refresh/signing alteram live
  references de contas, varrem sessões por subject, limpam tokens diretamente e escrevem clients/resources nos
  dictionaries do fake.
- **O seed compartilhado ainda importa o fake:** `Tests.UserAccounts/UserAccountsModuleSeed.cs` obtém os subjects
  determinísticos de Alice/Bob em `MemoryStorage`.
- **`Tests.Storage` ainda executa contratos contra o fake:** existem 11 especializações `InMemory` — medição:
  `(rg -n "public sealed class InMemory" Tests.Storage -g "*.cs" | Measure-Object -Line).Lines` —; o contrato de
  `IStorageSession` ainda não tem twin EF, e há composições parciais Configuration EF + Operational fake.
- **`Tests.UserAccounts` ainda protege paridade com o fake:** `UserDirectoryContractTests` possui uma especialização
  `InMemory`, enquanto até a variante SQLite usa realms estáticos do fake.
- **Atomicidade ainda é capability opcional:** os consumers fazem cast em runtime; na ausência da capability,
  authorization code usa get-then-remove e refresh token usa `UpdateAsync` não condicional.
- **A bridge de scopes padrão é global aos realms vivos:** a composição atual fornece os identity scopes padrão a
  todos os realms; o resource server demo continua sendo dado explícito da composição.
- **Não há automação de CI no repositório:** não existem `.github/workflows` nem arquivo de pipeline na raiz; CI
  permanece fora deste plano por DF24.
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
- **O seed Configuration já é reutilizável:** os modos `None|Product|Demo|All` vivem em
  `RoyalIdentity.Migrations`; testes referenciam o runner e executam `StorageMigrationRunner` com SQLite +
  `ConfigurationSeedMode.Demo`. O novo Demo pode usar o mesmo caminho sem ciclo porque fica acima de
  Migrations/SQLite no grafo. Alice/Bob continuam seed test-only de `UserAccounts`; código produtivo não pode
  referenciar `Tests.*`.
- **Não existe dado durável a migrar do fake:** o rollout troca composição e provisiona bancos vazios/externos; não
  há exportação de dictionaries in-memory.
- **Documentos de fundação estão defasados:** ainda descrevem o fake como implementação/default de referência e
  precisam ser corrigidos somente após o corte real.

### Superfícies impactadas a mapear

- `RoyalIdentity.Server` — composição PostgreSQL fixa, conexões por `DbContext`, Data Protection e startup.
- `RoyalIdentity.Demo` — novo host SQLite in-memory fixo que invoca migrations/seed demo existentes no runner e
  semeia suas contas pelos casos de uso do módulo.
- `RoyalIdentity.Migrations` e/ou runner próprio de `UserAccounts` — migrations, seeds e resultado operacional por
  família.
- `RoyalIdentity.Storage.EntityFramework*` — registro dos contexts/providers, gateway, protectors e validações
  funcionais já existentes.
- `RoyalIdentity.UserAccounts.*` — PostgreSQL no Server/runner, SQLite no Demo/testes, options por realm, migrations
  e adapter.
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

1. Compor `RoyalIdentity.Server` exclusivamente sobre Configuration + Operational EF PostgreSQL e `UserAccounts`
   PostgreSQL, sem fallback in-memory, seletor de provider ou execução de migration/seed.
2. Entregar provisionamento externo PostgreSQL e um host `RoyalIdentity.Demo` SQLite in-memory, fixo e
   self-provisioned, suficientes para iniciar as três famílias em seus ambientes próprios.
3. Tornar uma composição SQLite/EF + `UserAccounts` o default de `Tests.Integration`, com handles e seeds neutros ao
   provider.
4. Remover os caminhos não atômicos, as capabilities opcionais conforme DF28 e todo o projeto
   `RoyalIdentity.Storage.InMemory`.
5. Preservar as semânticas fechadas na matriz, a evidência PostgreSQL definida em DF24 e a suíte completa verde.

## Fora de escopo

- Persistir ou redesenhar resources/scopes — destino: plano específico após a decisão 22 do baseline.
- Alterar semânticas de stores fechadas em `plan-data-storage-matrix.md`.
- Adicionar cache aos stores EF — destino: `plan-data-caching.md`.
- Implementar auditoria durável, outbox ou inbox — destino: `plan-data-audit-outbox.md`.
- Criar API/UI administrativa, write model geral ou coordenação cross-family de exclusão de realm.
- Criar/rotacionar signing keys em runtime ou reproteger material existente — destino: plano de KMS.
- Integrar o KMS como protector oficial; Data Protection permanece a solução transitória deste plano por DF20.
- Implementar providers SQL Server, Oracle ou outros, ou antecipar um seletor de provider no Server; o runner
  conserva somente o suporte SQLite/PostgreSQL já necessário a Demo/testes e produto.
- Introduzir transação distribuída entre Configuration, Operational e `UserAccounts`.
- Implementar Aspire/deployment orchestration além do contrato executável de provisionamento — destino:
  `.ai/backlogs/backlog-001.md`.
- Migrar estado do fake para banco; o fake não é uma fonte durável.
- Persistir ou administrar `UserAccountsRealmOptions`; este plano entrega apenas a fonte de configuração em runtime
  definida por DF23.
- Expor reset destrutivo de banco/realm no runtime EF ou no Server; a operação administrativa cross-family fica
  diferida conforme a seção de backlog.

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
- **DF8 — Provisionamento fora do host produtivo:** o processo web produtivo nunca chama `EnsureCreated`,
  `Migrate`, `MigrateAsync` ou seed. `RoyalIdentity.Demo` é outro executável e constitui a única exceção
  self-provisioned, conforme DF27. Fonte: decisão 23 do Plano 3 e respostas humanas a Q6/Q13.
- **DF9 — Independência das famílias:** Configuration, Operational e `UserAccounts` preservam contexts, conexões,
  migrations e ownership independentes mesmo quando compartilham banco físico; não há transação global. Fonte:
  ADR-013 e Planos 0/2/3.
- **DF10 — Cleanup explícito:** toda composição EF completa escolhe `Hosted` ou `External`; ausência ou duplicidade
  falha. Não há default silencioso. Esse cleanup remove registros Operational expirados; não é reset destrutivo de
  Configuration/Operational/`UserAccounts`, que fica diferido para administração cross-family. Fonte: decisão 17
  do Plano 3, implementação atual e resposta humana após Q13.
- **DF11 — Proteção fail-closed:** Plain exige registro + seleção explícitos e warning; profile/protector ausente
  falha, sem fallback; segredos não entram em Configuration persistida. Fonte: Planos 2/3.
- **DF12 — Signing keys do Server são externas:** o runner produtivo cria material utilizável; o Server valida e
  usa, mas nunca cria nem rotaciona keys. `RoyalIdentity.Demo`, por começar com SQLite in-memory vazio, cria signing
  keys efêmeras como parte do seed de cada execução; isso não introduz rotação runtime nem autoriza criação no
  Server. Fonte: decisões 19/27/28 do Plano 2 e DF27.
- **DF13 — Resources/scopes permanecem bridge:** o Plano 4 usa `IConfigurationResourceSource`/hook de composição e
  não adiciona persistência ou write facade pública. Fonte: decisão 22 do baseline e Plano 2.
- **DF14 — Seeds separados por finalidade e runner genérico:** `RoyalIdentity.Migrations` mantém uma única
  implementação Configuration e os modos explícitos já existentes `None|Product|Demo|All`, selecionáveis
  independentemente do provider. Os entry points oficiais fixam somente as combinações que lhes pertencem: o
  comando produtivo usa Product e o Demo usa Demo; o runner não transforma essa convenção de composição em
  restrição artificial da sua API. O novo host é dono de invocar o modo Demo, não de duplicar sua mecânica.
  Alice/Bob e dados de cenário dos testes pertencem às fixtures de `UserAccounts`; para contas demo, o Demo chama
  casos de uso públicos do módulo. A implementação do seed permanece em `RoyalIdentity.Migrations`: não é movida
  nem duplicada em providers ou projetos `Data.*`, embora o runner possa consumir internamente entidades e
  materializadores EF/Data necessários ao provisionamento. Fonte: matriz, ADR-018, Planos 0/2 e resposta humana
  pela opção 1 de Q13.
- **DF15 — Server não referencia o runner:** `RoyalIdentity.Server` referencia os adapters/providers PostgreSQL
  fixados por DF17/DF18, mas nunca `RoyalIdentity.Migrations`, Demo, SQLite nem projetos `Data.*` diretamente.
  Fonte: architecture.md, ADR-013 e decisão 23 do Plano 3.
- **DF16 — Cobertura provider real prévia:** contratos, migrations, concorrência e gateway EF já estão verdes em
  SQLite e possuem aceites PostgreSQL opt-in; este plano migra consumidores e composição, sem repetir o design dos
  providers. Fonte: encerramento dos Planos 2/3.
- **DF17 — Server fixo; runner reutilizável:** este plano não cria selector/options de provider no Server.
  `RoyalIdentity.Server` usa PostgreSQL e `RoyalIdentity.Demo`/testes usam SQLite. O runner conserva o suporte já
  existente a SQLite e PostgreSQL para servir essas duas composições, sem admitir mistura de providers numa mesma
  execução. Novos providers, bancos ou orquestração Aspire serão adicionados somente em plano futuro orientado por
  uso concreto. Fonte: respostas humanas a Q1/Q13 e escolha da opção 1.
- **DF18 — Server exclusivamente PostgreSQL:** `RoyalIdentity.Server` referencia somente os providers PostgreSQL
  de Configuration/Operational e `UserAccounts`; não referencia SQLite, Demo ou Migrations. A experiência local do
  Server usa PostgreSQL real, inicialmente executável via Podman; SQLite pertence ao Demo e aos testes. Fonte:
  respostas humanas a Q1/Q13.
- **DF19 — Connection string por `DbContext`:** Configuration, Operational e `UserAccounts` recebem conexões
  explícitas e independentes, sem connection default herdada. No Server local, os três valores podem ser iguais e
  apontar para o mesmo PostgreSQL/banco Podman; continuam sendo três chaves. KMS seguirá a mesma regra quando for
  entregue. Fonte: respostas humanas a Q2/Q13.
- **DF20 — Data Protection oficial e transitório até KMS:** o host oficial usa Data Protection para signing keys e
  payloads protegidos, inclusive em debug; não seleciona Plain/AES como alternativa oficial. A integração futura
  com KMS substituirá essa solução em plano próprio. Host e provisionamento usam configuração compatível de key
  ring, application name e purposes. Fonte: resposta humana a Q3.
- **DF21 — Runner único inclui `UserAccounts` e preserva SQLite/PostgreSQL:** `RoyalIdentity.Migrations`
  provisiona Configuration, Operational e `UserAccounts` com um provider uniforme por execução e uma seleção de
  seed explícita e independente. Os usos oficiais são PostgreSQL + Product para produção e SQLite + Demo para o
  host Demo; outras seleções válidas do runner genérico, inclusive `None`/`All`, não são proibidas pela API. O
  runner preserva conexão, history e resultado por família, sem topologia mixed-provider.
  `RoyalIdentity.Server` não ganha referência, dependência ou chamada relacionada a migrations. Fonte: respostas
  humanas a Q4/Q13, escolha da opção 1 e decisão posterior de manter o runner genérico.
- **DF22 — Host ignora estado de migrations:** o Server não consulta migrations pendentes, não executa
  `GetPendingMigrations*`, `EnsureCreated*` ou `Migrate*` e não possui readiness específica de schema. Validações
  funcionais já existentes, como snapshot e signing keys utilizáveis, permanecem e podem falhar naturalmente se o
  banco não tiver sido provisionado. Fonte: resposta humana a Q5 e regra histórica do projeto.
- **DF23 — Options de `UserAccounts` configuráveis por realm:** este plano entrega uma fonte runtime baseada em
  `IConfiguration`, com defaults globais e overrides indexados pelo `realmId` estável, através da
  composição/adapter e sem criar dependência do módulo puro no core. A Fase 1 fixa e documenta o shape da seção,
  fallback e validação; persistência e administração dessas options permanecem diferidas. Fonte: resposta humana a
  Q7.
- **DF24 — PostgreSQL sem CI neste plano:** não se cria pipeline de CI. PostgreSQL, por ser o provider produtivo,
  recebe evidência executável local/opt-in de provisionamento, startup e fluxo OIDC; SQLite permanece o provider
  dos testes default. Fonte: resposta humana a Q10.
- **DF25 — Implementações entram pela composição dos contratos:** projetos de storage implementam e entregam os
  contratos core-owned do IdP; a composition root escolhe exatamente uma implementação. A retirada do fake é uma
  migração de consumidores/composição já determinada por ADR-018, não uma decisão humana adicional sobre
  coexistência de factories. Fonte: resposta humana à antiga Q12, ADR-013 e ADR-018.
- **DF26 — Desempenho da fixture não é gate humano:** a duração da suíte deve ser medida durante a implementação,
  mas otimizações de topologia não são decisões de arquitetura deste plano. A Fase 4 registra protocolo, baseline e
  um limiar numérico de regressão material antes da migração em massa; a Fase 6 repete o mesmo protocolo e compara o
  resultado ao limiar. Começar pela composição SQLite isolada mais simples e otimizar somente quando a medição
  ultrapassar esse limite. Fonte: resposta humana à antiga Q11.
- **DF27 — Host Demo SQLite fixo e self-provisioned:** criar `RoyalIdentity.Demo`, executável web irmão e
  independente de `RoyalIdentity.Server`. O Demo referencia os providers SQLite e `.Integration`, usa bancos
  SQLite in-memory com conexões keep-alive e invoca `StorageMigrationRunner` com SQLite +
  `ConfigurationSeedMode.Demo` antes do tráfego. A conexão keep-alive compartilhada é aberta antes do runner.
  Configuration e Operational compartilham um banco; `UserAccounts` mantém outro banco/ownership e suas contas são
  semeadas pelos casos de uso públicos do módulo. A composição é fixa e direta, sem options de provider ou
  connection string. O Demo referencia diretamente `RoyalIdentity.Migrations`, que por sua vez referencia os
  providers SQLite/PostgreSQL e projetos `Data.*`; portanto PostgreSQL/Data entram indiretamente no grafo
  transitivo e podem acompanhar o artefato publicado do Demo. Essa consequência da reutilização do runner é aceita:
  o projeto Demo não possui `ProjectReference` direto, uso em source, configuração nem registro de DI para
  PostgreSQL ou `Data.*`, e nunca seleciona/executa PostgreSQL. Providers SQLite não passam a possuir seed nem outra
  família. O modo Demo cria deliberadamente somente `demo_realm`, seus clients, resources, contas e signing keys;
  não cria os realms internos `server`, `account` e `admin` do modo Product. Não usar `All`: além de misturar
  finalidades, ele exigiria `serverAdminRedirectUris` e quebraria a experiência sem configuração. Esta é uma exceção
  explícita a DF8/DF22 porque ocorre em outro produto executável, nunca no host produtivo. Fonte: respostas humanas
  a Q6/Q13, escolha da opção 1, decisão de aceitar a dependência transitiva e caminho já exercitado por
  `Tests.Integration`.
- **DF28 — Contratos atômicos incorporados aos contratos base:** `ConsumeAuthorizationCodeAsync` entra em
  `IAuthorizationCodeStore`; `TryConsumeAsync`/`TryUpdateAsync` entram em `IRefreshTokenStore`;
  `ISingleUseAuthorizationCodeStore`, `IVersionedRefreshTokenStore` e `IRefreshTokenStore.UpdateAsync` são
  removidos. `IStorage` continua retornando os contratos base realm-bound, agora obrigatoriamente atômicos. Fonte:
  resposta humana a Q9 e revisão do código atual.
- **DF29 — Hosts são composition roots independentes:** seguir Q8=A. `Tests.Host.Program` continua sendo o
  ambiente HTTP real dos testes e não executa/adapta `RoyalIdentity.Server.Program`. Os três hosts reutilizam os
  extensions pertencentes a core, Razor, providers e `UserAccounts`, mas escolhem suas implementações
  independentemente; `RoyalIdentity.Demo` segue a mesma regra e não executa/adapta o Server. Para evitar drift
  somente na ordem protocolar crítica, extrair em
  `RoyalIdentity/Extensions/ApplicationBuilderExtensions.cs` um `UseRoyalIdentityProtocol(...)` provider-neutral,
  limitado a realm discovery, realm CORS, authentication, authorization e mapeamento dos endpoints OIDC. Error
  handling, UI/Razor, static files, antiforgery específico e endpoints `/test/*` permanecem em cada `Program`.
  O extension exige que o caller já tenha instalado routing e não possui antiforgery/UI; cada host instala esses
  elementos na ordem documentada ao redor dele. Depois da Fase 4, `Tests.Host` é somente infraestrutura para
  `WebApplicationFactory`, não um executável standalone sem factory/storage.
  Server e Demo possuem shell/scaffolding web mínimos próprios; alguma duplicação de arquivos estritamente de host
  é consequência aceita dessa independência. Não duplicar protocolo ou UI de contas: esses permanecem em
  `UseRoyalIdentityProtocol(...)` e `RoyalIdentity.Razor`. Não extrair bootstrap geral compartilhado apenas para
  eliminar essa duplicação.
  Não criar `RoyalIdentity.Hosting`, bootstrap geral compartilhado nem referências de `Tests.Host`/Demo ao
  `RoyalIdentity.Server`. Fonte: respostas humanas a Q8/Q13.
- **DF30 — Consumers atômicos sem indireção vazia:** depois de DF28,
  `IAuthorizationCodeConsumer`/`DefaultAuthorizationCodeConsumer` e
  `IRefreshTokenConsumer`/`DefaultRefreshTokenConsumer` seriam apenas delegações sem política própria; por isso são
  removidos. `LoadCode` chama diretamente o `IAuthorizationCodeStore` realm-bound, e `RefreshTokenHandler` chama
  diretamente o `IRefreshTokenStore` realm-bound. A tolerância pós-consumo de refresh permanece no handler, onde já
  está implementada; esta remoção não altera sua semântica. Fonte: revisão do uso atual após DF28.

---

## Histórico de decisões

**2026-07-26 (respostas humanas sobre a composição):**

- Q1/Q2 foram refinadas por DF17-DF19: cada `DbContext` mantém connection string explícita, mas não se antecipa
  seletor de provider no Server; Server é PostgreSQL, Demo/testes são SQLite e o runner atende ambos sem mistura
  por execução.
- Q3 foi fechada por DF20: Data Protection é a solução oficial temporária até KMS, inclusive em debug.
- Q4/Q5 foram fechadas por DF21/DF22: o runner existente incorpora `UserAccounts`; o Server não referencia,
  consulta nem executa migrations.
- Q7 foi fechada por DF23: options de `UserAccounts` serão configuráveis por realm.
- Q10 foi fechada por DF24: nenhuma CI será criada; a evidência PostgreSQL é executável local/opt-in.
- A antiga Q11 foi retirada como gate arquitetural e convertida em DF26/tarefa de medição não bloqueante.
- A antiga Q12 foi retirada como gate arquitetural e convertida em DF25: implementações são escolhidas pela
  composition root e entregam os contratos do IdP.
- Q6 foi inicialmente fechada como profile opt-in provider-owned; Q13 a refinou em DF27 para um executável
  `RoyalIdentity.Demo` separado, fixo e SQLite in-memory.
- Q9 foi fechada por DF28 com contratos base fortes.
- Q8 foi fechada por DF29 com A: `Tests.Host` independente e somente o pipeline protocolar provider-neutral
  extraído no core. B foi descartada; C não cria uma abstração de hosting própria.

**2026-07-26 (revisão do plano):**

- Q13 foi aberta porque o seed atual está no runner, que já referencia `.Sqlite`; a extensão demo não poderia
  reutilizá-lo por referência inversa sem criar ciclo.
- Os demais achados válidos foram incorporados em DF23/DF26/DF29/DF30 e nas tarefas, critérios, invariantes e riscos
  correspondentes.

**2026-07-27 (resposta humana a Q13):**

- Q13 foi fechada por DF17/DF18/DF21/DF27: criar um `RoyalIdentity.Demo` SQLite in-memory, fixo e com seed próprio;
  tornar o Server exclusivamente PostgreSQL e não antecipar options de provider no host.
- A escolha posterior da opção 1 refinou “seed próprio”: o Demo referencia `RoyalIdentity.Migrations` e invoca o
  `StorageMigrationRunner`/modo Demo já usado por `Tests.Integration`. O runner conserva SQLite para Demo/testes e
  PostgreSQL para produto; não há ciclo porque Demo fica acima de runner/providers.
- Foi aceita a consequência dessa escolha: PostgreSQL/`Data.*` entram transitivamente no grafo do Demo pelo runner,
  mas o Demo não possui referência direta, uso, configuração, registro ou execução desses providers.
- O runner permanece genérico: provider uniforme e modo de seed são seleções independentes; apenas os entry points
  oficiais fixam PostgreSQL + Product e SQLite + Demo.
- O Demo seleciona somente `ConfigurationSeedMode.Demo` e expõe deliberadamente apenas `demo_realm`; os realms
  internos do produto não fazem parte da experiência demo e `All` não é usado.
- O reset destrutivo de dados foi distinguido do cleanup Operational e diferido para a futura composição
  administrativa cross-family.

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

- `RoyalIdentity.Server`: compõe diretamente PostgreSQL para Configuration, Operational e `UserAccounts`, recebe
  uma connection string explícita por `DbContext` e não contém selector de provider nem acesso direto a entidades
  `Data.*`.
- `RoyalIdentity.Demo`: composition root web separada, fixa em SQLite in-memory e dona de invocar a orquestração
  `StorageMigrationRunner`/seed Demo já existente; nunca executa/adapta o `Program` do Server nem usa/configura
  PostgreSQL. A referência ao runner traz PostgreSQL/Data somente como dependências transitivas aceitas. Sua
  superfície funcional contém apenas `demo_realm`; os realms internos Product não são semeados.
- `IStorage`/`IStorageProvider`: são fornecidos exclusivamente pelo gateway EF completo; exatamente uma composição
  fica resolvível.
- `IUserDirectory` e portas realm-bound de conta: são fornecidos por
  `RoyalIdentity.UserAccounts.Integration`; o core continua sem referência ao módulo.
- `RoyalIdentity.Migrations`: recebe um provider uniforme SQLite/PostgreSQL e conexão por família, aplica migrations
  de Configuration, Operational e `UserAccounts` explicitamente, executa o modo de seed selecionado
  `None|Product|Demo|All` sem acoplar a seleção ao provider e retorna resultado independente por família.
- Startup do Server: não possui lógica ou inspeção de migrations; preserva apenas validações funcionais exigidas
  para atender requests, conforme DF22. O startup do Demo invoca o runner compartilhado com SQLite + Demo antes do
  tráfego por DF27.
- Configuração de cleanup: seleciona exatamente um modo `Hosted|External`, sem default.
- Proteção de signing keys e payload Operational: usa Data Protection oficial com material externo compatível com
  o provisionamento, conforme DF20; KMS permanece evolução futura.
- Contrato atômico de authorization code/refresh token: incorpora as operações seguras aos contratos base conforme
  DF28; nenhum consumer detecta capability opcional.
- `Tests.Host`: permanece agnóstico de storage; registros do fake, enquanto ainda existirem, pertencem apenas à
  factory legada transitória. A factory persistente nunca herda de uma factory que registra o fake e nunca tenta
  neutralizá-lo removendo descriptors seletivamente.
- Fixture HTTP: expõe handles imutáveis compostos por ids primitivos, caminhos e outros valores neutros de
  realm/client/resource/subject/session, além de operações test-only de setup; nunca expõe `Realm`,
  `MemoryStorage`, dictionaries ou live references. Quando um teste precisa do objeto `Realm`, ele o obtém de
  `IRealmStore`/snapshot após o seed, dentro da composição corrente.
- Escrita de clients da fixture: um helper interno e exclusivo de `Tests.Integration/Prepare` usa o
  `ConfigurationSqliteDbContext` e o `ClientMaterializer` existentes para preparar dados, salva a alteração e chama
  `IConfigurationSnapshotRefresher` antes do request. Cenários consomem somente o helper/handles; nenhum contrato
  público de escrita ou write model administrativo é adicionado ao produto.
- `IConfigurationSnapshotRefresher`: é chamado pela fixture depois de writes de Configuration e antes do request
  que consome os dados.
- `IConfigurationResourceSource`: continua sendo a rota volátil explícita de resources/scopes em host/fixtures.
- Topologia da fixture: começa com SQLite in-memory isolado por factory. Configuration e Operational usam o mesmo
  banco nomeado/keep-alive para preservar a cobertura HTTP da topologia compartilhada, embora cada `DbContext`
  mantenha registro e connection string explícitos; `UserAccounts` usa banco/keep-alive separado, conforme sua
  ownership. As três histories continuam independentes. O custo é medido conforme DF26.
- Composição dos testes: cada composition root registra exatamente uma implementação dos contratos do IdP. A
  composição legada existe apenas enquanto seus consumidores ainda não migraram e é excluída com eles, conforme
  DF25.

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
  -> entry point público nomeado RoyalIdentity.Server.ServerProgram
  -> RoyalIdentity.Storage.EntityFramework
  -> provider EF PostgreSql
  -> RoyalIdentity.UserAccounts.Integration
  -> provider UserAccounts PostgreSql
  -X-> RoyalIdentity.Demo
  -X-> providers Sqlite
  -X-> RoyalIdentity.Migrations
  -X-> RoyalIdentity.Storage.InMemory
  -X-> RoyalIdentity.Data.*
  -X-> Program público/gerado no namespace global

RoyalIdentity.Migrations/
  aplica schemas Sqlite/PostgreSql e seed Configuration None|Product|Demo|All
  mantém seleção de seed independente do provider
  preserva ownership e resultado por família
  -X-> RoyalIdentity.Demo

RoyalIdentity.Demo/
  composição web fixa, sem options de provider/conexão
  -> entry point público nomeado RoyalIdentity.Demo.DemoProgram
  -> RoyalIdentity.Migrations (StorageMigrationRunner + seed Demo)
     -> providers PostgreSql/Data.* apenas transitivamente; Demo não os usa, configura ou registra
  -> Configuration + Operational Sqlite no mesmo banco in-memory/keep-alive
  -> UserAccounts Sqlite em banco in-memory/keep-alive próprio
  -> migrations + seed somente de demo_realm antes do tráfego
  -X-> RoyalIdentity.Server
  -X-> Program público no namespace global
  -X-> ProjectReference/uso/configuração/registro direto de providers PostgreSql ou Data.*

Tests.Host + Tests.Integration/
  preservam o host de testes independente definido em DF29
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
- Cleanup é explícito e idempotente; modo `External` pressupõe provisionamento externo e não autoriza inspeção de
  migrations pelo host.
- `UserAccounts` conserva optimistic concurrency/retry e action-token conditional update; o plano não bypassa seus
  casos de uso com mutação de entidades vivas.
- O processo web produtivo não escreve schema nem seed; somente o executável separado `RoyalIdentity.Demo` invoca o
  runner compartilhado com SQLite e modo Demo antes do tráfego.
- `UseRoyalIdentityProtocol(...)` é chamado somente depois de `UseRouting`; instala realm discovery antes de
  authentication e não instala UI/antiforgery. Cada host adiciona seu antiforgery/UI depois do pipeline protocolar
  na ordem documentada.

### Triagem obrigatória de divergências

Toda asserção que precise mudar durante as Fases 4-7 deve ser classificada e registrada no `Resultado da Fase`, com
o teste afetado, o comportamento anterior, a evidência do backing real e a correção aplicada:

- **artefato-do-fake:** o teste dependia de comportamento não normativo do fake; ajustar o teste à matriz/ADR.
- **regressão-do-módulo:** `UserAccounts` viola a matriz ou ADR aplicável; corrigir o módulo e preservar a asserção
  normativa.
- **defeito-de-produto:** o backing real revela defeito do core mascarado pelo fake; corrigir o produto e registrar
  o mecanismo que tornou o defeito observável.
- **sem-decisão-normativa:** não existe fonte normativa suficiente; parar o cenário afetado e abrir pergunta ao
  humano antes de mudar produto ou asserção.

Nenhuma divergência pode ser automaticamente classificada como artefato do fake apenas porque surgiu após a troca
do backing.

### Compatibilidade, migração e rollout

- Primeiro entregar provisionamento PostgreSQL e as composições reais do Server/Demo; depois criar a fixture
  conjunta e migrar os grupos de testes.
- Manter o fake apenas como suporte temporário durante Fases 1-7, sem adicionar capabilities ou comportamento.
- Deixar a factory persistente como composição canônica somente após todos os grupos HTTP estarem verdes nela.
- Manter `Tests.Host` storage-agnóstico; cada factory registra diretamente a implementação dos contratos que
  pretende exercitar, sem registrar dois backings e tentar corrigir a composição depois.
- Preparar `Tests.Storage`, `Tests.UserAccounts` e guards arquiteturais para viver sem o fake antes da quebra pública.
- Aplicar DF28, remover fallbacks e excluir o projeto fake no mesmo corte compilável da Fase 8.
- Não há dual-write, import/export de dictionaries nem compatibilidade de dados com processos in-memory anteriores.
- Hosts produtivos devem executar o runner antes do novo binário; o Server nunca corrige schema produtivo.
  `RoyalIdentity.Demo` é descartável e self-provisioned por DF27.
- Medir o tempo da suíte antes/depois da migração com o mesmo protocolo; baseline, limiar numérico e comandos são
  registrados na Fase 4, e otimização de fixture só entra quando a Fase 6 ultrapassar esse limiar.
- O fechamento PostgreSQL é local/opt-in conforme DF24; CI não faz parte deste plano.

---

## Ordem de execução

1. **Fase 1 (contrato de configuração e composição)** — aplica DF17-DF23 e cria a superfície PostgreSQL validada
   que as demais fases consomem.
2. **Fase 2 (provisionamento externo)** — estende o runner genérico para os três schemas, preserva todos os modos de
   seed e prepara o uso produtivo PostgreSQL + Product.
3. **Fase 3 (Server e Demo reais)** — troca o host oficial e entrega o Demo somente depois de existir
   provisionamento/composição testáveis.
4. **Fase 4 (fixture SQLite unificada)** — aplica DF29 e reproduz a composição integral com lifetime e dados
   controlados.
5. **Fase 5 (primeiros grupos HTTP)** — migra setup de conta/configuração e os fluxos login/authorize/token.
6. **Fase 6 (fluxos restantes e default)** — elimina acessos diretos do HTTP ao fake e torna a factory persistente
   a única composição canônica.
7. **Fase 7 (contratos de teste)** — retira os últimos consumidores do fake sem ainda alterar os contratos públicos.
8. **Fase 8 (remoção da transição)** — aplica DF28, apaga fallbacks e exclui o fake num único corte compilável.
9. **Fase 9 (paridade e fechamento)** — executa DF24, regressão completa e atualiza a documentação normativa.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

---

## Fase 1 - contrato de configuração e composição

**Depende de:** DF3, DF4, DF9-DF12, DF15, DF17-DF23 e DF29.

**Escopo:** `RoyalIdentity/Extensions`, `RoyalIdentity.Server`, `Tests.Host`, `Tests.Integration`,
options/validators de composição, `Tests.Architecture` e ADR da separação Server/Demo.

**O que/como:** criar o entry point PostgreSQL do Server com configuração tipada por família/`DbContext`, validação
antes do tráfego e grafo de dependências permitido. Não criar abstração de provider nem trocar o backing antes de a
superfície estar testada.

**Tarefas:**

- [x] Registrar em ADR nova a separação entre `RoyalIdentity.Server` PostgreSQL e `RoyalIdentity.Demo` SQLite
  self-provisioned, ambos composition roots independentes e sem seletor de provider; manter detalhes de
  implementação somente neste plano.
- [x] Criar options tipadas independentes para as connection strings PostgreSQL de Configuration, Operational e
  `UserAccounts`; não criar enum, selector, factory ou options de provider.
- [x] Permitir que as três connection strings apontem para o mesmo PostgreSQL/banco no ambiente local, mantendo
  cada chave explícita e cada `DbContext`/history independente.
- [x] Criar options tipadas para snapshot, cleanup e Data Protection; não oferecer seletor oficial
  Plain/AES.
- [x] Validar presença e formato das conexões sem materializar secrets em mensagens; igualdade entre valores é
  permitida e não os transforma em uma conexão default implícita.
- [x] Alterar `AddHostServices`/entry point escolhido para receber `IConfiguration` e ambiente explicitamente.
- [x] Criar `UseRoyalIdentityProtocol(...)` no core com o limite exato de DF29; manter UI, error handling,
  static files, antiforgery específico e endpoints test-only fora do extension.
- [x] Documentar no XML/API do extension e provar nos composition roots a precondição `UseRouting` antes da chamada, a
  ordem interna `UseRealmDiscovery` antes de authentication e a responsabilidade do caller por UI/antiforgery
  depois do pipeline protocolar.
- [x] Fazer `RoyalIdentity.Server.Program` e `Tests.Host.Program` chamarem o extension, preservando composition
  roots e complementos próprios; o Demo será acrescentado na Fase 3.
- [x] Fixar e documentar a seção de `IConfiguration` para defaults globais e overrides de
  `UserAccountsRealmOptions` por `realmId`; entregar o resolver na composição/adapter, preservar o módulo puro e
  validar options inválidas antes do tráfego.
- [x] Cobrir dois realms com overrides distintos, fallback de realm sem override para os defaults e rejeição de
  configuração inválida, sem introduzir persistência/admin.
- [x] Não adicionar options, serviços ou validators que consultem estado de migrations.
- [x] Substituir guards que hoje proíbem qualquer referência EF no Server por guards que permitam adapters/providers
  PostgreSQL exigidos e continuem proibindo SQLite, Demo, `Data.*`, `Migrations` e dependências inversas.
- [x] Cobrir conexões ausentes/inválidas, três chaves iguais válidas, cleanup ausente e protection incompatível.

**Critérios de aceite:** cada `DbContext` do Server possui connection string PostgreSQL explícita; não existe
selector/options de provider; as três strings podem apontar para o mesmo banco local; cleanup ou Data Protection
ausentes/ambíguos falham antes de servir requests; nenhum erro contém secret; options de `UserAccounts` variam por
realm sem acoplar o módulo ao core; não existe inspeção de migrations; Server e `Tests.Host` usam
`UseRoyalIdentityProtocol(...)` sem compartilhar seus `Program`; `Tests.Host` não referencia
`RoyalIdentity.Server`; routing, realm discovery, authentication e antiforgery respeitam as precondições
documentadas; os guards refletem DF3/DF15/DF17-DF22/DF29.

**Testes:**

```powershell
dotnet build RoyalIdentity.Server/RoyalIdentity.Server.csproj
dotnet test Tests.Architecture
dotnet test Tests.Integration --filter "FullyQualifiedName~HostConfiguration"
```

### Resultado da Fase 1

**Entregáveis:**

- Criada a [ADR-019](../../adrs/ADR-019.md), que fixa Server PostgreSQL, Demo SQLite in-memory e composition roots
  independentes sem seletor de provider; o índice `ADR.md` foi atualizado.
- Criada a superfície `RoyalIdentity:Connections:{Configuration|Operational|UserAccounts}` com três options
  PostgreSQL independentes e validação real via `NpgsqlConnectionStringBuilder`. Valores iguais são aceitos e
  mensagens de erro não incluem a connection string.
- Vinculadas e validadas no startup as seções `RoyalIdentity:Snapshot`, `RoyalIdentity:Cleanup` e
  `RoyalIdentity:DataProtection`. Snapshot e cleanup reutilizam respectivamente
  `ConfigurationSnapshotRefreshOptions` e `OperationalCleanupOptions` como contrato de configuração; enquanto o
  backing do Server ainda é in-memory, os valores de snapshot/cleanup são validados, mas só passam a dirigir os
  serviços runtime na composição EF da Fase 3. Data Protection não expõe selector e rejeita configuração de provider
  alternativo.
- Criado `ConfigurationUserAccountsRealmOptionsResolver` na `.Integration`, com defaults globais, overrides em
  `RoyalIdentity:UserAccounts:Options:Realms:{realmId}`, cópias independentes e validação antecipada de todos os
  realms configurados.
- Criado `UseRoyalIdentityProtocol(...)` no core. Server e `Tests.Host` agora instalam explicitamente routing antes
  do extension e antiforgery/UI depois dele, sem compartilhar seus entry points.
- Atualizados os guards do grafo: Server admite somente core/Razor, adapters/providers PostgreSQL,
  `UserAccounts.Integration`/PostgreSQL e o fake transitório; SQLite, Demo, `Data.*`, Migrations, `Tests.*` e
  dependências inversas continuam proibidos. Um guard adicional impede inspeção/aplicação de migrations no source
  do Server.

**Arquivos principais:** `RoyalIdentity.Server/Configuration/*`, `RoyalIdentity.Server/HostServices.cs`,
`RoyalIdentity.Server/Program.cs`, `RoyalIdentity.Server/README.md`,
`RoyalIdentity/Extensions/ApplicationBuilderExtensions.cs`,
`RoyalIdentity.UserAccounts.Integration/ConfigurationUserAccountsRealmOptionsResolver.cs`,
`Tests.Integration/Configuration/HostConfigurationTests.cs`,
`Tests.Architecture/ConfigurationStorageBoundaryTests.cs`, `adrs/ADR-019.md` e `ADR.md`.

**Verificação:**

- `dotnet build RoyalIdentity.Server/RoyalIdentity.Server.csproj --no-restore` — verde, 0 erros.
- `dotnet test Tests.Architecture --no-restore` — 51 aprovados, 0 falhas, 0 ignorados.
- `dotnet test Tests.Integration --filter "FullyQualifiedName~HostConfiguration" --no-restore` — 13 aprovados,
  0 falhas, 0 ignorados.
- `dotnet test Tests.Integration --no-restore` — 282 aprovados, 0 falhas, 0 ignorados.

**Desvios e pendências:** nenhum desvio arquitetural. A referência
`Tests.Integration -> RoyalIdentity.Server`, necessária para validar a superfície de configuração, usa o alias
`RoyalIdentityServer`: sem ele, a compilação reproduziu `CS0433` entre `RoyalIdentity.Server.Program` e
`Tests.Host.Program`. `AddInMemoryStorage()` permanece deliberadamente no Server até a troca integral e testada da
Fase 3; nenhuma migration, consulta de schema ou composição EF runtime foi antecipada.

Há um efeito transitório aceito: `appsettings.json` mantém as três connections vazias para não versionar destino ou
secret, e `ValidateOnStart` faz `dotnet run --project RoyalIdentity.Server` falhar até que as três chaves sejam
fornecidas por ambiente/secret store. Os nomes são
`RoyalIdentity__Connections__Configuration__ConnectionString`,
`RoyalIdentity__Connections__Operational__ConnectionString` e
`RoyalIdentity__Connections__UserAccounts__ConnectionString`. Nesta fase esses valores, snapshot e cleanup formam
uma superfície validada, mas o backing ainda é in-memory; a Fase 3 torna a configuração efetiva e entrega o runbook
Podman.

---

## Fase 2 - provisionamento externo das três famílias

**Depende de:** Fase 1, DF8, DF9, DF12, DF14, DF15 e DF17-DF21.

**Escopo:** `RoyalIdentity.Migrations` e/ou runner da família `UserAccounts`, providers de `UserAccounts`, seeds,
scripts/README e testes de migration.

**O que/como:** estender `RoyalIdentity.Migrations` para aplicar migrations de Configuration, Operational e
`UserAccounts` fora do Server, preservando o suporte existente SQLite/PostgreSQL. Cada execução usa um único
provider nas três famílias e uma seleção de seed independente. PostgreSQL + Product e SQLite + Demo são as
combinações dos entry points oficiais, não limitações artificiais do runner genérico.

**Tarefas:**

- [x] Implementar a seleção explícita da família `UserAccounts` no runner escolhido sem acoplá-la ao gateway
  `IStorage`.
- [x] Estender `StorageFamilySelection`, `StorageMigrationReport` e o resultado/código de saída do runner para
  representar `UserAccounts` explicitamente, preservando os modos de seed `None|Product|Demo|All`.
- [x] Reescrever `MigrationsRunner_ProjectGraph_References_Providers_Only` como allowlist dos providers EF
  SQLite/PostgreSQL do core e de `UserAccounts`, continuando a proibir Demo e referências diretas a
  `RoyalIdentity/RoyalIdentity.csproj` e `Tests.*`.
- [x] Registrar junto do guard a justificativa de ADR-013: o runner é composition root das duas famílias
  independentes e não traduz tipos entre elas; portanto seu papel não é o adapter `.Integration`.
- [x] Aceitar provider uniforme por execução e connection string independente por `DbContext`, conforme DF19,
  preferindo secrets por variáveis de ambiente; permitir que as três apontem para o mesmo banco.
- [x] Preservar os modos SQLite/PostgreSQL e rejeitar qualquer topologia mixed-provider antes de I/O.
- [x] Cobrir no parsing/composição que provider e seed são seleções independentes: não inferir nem rejeitar um modo
  apenas por ser SQLite/PostgreSQL, preservando as validações próprias dos dados exigidos por cada seed.
- [x] Aplicar migrations das famílias selecionadas em ordem documentada, sem transação distribuída.
- [x] Retornar status independente por família e preservar códigos de saída não zero em falha parcial.
- [x] Manter a única implementação `ConfigurationSeed` com modos `None|Product|Demo|All` idempotentes e separados de
  migration. Não acoplar modo de seed ao provider: os entry points oficiais selecionam PostgreSQL + Product e
  SQLite + Demo, enquanto o runner continua genérico.
- [x] Provar segunda execução idempotente, banco compartilhado e bancos separados em SQLite/PostgreSQL.
- [x] Provar que falha na terceira família não reporta rollback inexistente das anteriores.
- [x] Atualizar a documentação do(s) runner(s) com comandos que não exponham connection strings/chaves.

**Critérios de aceite:** banco vazio pode receber os três schemas somente pelo(s) comando(s) externo(s); execução
repetida é idempotente; cada família tem resultado identificável; banco compartilhado não colide histories/tabelas;
SQLite e PostgreSQL permanecem verdes sem permitir mistura de providers numa execução; a seleção de seed permanece
explícita e independente do provider, com Product/Demo mutuamente exclusivos apenas nos entry points oficiais;
`RoyalIdentity.Server` não referencia nem chama o runner.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~MigrationRunner|FullyQualifiedName~Migration"
dotnet test Tests.UserAccounts --filter "FullyQualifiedName~Migration"
dotnet test Tests.Architecture
```

### Resultado da Fase 2

**Entregáveis:**

- `RoyalIdentity.Migrations` agora seleciona e reporta Configuration, Operational e `UserAccounts` explicitamente,
  na ordem Configuration → Operational → UserAccounts e sem transação distribuída. A falha de uma família em
  topologia separada não invalida nem impede as demais; em banco declarado `Shared`, a falha interrompe as
  seguintes sem alegar rollback. Esse fail-stop considera qualquer seleção com mais de uma família, inclusive
  `Configuration | Operational`, e não apenas o valor agregado `All`.
- O CLI ganhou `--provider` uniforme, `--user-accounts-connection[-env]` e seleção parcial por lista em
  `--families`. Cada família selecionada exige sua própria connection string, mesmo quando os três valores apontam
  para o mesmo banco. `--configuration-provider` permanece somente como alias compatível de `--provider`;
  seletores de provider por família não existem, tornando mixed-provider irrepresentável. O valor público
  `StorageFamilySelection.All` agora inclui `UserAccounts`; chamadores anteriores precisam fornecer a terceira
  connection ou selecionar explicitamente `Configuration | Operational`, como faz a fixture EF transitória.
- O seed continua sendo a única implementação `ConfigurationSeed`, com `None|Product|Demo|All`, independente do
  provider e aplicável somente quando Configuration foi selecionada. O runner não cria seed ou write model de
  `UserAccounts`; o Demo fará isso pelos casos de uso públicos na Fase 3B.
- `UserAccounts` recebeu a history própria `__UserAccountsMigrationsHistory` em SQLite/PostgreSQL, inclusive nas
  extensions de DI e factories de design. Como Configuration e UserAccounts já usaram a history default, os
  bootstraps SQLite/PostgreSQL agora atribuem a tabela legada pelos migration ids antes de qualquer relocação:
  Configuration deixa uma history integralmente UserAccounts intacta para o bootstrap do módulo, que então a move.
  O outcome distingue ausência real (`NoHistory`) de uma history pertencente a outra família (`ForeignHistory`);
  history mista/ambígua falha fechado. A relocação de compatibilidade de UserAccounts permanece responsabilidade
  explícita do runner: uma composição futura que migre o módulo por fora deverá tratar a history legada antes de
  chamar `MigrateAsync`.
- O grafo de `RoyalIdentity.Migrations` referencia diretamente somente os quatro providers
  EF/`UserAccounts` SQLite/PostgreSQL. O guard registra a justificativa de ADR-013: o runner é composition root das
  famílias independentes, não adapter de tradução entre core e módulo.
- O README do runner documenta banco compartilhado, três bancos, seleção parcial, seeds/protectors e uso de
  variáveis de ambiente sem secrets na linha de comando. O script PostgreSQL registra o aceite das três famílias.
  A fixture EF transitória de `Tests.Integration` continua selecionando somente Configuration + Operational até a
  migração integral da Fase 4.

**Arquivos principais:** `RoyalIdentity.Migrations/MigrationRunnerOptions.cs`,
`RoyalIdentity.Migrations/StorageMigrationRunner.cs`,
`RoyalIdentity.Migrations/UserAccountsMigrationsHistoryBootstrap.cs`,
`RoyalIdentity.Migrations/MigrationRunnerDiagnostics.cs`, `RoyalIdentity.Migrations/README.md`,
providers SQLite/PostgreSQL de `UserAccounts`,
`Tests.Storage/Operational/{StorageMigrationRunnerTests,PostgreSqlStorageMigrationRunnerTests}.cs`,
`Tests.Architecture/ConfigurationStorageBoundaryTests.cs` e `scripts/Test-OperationalPostgreSql.ps1`.

**Verificação:**

- `dotnet build RoyalIdentity.sln --no-restore` — verde, 0 erros; 7 warnings preexistentes nesta execução
  incremental de SDK/pacotes/código.
- `dotnet test Tests.Storage --filter "FullyQualifiedName~MigrationRunner|FullyQualifiedName~Migration"` —
  60 aprovados, 0 falhas, 19 PostgreSQL ignorados sem opt-in.
- `dotnet test Tests.UserAccounts --filter "FullyQualifiedName~Migration"` — 4 aprovados, 0 falhas,
  1 PostgreSQL ignorado sem opt-in.
- `dotnet test Tests.Architecture --no-restore` — 51 aprovados, 0 falhas.
- `scripts/Test-OperationalPostgreSql.ps1 -Filter "FullyQualifiedName~PostgreSqlStorageMigrationRunnerTests"` —
  3 aprovados contra PostgreSQL 17 real em Podman, cobrindo banco compartilhado, history legada de UserAccounts
  no banco compartilhado e três bancos separados.
- `scripts/Test-OperationalPostgreSql.ps1 -Filter "FullyQualifiedName~PostgreSqlOperationalMigrationTests"` —
  14 aprovados contra PostgreSQL 17 real em Podman, incluindo history estrangeira, coexistência válida e history
  mista com falha fechada.
- `scripts/Test-UserAccountsPostgreSql.ps1` — 1 migration acceptance aprovado contra PostgreSQL 17 real,
  cobrindo a extension de DI e a history dedicada do módulo.
- Suítes completas: `Tests.Storage` 586 aprovados/43 opt-in ignorados; `Tests.UserAccounts`
  194 aprovados/1 opt-in ignorado; `Tests.Integration` 282 aprovados, sem falhas.

**Desvios e pendências:** nenhum desvio arquitetural. `RoyalIdentity.Server` continua sem referência ou chamada ao
runner. Os entry points oficiais PostgreSQL + Product e SQLite + Demo só serão materializados na Fase 3; a Fase 2
mantém deliberadamente o runner genérico e não antecipa composição de host.

---

## Fase 3 - composições reais e fail-fast do Server/Demo

**Depende de:** Fases 1-2, DF3, DF4, DF8-DF13, DF15, DF17-DF23 e DF27.

**Escopo:** `RoyalIdentity.Server`, novo `RoyalIdentity.Demo`, `RoyalIdentity.Migrations`, providers PostgreSQL/SQLite,
`.Integration`/providers de `UserAccounts`, startup validators, appsettings/runbook local, solução,
`Tests.Architecture` e testes de host.

**O que/como:** trocar `AddInMemoryStorage()` do Server pela composição PostgreSQL integral e criar o Demo como
composition root SQLite integral e self-provisioned. Ambos registram snapshot, resource bridge,
Operational/profiles, cleanup, gateway e `UserAccounts`; somente o Demo invoca o runner compartilhado no startup.

**Sequência interna da fase:**

1. **Fase 3A — Server PostgreSQL:** composição, validação sem I/O e retirada do fake.
2. **Fase 3B — Demo SQLite:** projeto/shell web mínimo, composição fixa, migrations, seed e fluxo OIDC.
3. **Fase 3C — execução local:** runbook Podman → runner → Server.

**Tarefas:**

- [x] Referenciar no Server somente EF/PostgreSQL, `UserAccounts.Integration`/PostgreSQL, Razor e core; proibir
  SQLite, Demo, Migrations, InMemory e `Data.*`.
- [x] Substituir os top-level statements do Server por um entry point público nomeado
  `RoyalIdentity.Server.ServerProgram`, não estático e com `Main` estático. Não manter ao lado dele um `Program`
  global gerado implicitamente; permitir `WebApplicationFactory<RoyalIdentity.Server.ServerProgram>` e deixar
  somente o `Program` de `Tests.Host` no namespace global.
- [x] Depois da conversão para `ServerProgram`, remover o alias `RoyalIdentityServer` do `ProjectReference` de
  `Tests.Integration`, os `extern alias` consumidores e o guard transitório que exige esse alias. Substituí-lo por
  guard que prove o entry point nomeado e a ausência de `Program` global público/gerado no Server.
- [x] Adicionar em `Tests.Architecture` um teste default e sem I/O que monte a composição real do Server com
  Configuration, Operational e `UserAccounts` PostgreSQL e construa o provider com `ValidateOnBuild = true` e
  `ValidateScopes = true`, sem iniciar hosted services nem abrir conexão.
- [x] No teste do grafo produtivo, provar resolução única de `IStorage`/`IUserDirectory` e ausência de initializer
  SQLite demo, runner ou serviço que execute migration/seed; permitir referência de teste ao Server somente para
  exercitar essa composition root.
- [x] Configurar ASP.NET Data Protection e o protector de signing keys compatível com o provisionamento, preservando
  DF20 e a futura substituição por KMS.
- [x] Registrar contexts PostgreSQL de Configuration/Operational com histories corretas e as três connection
  strings explícitas definidas na Fase 1.
- [x] Registrar snapshot source e materializar o `ConfigurationSnapshotRefreshOptions` concreto a partir da
  configuração já validada na Fase 1; remover o valor fixo do backing in-memory e manter o resource bridge sem
  profile ou branch Demo.
- [x] Alimentar `AddEntityFrameworkOperationalCleanup(...)` a partir do mesmo
  `OperationalCleanupOptions`/seção validada na Fase 1, garantindo que o modo usado para escolher o scheduler seja
  idêntico ao modo efetivo resolvido por `IOptions`; não usar literal duplicado.
- [x] Registrar Operational storage, profiles e exatamente um modo de cleanup.
- [x] Registrar o gateway `AddEntityFrameworkStorage()` completo.
- [x] Registrar `UserAccounts.PostgreSql`, fonte configurável de options por realm conforme DF23 e
  `AddUserAccountsForRoyalIdentity()`.
- [x] Criar `RoyalIdentity.Demo` como projeto web próprio na solução, similar ao Server na superfície HTTP/UI, mas
  sem referência entre os dois executáveis.
- [x] Referenciar diretamente no Demo EF/SQLite, `UserAccounts.Integration`/SQLite,
  `RoyalIdentity.Migrations`, Razor e core; proibir Server/InMemory e qualquer `ProjectReference`, uso em source,
  configuração ou registro de DI direto de PostgreSQL/`Data.*`. Aceitar e documentar que essas assemblies entram
  transitivamente por `RoyalIdentity.Migrations`, sem serem usadas pelo Demo.
- [x] Implementar composição fixa `AddRoyalIdentityDemo()` (ou nome equivalente), sem options de provider/conexão:
  Configuration + Operational compartilham SQLite in-memory nomeado/keep-alive e `UserAccounts` usa outro.
- [x] Abrir as conexões keep-alive nomeadas antes do provisionamento e implementar initializer do Demo que invoca
  `StorageMigrationRunner` com SQLite, as três famílias e `ConfigurationSeedMode.Demo` antes de liberar tráfego;
  resources usam a bridge e contas usam os casos de uso públicos de `UserAccounts`.
- [x] Manter o conjunto de realms do Demo deliberadamente mínimo: semear somente `demo_realm`, seus clients,
  resources, contas e signing keys; não usar `ConfigurationSeedMode.All` nem criar os realms internos `server`,
  `account` e `admin`.
- [x] Garantir que o seed do Demo crie signing keys efêmeras utilizáveis para todos os realms habilitados e que o
  runtime consiga desprotegê-las com o mesmo protector da execução, sem criar política de rotação.
- [x] Reutilizar a implementação `ConfigurationSeed` existente no runner; o Demo apenas seleciona o modo Demo e não
  duplica primitivas nem usa entidades `Data.*` em seu próprio source.
- [x] Configurar Data Protection efêmero do Demo em diretório temporário exclusivo por processo, com application
  name fixo e os mesmos purposes usados pelo protector runtime. Criar/abrir o key ring antes de invocar o runner,
  fazer initializer e runtime resolverem a configuração compatível durante toda a vida do host e remover o
  diretório temporário somente no teardown. Não expor option obrigatória ao usuário do Demo.
- [x] Escolher um modo de cleanup Operational fixo para o Demo, sem options obrigatórias para quem o executa.
- [x] Fazer Demo, Server e `Tests.Host` usarem `UseRoyalIdentityProtocol(...)`, mantendo UI/static
  files/antiforgery e detalhes de hosting em cada entry point.
- [x] Adicionar um teste estreito em `Tests.Integration` que hospede o `RoyalIdentity.Demo` real por
  `WebApplicationFactory<RoyalIdentity.Demo.DemoProgram>`, sem passar por `Tests.Host`, e execute o fluxo OIDC demo.
- [x] Implementar o entry point real como tipo público nomeado
  `RoyalIdentity.Demo.DemoProgram`, não estático e com `Main` estático, em vez de top-level statements +
  `public partial class Program` global. Isso permite usá-lo como argumento genérico da `WebApplicationFactory` e
  evita colisão `CS0433` com o `Program` global já exposto por `Tests.Host`; seguir o princípio do entry point
  nomeado `MigrationRunnerProgram`.
- [x] Provar por guard que Demo não referencia Server e não possui referência direta/uso em source/registro de
  PostgreSQL ou `Data.*`; aceitar explicitamente o caminho transitivo Demo → Migrations → providers/Data, sem
  seleção nem execução de PostgreSQL pelo Demo. Provar também que Server não referencia Demo/SQLite/Migrations e
  que não existe caminho inverso Migrations → Demo. Reutilizar a técnica de
  `ConfigurationStorageBoundaryTests.Core_DoesNotReference_DataOrAdapter`: combinar leitura dos
  `ProjectReference` diretos com `Assembly.GetReferencedAssemblies()` para detectar uso compilado real após a poda
  do compilador, sem rejeitar a dependência transitiva aceita.
- [x] Remover `AddInMemoryStorage()` e a referência `RoyalIdentity.Storage.InMemory` do Server.
- [x] Preservar as validações funcionais de snapshot/signing keys, sem implementar readiness de schema e sem
  `GetPendingMigrations*`, `EnsureCreated*`, `Migrate*` ou seed.
- [x] Validar todos os profiles Operational selecionados pelos realms do snapshot antes do tráfego.
- [x] Preservar bootstrap de snapshot, `SigningKeyStartupValidator` e ordem
  `UseRealmDiscovery` antes de `UseAuthentication`.
- [x] Cobrir protector incompatível, profile ausente, key inválida e `IUserSecurityStateProvider` exigido por
  policy; não criar teste de migrations pendentes no host.
- [x] Documentar e fornecer comando/script local para PostgreSQL 17 via Podman, execução prévia do runner e startup
  do Server; manter três chaves de conexão explícitas, ainda que apontem para o mesmo banco, e não versionar senha.
- [x] Cobrir startup do Demo a partir de memória vazia, provar que somente `demo_realm` foi semeado e executar um
  fluxo OIDC completo com conta real nesse realm.
- [x] No teste do Demo, comprovar que as signing keys criadas pelo initializer são desprotegidas pelo runtime e que
  o diretório temporário do key ring é removido somente depois do encerramento da factory.

**Critérios de aceite — 3A/Server:** inicia sobre PostgreSQL previamente provisionado e resolve exatamente um
`IStorage` EF e um `IUserDirectory` de `UserAccounts`; configuração inválida falha antes de aceitar request; o
projeto não referencia SQLite/Demo/InMemory/Migrations/Data e não executa migration/seed. A composição PostgreSQL
completa é construída por teste default com validação de scopes/build, sem I/O e sem serviços de bootstrap demo.
Seu entry point público é `RoyalIdentity.Server.ServerProgram`, hospedável diretamente por `WebApplicationFactory`
sem alias de assembly e sem declarar outro `Program` no namespace global.

**Critérios de aceite — 3B/Demo:** inicia sem configuração de storage sobre SQLite in-memory vazio, invoca o runner
com migrations + seed Demo, cria signing keys efêmeras desprotegíveis pelo runtime com key ring temporário por
processo e conclui um fluxo OIDC com conta real somente em `demo_realm`; `server`, `account` e `admin` não são
semeados. Seu `DemoProgram` público e nomeado é alcançável por
`WebApplicationFactory<RoyalIdentity.Demo.DemoProgram>` sem colidir com `Tests.Host.Program`. O projeto referencia
o runner reutilizável e aceita PostgreSQL/Data somente no grafo transitivo, sem referência direta, uso,
configuração, registro ou execução; não referencia o Server. O scaffolding duplicado limita-se ao shell de host,
enquanto protocolo e UI de contas permanecem compartilhados.

**Critérios de aceite — 3C/local:** resource bridge segue DF13 e o runbook Podman permite executar localmente
runner + Server com três connection strings explícitas e sem secret versionada.

**Testes:**

```powershell
dotnet build RoyalIdentity.Server/RoyalIdentity.Server.csproj
dotnet build RoyalIdentity.Demo/RoyalIdentity.Demo.csproj
dotnet test Tests.Architecture --filter "FullyQualifiedName~ServerPostgreSqlComposition|FullyQualifiedName~DemoSqliteComposition|FullyQualifiedName~ModuleBoundary"
dotnet test Tests.Integration --filter "FullyQualifiedName~HostComposition|FullyQualifiedName~HostStartup|FullyQualifiedName~SqliteDemo"
```

### Resultado da Fase 3

Concluída em 2026-07-27. O `RoyalIdentity.Server` passou a ser um composition root exclusivamente PostgreSQL,
com entry point nomeado `ServerProgram`, três conexões explícitas, Configuration/Operational EF,
`UserAccounts` real, Data Protection, cleanup e validações fail-fast; não referencia nem executa
SQLite/InMemory/Migrations/Demo. O grafo completo é construído sem I/O com `ValidateOnBuild`/`ValidateScopes`.

Foi criado o executável independente `RoyalIdentity.Demo`, fixo e sem configuração de storage: Configuration e
Operational compartilham um SQLite in-memory nomeado, `UserAccounts` usa outro, e um initializer abre os
keep-alives, executa o runner reutilizável, seleciona somente o seed Demo e cria a conta real pelo caso de uso
público do módulo. O key ring Data Protection é exclusivo da execução e removido no teardown. O teste
`SqliteDemo` provou startup vazio, somente `demo_realm`, login, authorization code, emissão de access/ID token e
compatibilidade da signing key semeada com o runtime.

O teste real `scripts/Test-ServerPostgreSql.ps1` executou PostgreSQL 17 via Podman, aplicou as três famílias pelo
runner e iniciou o Server com sucesso. Essa validação revelou dois defeitos mascarados pelo SQLite/fake e ambos
foram corrigidos: uma versão intermediária da nova sobrecarga de conexão explícita de `UserAccounts` registrava o
contexto em pool, incompatível com o `IDomainEventDispatcher` scoped consumido pelo seu construtor, e o
`SigningKeyStartupValidator` consultava keys enquanto o reader de realms ainda estava aberto no mesmo contexto
Npgsql. Também foi adicionado fail-fast para profiles Operational selecionados e indisponíveis.

Comandos finais: `dotnet build RoyalIdentity.sln --no-restore` (sucesso);
`dotnet test Tests.Architecture/Tests.Architecture.csproj --no-build` (56/56);
`dotnet test Tests.Storage/Tests.Storage.csproj --no-restore` (589 aprovados, 43 opt-in ignorados);
`dotnet test Tests.Integration/Tests.Integration.csproj --no-build` (283/283);
`dotnet test Tests.UserAccounts/Tests.UserAccounts.csproj --no-restore` (194 aprovados, 1 opt-in ignorado); e
`./scripts/Test-ServerPostgreSql.ps1` (PostgreSQL 17 + runner + Server aprovados).

Revisão posterior prendeu o segundo defeito com um aceite PostgreSQL opt-in: o
`SigningKeyStartupValidator` percorre dois realms habilitados no gateway EF real sem sobrepor comandos Npgsql.
O atalho público `AddUserAccountsPostgreSql` também ganhou uma guarda de composição com
`ValidateOnBuild`/`ValidateScopes`, incluindo o adapter `AddUserAccountsForRoyalIdentity`; ela confirmou que o
registro do WorkContext e a integração completa não reproduzem o captive dependency do `DbContextPool` descartado
na implementação intermediária da sobrecarga explícita. Verificação posterior:
`./scripts/Test-ConfigurationPostgreSql.ps1` (44/44 PostgreSQL);
`dotnet test Tests.Storage/Tests.Storage.csproj --no-restore` (589 aprovados, 44 opt-in ignorados); e
`dotnet test Tests.UserAccounts/Tests.UserAccounts.csproj --no-restore` (195 aprovados, 1 opt-in ignorado).

---

## Fase 4 - fixture SQLite unificada, handles e seeds

**Depende de:** Fases 2-3, DF5, DF13, DF14, DF16, DF25, DF26 e DF29.

**Escopo:** `Tests.Host`, `Tests.Integration/Prepare`, `Tests.UserAccounts/UserAccountsModuleSeed.cs`, helpers de
Configuration/Operational e resource bridge test-only.

**O que/como:** criar uma factory integral SQLite in-memory com Configuration + Operational migrados e
`UserAccounts` real. Configuration e Operational compartilham o mesmo banco SQLite nomeado da factory, com
connection strings/registrations explícitos e histories distintas; `UserAccounts` usa banco/keep-alive separado.
Expor dados por handles neutros e operações explícitas de setup; não substituir um acesso ao fake por outro static
global.

**Tarefas:**

- [ ] Medir e registrar, com comando e ambiente, o tempo da suíte `Tests.Integration` ainda fake e o startup
  cold/warm da factory persistente. Usar warm-up e três execuções `--no-build`, registrar cada valor e a mediana.
- [ ] Fixar no `Resultado da Fase 4`, antes da migração em massa, um limiar numérico de regressão material e sua
  justificativa; a comparação da Fase 6 usa exatamente o mesmo protocolo e ambiente comparável.
- [ ] Manter `Tests.Host.Program` como composition root independente conforme DF29; remover
  `AddInMemoryStorage()` e sua referência ao projeto fake, deixando o projeto storage-agnóstico.
- [ ] Remover ou substituir os launch profiles de `Tests.Host` que o apresentam como executável standalone e
  atualizar qualquer script/documentação equivalente; depois desta fase o projeto só inicia por uma
  `WebApplicationFactory` que registra o backing antes do startup.
- [ ] Fazer cada factory registrar diretamente uma única implementação dos contratos do IdP. Enquanto houver
  consumers legados, sua factory registra explicitamente o fake; a persistente registra EF + `UserAccounts`.
- [ ] Fazer a factory persistente construir o service provider com `ValidateScopes` e `ValidateOnBuild`.
- [ ] Criar por factory um banco SQLite in-memory nomeado/keep-alive compartilhado por Configuration e Operational;
  registrar connection string própria em cada `DbContext`, aplicar suas migrations/histories distintas antes do
  host e manter a conexão aberta até o teardown.
- [ ] Criar outro banco SQLite in-memory/keep-alive para `UserAccounts`, preservando sua ownership e migrations
  próprias.
- [ ] Registrar Configuration + Operational EF, `UserAccounts` SQLite e cleanup `External` na fixture.
- [ ] Usar protectors determinísticos test-only sem variável de ambiente process-global compartilhada.
- [ ] Semear Configuration demo/teste, Alice/Bob e property scopes por owner correto.
- [ ] Mover `AliceSubjectId`/`BobSubjectId` para o seed test-only e remover seu import de InMemory.
- [ ] Expor handles imutáveis para realms internos/demo, clients, resources e subjects usando somente ids
  primitivos, paths e valores provider-neutral; nenhum handle pode conter `Realm`.
- [ ] Obter qualquer objeto `Realm` usado pelo teste via `IRealmStore`/snapshot depois do seed e dentro da
  composição corrente.
- [ ] Criar helper interno exclusivamente em `Tests.Integration/Prepare` que persiste clients pelo
  `ConfigurationSqliteDbContext` e `ClientMaterializer` existentes, salva a alteração e chama
  `IConfigurationSnapshotRefresher` antes do request. Expor aos cenários somente a operação/handles
  provider-neutral, sem criar contrato público de escrita nem write model administrativo.
- [ ] Criar source/hook explícito para resources/scopes voláteis, sem nova tabela/contrato público.
- [ ] Criar operações test-only de conta via features reais do módulo para seed, claims e activate/deactivate.
- [ ] Criar setup Operational focado apenas onde a API pública não permite preparar o cenário.
- [ ] Provar smoke de discovery, login, authorize, token e sessão na composição integral.
- [ ] Provar que `ValidateScopes`/`ValidateOnBuild` faz captive dependency falhar no startup da factory, em vez de
  produzir falha tardia/intermitente.
- [ ] Garantir teardown de arquivos/conexões e ausência de contaminação entre duas factories paralelas.

**Critérios de aceite:** uma factory inicia sem resolver `MemoryStorage`; os três backings reais estão presentes;
Alice/Bob mantêm subjects determinísticos; writes de client são visíveis após refresh; resources usam a bridge;
duas fixtures não compartilham DB mutável, env var ou handle estático; nenhum handle contém `Realm`; captive
dependencies falham durante a construção; um fluxo OIDC completo passa; baseline e startup cold/warm estão
registrados com protocolo, mediana e limiar numérico. `Tests.Host` não registra nem referencia qualquer backing e
não oferece launch profile standalone; a suíte HTTP cobre Configuration + Operational no mesmo banco com histories
distintas e `UserAccounts` em banco separado.

**Testes:**

```powershell
dotnet test Tests.UserAccounts --filter "FullyQualifiedName~UserAccountsModuleSeed"
dotnet test Tests.Integration --filter "FullyQualifiedName~EntityFrameworkStorageOidcFlow|FullyQualifiedName~PersistentStorage|FullyQualifiedName~ServiceProviderValidation"
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
- [ ] Antes de executar os filtros da fase, rodar
  `dotnet test Tests.Integration --no-build --list-tests`, provar que cada filtro seleciona ao menos um teste e
  registrar no resultado as contagens esperada e executada.
- [ ] Classificar e registrar toda asserção alterada nos quatro buckets de triagem; corrigir produto/módulo quando
  aplicável, sem adaptar silenciosamente a expectativa ao backing real.

**Critérios de aceite:** todos os grupos listados executam somente sobre a factory integral; seus arquivos não
referenciam namespace/tipos do fake; alterações de conta passam por `UserAccounts`; writes de Configuration ficam
visíveis no snapshot; nenhuma asserção depende de live reference; cada filtro seleciona ao menos um teste e possui
contagens esperada/executada registradas; toda asserção alterada está classificada.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~Login|FullyQualifiedName~UserInfo|FullyQualifiedName~Claims|FullyQualifiedName~ActiveRule"
dotnet test Tests.Integration --filter "FullyQualifiedName~CodeAuthorize|FullyQualifiedName~CodeToken|FullyQualifiedName~ClientToken|FullyQualifiedName~Discovery|FullyQualifiedName~Jwk|FullyQualifiedName~SigningAlgorithm"
```

### Resultado da Fase 5

*a preencher*

---

## Fase 6 - migração dos fluxos restantes e troca do default

**Depende de:** Fase 5, DF2, DF5, DF7, DF13, DF14, DF25 e DF26.

**Escopo:** refresh/revocation, logout/session, UI/consent, realm isolation, demais caracterizações,
`AppFactory` e factories opt-in parciais.

**O que/como:** migrar os grupos restantes, eliminar preparações específicas do fake e somente então deixar a
factory integral como única composição canônica das 29 classes.

**Tarefas:**

- [ ] Migrar refresh token, claims mode e revocation sem `UpdateAsync` manual de cenário.
- [ ] Substituir limpeza global de access tokens pela remoção do JTI conhecido ou hook Operational test-only.
- [ ] Migrar end session, lifecycle de sessão, logout e revogação por subject.
- [ ] Capturar `sid` no próprio fluxo e consultar por id, sem scan de `UserSessions`.
- [ ] Migrar UI login/consent, issuer URI, eventos e isolamento por realm.
- [ ] Substituir `FakeSessionStorage` baseado em stores concretos por doubles locais de contratos ou gateway EF.
- [ ] Mudar cada classe uma única vez para a factory persistente e remover a factory/ramo legado assim que seu
  último consumer for migrado.
- [ ] Manter a factory persistente como única composição canônica, sem exigir renome; absorver/remover
  `EntityFrameworkStorageAppFactory` e `UserAccountsAppFactory` parciais.
- [ ] Remover o global using e todas as referências a `MemoryStorage`/`RealmMemoryStore` de `Tests.Integration`.
- [ ] Remover de `Tests.Integration` a referência temporária ao projeto fake criada para a coexistência da Fase 4.
- [ ] Executar busca estática/guard arquitetural que rejeite handles contendo `Realm` e confirme que realms usados
  nos testes são carregados da composição corrente.
- [ ] Antes de executar os filtros da fase, rodar
  `dotnet test Tests.Integration --no-build --list-tests`, provar que cada filtro seleciona ao menos um teste e
  registrar no resultado as contagens esperada e executada.
- [ ] Classificar e registrar toda asserção alterada nos quatro buckets de triagem.
- [ ] Executar toda a suíte HTTP sobre o novo default antes de tocar nos contratos atômicos.
- [ ] Medir, no mesmo ambiente/protocolo da Fase 4, warm-up e três execuções `--no-build` da suíte persistente
  completa; registrar cada valor, mediana, razão contra o baseline e comparação com o limiar numérico fixado.
- [ ] Se a mediana ultrapassar o limiar, tratar ou aceitar explicitamente a regressão com causa/evidência antes de
  fechar a fase; abaixo do limiar, não criar otimização especulativa.

**Critérios de aceite:** as 29 classes antes ligadas a `AppFactory` executam sobre EF + `UserAccounts`; não existem
factories parciais; `Tests.Integration` não contém uso de `MemoryStorage`, getters do fake ou mutação de dictionary;
todos os filtros executam ao menos um teste e têm suas contagens registradas; toda mudança de asserção está
classificada; a comparação de duração está registrada; todos os fluxos e caracterizações permanecem verdes; os
fallbacks ainda não foram ampliados nem acionados pelo EF; a referência temporária ao projeto fake foi removida; a
mediana observada está dentro do limiar numérico da Fase 4 ou possui tratamento/aceite explícito registrado.

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

**Escopo:** `Tests.Storage`, `Tests.UserAccounts`, `Tests.Architecture` e referências de projeto restantes.

**O que/como:** retirar todos os consumidores que ainda obrigariam o projeto fake a compilar depois da quebra
definida em DF28.
Preservar cobertura sem transformar doubles locais em outro backing geral.

**Tarefas:**

- [ ] Adicionar a variante EF completa de `StorageSessionContractTests`.
- [ ] Substituir `CompositeStorageSessionTests` Configuration EF + Operational fake pelo gateway EF completo.
- [ ] Remover fallbacks de stores fake do harness SQLite Configuration; usar gateway EF ou doubles locais focados.
- [ ] Remover as 11 variantes `InMemory` e `InMemoryStorageHarness` de `Tests.Storage`.
- [ ] Registrar antes da remoção a quantidade de testes concretos exposta pelas 11 variantes `InMemory` e, depois,
  a quantidade/cobertura dos substitutos EF; anotar o comando ao lado de cada número.
- [ ] Substituir em `OperationalContractsShapeTests` os casos que usam o fake por doubles locais de caracterização
  do contrato ainda transitório; registrar as asserções que serão removidas/reformuladas na Fase 8, sem antecipar
  DF28.
- [ ] Remover a especialização `InMemory` de `UserDirectoryContractTests`.
- [ ] Tornar os realms da variante `UserAccountsSqlite` independentes de `MemoryStorage`.
- [ ] Remover referências ao fake de `Tests.Storage` e `Tests.UserAccounts`; confirmar que Server, `Tests.Host` e
  `Tests.Integration` já foram limpos nas Fases 3, 4 e 6.
- [ ] Substituir o teste arquitetural do grafo do fake por allowlist genérica de dependências, sem conservar o nome
  literal do projeto removido.
- [ ] Mapear cada teste concreto removido para cobertura EF/módulo equivalente e registrar qualquer perda real.
- [ ] Classificar pelos quatro buckets toda divergência de asserção encontrada nesta fase.

**Critérios de aceite:** somente o próprio projeto `RoyalIdentity.Storage.InMemory` e a entrada na solução permanecem;
nenhum projeto de produção/teste o referencia; contratos de core e `UserDirectory` rodam sobre providers reais;
`IStorageSession` possui cobertura EF; contagens anterior/substituta e comandos estão registrados; não houve perda de
cenário sem substituição registrada; mudanças de asserção foram triadas.

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

**Depende de:** Fase 7, DF6, DF7, DF28 e DF30.

**Escopo:** contratos/stores/handlers do core, adapter EF Operational, shape tests, solução e
`RoyalIdentity.Storage.InMemory`.

**O que/como:** aplicar a quebra pública definida em DF28 e excluir o fake no mesmo corte. O código intermediário não
precisa suportar uma implementação sem atomicidade; a branch deve voltar a compilar e testar antes de encerrar a
fase.

**Tarefas:**

- [ ] Tornar consumo de authorization code single-use uma dependência obrigatória de compilação.
- [ ] Tornar transição de refresh token versionada/condicional uma dependência obrigatória de compilação.
- [ ] Fazer `LoadCode` consumir diretamente o `IAuthorizationCodeStore` realm-bound e remover
  `IAuthorizationCodeConsumer`, `DefaultAuthorizationCodeConsumer`, seus registros, casts, logging de fallback e
  get-then-remove.
- [ ] Fazer `RefreshTokenHandler` consumir diretamente o `IRefreshTokenStore` realm-bound e remover
  `IRefreshTokenConsumer`, `DefaultRefreshTokenConsumer`, seus registros, casts, fallback não condicional e
  `IRefreshTokenStore.UpdateAsync`.
- [ ] Remover `ISingleUseAuthorizationCodeStore`, `IVersionedRefreshTokenStore` e composites redundantes conforme
  DF28.
- [ ] Preservar no `RefreshTokenHandler` a tolerância pós-consumo existente e provar que a retirada da indireção não
  altera sua janela/política.
- [ ] Atualizar adapter EF, mocks/doubles locais e testes de shape para o contrato final.
- [ ] Preservar testes concorrentes de code single-use e refresh transition/tolerance.
- [ ] Remover `AddInMemoryStorage`, extensões, facades e todos os arquivos de
  `RoyalIdentity.Storage.InMemory`.
- [ ] Remover o projeto da solução, props/referências e guards históricos restantes.
- [ ] Executar busca estática em código/projetos/solução para provar ausência do fake e dos fallbacks.

**Critérios de aceite:** nenhum handler possui ramo não atômico; as duas interfaces/classes consumer e
`IRefreshTokenStore.UpdateAsync` não existem; handlers dependem dos stores base realm-bound; a composição EF
satisfaz os contratos em compile time; nenhuma referência, símbolo ou projeto InMemory permanece em
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
rg -n "ISingleUseAuthorizationCodeStore|IVersionedRefreshTokenStore|IAuthorizationCodeConsumer|IRefreshTokenConsumer|DefaultAuthorizationCodeConsumer|DefaultRefreshTokenConsumer|fallback|UpdateAsync" RoyalIdentity/Contracts RoyalIdentity.Storage.EntityFramework Tests.Storage
```

Para as duas buscas `rg`, o resultado esperado deve ser documentado na fase: zero para símbolos removidos; menções
legítimas a `UpdateAsync` não relacionadas a refresh ou a texto histórico devem ser classificadas explicitamente.

### Resultado da Fase 8

*a preencher*

---

## Fase 9 - PostgreSQL, regressão final e fechamento documental

**Depende de:** Fase 8 e DF1-DF30.

**Escopo:** aceites PostgreSQL, suíte completa, `ADR-018`, foundations, roadmap/macro, backlog, README de migrations,
`AGENTS.md` e este plano.

**O que/como:** executar localmente a evidência PostgreSQL definida em DF24, fechar regressão de solução e alinhar
toda documentação ao estado real. Não criar CI nem marcar conclusão com teste obrigatório não executado.

**Tarefas:**

- [ ] Provisionar PostgreSQL 17 efêmero com porta dinâmica não padrão e as três famílias.
- [ ] Executar startup/fluxo OIDC PostgreSQL local/opt-in conforme DF24 e registrar comando, contagens e skips.
- [ ] Executar novamente contratos, migrations, concorrência e gateway SQLite/PostgreSQL afetados.
- [ ] Executar `dotnet build` e `dotnet test` da solução completa.
- [ ] Atualizar ADR-018 adicionando uma seção `## 4. Revisão` que registre a conclusão da migração e a remoção
  efetiva do fake, sem reescrever o corpo da decisão, conforme a regra de revisão de `ADR.md`.
- [ ] Atualizar o índice de ADRs em `ADR.md`, que ainda lista somente ADR-001 a ADR-009.
- [ ] Atualizar `product.md`, `tech.md`, `structure.md` e `architecture.md` onde ainda descrevem o default antigo.
- [ ] Atualizar `plans-roadmap-02.md`, `plan-data-macro.md`, backlog e `AGENTS.md` com Plano 4 concluído e próximo
  item real.
- [ ] Atualizar README/scripts do provisionamento PostgreSQL com três conexões/famílias, protection, cleanup,
  Podman local e execução obrigatória do runner antes do Server.
- [ ] Criar/atualizar README do `RoyalIdentity.Demo` com o comando direto, ausência de configuração de storage e o
  caráter efêmero do SQLite in-memory: cada restart perde usuários, consents, tokens e sessões.
- [ ] Confirmar que documentação histórica não é apresentada como instrução vigente.
- [ ] Preencher `Resultado da Fase` de todas as fases, riscos, desvios e pendências.
- [ ] Marcar o plano `CONCLUIDO` e atualizar a barra somente após todas as decisões e gates de aceite.

**Critérios de aceite:** evidência PostgreSQL cumpre DF24; solução completa verde; nenhuma instrução vigente indica
InMemory como default; ADR-018 registra a consequência já realizada; documentação operacional permite provisionar e
iniciar o host sem secret em linha de comando; o README não sugere durabilidade no demo in-memory; Q13 foi
convertida em DF17/DF18/DF21/DF27 e não restam perguntas abertas; todas as decisões fechadas foram aplicadas.

**Testes:**

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

Executar também o script PostgreSQL definido/atualizado pela fase e registrar o comando exato em
`Resultado da Fase 9`. CI permanece fora de escopo por DF24.

### Resultado da Fase 9

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(es) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| Objetivo 1 — Server integral sem fake | 1-3 | DF3, DF4, DF8-DF13, DF15, DF17-DF23 | Server exclusivamente PostgreSQL; exatamente um gateway EF e um `IUserDirectory` real; zero SQLite/Demo/Migrations no grafo | `HostConfiguration`, `ServerPostgreSqlComposition`, `HostStartup`, `Tests.Architecture` |
| Objetivo 2 — provisionamento e Demo | 1-3 | DF8, DF9, DF11, DF12, DF14, DF17-DF22, DF27 | runner genérico SQLite/PostgreSQL para três famílias; Demo com entry point nomeado reutiliza runner + seed exclusivo de `demo_realm`, aceita PG/Data somente transitivos e compartilha key ring efêmero entre initializer/runtime | testes de migration/runner, `DemoSqliteComposition` e fluxo OIDC demo |
| Objetivo 3 — testes default reais | 4-6 | DF2, DF5, DF13, DF14, DF16, DF25, DF26, DF29 | 29 consumers sobre factory integral; zero uso do fake em `Tests.Integration`; `Tests.Host` permanece independente | grupos HTTP + `--list-tests` + `dotnet test Tests.Integration` |
| Objetivo 4 — remover transição/fake | 7-8 | DF6, DF7, DF28, DF30 | atomicidade obrigatória nos contratos base; handlers chamam stores realm-bound; zero consumer vazio/fallback/projeto/referência fake | `Tests.Identity`, `Tests.Storage`, concorrência e buscas `rg` |
| Objetivo 5 — paridade e fechamento | 9 | DF1, DF2, DF16, DF24 | aceite PostgreSQL local cumprido, solução/doc normativa verde | script PostgreSQL + `dotnet test RoyalIdentity.sln` |

---

## Invariantes a preservar

1. Todo dado de client, key, conta, sessão, token, consent ou configuração permanece realm-scoped.
2. `RoyalIdentity` não referencia providers, Server, `UserAccounts` ou qualquer fake.
3. `RoyalIdentity.Pipelines` permanece sem dependência de domínio.
4. `Data.*` permanece puro e só é adaptado por `RoyalIdentity.Storage.EntityFramework`.
5. `UserAccounts` puro não referencia o core; somente `.Integration` conhece os dois lados.
6. O Server não referencia SQLite, Demo, `Data.*`, `RoyalIdentity.Migrations` ou `Tests.*`.
7. O processo web produtivo nunca aplica migration, `EnsureCreated` ou seed; `RoyalIdentity.Demo` é outro
   executável, fixo em SQLite in-memory e self-provisioned por DF27.
8. Configuration, Operational e `UserAccounts` mantêm migrations/histories/resultado próprios.
9. Não há transação global nem promessa de rollback conjunto entre famílias.
10. Cleanup possui exatamente um modo explícito.
11. Plain nunca é default e proteção ausente/incompatível falha fechado.
12. O Server consegue desproteger as signing keys provisionadas externamente e nunca cria/rotaciona esse material;
    o Demo cria signing keys efêmeras no seed de cada execução e também não implementa rotação runtime.
13. Resources/scopes permanecem voláteis por DF13.
14. Authorization codes são single-use sob concorrência real.
15. Refresh transitions são condicionais e preservam a tolerância pós-consumo vigente.
16. Nenhuma nova paridade é adicionada ao fake durante sua janela restante.
17. Setup de conta usa o módulo ou seam test-only; nunca live reference de entidade.
18. Write de Configuration usado por teste fica no helper interno de `Tests.Integration/Prepare`, usa
    `ConfigurationSqliteDbContext`/`ClientMaterializer`, não cria contrato público e é seguido de refresh explícito
    do snapshot.
19. Fixtures não compartilham DB, secret store, env var mutável ou handle estático.
20. `UseRealmDiscovery` continua antes de `UseAuthentication`.
21. Validators continuam sinalizando falhas esperadas por `context.Response`, sem lançar por erro de protocolo.
22. A fase de exclusão não remove cobertura sem mapear seu provider/teste substituto.
23. Handles de fixture não contêm `Realm`; objetos de realm usados por testes vêm do store/snapshot da composição
    corrente.
24. Cada composition root resolve exatamente uma implementação dos contratos de storage que pretende exercitar.
25. Toda mudança de asserção durante a migração possui classificação e evidência nos quatro buckets de triagem.
26. `Tests.Host` e `RoyalIdentity.Demo` não referenciam nem executam `RoyalIdentity.Server`; os três hosts
    compartilham somente extensions provider-neutral e contratos/UI aplicáveis.
27. Depois da Fase 4, `Tests.Host` só inicia por factory que fornece storage; nenhum launch profile o anuncia como
    host standalone.
28. Configuration e Operational compartilham o banco SQLite da fixture HTTP, com histories distintas;
    `UserAccounts` conserva banco/ownership próprios.
29. O Server não possui seletor de provider e usa somente PostgreSQL; o runner preserva SQLite/PostgreSQL com um
    provider uniforme por execução e seleção de seed independente; os entry points oficiais usam respectivamente
    SQLite + Demo e PostgreSQL + Product.
30. Reset destrutivo de dados não é cleanup Operational nem option de runtime EF; permanece fora deste plano.
31. O Demo aceita PostgreSQL/`Data.*` somente como dependências transitivas de `RoyalIdentity.Migrations`; não
    possui referência direta, uso em source, configuração, registro de DI nem execução desses providers.
32. O key ring Data Protection do Demo é temporário e exclusivo por processo, mas application name e purposes são
    fixos e compartilhados entre initializer e runtime durante toda a execução.
33. O entry point público do Demo é `RoyalIdentity.Demo.DemoProgram`; o projeto Demo não declara um `Program`
    público no namespace global que possa colidir com `Tests.Host.Program`.
34. O seed do Demo cria somente `demo_realm` e seus dados funcionais; os realms internos Product não são copiados
    para a experiência demo.
35. O entry point público do Server é `RoyalIdentity.Server.ServerProgram`; `Tests.Integration` referencia o
    assembly normalmente, sem `extern alias`, e somente `Tests.Host` mantém um `Program` no namespace global.

---

## Critérios globais de conclusão

- DF17-DF30 foram aplicadas e eventuais desvios estão registrados nos resultados das fases.
- `RoyalIdentity.Server` inicia exclusivamente sobre PostgreSQL e não oferece selector/fallback SQLite/in-memory.
- `RoyalIdentity.Server.ServerProgram` é hospedável diretamente por `WebApplicationFactory` sem alias de assembly
  nem `Program` global concorrente.
- `RoyalIdentity.Demo` inicia sem configuração de storage sobre SQLite in-memory, invoca o runner compartilhado com
  o modo Demo, usa o mesmo key ring temporário no initializer/runtime e conclui o fluxo OIDC em `demo_realm`, único
  realm semeado. `RoyalIdentity.Demo.DemoProgram` é hospedável sem colisão com `Tests.Host.Program`. Dependências
  PostgreSQL/`Data.*` são apenas transitivas pelo runner, sem uso/configuração/registro/execução pelo Demo.
- As três famílias PostgreSQL são provisionáveis externamente; o host produtivo não contém inspeção ou execução de
  migrations, e somente o executável Demo possui a exceção DF27.
- `Tests.Integration` roda integralmente sobre EF/SQLite + `UserAccounts`.
- A duração antes/depois da suíte está registrada com protocolo, comandos, medianas e limiar numérico; o resultado
  final está dentro desse limiar ou possui tratamento/aceite explícito.
- Nenhum código de teste resolve `MemoryStorage`, `RealmMemoryStore` ou stores concretos do fake.
- Nenhum handler detecta capability atômica opcional ou executa fallback não atômico; as indireções consumer vazias
  de code/refresh foram removidas.
- `RoyalIdentity.Storage.InMemory` não existe na solução nem no grafo de projetos.
- Resources/scopes continuam pela bridge e nenhuma semântica fechada na matriz foi reaberta.
- O aceite PostgreSQL local/opt-in definido em DF24 está verde e registrado; nenhuma CI foi criada por este plano.
- Foundations, ADR-018, roadmap, backlog, README e `AGENTS.md` refletem o estado final.
- `dotnet build RoyalIdentity.sln` está verde.
- `dotnet test RoyalIdentity.sln` está verde.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| Runner e host usam Data Protection incompatível | key ring, application name ou purposes divergem | signing keys/payloads ficam ilegíveis e host não sobe | DF20, configuração única e teste cross-process/instância | Aberto |
| Initializer e runtime do Demo usam key rings diferentes | diretório temporário é criado tarde, recriado ou removido antes do shutdown | signing keys do seed ficam ilegíveis ainda na mesma execução | criar key ring por processo antes do runner, compartilhar application name/purposes e remover somente no teardown | Aberto |
| Banco não foi provisionado antes do host | operador inicia Server sem executar o runner | falha funcional no primeiro bootstrap/acesso | runbook externo, erro sanitizado e nenhuma tentativa automática de migration | Aberto |
| Demo deixa de autenticar | initializer chama runner/seed sem accounts/resources/signing keys compatíveis | `dotnet run` parece funcional, mas login/authorize falha | DF27, modo Demo do runner + seed de contas e fluxo OIDC completo a partir de memória vazia | Aberto |
| Entry points web colidem com `Tests.Host.Program` | Server ou Demo expõe outro `Program` no namespace global | `Tests.Integration` falha com `CS0433` ou exige aliases contagiosos para consumir tipos dos hosts | substituir top-level statements por `RoyalIdentity.Server.ServerProgram` e usar `RoyalIdentity.Demo.DemoProgram`, ambos públicos/não estáticos com `Main` estático; somente `Tests.Host` mantém `Program` global | Aberto |
| SQLite Demo desaparece após o runner | keep-alive é aberto depois das conexões transitórias do migration runner | schema/seed somem antes do startup | abrir os dois keep-alives nomeados antes de invocar o runner e mantê-los até o shutdown | Aberto |
| Bootstrap Demo vaza para o Server | composição/extension compartilhada inclui migration ou seed | processo produtivo altera schema/dados | executáveis irmãos, guards bidirecionais e extension compartilhado limitado ao protocolo | Aberto |
| Demo absorve ownership de `UserAccounts` | host manipula entidades/tabelas internas do módulo para semear contas | viola ADR-013/ADR-015 | Demo usa migrations do provider e casos de uso públicos do módulo | Aberto |
| Modos Product/Demo são confundidos | entry point seleciona o modo errado da implementação compartilhada | dados administrativos entram no Demo ou dados demo em produção | Server nunca chama runner; CLI produtivo seleciona Product; Demo seleciona Demo e prova somente `demo_realm`; testes negativos dos dois entry points | Aberto |
| Server local parece simples mas não foi provisionado | PostgreSQL Podman sobe vazio e o Server é iniciado diretamente | bootstrap funcional falha | runbook único Podman → runner → Server, erro sanitizado e nenhuma migration automática | Aberto |
| Histories colidem em banco compartilhado | terceira família usa nome/schema incompatível | migrations são ignoradas ou reaplicadas | teste same-database e history explícita por owner | Aberto |
| Snapshot não reflete setup | helper grava client e não publica refresh | testes falham ou exercitam dados antigos | helper único + `IConfigurationSnapshotRefresher` obrigatório | Aberto |
| Estado global contamina fixtures | env AES estática, arquivo ou connection compartilhada | flakiness/paralelismo inseguro | material/lifetime por fixture e teste com duas factories | Aberto |
| Topologia persistente degrada a suíte | migrations/seed se repetem nas factories | feedback local fica mais lento | baseline, três medições, mediana e limiar numérico nas Fases 4/6 conforme DF26 | Aberto |
| Composição resolve backing incorreto | duas implementações dos mesmos contratos são registradas | teste passa pelo storage errado | `Tests.Host` agnóstico, registro explícito por factory e validação de resolução | Aberto |
| Extension protocolar vira bootstrap geral | `UseRoyalIdentityProtocol` passa a registrar storage, UI ou endpoints test-only | hosts deixam de ser composition roots independentes | limite de DF29 + guard contra referências a Server/providers/Razor no extension | Aberto |
| Ordem de middleware é usada incorretamente | host chama o extension antes de routing ou mistura antiforgery/UI dentro dele | realm discovery/authentication ou endpoints falham | precondições XML, ordem explícita nos três entry points e testes de composição | Aberto |
| `Tests.Host` parece executável mas não possui storage | launch profile antigo inicia o projeto fora de uma factory | snapshot hosted service falha no startup | remover/substituir profiles e documentar uso exclusivo por `WebApplicationFactory` | Aberto |
| Live references são reproduzidas em outro seam | setup altera entidade EF diretamente | teste deixa de representar comportamento real | features do módulo ou hook test-only explícito | Aberto |
| Handle estático mascara options/realm atual | fixture carrega `Realm` no handle e o reutiliza entre composições | teste lê estado/options fora do snapshot corrente | handles primitivos, carga por store/snapshot e guard estático | Aberto |
| Resource bridge é “resolvida” com persistência acidental | cenário precisa adicionar/remover resource | quebra DF13 e antecipa redesign | source volátil da fixture + guard de arquitetura | Aberto |
| Limpeza direta de Operational vira API pública genérica | teste chama `Clear()` por conveniência | contrato de produto cresce por setup | remover handle conhecido ou hook test-only focado | Aberto |
| Reset administrativo é confundido com cleanup | option EF apaga dados válidos como se fossem expirados | perda cross-family sem coordenação/auditoria | manter fora do runtime; futuro comando/admin explícito, autorizado e orquestrado | Aberto |
| Quebra atômica ocorre cedo demais | DF28 é aplicada com fake ainda referenciado | fake precisa ganhar paridade ou solução não compila | Fases 6-8 e corte coordenado DF7 | Aberto |
| Testes concretos do fake somem sem equivalente | variante/harness é apagado sem mapa | perda silenciosa de regressão | inventário por cenário na Fase 7 | Aberto |
| Divergência real é tratada como artefato do fake | asserção muda apenas para a suíte voltar a passar | regressão de módulo ou defeito do core fica mascarado | triagem obrigatória em quatro buckets e correção no owner | Aberto |
| Filtro executa zero testes | nome/classe muda durante a migração | fase aparenta verde sem exercitar o grupo | `--list-tests`, contagem esperada/executada e registro por filtro | Aberto |
| SQLite mascara diferença produtiva | fluxo só é executado no default SQLite | regressão de provider chega a produção | aceite PostgreSQL real local/opt-in de DF24 | Aberto |
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
- API/UI administrativa e gerenciamento de realms/clients/users — destino: roadmap administrativo.
- Reset destrutivo de realm/instalação — futuro comando ou módulo administrativo que orquestre Configuration,
  Operational e `UserAccounts`, com escopo explícito, autorização, confirmação, auditoria e reseed opcional; não é
  option de `AddEntityFrameworkStorage` nem cleanup de expirados.
- Persistência e administração de `UserAccountsRealmOptions` — este plano entrega somente configuração runtime por
  defaults + overrides de realm; armazenamento durável e UI/API ficam para o roadmap administrativo.

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
- [ADR.md](../../ADR.md).
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
