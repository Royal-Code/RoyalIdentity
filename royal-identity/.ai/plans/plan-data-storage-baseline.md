# Plan: Baseline dos contratos de storage do IdP (`plan-data-storage-baseline`)

## Status: RASCUNHO - decisões Q4-Q17 abertas; nenhuma fase iniciada

## Progresso

`░░░░░` **0%** - 0 de 5 fases concluídas

| Fase | Estado |
|---|---|
| Fase 1 - Inventário de contratos, consumidores e comportamento atual | Pendente |
| Fase 2 - Classificação por ciclo de vida e fronteira | Pendente |
| Fase 3 - Contract tests reutilizáveis | Pendente |
| Fase 4 - Seeds, dados globais e dependências entre stores | Pendente |
| Fase 5 - Paridade obrigatória e ordem de migração | Pendente |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluída, `%` e `X de N`). Exemplo de barra: `████░`.
> Antes de fechar uma fase, confirme que decisões, critérios de aceite, testes e invariantes relacionados foram aplicados.

---

## Contexto

### Fontes verificadas

- [template-ai-implementation-plan.md](../references/template-plan/template-ai-implementation-plan.md) — estrutura,
  gates de decisão, rastreabilidade e manutenção obrigatórios para este plano.
- [plan-data-macro.md](plan-data-macro.md) — define este baseline como Plano 1 e exige inventário das facades,
  classificação configuração×operacional, testes de caracterização, mapa de seeds/dependências, critérios de paridade
  e ordem de migração por store.
- [plans-roadmap-02.md](plans-roadmap-02.md) — não há outro plano ativo; este é o próximo sub-plano recomendado.
- [ADR-003](../../adrs/ADR-003.md) — testes em C#/xUnit, com ênfase em integração e sem dependência obrigatória de
  banco ou serviço externo.
- [ADR-013](../../adrs/ADR-013.md) — contratos de storage pertencem ao core; `Data.Configuration` e
  `Data.Operational` são dados puros; somente `Storage.EntityFramework` adapta `Data.*` às facades; `UserAccounts`
  mantém persistência própria.
- [ADR-014](../../adrs/ADR-014.md) — `IUserSessionStore` é persistência operacional do IdP, puro e realm-bound;
  `IUserDirectory` e portas de conta não pertencem a `IStorage`.
- [ADR-018](../../adrs/ADR-018.md) — `RoyalIdentity.Storage.InMemory` é fake transitório e não deve ganhar paridade
  de features como destino durável.
- [product.md](../foundation/product.md) — isolamento por realm, code de uso único, disponibilidade histórica de
  chaves, tolerância de consumo de refresh token, consentimento e imutabilidade de realms internos.
- [tech.md](../foundation/tech.md) e [structure.md](../foundation/structure.md) — pipeline, consumidores de storage,
  direção de dependências e arquivos de alto risco; fatos divergentes foram conferidos no código atual.
- [architecture.md](../foundation/architecture.md) — `Data.*` são projetos de dados puros, sem Feature-Slice e sem
  referência ao core; `Storage.EntityFramework` é o adapter.
- `RoyalIdentity/Contracts/Storage/*.cs`, `RoyalIdentity/Users/Contracts/IUserSessionStore.cs` e
  `RoyalIdentity/Contracts/Storage/ResourceStoreExtensions.cs` — contratos e semântica pública atuais.
- `RoyalIdentity.Storage.InMemory/*.cs` — única implementação atual de `IStorage` e comportamento observável do fake.
- `Tests.Integration/Storage/ResourceStoreTests.cs`, `Tests.Integration/Realm/RealmIsolationTests.cs` e testes de
  endpoints/usuários — cobertura atual direta ou incidental dos stores.
- Comandos locais `rg --files -g "*.csproj"`, `rg -n "GetRealmMemoryStore|GetDemoRealmStore|GetServerRealmStore"
  Tests.Integration -g "*.cs"` e inventário dos arquivos de contratos — evidências do estado descrito abaixo.

### Estado atual do código (verificado em 2026-07-21)

- **Não existe backing EF do core:** a solução não contém `RoyalIdentity.Data.Configuration`,
  `RoyalIdentity.Data.Operational`, `RoyalIdentity.Storage.EntityFramework`, `.Sqlite` ou `.PostgreSql`; somente
  `RoyalIdentity.Storage.InMemory` implementa `IStorage`.
- **Gateway atual:** `IStorage` expõe `ServerOptions`, `IRealmStore`, `IAuthorizeParametersStore` e oito stores
  realm-bound: clients, resources, keys, access tokens, refresh tokens, authorization codes, consents e sessões.
- **Contratos adjacentes:** `IMessageStore`, `IReplayCache`, `IStorageProvider` e `IStorageSession` vivem em
  `Contracts/Storage`, mas não são stores obtidos de `IStorage`; `IUserSessionStore` vive em `Users/Contracts` e é
  obtido por `IStorage.GetUserSessionStore(realm)`.
- **Binding por realm:** `MemoryStorage` mantém um `RealmMemoryStore` por `Realm.Id`; getters realm-bound lançam
  `ArgumentException` quando o realm não está cadastrado.
- **Vida dos registros:** os stores in-memory guardam e retornam as próprias instâncias mutáveis de modelos do core;
  não há materialização, detach ou cópia entre escrita e leitura.
- **Escritas duplicadas divergentes:** access/refresh token usam `TryAdd`, authorization code e sessão sobrescrevem,
  consent e realm fazem upsert, e key atribui pelo `KeyId`; os contratos não documentam uma política uniforme.
