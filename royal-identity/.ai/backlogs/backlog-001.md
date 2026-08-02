# Backlog: RoyalIdentity

Itens identificados como válidos mas diferidos do planejamento ativo. Cada item tem uma justificativa de deferral e uma nota sobre o momento certo de atacá-lo.

---

## OpenID Connect Session Management / Check Session — ✅ CONCLUÍDO

**Identificador:** `BL-OIDC-SESSION-MANAGEMENT`

**Área:** OpenID Connect / Sessão / Logout / Browser

**Status:** Promovido e concluído em 2026-08-01.

**Plano:** [plan-oidc-session-management.md](../plans/plan-oidc-session-management.md).

**Roadmap:** item “OpenID Connect Session Management e Check Session” de
[plans-roadmap-02.md](../plans/plans-roadmap-02.md).

**Resultado:** OP User Agent State opaco e realm-scoped, `session_state` origin-bound, `prompt=none` sem UI,
endpoint/discovery sob o mesmo gate HTTPS, iframe com Web Crypto/CSP por nonce e aceite Chromium cross-site
opt-in. A distribuição também preserva AGPLv3 para a obra combinada e as atribuições Apache-2.0 do material
derivado de IS4/IdentityModel.

---

## Aderência RFC 9700 e assessment de clients

**Identificador:** `BL-SEC-RFC9700`

**Área:** OAuth2/OIDC / Segurança / Clients / Admin

**Status:** Promovido a plano em 2026-07-30.

**Plano:** [plan-rfc9700-security-hardening.md](../plans/plan-rfc9700-security-hardening.md).

**Roadmap:** item “Aderência e hardening OAuth 2.0 conforme RFC 9700” de
[plans-roadmap-02.md](../plans/plans-roadmap-02.md); a apresentação dos findings também se relaciona ao item
“API e UI Administrativa”.

**Deferral original:** a base herdada/rearquitetada do IS4 mantém alguns comportamentos configuráveis que não
representam os defaults da BCP atual: redirect wildcard/ignore-case, implicit/hybrid e refresh token reutilizável.
O password grant já foi removido desde o início. Antes do Admin expor essas opções, o core precisa aplicar defaults
seguros, corrigir os requisitos não configuráveis e oferecer um diagnóstico derivado.

**Quando revisitar:** após concluir `plan-replay-protection.md`; executar antes ou em paralelo à fundação do Admin,
mas antes de publicar telas de configuração de clients.

**Decisões de design:**

- Não criar `OAuthSecurityProfile` ou preset equivalente.
- Criar `ClientSecurityAssessment` com factory method puro e findings identificados por `RuleId`.
- Não criar `IClientSecurityAssessor`, snapshot, cache ou persistência do assessment.
- Calcular o assessment a partir do `Client` e das opções efetivas do realm.
- Manter somente comparação case-insensitive e wildcard de redirect como relaxamentos configuráveis, ambos
  desabilitados por default e classificados como não aderentes.
- Corrigir PKCE downgrade, front-channel legado, replay de refresh, clickjacking, metadata e logs sem switches de
  compatibilidade.
- O futuro Admin calcula o resultado em leitura/após edição, localiza a apresentação por `RuleId` e nunca grava
  status/findings derivados.

---

## Gestão de Realms via API Administrativa

**Área:** Realm / Admin API
**Deferral:** A camada de domínio (`IRealmManager`) é criada no `plan-realm-hardening.md`. Endpoints REST e UI ficam para quando a demanda por administração remota for real.
**Quando revisitar:** Ao iniciar o desenvolvimento de APIs administrativas ou do painel admin.
**Nota de design:** Endpoints em `/{admin}/manage/realms/*` como Minimal APIs, no realm `admin` já existente como
constante. O CRUD de clients/realm deve consumir `ClientSecurityAssessment` conforme
[plan-rfc9700-security-hardening.md](../plans/plan-rfc9700-security-hardening.md), sem persistir o resultado.

---

## UI Administrativa (realm `admin`)

