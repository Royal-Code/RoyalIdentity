# Plan: Proteção real contra replay de `jti` (`plan-replay-protection`)

## Status: RASCUNHO - inventário verificado em 2026-07-29; Q1-Q3 fechadas; implementação não iniciada

## Progresso

`░░░` **0%** - 0 de 3 fases

| Fase | Estado |
|---|---|
| Fase 1 - contrato atômico e composição fail-closed | Pendente |
| Fase 2 - backing real e prova de concorrência | Pendente |
| Fase 3 - composições, aceite PostgreSQL e fechamento | Pendente |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluída, `%` e `X de N`). Exemplo de barra: `█░░`.
> Antes de fechar uma fase, confirme que decisões, critérios de aceite, testes e invariantes relacionados foram
> aplicados.

> **Gate de planejamento:** não há decisão aberta. Qualquer decisão ausente encontrada durante a execução vira
> `Q<n>` em uma seção `Perguntas ao humano` reaberta, e a fase correspondente é marcada `Bloqueada`.

---

## Contexto

### Fontes verificadas

- [plan-data-storage-matrix.md](plan-data-storage-matrix.md) — RC-01/RC-02 marcados `substituir`; alvo nomeado é
  operação atômica add-if-absent; API atual não recebe `CancellationToken` (DF23).
- [backlog-001.md](../backlogs/backlog-001.md) — item "Replay cache com proteção real (check+add atômico)",
  arquivado com destino `plan-data-caching.md` por introduzir backing distribuído.
- [plan-data-macro.md](plan-data-macro.md) — Plano 5 rebaixado a não necessário; o item de replay é destacado
  como requisito de segurança que não deve herdar essa condicionalidade.
- [plan-data-operational-storage.md](plan-data-operational-storage.md) — MP-2/MP-3, `protocol_artifacts`,
  DF38 (handle bruto nunca persistido, somente digest SHA-256 com separação de domínio), DF24 (PostgreSQL
  opt-in, sem CI), harness de concorrência SQLite/PostgreSQL.
- [ADR-013](../../adrs/ADR-013.md) — storages como facades; `Data.*` puro; adaptação somente por
  `RoyalIdentity.Storage.EntityFramework`.

### Estado atual do código (verificado em 2026-07-29)

- **O default de DI não protege:** `RoyalIdentity/Extensions/ServiceCollectionExtensions.cs:63` registra
  `services.AddTransient<IReplayCache, DefaultReplayNoCache>()`.
- **`DefaultReplayNoCache` é no-op total:** `AddAsync` retorna `Task.CompletedTask` e `ExistsAsync` retorna
  sempre `false`. O ramo de detecção do caller é inalcançável nessa composição.
- **O doc da classe está incorreto:** o XML doc de `DefaultReplayNoCache` diz
  *"Default implementation of the replay cache using IDistributedCache"*, copiado de
  `DefaultReplayDistributedCache`.
- **O check+add do caller não é atômico:**
  `RoyalIdentity/Contracts/Defaults/SecretsEvaluators/PrivateKeyJwtSecretEvaluator.cs:154` chama
  `ExistsAsync` e a linha 160 chama `AddAsync`; são duas operações separadas.
- **Consumidor único:** `IReplayCache` só é injetado em `PrivateKeyJwtSecretEvaluator`; as demais ocorrências
  são as duas implementações, a interface e o registro de DI.
- **A API não recebe `CancellationToken`:** `RoyalIdentity/Contracts/Storage/IReplayCache.cs` declara
  `AddAsync(string, string, DateTimeOffset)` e `ExistsAsync(string, string)`.
- **A chave distribuída concatena sem delimitador:** `DefaultReplayDistributedCache` monta
  `Prefix + purpose + handle`, com `Prefix = nameof(DefaultReplayDistributedCache) + ":"`.
- **`IDistributedCache` não oferece add-if-absent atômico:** expõe `GetAsync`/`SetAsync`; não há operação
  condicional de inserção.
