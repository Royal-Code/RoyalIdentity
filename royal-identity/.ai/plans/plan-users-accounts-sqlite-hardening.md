# Plan: Endurecimento do backing `UserAccounts` — concorrência resiliente, migrations e seed (`plan-users-accounts-sqlite-hardening`)

## Status: CONCLUÍDO - Fases 1-3 concluídas (concorrência resiliente; migrations; seed reutilizável)

## Progresso

`████████████` **100%** - 3 de 3 fases

| Fase | Estado |
|---|---|
| Fase 1 - Concorrência resiliente (retry no handler) | Concluida |
| Fase 2 - Migrations dos providers (`.Sqlite`/`.PostgreSql`) | Concluida |
| Fase 3 - Seed reutilizável e módulo como backing de testes | Concluida |

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

*royal-identity (módulo) — CONCLUÍDO:*

- [x] Aplicado `[WithRetryOnConcurrency]` aos use cases de **mutação pura** (`ChangeOwnPassword`,
      `ChangeUserAccountPassword`, `UnlockPasswordCredential`, `BlockUserAccount`, `UnblockUserAccount`).
- [x] **`AuthenticateLocalCredential` fica de fora do retry** (DF5/Q4 — best-effort), mas **não** fica sem tratamento:
      troca `[WithWorkContext]` por `IWorkContext work` injetado direto e usa a primitiva manual
      `RetryOnConcurrencyAsync<LocalAuthenticationResult>` com `MaxAttempts = 1` (uma execução, zero retries).
      Assim o conflito devolve `Problems.InvalidState` (mesmo `typeId` `user_account.concurrency_conflict`) e a
      primitiva ainda faz rollback/`CleanUp()` antes do retorno, sem deixar o WorkContext scoped contaminado — ver
      "Achados da revisão" abaixo.
- [x] Fluxos com token (`ResetPasswordWithToken`, `ChangeExpiredPasswordWithToken`, `ConfirmEmailVerification`,
      `ConfirmPhoneVerification`): retry **escopado** ao agregado, **após** o consumo do token. Como
      `[WithRetryOnConcurrency]` envolveria a execução inteira (incluindo o consumo), as quatro use cases trocaram
      `[WithWorkContext]` por injeção direta de `IWorkContext work` (mesmo padrão já usado em `RequestPasswordRecovery`)
      e chamam `work.RetryOnConcurrencyAsync(...)` manualmente só em torno de
      `{recarregar agregado → revalidar precondições dependentes do agregado → reaplicar mutação → SaveAsync}`, com
      `onExhausted` retornando o mesmo `Problem` das demais. As duas de verificação (email/telefone) não estavam na
      lista de tarefas original — ver "Achados da revisão".
- [x] Esgotamento mapeado para `Problems.InvalidState` (409, `typeId` `user_account.concurrency_conflict`) — fixado via
      `services.Configure<RetryOnConcurrencyOptions>(o => o.ExhaustedProblemTypeId = "user_account.concurrency_conflict")`
      em `UserAccountsWorkContextExtensions.ConfigureUserAccounts` (invariante do módulo, não configurável por
      appsettings; `MaxAttempts` continua vindo da seção `"RetryOnConcurrency"`, default 3). Descoberta: a lib evoluiu
      além do previsto na DF5.1 — mesmo sem `Operation` explícito no atributo, o gerador sempre injeta
      `IConcurrencyRetryProblemFactory` e deriva uma chave de operação (`{Namespace}.{Comando}`), então o typeId fixo
      no `IOptions` já cobre todos os comandos sem precisar de `AddConcurrencyRetryProblem` por comando.
- [x] Efeito colateral descoberto e corrigido: `AddUnitOfWorkAccessor` (chamado por `ConfigureUserAccounts`) sempre
      vincula `RetryOnConcurrencyOptions` a `IConfiguration` (`BindConfiguration`); cenários de teste com
      `ServiceCollection` nu (sem host) quebravam ao resolver **qualquer** handler com `[WithWorkContext]`, retry ou
      não. Corrigido com `TryAddSingleton<IConfiguration>(new ConfigurationBuilder().Build())` no próprio
      `ConfigureUserAccounts` (não-invasivo: hosts reais que já registram `IConfiguration` não são afetados).
