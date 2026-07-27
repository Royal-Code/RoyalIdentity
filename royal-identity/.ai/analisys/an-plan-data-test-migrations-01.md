# Análise — Revisão rigorosa do plano `plan-data-test-migration` (Plano 4)

> **Status:** revisão **pré-implementação** do rascunho. Não é ADR, não fecha decisão e não altera o plano.
>
> **Objetivo:** verificar, contra o código real, se o inventário do plano é fiel; se as fases são executáveis na
> ordem proposta; se os critérios de aceite são falsificáveis; e se falta decisão, risco ou tarefa que só
> apareceria no meio da execução — quando corrigir custa caro.
>
> **Escopo revisado:** [.ai/plans/plan-data-test-migration.md](../plans/plan-data-test-migration.md) (1004 linhas,
> 9 fases, DF1-DF16, Q1-Q10), confrontado com `RoyalIdentity.Server`, `RoyalIdentity.Migrations`,
> `RoyalIdentity.Storage.EntityFramework*`, `RoyalIdentity.Storage.InMemory`, `RoyalIdentity.UserAccounts.*`,
> `Tests.Integration`, `Tests.Storage`, `Tests.UserAccounts`, `Tests.Host` e `Tests.Architecture`.
>
> **Método:** leitura direta dos artefatos citados; reprodução de **cada contagem** declarada em `Estado atual do
> código`; execução dirigida de suítes para medir custo de fixture. Data: 2026-07-26. Worktree limpo, nenhuma
> alteração aplicada.

---

## 1. Veredito

O plano é **sólido e incomum na qualidade do inventário**: das oito afirmações quantitativas de `Estado atual do
código`, sete reproduzem exatamente com comando direto sobre o repositório. A sequência de fases é correta no ponto
que mais importa — o fake só é removido depois que o default real está verde (DF7), e a quebra pública de
atomicidade acontece num único corte compilável (Fase 8) — e as bordas arquiteturais (DF3, DF9, DF15) estão
alinhadas com ADR-013/015 e com o que os guards de `Tests.Architecture` já exigem.

**Não recomendo iniciar a Fase 1 sem tratar quatro pontos.** Três são omissões de decisão que a execução vai
encontrar de qualquer forma, mas tarde: o **custo e a topologia da fixture** (a suíte HTTP hoje roda em 8 s; a
composição integral custa por fixture cerca de 100× o que custa a fake), o **mecanismo de coexistência** entre o
`AppFactory` fake e a factory integral durante as Fases 5-6 — que decide se ~20 arquivos são editados uma ou duas
vezes —, e a **divergência comportamental módulo × fake** ao migrar 269 testes que nunca rodaram sobre
`UserAccounts`. O quarto é uma redação de invariante que inverte o sentido de uma regra de segurança.

Além disso há um achado factual isolado: **uma das oito contagens do inventário não é reproduzível** por nenhum
padrão evidente.

Nenhum desses pontos invalida o desenho. São correções de rascunho, não de arquitetura.

---

## 2. Conformidade com o template

Confrontado com [template-ai-implementation-plan.md](../references/template-plan/template-ai-implementation-plan.md):

| Exigência do template | Situação |
|---|---|
| Todas as seções do shape presentes e na ordem | **Conforme** |
| Barra de progresso com um caractere por fase (`░`/`█`) | **Conforme** — 9 caracteres para 9 fases |
| Cada fase com `Depende de`, `Escopo`, `O que/como`, `Tarefas`, `Critérios de aceite`, `Testes`, `Resultado` | **Conforme** nas 9 fases |
| Tarefas iniciando com verbo de ação e marcadas `- [ ]` | **Conforme** |
| Toda decisão citada por uma fase existe em `Decisões fechadas` | **Conforme** — DF1-DF16 cobrem todas as citações |
| Incertezas em `Perguntas ao humano`, não em `Decisões fechadas` | **Conforme** |
| `Histórico de decisões` separado, com `SUPERSEDED` | **Conforme** — dois itens pré-plano |
| Comandos de teste executáveis no repositório | **Conforme** com uma ressalva (§6.4) |
| Riscos com gatilho, impacto, mitigação e estado | **Conforme** — 14 linhas, todas `Aberto`, apropriado para RASCUNHO |
| Não marcar fase concluída com decisão aberta | **Conforme** — o "Gate de planejamento" é explícito e vai além do template |

