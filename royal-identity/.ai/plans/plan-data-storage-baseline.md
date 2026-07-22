# Plan: Baseline dos contratos de storage do IdP (`plan-data-storage-baseline`)

## Status: EM EXECUÇÃO - Fases 1-2 de 5 concluídas

## Progresso

`██░░░` **40%** - 2 de 5 fases concluídas

| Fase | Estado |
|---|---|
| Fase 1 - Inventário de contratos, consumidores e comportamento atual | Concluida |
| Fase 2 - Classificação por ciclo de vida e fronteira | Concluida |
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
  Tests.Integration Tests.UserAccounts -g "*.cs"` e inventário dos arquivos de contratos — evidências do estado
  descrito abaixo.

### Estado atual do código (verificado em 2026-07-21)

- **Não existe backing EF do core:** a solução não contém `RoyalIdentity.Data.Configuration`,
  `RoyalIdentity.Data.Operational`, `RoyalIdentity.Storage.EntityFramework`, `.Sqlite` ou `.PostgreSql`; somente
  `RoyalIdentity.Storage.InMemory` implementa `IStorage`.
- **Gateway atual:** `IStorage` expõe `ServerOptions`, `IRealmStore`, `IAuthorizeParametersStore` e oito stores
  realm-bound: clients, resources, keys, access tokens, refresh tokens, authorization codes, consents e sessões.
- **Contratos adjacentes:** `IMessageStore`, `IReplayCache`, `IStorageProvider` e `IStorageSession` vivem em
  `Contracts/Storage`, mas não são stores obtidos de `IStorage`; `IUserSessionStore` vive em `Users/Contracts` e é
  obtido por `IStorage.GetUserSessionStore(realm)`.
- **Authorize parameters têm estado server-side:** `IAuthorizeParametersStore` guarda `NameValueCollection` entre o
  redirect para login/consent e o callback; a implementação fake usa um handle aleatório em dictionary e o callback
  faz read+delete.
- **Message store atual não armazena server-side:** `ProtectedDataMessageStore.WriteAsync` serializa e protege o
  payload com ASP.NET Data Protection e devolve o próprio ciphertext Base64Url como id; `ReadAsync` desempacota esse
  valor e `DeleteAsync` é no-op. É usado no fluxo de end-session/logout.
- **Replay cache atual é infraestrutura de segurança:** `PrivateKeyJwtSecretEvaluator` consulta `purpose+jti` e registra
  o handle até `exp+5min`; o default DI é `DefaultReplayNoCache`, enquanto existe implementação opcional sobre
  `IDistributedCache`.
- **Provider/session atuais não guardam registros:** somente o key cache usa `IStorageProvider.CreateSession()` para
  obter um `IStorage` de vida curta ao reler o realm e calcular o TTL do cache; no fake a sessão devolve o singleton e
  `Dispose()` é no-op.
- **Binding por realm:** `MemoryStorage` mantém um `RealmMemoryStore` por `Realm.Id`; getters realm-bound lançam
  `ArgumentException` quando o realm não está cadastrado.
- **Exclusão de realm já existe no fake:** `IRealmStore.DeleteAsync` retorna `false` para realm inexistente/interno e,
  para realm comum, remove o registro de realm e o `RealmMemoryStore` inteiro. `RealmIsolationTests` cobre a recusa do
  realm interno e a remoção do realm junto de um authorization code/data store. Não há caller de produção nem
  `IRealmManager.DeleteAsync`; quando o core in-memory é composto com `UserAccounts` real, essa operação não alcança a
  persistência própria do módulo e pode deixar contas órfãs.
- **Vida dos registros:** os stores in-memory guardam e retornam as próprias instâncias mutáveis de modelos do core;
  não há materialização, detach ou cópia entre escrita e leitura.
- **Escritas duplicadas divergentes:** access/refresh token usam `TryAdd`, authorization code e sessão sobrescrevem,
  consent e realm fazem upsert, e key atribui pelo `KeyId`; os contratos não documentam uma política uniforme.
- **Uso único sem transição atômica válida:** `IAuthorizationCodeStore` separa get/remove; o fake de refresh token usa
  `TryUpdate` contra a mesma referência mutável e ignora o resultado. Ambos são evidências a classificar `substituir`,
  não modelos a corrigir/copiar. O provider EF deve cumprir DF15 com transição condicional real.
- **Lookup de key é outlier de ausência:** `IKeyStore.GetKeyAsync` lança `ArgumentException` quando o key id não existe,
  e `GetKeysAsync` propaga essa falha; os demais lookups individuais de realm/client/token/code/consent/session retornam
  `null`. As exceções do resource store tratam inconsistência/ambiguidade da hierarquia, não simples ausência.
- **Configuração sem facades completas de escrita:** `IClientStore`, `IResourceStore` e `IKeyStore` são
  predominantemente de leitura; muitos testes preparam clients/resources/keys mutando diretamente os dicionários de
  `RealmMemoryStore`.
- **Acoplamento dos testes ao fake:** há 56 ocorrências de `GetRealmMemoryStore`/`GetDemoRealmStore`/
  `GetServerRealmStore` em 16 arquivos: 55 ocorrências/15 arquivos em `Tests.Integration` e uma ocorrência adicional em
  `Tests.UserAccounts/UserDirectoryContractTests.cs`, usada para semear diretamente o lado in-memory do contract test.
  A superfície inclui setup de clients, resources, usuários e inspeção de estado operacional.
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
- **Decisões fechadas, detalhamento pendente:** Q1-Q17 estão respondidas. As escolhas por store, campo ou método
  exigidas por DF16, DF18, DF19 e DF25 serão produzidas na matriz durante as fases, sem delegar a semântica à
  implementação fake ou à collation do provider.

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
- `Tests.UserAccounts/UserDirectoryContractTests.cs` — ocorrência adicional de seed direto no fake usada pelo
  precedente de contract tests.
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
- Resolver o redesign de resources/scopes dentro deste baseline — DF22 bloqueia sua persistência até o redesign.
- Implementar PAR (RFC 9126), um `PersistentDataMessageStore` ou redesenhar os stores de authorization request —
  análise em [an-par-rfc-9126.md](../analisys/an-par-rfc-9126.md) e destino no backlog.

---

## Perguntas ao humano

> Q1-Q17 foram respondidas e estão registradas em `Decisões fechadas` e `Histórico de decisões`.
> Não há pergunta aberta antes do início da Fase 1. Se o inventário encontrar uma semântica não coberta por essas
> decisões ou pelas fontes normativas, ela deve voltar ao humano; não pode ser inferida do fake.

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
- **DF12 — Matriz em plano irmão:** o catálogo durável será `.ai/plans/plan-data-storage-matrix.md`, separado deste
  plano executável e seguindo o precedente de `plan-users-accounts-test-matrix.md`. Fonte: resposta humana Q4.1.
- **DF13 — Projeto único de contract tests de data-storages:** criar `Tests.Storage` para hospedar a suíte
  provider-neutral de todos os storages do core, começando por `MemoryStorage` e recebendo as fixtures dos providers
  futuros. Fonte: resposta humana Q5.1.
- **DF14 — Classificação do estado transitório atual:** `IAuthorizeParametersStore` é estado operacional;
  `IMessageStore` e `IReplayCache` são infraestrutura adjacente; `IStorageProvider`/`IStorageSession` representam o
  lifecycle do adapter, com semântica final definida por DF21. PAR e os possíveis novos contratos/implementações de
  mensagens ou authorization requests não fazem parte deste baseline. Fonte: resposta humana Q6.1 e
  `an-par-rfc-9126.md`.
- **DF15 — Consumo concorrente explícito:** authorization code single-use e transições de refresh token exigem
  operações atômicas/condicionais no comportamento alvo. A separação atual get+remove/update é classificada como
  contrato a substituir no Plano 3; a tolerância de refresh token permanece uma regra distinta. Fonte: resposta
  humana Q7.1 e product.md.
- **DF16 — Escrita repetida por operação:** não existe upsert global. A matriz atribui explicitamente `reject`,
  `idempotent no-op`, `upsert` ou `replace` a cada create/store/save/remove conforme a semântica do método. O
  comportamento divergente do fake não é fonte suficiente. Fonte: resposta humana Q8.1 e DF3/DF7.
- **DF17 — Persistência explícita, sem live reference:** cada leitura pode materializar uma nova instância; somente
  operações explícitas persistem alterações. Testes e providers não dependem de identidade de objeto entre leituras,
  e o fake não será ampliado apenas para emular materialização EF. Fonte: resposta humana Q9.1 e ADR-018.
- **DF18 — Comparação definida por campo:** case sensitivity e igualdade são decididas por identificador
  protocolar/negocial e registradas com comparador explícito na matriz. Collation de SQLite/PostgreSQL deve implementar
  essa regra e nunca defini-la implicitamente. Fonte: resposta humana Q10.1 e requisito de paridade entre providers.
- **DF19 — Expiração por tipo, cleanup separado:** a matriz decide por tipo se uma leitura expõe ou oculta um registro
  logicamente expirado. Expiração lógica, retenção necessária ao fluxo e remoção física/TTL são dimensões separadas.
  Fonte: resposta humana Q11.1 e invariantes de code, refresh token, consent e sessão em product.md.
- **DF20 — Exclusão permanente com tombstone configuracional:** excluir realm não interno é uma transição irreversível,
  distinta de `Enabled = false`. Providers EF preservam Configuration como tombstone lógico, invisível aos lookups
  normais e incapaz de atender novas requests; path e domain permanecem reservados enquanto existir o tombstone.
  Dados Operational são removidos fisicamente, e contas devem ser removidas posteriormente pelo próprio
  `UserAccounts`. Cada família mantém ownership; não se assume FK nem transação distribuída. Este baseline registra o
  requisito e o gap do `IRealmStore.DeleteAsync` atual, mas não escolhe saga, evento, chamada direta ou outro seam. A
  coordenação idempotente/retomável e sua semântica de conclusão exigem decisão arquitetural junto ao futuro fluxo
  administrativo de exclusão de realm. Fonte: resposta humana Q12.1/Q12.2, product.md e ADR-013/015.
- **DF21 — `IStorageSession` é lifetime, não Unit of Work global:** session/provider delimitam acesso e disposal do
  adapter. Transações ficam dentro de operações explícitas de cada store; consistência entre famílias não depende de
  uma transação distribuída implícita. Fonte: resposta humana Q13.1 e separação Configuration×Operational da ADR-013.
- **DF22 — Resources bloqueados até o redesign:** o Plano 2 pode persistir realms/options, clients e keys antes do
  redesign, mas não implementa persistência de resources/scopes enquanto o modelo instável não estiver fechado.
  Fonte: resposta humana Q14.1, product.md, structure.md e DF11.
- **DF23 — I/O assíncrono cancelável:** operações que podem fazer I/O devem ser assíncronas, receber
  `CancellationToken` e encaminhá-lo a toda chamada EF assíncrona. Implementações puramente in-memory não precisam
  simular cancelamento de I/O inexistente. `IRealmStore.GetByPath` síncrono é contrato a substituir antes ou durante o
  Plano 2. Fonte: resposta humana Q15.1.
- **DF24 — Ordenação somente por regra:** listagens têm ordem determinística apenas quando protocolo ou regra de
  negócio a define; as demais são declaradas não ordenadas e testadas como conjuntos. Ordem incidental de dictionary
  ou plano SQL não é contrato. Fonte: resposta humana Q16.1 e DF3/DF7.
- **DF25 — Ausência decidida por método:** a matriz decide por nome e semântica de cada operação quando retornar
  `null`, lançar, produzir resultado discriminado ou fazer remoção/revogação idempotente. Não há normalização global
  nem preservação automática do fake. Fonte: resposta humana Q17.1 e DF3.

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
- **Q4 — Onde deve viver o artefato durável do baseline?**
  - **Opções consideradas:** plano irmão em `.ai/plans` (A); incorporar ao plano executável (B); referência separada
    em `.ai/references` (C).
  - **Resposta Q4.1:** A.
  - **Considerações Q4.1:** `plan-users-accounts-test-matrix.md` é o precedente local para uma matriz durável
    consumida por mais de uma fase/plano.
  - **Conclusão Q4:** DF12.
- **Q5 — Onde deve viver a suíte provider-neutral?**
  - **Opções consideradas:** `Tests.Integration/Storage/Contracts` (A); novo projeto `Tests.Storage` (B).
  - **Resposta Q5.1:** B; um único projeto para testar todos os data-storages.
  - **Considerações Q5.1:** o projeto fica independente dos testes HTTP e poderá receber fixtures in-memory, SQLite e
    PostgreSQL sem duplicar os cenários de contrato.
  - **Conclusão Q5:** DF13.
- **Q6 — Como classificar o estado transitório e os contratos adjacentes atuais?**
  - **Opções consideradas:** classificar authorize parameters como operacional e os demais conforme sua função atual
    (A); apenas inventariar e decidir individualmente na Fase 2 (B).
  - **Resposta Q6.1:** A.
  - **Considerações Q6.1:** ownership semântico e backing físico são dimensões distintas. PAR,
    `PersistentDataMessageStore` e um possível redesign de authorization requests foram separados para
    [an-par-rfc-9126.md](../analisys/an-par-rfc-9126.md) e backlog.
  - **Conclusão Q6:** DF14.
- **Q7 — Como garantir uso único e transições de refresh token sob concorrência?**
  - **Opções consideradas:** exigir operação atômica/condicional no alvo (A); manter get+remove/update e delegar
    serialização ao caller (B).
  - **Resposta Q7.1:** A.
  - **Considerações Q7.1:** single-use é invariante de produto; a tolerância de refresh não elimina a necessidade de
    transição condicional.
  - **Conclusão Q7:** DF15.
- **Q8 — Qual política aplicar a escritas repetidas?**
  - **Opções consideradas:** decidir por operação (A); upsert global (B); copiar divergências do fake (C).
  - **Resposta Q8.1:** A; upsert é útil em casos próprios, mas não serve como regra universal.
  - **Considerações Q8.1:** create, save, emissão, remoção e revogação possuem semânticas distintas.
  - **Conclusão Q8:** DF16.
- **Q9 — A persistência pode depender da mesma referência de objeto após leitura?**
  - **Opções consideradas:** exigir persistência explícita e admitir nova materialização (A); emular tracking implícito
    do fake (B).
  - **Resposta Q9.1:** A.
  - **Considerações Q9.1:** cada scope/`DbContext` pode materializar uma nova instância; não serão criados ou ampliados
    fakes apenas para imitar EF.
  - **Conclusão Q9:** DF17.
- **Q10 — Quem define igualdade/case de identificadores?**
  - **Opções consideradas:** regra explícita por campo (A); `Ordinal` global (B); collation do provider (C).
  - **Resposta Q10.1:** A, substituindo a preferência inicial por C após avaliar a divergência entre providers.
  - **Considerações Q10.1:** collation é mecanismo físico para cumprir o contrato; não pode alterar identidade entre
    SQLite, PostgreSQL ou ambientes.
  - **Conclusão Q10:** DF18.
- **Q11 — Como relacionar leitura de expirados e cleanup físico?**
  - **Opções consideradas:** decidir por tipo e separar cleanup (A); ocultar todo expirado (B); retornar todo expirado
    ao core (C).
  - **Resposta Q11.1:** A.
  - **Considerações Q11.1:** codes, refresh tokens, consent e sessões têm ciclos distintos; tolerância e retenção podem
    exigir o registro após ele deixar de ser utilizável.
  - **Conclusão Q11:** DF19.
- **Q12 — Qual semântica de exclusão de realm não interno?**
  - **Opções consideradas:** cascata total (A); bloquear por dependências (B); reter operacional por política ainda não
    definida (C).
  - **Resposta Q12.1:** A, inicialmente refinada como cascata lógica coordenada e retomável.
  - **Resposta Q12.2:** para providers EF, manter Configuration por exclusão lógica permanente e remover fisicamente
    Operational; preservar path/domain do realm e remover posteriormente as contas pelo owner `UserAccounts`.
  - **Considerações Q12.2:** `Deleted` não equivale a `Enabled = false` e não admite restauração normal porque dados
    ativos serão apagados. Lookups normais tratam o tombstone como ausente. O fake pode continuar removendo fisicamente
    desde que testes verifiquem o resultado observável, não linhas internas. O mecanismo cross-family não foi decidido:
    cada família mantém banco/ownership próprios, e saga/evento/chamada/seam ficam para decisão arquitetural do futuro
    fluxo administrativo de exclusão.
  - **Conclusão Q12:** DF20.
- **Q13 — O que representa `IStorageSession`?**
  - **Opções consideradas:** lifetime/disposal (A); Unit of Work cross-store (B); legado a substituir (C).
  - **Resposta Q13.1:** A.
  - **Considerações Q13.1:** transações pertencem às operações de cada store; famílias separadas não recebem Unit of
    Work distribuída implícita.
  - **Conclusão Q13:** DF21.
- **Q14 — Como avançar enquanto resources/scopes estão em redesign?**
  - **Opções consideradas:** persistir o shape atual (A); avançar com realms/options/clients/keys e bloquear resources
    (B); bloquear todo Plano 2 (C).
  - **Resposta Q14.1:** B.
  - **Considerações Q14.1:** permite progresso em configuração estável sem cristalizar o modelo conhecido como
    instável.
  - **Conclusão Q14:** DF22.
- **Q15 — Qual regra de cancelamento e API síncrona?**
  - **Opções consideradas:** I/O assíncrono com `CancellationToken` e substituição do lookup síncrono (A); manter
    best-effort e lookup síncrono (B).
  - **Resposta Q15.1:** A, com a qualificação de que in-memory não precisa simular cancelamento de I/O inexistente.
  - **Considerações Q15.1:** providers EF encaminham `ct` a queries, enumerações e `SaveChangesAsync`; APIs que podem
    acessar I/O não permanecem síncronas.
  - **Conclusão Q15:** DF23.
- **Q16 — Quando uma listagem tem ordem determinística?**
  - **Opções consideradas:** somente quando há regra (A); sempre (B); copiar a ordem do fake (C).
  - **Resposta Q16.1:** A.
  - **Considerações Q16.1:** resultados sem regra são conjuntos; ordenar tudo adicionaria custo e contrato artificial.
  - **Conclusão Q16:** DF24.
- **Q17 — Como tratar lookup/remoção ausente?**
  - **Opções consideradas:** decidir por método (A); normalizar todos (B); copiar o fake (C).
  - **Resposta Q17.1:** A.
  - **Considerações Q17.1:** `Find`, `Get`, consumo, remoção e revogação possuem expectativas diferentes que precisam
    aparecer na matriz.
  - **Conclusão Q17:** DF25.

---

## Design alvo

### Contratos e bordas

- **Catálogo de storage:** `.ai/plans/plan-data-storage-matrix.md`, com uma linha por método/propriedade pública e, no
  mínimo: owner; lifecycle; binding de realm;
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

Este plano não define tabelas. O artefato de classificação deve usar o seguinte mapa conceitual já decidido:

```text
Configuration (ADR-013 / plan-data-macro)
  ServerOptions global
  Realm + RealmOptions
  Client
  IdentityScope / ResourceServer / Scope / ProtectedResource (shape atual bloqueado por DF22)
  KeyParameters (até KMS)