- **Uso único sem primitiva atômica:** `IAuthorizationCodeStore` separa get/remove; `IRefreshTokenStore` separa
  get/update/remove. O produto exige code de uso único e tolerância de refresh token, mas a atomicidade de um futuro
  provider concorrente não está expressa nos contratos.
- **Configuração sem facades completas de escrita:** `IClientStore`, `IResourceStore` e `IKeyStore` são
  predominantemente de leitura; muitos testes preparam clients/resources/keys mutando diretamente os dicionários de
  `RealmMemoryStore`.
- **Acoplamento dos testes ao fake:** há 55 ocorrências de `GetRealmMemoryStore`/`GetDemoRealmStore`/
  `GetServerRealmStore` em 15 arquivos de `Tests.Integration`; isso inclui setup de clients, resources, usuários e
  inspeção de estado operacional.
- **Precedente de contract test:** `Tests.UserAccounts/UserDirectoryContractTests.cs` executa o mesmo contrato contra
  fake e módulo+SQLite, mas não existe equivalente provider-neutral para os stores do core.
- **Cobertura atual desigual:** `ResourceStoreTests` cobre regras próprias do resource store e
  `RealmIsolationTests` cobre vários stores por fluxo/direto; não há uma matriz método×comportamento×teste para todos
  os contratos.
- **Ciclo do storage:** `MemoryStorage` é singleton, `IStorage` é transient apontando para esse singleton e
  `IStorageProvider` é singleton; `StorageProvider.CreateSession()` devolve a própria instância e `Dispose()` é no-op.
- **Cancelamento e ordenação:** a maioria das implementações fake ignora `CancellationToken`; somente listagens de
  keys ordenam explicitamente por `Created`, enquanto outras enumerações não fecham ordem.
- **Expiração:** tokens, codes, consent e sessões carregam tempos/lifetimes, mas os stores não têm política uniforme de
  filtrar ou limpar expirados; parte da validação acontece nos consumidores.
- **Modelo instável de resources:** `Client.AllowedScopes`/`AllowOfflineAccess` e a hierarquia de resources continuam
  marcados como redesign nas foundations; persistir esse shape sem gate pode cristalizar dívida conhecida.

### Lacunas, conflitos e restrições

- **Baseline não é implementação EF:** por DF1, qualquer mudança de contrato público ou produção encontrada deve ser
  descrita como requisito para plano posterior, não implementada aqui.
- **Caracterizar não significa preservar tudo:** DF3 exige classificar cada comportamento do fake como obrigatório,
  descartado ou substituído; um teste não pode tornar acidente do fake em contrato durável sem fonte/decisão.
- **ADR-003 versus providers futuros:** este plano deve ser executável sem serviço externo; PostgreSQL pertence aos
  planos de implementação dos providers, não ao gate obrigatório deste baseline.
- **Setup fora das facades:** contract tests provider-neutral precisam de uma porta de fixture test-only para semear e
  inspecionar estado sem criar APIs administrativas no core durante o baseline.
- **Duas famílias de persistência:** dados do IdP e `UserAccounts` não compartilham `DbContext` nem adapter; misturá-los
  violaria ADR-013/015.
- **Decisões abertas:** Q4-Q17 impedem fechar a matriz final e a Fase 5; as fases anteriores só podem avançar sem
  antecipar essas respostas.

### Superfícies impactadas a mapear

- `RoyalIdentity/Contracts/Storage/` — contratos públicos, extensões e semântica observável.
- `RoyalIdentity/Users/Contracts/IUserSessionStore.cs` — store operacional realm-bound fora da pasta de storage.
- `RoyalIdentity.Storage.InMemory/` — fake atual, seeds, índices, lifetime e comportamentos a classificar.
- `RoyalIdentity/Contracts/Defaults/`, `RoyalIdentity/Contexts/`, `RoyalIdentity/Handlers/`,
  `RoyalIdentity/Authentication/`, `RoyalIdentity/Responses/` e `RoyalIdentity/Users/Defaults/` — consumidores e
  premissas de persistência.
- `RoyalIdentity/Utils/Caching/` — consumidor de `IStorageProvider`/`IStorageSession` e premissas de lifetime.
- `RoyalIdentity.Server/` — composição, seed/demo e escolha de backing.
- `Tests.Integration/` — testes de fluxo, acesso direto ao fake e destino atual provável dos contract tests.
- Futuros `RoyalIdentity.Data.Configuration`, `RoyalIdentity.Data.Operational` e
  `RoyalIdentity.Storage.EntityFramework*` — consumidores do baseline, sem criação neste plano.

---

## Objetivo

1. Produzir um inventário completo e verificável de contratos, métodos, consumidores, binding por realm e
   comportamento atual dos stores do IdP.
2. Classificar cada superfície como configuração, operacional ou infraestrutura fora de `Data.*`, sem atravessar a
   fronteira de `UserAccounts`.
3. Entregar contract tests provider-neutral que rodem contra `MemoryStorage` agora e possam ser reutilizados pelos
   providers EF sem reescrever os cenários.
4. Registrar seeds, dados globais, dependências e acessos diretos ao fake que precisam de substituição futura.
5. Fechar uma matriz de paridade por comportamento e uma ordem de migração por store que sejam entradas executáveis
   para `plan-data-configuration-storage.md`, `plan-data-operational-storage.md` e
   `plan-data-test-migration.md`.

## Fora de escopo

