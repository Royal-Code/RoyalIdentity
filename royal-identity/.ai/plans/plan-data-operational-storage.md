# Plan: Persistência EF dos dados operacionais do IdP (`plan-data-operational-storage`)

## Status: RASCUNHO - aguardando as questões ainda abertas de Q1-Q12

## Progresso

`░░░░░░░░` **0%** - 0 de 8 fases

| Fase | Estado |
|---|---|
| Fase 1 - contratos, fronteiras e modelo Operational | Bloqueada |
| Fase 2 - access tokens e consents sobre SQLite | Bloqueada |
| Fase 3 - sessões SSO sobre SQLite | Bloqueada |
| Fase 4 - authorization codes e consumo atômico | Bloqueada |
| Fase 5 - refresh tokens e transições condicionais | Bloqueada |
| Fase 6 - authorize parameters, cleanup e purge de realm | Bloqueada |
| Fase 7 - PostgreSQL, migrations, runner e gateway EF completo | Bloqueada |
| Fase 8 - paridade, fluxos e fechamento | Bloqueada |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluída, `%` e `X de N`). Exemplo de barra: `████░░░░`.
> Antes de fechar uma fase, confirme que decisões, critérios de aceite, testes e invariantes relacionados foram
> aplicados.

> **Bloqueio atual:** as Fases 1–8 não devem ser iniciadas antes de Q1–Q12 serem respondidas e convertidas em
> decisões fechadas. A matriz do baseline é normativa para as semânticas já resolvidas; este plano não as
> reinfere nem as altera implicitamente.

---

## Contexto

### Fontes verificadas

- [template-ai-implementation-plan.md](../references/template-plan/template-ai-implementation-plan.md) — shape e
  regras deste plano.
- [plan-data-macro.md](plan-data-macro.md) — o Plano 3 persiste a família Operational depois de Configuration e
  antes da troca do backing dos testes.
- [plan-data-storage-baseline.md](plan-data-storage-baseline.md) — decisões DF1–DF25, fronteiras e gates dos
  Planos 2/3/4.
- [plan-data-storage-matrix.md](plan-data-storage-matrix.md) — catálogo normativo das operações AT/RT/AC/CN/SS/AP,
  mudanças MP-2/MP-3/MP-5/MP-6/MP-7 e ordem de implementação por store.
- [plan-data-configuration-storage.md](plan-data-configuration-storage.md) — implementação concluída da família
  Configuration, mappings extensíveis, providers, runner e restrições de composição herdadas.
- [ADR-013](../../adrs/ADR-013.md) — `Data.*` puro, contratos no core e adapter único em
  `RoyalIdentity.Storage.EntityFramework`.
- [ADR-014](../../adrs/ADR-014.md) e [ADR-017](../../adrs/ADR-017.md) — ownership da sessão SSO no core,
  expiração, idle touch, `SecurityStamp` e revogação por subject.
- [ADR-018](../../adrs/ADR-018.md) — o fake in-memory é transitório e não deve receber nova paridade de produção.
- [architecture.md](../foundation/architecture.md) — `Data.*` não usa Feature-Slice nem referencia o core.
- [an-par-rfc-9126.md](../analisys/an-par-rfc-9126.md) — PAR permanece uma evolução separada; não deve ser
  confundido com a continuação interna de authorize parameters.
- `RoyalIdentity/Contracts/Storage/*.cs` e `RoyalIdentity/Users/Contracts/IUserSessionStore.cs` — contratos atuais.
- `RoyalIdentity/Contexts/Decorators/LoadCode.cs`, `LoadRefreshToken.cs`,
  `RoyalIdentity/Handlers/RefreshTokenHandler.cs` e `RoyalIdentity/Pipes.cs` — pontos atuais de consumo e
  concorrência.
- `old-is4/src/Storage/src/Stores/Serialization/ClaimLite.cs`,
  `old-is4/src/Storage/src/Models/RefreshToken.cs` e stores default de persisted grants — precedente histórico
  verificado para claims mínimas, JWT não persistido e snapshot de access token dentro do refresh token.
- `RoyalIdentity/Models/Tokens/*.cs`, `RoyalIdentity/Models/Consent.cs` e
  `RoyalIdentity/Users/UserSession*.cs` — grafos que precisam de round-trip completo.
- `Tests.Storage/Storage/Contracts/*.cs` — suíte provider-neutral já criada pelo baseline.
- `RoyalIdentity.Data.Configuration`, `RoyalIdentity.Storage.EntityFramework*`,
  `RoyalIdentity.Migrations` e `scripts/Test-ConfigurationPostgreSql.ps1` — precedentes locais implementados no
  Plano 2.
