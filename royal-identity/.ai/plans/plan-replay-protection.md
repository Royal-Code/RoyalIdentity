# Plan: Proteção real contra replay de `jti` (`plan-replay-protection`)

## Status: RASCUNHO - inventário verificado em 2026-07-29; Q1-Q3 abertas; implementação não iniciada

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

> **Gate de planejamento:** a Fase 1 não pode iniciar com Q1 ou Q3 abertas; a Fase 2 não pode iniciar com Q2
> aberta. Respostas humanas viram `DF<n>` em `Decisões fechadas` e entram em `Histórico de decisões` antes do
> primeiro edit da fase correspondente.

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
- **Cobertura zero:** busca por `ReplayCache|PrivateKeyJwt` nos projetos `Tests.*` não retorna nenhum arquivo.
- **O contrato é global, não realm-bound:** `purpose` e `handle` são as únicas chaves; nenhum outro store do IdP
  tem essa forma.

### Lacunas, conflitos e restrições

- **A API atual não permite a correção:** duas operações independentes não podem ser tornadas atômicas pelo
  caller; a substituição do contrato é pré-requisito, não preferência.
- **Não há default seguro possível sem decisão:** manter um no-op silencioso reproduz o defeito; falhar fechado
  altera o comportamento de `private_key_jwt` em composições existentes.
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
- `RoyalIdentity.Storage.EntityFramework` + `RoyalIdentity.Data.Operational` — destino possível do backing (Q2).
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

## Perguntas ao humano

- **Q1 — Política de default quando não há backing real registrado:** o que a composição faz?
  - **Opções:**
    - **A)** Fail-closed no registro: `AddOpenIdConnectProviderServices()` não registra `IReplayCache`; a
      composition root escolhe explicitamente, como já ocorre com cleanup (DF10 do Plano 3) e proteção de payload
      (DF11). Sem escolha, resolver o serviço falha antes do tráfego.
    - **B)** Fail-closed no uso: registra um default que recusa `private_key_jwt` com credencial inválida e log
      explícito, mantendo os demais métodos de autenticação funcionando.
    - **C)** Default in-memory por instância: correto em instância única, insuficiente em cluster; exige warning
      e documentação de limitação.
  - **Impacto se não decidir:** bloqueia o contrato, o registro de DI e os testes negativos da Fase 1.
  - **Status:** Aberta.

- **Q2 — Backing real:** onde vive o armazenamento de handles?
  - **Opções:**
    - **A)** Família Operational (EF): nova entidade em `RoyalIdentity.Data.Operational` com unique constraint,
      reusando o padrão de vencedor único de `protocol_artifacts` e a limpeza/TTL já existente (MP-6). Exige
      storage EF onde hoje há `IDistributedCache` opcional.
    - **B)** `IDistributedCache` com operação condicional nativa do backing (por exemplo Redis `SET NX`),
      substituindo `DefaultReplayDistributedCache`. Não é expressável pela API de `IDistributedCache`; exige
      dependência direta do cliente do backing.
    - **C)** Ambos, com a composition root escolhendo.
  - **Impacto se não decidir:** bloqueia o modelo de dados, as referências de projeto e a Fase 2 inteira.
  - **Status:** Aberta.

- **Q3 — Escopo de realm no contrato:** a chave passa a incluir `realmId`?
  - **Opções:**
    - **A)** Sim: `jti` é único por emissor, mas o contrato passa a ser realm-bound como todo o resto do storage
      do IdP; colisão entre realms deixa de ser possível por construção.
    - **B)** Não: mantém `purpose` + `handle` global, com delimitador explícito entre os campos; um `jti`
      registrado num realm bloqueia o mesmo `jti` em outro.
  - **Impacto se não decidir:** bloqueia a assinatura do contrato e o desenho da chave/índice.
  - **Status:** Aberta.

---

## Decisões fechadas

- **DF1 — Operação única substitui check+add:** o contrato passa a expor uma operação atômica add-if-absent que
  informa se o handle já existia. `AddAsync`/`ExistsAsync` são removidos. Fonte: matriz RC-01/RC-02
  (`substituir`) e ausência de atomicidade verificada no caller.
- **DF2 — `CancellationToken` obrigatório:** a nova operação recebe `CancellationToken`, fechando a lacuna
  registrada na DF23 do baseline. Fonte: matriz.
- **DF3 — Nenhum no-op silencioso permanece:** nenhuma composição pode obter proteção aparente e efeito nulo;
  a forma concreta disso é Q1, mas o no-op atual é removido em qualquer resposta. Fonte: precedentes DF10/DF11
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

---

## Design alvo

### Contratos e bordas

- `IReplayCache` (core): uma operação, semântica de add-if-absent.