- Alterar interfaces públicas, modelos do core, handlers ou implementações de produção — destino: planos 2 e 3.
- Criar `Data.*`, `Storage.EntityFramework*`, `DbContext`, entidades EF, mappings ou migrations — destino: planos 2 e 3.
- Tornar SQLite/EF o default de integração ou remover `MemoryStorage` — destino: plano 4.
- Alterar o módulo ou os providers de `RoyalIdentity.UserAccounts` — família de persistência própria, já endurecida.
- Implementar cache ou audit/outbox — destinos condicionais: planos 5 e 6 do macro-plano.
- Criar API/UI administrativa ou contratos de CRUD motivados apenas por uma futura UI — destino: plano próprio.
- Resolver o redesign de resources/scopes dentro deste baseline — destino precisa ser fechado em Q14.

---

## Perguntas ao humano

> Q1-Q3 foram respondidas e estão em `Histórico de decisões`. Q4-Q17 permanecem abertas. Fases podem inventariar fatos
> antes dessas respostas, mas nenhuma semântica ambígua pode entrar como contrato obrigatório ou ser diferida ao final
> sem contrariar DF3.

- **Q4 — Artefato durável do baseline:** onde deve viver a matriz completa de contratos/comportamentos/paridade?
  - **Opções:**
    - **A)** Arquivo irmão `.ai/plans/plan-data-storage-matrix.md`, seguindo o precedente de
      `plan-users-accounts-test-matrix.md`.
    - **B)** Seções mantidas dentro deste plano, sem documento auxiliar.
    - **C)** Referência em `.ai/references/data-storage/storage-baseline.md`, separada dos planos executáveis.
  - **Impacto se não decidir:** Fases 1-2 não têm destino estável para o catálogo e a Fase 5 não tem artefato de handoff.
  - **Status:** Aberta.

- **Q5 — Projeto dos contract tests:** onde deve viver a suíte provider-neutral?
  - **Opções:**
    - **A)** Em `Tests.Integration/Storage/Contracts`, ampliando o projeto que já cobre flows e `ResourceStore`.
    - **B)** Em um novo `Tests.Storage`, isolado dos testes HTTP e preparado para referenciar cada provider.
  - **Impacto se não decidir:** bloqueia a estrutura física, referências de projeto e comando focado da Fase 3.
  - **Status:** Aberta.

- **Q6 — Contratos adjacentes a `IStorage`:** qual destino de `IMessageStore`, `IReplayCache`,
  `IStorageProvider` e `IStorageSession`?
  - **Opções:**
    - **A)** Inventariar todos; classificar message/replay como infraestrutura fora de `Data.*` e provider/session como
      seam de lifetime do adapter, salvo nova decisão específica.
    - **B)** Tratar todos como persistência operacional candidata a `Data.Operational`.
    - **C)** Inventariar, mas exigir decisão individual por contrato na Fase 2.
  - **Impacto se não decidir:** a classificação configuração×operacional fica incompleta.
  - **Status:** Aberta.

- **Q7 — Atomicidade de uso único:** qual é o critério de paridade para authorization code e consumo/update de refresh
  token sob concorrência?
  - **Opções:**
    - **A)** Exigir operação atômica/condicional no comportamento alvo; registrar redesign de contrato para o Plano 3.
    - **B)** Preservar get+remove/update separados e considerar a serialização responsabilidade exclusiva do caller.
  - **Impacto se não decidir:** não há critério falsificável para single-use/tolerância em provider concorrente.
  - **Status:** Aberta.

- **Q8 — Colisão e repetição de escrita:** como definir create/store/save repetido?
  - **Opções:**
    - **A)** Decidir por store em uma tabela explícita (`reject`, `idempotent no-op`, `upsert`, `replace`), sem política
      global artificial.
    - **B)** Padronizar todas as escritas como upsert.
    - **C)** Preservar exatamente a divergência atual do fake.
  - **Impacto se não decidir:** testes de duplicidade podem estabilizar acidentes de `TryAdd`/indexer.
  - **Status:** Aberta.

- **Q9 — Mutabilidade após leitura:** o comportamento alvo pode depender de alterar a instância retornada pelo store
  sem chamar método de persistência?
  - **Opções:**
    - **A)** Não; somente operações explícitas persistem, e live references do fake são comportamento a descartar.
    - **B)** Sim; adapters devem emular tracking implícito observado no fake.
  - **Impacto se não decidir:** o mesmo teste pode passar no fake e falhar após materialização EF.
  - **Status:** Aberta.

- **Q10 — Comparação de identificadores:** qual regra de case/collation deve valer para realm id/path/domain, client id,
  key id, scope name, resource URI, token handle, subject id e session id?
  - **Opções:**
    - **A)** Decidir por campo protocolar/negocial e registrar comparador explícito na matriz.
    - **B)** Usar `Ordinal` case-sensitive para todos.
    - **C)** Delegar à collation default de cada provider.
  - **Impacto se não decidir:** SQLite, PostgreSQL e dicionários podem resolver a mesma chave de formas diferentes.
  - **Status:** Aberta.

- **Q11 — Expiração e limpeza:** stores devem retornar registros expirados para validação no core ou filtrá-los na
  leitura?
  - **Opções:**
    - **A)** Decidir por tipo e separar semântica de leitura da política de cleanup/TTL.
    - **B)** Todo `Get` trata expirado como ausente.
    - **C)** Todo `Get` retorna o registro; somente callers validam tempo.
  - **Impacto se não decidir:** muda respostas de code/token/consent/session e o desenho dos índices/cleanup do Plano 3.
  - **Status:** Aberta.