- [RFC 6749, §§4.1.2 e 10.5](https://www.rfc-editor.org/rfc/rfc6749.html#section-4.1.2) — authorization code
  curto, vinculado ao client/redirect e de uso único.
- [RFC 9700, §4.14](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.14) — confidencialidade, vínculo ao client
  e detecção de replay de refresh tokens.
- [ExecuteUpdate/ExecuteDelete — EF Core](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete)
  — operações em lote, contagem de linhas afetadas, ausência de concorrência automática e limite de retorno do
  registro alterado.
- [Transactions — EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions) — atomicidade de
  múltiplos comandos e limitações de transações entre contexts/providers.
- [Custom Migrations History Table — EF Core](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/history-table)
  — separação explícita da linha evolutiva Operational quando duas famílias usam o mesmo banco.
- [Unmapped JSON members — System.Text.Json](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members)
  — compatibilidade de payload Configuration aditivo com propriedades ausentes/desconhecidas.

### Estado atual do código (verificado em 2026-07-23)

- **Configuration EF está concluído:** existem `RoyalIdentity.Data.Configuration`,
  `RoyalIdentity.Storage.EntityFramework`, providers SQLite/PostgreSQL, migrations, SQL e runner. As portas
  Configuration são registráveis, mas não existe um `IStorage` EF produtivo parcial.
- **Operational EF ainda não existe:** não há `RoyalIdentity.Data.Operational`, entidades, mappings, contexts,
  migrations ou stores EF de tokens, codes, consents, sessões e authorize parameters.
- **O gateway mistura famílias:** `IStorage` expõe Configuration e Operational. A propriedade
  `AuthorizeParameters` ainda é global; os demais stores operacionais são realm-bound.
- **Authorization code usa get seguido de remove:** `LoadCode` lê, verifica client/redirect, remove o code e só
  depois valida expiração; `PkceMatchValidator` roda em seguida. Duas requests concorrentes podem ler o mesmo code
  antes de qualquer remoção.
- **Refresh token usa mutação + update genérico:** `LoadRefreshToken` lê e valida o token; o handler cria/renova
  access token antes de marcar `ConsumedTime` e chama `UpdateAsync`. O contrato não distingue primeira transição,
  repetição tolerada e conflito concorrente.
- **A política de claims no refresh está no lugar errado:** `Client.UpdateAccessTokenClaimsOnRefresh` é um `bool`
  com default `false`, persistido também na coluna Configuration `update_access_token_claims_on_refresh`. O alvo
  decidido remove a opção do client e torna a política exclusiva do realm, com `Current` como default.
- **A tolerância pós-consumo é comportamento de produto existente:** `RefreshTokenPostConsumedTimeTolerance`
  aceita repetição por uma janela, e `TimeSpan.MaxValue` permite o token reutilizável. O baseline exige que essa
  política não seja misturada acidentalmente com a primitiva atômica.
- **Authorize parameters não possui realm nem TTL:** login/consent escrevem uma `NameValueCollection`; resolvers
  podem lê-la repetidamente; o callback lê e apaga. O fake usa um dicionário global.
- **Sessões já possuem semântica rica no core:** `UserSession` tem `sid`, subject, método/idp, timestamps,
  `SecurityStamp`, estado ativo, expiração e clients deduplicados; o store possui touch e revogação em massa por
  subject.
- **A suíte provider-neutral já caracteriza o alvo preservado:** há cenários para realm isolation, comparadores,
  ausência, expiração lógica, idempotência, contagens efetivas e rematerialização. Os aceites de atomicidade, TTL,
  colisão e cleanup continuam reservados ao provider EF, não ao fake.
- **O host padrão continua in-memory:** `RoyalIdentity.Server` ainda chama `AddInMemoryStorage()`. A troca de backing
  dos hosts/testes pertence ao Plano 4.
- **O runner migra apenas Configuration:** `RoyalIdentity.Migrations` aceita somente provider/conexão/seed da
  família Configuration.
- **Resources continuam voláteis:** o bridge implementado no Plano 2 permanece necessário até o redesign DF22.

### Lacunas, conflitos e restrições

- O modelo híbrido de projeção relacional + payload versionado foi decidido, mas a topologia física entre uma tabela
  compartilhada de protocol artifacts e tabelas tipadas, bem como as FKs internas, ainda depende das partes abertas
  de Q1.
- Authorization codes, refresh tokens e authorize parameters contêm handles bearer ou equivalentes. O baseline
  não decidiu se o banco guarda esses valores, seus digests ou payload protegido.
- MP-2 e MP-3 exigem atomicidade, mas não fecharam as assinaturas públicas nem o resultado observável de conflito.
- O fake permanece default até o Plano 4, mas MP-2/MP-3 não podem obrigar nova paridade no fake. É necessário um
  caminho de compatibilidade explícito e temporário.
- O default numérico da janela de authorize parameters foi deliberadamente deixado ao Plano 3.
- DF19/MP-6 separaram expiração lógica, retenção e purge físico, mas não decidiram os períodos de retenção nem quem
  agenda a limpeza.
- A opção de janela de authorize interaction entra em `RealmOptions.Authentication`, portanto altera o grafo
  Configuration concluído no Plano 2. A mudança é aditiva no payload JSON e não requer migration relacional, mas
  precisa provar leitura de payload v1 anterior sem a propriedade e round-trip novo sem bump de versão.
- EF usa `__EFMigrationsHistory` por default. Como Configuration e Operational podem compartilhar o mesmo banco,
  as duas famílias precisam configurar sua history explicitamente. PostgreSQL usa
  `configuration.__EFMigrationsHistory` e `operation.__EFMigrationsHistory`; SQLite, que não possui schemas, usa
  `__ConfigurationMigrationsHistory` e `__OperationalMigrationsHistory`. O schema das entidades PostgreSQL não
  configura automaticamente o schema da history table.
- Configuration e Operational podem usar bancos distintos. Portanto, Operational não pode depender de FK ou
  transação com `configuration.realms`/`configuration.clients`; realm/client são vínculos lógicos por valor.
- Dentro da própria família Operational, ainda é necessário decidir quais relações são FKs e quais permanecem
  vínculos lógicos. Em especial, `session_id` pode ser ausente em client credentials; o alvo já elimina
  `refresh_token.access_token_id` como dependência entre lifecycles.
- O fluxo atual persiste também access tokens JWT e o refresh recupera o access token original por
  `AccessTokenId`. O alvo decidido elimina essa dependência: JWT persistence vira opção por realm; reference tokens
  continuam obrigatoriamente persistidos; refresh usa claims atuais ou snapshot próprio conforme a opção do realm.
- Remover `Client.UpdateAccessTokenClaimsOnRefresh` afeta o schema relacional Configuration já entregue pelo Plano
  2. As migrations existentes não são reescritas: este plano adiciona migrations SQLite/PostgreSQL que removem
  `update_access_token_claims_on_refresh`, além da evolução aditiva do payload v1 de `RealmOptions`.
- O purge Operational após tombstone de realm é requisito fechado, mas o seam de orquestração cross-family foi
  deliberadamente adiado.
- O comportamento atual de repetição tolerada de refresh token precisa ser confrontado explicitamente com o
  hardening de replay da RFC 9700, sem expandir o plano por acidente.

### Superfícies impactadas a mapear

- `RoyalIdentity.Data.Operational` — entidades puras, context padrão, mappings neutros e operações de manutenção.
- `RoyalIdentity.Storage.EntityFramework` — materialização, stores operacionais, transições atômicas, gateway
  completo e lifecycle.
- `RoyalIdentity.Storage.EntityFramework.Sqlite` / `.PostgreSql` — mappings, contexts, migrations e registration
  extensions por provider.
- `RoyalIdentity` — MP-2, MP-3, MP-5, options e consumidores dos fluxos de code/refresh/authorize parameters.
- `RoyalIdentity.Storage.InMemory` — somente adaptação transitória mínima exigida por mudanças de shape; sem TTL,
  particionamento ou aceites de atomicidade.
- `RoyalIdentity.Migrations` — segunda família, uma ou duas conexões, sem auto-migrate no host.
- `Tests.Storage` — harness EF completo, contratos existentes e aceites exclusivos de Operational.
- `Tests.Identity` / `Tests.Integration` — regressões de consumo e emissão afetadas pelos novos contratos, ainda
  sobre a composição atual quando o Plano 4 não for pré-requisito.
- `scripts/` — SQL revisável e PostgreSQL 17 efêmero com porta dinâmica não padrão.

---

## Objetivo

1. Persistir access tokens, refresh tokens, authorization codes, consents, sessões SSO e authorize parameters em
   SQLite e PostgreSQL, com isolamento obrigatório por realm.
2. Implementar exatamente as semânticas AT/RT/AC/CN/SS/AP fechadas na matriz, incluindo create-only/upsert/no-op,
   materialização independente, ausência, comparadores e expiração lógica.
3. Substituir o consumo get+remove de authorization code por uma operação single-use atômica e o update trivial de
   refresh token por transições condicionais observáveis.
4. Tornar authorize parameters realm-bound, com TTL absoluto, leitura fail-closed, handle não adivinhável,
   regeneração em colisão e cleanup.
5. Entregar cleanup físico por tipo e purge Operational por realm sem apagar o tombstone/configuração.
6. Completar o gateway EF (`IStorage`, `IStorageProvider`, `IStorageSession`) como composição opt-in de
   Configuration + Operational, preservando contexts/bancos independentes e sem transação global.
7. Aplicar por realm a proteção de payload, a persistência de JWT e a origem das claims no refresh; remover a opção
   equivalente do client sem manter duas precedências concorrentes.
8. Estender migrations, SQL e runner separado; comprovar paridade SQLite/PostgreSQL e concorrência real antes do
   Plano 4.

## Fora de escopo

- Trocar o backing padrão de `RoyalIdentity.Server`, `Tests.Integration`, `Tests.Identity` ou demais testes HTTP —
  destino: `plan-data-test-migration.md` (Plano 4).
- Persistir/redesenhar resources e scopes — bloqueado por DF22.
- API/UI administrativa, writes de Configuration e coordenação completa da exclusão de realm —
  destino: plano administrativo/ADR própria.
- UserAccounts e seus dados — família independente, acessível somente pela `.Integration`.
- PAR/RFC 9126, `PersistentDataMessageStore`, `IAuthorizationRequestStore` e endpoint de PAR —
  destino: análise/backlog próprios.
- Persistir `IMessageStore` ou redesenhar `IReplayCache`.
- Cache geral sobre stores Operational — destino: plano de caching.
- Auditoria durável, outbox ou histórico forense de tokens/sessões.
- Sender-constrained tokens, DPoP/mTLS e famílias de refresh token, salvo se Q10 expandir expressamente este plano.
- Executar migrations ou seed automaticamente no host.
- Fornecer um `DbContext` combinado concreto; somente os mappings públicos que permitem ao consumidor criá-lo.
- Adicionar lookup de sessão por subject (MP-9) sem um caller comprovado; revogação usa a operação em massa já
  existente.

---

## Perguntas ao humano

> Remova esta seção quando não houver perguntas abertas. Nenhuma recomendação abaixo é decisão enquanto o autor não
> responder.

- **Q1 — Modelagem dos grafos, topologia física e relações internas:** como os campos consultáveis, os grafos de
  claims/coleções, as tabelas e os vínculos dentro da família Operational devem ser persistidos? A Parte 1 está
  respondida; responder as Partes 2 e 3.
  - **Parte 1 — projeção dos grafos:**
    - **A)** Modelo híbrido: colunas/tabelas para identidade, realm, client, subject, sid, tipo e timestamps
      consultados; payload JSON versionado para claims e grafos não consultados. Consultas, revogações e cleanup
      permanecem indexáveis, com round-trip/versionamento rigorosos. **Recomendada.**
    - **B)** Modelo totalmente relacional: claims, scopes, audiences, properties e coleções em tabelas filhas.
      Maximiza a consultabilidade, mas aumenta o custo de mapping/escrita de dados transitórios.
    - **C)** Payload quase opaco: somente chave, realm e timestamps em colunas. Simplifica o schema, mas impede
      índices eficientes de subject/client/sid.
    - **Resposta:** **A**. Claims persistidas possuem somente `Type`, `Value` e `ValueType`; `Issuer`,
      `OriginalIssuer` e `Claim.Properties` não pertencem ao contrato Operational. Metadados próprios de outros
      modelos, como `AuthorizationCode.Properties`, continuam no payload do respectivo agregado.
  - **Parte 2 — integridade relacional interna:**
    - **A)** FKs somente para ownership estrutural com lifecycle comum, como
      `user_session_clients → user_sessions`; referências entre agregados/lifecycles, como
      `token.session_id`, permanecem vínculos lógicos indexados. Evita que cleanup independente seja governado por
      cascade/restrict sem perder integridade dos children. **Recomendada.**
    - **B)** FKs para toda relação interna representável, nullable quando necessário. Aumenta a integridade do
      banco, mas acopla ordem de escrita, retenção e purge de access tokens/refresh tokens/sessões.
    - **C)** Nenhuma FK interna; toda correlação é por valor. Maximiza independência de lifecycle, mas perde a
      proteção relacional até para tabelas filhas estruturalmente dependentes.
  - **Parte 3 — topologia física dos protocol artifacts:**
    - **A)** Uma tabela `protocol_artifacts`, discriminada por tipo, para reference access tokens, refresh tokens,
      authorization codes e JWT metadata/full quando habilitado pelo realm. Consents, sessões/session-clients e
      authorize parameters permanecem em tabelas próprias. Preserva stores tipados e operações atômicas, reduz
      migrations e acomoda extensões de lifecycle compatível sem nova tabela. Exige discriminator em toda operação,
      constraints condicionais e índices por tipo. **Recomendada.**
    - **B)** Tabelas distintas para access tokens, refresh tokens e authorization codes. Maximiza constraints e
      isolamento de churn/cleanup, mas multiplica mappings/migrations e exige nova tabela para cada artifact novo.
    - **C)** Uma tabela IS4-like incluindo também consents. Reduz tabelas, mas mistura a identidade natural/upsert e
      retenção de consent com artifacts endereçados por handle/chave.
  - **Impacto se não decidir:** as Partes 2 e 3 bloqueiam entidades, mappings, migrations, ordem de escrita, índices
    e estratégia de cleanup da Fase 1. Formato de resposta sugerido:
    `Q1: Parte 2 A; Parte 3 A`.
  - **Status:** Parcialmente respondida — Parte 1=A; Partes 2/3 abertas.

- **Q2 — Persistência de handles bearer/opaques:** qual representação deve ser usada como chave de lookup?
  Esta pergunta não decide se JWTs são persistidos: essa política já foi fechada por realm na DF31. Para access
  tokens, o desenho deve distinguir a identidade `jti` do valor bearer apresentado quando houver token de
  referência, sem alterar silenciosamente a semântica AT-02.
  - **Opções:**
    - **A)** Digest criptográfico para authorization code, refresh token, authorize-parameters handle e
      identificador bearer de access token `Reference`; `jti` continua projetado e JWT segue a política do realm.
      O valor bruto não é persistido como chave. Reduz reutilização após leitura indevida do banco.
      **Recomendada.**
    - **B)** Valor bruto como PK/índice. Simplifica a implementação, mas uma leitura do banco expõe credenciais
      reutilizáveis.
    - **C)** HMAC determinístico com chave operacional. Protege também contra enumeração de entradas de baixa
      entropia, mas introduz distribuição e rotação de chave no caminho crítico.
  - **Impacto se não decidir:** bloqueia chaves, índices, materialização e política de segurança do schema.
  - **Status:** Aberta.

- **Q4 — Contrato atômico de authorization code (MP-2):** qual operação pública substitui get+remove no fluxo?
  - **Opções:**
    - **A)** Consume condicional recebe handle e vínculos esperados de client/redirect, remove atomicamente e
      retorna o code; `null` cobre ausente, já consumido ou vínculo inválido. Expiração/PKCE continuam no pipeline
      após o consumo. **Recomendada.**
    - **B)** O fluxo faz get/valida client/redirect e chama consume atômico somente por handle. Mantém duas idas e
      exige verificar que o objeto consumido é o observado.
    - **C)** Resultado discriminado (`Success`, `NotFound`, `ClientMismatch`, `RedirectMismatch`). Melhora o
      diagnóstico interno, mas amplia a superfície e o risco de diferenciação indevida no erro OAuth.
  - **Impacto se não decidir:** bloqueia o contrato MP-2, a refatoração do pipeline e os aceites concorrentes.
  - **Status:** Aberta.

- **Q5 — Contrato de transição do refresh token (MP-3):** como representar consumo, conflito e atualização do
  token reutilizável?
  A resposta deve distinguir perder a transição de aplicar a política de tolerância: um conflito nunca vira sucesso
  por si só. Se Q10 preservar a tolerância, o caller só pode avaliá-la sobre estado consumido rematerializado depois
  do conflito, tornando observável que houve outra transição vencedora.
  - **Opções:**
    - **A)** Estado + versão condicional: leitura traz versão; `TryConsume` distingue vencedor, conflito e já
      consumido; atualização posterior exige versão esperada. Preserva a tolerância como política do caller e torna
      perdas concorrentes observáveis. **Recomendada.**
    - **B)** Somente `TryConsume`; a atualização posterior do estado/payload do refresh reutilizável continua
      last-write-wins. Resolve a primeira transição, mas deixa RT-03 parcialmente desprotegido.
    - **C)** Transação de redemption abrangente, incluindo consumo e tokens emitidos. Aumenta a atomicidade, mas
      move emissão para a persistência ou cria UoW maior que o baseline.
  - **Impacto se não decidir:** bloqueia modelo de versão, contrato MP-3, handler e testes de concorrência.
  - **Status:** Aberta.

- **Q6 — Default do TTL de authorize parameters:** qual janela por realm deve ser usada?
  - **Opções:**
    - **A)** 10 minutos. Limita o estado abandonado e normalmente comporta login/consent. **Recomendada.**
    - **B)** 15 minutos. Tolera fluxos humanos mais longos, mantendo handles válidos por mais tempo.
    - **C)** 5 minutos. Reduz retenção/exposição, com maior risco de expiração durante o fluxo.
  - **Impacto se não decidir:** bloqueia o default público, validação de options e testes de TTL da Fase 6.
  - **Status:** Aberta.

- **Q7 — Retenção após estado terminal:** quando cada tipo se torna elegível ao purge físico?
  Refresh já conserva o grant mínimo ou snapshot próprio conforme DF32 e não prolonga a retenção do access token
  anterior. A resposta define apenas quando cada artifact deixa de ser fisicamente observável após seu próprio
  expiry/estado terminal.
  - **Opções:**
    - **A)** Elegibilidade imediata após deixar de ser semanticamente observável: code consumido é apagado; demais
      tipos após expiração/estado terminal e depois de encerradas suas dependências observáveis; refresh respeita
      tolerância e expiração. Não preserva histórico operacional. **Recomendada.**
    - **B)** Grace configurável por tipo. Suporta diagnóstico limitado, mas aumenta PII, volume e defaults.
    - **C)** Grace único global. Simplifica configuração, mas ignora lifecycles diferentes.
  - **Impacto se não decidir:** bloqueia queries de cleanup, colunas terminais e critérios de purge. Se B for
    escolhida, os defaults por tipo também precisam ser respondidos antes da Fase 6.
  - **Status:** Aberta.

