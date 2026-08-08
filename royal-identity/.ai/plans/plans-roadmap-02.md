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
- [plan-data-configuration-storage.md](plan-data-configuration-storage.md) — CONCLUÍDO (2026-07-22), 7/7 fases.
  Sub-plano 2 do `plan-data-macro.md`: persistência Configuration EF para ServerOptions, realms/options, clients
  e signing keys em SQLite/PostgreSQL, snapshot assíncrono, protectors explícitos, runner/seed/SQL separado do
  host e contract suite P2 validada contra PostgreSQL 17 real. Resources/scopes continuam voláteis e o host
  padrão continua in-memory até os Planos 3/4.
- [plan-data-operational-storage.md](plan-data-operational-storage.md) — CONCLUÍDO (2026-07-26), 8/8 fases.
  Sub-plano 3 do `plan-data-macro.md`: família Operational sobre EF (sessões SSO, authorization codes,
  access/refresh tokens, consents, authorize parameters), com code single-use (MP-2) e transição condicional de
  refresh (MP-3) sob concorrência real, AP realm-bound com TTL absoluto (MP-5), cleanup/purge sob modo de
  execução explícito (MP-6/MP-7), proteção de payload por realm, histories de migrations separadas por família
  (DF23), gateway `AddEntityFrameworkStorage()` completo e paridade validada contra PostgreSQL 17 real. O host
  padrão e a composição de `Tests.Integration` continuam in-memory; trocar esse default é o Plano 4.
- [plan-data-test-migration.md](plan-data-test-migration.md) — CONCLUÍDO (2026-07-29), 9/9 fases.
  Sub-plano 4 do `plan-data-macro.md`: `RoyalIdentity.Server` exclusivamente PostgreSQL e externamente
  provisionado; `RoyalIdentity.Demo` fixo em SQLite in-memory e self-provisioned; as três famílias no runner;
  `Tests.Integration` integralmente sobre EF/SQLite + `UserAccounts`; contratos atômicos definitivos; fallback,
  consumers transitórios e `RoyalIdentity.Storage.InMemory` removidos. O aceite local cobriu PostgreSQL 17,
  migrations/histories das três famílias, gateway, concorrência, startup e authorization challenge OIDC.
- [plan-replay-protection.md](plan-replay-protection.md) — CONCLUÍDO (2026-07-30), 3/3 fases. Proteção real
  contra replay de `private_key_jwt`: contrato atômico realm/issuer-bound, backings in-memory e Operational,
  declaração obrigatória por composition root, guard global das implementações produtivas e aceite PostgreSQL
  ponta a ponta.
- [plan-oauth21-token-error-responses.md](plan-oauth21-token-error-responses.md) — CONCLUÍDO (2026-07-31),
  4/4 fases. Baseline de erros do token endpoint conforme RFC 6749 §5.2 e OAuth 2.1 draft-15 §3.2.4: uma única
  forma de sinalizar erro, com o código sempre na primeira posição; `RoyalIdentity.Pipelines` sem nenhuma string
  protocolar; preflight que decide cardinalidade e mecanismo de autenticação antes de qualquer efeito; 401 com
  `WWW-Authenticate` para tentativa via header; taxonomia corrigida em `unauthorized_client`, `invalid_request`
  e PKCE; recusas de authorization code indistinguíveis por construção; 405/415/404 como Problem Details, sem
  códigos OAuth inventados. Guard arquitetural com códigos derivados por reflexão impede regressão.
- [plan-oidc-session-management.md](plan-oidc-session-management.md) — CONCLUÍDO (2026-08-01), 7/7 fases.
  OP User Agent State opaco/realm-scoped, `session_state` origin-bound, `prompt=none` sem UI,
  endpoint/discovery HTTPS, iframe Web Crypto frameable e aceites Node/Chromium cross-site. O fechamento adicionou
  documentação operacional e a distribuição AGPLv3 + Apache-2.0 com notice, 80 candidatos de proveniência
  classificados e gate reproduzível.
