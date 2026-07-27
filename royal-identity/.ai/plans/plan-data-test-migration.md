# Plan: Composição persistente do host e migração dos testes (`plan-data-test-migration`)

## Status: RASCUNHO - decisões de planejamento fechadas em 2026-07-26; implementação não iniciada

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
  `AddHostServices`, e `appsettings.json` não contém provider, conexões, snapshot, cleanup ou proteção.
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
- `RoyalIdentity.Storage.EntityFramework*` — registro dos contexts/providers, gateway, protectors e validações
  funcionais já existentes.
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
   in-memory; a composição produtiva não executa migration/seed, e o profile SQLite demo possui somente a exceção
   provider-owned de DF27.
2. Entregar provisionamento externo produtivo e bootstrap SQLite demo suficientes para iniciar as três famílias
   configuradas de forma consistente.
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
- Implementar providers SQL Server, Oracle ou outros; DF17 apenas preserva a extensibilidade do contrato.
- Introduzir transação distribuída entre Configuration, Operational e `UserAccounts`.
- Implementar Aspire/deployment orchestration além do contrato executável de provisionamento — destino:
  `.ai/backlogs/backlog-001.md`.
- Migrar estado do fake para banco; o fake não é uma fonte durável.

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
  `Migrate`, `MigrateAsync` ou seed. **AMENDED somente por DF27** para o profile SQLite demo in-memory. Fonte:
  decisão 23 do Plano 3 e resposta humana a Q6.
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
- **DF17 — Provider independente por família:** Configuration, Operational e `UserAccounts` possuem seleção
  independente de provider; KMS terá sua própria seleção quando existir. O contrato não pressupõe que todas as
  famílias usem o mesmo SGBD e deve admitir providers futuros, como SQL Server e Oracle, sem redesenhar as demais
  opções. Fonte: resposta humana a Q1.
- **DF18 — SQLite não é provider produtivo:** SQLite pertence a testes e à experiência local/demo; o provider
  produtivo atual é PostgreSQL. O Server rejeita SQLite fora de ambiente/profile explicitamente local, demo ou
  teste. Fonte: resposta humana a Q1.
- **DF19 — Connection string por `DbContext`:** Configuration, Operational e `UserAccounts` recebem conexões
  explícitas e independentes, sem connection default herdada; KMS seguirá a mesma regra quando for entregue. Fonte:
  resposta humana a Q2.
- **DF20 — Data Protection oficial e transitório até KMS:** o host oficial usa Data Protection para signing keys e
  payloads protegidos, inclusive em debug; não seleciona Plain/AES como alternativa oficial. A integração futura
  com KMS substituirá essa solução em plano próprio. Host e provisionamento usam configuração compatível de key
  ring, application name e purposes. Fonte: resposta humana a Q3.
- **DF21 — Runner único inclui `UserAccounts`:** `RoyalIdentity.Migrations` passa a provisionar Configuration,
  Operational e `UserAccounts`, preservando resultado por família. `RoyalIdentity.Server` não ganha referência,
  dependência ou chamada relacionada a migrations. Fonte: resposta humana a Q4.
- **DF22 — Host ignora estado de migrations:** o Server não consulta migrations pendentes, não executa
  `GetPendingMigrations*`, `EnsureCreated*` ou `Migrate*` e não possui readiness específica de schema. Validações
  funcionais já existentes, como snapshot e signing keys utilizáveis, permanecem e podem falhar naturalmente se o
  banco não tiver sido provisionado. Fonte: resposta humana a Q5 e regra histórica do projeto.
- **DF23 — Options de `UserAccounts` configuráveis por realm:** este plano entrega uma fonte configurável por realm
  através da composição/adapter, sem criar dependência do módulo puro no core. Fonte: resposta humana a Q7.
- **DF24 — PostgreSQL sem CI neste plano:** não se cria pipeline de CI. PostgreSQL, por ser o provider produtivo,
  recebe evidência executável local/opt-in de provisionamento, startup e fluxo OIDC; SQLite permanece o provider
  dos testes default. Fonte: resposta humana a Q10.
- **DF25 — Implementações entram pela composição dos contratos:** projetos de storage implementam e entregam os
  contratos core-owned do IdP; a composition root escolhe exatamente uma implementação. A retirada do fake é uma
  migração de consumidores/composição já determinada por ADR-018, não uma decisão humana adicional sobre
  coexistência de factories. Fonte: resposta humana à antiga Q12, ADR-013 e ADR-018.