Operational (ADR-013 / plan-data-macro)
  AuthorizeParameters
  AccessToken
  RefreshToken
  AuthorizationCode
  Consent + ConsentedScope
  UserSession + UserSessionClient

Adjacent infrastructure (classificação por DF14; lifecycle definido por DF21)
  IMessageStore
  IReplayCache
  IStorageProvider / IStorageSession
```

### Arquitetura alvo

```text
RoyalIdentity/
  Contracts/Storage/                 contratos existentes; não alterados neste plano
  Users/Contracts/IUserSessionStore  store operacional existente

Tests.Storage/
  Storage/Contracts/                 cenários provider-neutral
  Storage/Support/                   fixture abstrata + fixture MemoryStorage

.ai/plans/plan-data-storage-matrix.md
  catálogo método×semântica×teste×destino

Futuro, fora deste plano:
  RoyalIdentity.Data.Configuration/
  RoyalIdentity.Data.Operational/
  RoyalIdentity.Storage.EntityFramework[.Sqlite|.PostgreSql]/
```

### Segurança, concorrência e confiabilidade

- Isolamento por realm é obrigatório inclusive quando chaves/handles iguais existem em realms diferentes.
- Authorization codes permanecem single-use e refresh-token tolerance permanece; o Plano 3 deve introduzir as
  operações atômicas/condicionais exigidas por DF15.
- Duplicidade, ausência e erro são definidos por operação conforme DF16/DF25, sem política global.
- Keys usadas para validação continuam consultáveis após expirar para assinatura; keys futuras não entram em
  `ListAllKeysIdsAsync(now)`.
- Nenhum teste pode depender de live reference, collation do provider, ordem ou overwrite acidental; aplicar
  DF17/DF18/DF24.
- Expiração/cleanup, lifetime/transação e cancelamento seguem DF19/DF21/DF23.
- Exclusão de realm segue DF20: tombstone permanente em Configuration, purge de Operational e requisito de cleanup por
  `UserAccounts`, sem mover ownership nem escolher neste baseline o seam cross-family.

### Compatibilidade, migração e rollout

- Durante este plano, o host e a suíte continuam usando `MemoryStorage` como default.
- Os contract tests novos rodam somente contra o fake até existir fixture EF; isso não declara o fake como destino.
- Para exclusão de realm, os contract tests provider-neutral verificam somente efeitos observáveis: realm deixa de ser
  resolvido/atender requests e dados ativos ficam inacessíveis. Eles não exigem hard delete físico do provider EF nem
  tombstone físico do fake.
- A reserva de path/domain após o tombstone é requisito `substituir` para o provider EF e recebe teste de aceite no
  Plano 2; não se amplia o fake, que hoje permite recriação após remover fisicamente o realm.
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
5. **Fase 5 (Paridade/ordem)** — aplica DF15-DF25, ajusta a suíte ao alvo e entrega a sequência aos próximos planos.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
dotnet test Tests.Storage
dotnet test RoyalIdentity.sln
```