- [plan-refactoring-debt-closure.md](plan-refactoring-debt-closure.md) — CONCLUÍDO (2026-08-06), 5/5 fases.
  Removeu marcadores e superfícies protocolares inativas, preservou payloads pre-release v1, fechou `acr_values`
  como preferência ordenada e atribuiu cada capacidade diferida a um backlog/plano nominal.
- [plan-localization.md](plan-localization.md) — CONCLUÍDO (2026-08-07), 7/7 fases. Entregou política
  `Internationalization` realm-scoped, catálogos RESX `en`/`pt-BR`/`es-419`, seleção determinística de cultura,
  UI de conta e validação integralmente localizadas, metadata fiel, isolamento multi-realm e contratos de
  persistência/aceites SQLite e PostgreSQL.

## Execução atual

Não há plano de implementação em execução. A próxima execução é
[plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md), ligado ao item
“Aderência RFC 9700 e assessment de clients” do [backlog-001.md](../backlogs/backlog-001.md). Após o hardening,
executar [plan-reference-tokens-introspection.md](plan-reference-tokens-introspection.md), que depende da remoção
do front-channel legado e da rotação final de refresh token. Depois, executar
[plan-pushed-authorization-requests.md](plan-pushed-authorization-requests.md): o PAR consome a baseline final de
autenticação direta, `Client`, Configuration/Operational e discovery sem disputar migrations ou contratos com
Reference Tokens/Introspection.

O primeiro corte de persistência (Planos 0-4) está concluído. Os Planos 5 (caching) e 6 (audit/outbox) do macro
permanecem opcionais e condicionados, respectivamente, a um mecanismo claro de invalidação administrativa e a um
requisito real de durabilidade/integração. Nenhum plano novo pode reabrir as semânticas fechadas na matriz do
baseline; o plano RFC 9700 apenas as estende onde a rotação/família de refresh token exigir.

## Próximos planos

### 1. Persistência de Dados do IdP

**Plano-guia:** [plan-data-macro.md](plan-data-macro.md) (PLANEJADO — mapa, não implementável como plano único)

Substitui a descrição simples de "persistência de dados" do roadmap 01 por uma sequência de sub-planos, para
que nenhum deles fique grande demais:

| Ordem | Sub-plano | Propósito | Status |
|---|---|---|---|
| 0 | `plan-users-accounts-sqlite-hardening.md` | Retry, migrations e seed do módulo `UserAccounts` | **Concluído** (ver acima) |
| 1 | `plan-data-storage-baseline.md` | Caracterizar contratos e comportamento atual do `MemoryStorage` | **Concluído** (ver acima) |
| 2 | `plan-data-configuration-storage.md` | Persistir dados de configuração (ServerOptions/realms/clients/keys; catálogo de resources/scopes continua na bridge volátil) | **Concluído** (2026-07-22, 7/7) |
| 3 | `plan-data-operational-storage.md` | Persistir dados operacionais (sessions/tokens/codes/consents) | **Concluído** (2026-07-26, 8/8) |
| 4 | `plan-data-test-migration.md` | Migrar testes do fake para SQLite/EF + `UserAccounts` real | **Concluído** (2026-07-29, 9/9) |
| 5 | `plan-data-caching.md` | Cache sobre os stores EF, quando a semântica estiver estável | Não criado (pode ficar fora do primeiro corte) |
| 6 | `plan-data-audit-outbox.md` | Store durável de auditoria e outbox seletivo, se ainda fizer sentido | Não criado (pode ficar fora do primeiro corte) |

`RoyalIdentity.UserAccounts` mantém persistência própria e não entra neste storage EF do IdP (mesma fronteira
da ADR-013). Critério para avançar de 0 para 1: `UserAccounts` com schema versionado, seed único e concorrência
real testada.

### 2. Conformidade das respostas de erro do token endpoint com OAuth 2.1

**Plano criado:** [plan-oauth21-token-error-responses.md](plan-oauth21-token-error-responses.md)
(CONCLUÍDO — 2026-07-31, 4/4 fases; decisões fechadas em DF1-DF20, sem perguntas abertas)

