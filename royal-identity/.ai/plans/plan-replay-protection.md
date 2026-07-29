# Plan: Proteção real contra replay de `jti` (`plan-replay-protection`)

## Status: RASCUNHO - Q1-Q5 fechadas; implementação não iniciada

## Progresso

`░░░` **0%** - 0 de 3 fases

| Fase | Estado |
|---|---|
| Fase 1 - contrato atômico, in-memory e composições declaradas | Pendente |
| Fase 2 - backing durável Operational e concorrência | Pendente |
| Fase 3 - aceites reais e fechamento | Pendente |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluída, `%` e `X de N`). Exemplo de barra: `█░░`.
> Antes de fechar uma fase, confirme que decisões, critérios de aceite, testes e invariantes relacionados foram
> aplicados.

> **Gate de planejamento:** não há decisão aberta. Qualquer decisão ausente encontrada durante a execução vira
> `Q<n>` numa seção `Perguntas ao humano` reaberta, e a fase correspondente é marcada `Bloqueada`.

---

## Contexto

### Fontes verificadas

- [plan-data-storage-matrix.md](plan-data-storage-matrix.md) — RC-01/RC-02 marcados `substituir`; alvo nomeado é
  operação atômica add-if-absent; API atual não recebe `CancellationToken` (DF23); RC-01/RC-02 hoje classificados
  como `Adapter/Infrastructure`, com a nota "não constitui registro de `Data.*`".
- [backlog-001.md](../backlogs/backlog-001.md) — item "Replay cache com proteção real (check+add atômico)",
  registrado e diferido, com destino `plan-data-caching.md` por supor backing distribuído.
- [plan-data-macro.md](plan-data-macro.md) — Plano 5 rebaixado a não necessário; o item de replay é destacado
  como requisito de segurança que não deve herdar essa condicionalidade.
- [plan-data-operational-storage.md](plan-data-operational-storage.md) — MP-2/MP-3, `protocol_artifacts`,
  DF38, DF24 (PostgreSQL opt-in, sem CI), harness de concorrência SQLite/PostgreSQL.
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
- **Consumidor único:** `IReplayCache` só é injetado em `PrivateKeyJwtSecretEvaluator`.
- **O evaluator é registrado incondicionalmente:** `ServiceCollectionExtensions.cs:73` registra
  `IClientSecretEvaluator, PrivateKeyJwtSecretEvaluator` junto dos outros quatro; sem `IReplayCache` no
  container, a resolução da cadeia falha.
- **`ValidateOnBuild` não é garantido em Production:** `WebApplication.CreateBuilder` só liga validação de
  build/escopo em Development, então a ausência de registro apareceria apenas na primeira autenticação.
- **A API não recebe `CancellationToken`:** `IReplayCache` declara `AddAsync(string, string, DateTimeOffset)` e
  `ExistsAsync(string, string)`.
- **A chave distribuída concatena sem delimitador:** `DefaultReplayDistributedCache` monta
  `Prefix + purpose + handle`.
- **`IDistributedCache` não oferece add-if-absent atômico e nunca é registrado:** não há
  `AddDistributedMemoryCache`, `AddStackExchangeRedisCache` nem `PackageReference` de caching em nenhum
  `.csproj`; `IMemoryCache`/`AddMemoryCache` também não aparecem. `DefaultReplayDistributedCache` é código morto
  por duas razões independentes.
- **A validação do Server recusa seletor de provider em configuração:**
  `ServerConfigurationServiceCollectionExtensions.cs` reprova o startup se `RoyalIdentity:DataProtection` trouxer
  a chave `Provider`.
- **Cobertura zero:** busca por `ReplayCache|PrivateKeyJwt` nos projetos `Tests.*` não retorna nenhum arquivo.
- **O contrato é global:** `purpose` e `handle` são as únicas chaves; nenhum outro store do IdP tem essa forma.
- **Replay entre realms já é impossível:** o evaluator valida com
  `ValidAudiences = [issuerUri + Oidc.Routes.BuildTokenUrl(context.Realm.Path)]` e `ValidateAudience = true`.
- **O emissor é o `client_id`:** `ValidIssuer = clientId` com `ValidateIssuer = true`, e o evaluator exige
  `sub == iss`. O namespace natural do `jti` inclui o emissor.
- **Não há limite máximo de vida da assertion:** o evaluator exige `exp` presente
  (`RequireExpirationTime = true`, `ClockSkew` de 5 minutos), mas não valida duração máxima; a expiração do
  registro de replay é `exp + 5 min`.
- **A justificativa do digest atual não é transferível:** `OperationalLookupDigest` documenta que HMAC é
  deliberadamente dispensado *porque os handles cobertos são gerados com alta entropia* — o que não vale para um
  `jti` escolhido pelo cliente.
- **A manutenção Operational tem relatórios tipados:** `OperationalCleanupReport` e `OperationalPurgeReport` em
  `RoyalIdentity.Storage.EntityFramework/Operational/Maintenance/IOperationalMaintenance.cs`, com campos por tipo
  de registro, `Total` e `Add`.

### Lacunas, conflitos e restrições

- **A API atual não permite a correção:** duas operações independentes não podem ser tornadas atômicas pelo
  caller; a substituição do contrato é pré-requisito, não preferência.
- **Falhar sem default declarado é quebra de upgrade:** por DF12 nenhuma composição sobe sem declarar o backing,
  inclusive as que nunca usam `private_key_jwt`.
- **A propriedade do dado muda:** persistir replay em Operational contraria a classificação atual da matriz e
  exige reclassificação explícita (DF16).