- [x] Substituído o retry **manual** dos `ConcurrencyTests` por asserção do comportamento resiliente **do handler**.
      Técnica: rastreia uma instância *stale* do agregado no `DbContext` de um scope (via `UserAccountReader`), grava
      uma mutação concorrente por **outro** scope/`DbContext` (mesma conexão SQLite in-memory compartilhada) e resolve
      o handler a partir do scope *stale* — o mapa de identidade do EF devolve a instância desatualizada ao reload
      interno do handler, gerando um conflito **real** (não simulado) na primeira tentativa. **11/11 verdes**; suíte
      completa da solução **558/558 verde**.

**Achados da revisão pós-implementação (2026-07-20) — 4 corrigidos:**

Uma revisão da entrega inicial da Fase 1 achou 3 bugs e 1 inconsistência, todos confirmados e corrigidos:

1. **Alta — `SaveAsync` descartado nos fluxos manuais.** Em `ResetPasswordWithToken`/`ChangeExpiredPasswordWithToken`,
   o corpo do retry fazia `await work.SaveAsync(ct); return result;` — o `SaveResult` de uma falha de persistência
   **não-concorrência** (`DbUpdateException`, que `SaveAsync` converte em `SaveResult` falho **sem lançar**) era
   descartado, devolvendo sucesso com o token **já consumido**. Corrigido: `return await work.SaveAsync(ct);`.
2. **Alta — retry não revalidava precondições dependentes do agregado.** `PasswordHistoryPolicy.Validate` (e, em
   `ChangeExpiredPasswordWithToken`, `StillRequiresPasswordChange`) rodava **uma vez**, antes do consumo do token,
   contra o snapshot pré-corrida. O agregado (`SetPassword`/`ResetPassword`) não tem defesa própria contra reuso —
   numa corrida real em que o estado muda entre a checagem e a 2ª tentativa do retry, a política podia ser violada
   silenciosamente. Corrigido: as checagens que dependem do estado do agregado agora rodam **dentro** do corpo do
   retry, contra o `fresh` recarregado; o consumo do token continua fora do laço (Q3, inalterado).
3. **Alta — `ConfirmEmailVerification`/`ConfirmPhoneVerification` fora do escopo.** A nota original do plano
   ("Consumo dos use cases de mutação") já citava "e os fluxos de verificação" como precisando do mesmo tratamento de
   retry escopado — a entrega inicial só cobriu os dois fluxos de senha. Corrigido: mesmo padrão (`IWorkContext work`
   manual + `RetryOnConcurrencyAsync` escopado) aplicado às duas.