- **`IDistributedCache` nunca é registrado:** a busca por `IDistributedCache` no repositório retorna apenas o
  campo/construtor/doc de `DefaultReplayDistributedCache` e o doc copiado de `DefaultReplayNoCache`. Não há
  `AddDistributedMemoryCache`, `AddStackExchangeRedisCache` nem `PackageReference` de caching em nenhum
  `.csproj`; `IMemoryCache`/`AddMemoryCache` também não aparecem. `DefaultReplayDistributedCache` é código morto
  por duas razões independentes: nada aponta `IReplayCache` para ela e seu construtor não teria como ser
  satisfeito.
- **A validação do Server recusa seletor de provider em configuração:**
  `RoyalIdentity.Server/Configuration/ServerConfigurationServiceCollectionExtensions.cs` reprova o startup se a
  seção `RoyalIdentity:DataProtection` trouxer a chave `Provider`, com a mensagem
  *"The Server does not support a protection provider selector"*.
- **Cobertura zero:** busca por `ReplayCache|PrivateKeyJwt` nos projetos `Tests.*` não retorna nenhum arquivo.
- **O contrato é global, não realm-bound:** `purpose` e `handle` são as únicas chaves; nenhum outro store do IdP
  tem essa forma.
- **Replay entre realms já é impossível:** o evaluator valida a assertion com
  `ValidAudiences = [issuerUri + Oidc.Routes.BuildTokenUrl(context.Realm.Path)]` e `ValidateAudience = true`,
  além de `ValidIssuer = clientId` com `ValidateIssuer = true`. Uma assertion emitida para um realm é rejeitada
  em outro antes de o replay cache ser consultado.
- **O evaluator é registrado incondicionalmente:** `ServiceCollectionExtensions.cs:73` registra
  `IClientSecretEvaluator, PrivateKeyJwtSecretEvaluator` junto dos outros quatro; sem `IReplayCache` no
  container, a resolução da cadeia falha.

### Lacunas, conflitos e restrições

- **A API atual não permite a correção:** duas operações independentes não podem ser tornadas atômicas pelo
  caller; a substituição do contrato é pré-requisito, não preferência.
- **Falhar sem default declarado é quebra de upgrade:** por DF12 nenhuma composição sobe sem declarar o backing,
  inclusive as que nunca usam `private_key_jwt`; cada composition root ganha uma linha obrigatória.
- **`IDistributedCache` sozinho não resolve:** qualquer implementação sobre ele precisa de operação nativa
  condicional do backing (por exemplo `SET NX`) ou de outro armazenamento.
- **A família Operational já resolve o problema equivalente:** `protocol_artifacts` prova vencedor único sob
  concorrência real, mas usar Operational significa exigir storage EF onde hoje só há `IDistributedCache`
  opcional.
- **Sem cobertura, qualquer regressão é silenciosa:** não existe teste que falhe se a proteção for removida.

### Superfícies impactadas a mapear

- `RoyalIdentity/Contracts/Storage/IReplayCache.cs` — contrato público a substituir.
- `RoyalIdentity/Contracts/Defaults/DefaultReplayNoCache.cs` e `DefaultReplayDistributedCache.cs` —
  implementações atuais.
- `RoyalIdentity/Contracts/Defaults/SecretsEvaluators/PrivateKeyJwtSecretEvaluator.cs` — consumidor único.
- `RoyalIdentity/Extensions/ServiceCollectionExtensions.cs` — registro default.
- `RoyalIdentity.Storage.EntityFramework` + `RoyalIdentity.Data.Operational` — backing durável (DF10).
- `RoyalIdentity.Server`, `RoyalIdentity.Demo`, `Tests.Integration/Prepare` — composições que passam a declarar
  a escolha.
- `Tests.Storage`, `Tests.Integration`, `Tests.Architecture` — concorrência, fluxo e guards.
- `plan-data-storage-matrix.md`, `backlog-001.md`, `plan-data-macro.md` — registro normativo a fechar.