- **Q12 — Exclusão de realm:** remover um realm não interno deve apagar em cascata configuração e dados operacionais?
  - **Opções:**
    - **A)** Preservar a cascata total observada no fake.
    - **B)** Impedir exclusão enquanto houver dependências.
    - **C)** Remover configuração e reter dados operacionais conforme política de retenção a definir.
  - **Impacto se não decidir:** afeta integridade referencial, retenção e separação entre os dois `Data.*`.
  - **Status:** Aberta.

- **Q13 — Semântica de `IStorageSession`:** a sessão deve representar somente lifetime/disposal de acesso ou também
  unidade transacional?
  - **Opções:**
    - **A)** Somente lifetime do adapter; transações são locais a operações explícitas dos stores.
    - **B)** Unit of Work/transaction compartilhada entre stores.
    - **C)** Marcar `IStorageProvider`/`IStorageSession` como legado a substituir, após mapear os dois consumidores de cache.
  - **Impacto se não decidir:** o adapter EF não tem boundary de contexto/transação definido.
  - **Status:** Aberta.

- **Q14 — Resources sob redesign:** qual pré-requisito o baseline deve impor ao Plano 2?
  - **Opções:**
    - **A)** Persistir o modelo atual com migration futura assumida e risco explícito.
    - **B)** Migrar primeiro realms/options/clients/keys e bloquear resources até o redesign fechar.
    - **C)** Exigir a conclusão do redesign antes de iniciar qualquer parte do Plano 2.
  - **Impacto se não decidir:** a ordem de migração de configuração não pode ser fechada.
  - **Status:** Aberta.

- **Q15 — Cancelamento e API síncrona:** qual comportamento alvo deve ser registrado?
  - **Opções:**
    - **A)** Exigir que providers honrem `CancellationToken` e registrar `IRealmStore.GetByPath` síncrono como contrato a
      substituir antes/do Plano 2.
    - **B)** Preservar cancelamento best-effort e manter lookup síncrono como requisito.
  - **Impacto se não decidir:** contract tests podem exigir comportamento impossível ou inadequado para I/O real.
  - **Status:** Aberta.

- **Q16 — Ordenação:** quais listagens precisam de ordem determinística?
  - **Opções:**
    - **A)** Exigir ordem somente quando protocolo/regra existente a define; declarar as demais não ordenadas.
    - **B)** Definir ordem determinística para toda listagem no baseline.
    - **C)** Preservar a ordem observada de cada coleção do fake.
  - **Impacto se não decidir:** testes podem depender acidentalmente da enumeração de dictionary ou do plano SQL.
  - **Status:** Aberta.

- **Q17 — Ausência e erro de lookup:** preservar os resultados atuais (`null` em finds, exceção em `GetKeyAsync`, no-op
  em removes) ou normalizar?
  - **Opções:**
    - **A)** Decidir por método conforme nome/contrato e registrar exceções explícitas na matriz.
    - **B)** Normalizar todo lookup ausente para `null` e toda remoção ausente para no-op.
    - **C)** Preservar exatamente o fake sem reavaliação.
  - **Impacto se não decidir:** adapters podem divergir em fluxos de ausência e revogação idempotente.
  - **Status:** Aberta.

---

## Decisões fechadas

- **DF1 — Baseline sem mutação de produção:** este plano caracteriza, testa e documenta; não altera interfaces ou
  código de produção. Gaps que exigem mudança viram requisito explícito dos Planos 2/3. Fonte: resposta humana Q1.1.
- **DF2 — Contract tests reutilizáveis:** a caracterização será uma suíte provider-neutral, executada primeiro contra
  `MemoryStorage` e reutilizável pelos providers EF. Fonte: resposta humana Q2.1.
- **DF3 — Semântica fechada antes da conclusão:** todo comportamento relevante deve terminar classificado como
  preservar, descartar ou substituir; nenhuma ambiguidade de persistência pode ser empurrada silenciosamente aos
  planos EF. Fonte: resposta humana Q3.1.
- **DF4 — Fronteiras de projeto:** contratos permanecem no core; `Data.Configuration`/`Data.Operational` são puros e
  somente `Storage.EntityFramework` os adapta. Fonte: ADR-013 §2.1-2.3.
- **DF5 — Separação por ciclo de vida:** configuração e operacional são famílias de dados distintas; sessão pertence
  ao operacional. Fonte: ADR-013 §2.2/§2.5 e plan-data-macro.
- **DF6 — Realm isolation:** todo store realm-bound deve impedir leitura, mutação e remoção cross-realm. Fonte:
  product.md e ADR-013/014.
- **DF7 — Fake transitório:** `MemoryStorage` serve como implementação inicial da suíte, mas seus acidentes não são
  automaticamente o contrato alvo e não justificam ampliar features do fake. Fonte: ADR-018.
- **DF8 — UserAccounts fora deste storage:** contas e credenciais continuam na família `RoyalIdentity.UserAccounts`;
  nenhum `Data.*` do core passa a possuí-las. Fonte: ADR-013/015/018.
- **DF9 — Keys temporariamente em configuração:** o storage de configuração persiste keys/metadados enquanto o KMS
  não existir; este baseline não cria KMS. Fonte: plan-data-macro Plano 2 e ADR-013.
- **DF10 — Execução local sem dependência externa:** os gates obrigatórios deste baseline usam xUnit e backing em
  memória; provider PostgreSQL não é requisito deste plano. Fonte: ADR-003 e DF2.
- **DF11 — Não estabilizar redesign de resources:** o baseline inventaria o contrato atual, mas não decide nem
  implementa a hierarquia futura de resources/scopes. Fonte: product.md, structure.md e AGENTS.md.