- **DF26 — Desempenho da fixture não é gate humano:** a duração da suíte deve ser medida durante a implementação,
  mas otimizações de topologia não são decisões de arquitetura deste plano. Começar pela composição SQLite isolada
  mais simples e otimizar somente se a medição demonstrar regressão material. Fonte: resposta humana à antiga Q11.
- **DF27 — Demo SQLite self-provisioned:** `RoyalIdentity.Storage.EntityFramework.Sqlite` expõe um extension method
  opt-in que registra e provisiona tudo que pertence a Configuration + Operational para um demo SQLite in-memory,
  incluindo conexões keep-alive, migrations e seed, antes do tráfego. Esta é uma exceção explícita a DF8/DF22,
  restrita a ambiente Development + profile Demo; nunca é selecionada em produção e não usa nem faz o Server
  referenciar `RoyalIdentity.Migrations`. A composição completa também chama a extensão própria de
  `RoyalIdentity.UserAccounts.Sqlite` para seu banco/seed; o provider SQLite do core não passa a referenciar nem
  possuir a família de contas. Fonte: resposta humana a Q6, com preservação de ADR-013/ADR-015.
- **DF28 — Contratos atômicos incorporados aos contratos base:** `ConsumeAuthorizationCodeAsync` entra em
  `IAuthorizationCodeStore`; `TryConsumeAsync`/`TryUpdateAsync` entram em `IRefreshTokenStore`;
  `ISingleUseAuthorizationCodeStore`, `IVersionedRefreshTokenStore` e `IRefreshTokenStore.UpdateAsync` são
  removidos. `IStorage` continua retornando os contratos base realm-bound, agora obrigatoriamente atômicos. Fonte:
  resposta humana a Q9 e revisão do código atual.
- **DF29 — `Tests.Host` é uma composition root independente:** seguir Q8=A. `Tests.Host.Program` continua sendo o
  ambiente HTTP real dos testes e não executa/adapta `RoyalIdentity.Server.Program`. Os dois hosts reutilizam os
  extensions pertencentes a core, Razor, providers e `UserAccounts`, mas escolhem suas implementações
  independentemente. Para evitar drift somente na ordem protocolar crítica, extrair em
  `RoyalIdentity/Extensions/ApplicationBuilderExtensions.cs` um `UseRoyalIdentityProtocol(...)` provider-neutral,
  limitado a realm discovery, realm CORS, authentication, authorization e mapeamento dos endpoints OIDC. Error
  handling, UI/Razor, static files, antiforgery específico e endpoints `/test/*` permanecem em cada `Program`.
  Não criar `RoyalIdentity.Hosting`, bootstrap geral compartilhado nem referência de `Tests.Host` ao
  `RoyalIdentity.Server`. Fonte: resposta humana a Q8.

---

## Histórico de decisões

**2026-07-26 (respostas humanas sobre a composição):**

- Q1/Q2 foram fechadas por DF17-DF19: provider e connection string são independentes por `DbContext`; SQLite é
  somente teste/local/demo, PostgreSQL é produtivo e novos providers permanecem possíveis.
- Q3 foi fechada por DF20: Data Protection é a solução oficial temporária até KMS, inclusive em debug.
- Q4/Q5 foram fechadas por DF21/DF22: o runner existente incorpora `UserAccounts`; o Server não referencia,
  consulta nem executa migrations.
- Q7 foi fechada por DF23: options de `UserAccounts` serão configuráveis por realm.
- Q10 foi fechada por DF24: nenhuma CI será criada; a evidência PostgreSQL é executável local/opt-in.
- A antiga Q11 foi retirada como gate arquitetural e convertida em DF26/tarefa de medição não bloqueante.
- A antiga Q12 foi retirada como gate arquitetural e convertida em DF25: implementações são escolhidas pela
  composition root e entregam os contratos do IdP.
- Q6 foi fechada por DF27 como exceção opt-in e provider-owned para o demo SQLite in-memory.
- Q9 foi fechada por DF28 com contratos base fortes.
- Q8 foi fechada por DF29 com A: `Tests.Host` independente e somente o pipeline protocolar provider-neutral
  extraído no core. B foi descartada; C não cria uma abstração de hosting própria.

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

