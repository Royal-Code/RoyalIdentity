# Macro-plano: Persistência de dados do IdP e aposentadoria do fake

## Status: PRIMEIRO CORTE CONCLUÍDO — Planos 0-4 concluídos; Planos 5/6 permanecem opcionais

Este documento organiza os próximos planos de dados após:

1. fechamento do `plan-users-security-lifecycle.md`;
2. execução do `plan-users-accounts-sqlite-hardening.md`;
3. decisão da ADR-018 de tratar o fake in-memory como transitório.

Ele **não** é um plano implementável único. É um mapa para evitar que um futuro
`plan-data-persistence.md` fique grande demais.

---

## Objetivo

Guiar a saída do IdP de `RoyalIdentity.Storage.InMemory` para persistência EFCore
com SQLite/PostgreSQL, preservando as fronteiras da ADR-013:

- `RoyalIdentity.Data.Configuration` e `RoyalIdentity.Data.Operational` são projetos de dados puros.
- `RoyalIdentity.Storage.EntityFramework` adapta `Data.*` às facades do core.
- `RoyalIdentity.UserAccounts` mantém persistência própria e não entra no storage EF do IdP.
- O fake in-memory foi removido depois que Configuration, Operational e a composição real ficaram prontas.

---

## Sequência recomendada

| Ordem | Plano a criar | Propósito |
|---|---|---|
| 0 | `plan-users-accounts-sqlite-hardening.md` | Fechar retry, migrations e seed do módulo `UserAccounts`. **CONCLUÍDO.** |
| 1 | `plan-data-storage-baseline.md` | Caracterizar contratos atuais e comportamento do `MemoryStorage`. **CONCLUÍDO (2026-07-22, 5/5 fases).** |
| 2 | `plan-data-configuration-storage.md` | Persistir dados de configuração do IdP. **CONCLUÍDO (2026-07-22, 7/7 fases).** |
| 3 | `plan-data-operational-storage.md` | Persistir dados operacionais do IdP. **CONCLUÍDO (2026-07-26, 8/8 fases).** |
| 4 | `plan-data-test-migration.md` | Migrar testes do fake para SQLite/EF + `UserAccounts` real. **CONCLUÍDO (2026-07-29, 9/9 fases).** |
| 5 | `plan-data-caching.md` | Adicionar cache sobre os stores EF quando a semântica estiver estável. **NÃO NECESSÁRIO no momento:** o essencial já está coberto (ver Plano 5). |
| 6 | `plan-data-audit-outbox.md` | Store durável de auditoria e outbox seletivo, se ainda fizer sentido. |

Se o trabalho precisar ser menor, as ordens 1 e 2 podem ser unidas. As ordens 5
e 6 devem ficar fora do primeiro corte de persistência.

---

## Plano 0 - `plan-users-accounts-sqlite-hardening.md`

**Escopo:** módulo `RoyalIdentity.UserAccounts`, não storage do core.

Fases:

1. Concorrência resiliente: retry real nos handlers, cumprindo ADR-017 §2.9.
2. Migrations dos providers `.Sqlite` e `.PostgreSql` do módulo.
3. Seed reutilizável Alice/Bob e ampliação da regressão opt-in.

Critério para avançar: `UserAccounts` com schema versionado, seed único e fluxo
de concorrência real testado. A partir daqui, o plano de dados do IdP não precisa
resolver pendências internas do módulo.

---

## Plano 1 - `plan-data-storage-baseline.md`

**CONCLUÍDO em 2026-07-22 (5/5 fases).** Saída entregue:

- [plan-data-storage-matrix.md](plan-data-storage-matrix.md) — catálogo completo (62 operações + suporte),
  ownership Configuration×Operational×Adapter, seeds/acessos diretos classificados e a seção
  "Paridade final e ordem de migração — Fase 5" com todas as semânticas fechadas (DF15-DF25),
  mudanças públicas MP-1..MP-10 e a ordem de migração por store para os Planos 2/3;
- `Tests.Storage` — contract suite provider-neutral (101 cenários verdes contra o fake) que os providers EF
  reutilizam adicionando apenas fixtures, mais a tabela de testes de aceite `substituir` (atômicos DF15,
  tombstone/reserva DF20, rejects create-only, authorize parameters realm-bound/TTL, normalização de domain,
  propagação de `CancellationToken`, disposal do adapter);
- gate do Plano 4 definido (o que precisa existir antes de trocar o backing default dos testes).

O Plano 2 foi concluído consumindo a matriz sem re-inferir semântica; o Plano 3 deve seguir a mesma regra.

---

## Plano 2 - `plan-data-configuration-storage.md`

**CONCLUÍDO em 2026-07-22 (7/7 fases, Q1-Q18/DF1-DF28 implementadas).**