---

## Objetivo

1. Substituir `AddAsync`/`ExistsAsync` por uma operação atômica de add-if-absent, com `CancellationToken`.
2. Eliminar a possibilidade de uma composição rodar `private_key_jwt` sem proteção efetiva contra replay.
3. Entregar pelo menos um backing real com vencedor único provado sob concorrência em SQLite e PostgreSQL.
4. Cobrir com teste o que hoje não tem teste: detecção de replay, corrida de dois chamadores e recusa
   fail-closed.
5. Fechar RC-01/RC-02 na matriz e retirar o item do backlog e da órbita do plano de caching.

## Fora de escopo

- Cache de leitura sobre stores EF — destino: `plan-data-caching.md`, rebaixado a não necessário.
- Pushed Authorization Requests (RFC 9126) e persistência de `IMessageStore` — destino: backlog e
  `an-par-rfc-9126.md`.
- Outros evaluators de secret (`client_secret_basic`/`post`, `tls_client_auth`) — não usam `IReplayCache`.
- API/UI administrativa para inspecionar ou limpar handles registrados.
- Reescrita do fluxo de validação de `private_key_jwt` além da troca da chamada de replay.

---

## Decisões fechadas

- **DF1 — Operação única substitui check+add:** o contrato passa a expor uma operação atômica add-if-absent que
  informa se o handle já existia. `AddAsync`/`ExistsAsync` são removidos. Fonte: matriz RC-01/RC-02
  (`substituir`) e ausência de atomicidade verificada no caller.
- **DF2 — `CancellationToken` obrigatório:** a nova operação recebe `CancellationToken`, fechando a lacuna
  registrada na DF23 do baseline. Fonte: matriz.
- **DF3 — Nenhum no-op silencioso permanece:** nenhuma composição pode obter proteção aparente e efeito nulo;
  a forma concreta está em DF12, e o no-op atual é removido. Fonte: precedentes DF10/DF11
  do Plano 3.
- **DF4 — Handle bruto não é persistido:** onde houver persistência, grava-se digest SHA-256 com separação de
  domínio, nunca o `jti` em claro. Fonte: DF38 do Plano 3.
- **DF5 — Semântica de recusa preservada:** replay detectado continua produzindo credencial inválida e log sem
  expor a assertion. Fonte: comportamento atual em `PrivateKeyJwtSecretEvaluator`.
- **DF6 — Prova de concorrência obrigatória:** vencedor único com dois chamadores simultâneos é aceite da
  Fase 2, em SQLite sempre e em PostgreSQL opt-in, reusando o formato dos testes de concorrência de MP-2.
  Fonte: Plano 3.
- **DF7 — Sem CI:** este plano não cria pipeline; PostgreSQL permanece aceite local/opt-in. Fonte: DF24 do
  Plano 4.
- **DF8 — Expiração preservada:** o registro do handle expira segundo o `exp` da assertion mais a folga já
  aplicada hoje; a proteção não depende de limpeza para estar correta. Fonte: caller atual.
- **DF9 — Seleção por extension, não por configuração:** a composition root escolhe o backing chamando uma
  extension dedicada; a configuração fornece apenas parâmetros da escolha feita. Não há chave de `appsettings`
  que selecione implementação. Fonte: resposta humana a Q2; idioma já usado por cleanup, proteção de payload e
  protector de signing keys; validação do Server que recusa `Provider` em configuração; DF17 do Plano 4.
- **DF10 — Duas implementações neste plano:** uma in-memory por instância e uma durável sobre a família
  Operational, ambas satisfazendo o mesmo contrato. A in-memory segue o tratamento do Plain da DF11 do Plano 3:
  registro explícito, warning e nunca default; é válida somente em instância única. Fonte: resposta humana a Q2.
- **DF11 — Redis e demais backings distribuídos ficam fora:** só entram quando existir deployment que precise
  deles, como extension adicional sobre o mesmo contrato. Nenhum pacote de cache distribuído entra no grafo do
  Server por este plano. Fonte: resposta humana a Q2 e DF17 do Plano 4.