- `RoyalIdentity.Server`: possui o binding/validation independente de Configuration, Operational e `UserAccounts`,
  registra adapters/providers conforme DF17-DF23 e não contém acesso direto a entidades `Data.*`.
- `IStorage`/`IStorageProvider`: são fornecidos exclusivamente pelo gateway EF completo; exatamente uma composição
  fica resolvível.
- `IUserDirectory` e portas realm-bound de conta: são fornecidos por
  `RoyalIdentity.UserAccounts.Integration`; o core continua sem referência ao módulo.
- `RoyalIdentity.Migrations`: recebe provider e conexão por família, aplica migrations de Configuration,
  Operational e `UserAccounts` explicitamente e retorna resultado independente por família.
- Startup do host: não possui lógica ou inspeção de migrations; preserva apenas validações funcionais exigidas para
  atender requests, conforme DF22.
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
- `IConfigurationSnapshotRefresher`: é chamado pela fixture depois de writes de Configuration e antes do request
  que consome os dados.
- `IConfigurationResourceSource`: continua sendo a rota volátil explícita de resources/scopes em host/fixtures.
- Topologia da fixture: começa com SQLite in-memory isolado por factory e conexão keep-alive própria por
  `DbContext`; seu custo é medido, não convertido em gate arquitetural antecipado.
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
  -> RoyalIdentity.Storage.EntityFramework
  -> provider EF Sqlite/PostgreSql selecionado
  -> RoyalIdentity.UserAccounts.Integration
  -> provider UserAccounts Sqlite/PostgreSql selecionado
  -X-> RoyalIdentity.Migrations
  -X-> RoyalIdentity.Storage.InMemory
  -X-> RoyalIdentity.Data.*

RoyalIdentity.Migrations/
  aplica schemas e seeds fora do processo web produtivo
  preserva ownership e resultado por família

RoyalIdentity.Storage.EntityFramework.Sqlite/
  profile Demo opt-in provisiona Configuration + Operational in-memory
  -X-> RoyalIdentity.UserAccounts.Sqlite

RoyalIdentity.UserAccounts.Sqlite/
  mantém registro, migrations e seed próprios para contas demo

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
- O processo web produtivo não escreve schema nem seed; somente o profile `Development + Demo` executa os
  initializers provider-owned de DF27 antes do tráfego.

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

- Primeiro entregar provisionamento e composição real do Server; depois criar a fixture conjunta e migrar os grupos
  de testes.
- Manter o fake apenas como suporte temporário durante Fases 1-7, sem adicionar capabilities ou comportamento.
- Deixar a factory persistente como composição canônica somente após todos os grupos HTTP estarem verdes nela.
- Manter `Tests.Host` storage-agnóstico; cada factory registra diretamente a implementação dos contratos que
  pretende exercitar, sem registrar dois backings e tentar corrigir a composição depois.
- Preparar `Tests.Storage`, `Tests.UserAccounts` e guards arquiteturais para viver sem o fake antes da quebra pública.
- Aplicar DF28, remover fallbacks e excluir o projeto fake no mesmo corte compilável da Fase 8.
- Não há dual-write, import/export de dictionaries nem compatibilidade de dados com processos in-memory anteriores.
- Hosts produtivos devem executar o runner antes do novo binário; o Server nunca corrige schema produtivo. O
  bootstrap in-memory de DF27 é exclusivo do profile demo.
- Medir o tempo da suíte antes/depois da migração e registrar os comandos; otimização de fixture só entra se os
  números demonstrarem regressão material.
- O fechamento PostgreSQL é local/opt-in conforme DF24; CI não faz parte deste plano.

---

## Ordem de execução

1. **Fase 1 (contrato de configuração e composição)** — aplica DF17-DF23 e cria a superfície validada que as demais
   fases consomem.
2. **Fase 2 (provisionamento externo)** — prepara externamente os três schemas e seeds produtivos.
3. **Fase 3 (Server real)** — troca o host oficial somente depois de existir configuração e provisionamento.
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

**Escopo:** `RoyalIdentity/Extensions`, `RoyalIdentity.Server`, `Tests.Host`, options/validators de composição,
`Tests.Architecture` e eventual ADR exigida pelas respostas.