---

## Histórico de decisões

**Criação do plano (escopo e método):**

- **Q1 — O baseline pode alterar contratos/código de produção?**
  - **Opções consideradas:** somente caracterizar (A); permitir normalização neste plano (B).
  - **Resposta Q1.1:** A.
  - **Considerações Q1.1:** o macro-plano define este passo como inventário/caracterização anterior à implementação;
    mudanças de persistência pertencem aos Planos 2/3.
  - **Conclusão Q1:** DF1.
- **Q2 — Qual forma dos testes de caracterização?**
  - **Opções consideradas:** contract tests provider-neutral (A); testes específicos do fake (B); somente matriz (C).
  - **Resposta Q2.1:** A.
  - **Considerações Q2.1:** `UserDirectoryContractTests` já demonstra a execução do mesmo contrato contra dois
    backings; o macro-plano exige testes de caracterização e o Plano 4 trocará o backing.
  - **Conclusão Q2:** DF2.
- **Q3 — O baseline fecha toda semântica relevante antes de concluir?**
  - **Opções consideradas:** fechar no baseline (A); permitir diferidos sem decisão (B).
  - **Resposta Q3.1:** A.
  - **Considerações Q3.1:** os providers EF precisam receber critérios de paridade falsificáveis, não reproduzir o
    fake por tentativa.
  - **Conclusão Q3:** DF3.

---

## Design alvo

### Contratos e bordas

- **Catálogo de storage:** uma linha por método/propriedade pública com, no mínimo: owner; lifecycle; binding de realm;
  callers; comportamento atual; fonte normativa; classificação `preservar|descartar|substituir`; cenário de teste;
  plano destino; questão/decisão relacionada.
- **Contract suite test-only:** abstração de fixture/provider cria o backing, realms e dados necessários sem expor
  `MemoryStorage` aos cenários. A primeira fixture usa `AddInMemoryStorage`; providers futuros adicionam fixtures sem
  duplicar os testes.
- **Setup separado de comportamento:** a fixture pode ter hooks test-only para inserir configuração e inspecionar
  estado, porque os contratos atuais não oferecem writes de clients/resources; esses hooks não viram API do core por
  efeito deste plano.
- **Extensões incluídas:** `ResourceStoreExtensions.ResolveAuthorizedSubsetAsync` entra no inventário por carregar
  semântica de storage consumida pelos handlers, embora não seja método de interface.
- **Borda de conta excluída:** `IUserDirectory` e suas portas não entram no catálogo de storage do core; somente
  `IUserSessionStore` entra por ser obtido de `IStorage` e pertencer a `Data.Operational`.

### Modelo, dados e persistência

Este plano não define tabelas. O artefato de classificação deve usar o seguinte mapa conceitual já decidido, deixando
Q6 explícita para as superfícies adjacentes:

```text
Configuration (ADR-013 / plan-data-macro)
  ServerOptions global
  Realm + RealmOptions
  Client
  IdentityScope / ResourceServer / Scope / ProtectedResource (shape atual; Q14)
  KeyParameters (até KMS)

Operational (ADR-013 / plan-data-macro)
  AuthorizeParameters (classificação final depende de Q6)
  AccessToken
  RefreshToken
  AuthorizationCode
  Consent + ConsentedScope
  UserSession + UserSessionClient

Adjacent infrastructure (destino depende de Q6/Q13)
  IMessageStore
  IReplayCache
  IStorageProvider / IStorageSession
```

### Arquitetura alvo

```text
RoyalIdentity/
  Contracts/Storage/                 contratos existentes; não alterados neste plano
  Users/Contracts/IUserSessionStore  store operacional existente

<projeto definido em Q5>/
  Storage/Contracts/                 cenários provider-neutral
  Storage/Support/                   fixture abstrata + fixture MemoryStorage

<artefato definido em Q4>
  catálogo método×semântica×teste×destino

Futuro, fora deste plano:
  RoyalIdentity.Data.Configuration/
  RoyalIdentity.Data.Operational/
  RoyalIdentity.Storage.EntityFramework[.Sqlite|.PostgreSql]/
```

### Segurança, concorrência e confiabilidade

- Isolamento por realm é obrigatório inclusive quando chaves/handles iguais existem em realms diferentes.
- Authorization codes permanecem single-use; refresh-token tolerance permanece; a primitiva concorrente exata
  depende de Q7.
- Remoções/revogações que já são declaradas idempotentes não podem passar a falhar por ausência sem decisão Q17.
- Keys usadas para validação continuam consultáveis após expirar para assinatura; keys futuras não entram em
  `ListAllKeysIdsAsync(now)`.
- Nenhum teste pode depender de live reference, collation, ordem ou overwrite acidental antes de Q8-Q10/Q16.
- Cancellation, lifetime/transação e cleanup precisam de decisão Q11/Q13/Q15 antes da matriz final.

### Compatibilidade, migração e rollout

- Durante este plano, o host e a suíte continuam usando `MemoryStorage` como default.
- Os contract tests novos rodam somente contra o fake até existir fixture EF; isso não declara o fake como destino.
- Cada requisito `substituir` deve apontar para Plano 2 ou 3 e declarar qual teste ficará vermelho/pendente até a nova
  implementação existir, sem alterar produção neste plano.
- O Plano 4 só pode migrar testes de fluxo após os Planos 2/3 atenderem a matriz e substituírem os acessos diretos ao
  fake mapeados na Fase 4.

---

## Ordem de execução