Saída entregue: modelo Configuration puro, mappings/migrations SQLite e PostgreSQL, stores EF de
ServerOptions/realms/clients/keys, snapshot assíncrono defensivo, protectors explícitos, runner/seed/SQL separado
do host e paridade P2 validada contra PostgreSQL 17 real. Ao término do Plano 2, o host padrão permanecia
in-memory e o adapter não registrava um `IStorage` parcial. A composição produtiva completa
(`AddEntityFrameworkStorage`) foi entregue pelo Plano 3, e o default foi trocado pelo Plano 4.

**Escopo:** dados de configuração duráveis e de baixa rotatividade.

Projetos esperados:

- `RoyalIdentity.Data.Configuration`
- `RoyalIdentity.Storage.EntityFramework`
- `RoyalIdentity.Storage.EntityFramework.Sqlite`
- `RoyalIdentity.Storage.EntityFramework.PostgreSql`
- `RoyalIdentity.Migrations`

Dados alvo:

- `ServerOptions`;
- realms;
- realm options;
- clients;
- signing keys e metadados de chaves enquanto KMS não existir.

`Resources/scopes` não são persistidos pela DF22 do baseline: permanecem numa bridge volátil até o redesign.

Fases fechadas no plano executor:

1. Fronteiras, projetos e modelo extensível.
2. Modelo híbrido e provider SQLite.
3. Adapter, lifecycle e snapshot assíncrono.
4. ServerOptions, realms, clients e bridge de resources.
5. Proteção e persistência de signing keys.
6. PostgreSQL, migrations, runner e seeds.
7. Paridade, integração e fechamento.

Fora de escopo:

- contas de usuário;
- sessões/tokens/codes/consents;
- UI/API administrativa;
- KMS completo.

Decisões operacionais entregues por este plano: à época o host padrão ainda era in-memory; o IdP não escreve
Configuration; migrations e seed opcional rodam em `RoyalIdentity.Migrations`, nunca no host; SQL revisável é um
caminho disponível em produção. O default foi trocado posteriormente pelo Plano 4.

---

## Plano 3 - `plan-data-operational-storage.md`

**CONCLUÍDO (2026-07-26, 8/8 fases.)** Saída entregue: família Operational sobre EF nos dois providers, code
single-use e transição condicional de refresh sob concorrência real, authorize parameters realm-bound com TTL
absoluto, cleanup/purge sob modo de execução explícito, proteção de payload por realm, histories de migrations
separadas por família e o gateway `AddEntityFrameworkStorage()` completo. Ao término deste plano, o host e
`Tests.Integration` ainda eram in-memory e o fallback transitório continuava vivo; o Plano 4 removeu ambos.

**Escopo:** dados operacionais de alta rotatividade.

Projetos esperados:

- `RoyalIdentity.Data.Operational`
- extensões em `RoyalIdentity.Storage.EntityFramework`
- providers SQLite/PostgreSQL correspondentes

Dados alvo:

- sessões SSO;
- authorization codes;
- access/refresh tokens;
- consents;
- dados necessários para revogação por subject/sid/client.

Fases sugeridas:

1. Criar projeto `Data.Operational` com entidades persistentes puras.
2. Implementar session store EF, incluindo expiração, idle e revogação por subject.
3. Implementar stores de authorization code e token.
4. Implementar consent store com isolamento por realm/user/client/scope.
5. Definir limpeza/TTL operacional sem depender de cache.
6. Validar refresh-token tolerance e consumo single-use.
7. Rodar suíte OIDC com operational storage EF opt-in.

Pontos de atenção:

- realm isolation em toda query;
- operações de consumo/revogação precisam ser idempotentes;
- sessões e tokens têm volume e lifecycle diferentes dos dados de configuração.

---

## Plano 4 - `plan-data-test-migration.md`

**CONCLUÍDO em 2026-07-29 (9/9 fases).**

Saída entregue:

- `RoyalIdentity.Server` tornou-se um composition root exclusivamente PostgreSQL, com três conexões explícitas,
  Data Protection e provisionamento externo obrigatório;
- `RoyalIdentity.Demo` foi criado como executável zero-configuração sobre SQLite in-memory, com seed e conta reais,
  inteiramente efêmero;
- `RoyalIdentity.Migrations` passou a provisionar Configuration, Operational e UserAccounts, preservando conexão,
  history e resultado por família;
- `PersistentStorageAppFactory` tornou-se o default de `Tests.Integration`, com EF/SQLite + `UserAccounts`;
- as capabilities atômicas passaram ao contrato base, os consumers/fallbacks transitórios foram removidos e
  `RoyalIdentity.Storage.InMemory` foi excluído;