**O que/como:** criar um entry point de registro persistente com configuração tipada por família/`DbContext`,
validação antes do tráfego e grafo de dependências permitido. Não trocar o backing do Server antes de a superfície
estar testada.

**Tarefas:**

- [ ] Registrar em ADR nova somente decisões arquiteturais que não estejam cobertas pelas ADRs existentes.
- [ ] Criar options tipadas independentes para provider e connection string de Configuration, Operational e
  `UserAccounts`; deixar o shape extensível a KMS e providers futuros sem registrá-los antecipadamente.
- [ ] Restringir SQLite a ambiente/profile de teste, local ou demo; PostgreSQL é o provider produtivo deste plano.
- [ ] Criar options tipadas para snapshot, cleanup e Data Protection; não oferecer seletor oficial
  Plain/AES.
- [ ] Validar presença, formato, combinações permitidas e duplicidade sem materializar secrets em mensagens.
- [ ] Alterar `AddHostServices`/entry point escolhido para receber `IConfiguration` e ambiente explicitamente.
- [ ] Criar `UseRoyalIdentityProtocol(...)` no core com o limite exato de DF29; manter UI, error handling,
  static files, antiforgery específico e endpoints test-only fora do extension.
- [ ] Fazer `RoyalIdentity.Server.Program` e `Tests.Host.Program` chamarem o extension, preservando os dois
  composition roots e seus complementos próprios.
- [ ] Entregar a fonte configurável por realm de `UserAccounts` na composição/adapter, preservando o módulo puro.
- [ ] Não adicionar options, serviços ou validators que consultem estado de migrations.
- [ ] Substituir guards que hoje proíbem qualquer referência EF no Server por guards que permitam adapters/providers
  e continuem proibindo `Data.*`, `Migrations` e dependências inversas.
- [ ] Cobrir configuração SQLite/PostgreSQL, campos ausentes, provider inválido, cleanup ausente e protection
  incompatível.

**Critérios de aceite:** cada `DbContext` possui provider e connection string explícitos; SQLite é rejeitado em
profile produtivo; cleanup ou Data Protection ausentes/ambíguos falham antes de servir requests; nenhum erro contém
secret; options de `UserAccounts` variam por realm sem acoplar o módulo ao core; não existe inspeção de migrations;
Server e `Tests.Host` usam `UseRoyalIdentityProtocol(...)` sem compartilhar seus `Program`; `Tests.Host` não
referencia `RoyalIdentity.Server`; os guards arquiteturais refletem DF3/DF15/DF21/DF22/DF29.

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

**Depende de:** Fase 1, DF8, DF9, DF12, DF14, DF15, DF17-DF21.

**Escopo:** `RoyalIdentity.Migrations` e/ou runner da família `UserAccounts`, providers de `UserAccounts`, seeds,
scripts/README e testes de migration.

**O que/como:** estender `RoyalIdentity.Migrations` para aplicar migrations de Configuration, Operational e
`UserAccounts` fora do host. Preservar provider, conexão, history, resultado e ownership próprios; a exceção local
do demo SQLite pertence a DF27 e não altera o runner produtivo.

**Tarefas:**

- [ ] Implementar a seleção explícita da família `UserAccounts` no runner escolhido sem acoplá-la ao gateway
  `IStorage`.
- [ ] Reescrever `MigrationsRunner_ProjectGraph_References_Providers_Only` como allowlist dos providers EF
  do core e dos providers `UserAccounts`, continuando a proibir referências diretas a
  `RoyalIdentity/RoyalIdentity.csproj` e `Tests.*`.
- [ ] Registrar junto do guard a justificativa de ADR-013: o runner é composition root das duas famílias
  independentes e não traduz tipos entre elas; portanto seu papel não é o adapter `.Integration`.
- [ ] Aceitar provider e connection string independentes por `DbContext`, conforme DF17-DF19, preferindo secrets por
  variáveis de ambiente.
- [ ] Desacoplar a seleção de provider Configuration/Operational no runner atual e cobrir topologias mistas.
- [ ] Aplicar migrations das famílias selecionadas em ordem documentada, sem transação distribuída.
- [ ] Retornar status independente por família e preservar códigos de saída não zero em falha parcial.
- [ ] Manter seed Configuration `Product|Demo` idempotente e separado de migration.
- [ ] Provar segunda execução idempotente, banco compartilhado, bancos separados e combinações de provider
  autorizadas por DF17/DF18.