O **Gate de planejamento** ("nenhuma fase que dependa de Q1-Q10 pode ser iniciada enquanto a pergunta estiver
aberta") é um acréscimo ao template que funciona bem aqui: o plano tem 10 perguntas abertas e sem esse gate a
tentação de "começar pela Fase 4" seria real.

---

## 3. Verificação do inventário

Cada afirmação de `Estado atual do código (verificado em 2026-07-26)` foi reproduzida.

| # | Afirmação do plano | Resultado | Evidência |
|---|---|---|---|
| 1 | Host oficial ainda in-memory; Server referencia só Razor, core e InMemory | **Confere** | [HostServices.cs:15](../../RoyalIdentity.Server/HostServices.cs#L15) chama `AddInMemoryStorage()`; o `.csproj` tem exatamente 3 `ProjectReference` |
| 2 | `Program` não passa configuração/ambiente a `AddHostServices` | **Confere** | [Program.cs:15](../../RoyalIdentity.Server/Program.cs#L15) — `builder.Services.AddHostServices();`, sem argumento |
| 3 | Gateway EF completo compõe as duas famílias e recusa cleanup implícito | **Confere** | `AddEntityFrameworkStorage()` + validação de `CleanupExecutionMode` |
| 4 | Provisionamento do core é externo; o Server não referencia o runner | **Confere** | guard `MigrationsRunner_ProjectGraph_References_Providers_Only` e ausência da referência no `.csproj` do Server |
| 5 | `UserAccounts` pronto para runtime; providers com migrations, sem comando integrado ao runner | **Confere** | `AddUserAccountsSqlite`, `AddUserAccountsSqliteInMemory`, `AddUserAccountsPostgreSql`, `AddUserAccountsForRoyalIdentity`; nenhuma referência a `UserAccounts` em `RoyalIdentity.Migrations` |
| 6 | Factories opt-in são complementares, não cumulativas | **Confere** | `EntityFrameworkStorageAppFactory` mantém contas fake; `UserAccountsAppFactory` mantém core fake |
| 7 | 29 classes usam `IClassFixture<AppFactory>` | **Confere exatamente** | `grep -rl "IClassFixture<AppFactory>" Tests.Integration \| wc -l` → **29** |
| 8 | 381 ocorrências de `MemoryStorage` em 36 arquivos | **Confere exatamente** | `grep -ro "MemoryStorage" Tests.Integration` → **381**; `grep -rl` → **36** |
| 9 | 265 usos de `MemoryStorage.DemoRealm` | **Confere exatamente** | `grep -ro "MemoryStorage\.DemoRealm" Tests.Integration` → **265** |
| 10 | 28 usos do subject estático de Alice | **Confere exatamente** | `grep -ro "MemoryStorage\.AliceSubjectId" Tests.Integration` → **28** |
| 11 | **64 ocorrências de getters de `RealmMemoryStore`** | **Não reproduzido** | ver §5.1 |
| 12 | `UserAccountsModuleSeed` importa subjects do fake | **Confere** | `Tests.UserAccounts/UserAccountsModuleSeed.cs` lê `MemoryStorage.AliceSubjectId`/`BobSubjectId` |
| 13 | `Tests.Storage`: 11 especializações `InMemory`, `IStorageSession` sem twin EF, composição parcial Config EF + Operational fake | **Confere** | 11 classes `sealed class InMemory : *ContractTests` + `InMemoryStorageHarness`; `StorageSessionContractTests` só tem `InMemory`; `CompositeStorageSessionTests` combina `ConfigurationSqliteDbContext` com `AddInMemoryStorage()` |
| 14 | `UserDirectoryContractTests` tem especialização `InMemory`, e a variante SQLite usa realms estáticos do fake | **Confere** | linhas 271 e 304 do arquivo; `using RoyalIdentity.Storage.InMemory` na linha 7 |
| 15 | Atomicidade ainda é capability opcional com cast em runtime | **Confere** | `DefaultAuthorizationCodeConsumer.cs:23` e `DefaultRefreshTokenConsumer.cs:24,48` |

**Complemento verificado que o inventário não registra** (não são erros, são fatos úteis à execução):

- Apenas **quatro** `.csproj` referenciam o fake: `RoyalIdentity.Server`, `Tests.Host`, `Tests.Storage`,
  `Tests.UserAccounts`. `Tests.Integration` o alcança **transitivamente** por `Tests.Host`. `Tests.Identity`,
  `Tests.Endpoints`, `Tests.WebApp`, `Tests.Pipelines` e `Tests.Security` **não têm nenhum uso** — a lista de
  `Superfícies impactadas` está completa.
- `Tests.Integration` **já referencia `RoyalIdentity.Migrations`** (herdado da Fase 8 do Plano 3). DF15 restringe
  só o Server, então não há conflito, mas o inventário deveria registrar isso para a Fase 4 não o redescobrir.
- `AddInMemoryStorage()` registra exatamente cinco coisas: `MemoryStorage`, `IStorage`, `IStorageProvider`,
  `IConfigurationSnapshotSource` + `ConfigurationSnapshotRefreshOptions`, e `IUserDirectory`. **Não** registra
  `IReplayCache`, `IMessageStore` nem os stores de mensagem — todos são core-owned. A remoção do fake não perde
  nenhuma implementação sem substituto EF/módulo.
- `AddUserAccountsForRoyalIdentity()` usa `services.Replace(...)` para `IUserDirectory`, não `TryAdd`. Funciona
  **com ou sem** o fake registrado antes (o `Replace` do `Microsoft.Extensions.DependencyInjection` adiciona
  quando não há descriptor prévio). A Fase 3 pode chamá-lo num Server sem fake sem nenhuma adaptação — o
  comentário XML do método, que diz "call after the IdP storage", está desatualizado quanto à obrigatoriedade.
- A bridge volátil aplica `StandardIdentityScopes` (openid/profile/email/address/phone) a **todo realm vivo**, não
  só ao demo. A preocupação natural — "realms internos ficam sem identity scopes quando o fake sair" — **não se
  materializa**. Só `ResourceServer` é por realm e explícito.

---

## 4. Achados bloqueantes

### 4.1 — O custo de execução da suíte não é decisão, critério nem risco

**Ponto mais importante desta revisão.** O plano transforma 29 fixtures HTTP fake em 29 composições integrais de
três famílias (migrations Configuration + Operational + `UserAccounts`, mais seed) e **não menciona custo de
execução em lugar nenhum**: não há pergunta sobre topologia de fixture, não há critério de aceite com orçamento de
tempo, e a tabela de riscos não tem linha para isso.

Medições feitas agora, com `--no-build`, na máquina de desenvolvimento:

| Suíte | Testes | Duração reportada |
|---|---|---|
| `Tests.Integration` completo (default fake, hoje) | 269 | **8 s** |
| `IssuerUriTests` (fixture fake, 4 testes) | 4 | **1 s** |
| `EntityFrameworkStorageOidcFlowTests` (fixture EF, 2 famílias) | 3 | **4 s** |
| `UserAccountsOptInRegressionTests` (fixture core fake + `UserAccounts` SQLite) | 6 | **3 s** |

O custo marginal de uma fixture EF de duas famílias é da ordem de **3 s**; o de `UserAccounts` SQLite, da ordem de
**2-3 s**. A fixture integral soma as três famílias. Multiplicado por 29 fixtures, o setup sozinho fica na casa de
**dois minutos de trabalho**, contra 8 s da suíte inteira hoje — mesmo com o paralelismo do xUnit absorvendo parte,
a ordem de grandeza da regressão é essa, e ela recai sobre o ciclo de desenvolvimento inteiro, não só sobre a CI.

Isso não é motivo para não migrar. É motivo para **decidir a topologia antes de escrever a fixture**, porque as
alternativas produzem código diferente e são caras de trocar depois:

- **A)** um banco SQLite em arquivo por fixture, migrado do zero — é o que `EntityFrameworkStorageAppFactory` faz
  hoje; mais simples, mais caro;
- **B)** migrar **uma vez** um arquivo-template e **copiá-lo** por fixture — mantém o isolamento e paga migration
  uma vez só;
- **C)** SQLite in-memory com `ICollectionFixture` compartilhada entre classes que não mutam configuração,
  reservando fixture própria para as que mutam — o mais rápido e o que exige triagem por classe.