1. **Fase 1 (Inventário)** — cria a lista completa antes de classificar ou testar por amostragem.
2. **Fase 2 (Classificação)** — separa ownership/lifecycle antes de desenhar fixtures e handoffs.
3. **Fase 3 (Contract tests)** — trava somente invariantes referenciados e comportamentos já decididos.
4. **Fase 4 (Seeds/dependências)** — mapeia como remover setup e inspeção direta do fake.
5. **Fase 5 (Paridade/ordem)** — resolve Q7-Q17, ajusta a suíte ao alvo e entrega a sequência aos próximos planos.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

---

## Fase 1 - Inventário de contratos, consumidores e comportamento atual

**Depende de:** DF1, DF3, DF4, DF7 e Q4.

**Escopo:** `RoyalIdentity/Contracts/Storage`, `IUserSessionStore`, `ResourceStoreExtensions`, implementações
`RoyalIdentity.Storage.InMemory`, consumidores no core/server e artefato definido em Q4.

**O que/como:** gerar o catálogo por inspeção estática e confirmar cada linha na implementação fake e nos callers.
Não classificar comportamento como obrigatório apenas porque existe no fake.

**Tarefas:**

- [ ] Listar todas as interfaces, propriedades, métodos e extensões que compõem a superfície de storage.
- [ ] Mapear cada método de `IStorage` ao getter/propriedade, implementação fake e dicionário subjacente.
- [ ] Mapear consumidores por símbolo, incluindo caches, middleware, handlers, defaults, responses e server wiring.
- [ ] Registrar assinatura, retorno de ausência, exceções, duplicate-write, mutabilidade, ordem, cancelamento e tempo.
- [ ] Registrar quais regras possuem fonte normativa e quais são apenas comportamento observado.
- [ ] Registrar todos os testes existentes que cobrem cada linha direta ou incidentalmente.
- [ ] Executar `rg` de verificação e anexar os comandos/resultados resumidos ao artefato.

**Critérios de aceite:** 100% dos membros públicos dos contratos inventariados; cada membro aponta para implementação,
callers, cobertura atual e fonte/ausência de fonte; nenhum comportamento sem referência está marcado `preservar`.

**Testes:** inspeção documental com `git diff --check`; `dotnet build RoyalIdentity.sln` somente se a fase adicionar
artefato compilável além de documentação.

### Resultado da Fase 1

*a preencher*

---

## Fase 2 - Classificação por ciclo de vida e fronteira

**Depende de:** Fase 1, DF4, DF5, DF8, DF9, Q6 e Q14.

**Escopo:** catálogo do baseline, ADR-013, macro-plano, contratos adjacentes e modelo atual de resources/options.

**O que/como:** atribuir owner e lifecycle a cada superfície sem desenhar schema. Marcar explicitamente o que não vai
para `Data.*` e o que está bloqueado por redesign.

**Tarefas:**

- [ ] Classificar `ServerOptions`, realms/options, clients, resources/scopes e keys como configuração conforme fontes.
- [ ] Classificar tokens, codes, consents e sessões como operacional conforme fontes.
- [ ] Fechar a classificação de authorize parameters e contratos adjacentes conforme Q6.
- [ ] Registrar dependências cross-store de cada operação e se cruzam Configuration×Operational.
- [ ] Marcar tipos do core que precisarão de mapping pelo adapter, sem copiá-los para `Data.*` neste plano.
- [ ] Marcar a instabilidade de resources/scopes e aplicar o gate decidido em Q14.
- [ ] Verificar que nenhuma entidade/porta de `UserAccounts` foi incluída em `Data.*`.

**Critérios de aceite:** toda linha tem exatamente um owner (`Configuration`, `Operational`, `Adapter/Infrastructure` ou
`fora do storage`); nenhuma linha fica `a definir`; dependências cross-store e bloqueios de redesign estão explícitos.

**Testes:** `dotnet test Tests.Architecture --no-restore`; `git diff --check`.

### Resultado da Fase 2

*a preencher*

---

## Fase 3 - Contract tests reutilizáveis

**Depende de:** Fases 1-2, DF2, DF3, DF6, DF7, DF10, Q5 e decisões específicas aplicáveis a cada cenário.

**Escopo:** projeto definido em Q5; fixture test-only provider-neutral; fixture `MemoryStorage`; contratos classificados
nas Fases 1-2.

**O que/como:** criar testes por contrato/comportamento, não por detalhe de dictionary. A fixture abstrai setup e
inspeção provider-specific. Antes da Fase 5, adicionar apenas invariantes com fonte ou perguntas já respondidas.

**Tarefas:**

- [ ] Criar a abstração test-only de fixture com lifecycle isolado por teste e controle de tempo quando necessário.
- [ ] Criar a fixture `MemoryStorage` sem expor o tipo concreto aos cenários provider-neutral.
- [ ] Criar grupos de contrato por store, com nomes que descrevam comportamento e não implementação.
- [ ] Cobrir isolamento por realm com handles/ids iguais em dois realms para cada store realm-bound.
- [ ] Cobrir regras normativas já fechadas de realms internos, enabled resources, keys atuais/históricas, sessão,
      revogação idempotente, consent e fluxos single-use no nível permitido pelas decisões.
- [ ] Cobrir ausência, duplicidade, ordem, expiração, mutabilidade, cancelamento e concorrência somente após a respectiva
      Q7-Q17 estar respondida.
- [ ] Relacionar cada teste à linha do catálogo e eliminar cobertura duplicada que não agrega contrato.
- [ ] Garantir que a suíte focada não requer Podman, PostgreSQL, rede ou relógio real.