Corrige a baseline já exigida pelo RFC 6749 e incorpora a classificação adicional do OAuth 2.1 para PKCE antes
do hardening RFC 9700. O contrato é o valor exato de `error`, o status HTTP e os headers normativos; encontrar o
nome esperado apenas em `error_description` deixa de satisfazer os testes.

Escopo principal:

- Contrato explícito de resposta para separar código, descrição, status e headers.
- HTTP 401 e `WWW-Authenticate` para `invalid_client` após tentativa via `Authorization`.
- Rejeição antecipada de parâmetros não repetíveis, credenciais e mecanismos de autenticação múltiplos.
- Taxonomia exata para `invalid_request`, `invalid_client`, `invalid_grant`, `unauthorized_client`,
  `unsupported_grant_type` e `invalid_scope`.
- Preservação de erros de extensão, inclusive `invalid_target` do RFC 8707 e extension grants.
- PKCE: verifier/challenge com presença divergente usa `invalid_request`; verifier incorreto usa
  `invalid_grant`.
- Testes que desserializam JSON e comparam `error`, status e headers, sem assertions por substring.

Este plano não altera storage e deve ser executado antes da Fase 3 do plano RFC 9700. A auditoria completa dos
erros de authorize, revocation, UserInfo e protected resources permanece fora deste corte, com uma exceção
explícita: DF20 mantém o `ResourcesValidator` compartilhado e corrige o campo `error` de
`invalid_scope`/`invalid_target` também no authorize, sem tocar no transporte. DF18 fixa `invalid_grant` 400,
nunca 5xx, para `code_challenge_method` persistido desconhecido; DF19 deixa `RoyalIdentity.Pipelines` sem
nenhuma seleção de código OAuth.

### 2.1. OpenID Connect Session Management e Check Session

**Plano concluído:** [plan-oidc-session-management.md](plan-oidc-session-management.md)
(CONCLUÍDO em 2026-08-01 — 7/7 fases)

Implementa o OP side do OpenID Connect Session Management 1.0 sem portar a infraestrutura de sessão legada do
IS4. O plano cria um OP User Agent State opaco e realm-scoped, corrige `prompt=none`, move `session_state` para
as Authentication Responses, mapeia e protege o OP iframe e fecha as atribuições Apache-2.0 do código derivado.

Escopo principal:

- Estado opaco distinto de `sid`, protegido no ticket e espelhado em cookie JavaScript-readable realm-scoped.
- `session_state` origin-bound em sucessos e erros OIDC aplicáveis, sem persistência no authorization code.
- `prompt=none` sem UI, com `login_required` e `consent_required` corretos.
- Rota/discovery somente sob HTTPS e feature gate efetivo por realm.
- OP iframe com Web Crypto, validação de parent/origin, CSP por nonce e headers sem bloquear framing.
- Aceite Playwright opt-in com OP/RP em origins diferentes e dois realms.
- Licença Apache-2.0 e notices preservados dentro da distribuição AGPLv3.

O plano consome os helpers/writer finais de `plan-oauth21-token-error-responses.md` antes de alterar
`ConsentDecorator` e entrega a fatia diferida do authorization endpoint necessária a `prompt=none`. O plano RFC
9700, executado depois, preserva por regressão a exceção de framing exclusiva do OP iframe.

O plano deve executar depois de
[plan-oauth21-token-error-responses.md](plan-oauth21-token-error-responses.md), para não disputar os contratos de
erro, e antes de `plan-refactoring-debt-closure.md`, que depende de sua baseline funcional. O hardening RFC
9700 executa depois e preserva a exceção de framing exclusiva do OP iframe.

### 2.2. Fechamento de dívidas de refatoração e superfícies inativas

**Plano criado:** [plan-refactoring-debt-closure.md](plan-refactoring-debt-closure.md)
(CONCLUÍDO — 5/5 fases; encerrado em 2026-08-06)

Fecha marcadores antigos, decisões que deixaram de ser refatorações ativas e superfícies protocolares anunciadas
sem implementação. Também corrige a documentação do redesign concluído de resources, sem antecipar sua
persistência.

Escopo principal:

- Tornar finais as decisões de manter a herança de `IWith*` e o wrapper obfuscado dos eventos.
- Remover markers já atendidos e símbolos `[Obsolete]` sem callers.
- Retirar discovery/options de introspection e Device Authorization enquanto não houver endpoints reais.
- Remover branches vazios que impedem `IExtensionsGrantsProvider` de tratar grants registrados.
- Remover `UseLogService` e blocos TODO sem criar auditoria antecipada.
- Fixar `acr_values` como lista de preferência sem catálogo/claim/metadata fictícia.
- Atualizar o shape corrente das options Configuration mantendo v1 e validar realms/providers.
- Atualizar foundations/matriz para registrar que somente a persistência do catálogo de resources está diferida.

Este plano depende da conclusão do plano OAuth 2.1 e de
[plan-oidc-session-management.md](plan-oidc-session-management.md). A persistência EF do catálogo de
resources/scopes continuará reservada para um futuro `plan-data-resource-catalog-storage.md`, ainda não criado.

### 2.3. Localization realm-scoped da UI

**Plano criado:** [plan-localization.md](plan-localization.md)
(CONCLUÍDO — 7/7 fases, 2026-08-07)

Fecha a última pendência antiga de `redesign-todo.md`: transforma o scaffold órfão
`InternationalizationOptions` em configuração persistida por realm, seleciona cultura por request e localiza
integralmente a UI de conta com catálogos `.resx` neutro/inglês, `pt-BR` e espanhol latino-americano `es-419`.
O [inventário de recursos](../analisys/an-localization-resource-inventory.md) fixa a baseline em 62 chaves por
cultura, distribuídas em dois catálogos lógicos e seis arquivos físicos `.resx` (186 entradas).
Novos realms nascerão com localization ativa, default `en` e suporte a `en`, `pt-BR` e `es-419`; cada realm
continua podendo desabilitar a negociação explicitamente.

Escopo principal:

- `RealmOptions.Internationalization` com validação BCP 47, cópia profunda e payload Configuration v1 corrente.
- `IStringLocalizer<T>` sobre `.resx`, catálogos com 62 chaves semânticas por cultura e paridade de
  chaves/placeholders.
- Precedência cookie realm-scoped → `ui_locales` validado → `Accept-Language` → default do realm → neutro.
- `RequestLocalization` depois de realm discovery e antes de autenticação/renderização.
- Códigos estáveis entre core/page services e UI, removendo as três mensagens configuráveis de `AccountOptions`
  sem reduzir a proteção contra enumeração de contas.
- Login, consentimento, logout, erro, perfil, validações, acessibilidade e `html lang`/`dir` localizados.
- `ui_locales_supported` fiel à interseção entre configuração do realm e catálogos realmente entregues.
- Validação de cada snapshot antes da publicação, com last-known-good preservado em refresh inválido.

O plano depende da conclusão de
[plan-refactoring-debt-closure.md](plan-refactoring-debt-closure.md), para não disputar a remoção de options e a
baseline funcional do predecessor. Não depende do RFC 9700, mas executa antes dele na ordem recomendada para deixar a
infraestrutura que o futuro Admin reutilizará ao localizar findings por `RuleId`. Overrides de tradução por
realm, claims localizados e conteúdo multilíngue cadastrado pelo tenant permanecem fora deste corte.

### 3. Aderência e hardening OAuth 2.0 conforme RFC 9700

**Plano criado:** [plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md)
(RASCUNHO — 0/6 fases)

**Backlog relacionado:** item “Aderência RFC 9700 e assessment de clients” em
[backlog-001.md](../backlogs/backlog-001.md).

Estabelece defaults seguros e enforcement para os requisitos aplicáveis do RFC 9700 sem criar um
`OAuthSecurityProfile`. O estado real de `Client` + `RealmOptions` é avaliado sob demanda por
`ClientSecurityAssessment.Create(...)`; assessment e findings não são persistidos.

Escopo principal:

- `ClientSecurityAssessment` determinístico, com `RuleId` e `ClientSecurityFinding`.
- Redirect URI ordinal/sem wildcard por default, com os dois relaxamentos configuráveis por realm e classificados
  como não aderentes.