- **O `exp` é escolhido pelo cliente:** sem limite de duração, o registro de replay herdaria a retenção que o
  cliente decidir; DF19 fecha isso com um teto por realm.
- **Sem cobertura, qualquer regressão é silenciosa:** não existe teste que falhe se a proteção for removida.
- **Não existe infraestrutura de teste para `private_key_jwt`:** nenhum teste provisiona client com chave
  assimétrica nem monta assertion assinada.

### Superfícies impactadas a mapear

- `RoyalIdentity/Contracts/Storage/IReplayCache.cs` — contrato público a substituir e renomear.
- `RoyalIdentity/Contracts/Defaults/DefaultReplayNoCache.cs` e `DefaultReplayDistributedCache.cs` — a remover.
- `RoyalIdentity/Contracts/Defaults/SecretsEvaluators/PrivateKeyJwtSecretEvaluator.cs` — consumidor único.
- `RoyalIdentity/Extensions/ServiceCollectionExtensions.cs` — registro default e startup validator.
- `RoyalIdentity.Data.Operational` + `RoyalIdentity.Storage.EntityFramework` — backing durável e manutenção.
- `RoyalIdentity.Server`, `RoyalIdentity.Demo`, `Tests.Integration/Prepare` — composições que declaram a escolha.
- `Tests.Storage`, `Tests.Integration`, `Tests.Architecture` — concorrência, fluxo, guards.
- `plan-data-storage-matrix.md`, `backlog-001.md`, `plan-data-macro.md` — registro normativo a fechar.

---

## Objetivo

1. Substituir `AddAsync`/`ExistsAsync` por uma operação atômica de add-if-absent, com `CancellationToken`.
2. Eliminar a possibilidade de uma composição rodar `private_key_jwt` sem proteção efetiva, com falha detectada
   no startup em qualquer ambiente.
3. Isolar o registro por realm e por emissor, sem interferência entre clients nem entre realms.
4. Entregar duas implementações do mesmo contrato, com vencedor único provado sob concorrência.
5. Cobrir com teste o que hoje não tem teste: replay recusado, corrida, isolamento e falha de composição.
6. Limitar a duração aceita da client assertion, tornando a retenção um valor do servidor.
7. Fechar RC-01/RC-02 na matriz, com a reclassificação de propriedade registrada, e retirar o item do backlog.

## Fora de escopo

- Cache de leitura sobre stores EF — destino: `plan-data-caching.md`, rebaixado a não necessário.
- Pushed Authorization Requests (RFC 9126) e persistência de `IMessageStore` — destino: backlog e
  `an-par-rfc-9126.md`.
- Outros evaluators de secret (`client_secret_basic`/`post`, `tls_client_auth`) — não usam o contrato.
- API/UI administrativa para inspecionar ou limpar handles registrados.
- Redesenho da validação de `private_key_jwt` além da troca da chamada de replay e do teto de duração de DF19.

---

## Decisões fechadas

- **DF1 — Operação única substitui check+add:** o contrato passa a expor uma operação atômica add-if-absent que
  informa se o handle já existia. `AddAsync`/`ExistsAsync` são removidos. Fonte: matriz RC-01/RC-02.
- **DF2 — `CancellationToken` obrigatório:** a nova operação recebe `CancellationToken`. Fonte: matriz DF23.
- **DF3 — Nenhum no-op silencioso permanece:** nenhuma composição obtém proteção aparente com efeito nulo.
  Fonte: precedentes DF10/DF11 do Plano 3.
- **DF4 — Handle bruto não é persistido:** grava-se digest do handle, nunca o `jti` em claro. A separação entre
  campos é feita no **cálculo do digest**, com codificação inequívoca, não por concatenação de colunas.
  Fonte: DF38 do Plano 3, adaptada por DF17.
- **DF5 — Semântica de recusa preservada:** replay detectado continua produzindo credencial inválida e log sem
  expor a assertion. Fonte: comportamento atual do evaluator.
- **DF6 — Prova de concorrência obrigatória:** vencedor único com dois chamadores simultâneos é aceite das duas
  implementações; a durável em SQLite sempre e em PostgreSQL opt-in. Fonte: Plano 3.
- **DF7 — Sem CI:** este plano não cria pipeline; PostgreSQL permanece aceite local/opt-in. Fonte: DF24 do
  Plano 4.
- **DF8 — Enquanto o registro estiver retido, conflito é replay:** a operação não consulta a expiração do
  registro existente — qualquer conflito de chave responde replay. A limpeza só remove um registro depois que a
  assertion original já não pode ser aceita; depois disso, o mesmo `jti` pode ser inserido de novo, sem perda de
  proteção. Não há comparação de expiração no caminho de escrita, portanto não há corrida entre expirar e
  inserir, e a correção não depende da limpeza. Recusar um `jti` ainda retido é o comportamento correto:
  RFC 7519 §4.1.7 exige que o valor seja atribuído de modo a tornar colisão desprezível, e OpenID Connect Core §9
  exige `jti` na client assertion e uso único do token. RFC 7523 §3 apenas autoriza o servidor a reter os `jti`
  pelo tempo em que o JWT seria válido — sustenta a retenção, não uma proibição permanente. Fonte: revisão
  externa 2026-07-29; alternativa de upsert condicional avaliada e descartada em `Histórico de decisões`.
- **DF9 — Seleção por extension, não por configuração:** a composition root escolhe o backing chamando uma
  extension dedicada; a configuração fornece apenas parâmetros. Fonte: resposta humana a Q2; idioma de cleanup,
  proteção de payload e protector de signing keys; validação do Server que recusa `Provider` em configuração.