- **Q8 — Executor do cleanup periódico:** onde o agendamento deve viver?
  - **Opções:**
    - **A)** Hosted worker configurável sobre manutenção reutilizável, em batches idempotentes. Automatiza a
      operação, mas adiciona escrita periódica aos deployments. **Recomendada.**
    - **B)** Comando/job externo apenas. Dá controle operacional; sem scheduler, o banco cresce indefinidamente.
    - **C)** Ambos, com exatamente um modo habilitado. Maximiza flexibilidade e aumenta configuração/testes.
  - **Impacto se não decidir:** bloqueia lifecycle, options, registration e operação do cleanup.
  - **Status:** Aberta.

- **Q9 — Seam de purge Operational por realm:** qual superfície será exposta à futura orquestração administrativa?
  - **Opções:**
    - **A)** Porta de manutenção fora do core, em `Storage.EntityFramework`, usando primitivas. Não incha
      `IStorage` nem cria dependência cross-family. **Recomendada.**
    - **B)** Operação interna acessível somente por testes/runner até o plano administrativo. Reduz a API agora,
      mas exige promoção/refatoração posterior.
    - **C)** Método em `IStorage`. Facilita descoberta, mas mistura manutenção administrativa com runtime.
  - **Impacto se não decidir:** bloqueia a parte Operational de MP-7 e os testes de purge.
  - **Status:** Aberta.

- **Q10 — Política de refresh-token replay:** o Plano 3 preserva o baseline ou incorpora hardening da RFC 9700?
  - **Opções:**
    - **A)** Preservar `RefreshTokenPostConsumedTimeTolerance`, tornando apenas as transições atômicas; famílias,
      detecção/revogação de replay e sender constraint ficam para hardening próprio. Controla o escopo, mantendo a
      divergência explicitamente documentada. **Recomendada para este corte.**
    - **B)** Adicionar famílias/rotação agora. Aproxima o alvo da RFC 9700, mas amplia domínio, contratos, handlers,
      cleanup e testes.
    - **C)** Exigir sender-constrained refresh tokens via mTLS/DPoP. É mudança protocolar maior que o escopo atual.
  - **Impacto se não decidir:** bloqueia semântica RT-03, modelo, handler, cleanup e critérios de segurança.
  - **Status:** Aberta.

- **Q11 — Compatibilidade temporária com o fake:** como MP-2/MP-3 coexistem com o backing default até o Plano 4?
  - **Opções:**
    - **A)** Capability interfaces implementadas pelo EF, com fallback legado explicitamente transitório no core.
      O fake não cresce; o adapter EF nunca pode usar o fallback. **Recomendada.**
    - **B)** Default interface methods não atômicos. Reduz adaptação, mas a assinatura aparenta uma garantia que o
      default não entrega.
    - **C)** Implementar locks/CAS no fake. Uniformiza o comportamento, mas contraria ADR-018.
  - **Impacto se não decidir:** bloqueia mudanças públicas e mantém incompatibilidade entre o Plano 3 e o host
    in-memory.
  - **Status:** Aberta.

- **Q12 — Shape da janela de authorize interaction:** qual tipo público deve entrar em `AuthenticationOptions`?
  - **Opções:**
    - **A)** `TimeSpan`, com unidade explícita e validação `> 0`; nome sugerido
      `AuthorizationInteractionLifetime`. **Recomendada.**
    - **B)** Segundos como `int`, seguindo os lifetimes atuais de `Client`.
    - **C)** Minutos como `int`, seguindo `SessionOptions`, com menor granularidade.
  - **Impacto se não decidir:** bloqueia contrato público, serialização Configuration e options de AP.
  - **Status:** Aberta.

---

## Decisões fechadas

- **DF1 — Ownership:** `RoyalIdentity.Data.Operational` contém entidades persistentes, context padrão, mappings
  neutros e operações de dados sem referência ao core. Somente `RoyalIdentity.Storage.EntityFramework` adapta
  modelos/contratos do IdP. Fonte: ADR-013/architecture.
- **DF2 — Separação combinável:** Configuration e Operational possuem DbContexts e conexões próprias, podendo
  apontar para bancos distintos ou para o mesmo banco. Mappings ficam em extensões públicas, permitindo context
  combinado de terceiros sem fornecê-lo no produto. O gateway padrão cria um DbContext e uma instância de
  `DbConnection` por família mesmo quando as connection strings apontam para o mesmo banco; o pooling continua a
  cargo do provider, sem compartilhamento explícito de conexão/transação. Fonte: DF2/DF3 do Plano 2 e documentação
  EF de transações cross-context.
- **DF3 — Lifecycle:** DbContexts, factories e stores são scoped. `IStorageProvider.CreateSession()` cria um scope
  real e `IStorageSession.Dispose()` o encerra; sessão não é transação global. Fonte: DF6/DF20 do Plano 2 e DF21 do
  baseline.
- **DF4 — Providers e schemas:** SQLite e PostgreSQL são obrigatórios. PostgreSQL usa schema `operation`; SQLite
  usa os mesmos nomes de tabela sem schema. Fonte: Plano 2 Q12/DF18 e macro-plan.
- **DF5 — Isolamento:** toda PK/unique/query/mutação Operational inclui `RealmId` lógica ou fisicamente. Ids iguais
  em realms diferentes coexistem. Fonte: DF6 do baseline.
- **DF6 — Vínculos cross-family são lógicos:** não há FK Operational → Configuration nem transação entre famílias,
  pois os bancos podem ser distintos. `realmId`/`clientId` preservam comparação Ordinal. Fonte: DF2 deste plano,
  DF18/DF21 do baseline.
- **DF7 — Criação:** access token, refresh token, authorization code e sessão são create-only; duplicidade falha
  visivelmente e nunca sobrescreve. Consent é upsert pela chave `(realm, subject, client)`. Fonte:
  AT-01/RT-01/AC-01/SS-01/CN-01.
- **DF8 — Leituras e ausência:** lookups retornam `null` quando ausentes. Access token, refresh token, code, consent
  e sessão continuam materializáveis mesmo expirados/consumidos/inativos; somente authorize parameters filtra
  expirado e retorna `null`. Fonte: DF19/DF25 e matriz.
- **DF9 — Materialização:** toda leitura produz grafo independente; mutar o objeto devolvido não persiste sem
  operação explícita. Toda coleção e todo dado pertencente ao contrato Operational deve sobreviver ao round-trip;
  claims seguem o contrato mínimo da DF34. Fonte: DF17 e Q1 Parte 1.
- **DF10 — Comparadores:** identificadores operacionais, subject, client, sid, scope e handles usam comparação
  Ordinal/case-sensitive. Nenhuma collation default do provider redefine a semântica. Fonte: DF18.
- **DF11 — Authorization code:** o consumo no fluxo do token é single-use e atômico; apenas um concorrente pode
  obter sucesso. A remoção administrativa continua idempotente. A assinatura aguarda Q4. Fonte: DF15/MP-2.
- **DF12 — Refresh token:** a primeira transição de consumo e atualizações de estado são condicionais/atômicas; o
  CAS trivial atual não é alvo. A tolerância é política separada: conflito não é sucesso e somente estado consumido
  rematerializado pode ser submetido à tolerância pelo caller. A assinatura aguarda Q5. Fonte: DF15/MP-3.
- **DF13 — Access token:** reference access tokens são sempre persistidos e seguem AT-01..AT-04; remoção em massa
  usa tipo + subject + client Ordinal e é idempotente. JWTs seguem `JwtAccessTokenPersistenceMode` do realm
  (`None`, `Metadata` ou `Full`, default `None`); somente `Full` conserva o compact JWT. Persistir/remover um JWT não
  constitui revogação efetiva, pois validação stateless não consulta o store. Refresh não depende de uma linha do
  access token anterior. Fonte: AT-01..AT-04, comparação com IS4 e decisão humana.
- **DF14 — Consent:** scopes permanecem dentro do consent, preservando casing; ausência/removal são
  null/idempotente e upsert torna a última escrita efetiva. Fonte: CN-01..CN-03.
- **DF15 — Sessão:** create-only; record-client deduplica por client Ordinal, preserva `FirstSeenAt` e atualiza
  `LastSeenAt` via `TimeProvider`; ausência em record/touch é no-op; end e revogação por subject são idempotentes e
  contam apenas mudanças efetivas. Fonte: SS-01..SS-06/ADR-017.
- **DF16 — Authorize parameters:** o accessor passa a ser realm-bound; write grava expiração absoluta calculada
  pelo `TimeProvider`; read é repetível dentro da janela e fail-closed depois; delete é idempotente; handle possui
  ao menos 128 bits de entropia e colisão é regenerada internamente. Tipo/default aguardam Q6/Q12. Fonte: MP-5.
- **DF17 — Cleanup separado:** validação lógica não depende da execução do purge. Cleanup físico é por tipo, em
  batches e idempotente, com lazy cleanup de AP na leitura. Refresh conserva o grant/snapshot que seu modo exige e
  nunca prolonga a retenção da linha do access token anterior; elegibilidade/retention/agendamento aguardam Q7/Q8.
  Fonte: DF19/MP-6 e DF31/DF32.
- **DF18 — Realm deletion:** Configuration conserva tombstone/path/domain e Operational apaga fisicamente seus
  dados. Esta fase entrega o purge isolado; coordenação com Configuration/UserAccounts continua futura. O seam
  aguarda Q9. Fonte: DF20/MP-7.
- **DF19 — I/O:** todo acesso EF é assíncrono, propaga `CancellationToken` até o provider e não abre conexão em API
  síncrona. Fonte: DF23.
- **DF20 — Ordenação:** nenhuma listagem Operational recebe order implícito; só se adiciona ordem quando existir
  significado de negócio. Fonte: DF24.
- **DF21 — Gateway completo, mas opt-in:** após Operational, `Storage.EntityFramework` pode compor
  Configuration + Operational em `IStorage`/`IStorageProvider`/`IStorageSession`. O host padrão continua in-memory
  até o Plano 4. Fonte: DF20 do Plano 2 e macro-plan.
- **DF22 — Configuration no gateway:** `IStorage.ServerOptions` usa o snapshot já implementado e não faz I/O
  síncrono; realms/clients/keys vêm das portas Configuration; resources usam o bridge volátil DF22. Fonte:
  Plano 2.
- **DF23 — Migrations:** o host nunca executa `EnsureCreated`, `Migrate` ou seed. Migrations ficam nos providers,
  SQL é versionado e o runner geral aceita Configuration, Operational ou ambas. Cada família configura
  `MigrationsHistoryTable` explicitamente em todos os pontos que constroem options para migrations:
  `configuration.__EFMigrationsHistory` e `operation.__EFMigrationsHistory` no PostgreSQL;
  `__ConfigurationMigrationsHistory` e `__OperationalMigrationsHistory` no SQLite. Em banco já migrado pelo Plano 2,
  o runner executa um bootstrap idempotente **antes** de qualquer `MigrateAsync`: se a history legada default existe
  e a nova de Configuration não existe, move/renomeia a tabela preservando todas as linhas; se ambas existem, valida
  e falha fechado diante de ambiguidade, sem merge/delete automático; banco vazio cria diretamente as histories
  novas. SQL manual versionado oferece a mesma transição. Fonte: DF11/DF16/DF21 do Plano 2 e documentação EF de
  migrations history.
