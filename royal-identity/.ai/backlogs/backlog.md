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

---

## Federation / Identity Brokering (IdPs externos por realm)

**Área:** Autenticação / Federação
**Deferral:** Cada realm deveria poder configurar seus próprios IdPs externos (Google, GitHub, ADFS, OIDC genérico, SAML). Não há modelo de dados hoje. É uma feature de produto significativa.
**Quando revisitar:** Ao priorizar autenticação social/corporativa. Requer: modelo `ExternalIdentityProvider` por realm, callback handlers realm-aware, configuração de client_id/secret/discovery por realm.
**Nota de design:** É a extensão natural da auditoria de configurações por realm (Fase 7 do plan-realm-hardening).

---

## Realm Templates (copy-on-create)

**Área:** Gestão de Realms
**Deferral:** Permite criar novos realms a partir de um template. Complexidade baixa (copy-on-create), mas só tem valor quando houver CRUD de realms via UI/API.
**Quando revisitar:** Junto com a UI administrativa.
**Nota de design:**
- Modelo: `Realm.IsTemplate = true` — realms marcados como template não aceitam logins.
- Operação: "Criar realm a partir de template" = deep-copy das `RealmOptions` + deep-copy dos clients/resources/scopes do template.
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
- **Upload de imagens:** logo e favicon via upload (armazenados por realm), não apenas URI externo. Requer endpoint de upload e storage de assets.
- **CSS injetável:** campo `string? CustomCss` em `RealmBrandingOptions` — CSS injetado em `<style>` no layout, permitindo override de qualquer estilo. Sem sanitização obrigatória (é configuração de admin).
- **Theming avançado (Keycloak-style):** motor de templates HTML/CSS com themes por realm — escopo alto, avaliar quando a base de usuários justificar.

---

## Herança Live de Realm (Parent/Child)

**Área:** Gestão de Realms
**Deferral:** Modelo `Realm.ParentRealmId` com merge de options em runtime. Avaliado e rejeitado para o médio prazo: a complexidade de override/fallback em runtime e a definição de "o que propaga quando o template muda" não justifica o benefício para a maioria dos casos de uso. O modelo copy-on-create (descrito em Realm Templates) cobre 90% dos casos.
**Quando revisitar:** Somente se houver demanda explícita de herança dinâmica (ex: dezenas de realms que precisam mudar em sincronia).