A opção **C** entra em conflito direto com o isolamento: `ClientTokenTests`, `RealmIsolationTests` e
`RefreshTokenClaimsModeTests` escrevem clients e tokens. Essa tensão — velocidade × isolamento — é exatamente o
tipo de decisão que o plano manda perguntar ("Questione o humano antes de fechar decisão que altere ... custo
operacional").

**Recomendação:** criar uma pergunta (proposta como **Q11** em §7), acrescentar à Fase 4 um critério de aceite com
orçamento explícito (ex.: "`dotnet test Tests.Integration` conclui em ≤ N s na máquina de referência; o valor
medido é registrado no `Resultado da Fase`") e uma linha na tabela de riscos.

### 4.2 — A coexistência entre `AppFactory` fake e factory integral nas Fases 5-6 não está definida

O critério de aceite da Fase 5 exige que os grupos migrados executem "**somente** sobre a factory integral", mas o
`AppFactory` só passa a ser a composição integral na Fase 6 ("Tornar a composição integral a implementação de
`AppFactory`"). Só existem duas leituras, e elas dão trabalhos muito diferentes:

1. **Troca do tipo de fixture por arquivo na Fase 5** (`IClassFixture<AppFactory>` → `IClassFixture<PersistentAppFactory>`),
   e na Fase 6 a volta ao nome `AppFactory` — **cada arquivo migrado é editado duas vezes**, com ~20 arquivos e
   uma janela em que dois defaults convivem.
2. **`AppFactory` ganha um seletor interno** (subclasse/flag) e os arquivos nunca mudam — uma edição só, mas exige
   decidir na Fase 4 como o seletor é expresso sem virar configuração global mutável (o que colide com o
   invariante 19, "fixtures não compartilham ... handle estático").

O plano não escolhe. Como isso determina o volume de churn das Fases 5-6 e a forma da Fase 4, precisa estar
decidido **antes** da Fase 4, não durante a Fase 5.

**Recomendação:** fixar o mecanismo como tarefa explícita da Fase 4 e ajustar o critério da Fase 5 para nomear o
tipo de fixture que os grupos migrados usam nesse intervalo.

**Ponto adjacente, do mesmo tema:** hoje a factory EF funciona por *registrar-e-depois-remover* — `Tests.Host`
chama `AddInMemoryStorage()` e `EntityFrameworkStorageAppFactory.RemoveInMemoryStorage` desfaz quatro
registros. Esse padrão é frágil por construção: **qualquer registro do fake que a lista de remoção esqueça vira
fallback silencioso**, e o teste passa pelo motivo errado. A prova de que o risco é real está no código atual — a
lista remove `IStorage`, `IStorageProvider`, `IConfigurationSnapshotSource` e `ConfigurationSnapshotRefreshOptions`,
mas deliberadamente **mantém** `MemoryStorage` e `IUserDirectory`, o que é correto hoje (a borda de contas
continua fake) e passa a ser um bug no momento em que a fixture integral entrar. A Fase 4 deveria tornar
`Tests.Host` **agnóstico de storage** (não registrar backing nenhum; recebê-lo da factory) em vez de ampliar a
lista de remoção.

### 4.3 — Divergência comportamental módulo × fake não está na tabela de riscos

As Fases 5-6 apontam **269 testes** para `UserAccounts` pela primeira vez. Hoje, a cobertura do módulo sob HTTP é
`UserAccountsOptInRegressionTests` — **6 testes**, que o próprio
[backlog-001.md](../backlogs/backlog-001.md) descreve como "**representativa**, não a suíte inteira". E ADR-018
proíbe investir em paridade do fake, o que significa que divergências não são acidentes: são o estado esperado e
aceito. Login com mensagem genérica anti-enumeration, lockout, required action, `SecurityStamp`/`SessionsValidAfter`,
projeção de claims por escopo — todos têm comportamento **module-only** registrado no backlog.

A tabela de riscos tem "Testes concretos do fake somem sem equivalente" (perda de cobertura na Fase 7), que é
outra coisa. Falta o risco de execução das Fases 5-6: **asserções escritas contra o fake falharem em massa**, e a
equipe não ter regra para separar "artefato do fake, ajustar a asserção" de "regressão real do módulo, corrigir o
produto". Sem essa regra, a pressão de fazer a suíte ficar verde empurra para o primeiro diagnóstico sempre.

**Recomendação:** linha de risco + uma regra de triagem no `O que/como` da Fase 5 (ex.: toda asserção alterada
durante a migração é registrada no `Resultado da Fase` com a classificação `artefato-do-fake` ou
`comportamento-do-módulo`, e a segunda categoria exige confronto com a matriz ou com ADR-017 antes de mudar o
teste).

### 4.4 — Invariante 12 está redigido ao contrário

> 12. Signing keys persistidas permanecem **desprotegíveis** pelo host depois do provisionamento.

Lido literalmente, o invariante afirma que o host **não consegue** desproteger as chaves — o oposto do que DF12 e o
Plano 2 fixaram (o provisionamento cria material utilizável; o host valida e usa, mas não cria nem rotaciona).
Num invariante de segurança, ambiguidade de sentido é defeito: é exatamente a linha que alguém vai citar para
justificar uma decisão futura.

**Recomendação:** reescrever para algo como "o host consegue desproteger as signing keys persistidas com o
protector configurado, e nunca cria nem rotaciona material" (§10, item 4 do checklist).

---

## 5. Achados relevantes

### 5.1 — A contagem "64 ocorrências de getters de `RealmMemoryStore`" não é reproduzível

Sete das oito contagens do inventário batem no número exato. Esta não bate com nenhum padrão evidente:

| Padrão | Escopo | Resultado |
|---|---|---|
| identificador `RealmMemoryStore` | `Tests.Integration` | 16 |
| chamadas `GetRealmMemoryStore` | `Tests.Integration` | 16 |
| chamadas `GetRealmMemoryStore` | repositório (sem `obj/`) | 30 |
| acessos aos dicionários do store (`.Clients`, `.AccessTokens`, `.UserSessions`, …) | `Tests.Integration` | 103 |

Nenhum dá 64. Como o número serve para dimensionar o esforço das Fases 5-6, e o restante do inventário é
rigoroso a ponto de acertar 381/36/265/28/29 na mosca, vale corrigi-lo ou registrar o comando exato. **Sugestão
geral:** anotar o comando `rg`/`grep` ao lado de cada número, como a Fase 8 já faz para as buscas de verificação
final. Isso torna o inventário re-executável quando o plano for retomado.

### 5.2 — Q4=A quebra um guard arquitetural existente, e a Fase 2 não prevê ajustá-lo

`Tests.Architecture/ConfigurationStorageBoundaryTests.MigrationsRunner_ProjectGraph_References_Providers_Only`
afirma que `RoyalIdentity.Migrations` tem **exatamente 2** referências, e que **todas** contêm
`RoyalIdentity.Storage.EntityFramework.`. Estender o runner com a família `UserAccounts` (Q4 opção A) falha as duas
asserções.

A Fase 2 roda `dotnet test Tests.Architecture`, então a falha aparece — mas como surpresa, não como tarefa. Pior:
sem enunciar a regra nova, o ajuste tende a virar "aumentei o número para 4", que é o guard perdendo o sentido.

**Recomendação:** tarefa explícita na Fase 2 para reexpressar o guard como *allowlist* (providers EF do core +
providers `UserAccounts`, e a proibição que realmente importa: o runner não referencia `RoyalIdentity/RoyalIdentity.csproj`
diretamente nem qualquer projeto `Tests.*`). Vale também registrar por que isso **não** viola ADR-013: o runner é
composition root, referencia os dois lados sem traduzir tipos entre eles — papel diferente do de `.Integration`.

### 5.3 — "Handles imutáveis" precisa proibir explicitamente um handle do tipo `Realm`

Hoje 265 chamadas passam `MemoryStorage.DemoRealm` — uma **instância estática de `Realm`** — para os acessores de
store. Sob EF isso é uma armadilha silenciosa: os stores chaveiam por `realm.Id`, então um `Realm` estático
**funciona** para lookup, mas carrega um `RealmOptions` que **não é** o materializado do banco. O teste passaria
exercitando opções que a composição real não tem — precisamente a classe de defeito que a Fase 8 do Plano 3
descobriu com o issuer.

O `Design alvo` diz que "handles de teste carregam `realmId` explicitamente", o que aponta na direção certa, mas
os critérios de aceite das Fases 4 e 5 não falsificam isso.

**Recomendação:** critério de aceite da Fase 4 na forma "nenhum handle exposto pela fixture é do tipo `Realm`; o
`Realm` usado por um teste vem sempre de `IRealmStore`/snapshot dentro do escopo do request", com um guard estático
(`rg` por `static.*Realm ` na pasta de fixtures) na Fase 6.

### 5.4 — Q6=A cria um segundo seed de contas demo, sem dono nem regra de consistência

DF14 proíbe código produtivo referenciar `Tests.*`. Se Q6=A (perfil demo mantido no Server oficial), passa a existir
um seed de contas demo **produtivo**, ao lado do `UserAccountsModuleSeed` **test-only** — dois seeds com Alice/Bob,
subjects, roles e property scopes que precisam permanecer consistentes ou divergir de propósito.

O plano cita o problema pela metade: a tarefa da Fase 4 ("mover `AliceSubjectId`/`BobSubjectId` para o seed
test-only") resolve o lado dos testes, e Q6-A diz "contas demo próprias da composição, sem depender de `Tests.*`",
mas nada declara se os dois seeds compartilham identidades ou são deliberadamente distintos.

**Recomendação:** ao responder Q6, fechar junto: (a) se as identidades demo produtivas são as mesmas dos testes ou
outras; (b) qual é a fonte da verdade; (c) que a duplicação, se aceita, é registrada como tal. A opção mais limpa é
**identidades distintas** — o seed de teste não deve depender do que o operador provisiona.

### 5.5 — `Fontes verificadas` aponta caminho errado para duas interfaces

O plano lista, sob `RoyalIdentity/Contracts/Defaults/`, os arquivos `ISingleUseAuthorizationCodeStore.cs` e
`IVersionedRefreshTokenStore.cs`. Ambos estão em **`RoyalIdentity/Contracts/Storage/`**. Só os dois consumers
(`DefaultAuthorizationCodeConsumer.cs`, `DefaultRefreshTokenConsumer.cs`) ficam em `Defaults/`.

Erro pequeno, mas numa seção cujo nome é "verificadas".

**Nota de apoio a Q9:** a verificação confirma que a opção **A** é viável sem efeito colateral. `IRefreshTokenStore.UpdateAsync`
tem, no repositório inteiro, **quatro** consumidores: o fallback do `DefaultRefreshTokenConsumer` (duas
chamadas), a implementação fake, a implementação EF — que já **lança** quando a transição condicional falha — e
dois testes de cenário em `RefreshTokenClaimsModeTests`. Não existe uso legítimo fora da transição; removê-lo não
deixa buraco funcional.

---

## 6. Observações menores

**6.1 — Q3 é uma pergunta com três eixos.** Responder Q3 produz três decisões (signing keys, catálogo Operational,
key ring do ASP.NET Data Protection). O gate de planejamento é por pergunta, então um eixo indeciso trava os três.
Dividir em Q3a/Q3b/Q3c torna o gate verificável por eixo e permite iniciar a Fase 1 quando só o eixo de signing
keys importa.

**6.2 — Sete perguntas bloqueiam a Fase 1.** Q1, Q2, Q3, Q5, Q6, Q7 e Q8. Q6 (perfil demo) e Q7 (options por realm)
não constrangem de fato o *contrato* de configuração: ambas podem entrar como superfície aditiva na Fase 3, com o
default "sem demo" e o resolver atual. Mover as duas para dependência da Fase 3 reduz o lote de decisão inicial de
sete para cinco sem perder rastreabilidade.

**6.3 — A Fase 6 não deixa o host livre do fake, e isso não está dito.** O critério "`Tests.Integration` não contém
uso de `MemoryStorage`" é satisfeito **com** `Tests.Host` ainda chamando `AddInMemoryStorage()` e a factory
removendo os registros. Está correto e coerente com DF7, mas um leitor tende a concluir que a Fase 6 já elimina o
fake da composição — e a Fase 7 é que faz isso. Uma frase no `O que/como` da Fase 6 evita a leitura errada. (Se a
recomendação de §4.2 for adotada — `Tests.Host` agnóstico de storage —, esta observação desaparece.)

**6.4 — Os filtros de teste das Fases 5 e 6 não foram validados contra os nomes reais das classes.** Filtros como
`FullyQualifiedName~CodeAuthorize` ou `FullyQualifiedName~LoginConsent` são plausíveis, mas um filtro que não casa
nada **passa com zero testes executados** — falso verde numa fase inteira. Vale conferir cada filtro contra
`dotnet test --list-tests` ao iniciar a fase, e registrar a contagem esperada no `Resultado da Fase` (o Plano 3 já
fazia isso e foi o que pegou contagens defasadas em duas revisões).

**6.5 — Fase 9: convenção de atualização de ADR.** Atualizar ADR-018 deve seguir [ADR.md](../../ADR.md) §2.1 — nova
seção `## 4. Revisão`, não edição do corpo. E o índice de `ADR.md` **ainda lista apenas ADR-001..009**, embora
existam até ADR-018; a tarefa documental da Fase 9 é o lugar natural para corrigir isso.

**6.6 — Objetivo 5 pré-supõe a resposta de Q10.** "Preservar ... a paridade PostgreSQL exigida por Q10" assume que
Q10 exige paridade; a opção C não exige (só provisionamento + smoke). Redação neutra: "a evidência PostgreSQL
decidida em Q10".

**6.7 — Fase 7 sem contagem de referência.** O critério "não houve perda de cenário sem substituição registrada" é
falsificável e bom. Fica mais forte com número: registrar quantos testes as 11 especializações `InMemory` executam
hoje e quantos a cobertura substituta executa depois.

---

## 7. Perguntas ausentes

Propostas, na numeração seguinte à do plano:

- **Q11 — Topologia e orçamento da fixture integral (§4.1):** como as 29+ fixtures obtêm banco?
  - **A)** arquivo SQLite por fixture, migrado do zero (o que a factory EF faz hoje).
  - **B)** um arquivo-template migrado uma vez, copiado por fixture.
  - **C)** SQLite in-memory com `ICollectionFixture` compartilhada por classes que não mutam configuração, e
    fixture dedicada às que mutam.
  - **Impacto se não decidir:** a Fase 4 escreve a fixture antes de saber o orçamento; corrigir depois reescreve a
    fixture e possivelmente o particionamento de todas as classes migradas.

- **Q12 — Mecanismo de coexistência nas Fases 5-6 (§4.2):** troca de tipo de fixture por arquivo (duas edições por
  arquivo) ou seletor interno ao `AppFactory` (uma edição)?
  - **Impacto se não decidir:** o volume de churn das Fases 5-6 e a forma da Fase 4 ficam indefinidos.

- **Q13 — Regra de triagem para asserções que falharem na migração (§4.3):** quem classifica uma falha como
  artefato do fake × regressão do módulo, e o que precisa ser confrontado antes de alterar o teste?
  - **Impacto se não decidir:** divergências reais de comportamento podem ser absorvidas como ajuste de teste.

---

## 8. Riscos ausentes na tabela

Sugestões, no formato da tabela existente:

| Risco | Gatilho | Impacto | Mitigação |
|---|---|---|---|
| Suíte HTTP fica ordens de grandeza mais lenta | 29 fixtures migram 3 famílias cada | ciclo de desenvolvimento degrada; CI encarece | Q11 (topologia) + orçamento medido como critério de aceite da Fase 4 |
| Comportamento do módulo diverge do fake em massa | asserções escritas contra `MemoryUserAccount`/mensagens do fake | regressão real absorvida como "ajuste de teste" | Q13 (triagem) + registro classificado no `Resultado da Fase 5` |
| Registro do fake sobrevive por lista de remoção incompleta | `Tests.Host` registra e a factory remove seletivamente | teste passa contra o backing errado, silenciosamente | tornar `Tests.Host` agnóstico de storage na Fase 4 |
| Handle estático de `Realm` mascara options divergentes | fixture expõe `Realm` em vez de `realmId` | teste exercita `RealmOptions` que a composição real não tem | critério de aceite §5.3 + guard estático |
| Filtro de teste não casa nenhuma classe | nome usado no `--filter` não existe | fase fecha com zero testes executados | conferir com `--list-tests` e registrar contagem esperada |

---

## 9. Pontos fortes

Registrados porque a revisão foi rigorosa nos dois sentidos:

1. **O inventário é sério.** Sete de oito contagens reproduzem exatamente, incluindo números de difícil chute
   (381 em 36 arquivos, 265, 28, 29). É a diferença entre um plano escrito lendo código e um escrito lendo planos.
2. **A sequência resolve o problema difícil na ordem certa.** DF7 fixa que o fallback só cai depois do default real
   verde, e Fase 8 concentra a quebra pública num corte compilável. A alternativa — tornar atomicidade obrigatória
   antes — exigiria dar CAS ao fake, contrariando ADR-018; o plano identifica isso explicitamente em `Lacunas`.
3. **O plano recusa a alternativa superada do macro-plano** ("manter o fake em testes específicos") com
   `SUPERSEDED` no `Histórico de decisões`, em vez de deixar as duas direções conviverem.
4. **Fase 7 antes de Fase 8 está correto** e é contraintuitivo: desacoplar `Tests.Storage`/`Tests.UserAccounts`
   **antes** da quebra pública é o que impede a Fase 8 de virar um corte gigante.
5. **A Fase 8 exige prova estática de ausência** (duas buscas `rg` com resultado esperado documentado), incluindo a
   instrução de classificar explicitamente as menções legítimas remanescentes de `UpdateAsync` — o nível de rigor
   que o Plano 3 mostrou ser necessário.
6. **Os invariantes 17-19** (setup de conta via módulo, refresh de snapshot após write, fixtures sem estado
   compartilhado) atacam exatamente os três modos de falha que a migração produz; o 19 é resposta direta ao
   vazamento de variável de ambiente encontrado na revisão da Fase 8 do Plano 3.

---

## 10. Checklist de correções recomendadas

Antes de responder Q1-Q10 e iniciar a Fase 1:

1. **Criar Q11** (topologia/orçamento da fixture), acrescentar critério de aceite com orçamento na Fase 4 e a
   linha de risco correspondente. — §4.1
2. **Criar Q12** (coexistência Fases 5-6) e transformar a resposta em tarefa explícita da Fase 4; ajustar o
   critério de aceite da Fase 5 para nomear o tipo de fixture do intervalo. — §4.2
3. **Criar Q13** (triagem de falhas) e a linha de risco de divergência módulo × fake; incluir a regra de registro
   classificado no `Resultado da Fase 5`. — §4.3
4. **Reescrever o invariante 12** para afirmar que o host **consegue** desproteger as signing keys provisionadas e
   nunca cria nem rotaciona material. — §4.4
5. **Corrigir ou requalificar a contagem de `RealmMemoryStore`** e anotar o comando exato ao lado de cada número do
   inventário. — §5.1
6. **Acrescentar à Fase 2** a tarefa de reexpressar `MigrationsRunner_ProjectGraph_References_Providers_Only` como
   allowlist, com a justificativa de por que o runner pode referenciar as duas famílias sem ferir ADR-013. — §5.2
7. **Acrescentar critério de aceite** proibindo handle do tipo `Realm` na fixture (Fase 4) e o guard estático
   correspondente (Fase 6). — §5.3
8. **Ampliar Q6** para fechar a relação entre o seed demo produtivo e o seed test-only (identidades, fonte da
   verdade, duplicação aceita). — §5.4
9. **Corrigir o caminho** de `ISingleUseAuthorizationCodeStore.cs`/`IVersionedRefreshTokenStore.cs` em
   `Fontes verificadas` (estão em `Contracts/Storage/`). — §5.5
10. **Registrar em `Estado atual do código`** que `Tests.Integration` já referencia `RoyalIdentity.Migrations`, que
    apenas quatro `.csproj` referenciam o fake, e que `AddInMemoryStorage()` não registra `IReplayCache`/`IMessageStore`. — §3
11. **Considerar** dividir Q3 em três eixos e mover Q6/Q7 para dependência da Fase 3. — §6.1, §6.2
12. **Validar os filtros `--filter`** das Fases 5-6 com `--list-tests` e registrar contagem esperada. — §6.4
13. **Fase 9:** atualizar ADR-018 por seção `## 4. Revisão` e corrigir o índice de `ADR.md` (parado em ADR-009). — §6.5

---

## 11. Referências

- [plan-data-test-migration.md](../plans/plan-data-test-migration.md) — objeto da revisão.
- [template-ai-implementation-plan.md](../references/template-plan/template-ai-implementation-plan.md) — shape e regras.
- [plan-data-operational-storage.md](../plans/plan-data-operational-storage.md) — DF39 (janela do fallback), Fase 8.
- [plan-data-storage-matrix.md](../plans/plan-data-storage-matrix.md) — semânticas normativas.
- [ADR-013](../../adrs/ADR-013.md), [ADR-015](../../adrs/ADR-015.md), [ADR-017](../../adrs/ADR-017.md), [ADR-018](../../adrs/ADR-018.md).
- [backlog-001.md](../backlogs/backlog-001.md) — "Substituir o storage fake in-memory…" (regressão opt-in representativa).
- `RoyalIdentity.Server/HostServices.cs`, `RoyalIdentity.Server/Program.cs`.
- `RoyalIdentity.Storage.InMemory/Extensions/ServiceCollectionExtensions.cs`.
- `RoyalIdentity.Storage.EntityFramework/Configuration/Resources/ConfigurationResourceBridgeOptions.cs`.
- `RoyalIdentity.UserAccounts.Integration/UserAccountsIntegrationExtensions.cs`.
- `Tests.Architecture/ConfigurationStorageBoundaryTests.cs`.
- `Tests.Integration/Prepare/EntityFrameworkStorageAppFactory.cs`, `UserAccountsAppFactory.cs`, `CharacterizationSeed.cs`.
- `Tests.Storage/Configuration/CompositeStorageSessionTests.cs`, `Tests.Storage/Configuration/Support/SqliteConfigurationStorageHarness.cs`.
- `Tests.UserAccounts/UserDirectoryContractTests.cs`.