---

## Fase 1 - Inventário de contratos, consumidores e comportamento atual

**Depende de:** DF1, DF3, DF4, DF7 e DF12.

**Escopo:** `RoyalIdentity/Contracts/Storage`, `IUserSessionStore`, `ResourceStoreExtensions`, implementações
`RoyalIdentity.Storage.InMemory`, consumidores no core/server e `plan-data-storage-matrix.md`.

**O que/como:** gerar o catálogo por inspeção estática e confirmar cada linha na implementação fake e nos callers.
Não classificar comportamento como obrigatório apenas porque existe no fake.

**Tarefas:**

- [x] Listar todas as interfaces, propriedades, métodos e extensões que compõem a superfície de storage.
- [x] Mapear cada método de `IStorage` ao getter/propriedade, implementação fake e dicionário subjacente.
- [x] Mapear consumidores por símbolo, incluindo caches, middleware, handlers, defaults, responses e server wiring.
- [x] Registrar assinatura, retorno de ausência, exceções, duplicate-write, mutabilidade, ordem, cancelamento e tempo.
- [x] Registrar quais regras possuem fonte normativa e quais são apenas comportamento observado.
- [x] Registrar todos os testes existentes que cobrem cada linha direta ou incidentalmente.
- [x] Executar `rg` de verificação e anexar os comandos/resultados resumidos ao artefato.