- **DF10 — Duas implementações neste plano:** uma in-memory por instância e uma durável sobre a família
  Operational. A in-memory segue o tratamento do Plain da DF11 do Plano 3: registro explícito, warning e nunca
  default; válida somente em instância única. Fonte: resposta humana a Q2.
- **DF11 — Redis e demais backings distribuídos ficam fora:** entram como extension adicional sobre o mesmo
  contrato quando existir deployment que precise. Nenhum pacote de cache distribuído entra no grafo por este
  plano. Fonte: resposta humana a Q2 e DF17 do Plano 4.
- **DF12 — Sem registro default; a composition root declara:** `AddOpenIdConnectProviderServices()` não registra
  o contrato. Fonte: resposta humana a Q1.
- **DF13 — Chave por realm e emissor:** a identidade do registro é `(realmId, issuer, purpose, handleDigest)`.
  RFC 7519 §4.1.7 exige que o `jti` seja atribuído de modo a tornar colisão desprezível, inclusive entre
  emissores distintos, e OpenID Connect Core §9 exige uso único da client assertion; o evaluator já valida
  `iss = client_id`. Assim, um client não pode bloquear o `jti` de outro, nem um realm o de outro. Fonte: resposta humana a Q3, estendida pela revisão
  externa 2026-07-29.
- **DF14 — Exatamente uma estratégia, verificada no startup:** as extensions registram um marker da estratégia
  escolhida e um startup validator recusa a subida, em qualquer ambiente, em cinco casos: nenhuma estratégia;
  mais de uma; marker sem store correspondente; store sem marker; marker e implementação incompatíveis. A ordem
  de registro nunca decide silenciosamente entre in-memory e Operational. A mensagem nomeia
  `AddInMemoryReplayProtection()` e `AddOperationalReplayProtection()`. Fonte: revisão externa;
  `ValidateOnBuild` não é garantido em Production; precedentes `SigningKeyStartupValidator`,
  `OperationalPayloadProfilesStartupValidator` e a recusa de seleção dupla em
  `AddEntityFrameworkOperationalCleanup`.
- **DF15 — Toda fase termina verde:** nenhuma fase entrega um estado em que o contrato esteja trocado sem
  implementação registrada nas composições. Build e suítes verdes são aceite de cada fase, não só do plano.
  Fonte: revisão externa.
- **DF16 — Reclassificação de RC-01/RC-02:** replay deixa de ser `Adapter/Infrastructure` e passa a ser dado
  **Operational** realm-scoped e efêmero, com entidade própria em `RoyalIdentity.Data.Operational`. A matriz é
  atualizada como reclassificação consciente. Fonte: revisão externa.
- **DF17 — Digest é pseudonimização, não confidencialidade:** o `jti` não é segredo — não autoriza nada sozinho,
  e a assertion é autenticada por assinatura. O digest evita persistir o valor literal e dá separação de domínio;
  não se assume resistência a enumeração por dicionário sobre `jti` previsível. A justificativa de alta entropia
  do `OperationalLookupDigest` **não** é herdada. Se confidencialidade perante acesso ao banco entrar no threat
  model, a troca por digest autenticado é alteração isolada de implementação. Fonte: revisão externa.
- **DF18 — Renomear o contrato:** `IReplayCache` passa a `IReplayProtectionStore`. O nome atual descreve cache
  opcional e foi o que manteve este item arquivado junto de um plano de performance. Como a quebra pública já
  ocorre, o rename é custo marginal. Fonte: revisão externa.
- **DF19 — Duração máxima aceita da client assertion:** o evaluator recusa assertion cujo `exp` ultrapasse
  `now + ClientAssertionMaxLifetime`, com `now` vindo de `TimeProvider` e a tolerância de `ClockSkew` já aplicada
  na validação. O teto é uma option por realm. A comparação é contra o instante do servidor, e não contra
  `exp - iat`: `iat` é opcional e pode vir adiantado, enquanto o que precisa ser limitado é exatamente a retenção
  do registro, que é função de `exp` e do relógio do servidor. Recusa segue DF5. Fonte: resposta humana a Q4;
  revisão externa.
- **DF20 — Poda periódica do backing in-memory:** a implementação in-memory remove registros expirados por poda
  periódica, fora do caminho de decisão de `TryAddAsync`. A poda não é necessária para impedir replay da assertion
  original — ela só evita crescimento indefinido em processo longevo. Depois da poda, o mesmo `jti` numa nova
  assertion pode ser aceito, que é o mesmo comportamento da limpeza do backing durável. Critérios mínimos:
  remove quando `ExpiresAtUtc <= now`; o timer é criado por `TimeProvider`; o timer é encerrado no descarte da
  store; execuções não se sobrepõem; o comportamento é provado com relógio controlado, de forma determinística; e
  há teste provando que `TryAddAsync` **não** consulta expiração — um registro vencido e ainda não podado
  continua respondendo replay. Fonte: revisão externa.
- **DF21 — Valor e faixa do teto:** default de **10 minutos**; faixa configurável por realm de `> 0` até
  `<= 1 hora`. Dez minutos é o piso coerente com o `ClockSkew` de 5 minutos já aplicado na validação: um client
  que emita assertion de 5 minutos com relógio 5 minutos adiantado produz `exp = now + 10min` no relógio do
  servidor e continua aceito. Um teto de 5 minutos comparado contra o relógio do servidor contradiria a própria
  tolerância. Uma hora fica reservada como override máximo para integração legada, não como default, porque
  amplia sem necessidade a janela de uso de uma assertion vazada. A option valida faixa e protege
  `now + lifetime` contra overflow de `DateTimeOffset`. Se o `ClockSkew` deixar de ser constante, os dois passam
  a ser validados juntos: o teto nunca pode ser menor que a tolerância. Fonte: resposta humana a Q5.