- Correção de PKCE downgrade e remoção de implicit/hybrid/front-channel token.
- Rotação de refresh token com família, retry sem ramificação e revogação no replay.
- Clickjacking, referrer, metadata/mTLS, issuer identification e redação de logs.
- Contrato/handoff para o futuro Admin calcular e apresentar findings em tempo real, sem snapshot.

O plano depende do fechamento de `plan-replay-protection.md`; sua Fase 3 depende também da conclusão de
[plan-oauth21-token-error-responses.md](plan-oauth21-token-error-responses.md). A apresentação administrativa
depende do item 5 deste roadmap; a implementação do core não depende da existência do Admin.

### 3.1. Reference Tokens e Token Introspection RFC 7662

**Plano criado:** [plan-reference-tokens-introspection.md](plan-reference-tokens-introspection.md)
(RASCUNHO — 0/7 fases; Q1/Q2 pendentes antes da Fase 5)

Completa o suporte parcial já existente para access tokens opacos: adiciona o tipo persistido por client, centraliza
a emissão inicial e por refresh, preserva o handle somente como digest no storage Operational e entrega validação
remota autenticada para ResourceServers pelo Token Introspection.

Escopo principal:

- `Client.AccessTokenType` com JWT como default e migrations Configuration SQLite/PostgreSQL.
- Handle reference com 256 bits de entropia, sem assinatura, claim `jti`, persistência bruta ou log em claro.
- Emissão por authorization code, client credentials e refresh, sempre pelo `DefaultTokenFactory`.
- Bearer, expiração e revogação exercitados com tokens realmente emitidos, preservando AT-01..AT-04.
- Endpoint RFC 7662 autenticado por `ResourceServer.Secrets`, com `active=false` indistinguível e disclosure
  restrito à audience/resource do caller.
- Discovery e metadata de autenticação fiéis, além de aceites multi-realm e PostgreSQL real.

Q1 decide os métodos de autenticação do ResourceServer no primeiro corte; Q2 decide se introspection cobre somente
reference access tokens ou também JWT/refresh tokens. As Fases 1-4 não dependem dessas respostas, mas o endpoint e
os aceites finais não iniciam enquanto elas estiverem abertas.

O plano executa depois de [plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md), para não
disputar `RefreshTokenHandler`, migrations Operational nem a remoção da emissão no authorization endpoint. A
persistência do catálogo de ResourceServers/scopes/secrets permanece fora deste corte.

### 3.2. Pushed Authorization Requests RFC 9126

**Plano criado:** [plan-pushed-authorization-requests.md](plan-pushed-authorization-requests.md)
(RASCUNHO — 0/7 fases; Q1/Q2/Q3 pendentes antes da Fase 1)

Implementa PAR como feature protocolar completa: endpoint direto autenticado, validação antecipada da
authorization request, `request_uri` opaco client-bound, consumo atômico no authorization endpoint, policy por
realm/client e metadata fiel.

Escopo principal:

- `/{realm}/connect/par` em HTTPS, POST form, com os mesmos métodos de autenticação do token endpoint.
- Referência URN com 256 bits de entropia, persistida somente por digest realm/type-bound.
- Store Operational próprio, payload protegido, TTL absoluto, cleanup e concorrência SQLite/PostgreSQL.
- Resolução antes da materialização de `AuthorizeContext`, sem mesclar parâmetros do front channel.
- Continuação de login/consentimento preservada no `IAuthorizeParametersStore` repetível já existente.
- `RequirePushedAuthorizationRequests` por realm/client, default `false`, e discovery RFC 9126 coerente.
- Separação explícita de PAR e JAR; a metadata atual de Request Object não poderá anunciar o stub como suporte
  real.

Q1 decide entre facade PAR específica e uma facade geral com operações explicitamente distintas; Q2 decide uso
estritamente único ou tolerância segura de reload; Q3 fecha lifetime default/faixa. Nenhuma fase inicia enquanto
essas decisões de arquitetura/segurança estiverem abertas.

O plano executa depois de
[plan-reference-tokens-introspection.md](plan-reference-tokens-introspection.md), para consumir o desenho final de
`Client`, autenticação direta, endpoints, discovery e migrations. JAR/JARM, Dynamic Client Registration, FAPI,
Admin e persistência do catálogo de resources/scopes continuam fora deste corte.