**Critérios de aceite:** 100% dos membros públicos dos contratos inventariados; cada membro aponta para implementação,
callers, cobertura atual e fonte/ausência de fonte; nenhum comportamento sem referência está marcado `preservar`.

**Testes:** inspeção documental com `git diff --check`; `dotnet build RoyalIdentity.sln` somente se a fase adicionar
artefato compilável além de documentação.

### Resultado da Fase 1

**Concluída em 2026-07-21.** Criado
[plan-data-storage-matrix.md](plan-data-storage-matrix.md) com o inventário estático completo do baseline:

- 15 contratos e uma extensão, totalizando 62 métodos/propriedades contratuais; os sete membros públicos do tipo de
  suporte `ResourceResolution` também foram catalogados;
- cada operação aponta para owner inicial, binding, backing fake, comportamento observado, consumidores, cobertura,
  fonte/classificação inicial, cenário ou lacuna e plano destino;
- os 11 membros de `IStorage` foram ligados aos estados globais ou ao dictionary correspondente de
  `RealmMemoryStore`, e os contratos adjacentes foram separados de `Data.*`;
- foram registrados os gaps de consumo atômico de authorization code, CAS ineficaz de refresh token, lookup de key
  como outlier, exclusão real de realm no fake sem coordenação com `UserAccounts`, resources bloqueados e superfícies
  sem cobertura direta;