**Critérios de aceite:** todos os comportamentos marcados `preservar` até esta fase têm teste provider-neutral; a fixture
in-memory executa a suíte verde; nenhum teste referencia `ConcurrentDictionary`, `RealmMemoryStore` ou getters de setup
do fake; cada cenário aponta para fonte/decisão.

**Testes:** comando focado definido após Q5, mais `dotnet test RoyalIdentity.sln`.

### Resultado da Fase 3

*a preencher*

---

## Fase 4 - Seeds, dados globais e dependências entre stores

**Depende de:** Fases 1-3, DF1, DF7, DF8 e catálogo definido em Q4.

**Escopo:** `MemoryStorage`, `RealmMemoryStore`, server/demo/internal realms, tests/server composition e fixture de
contract tests.

**O que/como:** separar seed necessário de produto/dev, fixture de teste e acesso acidental ao fake; mapear ordem de
criação e dependências sem criar seed público de produção.

**Tarefas:**

- [ ] Inventariar `ServerOptions`, realms internos/demo, clients, resources/scopes, keys, authorize parameters e dados
      operacionais criados estaticamente ou por teste.
- [ ] Inventariar as 55 ocorrências atuais de getters diretos do fake e classificar setup, inspeção ou dependência real.
- [ ] Mapear dependências mínimas de seed: realm→options→resources/clients/keys e realm→dados operacionais.
- [ ] Separar seed de host/demo, fixture compartilhada e dados específicos de cenário.
- [ ] Definir no catálogo a substituição futura de cada acesso direto por facade, fixture ou seed do provider.
- [ ] Registrar dependências de `UserAccounts` apenas no nível de composição do teste, sem mover suas tabelas/seeds.
- [ ] Validar que a fixture provider-neutral consegue criar dois realms isolados e dados com ids colidentes.

**Critérios de aceite:** todo seed/dado global e todo acesso direto identificado tem owner, finalidade, dependências e
destino; não há setup indispensável escondido em mutation de dictionary sem estratégia test-only; a ordem de seed é
falsificável pela fixture.

**Testes:** contract suite focada; testes de integração afetados pelo refactor exclusivamente test-only; ao final,
`dotnet test Tests.Integration --no-restore`.

### Resultado da Fase 4

*a preencher*

---

## Fase 5 - Paridade obrigatória e ordem de migração

**Depende de:** Fases 1-4, DF3, respostas Q7-Q17 e macro-planos 2-4.

**Escopo:** catálogo final, contract suite, roadmap/macro/backlog e handoff para os três planos seguintes.

**O que/como:** decidir cada comportamento observado, revisar testes para o alvo e ordenar stores por dependência e
risco. Nenhuma decisão de schema/provider entra aqui.

**Tarefas:**

- [ ] Resolver Q7-Q17 com respostas humanas e mover conclusões para `Decisões fechadas`/`Histórico de decisões`.
- [ ] Classificar cada comportamento como `preservar`, `descartar` ou `substituir`, com fonte e plano destino.
- [ ] Remover/ajustar testes que tenham cristalizado comportamento descartado durante a caracterização.
- [ ] Garantir teste provider-neutral para todo comportamento final marcado `preservar`.
- [ ] Produzir lista explícita de mudanças públicas requeridas antes/durante os Planos 2/3, sem implementá-las.
- [ ] Ordenar stores de configuração e operacionais respeitando dependências e Q14.
- [ ] Definir o gate que permite ao Plano 4 trocar o backing default e remover acessos ao fake.
- [ ] Atualizar `plan-data-macro.md`, `plans-roadmap-02.md`, backlog e AGENTS.md com o resultado real quando concluído.
- [ ] Executar a suíte completa e registrar contagens, skips e limitações.

**Critérios de aceite:** zero perguntas abertas; toda linha do catálogo tem classificação final, teste ou justificativa
de descarte, owner e plano destino; ordem de migração não contém dependência circular; Planos 2/3/4 conseguem usar o
artefato sem inferir semântica; suíte completa verde.

**Testes:** contract suite focada; `dotnet build RoyalIdentity.sln`; `dotnet test RoyalIdentity.sln`.

### Resultado da Fase 5

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(es) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| Objetivo 1 - inventário completo | 1 | DF1, DF3 | 100% dos membros com implementação/callers/cobertura/fonte | buscas `rg`; `git diff --check` |
| Objetivo 2 - classificação de ownership | 2 | DF4, DF5, DF8, DF9, Q6, Q14 | toda linha com owner único e dependências | `dotnet test Tests.Architecture --no-restore` |
| Objetivo 3 - contract suite reutilizável | 3 | DF2, DF6, DF7, DF10, Q5, Q7-Q17 | comportamentos preservados cobertos sem tipo fake nos cenários | filtro de storage após Q5; solução completa |
| Objetivo 4 - seeds/dependências | 4 | DF1, DF7, DF8 | todo acesso direto/seed com finalidade e destino | contract suite; `Tests.Integration` |
| Objetivo 5 - paridade e ordem | 5 | DF3, Q7-Q17 | zero ambiguidades; handoff executável | build + solução completa |

---

## Invariantes a preservar

1. Toda leitura/escrita de clients, keys, resources, tokens, codes, consents e sessões permanece realm-scoped.
2. O core não referencia implementação de storage; `Data.*` futuros não referenciam o core; somente o adapter conhece
   ambos.