- **DF12 — Sem registro default; a composition root declara:** `AddOpenIdConnectProviderServices()` não registra
  `IReplayCache`. Sem declaração, a composição falha ao resolver a cadeia de `IClientSecretEvaluator` — em
  composições com `ValidateOnBuild`, na construção do provider. A mensagem de falha nomeia as extensions
  disponíveis. Fonte: resposta humana a Q1; DF10/DF11 do Plano 3; `ServiceCollectionExtensions.cs:73`.
- **DF13 — Contrato realm-bound:** a chave inclui `realmId`, como todo o resto do storage do IdP. Não há perda de
  proteção: a validação de audience já impede que uma assertion de um realm seja apresentada em outro. Fonte:
  resposta humana a Q3 e validação verificada no evaluator.

---

## Histórico de decisões

**Fase 2 (backing real):**

- **Q2 — Onde vive o armazenamento de handles:** Operational EF; `IDistributedCache` com operação condicional
  nativa; ou ambos com seleção pela composition root.
  - **Considerações:** `IDistributedCache` não expressa add-if-absent e **não está registrado em lugar nenhum**
    do repositório, nem há pacote de caching referenciado — a opção distribuída não é adaptação de infraestrutura
    existente, é introdução de dependência nova. A família Operational já tem unique constraint, vencedor único
    provado em `protocol_artifacts`, migrations por provider, runner e limpeza por TTL.
  - **Alternativa avaliada e descartada — seletor por `IConfiguration`:** permitir escolher o backing por
    `appsettings`. Descartada porque configuração não cria dependência: a união de todos os pacotes entraria no
    binário de todas as composições, cada opção exigiria validação e prova de concorrência próprias, e o Server
    hoje **recusa** explicitamente seletor de provider em configuração.
  - **Conclusão Q2:** fechada por DF9/DF10/DF11 — seleção por extension na composition root, duas implementações
    agora, distribuído diferido.

**Fase 1 (contrato e composição):**

- **Q1 — Comportamento quando a composition root não declara backing:** falhar ao resolver, atingindo o host
  inteiro, ou falhar só no uso, recusando `private_key_jwt` e preservando os demais métodos.
  - **Considerações:** o evaluator é registrado incondicionalmente, então a ausência de `IReplayCache` derruba a
    cadeia inteira. Falhar só no uso mantém o host de pé, mas transforma erro de configuração em `invalid_client`
    por request, indistinguível de falha legítima de credencial e descoberto em produção. Um default que resolve
    sozinho foi exatamente o que permitiu o no-op passar despercebido durante toda a vida do produto.
  - **Conclusão Q1:** fechada por DF12 — sem registro default, falha na composição, mensagem nomeando as
    extensions.

- **Q3 — Escopo de realm no contrato:** incluir `realmId` ou manter `purpose` + `handle` global.
  - **Considerações:** a validação de audience do evaluator já fixa o token endpoint do realm corrente, então
    replay entre realms não ocorre e a chave global não acrescenta proteção. Ela acrescentaria dois custos: falso
    positivo quando dois realms usam o mesmo `jti`, e um oráculo fraco entre realms pela rejeição. O custo de
    incluir o realm é nulo: `context.Realm` já está no call site.
  - **Conclusão Q3:** fechada por DF13 — contrato realm-bound.

## Design alvo

### Contratos e bordas

- `IReplayCache` (core): uma operação, semântica de add-if-absent.

```csharp
Task<bool> TryAddAsync(
    string realmId, string purpose, string handle, DateTimeOffset expiration, CancellationToken ct);
// true  = registrado agora; o chamador pode prosseguir.
// false = já existia; é replay.
```

- `PrivateKeyJwtSecretEvaluator`: uma única chamada substitui o par das linhas 154/160; o ramo de replay passa a
  ser alcançável em qualquer composição válida.