4. **Média — `AuthenticateLocalCredential` propagava `ConcurrencyException` sem tratamento.** A Q4 original ("best
   effort", sem retry) descreveu a consequência aceita como um "incremento perdido" (sobrescrita silenciosa) — um
   modelo que não corresponde à realidade uma vez que `Version` já é `IsConcurrencyToken()` (DF2): a segunda
   gravação **lança**, não sobrescreve silenciosamente. Sem captura em lugar nenhum, uma corrida real no login viraria
   erro não tratado na borda. Corrigido em duas camadas: no módulo, `AuthenticateLocalCredential` troca
   `[WithWorkContext]` por `IWorkContext work` e executa a primitiva manual
   `RetryOnConcurrencyAsync<LocalAuthenticationResult>` com `MaxAttempts = 1`; portanto não reexecuta a autenticação,
   mas converte o conflito em `Problems.InvalidState` (mesmo `typeId` `user_account.concurrency_conflict`) e faz
   rollback/`CleanUp()` antes do retorno — *fail-closed*: o resultado calculado sobre a conta stale é descartado,
   nunca devolvido como sucesso. Na borda, `LocalUserAuthenticator` **já** colapsava qualquer `Result` sem valor em
   `AuthenticationResult.Failed(InvalidCredentials)` — nenhuma mudança necessária ali; a `.Integration` continua sem
   conhecer `ConcurrencyException`/`WorkContext` (ADR-013).

Testes novos cobrindo os 4 achados: retry de `ChangeExpiredPasswordWithToken`/`ConfirmEmailVerification`/
`ConfirmPhoneVerification` (token consumido uma única vez); revalidação de histórico rejeitando um candidato que virou
reuso durante a corrida; auth devolvendo `Problem` controlado (não mais exceção) com o estado real preservado
(fail-closed). **11 testes em `ConcurrencyTests`** (era 7).

**Critérios de aceite:** primitiva + atributo entregues com testes nas libs; tentativas concorrentes de credencial
**não** geram exceção não tratada em nenhum use case (viram retry, `InvalidState` ao esgotar, ou `Problem` controlado
no caso da auth); fluxos com token não re-consomem o token sob retry e revalidam precondições contra o estado
recarregado; falha de persistência não-concorrência nunca vira falso-sucesso; auth permanece sem retry (best-effort)
mas fail-closed; nenhum evento despachado para tentativa não-commitada.

**Testes:** testes das libs (primitiva + gerador); `Tests.UserAccounts/ConcurrencyTests.cs` (11 testes, concorrência
via Sqlite in-memory compartilhado, conflitos reais via mapa de identidade do EF); regressão completa da solução
(558/558).

### Resultado da Fase 1

**Concluída.** Peças de biblioteca (2026-06-28) + aplicação no `royal-identity`.

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
- **Evolução posterior (consumida via `RoyalCode.SmartCommands`/`.WorkContext` `0.1.0`, já usada aqui):** `Operation` no
  atributo + `IConcurrencyRetryProblemFactory`/`AddConcurrencyRetryProblem*`/`ConcurrencyRetryOperations`. O gerador
  agora **sempre** injeta a factory e deriva uma chave de operação (`{Namespace}.{Comando}`) mesmo sem `Operation`
  explícito — foi assim que o typeId único do módulo pôde ser fixado só via `IOptions`, sem registro por comando.
- **Testes**: 8 (primitiva) + 4 (gerador: 2 snapshots `.g.cs`, 2 diagnostics). **Suíte `SmartCommands` 77/77 verde**;
  **`SmartCommands.sln` compila com 0 erros** (warning `CS8785` no `Tests.Models` é pré-existente — versão de analyzer no
  ambiente, sem relação com esta entrega).

Aplicado no `royal-identity`:

- `[WithRetryOnConcurrency]` em `ChangeOwnPassword`, `ChangeUserAccountPassword`, `UnlockPasswordCredential`,
  `BlockUserAccount`, `UnblockUserAccount`.
- Retry escopado manual (`IWorkContext.RetryOnConcurrencyAsync`) em `ResetPasswordWithToken`,
  `ChangeExpiredPasswordWithToken`, `ConfirmEmailVerification` e `ConfirmPhoneVerification`, após o consumo do
  token — as quatro trocaram `[WithWorkContext]` por `IWorkContext work` injetado direto (padrão já usado em
  `RequestPasswordRecovery`); as precondições dependentes do estado do agregado (histórico de senha,
  `StillRequiresPasswordChange`) são revalidadas **dentro** do corpo do retry, contra o estado recarregado.
- `AuthenticateLocalCredential` permanece fora do retry (Q4), mas troca `[WithWorkContext]` por `IWorkContext work` +
  `RetryOnConcurrencyAsync<LocalAuthenticationResult>` manual com `MaxAttempts = 1` — uma execução, zero retries;
  devolve `Problems.InvalidState` (mesmo `typeId`) em vez de deixar a exceção propagar, faz rollback/`CleanUp()` e
  opera em *fail-closed*, nunca devolvendo o resultado calculado sobre a conta stale.
- `UserAccountsWorkContextExtensions.ConfigureUserAccounts` ganhou o fix de `typeId` fixo (`ExhaustedProblemTypeId`) e
  o fallback `TryAddSingleton<IConfiguration>` (necessário porque `AddUnitOfWorkAccessor` sempre vincula
  `RetryOnConcurrencyOptions` a `IConfiguration`, o que quebrava testes com `ServiceCollection` nu).
- `Tests.UserAccounts/ConcurrencyTests.cs` reescrito: instância *stale* rastreada via `UserAccountReader` num scope +
  mutação concorrente por outro scope/`DbContext` (mesma conexão SQLite in-memory) + handler resolvido do scope
  *stale* → conflito real via mapa de identidade do EF. **11/11 verde** (inclui os 4 achados da revisão pós-entrega).
- **Suíte completa da solução: 558/558 verde** (`dotnet test RoyalIdentity.sln`).

---

## Fase 2 - Migrations dos providers (`.Sqlite`/`.PostgreSql`)

**Depende de:** Q5, Q6, Q7.

**O que/como:** dar schema versionado aos providers, saindo do `EnsureCreated`. Migration inicial por provider
refletindo os mapeamentos atuais (incluindo índices parciais de primário único e o token de concorrência por provider).

**Tarefas:**

- [x] Aplicar as decisões Q5–Q7 (migrations agora; testes rápidos com `EnsureCreated`; testes dedicados com `Migrate()`;
      Sqlite no CI e PostgreSql validado localmente por ora, via container Podman efêmero).
- [x] `IDesignTimeDbContextFactory` por provider (necessário: nenhum dos dois providers tinha acesso a `IConfiguration`
      fora de um host, e o tooling `dotnet ef` precisa construir o contexto sem subir a aplicação).
- [x] Migration inicial `.Sqlite` refletindo o modelo atual; validada (schema funcional, ver testes).
- [x] Migration inicial `.PostgreSql` (incluindo `xmin` como token e índices parciais com sintaxe PG) — com correção
      manual necessária (ver "Achado" abaixo).
- [x] Caminho de testes definido (Q6): suíte existente mantém `EnsureCreated` (rápida); testes novos dedicados aplicam
      `Migrate()` sobre SQLite in-memory compartilhado.

**Achado durante a implementação — migration do PostgreSQL tentava criar a coluna de sistema `xmin`:**

O mapeamento de `UserAccount.Version` para `xmin`/`xid` (`ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()`) já
existia (DF2), mas o scaffolder do EF não sabe que `xmin` é uma **coluna de sistema reservada do PostgreSQL**
(presente automaticamente em toda tabela) — `dotnet ef migrations add` gerou
`xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)` dentro do `CreateTable` de
`UserAccounts`, o que o PostgreSQL **rejeitaria** em tempo real (`column name "xmin" conflicts with a system
column name"`). Investiguei `UseXminAsConcurrencyToken()` (o helper histórico do Npgsql para este cenário) e
confirmei, inspecionando a DLL instalada, que **não existe mais no Npgsql EF Core 10.0.0** (presente em 8.0.11,
removido depois — o Npgsql atual unificou em torno do `IsRowVersion()` genérico do EF Core, sem suprimir sozinho
a geração de DDL para uma coluna de sistema). Não há API para "excluir só esta coluna" da migration (existe
`ExcludeFromMigrations()`, mas é por **tabela inteira**, usado para views). A correção aplicada — e documentada
como prática estabelecida para este cenário específico — foi editar manualmente a migration gerada, removendo a
linha da coluna `xmin` do `CreateTable` (o snapshot do modelo, usado para calcular o *diff* de migrations
futuras, **não** foi tocado — continua sabendo que `Version` mapeia para `xmin`). Deixei um comentário na
migration alertando que essa edição manual precisa ser refeita em qualquer migration futura que altere a tabela
`UserAccounts` (o `dotnet ef migrations add` volta a gerar a linha).

**Critérios de aceite:** decisões Q5–Q7 aplicadas; `AddUserAccountsSqlite` (file) cria/evolui schema via
`Database.MigrateAsync()`; migration PG gerada, corrigida e executada contra PostgreSQL 17 real em container Podman
efêmero (ver teste/script abaixo); validação automatizada em CI via .NET Aspire permanece registrada como futuro
(Q7); testes existentes seguem verdes.

**Testes:** `Tests.UserAccounts/UserAccountsSqliteMigrationTests.cs` (4 testes novos) aplicam `Migrate()` sobre uma
conexão SQLite in-memory compartilhada (não `EnsureCreated()`) e prova que o schema migrado é funcional: round-trip
de conta com credencial/email; os índices parciais únicos de e-mail e telefone primários
(`UX_UserAccountEmails_PrimaryPerAccount`/`UX_UserAccountPhones_PrimaryPerAccount`) rejeitam um segundo primário; e o
token de concorrência (`Version`) continua rejeitando atualização concorrente obsoleta. Suíte do módulo e da solução
verdes (ver "Resultado" abaixo).

**Teste PostgreSQL opt-in:** `Tests.UserAccounts/UserAccountsPostgreSqlMigrationTests.cs` recebe a connection string
por `ROYALIDENTITY_TEST_POSTGRES`; sem ela, fica explicitamente ignorado e a suíte normal permanece independente de
infraestrutura. `scripts/Test-UserAccountsPostgreSql.ps1` verifica/inicia a `podman machine`, sobe
`postgres:17-alpine` num container efêmero com porta host dinâmica (nunca 5432), aguarda `pg_isready`, injeta a
connection string apenas no processo e executa a categoria `PostgreSql`. O teste aplica `MigrateAsync()`, faz
round-trip de conta/credencial/e-mail, prova o índice parcial de e-mail primário e confirma o comportamento real do
`xmin` (valor gerado/atualizado e `DbUpdateConcurrencyException` para uma escrita stale). O container é removido em
`finally`; a machine não é parada, pois pode ser compartilhada. Validado localmente em PostgreSQL 17: **1/1 verde**.

**Geração de script SQL (Q5, para aplicação manual pelo DBA):** confirmado funcionando nos dois providers via
`dotnet ef migrations script` (comando padrão do EF, não precisa de código extra). Limitação encontrada: o
provider SQLite **não suporta** `--idempotent` ("Generating idempotent scripts for migrations is not currently
supported for SQLite") — script incremental funciona normalmente; PostgreSQL suporta `--idempotent` sem
problemas, e o script gerado confirma que a correção do `xmin` chegou corretamente até a saída SQL (sem nenhuma
menção a "xmin" no `CREATE TABLE`).

### Resultado da Fase 2

**Concluída.**

- **`RoyalIdentity.UserAccounts.Sqlite`**: `Microsoft.EntityFrameworkCore.Design` (10.0.10, `PrivateAssets="all"`) +
  `UserAccountsSqliteDesignTimeDbContextFactory` (conexão dummy `DataSource=design-time.db`, dispatcher no-op) +
  `Migrations/InitialCreate` refletindo o modelo completo (12 tabelas, todos os índices incluindo os dois parciais
  únicos de primário email/phone). Validada por `Migrate()` real em `UserAccountsSqliteMigrationTests.cs`.
- **`RoyalIdentity.UserAccounts.PostgreSql`**: mesma estrutura (`UserAccountsPostgreSqlDesignTimeDbContextFactory`,
  conexão dummy Npgsql) + `Migrations/InitialCreate` com a correção manual do `xmin` (ver achado acima). Executada
  contra PostgreSQL 17 real via `scripts/Test-UserAccountsPostgreSql.ps1`: migration, índice parcial e concorrência
  por `xmin` verdes. A automação em CI via .NET Aspire continua diferida para fase futura.
- Nenhum builder-extension novo tipo `MigrateDatabase()` foi criado: `EnsureDatabaseCreated()`/`SeedDatabase()` (já
  existentes) são mecanismos **exclusivos do hook de conexão in-memory do SQLite** — não fazem nada em
  `AddSqliteWorkContext`/`AddPostgreWorkContext` (confirmado lendo o código-fonte do pacote). Para os providers
  reais, aplicar a migration é responsabilidade explícita do host — prática padrão do EF Core (`await
  serviceProvider.GetRequiredService<TDbContext>().Database.MigrateAsync();` no startup), sem abstração adicional
  necessária.
- **Suíte completa sem infraestrutura: 562 aprovados + 1 PostgreSQL ignorado** (`dotnet test RoyalIdentity.sln`;
  `Tests.UserAccounts`: 193 aprovados + 1 ignorado). **Suíte PostgreSQL opt-in: 1/1 verde** via script Podman.
- Revisão pós-fase: a pilha Microsoft EF Core foi alinhada em `10.0.10` (`Relational` no módulo e `Design` nos dois
  providers, a mesma versão do `dotnet-ef`). Isso removeu a resolução transitiva vulnerável de
  `System.Security.Cryptography.Xml` 9.0.0; o audit NuGet dos dois providers ficou sem pacotes vulneráveis.

---

## Fase 3 - Seed reutilizável e módulo como backing de testes

**Depende de:** Q8, Q9; e da Fase 2 (se Q6 decidir migrations nos testes).

**O que/como:** extrair o seed Alice/Bob hoje duplicado para um **artefato único reutilizável** e habilitar o módulo
como backing de testes, executando o primeiro passo da ADR-018.

**Tarefas:**

- [x] Aplicar as decisões Q8 (seed test-only compartilhado) e Q9 (dual mantido; regressão opt-in ampliada).
- [x] Criar o seed reutilizável; fazer `UserAccountsAppFactory` e o contract test `UserAccountsSqlite` consumirem o
      mesmo artefato (eliminando a duplicação).
- [x] Conforme Q9, ampliar a regressão OIDC contra o módulo (além dos 5 testes representativos) e/ou preparar o flip do
      default.
- [x] Atualizar [plan-users-accounts-test-matrix.md](plan-users-accounts-test-matrix.md) e a nota da ADR-018/backlog
      com o estado real.

**Critérios de aceite:** Q8–Q9 decididas; seed único consumido pelos dois caminhos; regressão opt-in ampliada conforme
decisão; matriz/backlog atualizados; suíte verde.

**Testes:** contract tests fake×módulo; regressão OIDC opt-in (ampliada conforme Q9); suíte completa.

### Resultado da Fase 3

**Concluída.**

- **Seed único (Q8):** `Tests.UserAccounts/UserAccountsModuleSeed.cs` (test-only, sem API pública no módulo) —
  `SeedDefaultScopesAsync`/`SeedScopeAsync` (property scopes `profile`/`email`, idempotente) e
  `SeedDefaultAccountsAsync`/`SeedAccountAsync` (Alice/Bob determinísticos via `ICreateUserAccountHandler`,
  reaproveitando os `sub` do fake — `MemoryStorage.AliceSubjectId`/`BobSubjectId` — para que qualquer teste escrito
  contra o contrato compartilhado observe a mesma identidade em fake ou módulo). Como o arquivo vive fisicamente em
  `Tests.UserAccounts`, ele é **linked** (`<Compile Include="..\Tests.UserAccounts\UserAccountsModuleSeed.cs"
  Link="Prepare\UserAccountsModuleSeed.cs" />`) em `Tests.Integration.csproj` — evita tanto duplicar o arquivo quanto
  criar uma `ProjectReference` de teste-para-teste (a alternativa "pequeno projeto de suporte de testes" cogitada na
  Q8 foi descartada por peso desnecessário).
- **Consumidores migrados:** `UserAccountsAppFactory`/`UserAccountsSeedHostedService.StartAsync` (antes com
  `SeedAccountAsync`/`SeedScopeAsync` privados duplicados) e `UserDirectoryContractTests.UserAccountsSqlite`
  (`CreateHarnessAsync`/`SeedAsync`, antes com seed inline próprio) agora delegam inteiramente ao seed compartilhado.
  Em particular, `CreateHarnessAsync` chama `SeedDefaultAccountsAsync` diretamente; `SeedAccountAsync` permanece no
  contract test apenas para fixtures específicas de cada cenário. Os defaults locais `Alice()`/`Bob()` e os métodos
  privados duplicados foram removidos.
- **Idempotência coberta:** `UserAccountsModuleSeedTests` executa os seeds de scopes e contas duas vezes, em scopes de
  DI distintos, e prova que persiste exatamente `profile`/`email`, Alice/Bob, uma credencial, um e-mail primário
  verificado e a role `admin` por conta, preservando o estado ativo.
- **Regressão opt-in ampliada (Q9):** `UserAccountsOptInRegressionTests` foi de 5 para **6** testes HTTP — novo
  `Login_WhenInvalidPassword_IsRejected_WithGenericMessage_AndNoSession` prova que uma senha incorreta contra o
  módulo é rejeitada com a mesma mensagem genérica anti-enumeration do fake e não cria sessão (verificado via
  redirect em `demo/test/protected-resource`). Usa **Bob**, não Alice, para não poluir o contador de falhas
  compartilhado pelo `IClassFixture<UserAccountsAppFactory>` entre os testes da classe. Por decisão Q9, o **flip**
  completo do default para o módulo continua diferido para o `plan-data-persistence` — a regressão segue
  **representativa**, não a suíte inteira.
- **Documentação atualizada:** [plan-users-accounts-test-matrix.md](plan-users-accounts-test-matrix.md) (Fase 10 —
  contagem de testes da regressão opt-in e nota do seed único) e [backlog-001.md](../backlogs/backlog-001.md) (item
  "Substituir o storage fake in-memory..." — primeiro passo do habilitador marcado concluído; nota sobre a ampliação
  Q9 e o flip ainda pendente).
- **Suíte completa da solução: 564 aprovados + 1 PostgreSQL ignorado** (`dotnet test RoyalIdentity.sln`;
  `Tests.Security` 116, `Tests.Pipelines` 3, `Tests.Identity` 13, `Tests.Architecture` 15, `Tests.UserAccounts` 194 + 1
  ignorado, `Tests.Integration` 223).

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