```csharp
// Assinatura final depende de Q3 (realmId) — a forma abaixo é o shape sem realm.
Task<bool> TryAddAsync(string purpose, string handle, DateTimeOffset expiration, CancellationToken ct);
// true  = registrado agora; o chamador pode prosseguir.
// false = já existia; é replay.
```

- `PrivateKeyJwtSecretEvaluator`: uma única chamada substitui o par das linhas 154/160; o ramo de replay passa a
  ser alcançável em qualquer composição válida.
- Registro de DI: conforme Q1; em qualquer resposta, `DefaultReplayNoCache` deixa de existir.
- Chave: campos separados por delimitador explícito, sem concatenação ambígua; conteúdo conforme DF4 e Q3.

### Modelo, dados e persistência

Aplicável somente se Q2 = A ou C.

```text
operation.replay_handles          (nome final a definir na Fase 2)
  RealmId            text     null se Q3=B
  Purpose            text     not null
  HandleDigest       text     not null   -- SHA-256 com separação de domínio (DF4)
  ExpiresAtUtc       timestamp not null
  unique (RealmId, Purpose, HandleDigest)
  index (ExpiresAtUtc)                   -- limpeza por TTL
```

O vencedor único vem da unique constraint, como em `protocol_artifacts`: a segunda inserção viola e é traduzida
em `false`, sem leitura prévia.

### Arquitetura alvo

```text
RoyalIdentity/                      contrato IReplayCache + consumidor + política de registro
RoyalIdentity.Data.Operational/     entidade de replay, se Q2 = A ou C
RoyalIdentity.Storage.EntityFramework/  implementação EF do contrato
RoyalIdentity.Server / .Demo /
Tests.Integration/Prepare/          escolhem explicitamente o backing
```

### Segurança, concorrência e confiabilidade

- Dois chamadores simultâneos com o mesmo handle produzem exatamente um `true`.
- Falha do backing não pode ser traduzida em `true`: erro de infraestrutura falha fechado, nunca autoriza.
- Nenhum log, mensagem de erro ou exceção expõe a assertion, o `jti` em claro ou connection string.
- A limpeza de handles expirados é higiene de volume, não condição de correção: um handle expirado não
  autoriza replay porque a assertion já está fora do `exp`.
- Isolamento por realm conforme Q3; se realm-bound, a chave carrega `realmId` e nenhum lookup atravessa realms.

### Compatibilidade, migração e rollout

- Quebra pública de `IReplayCache` em corte único: contrato, implementações, consumidor e registro na mesma
  alteração compilável.
- Não há dado a migrar: handles são efêmeros e limitados ao `exp` da assertion.
- Composições existentes precisam declarar a escolha conforme Q1; a ausência de declaração é falha explícita,
  não degradação silenciosa.

---

## Ordem de execução

1. **Fase 1 (contrato e composição)** — fecha Q1/Q3 e torna a proteção alcançável; sem backing ainda.
2. **Fase 2 (backing real)** — fecha Q2 e prova vencedor único sob concorrência.
3. **Fase 3 (composições e fechamento)** — liga Server/Demo/testes, guards, aceite PostgreSQL e documentação.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

---

## Fase 1 - contrato atômico e composição fail-closed

**Depende de:** Q1, Q3, DF1-DF5.

**Escopo:** `RoyalIdentity/Contracts/Storage/IReplayCache.cs`, `Contracts/Defaults/DefaultReplay*.cs`,
`SecretsEvaluators/PrivateKeyJwtSecretEvaluator.cs`, `Extensions/ServiceCollectionExtensions.cs`,
`Tests.Identity` ou `Tests.Integration` para os testes negativos.

**O que/como:** substituir o contrato pela operação única, adequar o consumidor a uma só chamada e aplicar a
política de default decidida em Q1. Não introduzir persistência nesta fase.

**Tarefas:**

- [ ] Registrar Q1 e Q3 respondidas em `Histórico de decisões` e criar as DFs correspondentes.
- [ ] Substituir `IReplayCache` pela operação atômica com `CancellationToken`, conforme DF1/DF2 e a forma de Q3.
- [ ] Remover `DefaultReplayNoCache` e o registro em `ServiceCollectionExtensions.cs:63`.
- [ ] Aplicar a política de Q1 no registro de DI, com mensagem que nomeie a extensão a chamar.
- [ ] Reduzir o par das linhas 154/160 de `PrivateKeyJwtSecretEvaluator` a uma única chamada, preservando DF5.
- [ ] Adequar ou remover `DefaultReplayDistributedCache`, que não expressa add-if-absent atômico.
- [ ] Cobrir com teste: replay detectado recusa a credencial; primeira apresentação é aceita; composição sem
  backing falha conforme Q1; nenhuma mensagem contém a assertion ou o `jti` em claro.