- Seleção do backing (DF9): duas extensions dedicadas, sem chave de configuração que escolha implementação.

```csharp
services.AddInMemoryReplayProtection();     // instância única; warning explícito; nunca default (DF10)
services.AddOperationalReplayProtection();  // durável sobre a família Operational
```

- Registro default: não existe (DF12). `AddOpenIdConnectProviderServices()` não registra `IReplayCache`, e
  `DefaultReplayNoCache` é removida.
- `DefaultReplayDistributedCache` é removida: não expressa add-if-absent e depende de uma abstração sem
  implementação registrada no repositório.
- Chave: `realmId` + `purpose` + digest do handle, com delimitador explícito entre os campos, sem concatenação
  ambígua (DF4/DF13).

### Modelo, dados e persistência

Aplicável à implementação durável (DF10).

```text
operation.replay_handles          (nome final a definir na Fase 2)
  RealmId            text     not null
  Purpose            text     not null
  HandleDigest       text     not null   -- SHA-256 com separação de domínio (DF4)
  ExpiresAtUtc       timestamp not null
  unique (RealmId, Purpose, HandleDigest)
  index (ExpiresAtUtc)                   -- limpeza por TTL
```

O vencedor único vem da unique constraint, como em `protocol_artifacts`: a segunda inserção viola e é traduzida
em `false`, sem leitura prévia. A implementação in-memory obtém a mesma semântica com uma operação atômica de
dicionário concorrente, válida somente dentro do processo.

### Arquitetura alvo

```text
RoyalIdentity/                          contrato IReplayCache + consumidor + implementação in-memory
RoyalIdentity.Data.Operational/         entidade de replay
RoyalIdentity.Storage.EntityFramework/  implementação durável + extension de registro
RoyalIdentity.Server /                  escolhe a durável
RoyalIdentity.Demo /                    escolhe a in-memory, coerente com seu caráter efêmero
Tests.Integration/Prepare/              escolhe explicitamente, conforme o cenário

-X-> nenhum pacote de cache distribuído entra no grafo por este plano (DF11)
```

### Segurança, concorrência e confiabilidade

- Dois chamadores simultâneos com o mesmo handle produzem exatamente um `true`.
- Falha do backing não pode ser traduzida em `true`: erro de infraestrutura falha fechado, nunca autoriza.
- Nenhum log, mensagem de erro ou exceção expõe a assertion, o `jti` em claro ou connection string.
- A limpeza de handles expirados é higiene de volume, não condição de correção: um handle expirado não
  autoriza replay porque a assertion já está fora do `exp`.
- A chave carrega `realmId` (DF13); nenhum lookup atravessa realms.

### Compatibilidade, migração e rollout

- Quebra pública de `IReplayCache` em corte único: contrato, implementações, consumidor e registro na mesma
  alteração compilável.
- Não há dado a migrar: handles são efêmeros e limitados ao `exp` da assertion.
- Composições existentes precisam declarar a escolha (DF12); a ausência de declaração é falha explícita na
  construção do provider, não degradação silenciosa.

---

## Ordem de execução

1. **Fase 1 (contrato e composição)** — aplica DF12/DF13 e torna a proteção alcançável; sem backing ainda.
2. **Fase 2 (backing real)** — entrega as duas implementações da DF10 e prova vencedor único sob concorrência.
3. **Fase 3 (composições e fechamento)** — liga Server/Demo/testes, guards, aceite PostgreSQL e documentação.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

---

## Fase 1 - contrato atômico e composição fail-closed

**Depende de:** DF1-DF5, DF12, DF13.

**Escopo:** `RoyalIdentity/Contracts/Storage/IReplayCache.cs`, `Contracts/Defaults/DefaultReplay*.cs`,
`SecretsEvaluators/PrivateKeyJwtSecretEvaluator.cs`, `Extensions/ServiceCollectionExtensions.cs`,
`Tests.Identity` ou `Tests.Integration` para os testes negativos.