### 4. Administração de Sessões por Dispositivo

**Plano sugerido:** `plan-session-administration.md`

Extensão do modelo operacional de sessão para administração pelo usuário ou por admin.

Escopo principal:

- Metadados de sessão: user agent, IP, device name e `LastSeenAt`.
- Listar sessões ativas por usuário/realm.
- Encerrar sessão específica ou encerrar outras sessões.
- Preservar integração com logout SSO front-channel/back-channel.

A sessão básica já é coberta pelo `plan-users-edge-session.md` (concluído). Este plano trata a camada
administrativa e operacional mais rica.

### 5. API e UI Administrativa

**Plano sugerido:** `plan-admin-api-ui.md`

**Backlogs relacionados:** “Gestão de Realms via API Administrativa”, “UI Administrativa (realm `admin`)” e
“Aderência RFC 9700 e assessment de clients” em [backlog-001.md](../backlogs/backlog-001.md).

**Dependência de segurança:** consumir o contrato entregue por
[plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md), calcular o assessment em leitura/após
edição, localizar mensagens por `RuleId` e nunca persistir status/findings derivados.

**Dependência de localização:** reutilizar a infraestrutura de `IStringLocalizer<T>` entregue por
[plan-localization.md](plan-localization.md), criando catálogos próprios do Admin em vez de armazenar traduções
ou textos de findings em `Client`, `Realm` ou `ClientSecurityAssessment`.

Criação das APIs e telas administrativas, em projetos separados dos módulos de domínio.

Escopo principal:

- Administração de contas de usuário.
- Administração de realms, clients, resources/scopes e options.
- Reset de senha, ativar/desativar usuário e revogar sessões.
- Administração de propriedades dinâmicas por escopo.
- Integração com o realm `admin`.

Este plano depende das decisões de API/UI administrativa e deve respeitar a regra da ADR-013: módulos contêm
domínio + persistência; API e UI ficam separados.

### 6. Federation / Identity Brokering

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

### 7. MFA e Passwordless

**Plano sugerido:** `plan-auth-methods-mfa-passwordless.md`

Novos métodos de autenticação além de senha local.

Escopo principal:

- MFA por realm e por usuário.
- Passwordless e desafios temporários.
- Políticas por realm/client.
- Registro dos métodos em `amr`.
- Integração com login flow sem criar sessão antes da autenticação final.

Este plano depende do módulo de contas e do ciclo de credenciais já concluídos (ambos estão — ver "Concluído").

### 8. Key Management Service

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
2. ~~Executar os sub-planos 1-4 do `plan-data-macro.md` (storage-baseline → configuration-storage →
   operational-storage → test-migration).~~ CONCLUÍDO. Avaliar caching e audit-outbox (5-6) depois, só se ainda
   fizerem sentido no momento.
3. ~~Concluir `plan-replay-protection.md` (Fase 3).~~ CONCLUÍDO.
4. ~~Executar [plan-oauth21-token-error-responses.md](plan-oauth21-token-error-responses.md).~~ CONCLUÍDO.
5. ~~Executar [plan-oidc-session-management.md](plan-oidc-session-management.md).~~ CONCLUÍDO.
6. ~~Executar [plan-refactoring-debt-closure.md](plan-refactoring-debt-closure.md).~~ CONCLUÍDO.
7. ~~Executar [plan-localization.md](plan-localization.md).~~ CONCLUÍDO.
8. Executar [plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md) — PRÓXIMA EXECUÇÃO.
9. Executar [plan-reference-tokens-introspection.md](plan-reference-tokens-introspection.md), após fechar Q1/Q2.
10. Executar [plan-pushed-authorization-requests.md](plan-pushed-authorization-requests.md), após fechar Q1/Q2/Q3.
11. Evoluir administração de sessões por dispositivo.
12. Criar API/UI administrativa, consumindo `ClientSecurityAssessment` e a infraestrutura de localização.
13. Avançar federação, MFA/passwordless e KMS conforme prioridade de produto.
