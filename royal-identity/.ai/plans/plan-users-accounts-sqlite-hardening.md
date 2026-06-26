# Plan: Endurecimento do backing `UserAccounts` — concorrência resiliente, migrations e seed (`plan-users-accounts-sqlite-hardening`)

## Status: PLANEJADO - aguardando decisão das Questões em aberto

## Progresso

`---` **0%** - 0 de 3 fases

| Fase | Estado |
|---|---|
| Fase 1 - Concorrência resiliente (retry no handler) | Pendente |
| Fase 2 - Migrations dos providers (`.Sqlite`/`.PostgreSql`) | Pendente |
| Fase 3 - Seed reutilizável e módulo como backing de testes | Pendente |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `#` por fase concluida, `%` e `X de 3`). Ex.: 1 fase => `#--` **33%** - 1 de 3.
> Antes de fechar uma fase, resolva (ou rebaixe) as **Questões em aberto** atadas a ela.

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

## Questões em aberto

> Resolver antes de fechar a fase indicada. Não há recomendação embutida — são decisões do autor.

**Fase 1 (retry):**

- **Q1 — Onde mora o retry e como compõe com `[WithWorkContext]`?** Opções levantadas: (a) abandonar
  `[WithWorkContext]` nos use cases de credencial e gerir laço próprio (`IWorkContext` + recarregar + reaplicar +
  `ChangeTracker.Clear()` + salvar); (b) um helper reutilizável de retry chamado dentro do `Execute`; (c) um decorator
  no pipeline do SmartCommand/WorkContext, se existir seam para isso. Precisa verificar se o WorkContext expõe gancho de
  retry/execução antes de decidir.
- **Q2 — Política de retry:** número máximo de tentativas, com ou sem backoff, e o que retornar ao esgotar (qual
  `Problems.*`? propagar exceção?).
- **Q3 — Fluxos com token:** confirmar o **escopo** do retry (só a mutação do agregado, nunca o consumo do token) e
  revisar caso a caso `ResetPasswordWithToken`, `ChangeExpiredPasswordWithToken` e os fluxos de verificação.
- **Q4 — Semântica do contador de autenticação:** o contador de falhas exige retry estrito, ou tolera-se "perda
  eventual de incremento" sob contenção extrema (o pré-plano §10, cenário 2, listou "aceitar eventualidade pequena")?
  Afeta se `AuthenticateLocalCredential` entra ou não no laço de retry.

**Fase 2 (migrations):**

- **Q5 — Agora ou junto do `plan-data-persistence`?** Gerar as migrations do módulo já neste plano, ou coordenar com a
  migração do storage do core para uma convenção única de migrations/`__EFMigrationsHistory`?
- **Q6 — Testes continuam com `EnsureCreated`?** Manter `EnsureDatabaseCreated()` para os testes in-memory e usar
  migrations só para file/PostgreSql, ou aplicar migrations também nos testes (mais fiel, mais lento)?
- **Q7 — Convenção e CI:** localização/nomenclatura das migrations por provider, `IDesignTimeDbContextFactory`, e se há
  runner PostgreSql para validar a migration PG em CI (ou se fica validada só localmente).

**Fase 3 (seed/backing):**

- **Q8 — Forma e lar do seed:** paridade mínima Alice/Bob (igual ao seed atual) ou fixture mais rica? O seed é
  test-only (helper em `Tests.*`) ou uma **API pública de seed do módulo** reutilizável também por deploy/demo real?
- **Q9 — Virar o default de testes:** trocar o default de `Tests.Integration` para o módulo (flip), ou manter dual
  (fake default + módulo opt-in) até que o storage do core também migre? Rodar a suíte inteira contra o módulo é o alvo,
  mas o custo/sequência depende desta decisão.

---

## Ordem de execução

1. **Fase 1 (retry)** — maior valor e maior risco de design; destrava o cumprimento da ADR-017 §2.9.
2. **Fase 2 (migrations)** — independe da Fase 1; pode correr em paralelo, mas listada depois por prioridade.
3. **Fase 3 (seed/backing)** — depende de a Fase 2 ter o schema versionado (se Q6 decidir migrations nos testes) e
   consolida a direção da ADR-018.