**Critérios de aceite:** `IReplayCache` expõe uma única operação com `CancellationToken`; `AddAsync`/`ExistsAsync`
não existem; `DefaultReplayNoCache` não existe; o ramo de replay do evaluator é alcançável e coberto por teste;
uma composição sem backing real falha conforme Q1 e o teste prova isso; a solução compila e as suítes existentes
seguem verdes.

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

**Depende de:** Fase 1, Q2, DF4, DF6, DF8.

**Escopo:** conforme Q2 — `RoyalIdentity.Data.Operational` e `RoyalIdentity.Storage.EntityFramework`, ou o
cliente do backing distribuído; migrations dos providers; `Tests.Storage`.

**O que/como:** implementar add-if-absent atômico no backing escolhido, garantindo vencedor único por restrição
do próprio armazenamento, não por leitura prévia.

**Tarefas:**

- [ ] Registrar Q2 respondida e convertê-la em decisão fechada.
- [ ] Implementar o backing com inserção condicional; traduzir violação de unicidade em `false`, sem `ExistsAsync`
  prévio.
- [ ] Persistir digest conforme DF4; nunca o handle em claro.
- [ ] Criar entidade, mapeamentos e migrations por provider, se Q2 = A ou C, sem colidir com as histories
  existentes.
- [ ] Fazer falha de infraestrutura falhar fechado, nunca retornando `true`.
- [ ] Cobrir expiração: handle expirado não impede novo registro e não autoriza replay dentro do `exp`.
- [ ] Provar vencedor único com dois chamadores simultâneos, em SQLite sempre e PostgreSQL opt-in, reusando o
  formato dos testes de concorrência de authorization code.
- [ ] Integrar a limpeza de expirados ao mecanismo Operational existente, se Q2 = A ou C.

**Critérios de aceite:** duas chamadas simultâneas com o mesmo handle produzem exatamente um `true`, provado em
SQLite e no aceite PostgreSQL opt-in; nenhuma linha persiste handle em claro; falha do backing não autoriza;
handle expirado não bloqueia registro novo; migrations aplicam sem colidir histories.

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
| 2 — nenhuma composição sem proteção | 1, 3 | DF3; Q1 | composição sem backing falha; guard rejeita no-op | filtro `Replay`, `Tests.Architecture` |
| 3 — backing com vencedor único | 2 | DF4, DF6; Q2 | exatamente um `true` sob concorrência, SQLite e PostgreSQL | `Tests.Storage`, script PostgreSQL |
| 4 — cobertura do que não tinha teste | 1, 2, 3 | DF5, DF6 | replay recusado; corrida provada; fail-closed provado | filtros `Replay`/`PrivateKeyJwt` |
| 5 — registro normativo fechado | 3 | DF7 | RC-01/RC-02 sem `substituir`; item fora do backlog | revisão documental + suíte completa |

---

## Invariantes a preservar

1. Toda consulta e mutação permanece realm-scoped onde o contrato for realm-bound (Q3).
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

- Q1, Q2 e Q3 respondidas, convertidas em DFs e removidas de `Perguntas ao humano`.
- `IReplayCache` expõe somente a operação atômica, com `CancellationToken`.
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
| Fail-closed quebra composição existente | Q1=A/B e host sem backing declarado | `private_key_jwt` deixa de autenticar após upgrade | mensagem nomeando a extensão a chamar; runbook atualizado na Fase 3 | Aberto |
| Backing distribuído sem operação condicional | Q2=B sobre backing sem `SET NX` equivalente | proteção volta a ser não atômica | exigir operação condicional nativa; recusar backing que não a ofereça | Aberto |
| Exigir Operational encarece composições simples | Q2=A e host que só queria cache | dependência de storage EF para autenticar client | avaliar Q2=C; documentar a escolha por composição | Aberto |
| Digest mal separado permite colisão entre purposes | concatenação sem delimitador, como hoje | handle de um purpose bloqueia outro | delimitador explícito e separação de domínio no digest (DF4) | Aberto |
| Limpeza confundida com correção | TTL tratado como condição de segurança | expiração vira dependência de disponibilidade | correção vem do `exp` da assertion; limpeza é volume (DF8) | Aberto |
| Teste de concorrência passa por acaso | duas chamadas serializadas pelo harness | corrida não é exercitada | reusar o formato dos testes de MP-2, que já provam paralelismo real | Aberto |
| `jti` aparece em log ou exceção | mensagem de erro inclui o handle | vazamento de credencial em texto | asserção negativa nos testes de mensagem (DF5) | Aberto |

---

## Diferidos e backlog

- Cache de leitura sobre stores EF — destino: `plan-data-caching.md`, rebaixado a não necessário.
- PAR (RFC 9126) e `PersistentDataMessageStore` — destino: `an-par-rfc-9126.md` e backlog.
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