- buscas por símbolos confirmaram as 56 referências diretas ao fake (55 em 15 arquivos de `Tests.Integration` e uma
  em `Tests.UserAccounts`), nenhum caller de produção de `IRealmStore.DeleteAsync` e nenhum caller de
  `GetAllResourcesAsync`.

Nenhum comportamento sem ADR, foundation, regra de produto ou decisão DF foi marcado como `preservar`; as semânticas
ainda abertas por store/campo/método permanecem `avaliar` para fechamento na Fase 5. Como a fase alterou somente
documentação, não foi executado build; `git diff --check` concluiu sem erros.

---

## Fase 2 - Classificação por ciclo de vida e fronteira

**Depende de:** Fase 1, DF4, DF5, DF8, DF9, DF14 e DF22.

**Escopo:** catálogo do baseline, ADR-013, macro-plano, contratos adjacentes e modelo atual de resources/options.

**O que/como:** atribuir owner e lifecycle a cada superfície sem desenhar schema. Marcar explicitamente o que não vai
para `Data.*` e o que está bloqueado por redesign.

**Tarefas:**

- [x] Classificar `ServerOptions`, realms/options, clients, resources/scopes e keys como configuração conforme fontes.
- [x] Classificar tokens, codes, consents e sessões como operacional conforme fontes.
- [x] Aplicar a classificação de authorize parameters e contratos adjacentes conforme DF14.
- [x] Registrar dependências cross-store de cada operação e se cruzam Configuration×Operational.
- [x] Marcar tipos do core que precisarão de mapping pelo adapter, sem copiá-los para `Data.*` neste plano.
- [x] Marcar a instabilidade de resources/scopes e aplicar o bloqueio de DF22.
- [x] Mapear o comportamento atual de `IRealmStore.DeleteAsync`, seu teste e o gap com `UserAccounts`; registrar o alvo
      de tombstone Configuration + purge Operational de DF20, sem escolher o seam administrativo cross-family.