---

## Histórico de decisões

**Fase 2 (backing real):**

- **Q2 — Onde vive o armazenamento de handles:** Operational EF; `IDistributedCache` com operação condicional
  nativa; ou ambos.
  - **Considerações:** `IDistributedCache` não expressa add-if-absent e não está registrado em lugar nenhum do
    repositório, nem há pacote de caching referenciado. A família Operational já tem unique constraint, vencedor
    único provado, migrations por provider, runner e limpeza por TTL.
  - **Alternativa avaliada e descartada — seletor por `IConfiguration`:** configuração não cria dependência; a
    união de todos os pacotes entraria no binário de todas as composições, cada opção exigiria validação e prova
    próprias, e o Server hoje recusa seletor de provider em configuração.
  - **Conclusão Q2:** fechada por DF9/DF10/DF11.

**Fase 1 (contrato e composição):**

- **Q1 — Comportamento quando a composition root não declara backing:** falhar ao resolver ou falhar só no uso.
  - **Considerações:** falhar só no uso transforma erro de configuração em `invalid_client` por request,
    indistinguível de falha legítima de credencial. Um default que resolve sozinho foi o que permitiu o no-op
    passar despercebido.
  - **Conclusão Q1:** fechada por DF12, complementada por DF14 depois que a revisão externa mostrou que a
    ausência de registro não falha no startup em Production.

- **Q3 — Escopo de realm no contrato:** incluir `realmId` ou manter global.
  - **Considerações:** a validação de audience já impede replay entre realms, então a chave global não acrescenta
    proteção e acrescenta falso positivo e oráculo fraco.
  - **Conclusão Q3:** fechada por DF13, estendida ao emissor pela revisão externa — o mesmo argumento vale entre
    clients do mesmo realm, já que o `jti` é único por emissor.

**Fase 1 (retenção):**

- **Q4 — Retenção causada pelo `exp` remoto:** limitar a duração aceita da assertion ou aceitar a retenção e
  registrar o risco.
  - **Considerações:** reduzir a retenção abaixo do `exp` abriria janela de replay, então a única forma de
    limitar volume sem abrir buraco é limitar o que se aceita. O teto também reduz a janela de reuso de uma
    assertion vazada, que hoje é a que o cliente escolher.
  - **Conclusão Q4:** fechada por DF19 — teto por realm, comparado contra o instante do servidor.

**Revisão externa (2026-07-29):**