- **DF24 — Sem seed Operational:** dados operacionais nascem dos fluxos; runner não cria tokens, codes, consents ou
  sessões demo. Fixtures escrevem pelo data layer/store apropriado. Fonte: natureza da família e macro-plan.
- **DF25 — Fake transitório:** não adicionar TTL, particionamento, payload protection, cleanup ou testes de
  concorrência ao fake. Mudanças mínimas de shape não o tornam referência de paridade. Fonte: ADR-018/matriz.
- **DF26 — PAR e messages separados:** AP representa continuação interna multi-read; não é store de PAR nem
  `IMessageStore`. Fonte: análise PAR e DF14 do baseline.
- **DF27 — Relógio:** timestamps de expiração, consumo, sessão e cleanup usam `TimeProvider` da composição, em UTC;
  nenhum store chama relógio de parede diretamente. Fonte: matriz/ADR-017.
- **DF28 — Logs:** handles bearer, payloads, claims, subjects, connection strings e material de proteção não
  aparecem em logs/erros. Telemetria usa tipo, resultado agregado e contagens. Fonte: requisitos OAuth e
  precedentes de segurança do Plano 2.
- **DF29 — Evolução aditiva de RealmOptions:** a janela de authorize interaction,
  `OperationalStorageOptions` e `RefreshTokenOptions` alteram a família Configuration, mas não seu schema relacional.
  Propriedades ausentes no payload v1 materializam os defaults fechados; payload novo faz round-trip e
  `RealmOptionsPayloadSerializer.CurrentVersion` permanece `1`. Qualquer mudança que não satisfaça esses testes
  exige reabrir a decisão de versão antes de implementar. Fonte: Q6/Q12, DF30–DF32, Plano 2 DF5/DF25 e
  comportamento de membros JSON ausentes.
- **DF30 — Proteção Operational por realm:** `RealmOptions.OperationalStorage.PayloadProtectionProfile` seleciona
  por id um profile registrado pelo host; o default serializável é o id `default`, não um algoritmo/protector
  implícito. Secrets, chaves e key-ring paths nunca entram em Configuration. O envelope versionado persiste
  `protector_id`; novas escritas usam o profile atual e leitores anteriores continuam registrados para abrir linhas
  legadas. `Plain` exige registration + seleção explícitas e warning; profile ausente falha fechado, sem fallback.
  Campos relacionais consultáveis ficam em claro; o contexto autenticado inclui `realm_id`, artifact/record type,
  lookup key e payload version. Fonte: Q3=A e decisão humana.
- **DF31 — Persistência JWT por realm:** `RealmOptions.OperationalStorage.JwtAccessTokenPersistence` possui
  `None`, `Metadata` e `Full`, com default `None`. A opção afeta somente novas emissões e nunca desabilita a
  persistência obrigatória de reference tokens. `Metadata` não grava o compact JWT; `Full` grava o grafo completo e
  o compact JWT dentro do payload protegido conforme DF30. Fonte: decisão humana e precedente IS4.
- **DF32 — Claims no refresh por realm:** `RealmOptions.RefreshTokens.ClaimsMode` possui `Current` e `Snapshot`,
  com default `Current`. `Current` conserva somente o grant mínimo (subject/session/client/scopes/resources e
  vínculos futuros aplicáveis), revalida conta/sessão/configuração e reexecuta a emissão/consulta ao UserAccounts;
  nunca amplia o grant original. `Snapshot` conserva no próprio refresh payload os dados necessários para reproduzir
  as claims, sem depender da linha do access token. `Client.UpdateAccessTokenClaimsOnRefresh` é removido sem override
  por client. Fonte: decisão humana, `UserSession`, `DefaultProfileService` e precedente IS4.
- **DF33 — Remoção da opção do client:** remover `Client.UpdateAccessTokenClaimsOnRefresh`, a propriedade
  `ClientEntity`, mapping/materializers e a coluna Configuration `update_access_token_claims_on_refresh`. Migrations
  Configuration novas de SQLite/PostgreSQL removem a coluna; migrations já publicadas do Plano 2 não são reescritas.
  Não se infere configuração de realm a partir de clients possivelmente divergentes: payload v1 ausente adota
  `ClaimsMode.Current`, uma mudança de comportamento intencional e coberta por regressão. Fonte: decisão humana.
- **DF34 — Claim payload mínimo:** o contrato persistido de claim contém somente `Type`, `Value` e `ValueType`, como
  o `ClaimLite` do IS4. `Issuer`, `OriginalIssuer` e `Claim.Properties` não possuem semântica Operational; o
  materializador cria a claim com issuer canônico/default. Metadados `Properties` de outros modelos continuam
  preservados. Uma futura dependência de metadata de claim exige nova versão explícita, nunca perda silenciosa.
  Fonte: Q1 Parte 1 e comparação com IS4.

---

## Histórico de decisões

> Ao responder Q1–Q12, registrar aqui as opções consideradas, a resposta, as considerações verificadas e a
> conclusão antes de remover a pergunta de `Perguntas ao humano`.

### 2026-07-23 — Q1 Parte 1, Q3 e políticas correlatas de token/refresh

- **Q1 Parte 1:** escolhida **A**, modelo híbrido. Campos usados por lookup/revogação/cleanup ficam relacionais;
  grafos não consultados ficam em payload versionado. `ClaimPayload` foi reduzido a `Type`/`Value`/`ValueType`,
  alinhado ao IS4 e ao comportamento atual do core/UserAccounts. Partes 2/3 permanecem abertas.
- **Q3:** escolhida **A**, refinada para seleção por realm. O realm guarda somente um profile id; o host registra
  Data Protection/AES/Plain e seus secrets. Envelope guarda o protector usado, `Plain` nunca é fallback e profile
  ausente falha fechado.
- **Persistência JWT:** decisão nova fechada por realm: `None`/`Metadata`/`Full`, default `None`; reference tokens
  permanecem sempre persistidos. Armazenar JWT não é tratado como revogação.
- **Claims no refresh:** decisão nova fechada por realm: `Current`/`Snapshot`, default `Current`. `Current` reexecuta
  a emissão com claims atuais sem ampliar scopes/resources do grant; `Snapshot` guarda os dados no próprio refresh
  token. Nenhum modo depende da linha do access token anterior.
- **Client:** `Client.UpdateAccessTokenClaimsOnRefresh` é removido, sem override/precedência por client. A coluna
  Configuration correspondente recebe migrations novas; o novo default `Current` é intencional.

---

## Design alvo

As escolhas marcadas `[Qn]` não são design aprovado; mostram somente onde a resposta altera o desenho.

### Contratos e bordas

- `IOperationalStoreFactory` é a entrada scoped do adapter para criar stores realm-bound de access token, refresh
  token, authorization code, consent, sessão e authorize parameters.
- `IStorage.AuthorizeParameters` é substituído por accessor realm-bound, alinhado aos demais stores. O nome exato
  deve seguir o padrão existente (`GetAuthorizeParametersStore(Realm)`).
- MP-2 entra como operação/capability de consumo de authorization code; a assinatura final depende de Q4/Q11.
- MP-3 entra como operação/capability de transição de refresh token; assinatura/resultado dependem de Q5/Q11.
- A janela de authorize interaction vive em `RealmOptions.Authentication`; type/nome/default dependem de Q6/Q12.
  Essa é uma alteração aditiva da família Configuration: mantém payload version `1`, não gera migration relacional
  e exige teste de payload v1 anterior sem a propriedade além do round-trip novo (DF29).
- `RealmOptions.OperationalStorage` contém `PayloadProtectionProfile` e `JwtAccessTokenPersistence`; ambos são
  exclusivos do realm. O profile é apenas um identificador público resolvido pelo adapter/host, nunca material
  criptográfico.
- `RealmOptions.RefreshTokens.ClaimsMode` é a única política de origem das claims no refresh.
  `Client.UpdateAccessTokenClaimsOnRefresh` e sua coluna Configuration são removidos conforme DF33; não existe
  override por client.
- O adapter expõe registration de profiles Operational nomeados. Exatamente um profile selecionado é usado para
  novas escritas de cada realm; o `protector_id` do envelope seleciona leitores legados.
- A manutenção de cleanup/purge não vira CRUD administrativo nem transação cross-family; sua exposição depende de
  Q8/Q9.
- O gateway EF completo compõe:
  - `IConfigurationSnapshot` para `ServerOptions`;
  - `IConfigurationStoreFactory` para realms, clients, keys e resources bridge;
  - `IOperationalStoreFactory` para dados operacionais;
  - um scope próprio por `IStorageSession`.
- Nenhum store aceita `Realm` vivo no projeto puro. O adapter extrai `realm.Id` e instancia uma porta ligada ao
  valor.

### Modelo, dados e persistência

Independentemente da topologia física ainda aberta em Q1 Parte 3, as operações exigem as seguintes projeções
lógicas consultáveis. Se `protocol_artifacts` for escolhido, access/refresh/code compartilham a tabela e
`artifact_type` integra toda PK/query; se forem escolhidas tabelas distintas, os mesmos campos/índices permanecem:

```text
access-token artifact
  realm_id + lookup_key                    PK [Q2; digest quando bearer/reference]
  token_id/jti projetado quando necessário ao domínio
  subject_id, client_id, session_id
  access_token_type
  created_at_utc, expires_at_utc
  payload_version + protected_payload      [DF30/DF31; ausente para JWT mode None]

refresh-token artifact
  realm_id + handle_key                    PK [Q2]
  subject_id, client_id, session_id
  created_at_utc, expires_at_utc, consumed_at_utc
  state_version                            [Q5]
  claims_mode
  payload_version + protected_payload      [grant mínimo Current ou snapshot próprio]
  index (realm_id, subject_id)

authorization-code artifact
  realm_id + handle_key                    PK [Q2]
  client_id, redirect_uri
  created_at_utc, expires_at_utc
  payload_version + protected_payload

operation.consents
  realm_id + subject_id + client_id        PK
  created_at_utc, expires_at_utc
  payload_version + protected_payload/scopes

operation.user_sessions
  realm_id + session_id                    PK
  subject_id
  authentication_method, identity_provider
  started_at_utc, last_seen_at_utc, expires_at_utc
  security_stamp, is_active
  payload_version + protected_payload      [somente se necessário]
  index (realm_id, subject_id, is_active)

operation.user_session_clients
  realm_id + session_id + client_id        PK
  first_seen_at_utc, last_seen_at_utc
  FK para user_sessions quando Q1 selecionar ownership estrutural

operation.authorize_parameters
  realm_id + handle_key                    PK [Q2]
  created_at_utc, expires_at_utc
  payload_version + protected_payload
  index (realm_id, expires_at_utc)
```

- `expires_at_utc` é persistido para não recalcular validade com configuração alterada.
- Retenção pode exigir `ended_at_utc`/outro marcador terminal depois de Q7; não adicionar colunas sem essa decisão.
- Não há FK para realm/client/subject em outras famílias.
- FKs internas e vínculos lógicos seguem a Parte 2 de Q1. Nenhuma relação é inferida apenas porque duas colunas
  carregam o mesmo identificador.
- Reference access tokens sempre produzem artifact persistido. JWT produz nenhuma linha, metadata sem compact JWT ou
  payload completo conforme DF31; a policy efetiva é capturada na escrita e mudança posterior do realm não
  reinterpreta artifacts existentes.
- Refresh nunca guarda `AccessTokenId` como dependência observável. `Current` conserva o grant mínimo e reconsulta
  sessão/UserAccounts/configuração; `Snapshot` conserva seu próprio snapshot. Ambos preservam subject/client/scopes/
  resources originalmente autorizados e nunca ampliam o grant a partir de configuração atual.