- [x] Verificar que nenhuma entidade/porta de `UserAccounts` foi incluída em `Data.*`.

**Critérios de aceite:** toda linha tem exatamente um owner (`Configuration`, `Operational`, `Adapter/Infrastructure` ou
`fora do storage`); nenhuma linha fica `a definir`; dependências cross-store e bloqueios de redesign estão explícitos.

**Testes:** `dotnet test Tests.Architecture --no-restore`; `git diff --check`.

### Resultado da Fase 2

**Concluída em 2026-07-21.** A
[matriz de storage](plan-data-storage-matrix.md) passou a registrar a classificação arquitetural fechada:

- as 62 linhas contratuais possuem exatamente um owner: 24 `Configuration`, 30 `Operational` e 8
  `Adapter/Infrastructure`; os sete membros de suporte `ResourceResolution` pertencem à superfície Configuration,
  mas são resultados transitórios e não entidades;
- o lifecycle decorre explicitamente do owner: configuração durável/baixa rotatividade, operacional
  transitório/alta rotatividade e infraestrutura com lifetime técnico fora de `Data.*`;
- dependências diretas e orquestradas foram separadas, incluindo binding realm Configuration→Operational, grants que
  combinam resources com codes/tokens, revogação, sign-out, key cache e exclusão de realm;
- foram listados os grafos de tipos do core que exigirão mapping no `Storage.EntityFramework`, sem definir schema nem
  permitir referência do `Data.*` ao core;
- resources/scopes e `ResourceResolution` permanecem bloqueados pelo redesign de DF11/DF22, sem bloquear
  realms/options, clients e keys;