**O que/como:** substituir o contrato pela operação única realm-bound, adequar o consumidor a uma só chamada e
retirar o registro default. Não introduzir persistência nesta fase.

**Tarefas:**

- [ ] Substituir `IReplayCache` pela operação atômica realm-bound com `CancellationToken`, conforme DF1/DF2/DF13.
- [ ] Remover `DefaultReplayNoCache` e o registro em `ServiceCollectionExtensions.cs:63`, sem substituí-lo por
  outro default (DF12).
- [ ] Fazer a falha por ausência de declaração nomear as extensions disponíveis, em vez de emitir erro genérico
  de DI.
- [ ] Reduzir o par das linhas 154/160 de `PrivateKeyJwtSecretEvaluator` a uma única chamada, preservando DF5.
- [ ] Adequar ou remover `DefaultReplayDistributedCache`, que não expressa add-if-absent atômico.
- [ ] Passar `context.Realm.Id` no call site, aproveitando o realm já usado para montar o audience.
- [ ] Cobrir com teste: replay detectado recusa a credencial; primeira apresentação é aceita; composição sem
  backing declarado falha na construção do provider com mensagem que cita as extensions; o mesmo handle em
  realms distintos não interfere; nenhuma mensagem contém a assertion ou o `jti` em claro.

**Critérios de aceite:** `IReplayCache` expõe uma única operação realm-bound com `CancellationToken`;
`AddAsync`/`ExistsAsync` não existem; `DefaultReplayNoCache` não existe e nenhum outro default o substitui; o ramo
de replay do evaluator é alcançável e coberto por teste; uma composição sem declaração falha na construção do
provider, com mensagem citando as extensions, provado por teste; handles iguais em realms distintos não
interferem; a solução compila e as suítes existentes seguem verdes.

**Testes:**

```powershell
dotnet build RoyalIdentity.sln
dotnet test Tests.Identity
dotnet test Tests.Integration --filter "FullyQualifiedName~PrivateKeyJwt|FullyQualifiedName~Replay"
```

### Resultado da Fase 1

*a preencher*

---

## Fase 2 - backing real e prova de concorrência

**Depende de:** Fase 1, DF4, DF6, DF8, DF9-DF11.

**Escopo:** `RoyalIdentity` (implementação in-memory), `RoyalIdentity.Data.Operational` e
`RoyalIdentity.Storage.EntityFramework` (implementação durável), migrations dos providers, `Tests.Storage`.

**O que/como:** entregar as duas implementações da DF10 atrás do mesmo contrato, cada uma com vencedor único
garantido pela própria estrutura — unique constraint na durável, operação atômica de dicionário na in-memory —
nunca por leitura prévia.

**Tarefas:**

- [ ] Implementar a in-memory por instância com operação atômica única, expiração respeitada e warning explícito
  no registro, conforme DF10.
- [ ] Implementar a durável com inserção condicional; traduzir violação de unicidade em `false`, sem consulta
  prévia.
- [ ] Persistir digest conforme DF4; nunca o handle em claro.
- [ ] Criar entidade, mapeamentos e migrations por provider, sem colidir com as histories existentes.
- [ ] Fazer falha de infraestrutura falhar fechado, nunca retornando `true`.
- [ ] Cobrir expiração nas duas implementações: handle expirado não impede novo registro e não autoriza replay
  dentro do `exp`.
- [ ] Provar vencedor único com dois chamadores simultâneos nas duas implementações; a durável em SQLite sempre e
  em PostgreSQL opt-in, reusando o formato dos testes de concorrência de authorization code.
- [ ] Integrar a limpeza de expirados da durável ao mecanismo Operational existente.
- [ ] Não adicionar `PackageReference` de cache distribuído a nenhum projeto, conforme DF11.