- [ ] Provar que falha na terceira família não reporta rollback inexistente das anteriores.
- [ ] Atualizar a documentação do(s) runner(s) com comandos que não exponham connection strings/chaves.

**Critérios de aceite:** banco vazio pode receber os três schemas somente pelo(s) comando(s) externo(s); execução
repetida é idempotente; cada família tem resultado identificável; banco compartilhado não colide histories/tabelas;
as combinações de provider autorizadas por DF17/DF18 são aceitas e as demais são rejeitadas antes de I/O;
`RoyalIdentity.Server` não referencia nem chama o runner.

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

**Depende de:** Fases 1-2, DF3, DF4, DF8-DF13, DF15 e DF17-DF23, DF27.

**Escopo:** `RoyalIdentity.Server`, providers EF, `.Integration`/providers de `UserAccounts`, startup validators,
appsettings de exemplo e testes de host.

**O que/como:** trocar `AddInMemoryStorage()` pela composição integral. Registrar Data Protection, contexts
Configuration/Operational, snapshot, resource bridge, Operational/profiles, cleanup, protector de signing keys,
gateway completo e `UserAccounts`, nesta ordem lógica. Não adicionar qualquer lógica de migrations ao startup,
conforme DF22.

**Tarefas:**

- [ ] Referenciar no Server apenas adapters/providers permitidos por DF17/DF18.
- [ ] Configurar ASP.NET Data Protection e o protector de signing keys compatível com o provisionamento, preservando
  DF20 e a futura substituição por KMS.
- [ ] Registrar contexts Configuration/Operational com histories corretas para o(s) provider(s) selecionado(s).
- [ ] Registrar snapshot source/refresh interval e resource bridge; adicionar o profile local/demo somente conforme
  DF27.
- [ ] Registrar Operational storage, profiles e exatamente um modo de cleanup.
- [ ] Registrar o gateway `AddEntityFrameworkStorage()` completo.
- [ ] Registrar provider `UserAccounts`, fonte configurável de options por realm conforme DF23 e
  `AddUserAccountsForRoyalIdentity()`.
- [ ] Criar em `RoyalIdentity.Storage.EntityFramework.Sqlite` o extension method opt-in do demo que mantém
  conexões in-memory abertas, aplica migrations Configuration/Operational e seus seeds antes do tráfego.
- [ ] Compor no profile demo a extensão própria de `RoyalIdentity.UserAccounts.Sqlite` e seu seed, sem fazer o
  provider SQLite do core referenciar ou possuir a família de contas.
- [ ] Provar que a extensão demo só pode ser selecionada em `Development` + profile explícito e que o profile
  produtivo PostgreSQL não registra nenhum initializer de schema/seed.
- [ ] Remover `AddInMemoryStorage()` e a referência `RoyalIdentity.Storage.InMemory` do Server.
- [ ] Preservar as validações funcionais de snapshot/signing keys, sem implementar readiness de schema e sem
  `GetPendingMigrations*`, `EnsureCreated*`, `Migrate*` ou seed.
- [ ] Validar todos os profiles Operational selecionados pelos realms do snapshot antes do tráfego.
- [ ] Preservar bootstrap de snapshot, `SigningKeyStartupValidator` e ordem
  `UseRealmDiscovery` antes de `UseAuthentication`.
- [ ] Cobrir protector incompatível, profile ausente, key inválida e `IUserSecurityStateProvider` exigido por
  policy; não criar teste de migrations pendentes no host.

**Critérios de aceite:** o Server inicia sobre bancos previamente provisionados e resolve exatamente um `IStorage`
EF e um `IUserDirectory` de `UserAccounts`; configuração inválida falha antes de aceitar request; o projeto não
referencia InMemory/Migrations/Data; não há migration/seed no processo produtivo; a única exceção é o initializer
provider-owned do profile SQLite demo definido em DF27; o demo inicia de banco vazio e conclui um fluxo OIDC com
conta real; resource bridge segue DF13.

**Testes:**

```powershell
dotnet build RoyalIdentity.Server/RoyalIdentity.Server.csproj
dotnet test Tests.Architecture
dotnet test Tests.Integration --filter "FullyQualifiedName~HostComposition|FullyQualifiedName~HostStartup|FullyQualifiedName~SqliteDemo"
```

### Resultado da Fase 3

*a preencher*