---

## Fase 1 - Concorrência resiliente (retry no handler)

**Depende de:** Q1, Q2, Q3, Q4.

**O que/como:** implementar o **retry** que a ADR-017 §2.9 decide, transformando "conflito detectado" em "comportamento
resiliente": ao receber `DbUpdateConcurrencyException`, recarregar o agregado na versão atual, reaplicar a operação de
domínio e salvar de novo, dentro de uma política/limite; sem re-executar consumo de token.

**Tarefas:**

- [ ] Resolver Q1–Q4. Verificar se o RoyalCode.WorkContext expõe gancho de transação/retry antes de escolher o
      mecanismo (Q1).
- [ ] Implementar o mecanismo de retry escolhido (sem util estático; respeitando a arquitetura do módulo).
- [ ] Aplicar o retry aos use cases de mutação de agregado, escopando-o à mutação (nunca ao consumo de token — Q3).
- [ ] Substituir o retry **manual** dos `ConcurrencyTests` por asserção do comportamento resiliente real (os 7 cenários
      passam a provar o retry do handler, não um retry escrito no teste).
- [ ] Cobrir o esgotamento de tentativas (Q2) e o caso de fluxo com token sob conflito (Q3).

**Critérios de aceite:** Q1–Q4 decididas e registradas; duas tentativas concorrentes de credencial não geram exceção
não tratada; os 7 cenários passam exercitando o retry **do handler**; fluxos com token não re-consomem o token sob
retry; nenhum evento despachado para tentativa não-commitada.

**Testes:** `Tests.UserAccounts` (concorrência via Sqlite in-memory compartilhado); regressão completa da solução.

### Resultado da Fase 1

*a preencher*

---

## Fase 2 - Migrations dos providers (`.Sqlite`/`.PostgreSql`)

**Depende de:** Q5, Q6, Q7.

**O que/como:** dar schema versionado aos providers, saindo do `EnsureCreated`. Migration inicial por provider
refletindo os mapeamentos atuais (incluindo índices parciais de primário único e o token de concorrência por provider).

**Tarefas:**

- [ ] Resolver Q5–Q7 (escopo agora vs `plan-data-persistence`; testes com/sem migration; convenção + CI).
- [ ] `IDesignTimeDbContextFactory` por provider (se necessário para o tooling de migrations).
- [ ] Migration inicial `.Sqlite` refletindo o modelo atual; validar criação de schema file-based.
- [ ] Migration inicial `.PostgreSql` (incluindo `xmin` como token e índices parciais com sintaxe PG).
- [ ] Definir o caminho de testes (manter `EnsureCreated` in-memory vs aplicar migrations — Q6).

**Critérios de aceite:** Q5–Q7 decididas; `AddUserAccountsSqlite` (file) cria/evolui schema via migration; migration PG
gerada e (conforme Q7) validada; testes existentes seguem verdes.

**Testes:** round-trip de persistência sobre o schema migrado; suíte do módulo verde.

### Resultado da Fase 2

*a preencher*

---

## Fase 3 - Seed reutilizável e módulo como backing de testes

**Depende de:** Q8, Q9; e da Fase 2 (se Q6 decidir migrations nos testes).

**O que/como:** extrair o seed Alice/Bob hoje duplicado para um **artefato único reutilizável** e habilitar o módulo
como backing de testes, executando o primeiro passo da ADR-018.

**Tarefas:**

- [ ] Resolver Q8 (forma/lar do seed) e Q9 (flip do default vs dual).
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

- As três fases concluídas com suas Questões em aberto resolvidas e registradas.
- ADR-017 §2.9 cumprida no fluxo real (retry), não só na detecção.
- Providers com schema versionado; seed único; regressão contra o módulo no nível decidido na Q9.
- `dotnet test RoyalIdentity.sln` verde.

## Riscos

- **Composição do retry com `[WithWorkContext]`** (Q1): se o WorkContext não oferecer gancho, abandonar o auto-save nos
  use cases afetados muda a convenção — avaliar o impacto antes de propagar.
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