- PostgreSQL 17 real validou migrations, contratos, concorrência, gateway, startup do Server e authorization
  challenge OIDC; ADR-018 foi revisada com a consequência realizada.

---

## Plano 5 - `plan-data-caching.md`

**Escopo:** cache sobre stores EF já estáveis.

**Estado em 2026-07-29 — não criar este plano agora.** O que a fase 1 abaixo listaria como cacheável já está
coberto pelos Planos 2 e 3, e o caminho quente não bate no banco:

- **realms, clients e `ServerOptions`:** o snapshot de Configuration é cache em memória com publish atômico,
  intervalo de refresh, last-known-good em falha e reload após write de realm. Discovery lê de lá;
- **signing keys:** `RealmCaching`/`KeyCache` por realm, com TTL em `RealmOptions.Caching.KeyCacheDuration`;
- **resources/scopes:** já são memória, pela bridge volátil da DF22 — não há leitura de banco a evitar.

Sobra Operational, e é justamente onde cache é proibido: `ConsumeAuthorizationCodeAsync` (MP-2) e
`TryConsumeAsync`/`TryUpdateAsync` (MP-3) perdem a atomicidade com leitura stale, e cachear
`SecurityStamp`/`SessionsValidAfter` reabre a janela de revogação do ADR-017.

**Gatilho para revisitar:** um endpoint com latência medida e atribuída a acesso a dados — não a expectativa de
que cache seja bom por princípio. Sem esse número, o plano compra pouco e paga em risco de invalidação.

Fases sugeridas, se o gatilho ocorrer:

1. Classificar dados cacheáveis: discovery, realms, clients, scopes/resources e keys públicas.
2. Definir invalidação por atualização administrativa.
3. Implementar decorators de cache sobre `IStorage`/stores específicos.
4. Medir impacto em endpoints de discovery, authorize e token.
5. Adicionar testes de invalidação e isolamento por realm.

Não iniciar antes de:

- configuração EF estar estável;
- APIs administrativas ou mecanismo claro de update existirem — sem caminho de escrita não há invalidação a testar.

**Item que não herdou esta condicionalidade — já entregue:** a proteção real contra replay (RC-01/RC-02 da
matriz) saiu daqui e foi tratada por si, como requisito de segurança independente de performance, em
[plan-replay-protection.md](plan-replay-protection.md). Nenhum backing distribuído entrou no grafo: a
implementação durável vive na família Operational.

---

## Plano 6 - `plan-data-audit-outbox.md`

**Escopo:** durabilidade de auditoria e outbox seletivo.

Fases sugeridas:

1. Persistir entradas de `ISecurityAuditSink` com filtros por realm/categoria.
2. Definir retenção, consulta e índices de auditoria.
3. Decidir quais eventos precisam outbox de integração; não assumir que todo evento auditável vai para outbox.
4. Implementar tabela/outbox com idempotência e estado de entrega.
5. Criar publisher/dispatcher em processo ou worker, conforme decisão operacional.

Observação: este plano só deve existir se houver requisito de consulta durável,
integração externa ou entrega confiável. Auditoria em log pode continuar suficiente
por um tempo.

---

## Invariantes

1. Todo acesso a dados do IdP continua realm-scoped.
2. `RoyalIdentity.Data.*` não referencia `RoyalIdentity` core.
3. Só `RoyalIdentity.Storage.EntityFramework` adapta `Data.*` às facades do core.
4. `RoyalIdentity.UserAccounts` não é adaptado pelo storage EF do IdP.
5. SQLite é o provider principal para testes/dev; PostgreSQL é o alvo de produção.
6. Cache não muda semântica de storage; apenas envolve stores já corretos.
7. Outbox não deve ser criado como efeito colateral de auditoria sem decisão explícita.

---

## Gate histórico usado para iniciar o primeiro plano de dados

Antes de criar `plan-data-storage-baseline.md`, era necessário concluir ou rebaixar formalmente:

- achados restantes do `plan-users-security-lifecycle.md`;
- fases do `plan-users-accounts-sqlite-hardening.md`;
- estado do seed reutilizável do módulo;
- decisão sobre manter dual-run fake + módulo ou preparar flip incremental.

---

## Referências

- [plans-roadmap-01.md](plans-roadmap-01.md)
- [plan-users-security-lifecycle.md](plan-users-security-lifecycle.md)
- [plan-users-accounts-sqlite-hardening.md](plan-users-accounts-sqlite-hardening.md)
- [plan-users-accounts-module-v2.md](plan-users-accounts-module-v2.md)
- [backlog-001.md](../backlogs/backlog-001.md)
- [ADR-013](../../adrs/ADR-013.md)
- [ADR-017](../../adrs/ADR-017.md)
- [ADR-018](../../adrs/ADR-018.md)