---

## Fase 4 - fixture SQLite unificada, handles e seeds

**Depende de:** Fases 2-3, DF5, DF13, DF14, DF16, DF25, DF26 e DF29.

**Escopo:** `Tests.Host`, `Tests.Integration/Prepare`, `Tests.UserAccounts/UserAccountsModuleSeed.cs`, helpers de
Configuration/Operational e resource bridge test-only.

**O que/como:** criar uma factory integral SQLite in-memory com Configuration + Operational migrados e
`UserAccounts` real. Cada `DbContext` recebe connection string/keep-alive isolados. Expor dados por handles neutros
e operações explícitas de setup; não substituir um acesso ao fake por outro static global.

**Tarefas:**

- [ ] Medir e registrar, com comando e ambiente, o tempo da suíte `Tests.Integration` ainda fake e o startup
  cold/warm da factory persistente; a medição informa otimizações posteriores, sem bloquear o desenho.
- [ ] Manter `Tests.Host.Program` como composition root independente conforme DF29; remover
  `AddInMemoryStorage()` e sua referência ao projeto fake, deixando o projeto storage-agnóstico.
- [ ] Fazer cada factory registrar diretamente uma única implementação dos contratos do IdP. Enquanto houver
  consumers legados, sua factory registra explicitamente o fake; a persistente registra EF + `UserAccounts`.
- [ ] Fazer a factory persistente construir o service provider com `ValidateScopes` e `ValidateOnBuild`.
- [ ] Criar uma conexão SQLite in-memory nomeada e keep-alive própria para cada `DbContext`/factory, aplicar as
  migrations pelo runner de teste antes do host e manter as conexões abertas até o teardown.
- [ ] Registrar Configuration + Operational EF, `UserAccounts` SQLite e cleanup `External` na fixture.
- [ ] Usar protectors determinísticos test-only sem variável de ambiente process-global compartilhada.
- [ ] Semear Configuration demo/teste, Alice/Bob e property scopes por owner correto.
- [ ] Mover `AliceSubjectId`/`BobSubjectId` para o seed test-only e remover seu import de InMemory.
- [ ] Expor handles imutáveis para realms internos/demo, clients, resources e subjects usando somente ids
  primitivos, paths e valores provider-neutral; nenhum handle pode conter `Realm`.
- [ ] Obter qualquer objeto `Realm` usado pelo teste via `IRealmStore`/snapshot depois do seed e dentro da
  composição corrente.
- [ ] Criar helper de client que persiste pelo seam test-only e chama `IConfigurationSnapshotRefresher`.
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
registrados. `Tests.Host` não registra nem referencia qualquer backing.

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
- [ ] Medir, no mesmo ambiente/protocolo do baseline da Fase 4, a duração da suíte persistente completa, registrar
  a diferença e otimizar somente se houver regressão material demonstrada.

**Critérios de aceite:** as 29 classes antes ligadas a `AppFactory` executam sobre EF + `UserAccounts`; não existem
factories parciais; `Tests.Integration` não contém uso de `MemoryStorage`, getters do fake ou mutação de dictionary;
todos os filtros executam ao menos um teste e têm suas contagens registradas; toda mudança de asserção está
classificada; a comparação de duração está registrada; todos os fluxos e caracterizações permanecem verdes; os
fallbacks ainda não foram ampliados nem acionados pelo EF; a referência temporária ao projeto fake foi removida.

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

**Depende de:** Fase 7, DF6, DF7 e DF28.

**Escopo:** contratos/consumers do core, adapter EF Operational, shape tests, solução e
`RoyalIdentity.Storage.InMemory`.

**O que/como:** aplicar a quebra pública definida em DF28 e excluir o fake no mesmo corte. O código intermediário não
precisa suportar uma implementação sem atomicidade; a branch deve voltar a compilar e testar antes de encerrar a
fase.

**Tarefas:**

- [ ] Tornar consumo de authorization code single-use uma dependência obrigatória de compilação.
- [ ] Tornar transição de refresh token versionada/condicional uma dependência obrigatória de compilação.
- [ ] Remover casts, capability detection, logging de fallback e get-then-remove do
  `DefaultAuthorizationCodeConsumer`.
- [ ] Remover casts, fallback não condicional e `IRefreshTokenStore.UpdateAsync`.
- [ ] Remover `ISingleUseAuthorizationCodeStore`, `IVersionedRefreshTokenStore` e composites redundantes conforme
  DF28.
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