**Área:** UI / Admin
**Deferral:** O realm `admin` existe como constante mas não tem páginas. Depende da API administrativa e de decisões de UX sobre o painel.
**Quando revisitar:** Junto com a API administrativa.
**Relações:** item “API e UI Administrativa” de [plans-roadmap-02.md](../plans/plans-roadmap-02.md) e
[plan-rfc9700-security-hardening.md](../plans/plan-rfc9700-security-hardening.md).
**Nota de design:** Nas telas de client, calcular o assessment em tempo real; mostrar
`Compliant`/`Warning`/`NonCompliant`, localizar mensagens por `RuleId` e destacar opções de redirect relaxadas
como inseguras/não aderentes. Não criar tabela, snapshot ou campo persistido para assessment/findings.

---

## Key Management Service (KMS)

**Área:** Criptografia / Chaves
**Deferral:** A arquitetura já suporta chaves por realm (`GetKeyStore(realm)`). O plano é criar um módulo dedicado (projeto C# separado) tratando chaves, certificados, rotação, segredos — similar a um key vault.
**Quando revisitar:** Quando a rotação de chaves por realm virar requisito operacional ou quando iniciar o módulo de segurança dedicado.
**Nota de design:** Encaixa na arquitetura modular decidida em `an-users-arch.md` §3: módulo `RoyalIdentity.KMS` contém **domínio + persistência**; **API e UI em projetos separados** (não dentro do módulo). Plano futuro: `plan-kms` (domínio chaves/segredos/certificados → persistência → integração com `IKeyStore`).

---

## Federation / Identity Brokering (IdPs externos por realm)

**Área:** Autenticação / Federação
**Deferral:** Cada realm deveria poder configurar seus próprios IdPs externos (Google, GitHub, ADFS, OIDC genérico, SAML). Não há modelo de dados hoje. É uma feature de produto significativa.
**Quando revisitar:** Ao priorizar autenticação social/corporativa. Requer: modelo `ExternalIdentityProvider` por realm, callback handlers realm-aware, configuração de client_id/secret/discovery por realm.
**Nota de design:** É a extensão natural da auditoria de configurações por realm (Fase 7 do plan-realm-hardening).

---

## Realm Templates (copy-on-create)

**Área:** Gestão de Realms
**Deferral:** A máquina de cópia de `RealmOptions` (copy-on-create) foi adiantada no `plan-realm-options-redesign.md` como groundwork. O que resta é a feature em si, que só tem valor quando houver CRUD de realms via UI/API.
**Quando revisitar:** Junto com a UI administrativa.
**Nota de design:**
- **Já encaminhado no `plan-realm-options-redesign.md`:** deep-copy de `RealmOptions` via construtores de cópia (`RealmOptions(ServerOptions)` e `RealmOptions(RealmOptions)`); identidade do `Realm` sempre explícita (sem ctor de cópia de `Realm`); `ServerOptions` mantido como instância compartilhada.
- **Resta nesta feature:**
  - Modelo: `Realm.IsTemplate = true` — realms marcados como template não aceitam logins.
  - Operação "Criar realm a partir de template": wiring `IRealmManager.CreateAsync(..., Realm copyFrom)` usando `new RealmOptions(copyFrom.Options)` **+ deep-copy dos clients/resources/scopes** do template (toca os stores, fora do escopo do plano de options).
  - CRUD de realms via UI/API.
- Após criação, realm filho e template são independentes (sem herança live).
- Herança live (realm filho herdando dinamicamente do pai em runtime) foi avaliada e rejeitada por complexidade excessiva para o benefício obtido.

---

## Import/Export de Realm

**Área:** Gestão de Realms / Operações
**Deferral:** Backup, migração entre ambientes (dev → staging → prod). Valor claro mas não urgente.
**Quando revisitar:** Quando surgir demanda operacional real (ex: primeiro cliente de produção precisar migrar de ambiente).
**Nota de design:** JSON de exportação contendo: configuração do realm, clients, scopes/resources, usuários (sem senhas em plain text). Import valida conflicts (path/domain únicos).

---

## Enforcement de Quotas/Limites por Realm

**Área:** Configuração de Realm
**Deferral:** Os campos `MaxClients`, `MaxUsers`, `MaxActiveSessions` podem ser adicionados como nullable ao `RealmOptions` a qualquer momento (null = ilimitado). O enforcement (verificar ao criar) deve ser implementado junto com o CRUD de realms quando o storage tiver o `SaveAsync`.
**Quando revisitar:** Quando houver demanda SaaS (planos/tiers) ou necesidade de resource governance.
**Nota de design:**
- Campos: `int? MaxClients`, `int? MaxUsers`, `int? MaxActiveSessions` — todos nullable.
- Enforcement no `IRealmManager.CreateAsync` para entidades, e em `GetUserSessionStore` para sessões.
- Interface `IRealmLimitsPolicy` como ponto de extensão para implementações customizadas de enforcement.

---

## Branding Avançado por Realm

**Área:** UI / Configuração
**Deferral:** O básico (LogoUri, FaviconUri, PrimaryColor) é implementado no `plan-realm-hardening.md`. O avançado fica para depois.
**Quando revisitar:** Quando houver demanda de white-labeling ou quando stakeholders priorizarem customização visual.
**Nota de design:**
- **Demo logo ausente:** o antigo seed fake mencionava `/images/demo-logo.png`, mas o ativo não existe em
  `wwwroot`; por isso o seed atual do `RoyalIdentity.Demo` não anuncia uma imagem quebrada. Quando houver asset ou
  upload real, a configuração deve entrar no owner de branding/seed vigente — `MemoryStorage` foi removido.
- **Upload de imagens:** logo e favicon via upload (armazenados por realm), não apenas URI externo. Requer endpoint de upload e storage de assets.
- **CSS injetável:** campo `string? CustomCss` em `RealmBrandingOptions` — CSS injetado em `<style>` no layout, permitindo override de qualquer estilo. Sem sanitização obrigatória (é configuração de admin).
- **Theming avançado (Keycloak-style):** motor de templates HTML/CSS com themes por realm — escopo alto, avaliar quando a base de usuários justificar.

---

## Herança Live de Realm (Parent/Child)

**Área:** Gestão de Realms
**Deferral:** Modelo `Realm.ParentRealmId` com merge de options em runtime. Avaliado e rejeitado para o médio prazo: a complexidade de override/fallback em runtime e a definição de "o que propaga quando o template muda" não justifica o benefício para a maioria dos casos de uso. O modelo copy-on-create (descrito em Realm Templates) cobre 90% dos casos.
**Quando revisitar:** Somente se houver demanda explícita de herança dinâmica (ex: dezenas de realms que precisam mudar em sincronia).

---

## Reference Token (AccessTokenType.Reference) no DefaultTokenFactory

**Área:** Tokens / DefaultTokenFactory

**Status:** PROMOVIDO A PLANO em 2026-07-30; implementação não iniciada.

**Plano:** [plan-reference-tokens-introspection.md](../plans/plan-reference-tokens-introspection.md)
(RASCUNHO — 0/7 fases; Q1/Q2 pendentes antes da Fase 5).

**Estado verificado:** a infraestrutura de persistência e validação já entende `AccessTokenType.Reference`:
o store EF usa digest realm-scoped do handle, preserva o tipo, remove somente reference tokens e possui testes de
isolamento/paridade; o bearer pipeline valida tokens opacos e a integração prova esse caminho com um reference token
semeado diretamente no store. A emissão, entretanto, continua ausente:

- `DefaultTokenFactory.CreateAccessTokenAsync` ainda cria `AccessTokenType.Jwt` de forma fixa;
- a emissão a partir de refresh token em `RefreshTokenHandler.IssueFromSnapshotAsync` também fixa JWT;
- `Client` ainda não possui `AccessTokenType`, portanto faltam modelo, persistência Configuration, materialização e
  defaults;
- o teste de integração de bearer declara explicitamente que precisa semear o reference token porque a emissão só
  produz JWT;
- não existe endpoint de introspection implementado.

**Escopo promovido:** implementar a configuração por client, gerar handle opaco criptograficamente aleatório, aplicar o
mesmo tipo na emissão inicial e por refresh, nunca assinar o reference token, devolver o handle como `access_token`
e ampliar os testes de emissão/renovação/revogação. O plano também inclui Token Introspection RFC 7662, conforme
o destino conjunto já registrado por `plan-refactoring-debt-closure.md`.

**Quando executar:** após `plan-rfc9700-security-hardening.md`, para consumir a remoção do front-channel legado e
a rotação final de refresh token sem editar os mesmos handlers em paralelo.

**Nota de design:**
- Adicionar e persistir `Client.AccessTokenType`, com JWT como default seguro/compatível com o comportamento atual.
- Derivar o tipo de `request.Client.AccessTokenType` na emissão inicial e na renovação por refresh token.
- Para `AccessTokenType.Reference`: gerar string opaca aleatória como `token.Token`, não assinar via `jwtFactory`. O token é armazenado no store e o `access_token` retornado ao cliente é a string opaca.
- Introspection endpoint (`/{realm}/connect/introspect`) é o mecanismo de validação para resource servers que recebem reference tokens.
- Reaproveitar a cobertura já existente de store/bearer e adicionar emissão ponta a ponta, renovação, revogação,
  expiração, concorrência e isolamento por realm/client com handles opacos.

---

## Persistência EFCore do primeiro corte (PostgreSQL/SQLite) — ✅ CONCLUÍDO

**Área:** Storage / Persistência

**Status:** CONCLUÍDO em 2026-07-29 pelos Planos 0-4 do
[plan-data-macro.md](../plans/plan-data-macro.md): Configuration e Operational usam EFCore
SQLite/PostgreSQL, o gateway é completo, o runner provisiona também UserAccounts, o Server usa PostgreSQL e os
testes default usam EF/SQLite + módulo real. O fake foi removido.

**Entregue:**

- Entregues: `RoyalIdentity.Data.Configuration`, `RoyalIdentity.Storage.EntityFramework`, providers
  `.PostgreSql`/`.Sqlite` e `RoyalIdentity.Migrations`. Resources/scopes permanecem voláteis por DF22; não são
  entidades de `Data.Configuration`.
- Entregues também: `RoyalIdentity.Data.Operational`, gateway completo, migração dos testes e remoção do fake.
- `RoyalIdentity.Server` é PostgreSQL-only e externamente provisionado; `RoyalIdentity.Demo` usa SQLite in-memory;
  a suíte provider-neutral usa SQLite por default e mantém aceites PostgreSQL opt-in.
- Só `Storage.EntityFramework` implementa as facades do IdP; `Data.*` contêm DbContext/entidades/queries.

**Limite do corte:** o modelo de domínio de resources/scopes está concluído; sua persistência continua diferida
por DF22 e deve ser tratada no futuro `plan-data-resource-catalog-storage.md`, ainda não criado. Isso não reabre o
primeiro corte concluído.

---

## Caching sobre os stores EF

**Área:** Storage / Performance

**Status:** DIFERIDO — não necessário no momento.

**Estado verificado:** não existem `RoyalIdentity.Storage.Caching` nem
`.ai/plans/plan-data-caching.md`. O snapshot assíncrono de Configuration resolve consumidores síncronos e
invalidação de options, mas foi deliberadamente definido como projeção técnica, não como cache geral dos stores.

**Quando revisitar:** somente se medições demonstrarem benefício e depois de existir um mecanismo administrativo
claro de invalidação/versão para os dados de Configuration. Stores Operational exigem análise própria por causa de
TTL, consumo atômico, revogação e concorrência; um cache não pode alterar essas semânticas.

**Nota de design:**

- Criar plano próprio antes de implementar o projeto/adapters de caching.
- Cache deve envolver stores EF já corretos, manter isolamento por realm e nunca substituir decisões atômicas do
  banco.
- Invalidação administrativa, comportamento multi-instância, observabilidade e testes de stale reads são gates,
  não detalhes deixados para a implementação.

---

## Aspire e orquestração de ambiente

**Área:** Host / Operação / Developer Experience
**Deferral:** A composição local foi entregue em `Aspire/Aspire.AppHost`: PostgreSQL 17, três databases, execução
de `RoyalIdentity.Migrations` como job e startup do Server condicionado ao sucesso do runner. O aceite integral
permanece opt-in em `Aspire.Tests`.
**Quando revisitar:** Quando novos hosts/dependências precisarem entrar no AppHost ou quando houver desenho de
deployment além do ambiente local.
**Nota de design:**
- O AppHost Aspire deve subir bancos e demais dependências e executar `RoyalIdentity.Migrations` como
  workload/container separado antes dos hosts.
- Hosts do IdP, APIs administrativas e demais aplicações nunca executam migrations implicitamente.
- Configuration e Operational podem usar bancos distintos ou o mesmo banco; a orquestração deve aceitar ambos.
- Seed continua opt-in e separado de migrate; ambientes produtivos podem preferir os scripts SQL revisados.

---

## Módulo de Contas de Usuário (RoyalIdentity.UserAccounts)

> **Promovido a plano ativo (2026-06-17):** este item deixou de ser apenas backlog — virou
> [ADR-015](../../adrs/ADR-015.md) + [plan-users-accounts-module-v2.md](../plans/plan-users-accounts-module-v2.md)
> (módulo **`UserAccounts`** singular, projeto **`.Integration`** separado, provedores **`.PostgreSql`**, seam
> **`IUserClaimsProvider`**/`Claim`). A nota abaixo é o registro original do deferral.

**Área:** Usuários / Módulo de domínio
**Deferral:** O `plan-users-edge-session.md` refatora só a **borda + sessão** do IdP atrás de *facades* (`IUserDirectory`/`ISubjectStore`/`ILocalUserAuthenticator`/`IUserClaimsProvider`), com backing in-memory. O **domínio rico de contas** vira um módulo próprio fora da biblioteca do IdP, implementando essas facades. Decidido em `an-users-arch.md` — fechado em ADR-015 + `plan-users-accounts-module-v2`.
**Quando revisitar:** Quando a borda+sessão (`plan-users-edge-session.md`) estiver concluída e houver demanda por gestão real de contas (CRUD, recuperação de senha, admin).
**Nota de design:**
- Requisitos (de `an-users-pontos2.md` §4): dados OIDC obrigatórios; usuários/config por realm; email opcional/múltiplo/fictício; ID externo; **propriedades dinâmicas por escopo** ancoradas nos Identity Scopes (projetadas em claims via `IUserClaimsProvider`); credenciais (senha + futuro MFA/externo/passwordless) e lockout por realm; casos de uso administrativos; eventos de domínio; Inbox/Outbox; replicação entre instâncias.
- Persistência própria (EFCore). **API e UI em projetos separados** (não dentro do módulo).
- Relaciona-se com "Federation / Identity Brokering" (identidades externas vinculadas à conta).
- Esboço de fases: modelo (emails/ID externo/propriedades por escopo) → credenciais/MFA → casos de uso admin → eventos/inbox-outbox → replicação → integração com as facades de borda.

---

## Projeto compartilhado de segurança (RoyalIdentity.Security) — ✅ CONCLUÍDO

**Status:** CONCLUÍDO (2026-06-22)

**Área:** Segurança / Infra compartilhada

**Implementação:** Projeto `RoyalIdentity.Security` criado e entregue (ADR-016 + plan-royalidentity-security.md).

**Resultado final:**
- **Componentes entregues:** CryptoRandom, Base64Url, HashExtensions, FixedTimeComparer, PasswordHash (com formato $RIPWD$ reutilizável), KeyParameters, ECKeyHelper, SecurityKeyExtensions
- **Consumidores migrados:** RoyalIdentity (core), RoyalIdentity.Storage.InMemory, RoyalIdentity.UserAccounts
- **Testes:** Tests.Security com 116 testes aprovados
- **Build:** 0 erros, 9 warnings pré-existentes
- **Suites amplas (Fase 8):** 440/440 testes aprovados
  - Tests.Security: 116/116
  - Tests.Identity: 13/13
  - Tests.Pipelines: 3/3
  - Tests.UserAccounts: 91/91
  - Tests.Integration: 202/202
  - Tests.Architecture: 15/15

**Fases executadas:**
1. Esqueleto, guardrails e estrutura
2. Primitivas: random, encoding, hashing, comparação constante
3. Password hashing reutilizável ($RIPWD$ versionado)
4. Key material: KeyParameters, ECKeyHelper, extensões
5. Migração do core (RoyalIdentity)
6. Migração de módulos (UserAccounts, Storage.InMemory)
7. Duplicação removida — tipos migrados removidos do core, sem shims delegadores
8. Validação ampla e documentação — projeto entregue

**Manutenção futura:** Nenhuma — projeto completo. Possível extensão apenas se KMS ou novos módulos tiverem requisitos de segurança não cobertos.

---

## Rehash-on-login de hashes de senha (orquestração)

**Área:** Segurança / Contas de usuário
**Status:** Não aplicável no momento. A detecção de rehash e o resultado tri-estado foram **removidos**: há um único
formato versionado (`$RIPWD$`) e nenhum legado de produção a migrar (o formato pré-release `$PBKDF2$` foi descartado
antes de qualquer release). Sem legado e com um só conjunto de parâmetros, não há cenário em que um hash armazenado
precise ser regravado. `PasswordHash.Verify` retorna `bool`.
**Quando revisitar:** Apenas se um upgrade futuro de parâmetros PBKDF2 (ex.: aumentar iterações ou migrar algoritmo)
tornar hashes existentes mais fracos que a política corrente. Nesse momento, introduzir:
1. uma primitiva de detecção na lib de segurança (pura, sem conhecer realm);
2. a orquestração no domínio de contas (`UserAccounts` / `IPasswordProtector`): ao autenticar com sucesso, se
   a detecção indicar rehash, chamar `Create(password, currentOptions)` e persistir o novo hash na mesma transação de login.
**Nota de design:**
- Como só é possível regravar com a senha em mãos, a adoção é naturalmente *on-login* (não há migração em lote).
- O consumidor decide a política (`PasswordHashOptions`) por realm; a primitiva não conhece realm.

---

## Restrições de acesso por realm/grupo (Geo/Time/Client)

**Área:** Segurança / Contas de usuário / Autorização
**Deferral:** Surgiu na Q5 do [plan-users-security-lifecycle.md](../plans/plan-users-security-lifecycle.md). O plano de
*security lifecycle* cobre apenas o **bloqueio administrativo pessoal** (`UserAccountBlockState` com janela
`StartsAt`/`EndsAt`) e o **lockout por falha** (`PasswordLockout`, derivado da credencial). A proposta inicial de uma
tabela `AccountAccessRestriction` **per-account** para Geo/Time/Client foi **aposentada**: o autor avaliou que essas
restrições aplicam-se a **todos os usuários ou a grupos**, não por conta, e exigem um **design multifuncional próprio**.
**Quando revisitar:** Quando houver demanda de restrição por geolocalização, janela de horário ou client/app — e/ou junto
do **motor de permissões** futuro (funções administrativas gated por permissões).
**Nota de design:**
- Modelar como **políticas por realm/grupo**, não como linhas por conta.
- Tipos previstos: `GeoBlock`, `TimeWindow`, `ClientRestriction`.
- Relaciona-se com o futuro motor de permissões (quem pode aplicar/remover restrições) e com a UI/API administrativa.

---

## Substituir o storage fake in-memory pelo módulo + Sqlite in-memory nos testes

**Área:** Testes / Storage
**Estado:** CONCLUÍDO em 2026-07-29 pelo
[plan-data-test-migration.md](../plans/plan-data-test-migration.md), conforme a revisão da
[ADR-018](../../adrs/ADR-018.md). `Tests.Integration` usa EF/SQLite + `UserAccounts`; as facades, contracts
concretos e o projeto `RoyalIdentity.Storage.InMemory` foram removidos.
**Registro histórico:**
- **Primeiro passo (habilitador) — ✅ CONCLUÍDO** (`plan-users-accounts-sqlite-hardening.md` Fase 3, Q8): o **seed
  reutilizável do módulo** existe em `Tests.UserAccounts/UserAccountsModuleSeed.cs` — Alice/Bob determinísticos
  (`sub`, username, displayName, email verificado, roles, property scopes `profile`/`email`), idempotente. É
  *linked* (não `ProjectReference` teste-para-teste) em `Tests.Integration` e substituiu as antigas cópias de
  `UserAccountsAppFactory` e do contrato SQLite.
- **Migração concluída:** o seed reutilizável tornou-se parte da fixture persistente; a suíte integral substituiu a
  antiga regressão opt-in representativa, e os contratos restantes rodam somente contra implementações reais.
- **Comportamentos do ciclo de segurança** (required action, security-state/`SessionsValidAfter`, verificação) deixam de
  ser "module-only por falta de fake" e passam a poder subir ao contrato — agora com as duas pontas sendo módulo+Sqlite
  (ou módulo vs. providers `.Sqlite`/`.PostgreSql`), não fake vs. módulo.
- O lado de **storage do core** (realms/clients/keys/sessions/tokens) concluiu a mesma migração para EF/SQLite e
  PostgreSQL no primeiro corte do macro-plano.

---

## Pushed Authorization Requests (PAR / RFC 9126)

**Área:** OAuth2/OIDC / Authorization endpoint / Storage operacional

**Status:** PROMOVIDO A PLANO em 2026-07-30; implementação não iniciada.

**Análise:** [an-par-rfc-9126.md](../analisys/an-par-rfc-9126.md) inventaria os requisitos do RFC 9126, o estado
dos contratos atuais e as alternativas de store. O plano executável é
[plan-pushed-authorization-requests.md](../plans/plan-pushed-authorization-requests.md)
(RASCUNHO — 0/7 fases; Q1/Q2/Q3 pendentes antes da Fase 1).

**Escopo promovido:** endpoint PAR direto autenticado, validação antecipada e revalidação no authorization
endpoint, referência opaca com 256 bits de entropia, binding realm/client, TTL, consumo atômico, payload
Operational protegido, cleanup, policy global/por client, discovery e paridade SQLite/PostgreSQL.

**Quando executar:** depois de
[plan-reference-tokens-introspection.md](../plans/plan-reference-tokens-introspection.md), conforme
[plans-roadmap-02.md](../plans/plans-roadmap-02.md). Isso serializa mudanças em `Client`, Configuration,
Operational, autenticação direta e discovery.

**Decisões abertas:** Q1 fecha facade específica versus facade geral com famílias de operações; Q2 fecha
single-use estrito versus reload com binding server-side; Q3 fecha lifetime default/faixa. O plano mantém
`IAuthorizeParametersStore` como continuação interna repetível, descarta `IMessageStore` e
`IReplayProtectionStore` como backing de PAR e deixa JAR/JARM para plano próprio.

---

## Replay cache com proteção real (check+add atômico) — ✅ CONCLUÍDO

**Status:** CONCLUÍDO (2026-07-30) por [plan-replay-protection.md](../plans/plan-replay-protection.md).

**Área:** Segurança / `private_key_jwt` / Operational

**Resultado:** `IReplayCache` e as duas implementações antigas foram removidas. O contrato é
`IReplayProtectionStore`, com uma única operação atômica `TryAddAsync(realmId, issuer, purpose, handle,
expiration, ct)`. Não há registro default: cada composition root declara `AddInMemoryReplayProtection()` ou
`AddOperationalReplayProtection()`, e o startup falha em qualquer ambiente se nenhuma, duas ou uma inconsistente
for declarada. O Server usa a durável sobre `replay_handles`; o Demo, a in-memory. `Redis` e demais backings
distribuídos continuam fora — entram como extension adicional sobre o mesmo contrato quando existir deployment
que precise, e nenhum pacote de cache distribuído entrou no grafo.