3. `UserAccounts` mantém persistência própria e não entra nos `Data.*` do IdP.
4. Authorization codes continuam single-use e refresh-token tolerance não é removida por simplificação de storage.
5. Keys usadas para assinar continuam disponíveis para validação conforme sua janela histórica.
6. Consent continua isolado por realm/subject/client e preserva scopes/lifetime conforme regra do core.
7. Sessão continua pura, serializável, realm-bound, sem `HttpContext` no store e com clients deduplicados.
8. Realms internos continuam não removíveis; alteração de identidade imutável não é introduzida pelo baseline.
9. O fake não recebe feature parity nova como destino; mudanças neste plano limitam-se a testes e documentação.
10. Nenhum comportamento sem fonte ou resposta humana é promovido a critério de paridade.

---

## Critérios globais de conclusão

- As cinco fases estão concluídas e seus `Resultado da Fase` registram entregáveis, decisões, testes e desvios.
- Não existem perguntas abertas nem linhas `a definir` no catálogo.
- Todo membro público de storage e toda extensão relevante tem owner, lifecycle, realm binding, semântica e destino.
- Todo comportamento `preservar` possui contract test provider-neutral verde contra `MemoryStorage`.
- Todo comportamento `substituir` aponta para Plano 2/3 com mudança requerida e teste de aceite definido.
- Todos os seeds/acessos diretos ao fake possuem estratégia de migração para o Plano 4.
- A ordem de migração por store e os gates entre Planos 2, 3 e 4 estão documentados.
- `dotnet build RoyalIdentity.sln` e `dotnet test RoyalIdentity.sln` estão verdes, com skips justificados.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| Cristalizar acidente do fake | teste depende de live reference, dictionary order, collation ou overwrite sem fonte | provider EF precisa emular comportamento incorreto | DF3; gate Q7-Q17; revisão da suíte na Fase 5 | Aberto |
| Inventário incompleto | membro/caller não aparece no catálogo | Plano 2/3 descobre contrato tarde | buscas por interface, implementação e call site; critério de 100% na Fase 1 | Aberto |
| Fixture vira API administrativa | setup de teste motiva write contract público no baseline | expansão de contrato/escopo | DF1; hooks exclusivamente test-only | Mitigado |
| Atomicidade insuficiente | teste concorrente resgata/consome o mesmo handle duas vezes | violação OAuth/OIDC e revogação insegura | resolver Q7; requisito explícito para Plano 3 | Aberto |
| Colisão cross-realm no schema futuro | fixture usa somente ids globalmente únicos | vazamento ou unique index incorreto | todos os contract tests realm-bound usam ids iguais em dois realms | Aberto |
| Modelo de resources muda após persistência | Q14 aceita persistir shape instável sem gate | migration/schema retrabalhados | resolver Q14 e registrar ordem/bloqueio | Aberto |
| Split Configuration×Operational exige transação não definida | operação cruza stores e Q13 não fecha boundary | consistência parcial em falha | mapear dependências na Fase 2; resolver Q13 antes da Fase 5 | Aberto |
| Suíte provider-neutral acopla ao fake | cenários fazem cast ou acessam `RealmMemoryStore` | providers futuros não reutilizam testes | fixture separada; critério negativo na Fase 3 | Aberto |
| Baseline cresce para implementação | tarefa cria `Data.*`, EF ou muda core | plano deixa de ser auditável | DF1 e Fora de escopo; diferir com requisito/teste | Mitigado |

---

## Diferidos e backlog

- Implementação de configuração EF — destino: `plan-data-configuration-storage.md`.
- Implementação operacional EF e primitivas atômicas aprovadas — destino: `plan-data-operational-storage.md`.
- Troca do backing default, migração de testes HTTP e remoção do fake — destino: `plan-data-test-migration.md`.
- Cache sobre stores estáveis — destino condicional: `plan-data-caching.md`.
- Auditoria durável/outbox — destino condicional: `plan-data-audit-outbox.md`.
- Redesign completo de resources/scopes — destino definido após Q14; não implementar implicitamente neste plano.
- KMS e retirada futura das keys do Configuration storage — destino: `plan-kms.md`/ADR própria.
- API/UI administrativa e writes de configuração motivados por administração — destino: `plan-admin-api-ui.md` e
  plano de contratos correspondente.

---

## Referências

- [template-ai-implementation-plan.md](../references/template-plan/template-ai-implementation-plan.md).
- [plan-data-macro.md](plan-data-macro.md).
- [plans-roadmap-02.md](plans-roadmap-02.md).
- [plan-users-accounts-sqlite-hardening.md](plan-users-accounts-sqlite-hardening.md).
- [plan-realm-options-redesign.md](plan-realm-options-redesign.md).
- [ADR-003](../../adrs/ADR-003.md).
- [ADR-013](../../adrs/ADR-013.md).
- [ADR-014](../../adrs/ADR-014.md).
- [ADR-017](../../adrs/ADR-017.md).
- [ADR-018](../../adrs/ADR-018.md).
- [product.md](../foundation/product.md).
- [tech.md](../foundation/tech.md).
- [structure.md](../foundation/structure.md).
- [architecture.md](../foundation/architecture.md).
- [code-style.rules.md](../rules/code-style.rules.md).
- `RoyalIdentity/Contracts/Storage/*.cs`.
- `RoyalIdentity/Users/Contracts/IUserSessionStore.cs`.
- `RoyalIdentity/Contracts/Storage/ResourceStoreExtensions.cs`.
- `RoyalIdentity.Storage.InMemory/*.cs`.
- `Tests.Integration/Storage/ResourceStoreTests.cs`.
- `Tests.Integration/Realm/RealmIsolationTests.cs`.
- `Tests.UserAccounts/UserDirectoryContractTests.cs`.
