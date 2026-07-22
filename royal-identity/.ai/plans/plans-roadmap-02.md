# Roadmap de planos futuros (02)

> Substitui [plans-roadmap-01.md](plans-roadmap-01.md). Motivos: os itens 1 e 3 daquele roadmap foram concluídos
> (100% das fases); um plano de infraestrutura que não constava lá (`plan-royalidentity-security.md`) também
> concluiu; e o item 2 ("Persistência de Dados do IdP") foi refinado em [plan-data-macro.md](plan-data-macro.md),
> uma sequência de 7 sub-planos em vez de um único plano monolítico.

Este roadmap organiza os planejamentos que ficam depois dos planos já concluídos, para implementar a visão
definida em `../analisys/`.

## Concluído

- [plan-users-edge-session.md](plan-users-edge-session.md) — COMPLETED. Borda de usuários + sessão do IdP
  (`IUserDirectory`, `ISubjectStore`, `ILocalUserAuthenticator`, provider de claims e sessão).
- [plan-users-accounts-module-v2.md](plan-users-accounts-module-v2.md) — CONCLUÍDO, 10/10 fases. Módulo
  `RoyalIdentity.UserAccounts`: domínio rico de contas, propriedades dinâmicas por escopo, persistência própria
  EFCore + providers, integração opt-in com a borda do IdP, contract tests e seeds.
- [plan-users-security-lifecycle.md](plan-users-security-lifecycle.md) — CONCLUÍDO, 15 questões / 10 fases.
  Senha como credencial, lockout temporário/administrativo, troca/recuperação de senha, verificação de
  email/phone, password history/expiração, `SecurityStamp` e invalidação de sessão.
- [plan-royalidentity-security.md](plan-royalidentity-security.md) — CONCLUÍDO, 8/8 fases. Não estava no
  roadmap 01: extraiu `RoyalIdentity.Security` (crypto, hashing de senha, key material) como biblioteca
  compartilhada, removendo duplicação entre o core `RoyalIdentity` e o módulo `UserAccounts`. Ver
  [ADR-016](../../adrs/ADR-016.md).
- [plan-users-accounts-sqlite-hardening.md](plan-users-accounts-sqlite-hardening.md) — CONCLUÍDO, 3/3 fases.
  Nasceu da review-006 do `plan-users-security-lifecycle.md`, que achou lacunas entre o decidido e o implementado
  no backing do módulo `UserAccounts`: concorrência (retry só detecta, não resolve), migrations (só
  `EnsureCreated`) e seed (duplicado entre projetos de teste).
  1. **Concorrência resiliente (retry no handler)** — `[WithRetryOnConcurrency]` nos use cases de mutação pura
     de credencial; retry escopado manual nos fluxos com token (o consumo do token nunca re-executa);
     `AuthenticateLocalCredential` fora do retry (Q4), mas fail-closed; esgotamento mapeado para `typeId`
     `user_account.concurrency_conflict`; `ConcurrencyTests` reescrito contra os handlers reais, com conflitos
     genuínos (não simulados).
  2. **Migrations dos providers** (`.Sqlite`/`.PostgreSql`) — schema versionado por `IDesignTimeDbContextFactory`
     + migration inicial; correção manual da coluna de sistema `xmin` no provider PostgreSql; validado contra
     PostgreSQL 17 real via container Podman efêmero.
  3. **Seed reutilizável e módulo como backing de testes** — seed único (`Tests.UserAccounts/
     UserAccountsModuleSeed.cs`, linked em `Tests.Integration`) substituindo a duplicação Alice/Bob; regressão
     OIDC opt-in ampliada de 5 para 6 testes (Q9); flip completo do default para o módulo diferido para o
     `plan-data-macro.md`.

  Era também o **Plano 0** do `plan-data-macro.md` abaixo — suas três fases eram pré-requisito para o plano de
  dados do IdP não herdar pendências internas do módulo `UserAccounts`; pré-requisito agora satisfeito.
- [plan-data-storage-baseline.md](plan-data-storage-baseline.md) — CONCLUÍDO (2026-07-22), 5/5 fases. Sub-plano 1
  do `plan-data-macro.md`: inventário completo dos contratos de storage do IdP (62 operações),
  classificação Configuration×Operational×Adapter, contract suite provider-neutral `Tests.Storage`
  (101 cenários, reutilizável pelos providers EF), seeds/acessos diretos ao fake classificados com destino, e
  fechamento de todas as semânticas por operação (comparadores, duplicidade, expiração, ausência, ordem) na
  [plan-data-storage-matrix.md](plan-data-storage-matrix.md), com mudanças públicas MP-1..MP-10, ordem de
  migração por store e gates para os Planos 2/3/4.

## Em andamento

Nenhum plano ativo no momento. Próximo passo recomendado: criar o sub-plano 2 do `plan-data-macro.md`
(`plan-data-configuration-storage.md`), consumindo a matriz do baseline sem re-inferir semântica — ver
"Próximos planos" abaixo.

## Próximos planos

### 1. Persistência de Dados do IdP

**Plano-guia:** [plan-data-macro.md](plan-data-macro.md) (PLANEJADO — mapa, não implementável como plano único)