**Critérios de aceite:** duas chamadas simultâneas com o mesmo handle produzem exatamente um `true` nas duas
implementações, e na durável isso é provado em SQLite e no aceite PostgreSQL opt-in; nenhuma linha persiste handle
em claro; falha do backing não autoriza; handle expirado não bloqueia registro novo; migrations aplicam sem
colidir histories; nenhum pacote de cache distribuído entrou no grafo.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~Replay"
./scripts/Test-OperationalPostgreSql.ps1
```

### Resultado da Fase 2

*a preencher*

---

## Fase 3 - composições, aceite PostgreSQL e fechamento

**Depende de:** Fases 1-2, DF3, DF6, DF7.

**Escopo:** `RoyalIdentity.Server`, `RoyalIdentity.Demo`, `Tests.Integration/Prepare`, `Tests.Architecture`,
`plan-data-storage-matrix.md`, `backlog-001.md`, `plan-data-macro.md`, READMEs afetados.

**O que/como:** ligar o backing nas composições reais, provar por guard que nenhuma delas resolve proteção
inefetiva, executar o aceite PostgreSQL e fechar o registro normativo.

**Tarefas:**

- [ ] Registrar o backing escolhido em `RoyalIdentity.Server`, `RoyalIdentity.Demo` e na factory persistente de
  `Tests.Integration`.
- [ ] Adicionar guard arquitetural que rejeite reintrodução de implementação no-op de `IReplayCache`.
- [ ] Provar um fluxo `private_key_jwt` completo sobre a composição persistente: primeira apresentação aceita,
  repetição recusada.
- [ ] Executar o aceite PostgreSQL local/opt-in e registrar comando, contagens e ausência de containers
  residuais.
- [ ] Atualizar RC-01/RC-02 na matriz para o contrato final, removendo a marcação `substituir`.
- [ ] Remover o item de replay do `backlog-001.md` e a menção condicionada no `plan-data-macro.md`.
- [ ] Atualizar README/runbook onde a escolha do backing precisa ser declarada pelo operador.
- [ ] Executar `dotnet build` e `dotnet test` da solução completa.

**Critérios de aceite:** Server, Demo e a factory persistente resolvem um backing real; guard rejeita no-op; o
fluxo `private_key_jwt` recusa a segunda apresentação em teste de integração; aceite PostgreSQL verde e
registrado; matriz, backlog e macro-plano refletem o estado final; solução completa verde.

**Testes:**

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
dotnet test Tests.Architecture
./scripts/Test-ServerPostgreSql.ps1
```

### Resultado da Fase 3

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(es) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| 1 — operação atômica com CT | 1 | DF1, DF2 | contrato com uma operação; `AddAsync`/`ExistsAsync` inexistentes | `dotnet build`, `Tests.Identity` |
| 2 — nenhuma composição sem proteção | 1, 3 | DF3, DF12 | composição sem backing falha; guard rejeita no-op | filtro `Replay`, `Tests.Architecture` |
| 3 — backing com vencedor único | 2 | DF4, DF6, DF9-DF11 | exatamente um `true` sob concorrência, SQLite e PostgreSQL | `Tests.Storage`, script PostgreSQL |
| 4 — cobertura do que não tinha teste | 1, 2, 3 | DF5, DF6 | replay recusado; corrida provada; fail-closed provado | filtros `Replay`/`PrivateKeyJwt` |
| 5 — registro normativo fechado | 3 | DF7 | RC-01/RC-02 sem `substituir`; item fora do backlog | revisão documental + suíte completa |

---

## Invariantes a preservar

1. Toda consulta e mutação do replay store permanece realm-scoped (DF13).
2. `RoyalIdentity` não referencia providers, Server, Demo ou projetos `Data.*`.
3. `Data.*` permanece puro e só é adaptado por `RoyalIdentity.Storage.EntityFramework`.
4. Falha de infraestrutura nunca é traduzida em autorização.
5. Handle bruto não é persistido nem registrado em log.
6. O processo web não aplica migration nem seed; provisionamento continua externo.
7. Validators seguem sinalizando falha esperada por `context.Response`, sem lançar por erro de protocolo.
8. Nenhuma semântica fechada em `plan-data-storage-matrix.md` é reaberta além de RC-01/RC-02.
9. Nenhuma composição obtém proteção aparente com efeito nulo.

