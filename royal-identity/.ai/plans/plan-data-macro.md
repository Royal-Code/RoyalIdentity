# Macro-plano: Persistência de dados do IdP e aposentadoria do fake

## Status: EM EXECUÇÃO — Planos 0 e 1 concluídos; próximo: Plano 2

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
- O fake in-memory deixa de ser referência de longo prazo, mas só é removido quando o storage do core estiver pronto.

---

## Sequência recomendada

| Ordem | Plano a criar | Propósito |
|---|---|---|
| 0 | `plan-users-accounts-sqlite-hardening.md` | Fechar retry, migrations e seed do módulo `UserAccounts`. **CONCLUÍDO.** |
| 1 | `plan-data-storage-baseline.md` | Caracterizar contratos atuais e comportamento do `MemoryStorage`. **CONCLUÍDO (2026-07-22, 5/5 fases).** |
| 2 | `plan-data-configuration-storage.md` | Persistir dados de configuração do IdP. |
| 3 | `plan-data-operational-storage.md` | Persistir dados operacionais do IdP. |
| 4 | `plan-data-test-migration.md` | Migrar testes do fake para SQLite/EF + `UserAccounts` real. |
| 5 | `plan-data-caching.md` | Adicionar cache sobre os stores EF quando a semântica estiver estável. |
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

Os Planos 2 e 3 devem ser criados consumindo a matriz sem re-inferir semântica.

---

## Plano 2 - `plan-data-configuration-storage.md`

**Escopo:** dados de configuração de baixa rotatividade.

Projetos esperados:

- `RoyalIdentity.Data.Configuration`
- `RoyalIdentity.Storage.EntityFramework`
- `RoyalIdentity.Storage.EntityFramework.Sqlite`
- `RoyalIdentity.Storage.EntityFramework.PostgreSql`

Dados alvo:

- realms;
- realm options;
- clients;
- resources/scopes;
- signing keys e metadados de chaves enquanto KMS não existir.

Fases sugeridas:

1. Criar projeto `Data.Configuration` com entidades persistentes puras.
2. Mapear EFCore para SQLite primeiro.
3. Implementar stores de leitura/escrita de realm.
4. Implementar stores de clients e resources/scopes.
5. Implementar store de keys no escopo compatível com o core atual.
6. Adicionar provider PostgreSQL e migrations.
7. Rodar suíte de integração com configuration storage EF opt-in.

Fora de escopo:

- contas de usuário;
- sessões/tokens/codes/consents;
- UI/API administrativa;
- KMS completo.

---

## Plano 3 - `plan-data-operational-storage.md`

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

**Escopo:** trocar o backing dos testes sem misturar com o desenho dos stores.

Fases sugeridas:

1. Criar uma factory de testes com `Storage.EntityFramework.Sqlite` para o core.
2. Combinar essa factory com `UserAccounts` + SQLite como backing de contas.
3. Migrar a suíte OIDC por grupos: login/profile, authorize/token, refresh/revocation, logout/session, realm isolation.
4. Tornar SQLite/EF o default dos testes de integração.
5. Manter `MemoryStorage` apenas em testes específicos de fake, se ainda houver valor.
6. Remover o lado fake dos contract tests quando não houver mais contrato real a proteger.

Critério de aceite:

- suíte de integração verde sem depender de usuários fake;
- `MemoryStorage` não é mais o caminho principal de regressão;
- ADR-018 atualizada com o estado real.

---

## Plano 5 - `plan-data-caching.md`

**Escopo:** cache sobre stores EF já estáveis.

Fases sugeridas:

1. Classificar dados cacheáveis: discovery, realms, clients, scopes/resources e keys públicas.
2. Definir invalidação por atualização administrativa.
3. Implementar decorators de cache sobre `IStorage`/stores específicos.
4. Medir impacto em endpoints de discovery, authorize e token.
5. Adicionar testes de invalidação e isolamento por realm.

Não iniciar antes de:

- configuração EF estar estável;
- APIs administrativas ou mecanismo claro de update existirem.

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

## Critério para iniciar o primeiro plano de dados

Antes de criar `plan-data-storage-baseline.md`, concluir ou rebaixar formalmente:

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