- `IRealmStore.DeleteAsync` ficou com owner Configuration e efeito cross-store explícito: tombstone Configuration,
  purge Operational e limpeza posterior pelo próprio `UserAccounts`, sem escolher seam ou transação distribuída;
- portas, entidades e options de `UserAccounts` estão explicitamente fora de ambos os `Data.*`; ids de subject são
  apenas correlação escalar, sem FK/navegação cross-family.

O comando residual da evidência da Fase 1 também foi corrigido para enumerar todos os arquivos de
`Contracts/Storage` e somente `IUserSessionStore.cs` em `Users/Contracts`.

Validação: `dotnet test Tests.Architecture --no-restore` — 15 aprovados, 0 falhas, 0 ignorados; `git diff --check`
sem erros. O build acionado pelo teste manteve apenas warnings preexistentes do repositório.

---

## Fase 3 - Contract tests reutilizáveis

**Depende de:** Fases 1-2, DF2, DF3, DF6, DF7, DF10, DF13 e DF15-DF25.

**Escopo:** `Tests.Storage`; fixture test-only provider-neutral; fixture `MemoryStorage`; contratos classificados
nas Fases 1-2.

**O que/como:** criar testes por contrato/comportamento, não por detalhe de dictionary. A fixture abstrai setup e
inspeção provider-specific. Comportamentos `substituir` que o fake não consegue cumprir ficam como requisitos/testes de
aceite dos planos destino, sem forçar feature parity no fake.

**Tarefas:**

- [ ] Criar a abstração test-only de fixture com lifecycle isolado por teste e controle de tempo quando necessário.
- [ ] Criar a fixture `MemoryStorage` sem expor o tipo concreto aos cenários provider-neutral.
- [ ] Criar grupos de contrato por store, com nomes que descrevam comportamento e não implementação.
- [ ] Cobrir isolamento por realm com handles/ids iguais em dois realms para cada store realm-bound.
- [ ] Cobrir regras normativas já fechadas de realms internos, enabled resources, keys atuais/históricas, sessão,
      revogação idempotente, consent e fluxos single-use no nível permitido pelas decisões.
- [ ] Cobrir exclusão de realm pelo comportamento observável comum ao hard delete do fake e ao tombstone EF, sem
      inspecionar presença física da configuração.
- [ ] Registrar como teste de aceite futuro do Plano 2 que path/domain de tombstone não podem ser reutilizados, sem
      exigir essa feature do fake transitório.
- [ ] Cobrir ausência, duplicidade, ordem, expiração, mutabilidade, cancelamento e concorrência conforme
      DF15-DF19 e DF23-DF25, sem exigir do fake comportamentos classificados `substituir`.
- [ ] Relacionar cada teste à linha do catálogo e eliminar cobertura duplicada que não agrega contrato.
- [ ] Garantir que a suíte focada não requer Podman, PostgreSQL, rede ou relógio real.

**Critérios de aceite:** todos os comportamentos marcados `preservar` até esta fase têm teste provider-neutral; a fixture
in-memory executa a suíte verde; nenhum teste referencia `ConcurrentDictionary`, `RealmMemoryStore` ou getters de setup
do fake; cada cenário aponta para fonte/decisão.

**Testes:** `dotnet test Tests.Storage`; `dotnet test RoyalIdentity.sln`.

### Resultado da Fase 3

*a preencher*

---

## Fase 4 - Seeds, dados globais e dependências entre stores

**Depende de:** Fases 1-3, DF1, DF7, DF8 e DF12.

**Escopo:** `MemoryStorage`, `RealmMemoryStore`, server/demo/internal realms, tests/server composition e fixture de
contract tests.

**O que/como:** separar seed necessário de produto/dev, fixture de teste e acesso acidental ao fake; mapear ordem de
criação e dependências sem criar seed público de produção.

**Tarefas:**

- [ ] Inventariar `ServerOptions`, realms internos/demo, clients, resources/scopes, keys, authorize parameters e dados
      operacionais criados estaticamente ou por teste.
- [ ] Inventariar as 56 ocorrências atuais de getters diretos do fake — 55 em `Tests.Integration` e uma em
      `Tests.UserAccounts/UserDirectoryContractTests.cs` — e classificar setup, inspeção ou dependência real.
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

**Depende de:** Fases 1-4, DF3, DF15-DF25 e macro-planos 2-4.

**Escopo:** catálogo final, contract suite, roadmap/macro/backlog e handoff para os três planos seguintes.

**O que/como:** decidir cada comportamento observado, revisar testes para o alvo e ordenar stores por dependência e
risco. Nenhuma decisão de schema/provider entra aqui.

**Tarefas:**