- **Expiração versus unicidade:** apontado que uma linha expirada continua ocupando a chave única, contradizendo
  "registro expirado não impede novo registro".
  - **Alternativa proposta e descartada — upsert condicional** (`inserir; em conflito, substituir se
    `ExpiresAtUtc <= now`): resolve a contradição, mas coloca uma comparação de tempo no caminho de escrita, cria
    a necessidade de compare/update atômico também no in-memory e torna a correção dependente de relógio.
  - **Conclusão:** fechada por DF8 no sentido oposto — enquanto o registro estiver retido, o conflito responde
    replay, e a afirmação de que registro expirado não bloqueia foi **removida** do plano. É mais simples, mais
    estrito e mantém a correção independente de limpeza e de relógio.

**Fase 1 (teto de duração):**

- **Q5 — Valor default de `ClientAssertionMaxLifetime`:** 1 hora, 5-10 minutos, ou outro valor.
  - **Resposta:** opção B com default exato de **10 minutos**, faixa `> 0` a `<= 1 hora`, e documentação
    orientando clientes a emitirem assertions de 1 a 5 minutos.
  - **Considerações:** 10 minutos é o piso coerente com o `ClockSkew` de 5 minutos já aplicado — um client com
    assertion de 5 minutos e relógio 5 minutos adiantado produz `exp = now + 10min` no servidor e continua
    aceito, enquanto um teto de 5 minutos contradiria a própria tolerância. Uma hora como default ampliaria sem
    necessidade a janela de uso de uma assertion vazada; fica como override máximo para integração legada.
  - **Conclusão Q5:** fechada por DF21.

**Segunda revisão externa (2026-07-29):**

- **Atribuição normativa da DF8:** apontado que a RFC 7523 §3 apenas autoriza reter `jti` pelo tempo de validade
  do JWT, não exige unicidade por emissor. Verificado e corrigido: a exigência de atribuição sem colisão está na
  RFC 7519 §4.1.7 e o uso único da client assertion em OpenID Connect Core §9. A redação de DF8 passou de
  "conflito é sempre replay" para "enquanto o registro estiver retido", porque depois da limpeza o mesmo `jti`
  pode ser inserido de novo.
- **Expiração do in-memory indefinida:** apontado que "expiração respeitada" não se sustenta se DF8 proíbe
  consultar expiração na inserção e não há manutenção definida. Fechado por DF20, com poda periódica fora do
  caminho de decisão; a alternativa de reter até o descarte da instância foi descartada por contradizer a
  apresentação da in-memory como válida em qualquer host de instância única.
- **Default de uma hora:** apontado que é política de produto, não consequência de especificação. Reaberto como
  Q5, junto da exigência de faixa válida e proteção contra overflow em `now + lifetime`.
- **Marker sem exclusividade:** apontado que DF14 não cobria duas estratégias declaradas. Estendida para cinco
  casos de recusa.
- **Sequência 1→2 não liberável:** aceito como restrição declarada de rollout em vez de fusão das fases; o estado
  intermediário é estritamente melhor que o atual, mas não é o alvo.

---

## Design alvo

### Contratos e bordas

```csharp
public interface IReplayProtectionStore
{
    /// true  = registrado agora; o chamador pode prosseguir.
    /// false = já existia; é replay.
    Task<bool> TryAddAsync(
        string realmId,
        string issuer,
        string purpose,
        string handle,
        DateTimeOffset expiration,
        CancellationToken ct);
}
```

- `PrivateKeyJwtSecretEvaluator`: uma única chamada substitui o par das linhas 154/160, passando
  `context.Realm.Id` e o `client_id` já validado como issuer.
- Seleção do backing (DF9), sem chave de configuração que escolha implementação:

```csharp
services.AddInMemoryReplayProtection();     // instância única; warning explícito; nunca default (DF10)
services.AddOperationalReplayProtection();  // durável sobre a família Operational
```

- Cada extension registra também um marker da estratégia; o startup validator de DF14 exige marker e contrato.
- `DefaultReplayNoCache` e `DefaultReplayDistributedCache` são removidas.

### Modelo, dados e persistência

```text
operation.replay_handles          (nome final a definir na Fase 2)
  RealmId            text      not null
  Issuer             text      not null
  Purpose            text      not null
  HandleDigest       text      not null   -- digest de (versão + domínio + jti); realm/issuer/purpose ficam fora
  ExpiresAtUtc       timestamp not null
  unique (RealmId, Issuer, Purpose, HandleDigest)
  index (ExpiresAtUtc)                    -- limpeza por TTL
```

Vencedor único vem da unique constraint: a segunda inserção viola e é traduzida em `false`, sem leitura prévia e
sem comparação de expiração (DF8). A in-memory obtém a mesma semântica com uma única operação atômica de
dicionário concorrente, sem `TryRemove` seguido de `TryAdd`.

### Arquitetura alvo

```text
RoyalIdentity/                          contrato + consumidor + implementação in-memory + startup validator
RoyalIdentity.Data.Operational/         entidade de replay (DF16)
RoyalIdentity.Storage.EntityFramework/  implementação durável, manutenção e extension de registro
RoyalIdentity.Server /                  declara a durável
RoyalIdentity.Demo /                    declara a in-memory, coerente com ser efêmero
Tests.Integration/Prepare/              declara explicitamente, conforme o cenário

-X-> nenhum pacote de cache distribuído entra no grafo por este plano (DF11)
```

### Segurança, concorrência e confiabilidade

- Dois chamadores simultâneos com o mesmo `(realm, issuer, purpose, handle)` produzem exatamente um `true`.
- Falha do backing não é traduzida em `true`: erro de infraestrutura falha fechado, nunca autoriza.
- Nenhum log, mensagem ou exceção expõe a assertion, o `jti` em claro ou connection string.
- A expiração do registro cobre pelo menos o `exp` da assertion mais o `ClockSkew` aplicado na validação; nunca
  menos, para não abrir janela entre a expiração do registro e a da assertion.
- O `exp` aceito é limitado por `ClientAssertionMaxLifetime` (DF19), o que torna a retenção máxima um valor do
  servidor, não do cliente.
- Handles de realms distintos, ou de clients distintos no mesmo realm, não interferem.
- Todo tempo vem de `TimeProvider`, nunca de `DateTimeOffset.UtcNow` direto.

### Compatibilidade, migração e rollout

- Quebra pública em corte único por fase: contrato, implementação e registros das composições andam juntos
  (DF15). Nenhuma fase entrega o contrato trocado sem backing declarado.
- Não há dado a migrar: handles são efêmeros.
- Composições existentes precisam declarar a escolha (DF12/DF14); a ausência é falha explícita no startup.
- **Fases 1 e 2 formam uma sequência não liberável.** Entre elas o Server declara a in-memory, então um deploy
  multi-instância teria proteção por processo, não compartilhada. É estritamente melhor que o estado atual, em que
  não há proteção alguma, mas não é o alvo: não publicar o Server entre as duas fases.

---

## Ordem de execução

1. **Fase 1 (contrato, in-memory e composições)** — entrega proteção efetiva e mantém tudo verde.
2. **Fase 2 (backing durável)** — acrescenta a implementação Operational e a prova de concorrência.
3. **Fase 3 (aceites e fechamento)** — guards por composição, aceite PostgreSQL real e registro normativo.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

---

## Fase 1 - contrato atômico, in-memory e composições declaradas

**Depende de:** DF1-DF5, DF8-DF10, DF12-DF15, DF17-DF21.

**Escopo:** `RoyalIdentity/Contracts/Storage`, `Contracts/Defaults`, `SecretsEvaluators`,
`Extensions/ServiceCollectionExtensions.cs`, `RoyalIdentity.Server`, `RoyalIdentity.Demo`,
`Tests.Integration/Prepare`, `Tests.Identity`/`Tests.Integration`.

**O que/como:** substituir o contrato, entregar a implementação in-memory, trocar o consumidor, declarar a escolha
em todas as composition roots e instalar o startup validator — tudo no mesmo corte, terminando verde (DF15).

**Tarefas:**

- [ ] Criar `IReplayProtectionStore` com a operação atômica realm/issuer-bound e `CancellationToken`
  (DF1/DF2/DF13/DF18); remover `IReplayCache`.
- [ ] Implementar a in-memory por instância com **uma** operação atômica de dicionário, sem remove-then-add e
  sem consultar expiração na decisão, com `TimeProvider` injetado.
- [ ] Implementar a poda periódica do in-memory conforme DF20: remoção em `ExpiresAtUtc <= now`, timer criado por
  `TimeProvider`, timer encerrado no descarte da store e sem execuções sobrepostas; documentar no tipo que ela é
  higiene de memória, não condição de proteção.
- [ ] Criar `AddInMemoryReplayProtection()` com warning explícito de validade em instância única (DF10).
- [ ] Registrar o marker de estratégia nas extensions e implementar o startup validator de DF14, cobrindo os
  cinco casos de recusa — nenhuma estratégia, mais de uma, marker sem store, store sem marker e par
  incompatível — com mensagem nomeando as duas extensions.
- [ ] Remover `DefaultReplayNoCache`, `DefaultReplayDistributedCache` e o registro em
  `ServiceCollectionExtensions.cs:63`, sem substituí-los por outro default (DF12).
- [ ] Reduzir o par das linhas 154/160 do evaluator a uma única chamada, passando `context.Realm.Id` e o
  `client_id` validado; preservar DF5.
- [ ] Acrescentar `ClientAssertionMaxLifetime` a `RealmOptions` com default de **10 minutos** e faixa válida
  `> 0` até `<= 1 hora` (DF21), cópia no construtor de cópia e aritmética protegida contra overflow em
  `now + lifetime`, seguindo o padrão das demais options por realm.
- [ ] Recusar no evaluator assertion cujo `exp` ultrapasse `now + ClientAssertionMaxLifetime`, com `now` de
  `TimeProvider`, antes de tocar o replay store (DF19).
- [ ] Declarar a escolha em `RoyalIdentity.Server`, `RoyalIdentity.Demo` e na factory persistente de
  `Tests.Integration` (Fase 1 usa in-memory em todas; o Server troca para a durável na Fase 2).
- [ ] Criar a infraestrutura de teste de `private_key_jwt`: client com chave assimétrica, assertion assinada com
  `iss`/`sub`/`aud`/`exp`/`jti` corretos, reutilizável pelas Fases 2 e 3.
- [ ] Cobrir com teste: primeira apresentação aceita; segunda recusada; mesmo `jti` em clients distintos do mesmo
  realm não interfere; mesmo `jti` em realms distintos não interfere; assertion com `exp` além do teto é recusada
  e **não** registra handle; assertion dentro do teto é aceita; assertion de 5 minutos emitida com relógio 5
  minutos adiantado continua aceita sob o default de 10 minutos; a option recusa zero, negativo e valor acima de
  1 hora, e aceita os dois limites válidos; a poda do in-memory remove vencidos com relógio controlado e um
  registro vencido ainda não podado continua respondendo replay; duas estratégias declaradas simultaneamente
  falham o startup; host em ambiente **Production** sem declaração
  falha no startup com mensagem citando as extensions; nenhuma mensagem contém a assertion ou o `jti`.

**Critérios de aceite:** `IReplayCache` não existe; `IReplayProtectionStore` expõe somente a operação atômica
realm/issuer-bound com `CancellationToken`; nenhum default é registrado e nenhum no-op existe; o host em
Production sem declaração falha antes de aceitar tráfego, provado por teste; replay é recusado e os dois
isolamentos são provados; assertion acima de `ClientAssertionMaxLifetime` é recusada sem registrar handle;
`dotnet build RoyalIdentity.sln` e todas as suítes seguem verdes (DF15).

**Testes:**

```powershell
dotnet build RoyalIdentity.sln
dotnet test Tests.Identity
dotnet test Tests.Integration --filter "FullyQualifiedName~PrivateKeyJwt|FullyQualifiedName~ReplayProtection"
dotnet test RoyalIdentity.sln
```

### Resultado da Fase 1

*a preencher*

---

## Fase 2 - backing durável Operational e concorrência

**Depende de:** Fase 1, DF4, DF6, DF8, DF13, DF15-DF17.

**Escopo:** `RoyalIdentity.Data.Operational`, `RoyalIdentity.Storage.EntityFramework` (stores, manutenção,
extensions), migrations dos providers, `RoyalIdentity.Server`, `Tests.Storage`.

**O que/como:** entregar a implementação durável sobre Operational, integrá-la à manutenção existente e apontar
o Server para ela, mantendo tudo verde.

**Tarefas:**

- [ ] Criar a entidade de replay em `RoyalIdentity.Data.Operational` com a unique de DF13 e índice de expiração.
- [ ] Implementar o digest sobre `versão + domínio + handle` apenas — `RealmId`, `Issuer` e `Purpose` permanecem
  colunas próprias e não entram no digest (DF4/DF17) —, documentando no tipo por que a justificativa de alta
  entropia do `OperationalLookupDigest` não se aplica.
- [ ] Implementar a store durável com inserção protegida por unique constraint — não upsert; traduzir violação de
  unicidade em `false`, sem consulta prévia e sem comparar expiração (DF8).
- [ ] Criar mapeamentos e migrations por provider, sem colidir com as histories existentes.
- [ ] Criar `AddOperationalReplayProtection()` com o marker de estratégia de DF14.
- [ ] Fazer falha de infraestrutura falhar fechado, nunca retornando `true`.
- [ ] Estender `EntityFrameworkOperationalMaintenance`: limpeza em lotes por expiração e purge por realm.
- [ ] Acrescentar o novo contador a `OperationalCleanupReport` e a `OperationalPurgeReport`, incluindo `Total` e
  `Add`, e atualizar os testes que fixam o shape desses relatórios.
- [ ] Cobrir o limite exato de expiração na limpeza, coerente com a semântica já usada pelos demais tipos.
- [ ] Provar vencedor único com dois chamadores simultâneos na implementação durável, em SQLite sempre e em
  PostgreSQL opt-in, reusando o formato dos testes de concorrência de authorization code.
- [ ] Trocar a declaração do `RoyalIdentity.Server` para a durável, mantendo Demo e testes na in-memory.
- [ ] Não adicionar `PackageReference` de cache distribuído a nenhum projeto (DF11).

**Critérios de aceite:** duas chamadas simultâneas com a mesma identidade produzem exatamente um `true`, provado
em SQLite e no aceite PostgreSQL opt-in; nenhuma linha persiste `jti` em claro; falha do backing não autoriza;
conflito responde replay sem consultar expiração; limpeza e purge contabilizam o novo tipo nos relatórios
tipados; migrations aplicam sem colidir histories; `dotnet test RoyalIdentity.sln` verde (DF15).

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~ReplayProtection|FullyQualifiedName~Maintenance"
./scripts/Test-OperationalPostgreSql.ps1
dotnet test RoyalIdentity.sln
```

### Resultado da Fase 2

*a preencher*

---

## Fase 3 - aceites reais e fechamento

**Depende de:** Fases 1-2, DF6, DF7, DF14, DF16.

**Escopo:** `Tests.Architecture`, `Tests.Integration`, `Tests.Storage`, scripts PostgreSQL,
`plan-data-storage-matrix.md`, `backlog-001.md`, `plan-data-macro.md`, READMEs do Server e do Demo.

**O que/como:** provar por guard qual implementação cada composição resolve, exercitar replay contra PostgreSQL
real e fechar o registro normativo.

**Tarefas:**

- [ ] Adicionar guard provando que o Server resolve **a implementação Operational**, e não apenas "algo que não é
  no-op".
- [ ] Adicionar guard provando que o Demo resolve **a implementação in-memory**.
- [ ] Adicionar guard que rejeite reintrodução de qualquer implementação no-op do contrato.
- [ ] Criar aceite PostgreSQL opt-in que apresenta a mesma assertion duas vezes contra o backing durável real e
  exige aceite seguido de recusa.
- [ ] Estender `scripts/Test-ServerPostgreSql.ps1` para exercitar `private_key_jwt`, ou criar script próprio, e
  registrar comando, contagens e ausência de containers residuais.
- [ ] Atualizar RC-01/RC-02 na matriz: contrato final, remoção da marcação `substituir` e registro da
  reclassificação de `Adapter/Infrastructure` para Operational (DF16).
- [ ] Remover o item de replay do `backlog-001.md` e a menção condicionada no `plan-data-macro.md`.
- [ ] Atualizar READMEs do Server e do Demo com a extension declarada e, no Demo, a limitação de instância única.
- [ ] Documentar `ClientAssertionMaxLifetime`: default de 10 minutos, faixa até 1 hora como override para
  integração legada, e orientação para clientes emitirem assertions de 1 a 5 minutos.
- [ ] Executar `dotnet build` e `dotnet test` da solução completa.

**Critérios de aceite:** guards distinguem qual implementação cada composição resolve, e não apenas a ausência de
no-op; o aceite PostgreSQL apresenta a mesma assertion duas vezes contra o backing real, com aceite e recusa; a
matriz registra contrato final e reclassificação; backlog e macro-plano refletem o estado final; solução completa
verde.

**Testes:**

```powershell
dotnet build RoyalIdentity.sln
dotnet test Tests.Architecture
./scripts/Test-ServerPostgreSql.ps1
dotnet test RoyalIdentity.sln
```

### Resultado da Fase 3

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(es) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| 1 — operação atômica com CT | 1 | DF1, DF2, DF18 | contrato único; `IReplayCache` inexistente | `dotnet build`, `Tests.Identity` |
| 2 — falha real no startup | 1, 3 | DF3, DF12, DF14 | host Production sem declaração não sobe; guards por composição | filtro `ReplayProtection`, `Tests.Architecture` |
| 3 — isolamento por realm e emissor | 1, 2 | DF13 | mesmo `jti` em clients/realms distintos não interfere | filtros `PrivateKeyJwt`/`ReplayProtection` |
| 4 — duas implementações com vencedor único | 1, 2 | DF6, DF8, DF10 | exatamente um `true` sob concorrência, SQLite e PostgreSQL | `Tests.Storage`, script PostgreSQL |
| 5 — cobertura do que não tinha teste | 1, 2, 3 | DF5, DF6 | replay recusado; corrida; fail-closed; aceite PostgreSQL com replay real | filtros + scripts |
| 6 — registro normativo fechado | 3 | DF7, DF16 | RC-01/RC-02 sem `substituir` e reclassificados | revisão documental + suíte completa |

---

## Invariantes a preservar

1. Toda consulta e mutação do replay store permanece realm-scoped e issuer-scoped (DF13).
2. `RoyalIdentity` não referencia providers, Server, Demo ou projetos `Data.*`.
3. `Data.*` permanece puro e só é adaptado por `RoyalIdentity.Storage.EntityFramework`.
4. Falha de infraestrutura nunca é traduzida em autorização.
5. `jti` não é persistido nem registrado em log em claro.
6. A expiração do registro nunca é menor que a da assertion mais o `ClockSkew` aplicado.
7. A retenção máxima é determinada pelo servidor, nunca pelo `exp` escolhido pelo cliente (DF19).
8. A correção não depende de limpeza nem de comparação de relógio no caminho de escrita (DF8).
9. O processo web não aplica migration nem seed; provisionamento continua externo.
10. Nenhuma composição obtém proteção aparente com efeito nulo.
11. Toda fase termina com build e suítes verdes (DF15).
12. Nenhuma semântica fechada na matriz é reaberta além de RC-01/RC-02.

---

## Critérios globais de conclusão

- Nenhuma decisão aberta: Q1-Q5 fechadas por DF9-DF21.
- `IReplayProtectionStore` expõe somente a operação atômica realm/issuer-bound com `CancellationToken`.
- Nenhuma implementação no-op existe no repositório e nenhum default é registrado.
- Host em Production sem declaração falha antes de aceitar tráfego, provado por teste.
- Vencedor único provado nas duas implementações; a durável em SQLite e no aceite PostgreSQL opt-in.
- Assertion acima do teto de duração é recusada sem registrar handle, e a faixa da option é validada nos limites.
- Aceite PostgreSQL apresenta a mesma assertion duas vezes contra o backing real.
- Guards provam a implementação específica resolvida por Server e por Demo.
- RC-01/RC-02 atualizados e reclassificados; item removido do backlog e da órbita do plano de caching.
- `dotnet build RoyalIdentity.sln` verde.
- `dotnet test RoyalIdentity.sln` verde.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| Host deixa de subir após upgrade | DF12/DF14 e composição que ainda não declara | qualquer host para, inclusive quem nunca usou `private_key_jwt` | mensagem nomeando as extensions; Fase 1 já atualiza Server, Demo e fixtures | Aberto |
| In-memory usada em cluster | operador escolhe a extension in-memory em host replicado | replay atravessa instâncias e a proteção some | warning no registro, README e nome da extension declarando a limitação (DF10); guard prova que o Server usa a durável | Aberto |
| Fase intermediária deixa o produto quebrado | contrato trocado sem backing declarado | suítes vermelhas e host sem subir entre cortes | DF15: contrato, implementação e registros no mesmo corte | Mitigado |
| Linha expirada bloqueia registro legítimo | cliente reusa `jti` após o `exp` | autenticação recusada até a limpeza passar | DF8: reuso de `jti` é violação do cliente; recusar é o comportamento correto | Aceito |
| Teto de assertion recusa cliente legítimo | client emite assertion com `exp` acima de 10 minutos à frente do relógio do servidor | autenticação para após o upgrade, sem mudança do lado do cliente | DF21 mantém o teto como option por realm, com override até 1 hora para integração legada; README orienta assertions de 1 a 5 minutos | Aberto |
| Server publicado entre as Fases 1 e 2 | deploy multi-instância com a in-memory declarada | proteção por processo, não compartilhada entre réplicas | sequência declarada não liberável; Fase 2 troca o Server para a durável | Aberto |
| In-memory cresce indefinidamente | host longevo com a in-memory declarada e sem poda | consumo de memória proporcional ao volume de autenticações | DF20: poda periódica por `TimeProvider`, fora do caminho de decisão | Mitigado |
| Digest tratado como confidencialidade | `jti` previsível e acesso ao banco no threat model | enumeração por dicionário sobre os digests | DF17 declara o escopo; troca por digest autenticado é alteração isolada | Aceito |
| Guard aceita implementação errada | guard só rejeita no-op | in-memory registrada no Server passa despercebida | Fase 3 exige guard por implementação específica | Mitigado |
| Teste de concorrência passa por acaso | duas chamadas serializadas pelo harness | corrida não é exercitada | reusar o formato dos testes de MP-2, que já provam paralelismo real | Aberto |
| `jti` aparece em log ou exceção | mensagem de erro inclui o handle | vazamento de valor de credencial em texto | asserção negativa nos testes de mensagem (DF5) | Aberto |

---

## Diferidos e backlog

- Backing distribuído (Redis ou equivalente) — destino: extension adicional sobre o mesmo contrato, quando
  existir deployment que precise (DF11). Exige operação condicional nativa; `IDistributedCache` não serve.
- Digest autenticado/HMAC para o replay store — destino: alteração isolada, se confidencialidade perante acesso
  ao banco entrar no threat model (DF17).
- Inspeção/limpeza administrativa de handles registrados — destino: roadmap administrativo.
- Aplicação de proteção contra replay a outros artefatos de uso único — destino: avaliação futura.

---

## Referências

- [plan-data-storage-matrix.md](plan-data-storage-matrix.md) — RC-01/RC-02 e classificação atual.
- [plan-data-operational-storage.md](plan-data-operational-storage.md) — MP-2, DF38, DF24, harness de concorrência.
- [plan-data-macro.md](plan-data-macro.md) — Plano 5 e destaque do item de replay.
- [backlog-001.md](../backlogs/backlog-001.md) — item original.
- [ADR-013](../../adrs/ADR-013.md).
- `RoyalIdentity/Contracts/Storage/IReplayCache.cs`.
- `RoyalIdentity/Contracts/Defaults/DefaultReplayNoCache.cs` e `DefaultReplayDistributedCache.cs`.
- `RoyalIdentity/Contracts/Defaults/SecretsEvaluators/PrivateKeyJwtSecretEvaluator.cs`.
- `RoyalIdentity/Extensions/ServiceCollectionExtensions.cs`.
- `RoyalIdentity.Storage.EntityFramework/Operational/Maintenance/IOperationalMaintenance.cs`.
- `RoyalIdentity.Storage.EntityFramework/Operational/Materialization/OperationalLookupDigest.cs`.