- Índices de cleanup são definidos depois de Q7/Q8 sobre os predicados reais. Cleanup global começa pelo estado/
  timestamp terminal; cleanup realm-bound pode começar por `realm_id`. Os model tests comprovam a forma escolhida
  antes da migration SQLite inicial, evitando índice especulativo e migration corretiva.
- Claims persistem somente `Type`, `Value` e `ValueType` conforme DF34; `Issuer`, `OriginalIssuer` e
  `Claim.Properties` são deliberadamente fora do contrato, enquanto properties próprias de code/outros modelos
  continuam no payload.
- Payloads possuem envelope/versionamento explícito, profile por realm e falham fechado quando ilegíveis.
- A infraestrutura criptográfica genérica comprovada em Configuration é extraída/reusada, mas contratos,
  purposes e contexto autenticado Operational permanecem próprios; `IKeyMaterialProtector` não é reutilizado como
  interface de payload operacional.
- Collections retornam comparadores equivalentes aos modelos do core; não herdam comportamento da collation.

### Arquitetura alvo

```text
RoyalIdentity.Data.Operational/
  OperationalDbContext.cs
  OperationalModelOptions.cs
  OperationalModelBuilderExtensions.cs
  Entities/
  Maintenance/
  (EF Core only; NO RoyalIdentity reference)

RoyalIdentity.Storage.EntityFramework/
  Operational/
    Materialization/
    Stores/
    Atomic/
    Cleanup/
  Storage/
    EntityFrameworkStorage.cs
    EntityFrameworkStorageProvider.cs
    EntityFrameworkStorageSession.cs
  Extensions/
  (references RoyalIdentity + Data.Configuration + Data.Operational)

RoyalIdentity.Storage.EntityFramework.Sqlite/
  OperationalSqliteDbContext.cs
  public Operational SQLite mapping extension
  design-time factory + Operational Migrations/

RoyalIdentity.Storage.EntityFramework.PostgreSql/
  OperationalPostgreSqlDbContext.cs
  public Operational PostgreSQL mapping extension
  design-time factory + Operational Migrations/

RoyalIdentity.Migrations/
  Configuration and/or Operational selection
  one or two provider/connection pairs
  no Operational seed

Tests.Storage/
  Operational/Support/
  Operational provider acceptances
  complete EF StorageContractHarness
```

Context combinado de terceiro:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
	base.OnModelCreating(modelBuilder);
	modelBuilder.ApplyRoyalIdentityConfigurationPostgreSqlMappings(configurationOptions);
	modelBuilder.ApplyRoyalIdentityOperationalPostgreSqlMappings(operationalOptions);
}
```

O context combinado usa um provider e uma conexão; contexts separados podem usar providers/conexões diferentes.
Nenhum código do produto coordena commit entre ambos.

### Segurança, concorrência e confiabilidade

- Create-only usa constraint única como autoridade; check prévio pode melhorar erro, mas não substitui a constraint.
- Upsert de consent precisa ser um resultado indivisível por chave composta; concorrentes não criam duplicatas.
- Code consumption precisa alterar/remover exatamente uma linha e retornar sucesso para no máximo um concorrente.
- Refresh consumption usa predicado de estado/versão e contagem de linhas afetadas; zero linhas é resultado de
  conflito/estado alterado, não sucesso silencioso.
- `ExecuteUpdateAsync`/`ExecuteDeleteAsync` não fornecem concorrência automática nem retornam o registro anterior.
  Cada provider pode usar transação curta, SQL com `RETURNING` ou combinação equivalente; a semântica pública e os
  testes são provider-neutral.
- Não misturar entidades tracked com operações set-based no mesmo context sem limpar/recarregar o tracker.
- Transações explícitas ficam limitadas à operação Operational que precisa delas. Nada envolve Configuration,
  UserAccounts, dispatch de eventos ou chamadas externas.
- Os testes concorrentes usam scopes/DbContexts/conexões independentes e barreira de início; concorrência simulada
  dentro do mesmo `DbContext` não é aceite válido.
- O profile de proteção efetivo é resolvido a partir do snapshot do realm antes da operação. Profile ausente,
  envelope adulterado, protector antigo indisponível ou incompatibilidade de contexto falham fechado; nenhuma dessas
  falhas tenta `Plain`.
- `JwtAccessTokenPersistence=Full` combinado com profile `Plain` exige opt-ins independentes e warning explícito,
  pois grava um bearer reutilizável em texto claro. O compact JWT nunca entra em coluna consultável, PK ou log.

**Cleanup e purge:**

- Toda operação de cleanup recebe `now`, batch size e `CancellationToken`; produção fornece `now` por
  `TimeProvider`.
- Cada batch retorna contagens por tipo, não os registros removidos.
- AP expirado é ausente na leitura mesmo que o purge nunca rode; uma leitura pode remover preguiçosamente sem
  transformar falha de cleanup em falha do protocolo.
- Access token/code/consent/session/refresh seguem a elegibilidade definida em Q7 e nunca são filtrados antes disso
  por um lookup comum. Refresh não prolonga a retenção do access token anterior; a linha JWT/reference segue seu
  próprio expiry/estado e a grace eventualmente escolhida em Q7.
- Purge de realm remove todas as tabelas Operational e children no realm alvo, é repetível e não observa outro
  realm com ids colidentes.
- Cleanup e purge não registram handles, subjects ou payloads.

### Compatibilidade, migração e rollout

- `RoyalIdentity.Server` continua com `AddInMemoryStorage()` neste plano.
- A registration EF completa é opt-in e exige as duas famílias; não existe modo produtivo “Operational EF +
  Configuration ausente”.
- O gateway padrão resolve um DbContext/conexão por família. Connection strings iguais podem reutilizar o pool do
  provider, mas o produto não compartilha instância de `DbConnection` ou `DbTransaction`; context combinado de
  terceiro permanece opt-in.
- A fixture EF completa substitui o composite test-only do Plano 2 para a contract suite, mas os testes HTTP só
  migram no Plano 4.
- O fake recebe apenas o mínimo necessário para compilar o accessor realm-bound de AP e o mecanismo Q11; seu
  dicionário continua global, sem TTL, e não executa os aceites EF.
- Migrations Configuration e Operational mantêm assemblies/contexts próprios e usam a topologia exata da DF23. No
  PostgreSQL, o mesmo nome `__EFMigrationsHistory` é isolado pelos schemas `configuration`/`operation`; no SQLite,
  os nomes são distintos por não existir schema. Quando duas famílias usam o mesmo banco, o runner primeiro conclui
  o bootstrap da history legada e depois aplica ambas sequencialmente, sem transação distribuída e sem misturar suas
  linhas evolutivas.
- O bootstrap de history é infraestrutura do runner, não migration de domínio: ele roda por conexão/família antes
  de o EF consultar sua history. Scripts manuais equivalentes ficam em
  `scripts/sql/migration-history/{sqlite,postgresql}/`.
- Migrations Configuration já geradas não são reescritas; design-time factories, runner e scripts idempotentes são
  reconfigurados/regenerados para a nova history. O bootstrap preserva os migration ids existentes e não cria uma
  migration de domínio artificial apenas para mover metadata do EF.
- Novas migrations Configuration removem `update_access_token_claims_on_refresh` depois de o core/adapter passarem a
  usar exclusivamente `RealmOptions.RefreshTokens.ClaimsMode`. Não há backfill client → realm: payload v1 sem a
  opção materializa `Current`. Testes cobrem clients legados com `true`/`false` e documentam a mudança intencional.
- Alterar o profile de proteção, o modo JWT ou o modo de claims do refresh afeta somente novas escritas/emissões.
  Envelopes antigos conservam seu `protector_id`; refresh tokens já emitidos conservam seu `claims_mode`; artifacts
  JWT existentes seguem legíveis/limpáveis sem reinterpretar a configuração atual.
- SQL fica em `scripts/sql/operational/{sqlite,postgresql}/`.
- O runner aceita executar somente Configuration, somente Operational ou ambas. Seed continua exclusivo de
  Configuration.

---

## Ordem de execução

1. **Fase 1 (fronteiras/modelo/contracts)** — fecha primeiro os shapes públicos e o model extensível.
2. **Fase 2 (access tokens/consents)** — inicia pelos stores de menor risco na ordem normativa.
3. **Fase 3 (sessões)** — entrega o agregado mutável e a revogação por subject.
4. **Fase 4 (authorization codes)** — introduz a primeira primitiva de consumo atômico.
5. **Fase 5 (refresh tokens)** — trata a máquina de estado e concorrência de maior risco.
6. **Fase 6 (AP/cleanup/purge)** — fecha TTL, manutenção e remoção por realm.
7. **Fase 7 (PostgreSQL/operação/gateway)** — valida o alvo produtivo e completa runner/composição.
8. **Fase 8 (paridade/handoff)** — prova contratos, fluxos e gates do Plano 4.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
dotnet test Tests.Architecture
dotnet test Tests.Storage
dotnet test Tests.Identity
dotnet test Tests.Integration
dotnet test RoyalIdentity.sln
```

---

## Fase 1 - contratos, fronteiras e modelo Operational

**Depende de:** Q1–Q12 respondidas.

**Escopo:** criar o projeto puro, mappings neutros, modelo completo, seams do adapter e mudanças públicas antes de
implementar stores.

**O que/como:**

- Adicionar `RoyalIdentity.Data.Operational` à solução, referenciando somente EF Core/BCL.
- Criar `OperationalDbContext`, `OperationalModelOptions` e extensão pública de mapping fora do context.
- Modelar todas as entidades, relações internas e índices mínimos conforme Q1–Q3/Q7/Q8, sem FK cross-family.
- Criar accessor genérico `TContext : DbContext` no adapter; stores não dependem do context concreto.
- Fechar/implementar os contratos MP-2, MP-3 e MP-5 conforme Q4/Q5/Q11.
- Adicionar a opção de authorize interaction conforme Q6/Q12, incluindo copy constructor e cobertura do serializer
  de Configuration; tratar a mudança como aditiva sobre payload v1, sem migration relacional/bump automático.
- Adicionar `OperationalStorageOptions` e `RefreshTokenOptions` a `RealmOptions`, com defaults/clone/serialização
  definidos pelas DF29–DF32.
- Remover `Client.UpdateAccessTokenClaimsOnRefresh` do core e do materializador Configuration; preparar a migration
  de remoção da coluna conforme DF33, sem reescrever migrations do Plano 2.
- Criar serializers/materializers versionados com round-trip completo e falha fechada.
- Extrair/reusar primitivas genéricas de envelope/proteção do Plano 2 sem reutilizar
  `IKeyMaterialProtector` como contrato Operational.
- Criar resolver de profiles Operational nomeados e capturar no envelope o protector efetivamente usado.
- Centralizar os nomes/schemas de migrations history definidos pela DF23 para que runner, design-time factories e
  fixtures não possam divergir.
- Adicionar regras de arquitetura para impedir referências proibidas.

**Tarefas:**

- [ ] Atualizar solution/csproj e dependências.
- [ ] Criar entidades, DbSets, mappings, constraints e índices provider-neutral.
- [ ] Fixar FKs internas/vínculos lógicos da Parte 2 de Q1 e índices coerentes com as queries de cleanup de Q7/Q8.
- [ ] Fixar a topologia física da Parte 3 de Q1 e comprovar que todo store tipado inclui o discriminator quando
  houver tabela compartilhada.
- [ ] Criar `IOperationalStoreFactory` e seam de `DbContext` genérico.
- [ ] Alterar interfaces/consumidores somente até o ponto em que compilam; manter o comportamento nas fases por
  store.
- [ ] Aplicar a estratégia de compatibilidade Q11 sem dar aceites novos ao fake.
- [ ] Criar testes de model metadata, payload version e round-trip por tipo.
- [ ] Cobrir payload Configuration v1 anterior sem as novas options, defaults `Jwt=None`/`Claims=Current`, profile
  default e round-trip novo ainda em version `1`.