**Depende de:** Fase 8, DF1-DF29.

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
- [ ] Atualizar README/scripts do provisionamento com provider, três famílias, protection, cleanup e experiência
  demo decididos.
- [ ] Confirmar que documentação histórica não é apresentada como instrução vigente.
- [ ] Preencher `Resultado da Fase` de todas as fases, riscos, desvios e pendências.
- [ ] Marcar o plano `CONCLUIDO` e atualizar a barra somente após todas as decisões e gates de aceite.

**Critérios de aceite:** evidência PostgreSQL cumpre DF24; solução completa verde; nenhuma instrução vigente indica
InMemory como default; ADR-018 registra a consequência já realizada; documentação operacional permite provisionar e
iniciar o host sem secret em linha de comando; todas as decisões fechadas foram aplicadas.

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
| Objetivo 1 — Server integral sem fake | 1-3 | DF3, DF4, DF8-DF13, DF15, DF17-DF23, DF27 | exatamente um gateway EF e um `IUserDirectory` real; demo SQLite isolado da composição produtiva | `HostConfiguration`, `HostComposition`, `HostStartup`, `Tests.Architecture` |
| Objetivo 2 — provisionamento externo | 1-3 | DF8, DF9, DF11, DF12, DF15, DF17-DF22, DF27 | três schemas externos; exceção SQLite demo provider-owned e environment-gated | testes de migration/runner e startup |
| Objetivo 3 — testes default reais | 4-6 | DF2, DF5, DF13, DF14, DF16, DF25, DF26, DF29 | 29 consumers sobre factory integral; zero uso do fake em `Tests.Integration`; `Tests.Host` permanece independente | grupos HTTP + `--list-tests` + `dotnet test Tests.Integration` |
| Objetivo 4 — remover transição/fake | 7-8 | DF6, DF7, DF28 | atomicidade obrigatória nos contratos base; zero fallback/projeto/referência fake | `Tests.Identity`, `Tests.Storage`, concorrência e buscas `rg` |
| Objetivo 5 — paridade e fechamento | 9 | DF1, DF2, DF16, DF24 | aceite PostgreSQL local cumprido, solução/doc normativa verde | script PostgreSQL + `dotnet test RoyalIdentity.sln` |

---

## Invariantes a preservar

1. Todo dado de client, key, conta, sessão, token, consent ou configuração permanece realm-scoped.
2. `RoyalIdentity` não referencia providers, Server, `UserAccounts` ou qualquer fake.
3. `RoyalIdentity.Pipelines` permanece sem dependência de domínio.
4. `Data.*` permanece puro e só é adaptado por `RoyalIdentity.Storage.EntityFramework`.
5. `UserAccounts` puro não referencia o core; somente `.Integration` conhece os dois lados.
6. O Server não referencia `Data.*`, `RoyalIdentity.Migrations` ou `Tests.*`.
7. O processo web produtivo nunca aplica migration, `EnsureCreated` ou seed; a única exceção é o bootstrap
   provider-owned, environment-gated e in-memory de DF27.
8. Configuration, Operational e `UserAccounts` mantêm migrations/histories/resultado próprios.
9. Não há transação global nem promessa de rollback conjunto entre famílias.
10. Cleanup possui exatamente um modo explícito.
11. Plain nunca é default e proteção ausente/incompatível falha fechado.
12. O host consegue desproteger as signing keys provisionadas usando o protector configurado; nunca cria nem
    rotaciona esse material.
13. Resources/scopes permanecem voláteis por DF13.
14. Authorization codes são single-use sob concorrência real.
15. Refresh transitions são condicionais e preservam a tolerância pós-consumo vigente.
16. Nenhuma nova paridade é adicionada ao fake durante sua janela restante.
17. Setup de conta usa o módulo ou seam test-only; nunca live reference de entidade.
18. Write de Configuration usado por teste é seguido de refresh explícito do snapshot.
19. Fixtures não compartilham DB, secret store, env var mutável ou handle estático.
20. `UseRealmDiscovery` continua antes de `UseAuthentication`.
21. Validators continuam sinalizando falhas esperadas por `context.Response`, sem lançar por erro de protocolo.
22. A fase de exclusão não remove cobertura sem mapear seu provider/teste substituto.
23. Handles de fixture não contêm `Realm`; objetos de realm usados por testes vêm do store/snapshot da composição
    corrente.