Substitui a descrição simples de "persistência de dados" do roadmap 01 por uma sequência de sub-planos, para
que nenhum deles fique grande demais:

| Ordem | Sub-plano | Propósito | Status |
|---|---|---|---|
| 0 | `plan-users-accounts-sqlite-hardening.md` | Retry, migrations e seed do módulo `UserAccounts` | Concluído (ver acima) |
| 1 | `plan-data-storage-baseline.md` | Caracterizar contratos e comportamento atual do `MemoryStorage` | Concluído (ver acima) |
| 2 | `plan-data-configuration-storage.md` | Persistir dados de configuração (realms/clients/resources/scopes/keys/options) | Não criado |
| 3 | `plan-data-operational-storage.md` | Persistir dados operacionais (sessions/tokens/codes/consents) | Não criado |
| 4 | `plan-data-test-migration.md` | Migrar testes do fake para SQLite/EF + `UserAccounts` real | Não criado |
| 5 | `plan-data-caching.md` | Cache sobre os stores EF, quando a semântica estiver estável | Não criado (pode ficar fora do primeiro corte) |
| 6 | `plan-data-audit-outbox.md` | Store durável de auditoria e outbox seletivo, se ainda fizer sentido | Não criado (pode ficar fora do primeiro corte) |

`RoyalIdentity.UserAccounts` mantém persistência própria e não entra neste storage EF do IdP (mesma fronteira
da ADR-013). Critério para avançar de 0 para 1: `UserAccounts` com schema versionado, seed único e concorrência
real testada.

### 2. Administração de Sessões por Dispositivo

**Plano sugerido:** `plan-session-administration.md`

Extensão do modelo operacional de sessão para administração pelo usuário ou por admin.

Escopo principal:

- Metadados de sessão: user agent, IP, device name e `LastSeenAt`.
- Listar sessões ativas por usuário/realm.
- Encerrar sessão específica ou encerrar outras sessões.
- Preservar integração com logout SSO front-channel/back-channel.

A sessão básica já é coberta pelo `plan-users-edge-session.md` (concluído). Este plano trata a camada
administrativa e operacional mais rica.

### 3. API e UI Administrativa

**Plano sugerido:** `plan-admin-api-ui.md`

Criação das APIs e telas administrativas, em projetos separados dos módulos de domínio.

Escopo principal:

- Administração de contas de usuário.
- Administração de realms, clients, resources/scopes e options.
- Reset de senha, ativar/desativar usuário e revogar sessões.
- Administração de propriedades dinâmicas por escopo.
- Integração com o realm `admin`.

Este plano depende das decisões de API/UI administrativa e deve respeitar a regra da ADR-013: módulos contêm
domínio + persistência; API e UI ficam separados.

### 4. Federation / Identity Brokering

**Plano sugerido:** `plan-federation-identity-brokering.md`

Autenticação por provedores externos configuráveis por realm.

Escopo principal:

- Modelo `ExternalIdentityProvider` por realm.
- Providers OIDC, social/corporativo e possivelmente SAML.
- Callback handlers realm-aware.
- Vinculação de identidades externas a contas do `UserAccounts`.
- Respeito às restrições de IdP por client.
- Emissão correta de `idp` e `amr`.

O `plan-users-edge-session.md` preparou a costura para métodos externos, mas não implementa federação.

### 5. MFA e Passwordless

**Plano sugerido:** `plan-auth-methods-mfa-passwordless.md`

Novos métodos de autenticação além de senha local.

Escopo principal:

- MFA por realm e por usuário.
- Passwordless e desafios temporários.
- Políticas por realm/client.
- Registro dos métodos em `amr`.
- Integração com login flow sem criar sessão antes da autenticação final.

Este plano depende do módulo de contas e do ciclo de credenciais já concluídos (ambos estão — ver "Concluído").

### 6. Key Management Service

**Plano sugerido:** `plan-kms.md`

Criação do módulo `RoyalIdentity.KMS` para gerenciamento de chaves, segredos e certificados. Ainda não existe
como projeto na solução.

Escopo principal:

- Domínio de chaves, certificados e segredos.
- Persistência própria.
- Rotação de chaves por realm.
- Integração com `IKeyStore`.
- API e UI administrativas em projetos separados.

Este plano é parte da arquitetura modular definida na ADR-013, mas pode ser priorizado independentemente dos
planos de dados/sessão/admin quando a operação de chaves virar requisito.

## Ordem recomendada

1. ~~Concluir `plan-users-accounts-sqlite-hardening.md` (Fases 1-3).~~ CONCLUÍDO.
2. ~~Criar e executar o sub-plano 1 do `plan-data-macro.md` (storage-baseline).~~ CONCLUÍDO. Criar e executar
   os sub-planos 2-4 (configuration-storage → operational-storage → test-migration); avaliar caching e
   audit-outbox (5-6) depois, só se ainda fizerem sentido no momento.
3. Evoluir administração de sessões por dispositivo.
4. Criar API/UI administrativa.
5. Avançar federação, MFA/passwordless e KMS conforme prioridade de produto.