- [ ] Remover a opção do client em domínio/entity/mapping/materializers e testar leitura/migração de bancos com a
  coluna legada antes de seu drop.
- [ ] Testar profiles por realm: dois realms/protectors, rotação com leitor anterior, profile ausente, envelope/AAD
  adulterado e `Plain` explicitamente registrado.
- [ ] Testar `ClaimPayload` mínimo e provar que properties próprias de authorization code continuam preservadas.
- [ ] Fixar em teste a topologia das quatro histories da DF23 sem abrir conexão.
- [ ] Criar teste de context combinado de prova, inicialmente com mappings neutros.

**Critérios de aceite:**

- `Data.Operational` não referencia core, adapter, providers, host ou Configuration.
- Toda entidade possui realm na chave/índice aplicável e nenhuma FK cross-family.
- Um `DbContext` arbitrário aplica o mapping sem herdar de `OperationalDbContext`.
- Contratos MP-2/MP-3 não prometem atomicidade por uma implementação default não atômica.
- Realm options continuam round-trip após as novas opções; payload v1 anterior materializa os defaults sem migration
  relacional ou bump de payload version.
- Não resta opção de refresh claims no `Client`; somente o realm determina `Current`/`Snapshot`.
- Nenhum secret de protector entra no payload Configuration; profile inexistente nunca cai para `Plain`.
- Todo payload inválido/version desconhecida falha sem materialização parcial.

**Testes:**

```powershell
dotnet build RoyalIdentity.sln
dotnet test Tests.Architecture
dotnet test Tests.Storage --filter "FullyQualifiedName~OperationalModel|FullyQualifiedName~OperationalPayload|FullyQualifiedName~OperationalContractsShape"
```

### Resultado da Fase 1

*a preencher*

---

## Fase 2 - access tokens e consents sobre SQLite

**Depende de:** Fase 1.

**Escopo:** criar refinamentos/migration SQLite e implementar AT-01..AT-04 e CN-01..CN-03.

**O que/como:**

- Criar extensão pública `ApplyRoyalIdentityOperationalSqliteMappings` e context/design-time factory.
- Gerar migration inicial Operational SQLite sobre o model completo.
- Gerar nova migration Configuration SQLite que remove `update_access_token_claims_on_refresh`; não alterar a
  migration inicial já entregue pelo Plano 2.
- Configurar uma migrations history table exclusiva para Operational no SQLite; não reutilizar a history padrão de
  Configuration quando as famílias compartilham o arquivo/banco. Configuration passa a usar
  `__ConfigurationMigrationsHistory` e Operational usa `__OperationalMigrationsHistory`.
- Implementar o bootstrap SQLite da history Configuration legada `__EFMigrationsHistory` antes do primeiro
  `MigrateAsync` configurado com o novo nome.
- Implementar access-token store realm-bound create-only, lookup, remoção e remoção de reference tokens sobre a
  topologia escolhida em Q1.
- Aplicar `JwtAccessTokenPersistenceMode` na emissão: reference sempre persiste; JWT `None` não escreve,
  `Metadata` omite compact JWT e `Full` preserva o grafo completo protegido pelo profile do realm.
- Implementar consent store com upsert atômico por `(realm, subject, client)`.
- Criar harness SQLite Operational com migrations reais, nunca `EnsureCreated`.
- Reutilizar os contratos provider-neutral existentes e adicionar aceites de duplicidade/materialização/CT.

**Tarefas:**

- [ ] Aplicar collation/índices SQLite compatíveis com comparação Ordinal.
- [ ] Configurar e testar as histories SQLite de Configuration/Operational, inclusive no mesmo banco.
- [ ] Atualizar runner, design-time factories e fixtures SQLite para consumir a mesma configuração centralizada de
  history.
- [ ] Testar bootstrap SQLite: banco vazio; somente history legada; somente history nova; histories legada e nova
  simultâneas/ambíguas; repetição idempotente.
- [ ] Implementar materialização completa de `AccessToken` e `Consent`.
- [ ] Testar realms simultâneos com profiles de proteção e modos JWT diferentes sem cruzamento de policy/chaves.
- [ ] Mapear expiration como dado, sem query filter.
- [ ] Implementar remove em lote com contagem/efeito definido pela matriz.
- [ ] Testar ids colidentes entre realms e casings diferentes.
- [ ] Testar mutação do objeto materializado sem persistência implícita.
- [ ] Testar `CancellationToken` pré-cancelado e propagado ao comando EF.

**Critérios de aceite:**

- Duplicidade de access-token id no mesmo realm falha e não sobrescreve; em realm diferente é aceita.
- Reference token segue AT-01..AT-04 em todos os realms. JWT `None` não cria artifact; `Metadata` não contém o
  compact JWT; `Full` faz round-trip completo. Alterar a opção não reinterpreta linhas existentes.
- A migration Configuration remove a coluna legada do client e os fluxos não consultam mais essa propriedade.
- SQLite registra migrations Configuration/Operational apenas nas histories nomeadas da DF23; banco legado é
  realocado antes de o EF avaliar pending migrations, sem perder/duplicar ids.
- Lookup devolve token/consent expirado até cleanup explícito.
- Reference-token removal não remove JWT nem outro subject/client/realm.
- Consent concorrente não cria duas linhas; a última operação concluída é a efetiva.
- Scopes com casing distinto sobrevivem ao round-trip.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~AccessToken|FullyQualifiedName~Consent|FullyQualifiedName~SqliteOperational"
dotnet test Tests.Architecture
```

### Resultado da Fase 2

*a preencher*

---

## Fase 3 - sessões SSO sobre SQLite

**Depende de:** Fase 2.

**Escopo:** implementar SS-01..SS-06 e provar o comportamento de sessão definido pelas ADR-014/017.

**O que/como:**

- Implementar `IUserSessionStore` realm-bound sobre session + session-clients.
- Create é create-only e materializa todo o grafo.
- Record-client usa a PK composta para deduplicar e operação condicional para preservar `FirstSeenAt`/renovar
  `LastSeenAt`.
- End/touch/revogação por subject são set-based/condicionais, idempotentes e contam somente transições efetivas.
- Usar `TimeProvider` somente onde o contrato manda o store definir tempo; touch recebe timestamps do caller.

**Tarefas:**

- [ ] Implementar create/find/record/end/touch/end-by-subject.
- [ ] Preservar `SecurityStamp`, auth method/idp e expiração.
- [ ] Garantir no-op para record/touch ausentes.
- [ ] Evitar lost update entre record-client, touch e end.
- [ ] Testar clients case-sensitive, timestamps e rematerialização.
- [ ] Executar contratos existentes contra SQLite.

**Critérios de aceite:**

- Dois realms aceitam o mesmo sid sem interferência.
- Duplicidade de sid no mesmo realm falha visivelmente.
- Record concorrente do mesmo client deixa uma linha, mantém o primeiro `FirstSeenAt` e publica o maior/último
  `LastSeenAt` definido pela operação.
- End repetido não muda contagem/estado; revogação por subject respeita `exceptSessionId`.
- Lookup devolve sessão inativa/expirada enquanto não purgada.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~UserSession|FullyQualifiedName~SqliteOperational"
dotnet test Tests.Identity --filter "FullyQualifiedName~DefaultUserSessionService"
```

### Resultado da Fase 3

*a preencher*

---

## Fase 4 - authorization codes e consumo atômico

**Depende de:** Fases 1–3; Q4/Q11 fechadas.

**Escopo:** implementar AC-01..AC-03, MP-2 e migrar o fluxo de token.

**O que/como:**

- Implementar create-only/get/remove administrativo no adapter SQLite.
- Implementar a operação atômica Q4 sem filtrar expiração no lookup comum.
- Refatorar `LoadCode`/pipeline para que somente o vencedor prossiga.
- Preservar a ordem de segurança observável: vínculo client/redirect, consumo, expiração, PKCE e active-user
  conforme a decisão Q4 e o comportamento atual documentado.
- Não introduzir status OAuth diferente para ausente/já usado/vínculo inválido.

**Tarefas:**

- [ ] Adicionar aceite “dois consumers simultâneos, exatamente um sucesso”.
- [ ] Usar scopes/conexões independentes no teste.
- [ ] Confirmar que code expirado ainda é materializado e é consumido/rejeitado pelo pipeline conforme alvo.
- [ ] Manter remove administrativo ausente idempotente.
- [ ] Atualizar testes do pipeline/token endpoint.
- [ ] Documentar que revogação de tokens já emitidos por reuse de code não é adicionada neste plano.

**Critérios de aceite:**

- Nunca duas requests concorrentes chegam ao handler com o mesmo code.
- Invalid client/redirect não fornece oracle mais detalhado nem consome indevidamente conforme Q4.
- Falha de PKCE não permite segunda tentativa com o mesmo code se o comportamento consumptive atual for
  preservado.
- Fake continua transitório pelo caminho Q11; aceite de atomicidade roda somente em EF.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~AuthorizationCode|FullyQualifiedName~Atomic"
dotnet test Tests.Identity --filter "FullyQualifiedName~AuthorizationCode|FullyQualifiedName~Pkce"
dotnet test Tests.Integration --filter "FullyQualifiedName~AuthorizationCode"
```

### Resultado da Fase 4

*a preencher*

---

## Fase 5 - refresh tokens e transições condicionais

**Depende de:** Fase 4; Q5/Q10/Q11 fechadas.

**Escopo:** implementar RT-01..RT-05, MP-3 e reorganizar o handler para não emitir antes da transição exigida.

**O que/como:**

- Implementar create-only/get/remove/remove-by-subject.
- Persistir versão/estado conforme Q5 e realizar CAS por predicado + linhas afetadas.
- Mover a primeira transição para antes de efeitos de emissão que não podem ser revertidos.
- Separar claramente:
  1. lookup/materialização;
  2. validações de expiração/client/offline access/active user;
  3. transição atômica;
  4. política de tolerância;
  5. emissão e eventual atualização do token reutilizável.
- Remover a busca obrigatória do access token anterior por `AccessTokenId`.
- No modo `Current`, persistir no refresh somente o grant mínimo, reconstruir o principal protocolar a partir da
  sessão e reexecutar resolução de resources + emissão + `IUserClaimsProvider`. Scopes/resources atuais só podem
  restringir o conjunto originalmente autorizado, nunca ampliá-lo.
- No modo `Snapshot`, persistir dentro do refresh payload o snapshot necessário para reproduzir as claims; ainda
  validar account/session/client/expiração/consumo e nunca depender de uma linha do access token.
- Capturar `claims_mode` no refresh token emitido para que mudança posterior da opção do realm não altere a semântica
  de tokens existentes.
- Calcular `at_hash`, quando aplicável, sobre o novo access token devolvido na resposta; o compact JWT anterior não
  é requisito de refresh.
- Se Q10=A, preservar exatamente os resultados atuais de tolerância sem chamar repetição tolerada de detecção de
  replay.

**Tarefas:**

- [ ] Criar tipo de resultado mínimo definido em Q5.
- [ ] Implementar concorrência com contexts independentes.
- [ ] Tratar conflito sem retry cego de efeitos externos/emissão.
- [ ] Modelar o grant mínimo de `Current` com subject/session/client/scopes/resource URIs e contexto protocolar
  necessário (`auth_time`, `idp`, `amr` e futuro `acr` quando suportado), sem snapshot de profile claims.
- [ ] Modelar o snapshot próprio de `Snapshot` com `ClaimPayload` mínimo e sem compact JWT anterior.
- [ ] Remover `AccessTokenId` como dependência de leitura/retenção e cobrir refresh após cleanup do access token
  anterior.
- [ ] Testar que `Current` reflete remoção/adição de claims do UserAccounts somente dentro do grant original.
- [ ] Testar que `Snapshot` preserva as claims emitidas e que ambos os modos continuam aplicando active-user/session.
- [ ] Testar que dois realms com modos diferentes não compartilham policy.
- [ ] Testar expired/consumed lookup, tolerância zero, finita e infinita.
- [ ] Testar revogação ordinal por subject e isolamento por realm.
- [ ] Garantir que handle/payload nunca entra em log.

**Critérios de aceite:**

- Exatamente uma request observa a transição inicial `null → ConsumedTime`.
- Conflito não é convertido em sucesso silencioso.
- Quando Q10 preservar tolerância, eventual repetição tolerada usa estado consumido rematerializado depois do
  conflito; a perda do CAS isoladamente nunca autoriza emissão.
- A transição condicional nunca usa como estado esperado a mesma instância já mutada que será gravada; uma instância
  rematerializada ou versão persistida anterior deve falsificar o CAS trivial.
- Tolerância finita usa o timestamp persistido e `TimeProvider`, sem relógio do banco/processo divergente.
- Remove-by-subject retorna contagem efetiva e repetição retorna zero.
- Falha anterior à transição não cria token novo; falha posterior tem comportamento documentado e testado.
- `Current` é o default para payload RealmOptions v1 sem a nova propriedade; nenhuma opção do client interfere.
- Nenhum refresh válido exige que a linha do access token anterior ainda exista.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~RefreshToken|FullyQualifiedName~Atomic"
dotnet test Tests.Identity --filter "FullyQualifiedName~RefreshToken"
dotnet test Tests.Integration --filter "FullyQualifiedName~SessionRevocation"
```