- [ ] Aplicar DF15-DF25 a cada linha da matriz, detalhando as escolhas exigidas por store, campo, tipo e método.
- [ ] Classificar cada comportamento como `preservar`, `descartar` ou `substituir`, com fonte e plano destino.
- [ ] Remover/ajustar testes que tenham cristalizado comportamento descartado durante a caracterização.
- [ ] Garantir teste provider-neutral para todo comportamento final marcado `preservar`.
- [ ] Produzir lista explícita de mudanças públicas requeridas antes/durante os Planos 2/3, sem implementá-las.
- [ ] Ordenar stores de configuração e operacionais respeitando dependências e o bloqueio de DF22.
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
| Objetivo 1 - inventário completo | 1 | DF1, DF3, DF12 | 100% dos membros com implementação/callers/cobertura/fonte | buscas `rg`; `git diff --check` |
| Objetivo 2 - classificação de ownership | 2 | DF4, DF5, DF8, DF9, DF14, DF20-DF22 | toda linha com owner único e dependências | `dotnet test Tests.Architecture --no-restore` |
| Objetivo 3 - contract suite reutilizável | 3 | DF2, DF6, DF7, DF10, DF13, DF15-DF19, DF23-DF25 | comportamentos preservados cobertos sem tipo fake nos cenários | `dotnet test Tests.Storage`; solução completa |
| Objetivo 4 - seeds/dependências | 4 | DF1, DF7, DF8 | todo acesso direto/seed com finalidade e destino | contract suite; `Tests.Integration` |
| Objetivo 5 - paridade e ordem | 5 | DF3, DF15-DF25 | zero ambiguidades; handoff executável | build + solução completa |

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
11. Persistência não depende de identidade de objeto nem de mutação implícita após leitura.
12. Comparação de identificadores é explícita por campo e independe da collation default do provider.
13. Exclusão de realm não interno é permanente: Configuration mantém tombstone invisível e reserva path/domain;
    Operational é removido; `UserAccounts` remove suas contas por coordenação futura, preservando ownership.
14. Todo I/O assíncrono futuro encaminha o `CancellationToken`; APIs síncronas não escondem acesso a banco.

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
| Cristalizar acidente do fake | teste depende de live reference, dictionary order, collation ou overwrite sem fonte | provider EF precisa emular comportamento incorreto | DF3, DF16-DF18, DF24/DF25; revisão da suíte na Fase 5 | Aberto |
| Inventário incompleto | membro/caller não aparece no catálogo | Plano 2/3 descobre contrato tarde | buscas por interface, implementação e call site; critério de 100% na Fase 1 | Aberto |
| Fixture vira API administrativa | setup de teste motiva write contract público no baseline | expansão de contrato/escopo | DF1; hooks exclusivamente test-only | Mitigado |
| Atomicidade insuficiente | teste concorrente resgata/consome o mesmo handle duas vezes | violação OAuth/OIDC e revogação insegura | DF15; requisito explícito para Plano 3 | Aberto |
| Colisão cross-realm no schema futuro | fixture usa somente ids globalmente únicos | vazamento ou unique index incorreto | todos os contract tests realm-bound usam ids iguais em dois realms | Aberto |
| Modelo de resources muda após persistência | implementação ignora o bloqueio de DF22 | migration/schema retrabalhados | não persistir resources/scopes antes do redesign | Mitigado |
| Split Configuration×Operational tenta usar transação global | operação cruza stores/bancos | acoplamento ou consistência parcial em falha | DF21; mapear orquestração e operações locais na Fase 2 | Mitigado |
| Exclusão de realm falha entre famílias | tombstone existe, mas cleanup de Operational/`UserAccounts` não conclui | dados ativos órfãos após exclusão lógica | DF20 bloqueia novas requests; decidir seam e exigir coordenação idempotente/retomável no fluxo administrativo futuro | Aberto |
| Suíte provider-neutral acopla ao fake | cenários fazem cast ou acessam `RealmMemoryStore` | providers futuros não reutilizam testes | fixture separada; critério negativo na Fase 3 | Aberto |
| Baseline cresce para implementação | tarefa cria `Data.*`, EF ou muda core | plano deixa de ser auditável | DF1 e Fora de escopo; diferir com requisito/teste | Mitigado |

---

## Diferidos e backlog

- Implementação de configuração EF — destino: `plan-data-configuration-storage.md`.
- Implementação operacional EF e primitivas atômicas aprovadas — destino: `plan-data-operational-storage.md`.
- Troca do backing default, migração de testes HTTP e remoção do fake — destino: `plan-data-test-migration.md`.
- Cache sobre stores estáveis — destino condicional: `plan-data-caching.md`.
- Auditoria durável/outbox — destino condicional: `plan-data-audit-outbox.md`.
- Redesign completo de resources/scopes — pré-requisito de DF22; não persistir implicitamente neste plano/Plano 2.
- Seam e semântica de conclusão da exclusão cross-family de realm — decisão arquitetural/ADR do futuro fluxo
  administrativo; este baseline entrega somente requisito, gap e comportamento observável de DF20.
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