24. Cada composition root resolve exatamente uma implementação dos contratos de storage que pretende exercitar.
25. Toda mudança de asserção durante a migração possui classificação e evidência nos quatro buckets de triagem.
26. `Tests.Host` não referencia nem executa `RoyalIdentity.Server`; ambos compartilham somente extensions
    provider-neutral e contracts/providers explicitamente selecionados.

---

## Critérios globais de conclusão

- DF17-DF29 foram aplicadas e eventuais desvios estão registrados nos resultados das fases.
- `RoyalIdentity.Server` inicia sobre a composição persistente decidida e não oferece fallback in-memory.
- As três famílias são provisionáveis externamente em produção; o host produtivo não contém inspeção ou execução
  de migrations, e somente o demo in-memory possui a exceção DF27.
- `Tests.Integration` roda integralmente sobre EF/SQLite + `UserAccounts`.
- A duração antes/depois da suíte está registrada com comandos e não sofreu regressão material sem tratamento.
- Nenhum código de teste resolve `MemoryStorage`, `RealmMemoryStore` ou stores concretos do fake.
- Nenhum consumer detecta capability atômica opcional ou executa fallback não atômico.
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
| Banco não foi provisionado antes do host | operador inicia Server sem executar o runner | falha funcional no primeiro bootstrap/acesso | runbook externo, erro sanitizado e nenhuma tentativa automática de migration | Aberto |
| Demo deixa de autenticar | initializer core roda sem accounts/resources do módulo | `dotnet run` parece funcional, mas login/authorize falha | DF27, composição das duas extensões provider-owned e fluxo OIDC completo | Aberto |
| Exceção demo vaza para produção | profile/ambiente não é validado antes do initializer | processo produtivo altera schema/seed | exigir simultaneamente `Development` + `Demo`, teste negativo em PostgreSQL e ausência do initializer fora desse gate | Aberto |
| Provider SQLite do core absorve `UserAccounts` | extension demo tenta possuir migrations/seed de contas | viola ADR-013/ADR-015 e acopla famílias independentes | extensão core provisiona somente Configuration/Operational; composição chama a extensão própria de `UserAccounts.Sqlite` | Aberto |
| Histories colidem em banco compartilhado | terceira família usa nome/schema incompatível | migrations são ignoradas ou reaplicadas | teste same-database e history explícita por owner | Aberto |
| Snapshot não reflete setup | helper grava client e não publica refresh | testes falham ou exercitam dados antigos | helper único + `IConfigurationSnapshotRefresher` obrigatório | Aberto |
| Estado global contamina fixtures | env AES estática, arquivo ou connection compartilhada | flakiness/paralelismo inseguro | material/lifetime por fixture e teste com duas factories | Aberto |
| Topologia persistente degrada a suíte | migrations/seed se repetem nas factories | feedback local fica mais lento | medir nas Fases 4/6 e otimizar somente com evidência, conforme DF26 | Aberto |
| Composição resolve backing incorreto | duas implementações dos mesmos contratos são registradas | teste passa pelo storage errado | `Tests.Host` agnóstico, registro explícito por factory e validação de resolução | Aberto |
| Extension protocolar vira bootstrap geral | `UseRoyalIdentityProtocol` passa a registrar storage, UI ou endpoints test-only | hosts deixam de ser composition roots independentes | limite de DF29 + guard contra referências a Server/providers/Razor no extension | Aberto |
| Live references são reproduzidas em outro seam | setup altera entidade EF diretamente | teste deixa de representar comportamento real | features do módulo ou hook test-only explícito | Aberto |
| Handle estático mascara options/realm atual | fixture carrega `Realm` no handle e o reutiliza entre composições | teste lê estado/options fora do snapshot corrente | handles primitivos, carga por store/snapshot e guard estático | Aberto |
| Resource bridge é “resolvida” com persistência acidental | cenário precisa adicionar/remover resource | quebra DF13 e antecipa redesign | source volátil da fixture + guard de arquitetura | Aberto |
| Limpeza direta de Operational vira API pública genérica | teste chama `Clear()` por conveniência | contrato de produto cresce por setup | remover handle conhecido ou hook test-only focado | Aberto |
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