### Resultado da Fase 5

*a preencher*

---

## Fase 6 - authorize parameters, cleanup e purge de realm

**Depende de:** Fases 2–5; Q2/Q6–Q9/Q12 fechadas.

**Escopo:** implementar MP-5/MP-6/parte Operational de MP-7 e completar o gateway SQLite.

**O que/como:**

- Tornar AP realm-bound em todos os callers.
- Gerar handle com ao menos 128 bits, persistir conforme Q2 e regenerar colisões por generator injetável/testável.
- Gravar `CreatedAt`/`ExpiresAt` absolutos; read repetível dentro da validade e `null` depois.
- Implementar cleanup em batches por tipo conforme Q7 e o modo de execução Q8.
- Limpar access-token artifacts por seu próprio expiry/estado/grace, sem `NOT EXISTS` contra refresh tokens; o
  refresh já conserva grant/snapshot suficiente segundo DF32.
- Implementar purge por realm pelo seam Q9.
- Compor um `IStorage` EF completo sobre Configuration EF + Operational SQLite + resources bridge.

**Tarefas:**

- [ ] Migrar login, consent, resolver e callback para o accessor realm-bound.
- [ ] Cobrir clone/round-trip de `NameValueCollection`, inclusive chaves repetidas se suportadas pelo shape atual.
- [ ] Injetar handle generator em teste para forçar colisão.
- [ ] Criar opções de cleanup validadas (intervalo, batch, retenções escolhidas).
- [ ] Implementar e testar índices alinhados aos predicados reais de cleanup global/realm-bound definidos por Q7/Q8.
- [ ] Implementar lazy AP cleanup sem transformar delete falho em retorno de payload expirado.
- [ ] Semear todas as tabelas em dois realms, purgar um e provar isolamento.
- [ ] Criar `EntityFrameworkStorage`/provider/session e testes de scope/disposal com dois DbContexts.

**Critérios de aceite:**

