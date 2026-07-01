# Plan: Endurecimento do backing `UserAccounts` — concorrência resiliente, migrations e seed (`plan-users-accounts-sqlite-hardening`)

## Status: EM ANDAMENTO - Fase 1: primitiva + atributo/gerador concluídos nas libs (SmartCommands); aplicação no royal-identity pendente

## Progresso

`░░░░░░░░░░░░` **0%** - 0 de 3 fases (Fase 1 em andamento: peças de biblioteca prontas)

| Fase | Estado |
|---|---|
| Fase 1 - Concorrência resiliente (retry no handler) | Em andamento (libs OK; aplicação no royal-identity pendente) |
| Fase 2 - Migrations dos providers (`.Sqlite`/`.PostgreSql`) | Pendente |
| Fase 3 - Seed reutilizável e módulo como backing de testes | Pendente |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluida, `%` e `X de 3`). Ex.: 1 fase => `████░░░░░░░░` **33%** - 1 de 3.
> Antes de fechar uma fase, confirme que as decisões registradas para ela foram aplicadas.

---

## Contexto

A review-006 da Fase 10 do [plan-users-security-lifecycle.md](plan-users-security-lifecycle.md) confirmou três
lacunas entre o que está decidido e o que está implementado no backing do módulo `RoyalIdentity.UserAccounts`:

1. **Concorrência (alta):** a ADR-017 §2.9 decide *"optimistic concurrency + retry"* para mutações de
   credencial/contadores/stamp. Hoje só a **detecção** existe; o **retry** não.
2. **Regressão OIDC opt-in (média):** a suíte do IdP roda contra o fake por default; a regressão contra o módulo é
   representativa (5 testes HTTP). Rodar a suíte inteira contra o módulo depende de substituir o fake (ADR-018).
3. **Seed (baixa/média):** a ADR-018 aponta um seed reutilizável do módulo como primeiro passo para aposentar o fake;
   hoje o seed é duplicado (test factory + contract test).

Este plano endurece o backing **módulo + Sqlite/PostgreSql** até ele poder substituir o fake in-memory nos testes,
seguindo a direção já fixada na [ADR-018](../../adrs/ADR-018.md). Cobre três eixos: o **retry** (no módulo), as
**migrations** (nos providers) e o **seed/backing** (nos testes). O projeto `RoyalIdentity.UserAccounts.Sqlite` é o fio
condutor (o backing concreto a endurecer), mas o retry mora no módulo e o seed nos testes.

### Estado atual do código (verificado nesta sessão)

