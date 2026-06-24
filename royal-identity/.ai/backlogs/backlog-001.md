# Backlog: RoyalIdentity

Itens identificados como válidos mas diferidos do planejamento ativo. Cada item tem uma justificativa de deferral e uma nota sobre o momento certo de atacá-lo.

---

## Gestão de Realms via API Administrativa

**Área:** Realm / Admin API
**Deferral:** A camada de domínio (`IRealmManager`) é criada no `plan-realm-hardening.md`. Endpoints REST e UI ficam para quando a demanda por administração remota for real.
**Quando revisitar:** Ao iniciar o desenvolvimento de APIs administrativas ou do painel admin.
**Nota de design:** Endpoints em `/{admin}/manage/realms/*` como Minimal APIs, no realm `admin` já existente como constante.

---

## UI Administrativa (realm `admin`)

**Área:** UI / Admin
**Deferral:** O realm `admin` existe como constante mas não tem páginas. Depende da API administrativa e de decisões de UX sobre o painel.
**Quando revisitar:** Junto com a API administrativa.

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
- **Demo logo ausente:** O plano pedia `DemoRealm.Options.Branding.LogoUri = "/images/demo-logo.png"` em `MemoryStorage.cs`. O ativo `/images/demo-logo.png` não existe em `wwwroot` — optou-se por não adicionar uma URI apontando para arquivo inexistente, pois renderizaria `<img>` quebrada. Quando o ativo existir (upload ou asset estático incluído), basta adicionar a linha em `MemoryStorage.cs` estático e o layout já o renderiza corretamente.
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
**Deferral:** `DefaultTokenFactory.CreateAccessTokenAsync` hardcoda `AccessTokenType.Jwt` (linha 71). Não há suporte a emissão de reference tokens (token opaco, armazenado no store com lookup por string opaca, não por JTI extraído do JWT). O modelo `AccessToken` já tem a propriedade `AccessTokenType`, e a infraestrutura de store já existe — falta apenas a lógica de derivação do tipo a partir de `Client.AccessTokenType` e a geração do token opaco no lugar do JWT assinado.
**Quando revisitar:** Quando houver demanda de clientes que não podem validar JWTs localmente, ou quando a introspection endpoint for implementada (que é o mecanismo de validação natural de reference tokens).
**Nota de design:**
- Derivar tipo: `var tokenType = request.Client.AccessTokenType;` em vez do valor fixo `AccessTokenType.Jwt`.
- Para `AccessTokenType.Reference`: gerar string opaca aleatória como `token.Token`, não assinar via `jwtFactory`. O token é armazenado no store e o `access_token` retornado ao cliente é a string opaca.
- Introspection endpoint (`/{realm}/connect/introspect`) é o mecanismo de validação para resource servers que recebem reference tokens.
- O teste `AccessTokenStoreIsolation_JtiFromRealmA_NotFoundInRealmB` testa isolamento do store por JTI; quando reference tokens forem implementados, adicionar teste específico com token opaco.

---

## Persistência de Dados (EFCore: Postgres/Sqlite) e Caching

**Área:** Storage / Persistência
**Deferral:** Hoje o único storage é `RoyalIdentity.Storage.InMemory` (fake/referência para dev/test/integração/demo). A produção precisa de módulos de dados com EFCore atrás das *facades* `IStorage.GetXStore(realm)`. Decidido em `an-users-arch.md` (arquitetura modular) — exige a própria ADR (ADR-013) e plano (`plan-data-persistence`). Era a seção "Planos futuros" do `plan-users-edge-session.md`.
**Quando revisitar:** Quando a persistência real (Postgres) virar requisito, ou para substituir o in-memory por Sqlite nos testes.
**Nota de design:**
- Projetos: `RoyalIdentity.Data.Configuration` (realms/clients/resources/keys/options) e `RoyalIdentity.Data.Operational` (sessions/tokens/codes/consents) — **dados puros, NÃO dependem do core**; `RoyalIdentity.Storage.EntityFramework` (+ `.PostgreSql`/`.Sqlite`, mapeamentos/migrations por provedor); `RoyalIdentity.Storage.Caching`.
- Só `Storage.EntityFramework` implementa as facades do IdP; `Data.*` contêm DbContext/entidades/queries. Divisão config × operacional por ciclo de vida/volume (TTL/cache no operacional).
- Esboço de fases: entidades/DbContext → mapeamentos por provedor → impl. das facades → cache → migração in-memory→Sqlite nos testes.

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