- Handle de AP em realm A não resolve em realm B.
- Alterar o TTL do realm não muda a expiração já gravada.
- Colisão nunca sobrescreve nem escapa como falha aleatória.
- Cleanup nunca remove refresh token ainda observável pela tolerância escolhida.
- Cleanup do access token anterior não invalida refresh token ainda observável.
- Purge é idempotente, abrange todas as tabelas Operational e não toca Configuration/UserAccounts.
- `IStorageSession` descarta ambos os contexts, sem commit/transação global.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~AuthorizeParameters|FullyQualifiedName~Cleanup|FullyQualifiedName~PurgeRealm|FullyQualifiedName~StorageSession"
dotnet test Tests.Identity --filter "FullyQualifiedName~AuthorizationContext"
dotnet test Tests.Integration --filter "FullyQualifiedName~Login|FullyQualifiedName~Consent|FullyQualifiedName~AuthorizeCallback"
```

### Resultado da Fase 6

*a preencher*

---

## Fase 7 - PostgreSQL, migrations, runner e gateway EF completo

**Depende de:** Fase 6.

**Escopo:** implementar refinamentos PostgreSQL, migrations/SQL, estender o runner e validar o gateway completo no
provider produtivo.

**O que/como:**

- Criar extensão pública `ApplyRoyalIdentityOperationalPostgreSqlMappings`, context e design-time factory.
- Usar schema `operation` e tipos/refinamentos equivalentes ao SQLite.
- Configurar explicitamente `configuration.__EFMigrationsHistory` e `operation.__EFMigrationsHistory`; o schema
  das entidades não é considerado configuração implícita suficiente.
- Implementar o bootstrap PostgreSQL que move a history Configuration legada do schema default para
  `configuration` antes de qualquer `MigrateAsync`.
- Gerar migration inicial Operational PostgreSQL e scripts SQL revisáveis/idempotentes conforme o padrão P2.
- Gerar nova migration Configuration PostgreSQL que remove `update_access_token_claims_on_refresh`, equivalente à
  SQLite e sem editar migrations anteriores.
- Estender `RoyalIdentity.Migrations` para selecionar famílias e aceitar uma ou duas conexões.
- Quando ambas apontam ao mesmo banco, aplicar Configuration e Operational sequencialmente; quando distintas,
  reportar falha por família sem sugerir atomicidade conjunta.
- Criar fixture PostgreSQL opt-in e script Podman com PostgreSQL 17 e porta host dinâmica diferente de 5432.
- Registrar gateway EF completo por provider como opt-in, nunca no host padrão.

**Tarefas:**

- [ ] Testar context separado e context combinado PostgreSQL.
- [ ] Testar histories distintas ao executar Configuration + Operational no mesmo banco SQLite e PostgreSQL.
- [ ] Atualizar runner, design-time factories e fixtures PostgreSQL para consumir a mesma configuração centralizada
  de history.
- [ ] Testar bootstrap PostgreSQL: banco vazio; somente history legada; somente history nova; ambas presentes/
  ambíguas; repetição idempotente e preservação integral dos migration ids.
- [ ] Implementar estratégia provider-specific de MP-2/MP-3 com a mesma semântica SQLite.
- [ ] Testar migration from-empty, pending model changes e SQL versionado.
- [ ] Testar upgrade Configuration do schema do Plano 2, preservando clients e removendo somente a coluna obsoleta.
- [ ] Regenerar/revalidar os scripts idempotentes Configuration contra a nova history e versionar os scripts de
  bootstrap sem alterar migrations de domínio já geradas.
- [ ] Testar runner: Configuration-only, Operational-only, ambas/mesmo banco e ambas/bancos distintos.
- [ ] Confirmar que o gateway com mesmo banco mantém dois DbContexts/conexões sem compartilhar transação.
- [ ] Garantir que Operational rejeita seed.
- [ ] Criar/estender script de PostgreSQL efêmero reutilizando o precedente local.
- [ ] Validar logs/erros redigidos.

**Critérios de aceite:**

- SQLite/PostgreSQL concordam em casing, duplicidade, ausência, TTL, contagens e concorrência.
- SQLite/PostgreSQL concordam nos três modos JWT, nos dois modos de claims do refresh e na seleção de protector por
  realm.
- Migrations não dependem da ordem de conexão entre famílias além da aplicação explícita do runner.
- PostgreSQL usa `configuration.__EFMigrationsHistory`/`operation.__EFMigrationsHistory`; SQLite usa
  `__ConfigurationMigrationsHistory`/`__OperationalMigrationsHistory`.
- History Configuration legada é realocada antes de o EF consultar pending migrations; ambiguidade falha fechado e
  nunca causa reaplicação de migrations ou merge/delete silencioso.
- SQL manual cria o mesmo model sem rodar host.
- Gateway completo resolve todos os membros de `IStorage`.
- Nenhuma extension de runtime chama migrate/seed.
- PostgreSQL 17 real passa contratos e aceites atômicos.

**Testes:**

```powershell
dotnet test Tests.Storage
dotnet test Tests.Storage --filter "FullyQualifiedName~OperationalMigrationRunner"
./scripts/Test-OperationalPostgreSql.ps1
```

### Resultado da Fase 7

*a preencher*

---

## Fase 8 - paridade, fluxos e fechamento

**Depende de:** Fase 7.

**Escopo:** executar gates completos, eliminar fallback acidental do adapter EF e preparar o Plano 4.

**O que/como:**

- Executar toda a contract suite contra SQLite e PostgreSQL opt-in.
- Executar os aceites exclusivos do P3 listados na matriz:
  - duplicidade create-only;
  - code single-use concorrente;
  - refresh transition concorrente;
  - AP realm/TTL/expiração/colisão;
  - cleanup e purge por realm;
  - profiles de proteção por realm, rotação/leitor anterior e falha fechada;
  - JWT `None`/`Metadata`/`Full` e refresh `Current`/`Snapshot`;
  - CT e disposal real.
- Exercitar ao menos um fluxo OIDC completo opt-in sobre o gateway EF sem mudar o default dos testes.
- Procurar acesso global a AP, get+remove como consumo de code, generic update de refresh no fluxo e
  `EnsureCreated`/`Migrate` no host.
- Atualizar matriz, macro, AGENTS/backlog apenas com resultados reais e diferidos confirmados.
- Produzir handoff do Plano 4 com grupos de testes e seeds necessários à troca de backing.

**Tarefas:**

- [ ] Rodar build/test focal e solução completa.
- [ ] Rodar PostgreSQL real.
- [ ] Inspecionar migrations e SQL por secrets/dados demo.
- [ ] Confirmar que adapter EF nunca usa o fallback Q11.
- [ ] Confirmar que fake não recebeu paridade Operational nova.
- [ ] Confirmar que `UpdateAccessTokenClaimsOnRefresh` e sua coluna não permanecem em core, entities,
  materializers, mappings, migrations novas ou código de runtime.
- [ ] Registrar contagem final de contratos/aceites e arquivos.
- [ ] Executar `git diff --check`.

**Critérios de aceite:**

- Todos os contratos preservados e aceites substitutos verdes em ambos os providers.
- Code/refresh concorrentes possuem resultado determinístico e falsificável.
- Full gateway EF é utilizável opt-in e não é default do host.
- Runner/SQL operam uma ou duas famílias sem auto-migrate.
- Matriz não contém MP-2/3/5/6/parte Operational de MP-7 pendentes.
- O Plano 4 pode migrar testes sem redesenhar persistence contracts.

**Testes:**

```powershell
dotnet build RoyalIdentity.sln
dotnet test Tests.Architecture
dotnet test Tests.Storage
dotnet test Tests.Identity
dotnet test Tests.Integration
dotnet test RoyalIdentity.sln
./scripts/Test-OperationalPostgreSql.ps1
git diff --check
```

### Resultado da Fase 8

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(ões) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| Persistir Operational | 1–7 | DF1–DF10/DF23/DF29–DF34 | schema/model/materialização/histories equivalentes | contracts SQLite/PostgreSQL |
| Access token + consent | 2, 6, 7 | DF7/DF8/DF13/DF14/DF17/DF30/DF31 | AT/CN completos, modos JWT, profiles e cleanup independente | contract + provider acceptances |
| Sessões | 3, 7 | DF15/DF27 | SS-01..SS-06, touch/revogação concorrentes | session contracts + ADR-017 regressions |
| Code single-use | 1, 4, 7 | DF11 + Q4/Q11 | um vencedor concorrente | atomic code acceptance |
| Refresh conditional | 1, 5, 7 | DF12/DF32–DF34 + Q5/Q10/Q11 | transição/versão/tolerância, Current/Snapshot e sem dependência do AT | atomic refresh + claims-mode acceptance |
| AP realm-bound + TTL | 1, 6, 7 | DF16/DF29/DF30 + Q2/Q6/Q12 | realm, expiração, colisão, fail-closed e payload Configuration compatível | AP acceptances |
| Cleanup/purge | 6, 7 | DF17/DF18 + Q7–Q9 | batch por tipo e purge isolado | cleanup/purge acceptances |
| Gateway/lifecycle | 6–8 | DF3/DF21/DF22 | todos os membros, scopes reais, sem UoW global | StorageSession/full harness |
| Migrations/operação | 2, 7, 8 | DF4/DF23/DF24/DF33 | histories por família, drop da opção do client, runner/SQL separados e uma/duas conexões | migration/runner/Podman |
| Handoff Plano 4 | 8 | DF21/DF25 | EF completo sem alterar default | solução + OIDC opt-in |

---

## Invariantes a preservar

1. `RoyalIdentity.Data.Operational` nunca referencia core, Configuration, adapter, provider, host ou UI.
2. Somente `RoyalIdentity.Storage.EntityFramework` traduz entidades Operational para modelos do core.
3. Toda operação é realm-bound; nenhuma chave/consulta/mutação cruza realm.
4. Configuration e Operational podem residir em bancos diferentes e nunca exigem FK/transação conjunta.
5. O gateway padrão usa um DbContext/conexão por família mesmo no mesmo banco; não compartilha conexão/transação.
6. Authorization code é single-use sob concorrência real.
7. Refresh transition nunca usa o CAS trivial “valor esperado = mesma instância mutada”.
8. A tolerância pós-consumo não é confundida com a primitiva de concorrência.
9. Artifacts efetivamente persistidos de access/refresh/code, consent e session expirados continuam legíveis até
   purge; AP expirado falha fechado.
10. Refresh nunca depende da linha do access token anterior; cleanup desse access token não invalida o grant.
11. Create-only nunca sobrescreve; consent upsert nunca duplica.
12. Removals/no-ops/counts seguem exatamente a matriz.
13. Materialização é independente e completa; nenhuma referência viva do EF escapa.
14. Comparadores são Ordinal, não defaults de collation.
15. Sessão preserva `SecurityStamp`, expiração, clients e semântica ADR-017.
16. Cleanup não é requisito para correção lógica de expiração.
17. Purge de realm não apaga tombstone/configuração nem chama UserAccounts.
18. `IStorageSession` é lifetime, não UoW global.
19. Todo I/O EF é async e propaga `CancellationToken`.
20. Host não executa migrations/seed e permanece in-memory por default neste plano.
21. PostgreSQL usa histories `configuration.__EFMigrationsHistory`/`operation.__EFMigrationsHistory`; SQLite usa
    `__ConfigurationMigrationsHistory`/`__OperationalMigrationsHistory`, com bootstrap legado antes do EF.
22. Resources/scopes continuam no bridge volátil.
23. Fake não ganha TTL, protection, cleanup ou paridade atômica.
24. Handles bearer, payloads, claims e subjects não aparecem em logs.
25. PAR, messages e replay cache não são assimilados por AP.
26. Reference access token é sempre persistido; JWT segue exclusivamente o modo do realm e `None` é o default.
27. Profile de proteção é selecionado por realm, não contém secrets e nunca cai silenciosamente para `Plain`.
28. Refresh `Current` nunca amplia scopes/resources do grant original; `Snapshot` nunca dispensa validação de
    account/session/client.
29. Claims Operational persistem apenas `Type`/`Value`/`ValueType`; qualquer expansão exige versão explícita.
30. Não existe configuração ou override de origem das claims do refresh no `Client`.

---

## Critérios globais de conclusão

- Q1–Q12 respondidas, convertidas em DFs e removidas como bloqueio antes da Fase 1.
- Oito fases concluídas com resultado, arquivos, desvios e comandos registrados.
- `RoyalIdentity.Data.Operational` puro e mappings aplicáveis a context customizado/combined.
- Todos os stores Operational possuem migrations SQLite/PostgreSQL e paridade comprovada.
- MP-2/MP-3 são atômicos sob requests concorrentes com DbContexts independentes.
- MP-5/MP-6 e o purge Operational de MP-7 estão completos.
- Gateway EF completo resolve todos os membros sem I/O síncrono oculto e sem transação cross-family.
- Runner/SQL suportam uma ou duas famílias, com a topologia exata de histories da DF23 e bootstrap legado seguro;
  host não migra.
- PostgreSQL 17 real validado ou a fase permanece incompleta.
- `dotnet build RoyalIdentity.sln` e `dotnet test RoyalIdentity.sln` verdes.
- `git diff --check` sem erros.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| Payload perde dado contratual | serializer não cobre grafo completo | token/consent/code muda ao ler | DF34 + versionamento/round-trip por modelo | Aberto |
| Option aditiva quebra payload Configuration v1 | serializer/default não materializa ausência | realms existentes falham ou mudam de comportamento | DF29 + fixture de payload v1 anterior sem a propriedade | Aberto |
| Bearer handle vaza pelo banco | valor bruto vira PK/log | credencial reutilizável | Q2 + redaction DF28 | Aberto |
| Protection/profile inviabiliza leitura | profile ausente, rotação remove leitor ou AAD diverge | outage por realm | DF30 + envelope/protector id + testes multi-profile/rotação | Aberto |
| Code é consumido duas vezes | get/remove ou transação fraca | emissão duplicada | MP-2 + teste com connections independentes | Aberto |
| Invalid request consome code | contrato atômico ignora vínculo/ordem | DoS contra fluxo legítimo | decisão Q4 + pipeline tests | Aberto |
| Refresh emite antes de ganhar CAS | handler mantém ordem atual | tokens órfãos/duplicados | reorganizar Fase 5 | Aberto |
| Refresh tolerance mascara replay | `TimeSpan.MaxValue`/janela ampla | divergência RFC 9700 | decisão Q10 + backlog explícito | Aberto |
| Lost update no refresh reutilizável | concorrência muda estado/payload após consumo | grant/token subsequente incorreto | versão condicional Q5 | Aberto |
| Session client/touch perdem update | JSON/replace do agregado concorrente | logout/idle incorretos | tabela filha + operações condicionais | Aberto |
| Cleanup remove dado observável | eligibility ignora tolerância/lifecycle | refresh/diagnóstico quebrado | política Q7 + fake clock tests | Aberto |
| Refresh volta a depender do access token | handler conserva `AccessTokenId`/lookup legado | cleanup invalida refresh válido | DF32 + teste AT removido antes do refresh | Aberto |
| Current amplia o grant | emissão usa permissões atuais do client em vez do grant persistido | elevação de privilégio | interseção scopes/resources + testes negativos DF32 | Aberto |
| Snapshot conserva claim revogada | modo escolhido reutiliza profile claim antiga | autorização obsoleta até expiração | default Current + escolha explícita/TTL do realm | Aberto |
| Realm seleciona Full + Plain | JWT bearer fica legível no banco | reutilização após leak | opt-ins independentes + warning + DF30/DF31 | Aberto |
| Drop da opção do client perde precedência implícita | clients antigos tinham valores divergentes | comportamento muda para Current | DF33 + migration/regressão/documentação explícita | Aberto |
| Cleanup nunca roda | modo externo sem scheduler | crescimento ilimitado | decisão Q8 + observabilidade | Aberto |
| Dois workers disputam cleanup | múltiplos nós | locks/carga | batches idempotentes e índices de expiry | Aberto |
| Purge cruza realm | filtro incompleto/cascade | incidente multi-tenant | realm em PK/FK + cenário abrangente | Aberto |
| Combined context diverge | mapping provider fica no context concreto | customização de terceiro quebra | extensões públicas + model tests | Aberto |
| SQLite passa, PostgreSQL falha | estratégia atômica/provider difere | falso sinal de produção | aceites reais PostgreSQL 17 | Aberto |
| Fake aparenta garantia EF | fallback não documentado | testes/default escondem corrida | Q11 + assert de que EF não usa fallback | Aberto |
| Runner sugere atomicidade conjunta | duas conexões falham parcialmente | operação confusa | resultado por família + sem transação distribuída | Aberto |
| Histories de migrations se misturam | providers dependem da history default ou configuram somente Operational | diagnóstico/rollback/scripts acoplados | topologia explícita da DF23 para ambas as famílias + teste same-database | Aberto |
| Mudança da history reaplica migrations Configuration | EF consulta o novo local antes de realocar a history legada | tentativa de recriar tabelas/indisponibilidade | bootstrap pré-`MigrateAsync`, preservação de ids, casos legado/novo/ambíguo e SQL manual | Aberto |
| SQL diverge da migration | model muda sem regenerar | deploy manual incompleto | pending-model/script tests | Aberto |

---

## Diferidos e backlog

- Troca do backing padrão dos testes/host e remoção do fallback transitório — `plan-data-test-migration.md`.
- Persistência/redesign de resources/scopes — plano específico após DF22.
- Coordenação idempotente de tombstone Configuration + purge Operational + UserAccounts — ADR/plano administrativo.
- API administrativa e write model — plano próprio.
- PAR/RFC 9126 e eventual `IAuthorizationRequestStore`/`IPushedAuthorizationRequestStore` —
  [an-par-rfc-9126.md](../analisys/an-par-rfc-9126.md) e backlog.
- Persistent `IMessageStore` e redesign atômico de `IReplayCache`.
- Cache Operational.
- Auditoria/outbox/forense durável.
- Refresh-token families, replay revocation e sender constraint se Q10=A.
- Aspire e agendamento/container de migrations/maintenance.
- Lookup de sessão por subject (MP-9), enquanto não houver caller.

---

## Referências

- [template-ai-implementation-plan.md](../references/template-plan/template-ai-implementation-plan.md).
- [plan-data-macro.md](plan-data-macro.md).
- [plan-data-storage-baseline.md](plan-data-storage-baseline.md).
- [plan-data-storage-matrix.md](plan-data-storage-matrix.md).
- [plan-data-configuration-storage.md](plan-data-configuration-storage.md).
- [plans-roadmap-02.md](plans-roadmap-02.md).
- [ADR-013](../../adrs/ADR-013.md).
- [ADR-014](../../adrs/ADR-014.md).
- [ADR-017](../../adrs/ADR-017.md).
- [ADR-018](../../adrs/ADR-018.md).
- [product.md](../foundation/product.md).
- [tech.md](../foundation/tech.md).
- [structure.md](../foundation/structure.md).
- [architecture.md](../foundation/architecture.md).
- [code-style.rules.md](../rules/code-style.rules.md).
- [an-par-rfc-9126.md](../analisys/an-par-rfc-9126.md).
- [RFC 6749](https://www.rfc-editor.org/rfc/rfc6749.html).
- [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700.html).
- [EF Core — ExecuteUpdate/ExecuteDelete](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete).
- [EF Core — Transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions).
- [EF Core — Custom Migrations History Table](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/history-table).
- [System.Text.Json — unmapped members](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members).
- `RoyalIdentity/Contracts/Storage/*.cs`.
- `RoyalIdentity/Users/Contracts/IUserSessionStore.cs`.
- `RoyalIdentity/Contexts/Decorators/LoadCode.cs`.
- `RoyalIdentity/Contexts/Decorators/LoadRefreshToken.cs`.
- `RoyalIdentity/Handlers/RefreshTokenHandler.cs`.
- `Tests.Storage/Storage/Contracts/*.cs`.