---

## Critérios globais de conclusão

- Nenhuma decisão aberta: Q1-Q3 fechadas por DF9-DF13.
- `IReplayCache` expõe somente a operação atômica realm-bound, com `CancellationToken`.
- Nenhuma implementação no-op de `IReplayCache` existe no repositório.
- Vencedor único provado em SQLite e no aceite PostgreSQL opt-in.
- Fluxo `private_key_jwt` com replay recusado coberto por teste de integração.
- RC-01/RC-02 atualizados na matriz; item removido do backlog e da órbita do plano de caching.
- `dotnet build RoyalIdentity.sln` verde.
- `dotnet test RoyalIdentity.sln` verde.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| Host deixa de subir após upgrade | DF12 e composição que ainda não declara o backing | qualquer host para, inclusive quem nunca usou `private_key_jwt` | mensagem nomeando as extensions; Fase 3 atualiza Server, Demo, fixtures e runbook | Aberto |
| In-memory usada em cluster | operador escolhe a extension in-memory em host replicado | replay atravessa instâncias e a proteção some | warning no registro, README explícito e nome da extension declarando a limitação (DF10) | Aberto |
| Pressão futura por seletor em configuração | pedido de escolher backing por `appsettings` | união de dependências no binário e validação por opção | DF9; incoerência com a validação que já recusa `Provider` em configuração | Mitigado |
| Digest mal separado permite colisão entre purposes | concatenação sem delimitador, como hoje | handle de um purpose bloqueia outro | delimitador explícito e separação de domínio no digest (DF4) | Aberto |
| Limpeza confundida com correção | TTL tratado como condição de segurança | expiração vira dependência de disponibilidade | correção vem do `exp` da assertion; limpeza é volume (DF8) | Aberto |
| Teste de concorrência passa por acaso | duas chamadas serializadas pelo harness | corrida não é exercitada | reusar o formato dos testes de MP-2, que já provam paralelismo real | Aberto |
| `jti` aparece em log ou exceção | mensagem de erro inclui o handle | vazamento de credencial em texto | asserção negativa nos testes de mensagem (DF5) | Aberto |

---

## Diferidos e backlog

- Cache de leitura sobre stores EF — destino: `plan-data-caching.md`, rebaixado a não necessário.
- PAR (RFC 9126) e `PersistentDataMessageStore` — destino: `an-par-rfc-9126.md` e backlog.
- Backing distribuído (Redis ou equivalente) — destino: extension adicional sobre o mesmo contrato, quando
  existir deployment que precise dele (DF11). Exige operação condicional nativa; `IDistributedCache` não serve.
- Inspeção/limpeza administrativa de handles registrados — destino: roadmap administrativo.
- Aplicação de proteção contra replay a outros artefatos de uso único, se surgirem — destino: avaliação futura,
  não assumida por este plano.

---

## Referências

- [plan-data-storage-matrix.md](plan-data-storage-matrix.md) — RC-01/RC-02.
- [plan-data-operational-storage.md](plan-data-operational-storage.md) — MP-2, DF38, DF24, harness de concorrência.
- [plan-data-macro.md](plan-data-macro.md) — Plano 5 e destaque do item de replay.
- [backlog-001.md](../backlogs/backlog-001.md) — item original.
- [ADR-013](../../adrs/ADR-013.md).
- `RoyalIdentity/Contracts/Storage/IReplayCache.cs`.
- `RoyalIdentity/Contracts/Defaults/DefaultReplayNoCache.cs`.
- `RoyalIdentity/Contracts/Defaults/DefaultReplayDistributedCache.cs`.
- `RoyalIdentity/Contracts/Defaults/SecretsEvaluators/PrivateKeyJwtSecretEvaluator.cs`.
- `RoyalIdentity/Extensions/ServiceCollectionExtensions.cs`.