- **Token de concorrência existe e detecta conflito.** `UserAccount.Version` (uint, incrementado em `Touch()`) é
  mapeado como `IsConcurrencyToken()` no [UserAccountMap.cs:36](../../RoyalIdentity.UserAccounts/Infrastructure/Data/Mappings/UserAccountMap.cs#L36)
  e sobrescrito para `xmin`/`xid` `ValueGeneratedOnAddOrUpdate` no
  [UserAccountsPostgreSqlModelBuilderExtensions.cs:20-26](../../RoyalIdentity.UserAccounts.PostgreSql/UserAccountsPostgreSqlModelBuilderExtensions.cs#L20-L26).
  `Tests.UserAccounts/ConcurrencyTests.cs` e `UserAccountsPersistenceTests.UserAccount_Version_RejectsConcurrentUpdates`
  provam que o conflito é **detectado** (`DbUpdateConcurrencyException`).
- **Retry não existe no fluxo real.** Os use cases de mutação usam `[Command, WithValidateModel, WithWorkContext]`; o
  pipeline gerado é `Execute → CompleteAsync` (salva **uma vez**). Uma `DbUpdateConcurrencyException` propaga; não há
  laço de recarregar+reaplicar+salvar. O retry dos 7 cenários só existe **manualmente nos testes**.
- **Sem migrations.** Não há diretório de migrations em `RoyalIdentity.UserAccounts.Sqlite` nem em
  `.PostgreSql`. O único caminho de criação de schema é `AddUserAccountsSqliteInMemory()` →
  `EnsureDatabaseCreated()` (in-memory, testes). `AddUserAccountsSqlite(connectionStringName)` (file-based) **não tem**
  caminho de criação/evolução de schema.
- **Seed duplicado.** `Tests.Integration/Prepare/UserAccountsAppFactory.cs` (hosted service) e o
  `UserDirectoryContractTests.UserAccountsSqlite` semeiam Alice/Bob por caminhos próprios; tendem a divergir.
- **Fake é o default de testes.** `Tests.Integration`/`AppFactory` usa `RoyalIdentity.Storage.InMemory`; o módulo é
  opt-in via `UserAccountsAppFactory`. Direção fixada (ADR-018): convergir para o módulo, sem investir no fake.

### Consumo dos use cases de mutação (a mapear na Fase 1)

Mutações que tocam credencial/contador/stamp/estado do agregado e portanto entram no escopo de retry:
`AuthenticateLocalCredential`, `ChangeOwnPassword`, `ChangeUserAccountPassword`, `ChangeExpiredPasswordWithToken`,
`ResetPasswordWithToken`, `UnlockPasswordCredential`, `BlockUserAccount`, `UnblockUserAccount`. **Atenção:** alguns
**consomem token de ação antes de mutar o agregado** (`ResetPasswordWithToken`, `ChangeExpiredPasswordWithToken`, e os
fluxos de verificação) — o consumo é idempotente (UPDATE condicional) e **não** deve ser re-executado num retry, sob
pena de "consumir de novo" e falhar.

---

## Objetivo

1. Tornar o backing do módulo **resiliente a conflitos de concorrência** no fluxo real (cumprir a ADR-017 §2.9), não só
   detectar.
2. Dar aos providers um caminho de **schema versionado** (migrations), saindo do `EnsureCreated`.
3. Entregar um **seed reutilizável** do módulo e habilitar o módulo como **backing de testes**, executando o primeiro
   passo da ADR-018.

## Fora de escopo

- Migração do **storage do core** (realms/clients/keys/sessions/tokens) de in-memory para EFCore — pertence ao futuro
  `plan-data-persistence` (backlog "Persistência de Dados").
- **Remover** `RoyalIdentity.Storage.InMemory` por completo — depende também do storage do core (acima); aqui apenas se
  habilita o módulo como backing das contas.
- **Crescer o fake** para paridade de comportamento — recusado pela ADR-018.
- Novas features de ciclo de segurança (o `plan-users-security-lifecycle` está concluído).
- Provisionamento de PostgreSql em CI (se não houver runner), salvo decisão na Q7.

## Decisões fechadas

- **DF1 — Estratégia de concorrência:** optimistic concurrency + retry sobre `UserAccount.Version`; **consumo de token
  é exceção** (UPDATE condicional idempotente, sem retry). Fonte: Q11 / ADR-017 §2.9.
- **DF2 — Token por provider:** Sqlite usa o `uint Version` manual marcado `IsConcurrencyToken`; PostgreSql usa `xmin`.
  Já implementado; permanece.
- **DF3 — Eventos após o commit:** o despacho pós-commit (Fase 9) permanece; o retry recarrega/reaplica **antes** do
  commit, então não há evento despachado para uma tentativa que falhou.
- **DF4 — Direção do fake:** o fake in-memory é transitório; convergir para módulo + Sqlite; seed reutilizável é o
  primeiro passo. Fonte: ADR-018.
- **DF5 — Mecanismo de retry (Opção A):** o retry é emitido pelo **gerador do `Command`** via **atributo opt-in**
  (`[WithRetryOnConcurrency]`), com a **primitiva do laço no `WorkContext`** (captura `ConcurrencyException` → `CleanUp()`
  → re-tenta até a política). Política: **default 3 tentativas, sem backoff**, configurável (appsettings); ao esgotar,
  `Problems.InvalidState` (409, `typeId` `user_account.concurrency_conflict`). `AuthenticateLocalCredential` fica **fora**
  do retry (DF/Q4 — best-effort). Fluxos com token usam retry **escopado** ao agregado (consumo fora do laço). Implica
  evolução **aditiva** de `RoyalCode.SmartCommands` e `RoyalCode.WorkContext`. Fonte: Q1–Q4.

  - **DF5.1 — Esgotamento:** ao esgotar, a primitiva retorna `Problems.InvalidState` com **mensagem genérica fixa** de
    conflito de concorrência (não a `ex.Message` crua, que vaza detalhe do EF). A primitiva aceita um parâmetro opcional
    `Func<Problem>? onExhausted` para customizar o Problem; **o caminho gerado (atributo) sempre usa a mensagem genérica**
    (atributo não passa delegate), e a customização fica disponível só no **caminho manual** (fluxos com token / chamada
    direta). Customizar no caminho gerado fica como evolução futura (atributo apontando um membro da classe do comando).
  - **DF5.2 — Quantidade:** `[WithRetryOnConcurrency]` **sem argumento** usa `MaxAttempts` das options (appsettings;
    default **3**); `[WithRetryOnConcurrency(n)]` sobrescreve com o literal. O gerador distingue presença do argumento
    pela sintaxe (`ArgumentList.Arguments.Count`) e injeta `IOptions<RetryOnConcurrencyOptions>` no handler quando o
    argumento está ausente. **Caso de borda:** literal `<= 0` ⇒ **diagnostic** no gerador; em runtime a primitiva faz
    **clamp** defensivo (`MaxAttempts < 1 ⇒ 1`, isto é, executa uma vez sem retry). `n = 1` é válido (sem retry).
  - **DF5.3 — Transação:** entre tentativas, se houver transação corrente (`GetCurrentTransaction() != null`), a primitiva
    faz **rollback** antes do `CleanUp()` e do retry — senão comandos já enviados na tentativa falha não são desfeitos
    (duplicação/erro). Necessário porque o `CompleteAsync` só reverte quando o `SaveAsync` **retorna** Result falho; no
    conflito o `SaveAsync` **lança** `ConcurrencyException` antes do bloco de rollback. Hoje o `UserAccounts` roda com
    `BeginTransactions = false` (sem transação), então é correção **defensiva**, mas obrigatória na primitiva genérica.
  - **DF5.4 — Lar da primitiva (decidido na implementação):** a primitiva mora em **`RoyalCode.SmartCommands.WorkContext`**
    (o adapter WorkContext do SmartCommands), **não** em `RoyalCode.WorkContext.Abstractions` (EnterprisePatterns). Razão:
    a abstração base do WorkContext **não** referencia `RoyalCode.SmartProblems` (`Result`/`Problem`); colocá-la lá acoplaria
    a camada de aplicação à abstração de persistência. O `SmartCommands.WorkContext` já referencia `IWorkContext` +
    `ConcurrencyException` + `SmartProblems` + `IOptions`. **Consequência:** a Fase 1 das libs toca **apenas o repo
    `SmartCommands`** (primitiva + atributo + gerador juntos); o **`EnterprisePatterns` não é alterado**. A extensão é
    `IUnitOfWork.RetryOnConcurrencyAsync(...)` no namespace `RoyalCode.WorkContext` (já importado pelo handler gerado), com
    corpo `Func<Task<Result>>` (a lambda captura o `ct` externo para evitar `CS0136`).

## Histórico de decisões

> Q1-Q9 já foram respondidas. Esta seção preserva o histórico das alternativas avaliadas e as decisões finais que
> orientam as fases abaixo.

**Fase 1 (retry):**

- **Q1 — Onde mora o retry e como compõe com `[WithWorkContext]`?** Opções levantadas: (a) abandonar
  `[WithWorkContext]` nos use cases de credencial e gerir laço próprio (`IWorkContext` + recarregar + reaplicar +
  `ChangeTracker.Clear()` + salvar); (b) um helper reutilizável de retry chamado dentro do `Execute`; (c) um decorator
  no pipeline do SmartCommand/WorkContext, se existir seam para isso. Precisa verificar se o WorkContext expõe gancho de
  retry/execução antes de decidir.

  - **Respostas Q1.1**: Avaliar na lib de SmartCommand em `..\..\SmartCommands\src\`. Também avaliar WorkContext e compania em `..\..\EnterprisePatterns\RoyalCode.EnterprisePatterns\`.
    Verificar o que pode ser feito, se não há boa saída nativa, verificar qual a dificuldade de elaborar uma nova funcionalidade reutilizável para retry.

  - **Considerações Q1** (investigado nas libs): **(1) Não há retry nativo.** `UnitOfWork<TDbContext>.SaveAsync`
    captura `DbUpdateConcurrencyException` e **relança como `ConcurrencyException`** (RoyalCode); `UnitOfWorkAccessor.
    CompleteAsync` chama `SaveAsync` e deixa a exceção propagar — não há laço de retry em lugar nenhum. **No fluxo real,
    portanto, o que chega ao handler é `ConcurrencyException`, não a `DbUpdateConcurrencyException` crua** (que é o que os
    testes atuais capturam, pois salvam direto no DbContext). **(2) Blocos de construção existem:** `ConcurrencyException`,
    `UnitOfWork.CleanUp()` (faz `Detached` nas entries), transações, e o seam **`IDecorator<TModel,TResult>.HandleAsync(
    cmd, next, ct)`** habilitado por `[WithDecorators]`, que **envolve o handler inteiro** (o `next()` re-roda
    `BeginAsync → load → Execute → CompleteAsync`). **(3) Custo de criar reuso: baixo-moderado.** Um **decorator de
    retry** (~poucas dezenas de linhas) serve os fluxos de **mutação pura** (auth, troca com senha atual, unlock, block,
    unblock): captura `ConcurrencyException`, chama `CleanUp()` para destacar as entries (senão o reload no `next()`
    devolve a entidade obsoleta do change-tracker), re-tenta até o limite. **(4) Fluxos com token NÃO servem ao retry
    de handler inteiro:** `TryConsumeAsync` roda um `ExecuteUpdateAsync` **imediato** (consome no banco antes do
    `CompleteAsync`); re-rodar o handler re-consome → `false` → InvalidToken. Esses precisam de retry **escopado só à
    operação do agregado**, após o consumo (liga com a Q3).

  - **Q1 — Conclusão (parcial — resta 1 decisão):** a parte factual está respondida: **não há saída nativa** e a
    dificuldade de criar uma reutilizável é **baixa-moderada**. Desenho recomendado em **dois níveis**: (a) **decorator
    `[WithDecorators]` de retry** (captura `ConcurrencyException` + `CleanUp()` + limite) para os use cases de mutação
    pura; (b) um **helper de retry escopado** (recarrega agregado → reaplica → salva) para os fluxos com token, chamado
    **após** o consumo. **Decisão pendente do autor:** aprovar o desenho em dois níveis, ou preferir um único helper
    manual em todos os use cases (abandonando `[WithDecorators]` nos afetados). Default, se não houver objeção: dois
    níveis.

    > **⚠ SUPERSEDED por Q1.2:** a opção (a) "decorator de retry" **não funciona** — o `[WithDecorators]` envolve só o
    > `Execute`, e o save (onde nasce a `ConcurrencyException`) ocorre depois. Ver Considerações/Conclusão Q1.2 abaixo.

  - **Respostas Q1.2** (correção do autor): o `[WithDecorators]` fica **entre a chamada do comando e o save changes** —
    não dá para capturar a exception e re-tentar ali. Como reutilizável, ver algo integrado no **WorkContext**, ou no
    **source generator do `Command`** (gerando código com retry acoplado), e/ou algo integrado no `UnitOfWorkAccessor`.

  - **Considerações Q1.2** (confirmado no gerador — você está certo): em
    `CommandHandlerGenerator.GenerateImplementation`, o decorator envolve **apenas** `command.Execute(...)` (vira a
    lambda do mediator de decorators), e o `CompleteUnitOfWorkCommand` encadeia o `CompleteAsync`/**save depois**, via
    `.ContinueAsync`. Logo o **decorator de retry está descartado** (a `ConcurrencyException` nasce no save, fora do
    decorator). **Building blocks confirmados:** `IWorkContext` (via `IUnitOfWork`) expõe **`CleanUp(bool)`** (faz
    `Detached` nas entries) e `SaveAsync`; o `CompleteAsync` deixa a `ConcurrencyException` propagar. Então um laço
    `{Begin → reload → reaplicar → CompleteAsync}` com `CleanUp()` entre tentativas é viável. **Opções reutilizáveis:**
    - **A) O gerador do `Command` emite o laço (atributo opt-in, ex.: `[WithRetryOnConcurrency(max)]`).** Ele já monta
      `validate → Begin → finds → Execute → Complete`; com o atributo, envolve `{Begin → finds → Execute → Complete}`
      num `for` que captura `ConcurrencyException`, chama `Context.CleanUp()` e re-tenta. **Zero boilerplate** por use
      case; os `finds` re-rodam frescos. Custo: evoluir o gerador externo `RoyalCode.SmartCommands` (aditivo/opt-in,
      retrocompatível) + testes + release. Como **você é dono da lib**, é o lar certo de um concern de framework.
    - **B) Primitiva no WorkContext/accessor + o gerador injeta o corpo como delegate.** Ex.:
      `RetryOnConcurrencyAsync(Func<CT,Task<Result>> body, options, ct)` no WorkContext (dona do laço + `CleanUp` +
      política); o gerador emite o corpo como lambda e chama. Laço **testável na lib**, gerador mais fino. Toca **duas**
      libs (WorkContext + gerador).
    - **C) Sem mexer nas libs: helper de retry no módulo (serviço injetável, não util estático).** Os use cases de
      credencial **abandonam** o auto-save de `[WithWorkContext]`, injetam `IWorkContext` e chamam o helper com o corpo
      `{reload → mutar → SaveAsync}`, que faz o laço + `CleanUp()`. Contido neste repo; mais verboso; perde a convenção
      de auto-save nesses use cases.
    - **Ressalva (Q3) vale para todas:** o corpo re-tentado **não** pode incluir o consumo do token. Mutação pura →
      A/C direto; fluxos com token → retry **escopado** só ao agregado (consumo fora do laço), tipicamente no padrão C.
    - **Recomendação:** **A** (ergonomia de atributo) com a **primitiva da B** dentro do WorkContext (laço testável) —
      1 atributo novo + 1 método de runtime + fiação no gerador; e **C escopado** nos 2–3 fluxos com token. Se preferir
      **não** evoluir o SmartCommands agora, **C em tudo** destrava o plano sem tocar libs.

  - **Respostas Q1.3**: Opção A.

  - **Q1 — Conclusão (fechada):** **Opção A** — o **gerador do `Command`** (`RoyalCode.SmartCommands`) passa a emitir o
    laço de retry via **atributo opt-in** (ex.: `[WithRetryOnConcurrency]`), com a **primitiva do laço no `WorkContext`**
    (testável; captura `ConcurrencyException`, chama `CleanUp()`, aplica a política). Os use cases de **mutação pura**
    recebem o atributo; os **fluxos com token** usam retry **escopado** ao agregado (consumo fora do laço — Q3). Vira a
    **DF5**. Implica evoluir os repos `RoyalCode.SmartCommands` e `RoyalCode.WorkContext` (de propriedade do autor) de
    forma **aditiva/retrocompatível**, com nova versão consumida pelo `royal-identity`.

- **Q2 — Política de retry:** número máximo de tentativas, com ou sem backoff, e o que retornar ao esgotar (qual
  `Problems.*`? propagar exceção?).

  - **Respostas Q2.1**: Penso que pode ser configurável via appsettings isso, a nível de implantação, com valor fixo.
    Para o valor fixo, ver o que o mercado geralmente usa.
    O retorno é melhor ser um Problem, com algum código de estado bom. Acredito que InvalidState, 409, seria bom aqui. Validar.

  - **Considerações Q2**: validado — em `RoyalCode.SmartProblems`, `Problems.InvalidState` → **409 Conflict** (a própria
    doc da lib exemplifica `Problems.InvalidState("Resource is locked by another process")`, exatamente a semântica de
    conflito). 409/InvalidState está correto e idiomático. Sobre o número fixo: o padrão de mercado para **retry de
    concorrência otimista** é pequeno — tipicamente **3 tentativas**, **sem backoff exponencial** (backoff serve a falhas
    transitórias/distribuídas; aqui a tentativa seguinte só recarrega e re-aplica, imediatamente). Não confundir com o
    `EnableRetryOnFailure` do EF (default 6), que é para **falha transitória de conexão**, não concorrência.

  - **Q2 — Conclusão:** atende. Decisão: ao esgotar, retornar `Problems.InvalidState` (409) com `typeId` próprio (ex.:
    `user_account.concurrency_conflict`); **default 3 tentativas, sem backoff**, configurável via appsettings (valor de
    implantação, fixo no default).

- **Q3 — Fluxos com token:** confirmar o **escopo** do retry (só a mutação do agregado, nunca o consumo do token) e
  revisar caso a caso `ResetPasswordWithToken`, `ChangeExpiredPasswordWithToken` e os fluxos de verificação.

  - **Respostas Q3.1**: Deve validar o consumo do token antes de tentar a execução. Não sei se isso responde, se não é necessário melhorar a questão.

  - **Considerações Q3**: sua intuição aponta na direção certa, mas o ponto preciso não é "validar o consumo antes de
    executar" — é o **escopo do retry**. Como `TryConsumeAsync` roda um UPDATE condicional **imediato** (consome no banco
    antes do save do agregado), qualquer retry que **re-execute o consumo** falha na 2ª tentativa. A regra precisa: **o
    laço de retry envolve apenas {recarregar agregado → reaplicar mutação → salvar}, nunca o consumo do token**. O
    consumo permanece único, **uma vez, fora do laço** (já é idempotente por natureza). É exatamente o helper escopado da
    Q1(b).

  - **Q3 — Conclusão (refinada):** atende com a regra acima. Fechado: nos fluxos com token, consumir o token **uma vez,
    fora do laço de retry**; o retry cobre só a operação do agregado. Depende do mecanismo aprovado na Q1.

- **Q4 — Semântica do contador de autenticação:** o contador de falhas exige retry estrito, ou tolera-se "perda
  eventual de incremento" sob contenção extrema (o pré-plano §10, cenário 2, listou "aceitar eventualidade pequena")?
  Afeta se `AuthenticateLocalCredential` entra ou não no laço de retry.

  - **Respostas Q4.1**: não entendi o problema da questão, quais seriam as opções e impacto?

  - **Considerações Q4 (explicação):** o problema: sob **concorrência real** de duas falhas de senha na mesma conta,
    **sem** retry um incremento se perde — ambas leem a versão N do contador, uma sobrescreve a outra, e o contador fica
    em N+1 em vez de N+2. As opções e impacto:
    - **Opção A — retry estrito também na autenticação:** o contador fica exato; o lockout dispara na tentativa precisa.
      Custo: a autenticação é o caminho **mais quente**; sob contenção (ex.: brute-force concorrente) o retry adiciona
      recargas, e um atacante martelando geraria re-tentativas extras.
    - **Opção B — best-effort na autenticação (sem retry só nela; as demais mutações de credencial seguem com retry):**
      tolera-se a perda de **1 incremento apenas sob corrida exata**. Impacto de segurança: marginal — o lockout ainda
      dispara, só **1–2 tentativas depois** num cenário raro. O pré-plano §10 (cenário 2) já listou "aceitar
      eventualidade pequena" como aceitável.
    - **Trade-off:** exatidão do contador × custo no hot-path de auth. Como o lockout é um **throttle**, não um portão
      rígido, B costuma ser aceitável; mas é uma decisão de **risco sua**.

  - **Q4 — Continua:** qual opção? **(A)** retry estrito na auth; ou **(B)** best-effort sem retry na auth (demais
    mutações de credencial sob retry).

  - **Respostas Q4.2**: Opção B.

  - **Q4 — Conclusão:** atende. Decisão: **Opção B** — `AuthenticateLocalCredential` fica **fora** do laço de retry; as
    demais mutações de credencial (troca/reset/unlock/block) entram. Consequência aceita: sob corrida exata de falhas, o
    contador pode subir 1 a menos (lockout dispara 1–2 tentativas depois) — aceitável por ser **throttle**, não portão.

**Fase 2 (migrations):**

- **Q5 — Agora ou junto do `plan-data-persistence`?** Gerar as migrations do módulo já neste plano, ou coordenar com a
  migração do storage do core para uma convenção única de migrations/`__EFMigrationsHistory`?

  - **Respostas Q5.1**: migrations do UserAccounts agora, não esperar plan-data-persistence. O módulo tem providers próprios. Migrations deve opções de rodar junto com o módulo ou via script (manual), para atender cenários de clientes diferentes.

  - **Considerações Q5**: o "rodar junto com o módulo OU via script" é padrão e barato: a mesma migration pode ser
    aplicada por `db.Database.Migrate()` no startup (automático, opt-in) **e** gerada como **script SQL**
    (`dotnet ef migrations script`) para o DBA aplicar (controlado) — cobre clientes diferentes. A coordenação de
    nomenclatura com o futuro `plan-data-persistence` pode vir depois sem retrabalho (cada DbContext tem seu próprio
    `__EFMigrationsHistory`).

  - **Q5 — Conclusão:** atende. Decisão: gerar migrations do módulo **agora**, por provider; oferecer **aplicação opt-in
    via `Migrate()`** e **geração de script SQL** para aplicação manual; não esperar o `plan-data-persistence`.

- **Q6 — Testes continuam com `EnsureCreated`?** Manter `EnsureDatabaseCreated()` para os testes in-memory e usar
  migrations só para file/PostgreSql, ou aplicar migrations também nos testes (mais fiel, mais lento)?

  - **Respostas Q6.1**: manter EnsureCreated para testes unitários/rápidos in-memory, mas adicionar testes específicos com Migrate() em SQLite file/in-memory compartilhado.

  - **Considerações Q6**: bom equilíbrio — `EnsureCreated` mantém os testes rápidos; um conjunto pequeno com `Migrate()`
    prova que a migration **cria um schema funcional** (a falha clássica é o `EnsureCreated` divergir da migration e o
    bug só aparecer em produção).

  - **Q6 — Conclusão:** atende. Decisão: `EnsureCreated` no in-memory dos testes rápidos; testes dedicados aplicando
    `Migrate()` (SQLite file/in-memory compartilhado) para validar a fidelidade da migration.

- **Q7 — Convenção e CI:** localização/nomenclatura das migrations por provider, `IDesignTimeDbContextFactory`, e se há
  runner PostgreSql para validar a migration PG em CI (ou se fica validada só localmente).

  - **Respostas Q7.1**: Cada provider (PG e Sqlite) tem finalidades diferentes, então pode ser feito algo em cada um. Na questão de CI entraria nas considerações da Q5.

  - **Considerações Q7**: estrutura por provider ok — pasta `Migrations/` em cada projeto de provider, com
    `IDesignTimeDbContextFactory` por provider (necessário para o `dotnet ef` achar o contexto sem subir o host). O ponto
    que a Q5 **não** fechou é o **CI do PostgreSql**: validar a migration PG exige um Postgres real; sem runner PG no CI,
    a migration PG fica validada **localmente** (a do Sqlite roda no CI normalmente).

  - **Q7 — Continua (estreito):** há (ou deve haver) um serviço Postgres no CI para validar a migration PG, ou aceitamos
    **validação local apenas** da PG por ora? (O resto — localização/nomenclatura/design-time factory — está fechado.)

  - **Respostas Q7.2**: Podemos numa fase mais adiante criar uns projetos de .Net Aspire com Postgres e validar as coisas por lá. Até rodar versões completas.

  - **Considerações Q7.2**: ok — validação PG por projetos **.NET Aspire** (Postgres real) numa fase posterior, capaz de
    rodar versões completas/integração. Para **este** plano, a migration PG é **gerada e validada localmente**; a
    validação automatizada (Aspire) não bloqueia a Fase 2 e fica registrada como trabalho futuro.

  - **Q7 — Conclusão:** atende. Decisão: migrations por provider + `IDesignTimeDbContextFactory`; **Sqlite validada no
    CI** normalmente; **PG validada localmente** agora; **validação automatizada via .NET Aspire + Postgres** diferida
    para fase futura (registrar como item de escopo futuro/backlog).

**Fase 3 (seed/backing):**

- **Q8 — Forma e lar do seed:** paridade mínima Alice/Bob (igual ao seed atual) ou fixture mais rica? O seed é
  test-only (helper em `Tests.*`) ou uma **API pública de seed do módulo** reutilizável também por deploy/demo real?

- **Respostas Q8.1**: seed test-only por enquanto, em Tests.*; não criar API pública de seed no módulo.

  - **Considerações Q8**: ok test-only, sem API no módulo. Detalhe estrutural: o seed é consumido por **dois projetos de
    teste** (`Tests.Integration` na `UserAccountsAppFactory` e `Tests.UserAccounts` no contract test). Para ser artefato
    **único**, precisa de um **lar comum** alcançável pelos dois (um pequeno projeto de suporte de testes, ou um arquivo
    compartilhado/linked) — a definir na Fase 3.

  - **Q8 — Conclusão:** atende. Decisão: seed reutilizável **test-only**, em local compartilhado entre `Tests.Integration`
    e `Tests.UserAccounts`; **sem** API pública de seed no módulo.

- **Q9 — Virar o default de testes:** trocar o default de `Tests.Integration` para o módulo (flip), ou manter dual
  (fake default + módulo opt-in) até que o storage do core também migre? Rodar a suíte inteira contra o módulo é o alvo,
  mas o custo/sequência depende desta decisão.

- **Respostas Q9.1**: não virar o default inteiro ainda. Ampliar regressão opt-in primeiro; o flip completo combina melhor com plan-data-persistence.

  - **Considerações Q9**: alinhado com a correção já feita na Fase 10 (regressão opt-in representativa). Ampliar a
    cobertura opt-in antes de qualquer flip reduz risco; o flip completo só faz sentido quando o storage do **core**
    também migrar — daí casar com o `plan-data-persistence`.

  - **Q9 — Conclusão:** atende. Decisão: manter **dual** (fake default + módulo opt-in), **ampliar a regressão opt-in**
    nesta Fase 3; flip completo diferido para o `plan-data-persistence`.

---

## Ordem de execução

1. **Fase 1 (retry)** — maior valor e maior risco de design; destrava o cumprimento da ADR-017 §2.9.
2. **Fase 2 (migrations)** — independe da Fase 1; pode correr em paralelo, mas listada depois por prioridade.
3. **Fase 3 (seed/backing)** — depende de a Fase 2 ter o schema versionado (se Q6 decidir migrations nos testes) e
   consolida a direção da ADR-018.

---

## Fase 1 - Concorrência resiliente (retry no handler)

**Depende de:** Q1–Q4 (**todas decididas** — ver DF5).

**Escopo cross-repo:** a Opção A evolui de forma **aditiva/retrocompatível** o repo `..\..\SmartCommands` (primitiva +
atributo + gerador, todos no projeto `RoyalCode.SmartCommands.WorkContext`/`.Generators` — ver DF5.4), mais a aplicação
no `royal-identity`. O `EnterprisePatterns` **não** é tocado. Os repos avançam via versão local dos pacotes até o release.

**O que/como:** cumprir a ADR-017 §2.9. No fluxo real a exceção é **`ConcurrencyException`** (o `UnitOfWork.SaveAsync` a
converte da `DbUpdateConcurrencyException`); o retry recarrega o agregado na versão atual, reaplica a mutação e salva,
até a política. Mecanismo (DF5): **primitiva de laço no `WorkContext`** + **atributo opt-in emitido pelo gerador do
`Command`**; fluxos com token usam retry **escopado** (consumo fora do laço).

**Tarefas:**

*RoyalCode.SmartCommands.WorkContext (primitiva) — CONCLUÍDO:*

- [x] Adicionada a **primitiva de retry** `IUnitOfWork.RetryOnConcurrencyAsync(Func<Task<Result>> body,
      RetryOnConcurrencyOptions options, Func<Problem>? onExhausted = null, CT ct)` em
      `Extensions/ConcurrencyRetryExtensions.cs` (namespace `RoyalCode.WorkContext`; laço: executa o corpo → captura
      `ConcurrencyException` → **rollback se houver transação** (DF5.3) → `CleanUp()` → re-tenta até a política). Ao
      esgotar, retorna `onExhausted?.Invoke()` ou `Problems.InvalidState` genérico (DF5.1). `RetryOnConcurrencyOptions
      { MaxAttempts = 3 }` em `Options/`, sem backoff, com **clamp** `< 1 ⇒ 1` (DF5.2). **8 testes** em
      `Tests/Components/ConcurrencyRetryTests.cs` (sucesso direto; sucesso após N-1 falhas; esgotamento → InvalidState;
      `onExhausted` honrado; rollback + `CleanUp` entre tentativas; bordas de `MaxAttempts` 0/1/negativo) — verdes.

*RoyalCode.SmartCommands (atributo + gerador) — CONCLUÍDO:*

- [x] Criado o atributo **`[WithRetryOnConcurrency]` / `[WithRetryOnConcurrency(int maxAttempts)]`** (opt-in) e o
      `CommandHandlerGenerator` **envolve** `{Begin → finds → Execute → Complete}` numa lambda `async () => {…}` passada à
      primitiva (`this.accessor.Context.RetryOnConcurrencyAsync(…)`) — a **validação fica fora** do laço; os **finds
      dentro**. Sem argumento ⇒ injeta `IOptions<RetryOnConcurrencyOptions>` e passa `this.retryOptions.Value`; com
      argumento ⇒ `new RetryOnConcurrencyOptions { MaxAttempts = n }`; literal `<= 0` ⇒ **diagnostic `RCCMD025`**; sem
      WorkContext ⇒ **diagnostic `RCCMD024`** (DF5.2/DF5.4). Retrocompat: sem o atributo, o `.g.cs` é idêntico. **4 testes**
      em `Tests/Generators/RetryOnConcurrencyTests.cs` (snapshot com options, snapshot com valor, RCCMD024, RCCMD025) +
      **suíte do SmartCommands verde (77 testes)** + **solução `SmartCommands.sln` compila (0 erros)**.

*royal-identity (módulo) — PENDENTE (não solicitado nesta entrega):*

- [ ] Aplicar `[WithRetryOnConcurrency]` aos use cases de **mutação pura** (`ChangeOwnPassword`,
      `ChangeUserAccountPassword`, `UnlockPasswordCredential`, `BlockUserAccount`, `UnblockUserAccount`).
- [ ] **`AuthenticateLocalCredential` fica de fora** (DF5/Q4 — best-effort).
- [ ] Fluxos com token (`ResetPasswordWithToken`, `ChangeExpiredPasswordWithToken`, verificações): retry **escopado** ao
      agregado, **após** o consumo do token (consumo permanece único, fora do laço — Q3).
- [ ] Mapear o esgotamento para `Problems.InvalidState` (409, `typeId` `user_account.concurrency_conflict`).
- [ ] Substituir o retry **manual** dos `ConcurrencyTests` por asserção do comportamento resiliente **do handler** (os 7
      cenários passam a exercitar o retry real); cobrir esgotamento e o caso de fluxo com token sob conflito.

**Critérios de aceite:** primitiva + atributo entregues com testes nas libs; duas tentativas concorrentes de credencial
**não** geram exceção não tratada (viram retry ou `InvalidState` ao esgotar); os 7 cenários passam exercitando o retry do
handler; fluxos com token não re-consomem o token sob retry; auth permanece sem retry (best-effort); nenhum evento
despachado para tentativa não-commitada.

**Testes:** testes das libs (primitiva + gerador); `Tests.UserAccounts` (concorrência via Sqlite in-memory
compartilhado, agora sem retry manual); regressão completa da solução.

### Resultado da Fase 1

**Parcial — peças de biblioteca concluídas (2026-06-28); aplicação no `royal-identity` pendente.**

Entregue no repo `SmartCommands` (aditivo/retrocompatível; `EnterprisePatterns` intocado — DF5.4):

- **Primitiva** `RoyalCode.SmartCommands.WorkContext/Extensions/ConcurrencyRetryExtensions.cs` +
  `Options/RetryOnConcurrencyOptions.cs`. Estende `IUnitOfWork` (namespace `RoyalCode.WorkContext`), corpo
  `Func<Task<Result>>` (lambda captura o `ct` externo — evita `CS0136`). Laço: `body` → `ConcurrencyException` →
  rollback (se transação) → `CleanUp()` → re-tenta; ao esgotar, `onExhausted` ou `Problems.InvalidState` genérico;
  clamp `MaxAttempts < 1 ⇒ 1`.
- **Atributo + gerador**: `WithRetryOnConcurrencyAttribute` (`RoyalCode.SmartCommands`) + leitura no `Transform`,
  campos `HasRetryOnConcurrency`/`RetryMaxAttempts` na `CommandHandlerInformation`, novo nó
  `Commands/RetryOnConcurrencyCommand.cs`, injeção condicional de `IOptions<RetryOnConcurrencyOptions>` e diagnostics
  `RCCMD024`/`RCCMD025`. O corpo `{Begin → finds → Execute → Complete}` vai para um `bodyTarget` separado quando há
  retry; a validação permanece no corpo do método.
- **Testes**: 8 (primitiva) + 4 (gerador: 2 snapshots `.g.cs`, 2 diagnostics). **Suíte `SmartCommands` 77/77 verde**;
  **`SmartCommands.sln` compila com 0 erros** (warning `CS8785` no `Tests.Models` é pré-existente — versão de analyzer no
  ambiente, sem relação com esta entrega).

**Falta** (grupo `royal-identity`, ainda não solicitado): consumir a nova versão dos pacotes; aplicar
`[WithRetryOnConcurrency]` nos use cases de mutação pura; retry escopado nos fluxos com token; mapear esgotamento para o
`typeId` `user_account.concurrency_conflict`; substituir o retry manual dos `ConcurrencyTests`. Enquanto a Fase 1 não
fechar este grupo, a **tabela de progresso permanece em "Em andamento"** (não conta como fase concluída).

---

## Fase 2 - Migrations dos providers (`.Sqlite`/`.PostgreSql`)

**Depende de:** Q5, Q6, Q7.

**O que/como:** dar schema versionado aos providers, saindo do `EnsureCreated`. Migration inicial por provider
refletindo os mapeamentos atuais (incluindo índices parciais de primário único e o token de concorrência por provider).

**Tarefas:**

- [ ] Aplicar as decisões Q5–Q7 (migrations agora; testes rápidos com `EnsureCreated`; testes dedicados com `Migrate()`;
      Sqlite no CI e PostgreSql validado localmente por ora).
- [ ] `IDesignTimeDbContextFactory` por provider (se necessário para o tooling de migrations).
- [ ] Migration inicial `.Sqlite` refletindo o modelo atual; validar criação de schema file-based.
- [ ] Migration inicial `.PostgreSql` (incluindo `xmin` como token e índices parciais com sintaxe PG).
- [ ] Definir o caminho de testes (manter `EnsureCreated` in-memory vs aplicar migrations — Q6).

**Critérios de aceite:** decisões Q5–Q7 aplicadas; `AddUserAccountsSqlite` (file) cria/evolui schema via migration;
migration PG gerada e validada localmente; validação automatizada via .NET Aspire registrada como futuro; testes
existentes seguem verdes.

**Testes:** round-trip de persistência sobre o schema migrado; suíte do módulo verde.

### Resultado da Fase 2

*a preencher*

---

## Fase 3 - Seed reutilizável e módulo como backing de testes

**Depende de:** Q8, Q9; e da Fase 2 (se Q6 decidir migrations nos testes).

**O que/como:** extrair o seed Alice/Bob hoje duplicado para um **artefato único reutilizável** e habilitar o módulo
como backing de testes, executando o primeiro passo da ADR-018.

**Tarefas:**

- [ ] Aplicar as decisões Q8 (seed test-only compartilhado) e Q9 (dual mantido; regressão opt-in ampliada).
- [ ] Criar o seed reutilizável; fazer `UserAccountsAppFactory` e o contract test `UserAccountsSqlite` consumirem o
      mesmo artefato (eliminando a duplicação).
- [ ] Conforme Q9, ampliar a regressão OIDC contra o módulo (além dos 5 testes representativos) e/ou preparar o flip do
      default.
- [ ] Atualizar [plan-users-accounts-test-matrix.md](plan-users-accounts-test-matrix.md) e a nota da ADR-018/backlog
      com o estado real.

**Critérios de aceite:** Q8–Q9 decididas; seed único consumido pelos dois caminhos; regressão opt-in ampliada conforme
decisão; matriz/backlog atualizados; suíte verde.

**Testes:** contract tests fake×módulo; regressão OIDC opt-in (ampliada conforme Q9); suíte completa.

### Resultado da Fase 3

*a preencher*

---

## Invariantes a preservar

1. O **módulo puro** continua sem referência ao core `RoyalIdentity` nem a ASP.NET; só a `.Integration` conhece o IdP.
2. **Token bruto** (de ação) nunca entra em evento/auditoria/log; consumo idempotente permanece.
3. **Eventos após o commit** (Fase 9) — retry não despacha evento de tentativa não-commitada.
4. O **fake permanece como default** até a substituição decidida na ADR-018; nada de investir em paridade do fake.
5. Toda query de conta/credencial/token/property continua **filtrada por realm**.

## Critérios globais de conclusão

- As três fases concluídas com as decisões registradas aplicadas.
- ADR-017 §2.9 cumprida no fluxo real (retry), não só na detecção.
- Providers com schema versionado; seed único; regressão contra o módulo no nível decidido na Q9.
- `dotnet test RoyalIdentity.sln` verde.

## Riscos

- **Evolução cross-repo do retry** (Q1): a Opção A depende de mudanças aditivas em `RoyalCode.SmartCommands` e
  `RoyalCode.WorkContext`; controlar versionamento local, release dos pacotes e consumo no `royal-identity` antes de
  aplicar os atributos no módulo.
- **Fluxos com token sob retry** (Q3): retry mal escopado re-consome token e quebra o fluxo; exige revisão caso a caso.
- **Migration PG sem runner** (Q7): risco de a migration PG só ser validada localmente; registrar a limitação.
- **Flip do default de testes** (Q9): trocar o backing default pode expor divergências fake×módulo de uma vez; preferir
  incremental se o risco for alto.

## Referências

- [ADR-017](../../adrs/ADR-017.md) §2.9 (concorrência), §2.11 (eventos pós-commit).
- [ADR-018](../../adrs/ADR-018.md) (fake transitório; convergir para módulo + Sqlite; seed reutilizável).
- [plan-users-security-lifecycle.md](plan-users-security-lifecycle.md) — Fase 10 e nota da review-006.
- [plan-users-accounts-module-v2.md](plan-users-accounts-module-v2.md) — providers e backing do módulo.
- [plan-users-accounts-test-matrix.md](plan-users-accounts-test-matrix.md) — matriz de testes.
- [an-users-sec-preplan.md](../analisys/an-users-sec-preplan.md) §10 — os 7 cenários de concorrência.
- Backlog "Substituir o storage fake in-memory…" e "Persistência de Dados" em [backlog-001.md](../backlogs/backlog-001.md).
