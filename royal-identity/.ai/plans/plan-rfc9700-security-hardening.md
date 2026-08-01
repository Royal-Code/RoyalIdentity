# Plan: Aderência e hardening de segurança OAuth 2.0 conforme RFC 9700 (`plan-rfc9700-security-hardening`)

## Status: RASCUNHO - decisões de arquitetura fechadas; Q1 pendente antes da Fase 4

## Progresso

`░░░░░░` **0%** - 0 de 6 fases

| Fase | Estado |
|---|---|
| Fase 1 - Assessment determinístico e catálogo de regras | Pendente |
| Fase 2 - Validação configurável e segura de redirect URIs | Pendente |
| Fase 3 - Authorization Code, PKCE e remoção do front-channel legado | Pendente |
| Fase 4 - Rotação e detecção de replay de refresh tokens | Bloqueada por Q1 |
| Fase 5 - Segurança HTTP, metadata, client authentication e logs | Pendente |
| Fase 6 - Handoff administrativo, aceites e fechamento | Pendente |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluída, `%` e `X de 6`). Antes de fechar uma fase, confirme que decisões,
> critérios de aceite, testes e invariantes relacionados foram aplicados.

---

## Contexto

### Fontes verificadas

- [ADR-020](../../adrs/ADR-020.md) — payloads Configuration permanecem em v1 durante o pre-release; mudanças de
  shape atualizam fixtures/seeds e exigem reprovisionamento.
- [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700.html) — BCP 240: redirect exato, PKCE e downgrade,
  desuso do implicit, proteção contra replay, restrição de privilégios, proibição do password grant,
  client authentication, TLS/proxy, refresh tokens e clickjacking.
- [product.md](../foundation/product.md) — RoyalIdentity é a rearquitetura multi-realm do IS4; PKCE default-on,
  códigos single-use e tolerância pós-consumo de refresh token são invariantes atuais.
- [tech.md](../foundation/tech.md) e [structure.md](../foundation/structure.md) — opções efetivas pertencem a
  `RealmOptions`, o core não depende de hosts/UI/providers e mudanças de storage exigem adapter EF e contratos.
- [plan-realm-options-redesign.md](plan-realm-options-redesign.md) — opções promovidas para realm são cópias
  independentes dos defaults de `ServerOptions`; consumidores realm-aware leem `RealmOptions`.
- [plan-data-storage-matrix.md](plan-data-storage-matrix.md) — semânticas Configuration/Operational já fechadas;
  novos contratos devem estender a matriz sem reabrir decisões existentes.
- [plan-data-configuration-storage.md](plan-data-configuration-storage.md) — clients e grafos de
  `ServerOptions`/`RealmOptions` são Configuration; `Data.Configuration` permanece puro e o adapter materializa.
- [plan-data-operational-storage.md](plan-data-operational-storage.md) — consumo de refresh token já possui
  transição condicional/atômica e a tolerância é aplicada pelo caller sobre estado rematerializado.
- [plan-replay-protection.md](plan-replay-protection.md) — concluído (3/3); proteção de replay de
  `private_key_jwt` usa `IReplayProtectionStore` e o Server já possui backing Operational durável.
- [plan-oauth21-token-error-responses.md](plan-oauth21-token-error-responses.md) — baseline anterior para
  taxonomia, status e headers do token endpoint; a Fase 3 deste plano consome sua classificação de PKCE.
- [plan-oidc-session-management.md](plan-oidc-session-management.md) — predecessor que implementa o OP iframe e
  fixa a exceção protocolar de framing; a Fase 5 deve preservar seus testes de ausência de headers bloqueadores.
- [plan-refactoring-debt-closure.md](plan-refactoring-debt-closure.md) — predecessor que remove
  `LoggingOptions.UseLogService`, mantém os payloads Configuration pré-release em v1 e corrige o alias mTLS de revocation; este
  plano consome essa superfície simplificada sem reintroduzir option/branch ou a rota incorreta.
- [plan-localization.md](plan-localization.md) — predecessor que adiciona localization ao grafo de realm mantendo
  Server e Realm Options em v1; é a baseline funcional imediata para adicionar `RedirectUriValidation`.
- [plans-roadmap-02.md](plans-roadmap-02.md) e [backlog-001.md](../backlogs/backlog-001.md) — API/UI
  administrativa ainda não existem; este plano deve entregar um contrato puro que o futuro Admin consuma.

### Estado atual do código (verificado em 2026-07-31)

- **Defaults do client parcialmente seguros:** `Client.RequirePkce = true`,
  `AllowPlainTextPkce = false`, `AllowedResponseTypes = code` e `AllowedGrantTypes = authorization_code`.
- **Password grant ausente:** `TokenEndpoint` trata authorization code, refresh token e client credentials;
  não há handler de resource owner password credentials.
- **Redirect não aderente:** `DefaultRedirectUriValidator.MatchRedirectUri` aceita wildcard e usa
  `StringComparison.OrdinalIgnoreCase`; `RedirectUriValidator` exige URI absoluta, mas não recusa fragment ou HTTP.
- **Contrato de redirect sem opções do realm:** `IRedirectUriValidator` recebe apenas URI e `Client`.
- **Downgrade de PKCE possível:** `PkceMatchValidator` retorna sucesso quando o authorization code não contém
  `CodeChallenge`, mesmo que o token request envie `code_verifier`.
- **Front-channel legado ativo:** `DiscoveryOptions` anuncia `code`, `token` e `id_token`; `AuthorizeHandler`
  emite access token e ID token diretamente na authorization response.
- **Authorization code robusto:** `LoadCode` usa consumo atômico vinculado a code, client e redirect URI antes
  da emissão.
- **Refresh token sem família:** `TryConsumeAsync` é atômico, mas `RefreshToken` não registra família/sucessor;
  `TimeSpan.MaxValue` permite reutilização ilimitada e o handler pode manter o mesmo handle.
- **Privilégio de access token já restringido:** emissão usa scopes, resource indicators e audiences resolvidos.
- **Client authentication assimétrica parcial:** `private_key_jwt` usa replay protection; mTLS possui evaluator e
  confirmação, mas metadata/rotas precisam ser verificadas como conjunto antes de serem anunciadas.
- **Proteção web incompleta:** CSP existe em resultados com script, mas não há `frame-ancestors` nem
  `X-Frame-Options` global para login/consent/authorize; `Referrer-Policy: no-referrer` aparece apenas no
  `form_post`.
- **Redação de logs parcialmente resolvida:** a Fase 4 do `plan-oauth21-token-error-responses.md` (2026-07-31)
  achou o authorization code sendo escrito em claro por `DefaultCodeFactory` e o request bruto sendo logado sem
  redigir `code`/`code_verifier`. Os dois foram corrigidos, e a proteção deixou de ser configurável: nomes
  obrigatórios agora vivem em `LoggingOptions.AlwaysRedacted`, um piso que a configuração só amplia — porque
  `RealmOptions` é serializado inteiro no payload Configuration e um realm persistido manteria a lista antiga.
  **Permanecem para este plano:** `state`, `nonce` e a auditoria dos demais logs (assertions e tokens).
- **Assessment inexistente:** não existem `ClientSecurityAssessment`, `ClientSecurityFinding`, catálogo de
  `RuleId` ou snapshot persistido.
- **Admin inexistente neste repositório:** há login/consent/logout em `RoyalIdentity.Razor`; API e UI
  administrativas continuam no roadmap/backlog.
- **Persistência vigente:** clients têm projeção relacional e property-coverage test; opções de server/realm são
  payloads JSON versionados; Operational tem migrations SQLite/PostgreSQL e contracts provider-neutral.
- **Payloads pré-release:** após os predecessores, Server e Realm Options permanecem em v1 conforme ADR-020;
  adicionar `RedirectUriValidation` altera o shape corrente, exige reprovisionamento e não incrementa a versão.

### Lacunas, conflitos e restrições

- **Configuração não equivale a certificação:** um assessment calculado de `Client`/`RealmOptions` não comprova
  TLS, proxy, headers ou topologia de uma implantação.
- **Compatibilidade legada deliberada:** wildcard e comparação case-insensitive continuarão possíveis, mas os
  defaults serão seguros e o assessment deve classificá-los como não aderentes.
- **Sem consumidores de produção:** não há obrigação de preservar APIs, schema, migrations ou defaults atuais;
  não criar shims para comportamentos herdados do IS4.
- **Tolerância de refresh é invariante existente:** corrigir replay não autoriza remover o cenário de retry por
  falha de persistência no cliente; o retry não pode criar ramos na família.
- **Admin é outro plano:** este plano não pode adicionar lógica administrativa a `RoyalIdentity.Razor`; apenas
  entrega tipos e regras consumíveis e registra o handoff.
- **Replay protection já concluída:** não modificar, duplicar nem contornar `IReplayProtectionStore`; este plano
  consome a baseline implementada por `plan-replay-protection.md`.
- **OP iframe precisa ser enquadrável:** `check_session_iframe` é deliberadamente carregado por RPs em outra
  origem. O hardening browser-facing não pode tratá-lo como login/consent/error nem injetar
  `X-Frame-Options: DENY` ou `frame-ancestors 'none'` nessa rota.
- **Predecessores compartilham superfícies:** `LoggingOptions`, `DiscoveryHandler`, aliases mTLS e os payloads
  Configuration chegam a este plano já alterados por Debt Closure e Localization; não reabrir essas decisões.
- **Filtros amplos mascaram cobertura:** `FullyQualifiedName~Pkce` seleciona apenas `PkceHelperTests` hoje;
  `~RedirectUri` seleciona métodos incidentais sem provar a nova policy; `~Cors` seleciona testes existentes, mas
  o filtro composto da Fase 5 poderia permanecer verde sem nenhum teste novo de logging/headers.

### Superfícies impactadas a mapear

- `RoyalIdentity/Models`, `Options`, `Contracts`, `Contexts/Validators`, `Handlers`, `Responses` — regras e runtime.
- `RoyalIdentity.Data.Configuration` e `RoyalIdentity.Storage.EntityFramework/Configuration` — novas opções e
  roundtrip de configuração, default/projeção de client, seeds e property coverage.
- `RoyalIdentity.Data.Operational` e `RoyalIdentity.Storage.EntityFramework/Operational` — família/rotação de
  refresh token e migrations.
- `RoyalIdentity.Server`, `RoyalIdentity.Demo`, `Tests.Host`, `RoyalIdentity.Razor` — headers, proxy/TLS e
  composições.
- `Tests.Identity`, `Tests.Integration`, `Tests.Storage`, `Tests.Host`, `Tests.WebApp`,
  `Tests.Architecture` — unidade, fluxo, persistência, host e boundaries.
- Futuro `plan-admin-api-ui.md` — apresentação/localização dos findings; não é implementado aqui.

---

## Objetivo

1. Tornar os defaults e o runtime do authorization server compatíveis com os requisitos aplicáveis do RFC 9700.
2. Disponibilizar `ClientSecurityAssessment.Create(...)` puro, síncrono e determinístico, sem interface, DI,
   I/O ou persistência.
3. Produzir findings estáveis por `RuleId`, requisito normativo, status, severidade e remediação técnica.
4. Permitir configurar por realm a comparação de redirect URI e o uso de wildcard, com default
   `Ordinal`/sem wildcard e finding não aderente para qualquer relaxamento.
5. Corrigir PKCE downgrade, front-channel legado, replay de refresh token, headers, metadata e vazamento em logs
   sem introduzir um perfil agregado de segurança.
6. Entregar contratos e rastreabilidade para o futuro Admin calcular e apresentar a aderência em tempo real.

## Fora de escopo

- `OAuthSecurityProfile`, presets equivalentes ou um booleano global de “RFC 9700”.
- Persistir `ClientSecurityAssessment`, `ClientSecurityAssessmentSnapshot`, findings ou status derivado.
- Implementar a API/UI administrativa; destino: roadmap item “API e UI Administrativa” e backlog
  “UI Administrativa”.
- Implementar DPoP (RFC 9449), PAR (RFC 9126), JAR/JARM adicionais ou Dynamic Client Registration.
- Implementar suporte específico a native apps/redirect HTTP loopback; até existir classificação explícita de
  aplicação, authorization responses aceitam somente redirect HTTPS.
- Tratar RFC 9711/EAT; requer plano próprio de attestation.
- Declarar certificação da implantação ou avaliar TLS/proxy/rede a partir de modelos de configuração.
- Reabrir o redesign de resources/scopes ou semânticas fechadas que não sejam estendidas explicitamente aqui.

---

## Perguntas ao humano

- **Q1 — Tolerância default de retry do refresh token:** qual janela limitada substitui
  `TimeSpan.MaxValue`?
  - **Opções:**
    - **A)** 30 segundos — preserva retry imediato por falha de persistência do cliente com exposição curta.
    - **B)** `TimeSpan.Zero` — replay estrito; a capacidade continua configurável, mas vem desligada.
    - **C)** outro valor limitado definido pelo mantenedor.
  - **Impacto se não decidir:** bloqueia o default, os testes de borda e o fechamento da Fase 4; não bloqueia
    as Fases 1-3 e 5.
  - **Status:** Aberta.

---

## Decisões fechadas

- **DF1 — Sem perfil agregado:** aderência é derivada das configurações e comportamentos reais; não criar
  `OAuthSecurityProfile` nem equivalente. Fonte: decisão humana nesta discussão.
- **DF2 — Factory method puro:** `ClientSecurityAssessment` expõe factory method estático; não criar
  `IClientSecurityAssessor`, serviço DI ou pipeline para avaliação. Fonte: decisão humana nesta discussão.
- **DF3 — Assessment efêmero:** assessment e findings não são persistidos; cada consumidor recalcula com
  `Client` e `RealmOptions`. Fonte: decisão humana nesta discussão.
- **DF4 — Resultado derivado:** `Status` é calculado exclusivamente a partir de findings; não é setável pelo
  chamador. Não incluir `AssessedAt` ou `EvaluatorVersion` na instância. Fonte: decisão humana nesta discussão.
- **DF5 — Identidade estável das regras:** cada finding tem `RuleId` estável, nível normativo, status, severidade,
  descrição técnica e remediação; o Admin localiza a apresentação pelo `RuleId`. Fonte: decisão humana nesta
  discussão.
- **DF6 — Escopo honesto:** `ClientSecurityAssessment` avalia somente fatos observáveis em `Client` e
  `RealmOptions`; postura de deployment pertence a diagnóstico de host/startup. Fonte: decisão humana nesta
  discussão.
- **DF7 — Redirect configurável por realm:** `ServerOptions` fornece o template e `RealmOptions` mantém cópia
  independente de `RedirectUriValidationOptions`; comparação admite somente `Ordinal` ou
  `OrdinalIgnoreCase`, e wildcard é flag explícita. Fonte: decisão humana + padrão de
  `plan-realm-options-redesign.md`.
- **DF8 — Defaults de redirect aderentes:** comparação default é `Ordinal`; wildcard default é desabilitado.
  Relaxamentos são aceitos pelo runtime apenas quando configurados e sempre geram finding não aderente.
  Fonte: decisão humana + RFC 9700 §2.1.
- **DF9 — HTTPS no escopo atual:** redirect de authorization response usa HTTPS; HTTP loopback só será
  introduzido com suporte explícito a native apps, fora deste plano. Fonte: RFC 9700 §2.6 + ausência atual de
  `ApplicationType` no modelo.
- **DF10 — Correções sem switches legados:** PKCE downgrade, emissão de access token no authorization endpoint,
  refresh replay, clickjacking, referrer, metadata e redação de logs são corrigidos sem flags de compatibilidade.
  Fonte: decisão humana nesta discussão.
- **DF11 — Password grant continua ausente:** não adicionar handler, opção ou compatibilidade para resource owner
  password credentials; configuração residual com esse grant é finding não aderente e continua inexequível.
  Fonte: decisão humana + RFC 9700 §2.4.
- **DF12 — Authorization Code como fluxo interativo:** remover suporte runtime e anúncio de response types
  `token`, `id_token` e combinações implicit/hybrid; o fluxo interativo suportado termina em authorization code.
  Fonte: decisão humana de aceitar breaking changes + RFC 9700 §2.1.2.
- **DF13 — Rotação única com família:** refresh tokens são rotacionados; retry dentro da tolerância retorna o
  mesmo sucessor ainda ativo e não cria ramo; reutilização fora da tolerância ou após avanço da família revoga
  a família ativa. Fonte: RFC 9700 §§2.2.2/4.14.2 + invariante de tolerância do produto.
- **DF14 — Sender constraint como recomendação avaliável:** preservar `private_key_jwt`/mTLS, alinhar anúncio ao
  suporte real e produzir warning quando autenticação/token sender-constrained recomendados não forem usados;
  DPoP permanece diferido. Fonte: RFC 9700 §§2.2.1/2.5.
- **DF15 — Breaking changes diretos:** alterar contratos públicos, defaults, modelos, serializers e migrations
  diretamente, atualizando todos os consumidores e sem compatibility shims. Fonte: `AGENTS.md` + decisão humana.
- **DF16 — Handoff administrativo:** o futuro Admin calcula o assessment em leitura e após edição, exibe findings
  por `RuleId` e não grava status derivado. Fonte: decisão humana nesta discussão.
- **DF17 — Baseline de replay preservada:** `plan-replay-protection.md` está concluído; este plano preserva
  `IReplayProtectionStore`, o backing Operational declarado por host e seus guards, sem duplicar a solução.
  Fonte: plano concluído + `AGENTS.md`.
- **DF18 — Baseline de erros do token endpoint:** a Fase 3 depende da conclusão de
  `plan-oauth21-token-error-responses.md`; a Fase 4 também consome seus writers/status para erros de refresh.
  Verifier sem challenge retorna `invalid_request`, enquanto verifier incorreto contra challenge existente e
  replay/família revogada retornam `invalid_grant` sem recriar o transporte. Fonte: OAuth 2.1 draft-15
  §§3.2.4/4.1.3 + RFC 7636 §4.6.
  **Pré-requisito satisfeito em 2026-07-31:** o plano de erros concluiu 4/4 fases. A classificação de PKCE já
  existe e está coberta por `Tests.Integration/Endpoints/PkceTokenTests.cs`; este plano a consome e não
  reimplementa nada dela (DF17 do plano de erros). O writer aceita status e headers explícitos, e a família de
  refresh deve usar `context.Error(<código>, <descrição>)` como todo o resto — os helpers `InvalidGrant`/
  `InvalidClient`/`InvalidRequest` não existem mais.
- **DF19 — Exceção nominal de framing:** aplicar proteção contra framing a páginas sensíveis, mas excluir
  exclusivamente a rota `check_session_iframe`; preservar por teste a ausência de `X-Frame-Options: DENY` e de
  `frame-ancestors 'none'` nessa resposta, sem relaxar login/consent/logout/error. Fonte:
  `plan-oidc-session-management.md` DF16 + requisito funcional do OP iframe.
- **DF20 — Payloads pré-release v1:** executar após Debt Closure e Localization. A nova option altera ambos os
  grafos, mas `ServerOptionsPayload` e `RealmOptionsPayload` permanecem em v1; serializers aceitam somente v1,
  seeds/fixtures são reprovisionados e não há migration relacional ou JSON. Fonte: ADR-020 + ordem do roadmap.
- **DF21 — Topologia verificável de testes:** factory/validators puros ficam em classes nomeadas de
  `Tests.Identity`; HTTP/metadata/logging/headers ficam em classes nomeadas de `Tests.Integration`; payload/client
  Configuration e família Operational ficam em classes nomeadas de `Tests.Storage`; boundaries ficam em
  `Tests.Architecture`. Cada comando filtrado é separado e deve selecionar ao menos um teste; não usar OR para que
  outra fixture esconda uma classe ausente. Fonte: infraestrutura atual + regra dos planos predecessores.
- **DF22 — Metadata PKCE fiel:** enquanto `AllowPlainTextPkce` e seu caminho runtime permanecerem suportados,
  `code_challenge_methods_supported` anuncia `S256` e `plain`; a lista expressa capacidade, não preferência.
  `S256` continua obrigatório e clientes com `AllowPlainTextPkce=true` recebem finding não aderente. Remover
  `plain` da metadata exige remover no mesmo corte a option e o caminho runtime. Fonte: RFC 9700 §2.1.1 + RFC 8414.

---

## Histórico de decisões

**Discussão de desenho anterior a este plano:**

- **Perfil de segurança:** foi considerada uma enumeração `OAuthSecurityProfile`.
  - **Resposta humana:** rejeitada; as opções reais devem determinar a aderência.
  - **Conclusão:** DF1.
- **Serviço de assessment:** foi considerada a interface `IClientSecurityAssessor`.
  - **Resposta humana:** usar factory method em `ClientSecurityAssessment`.
  - **Conclusão:** DF2.
- **Snapshot:** foi considerado persistir `ClientSecurityAssessmentSnapshot`.
  - **Resposta humana:** inútil; a avaliação é recriada com os parâmetros atuais.
  - **Conclusão:** DF3/DF16.
- **Compatibilidade:** foi considerada migração gradual de clients existentes.
  - **Resposta humana:** o projeto está em desenvolvimento, sem clients de produção; breaking changes são
    aceitáveis.
  - **Conclusão:** DF15 e registro persistente em `AGENTS.md`.

**Revisão externa de posicionamento e execução (2026-07-31):**

- **Confirmados:** faltavam os predecessores Debt Closure/Localization, a cadeia de payloads estava aberta e a
  compatibilidade descrita contradizia a leitura fail-closed dos serializers. Conclusão: DF20.
- **Confirmados:** a Fase 4 também altera default/projeção Configuration do `Client` e os dois branches de
  `TimeSpan.MaxValue`; seus erros devem consumir a baseline OAuth 2.1. Conclusão: ampliar escopo e DF18.
- **Parcialmente confirmado:** `~Pkce` seleciona apenas o helper e não prova downgrade. `~RedirectUri` e `~Cors`
  não estão vazios — `FullyQualifiedName` também casa nomes de métodos —, mas selecionam cobertura incidental ou
  permitem que o filtro composto esconda a ausência de logging/headers. Conclusão: classes e comandos separados
  em DF21.
- **Decisão fechada:** o RFC 9700 recomenda S256, mas não proíbe suporte server-side a `plain`; RFC 8414 define
  `code_challenge_methods_supported` como a lista dos métodos suportados, e o RFC 9700 recomenda publicá-la. Como
  o plano preserva o opt-in de client, manter anúncio fiel conforme DF22, sem tratá-lo como preferência.
- **Dependência da Fase 4:** não forçar dependência funcional da Fase 3; ambas podem consumir diretamente o plano
  OAuth 2.1 já concluído. Conclusão: dependência explícita pela mesma baseline.
- **Correção adicional ao relatório:** Replay Protection não está mais ativo/pendente; já foi concluído e deve ser
  tratado como baseline preservada. Conclusão: atualizar estado e DF17.

---

## Design alvo

### Contratos e bordas

- `ClientSecurityAssessment.Create(Client client, RealmOptions realmOptions)`: factory pública, pura, síncrona,
  sem DI/I/O; valida argumentos, executa catálogo fechado de regras e deriva `Status`.
- `ClientSecurityFinding`: valor imutável contendo `RuleId`, `RequirementLevel`, `Status`, `Severity`,
  `Description` e `Remediation`.
- `ClientSecurityRuleIds`: constantes estáveis no core, por exemplo
  `RFC9700-2.1-REDIRECT-EXACT-MATCH`, `RFC9700-2.1.1-PKCE`,
  `RFC9700-2.1.2-NO-FRONTCHANNEL-ACCESS-TOKEN` e `RFC9700-2.2.2-REFRESH-REPLAY`.
- `SecurityAssessmentStatus`: derivado com precedência `NonCompliant` > `Warning` > `Compliant`.
- `IRedirectUriValidator`: evolui para receber `RedirectUriValidationOptions` e `CancellationToken`; custom
  implementations continuam substituíveis, mas recebem a política efetiva do realm.
- `RedirectUriValidationOptions`: `Comparison` fechado em `Ordinal`/`OrdinalIgnoreCase` e
  `AllowWildcards`, com `Validate()` e construtor de cópia.
- O assessment não entra em `Client`, `Realm`, `Data.Configuration`, payloads, tabelas ou caches.

### Modelo, dados e persistência

```text
ServerOptions
  RedirectUriValidation RedirectUriValidationOptions  template global

RealmOptions
  RedirectUriValidation RedirectUriValidationOptions  cópia independente

RedirectUriValidationOptions
  Comparison RedirectUriComparison = Ordinal
  AllowWildcards bool = false

Configuration payloads após os predecessores
  ServerOptionsPayload v1 -> v1 (shape corrente reprovisionado)
  RealmOptionsPayload  v1 -> v1 (shape corrente reprovisionado)

RefreshToken
  FamilyId string                  obrigatório, opaco e realm-bound
  Generation int                   >= 0
  ConsumedTime DateTime?
  SuccessorToken/identity          vínculo interno sem expor handle em log

refresh_token_families (ou representação relacional equivalente decidida na Fase 4)
  realm_id string
  family_id/digest string
  revoked_at_utc datetime nullable
  state_version int
  primary/unique (realm_id, family_id/digest)
```

- Options continuam no payload JSON versionado de Configuration; provar roundtrip, versões exatas e cópia
  independente. Payload anterior não materializa defaults: falha fechado e exige reprovisionamento.
- Família/rotação pertencem a Operational; criar migrations novas SQLite/PostgreSQL e atualizar contratos/matriz.
- Não persistir assessment, findings, texto de UI ou status de aderência.
- Nunca persistir/logar handles de refresh em claro quando o backing puder usar digest.

### Arquitetura alvo

```text
RoyalIdentity/
  Models/Security/ (ou pasta coerente verificada na Fase 1)
    ClientSecurityAssessment
    ClientSecurityFinding
    enums + RuleIds
  Options/
    RedirectUriValidationOptions
    RedirectUriComparison
  Contracts/Defaults/
    DefaultRedirectUriValidator
  Contexts/Validators + Handlers + Responses/
    enforcement RFC 9700

RoyalIdentity.Data.Configuration/
  dados puros das opções/client quando necessário

RoyalIdentity.Storage.EntityFramework/
  materialização Configuration/Operational
  rotação/família atômica

RoyalIdentity.Storage.EntityFramework.Sqlite|PostgreSql/
  migrations providers

Futuro Admin/
  chama ClientSecurityAssessment.Create(client, realm.Options)
  localiza apresentação por RuleId
  não persiste assessment
```

### Segurança, concorrência e confiabilidade

- Assessment nunca é usado como autorização runtime; validators/handlers enforçam as regras diretamente.
- Redirect default usa comparação ordinal exata, sem wildcard, sem fragment e HTTPS.
- Token request com `code_verifier` e code sem `code_challenge` falha com `invalid_request`.
- Nenhum access token ou ID token é emitido na authorization response; discovery não anuncia suporte removido.
- Rotação de refresh não emite/persiste tokens antes de uma decisão atômica; um replay não cria dois sucessores.
- Retry tolerado usa o mesmo sucessor ainda ativo; replay real revoga a família antes de nova emissão.
- `private_key_jwt` continua usando `IReplayProtectionStore`; não duplicar cache/check-add.
- Authorization/login/consent/error aplicam proteção contra framing e referrer leakage.
- Logs nunca incluem code, verifier, assertion, refresh token, state, nonce ou handles/family ids em claro.

### Compatibilidade, migração e rollout

- Não preservar implicit/hybrid, password grant configurado, wildcard default, comparação ignore-case default ou
  refresh reutilizável.
- Atualizar seeds e fixtures diretamente para o novo estado válido.
- Criar migrations incrementais Operational dos providers para famílias de refresh; não criar migration
  relacional para os payloads Configuration nem executar migrations nos hosts.
- Preservar Server/Realm v1; versões diferentes falham fechadas, shapes v1 antigos são reprovisionados e não se
  criam modos de compatibilidade nem migration relacional/JSON.
- Configurações relaxadas de redirect permanecem possíveis por decisão explícita e aparecem como
  `NonCompliant`.
- Findings/RuleIds são contrato para o futuro Admin; renomeá-los depois exige atualização coordenada de UI,
  localização e testes.

---

## Ordem de execução

**Pré-condição do plano:** concluir Debt Closure e Localization; iniciar sobre Logging/mTLS simplificados e
payloads Server/Realm v1.

1. **Fase 1 (assessment)** — fixa o vocabulário e torna cada correção observável.
2. **Fase 2 (redirect)** — introduz a única compatibilidade configurável decidida.
3. **Fase 3 (authorization flow)** — depende da baseline de erros OAuth 2.1 e remove superfícies legadas antes
   de consolidar metadata/headers.
4. **Fase 4 (refresh)** — estende Operational com decisão atômica e depende de Q1.
5. **Fase 5 (HTTP/metadata/logs)** — fecha recomendações transversais sobre as superfícies restantes.
6. **Fase 6 (handoff/aceites)** — valida ambos os providers, hosts, documentação e rastreabilidade administrativa.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

---

## Fase 1 - Assessment determinístico e catálogo de regras

**Depende de:** DF1-DF6, DF10-DF12, DF14-DF16, DF20-DF22 e conclusão de
[plan-refactoring-debt-closure.md](plan-refactoring-debt-closure.md) e
[plan-localization.md](plan-localization.md).

**Escopo:** `RoyalIdentity/Models` (local exato decidido pela estrutura existente), `Tests.Identity`,
`Tests.Architecture`, documentação XML pública.

**O que/como:** implementar os tipos imutáveis, factory method e catálogo de regras sem DI, persistência ou
dependência de Admin. A avaliação cobre configuração atual, inclusive violações que fases posteriores tornarão
inexequíveis, para manter diagnóstico de dados manualmente construídos.

**Tarefas:**

- [ ] Mapear cada regra aplicável do RFC 9700 a `RuleId`, nível normativo, severidade e condição verificável.
- [ ] Criar enums de status, requirement level e severidade com valores definidos.
- [ ] Criar `ClientSecurityFinding` imutável e sem texto localizado de UI.
- [ ] Criar `ClientSecurityAssessment` com factory method estático e `Status` derivado.
- [ ] Avaliar PKCE, plain, response/grant types, redirect policy/valores, refresh para public client,
  client authentication e sender constraint observável.
- [ ] Documentar que `Compliant` significa “sem findings de configuração aplicáveis”, não certificação de deploy.
- [ ] Adicionar testes table-driven para cada regra, precedência de status, nulidade e determinismo.
- [ ] Adicionar guard arquitetural para impedir dependência de UI/Data/provider.
- [ ] Criar `Tests.Identity/Security/ClientSecurityAssessmentTests.cs` e
  `Tests.Architecture/Rfc9700BoundaryTests.cs`.

**Critérios de aceite:** duas chamadas com os mesmos objetos produzem valores equivalentes; não há interface,
registro DI, relógio, I/O ou entidade persistente; todo `RuleId` é único; cada relaxamento de redirect resulta em
`NonCompliant`; recomendações assimétricas/sender-constrained resultam em `Warning`, não falso `Compliant`; cada
filtro obrigatório seleciona ao menos um teste.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~ClientSecurityAssessmentTests"
dotnet test Tests.Architecture --filter "FullyQualifiedName~Rfc9700BoundaryTests"
```

### Resultado da Fase 1

*a preencher*

---

## Fase 2 - Validação configurável e segura de redirect URIs

**Depende de:** Fase 1, DF7-DF9, DF15, DF20-DF21 e conclusão de
[plan-refactoring-debt-closure.md](plan-refactoring-debt-closure.md) e
[plan-localization.md](plan-localization.md), com Server e Realm Options v1.

**Escopo:** `ServerOptions`, `RealmOptions`, nova option, `IRedirectUriValidator`,
`DefaultRedirectUriValidator`, validators authorize/code/end-session, serializers/materializers Configuration,
seeds, `Tests.Identity`, `Tests.Storage`, `Tests.Integration`.

**O que/como:** adicionar política realm-scoped copiada dos defaults globais, evoluir o contrato de validator e
enforçar formato seguro. Manter somente os dois relaxamentos pedidos — ignore-case e wildcard — ambos opt-in e
diagnosticados.

**Tarefas:**

- [ ] Criar `RedirectUriComparison` fechado em `Ordinal` e `OrdinalIgnoreCase`.
- [ ] Criar `RedirectUriValidationOptions`, copy constructor e `Validate()`.
- [ ] Adicionar a option a `ServerOptions` e `RealmOptions`, incluindo todos os copy constructors.
- [ ] Falhar antes de editar se `ServerOptionsPayloadSerializer.CurrentVersion != 1` ou
  `RealmOptionsPayloadSerializer.CurrentVersion != 1`.
- [ ] Preservar Server/Realm Options v1, mantendo leitura fail-closed de outras versões e reprovisionando
  seeds/fixtures do shape anterior sem migration relacional/JSON.
- [ ] Evoluir `IRedirectUriValidator` para receber options efetivas e `CancellationToken`.
- [ ] Reescrever o default validator para não inspecionar wildcard quando a flag estiver desligada.
- [ ] Aplicar a mesma política a authorize, code redemption e post-logout redirect.
- [ ] Recusar URI não absoluta, fragment, HTTP e esquema não HTTPS no authorization redirect.
- [ ] Validar patterns wildcard no carregamento/publicação de configuração para impedir patterns vazios ou
  equivalentes a open redirect.
- [ ] Atualizar seeds/fixtures para URIs HTTPS e defaults seguros; localhost HTTP deixa de ser aceito neste corte.
- [ ] Adicionar testes de case, wildcard, fragment, esquema, open redirect, cópia e isolamento entre realms.
- [ ] Criar `Tests.Identity/Validators/RedirectUriValidationTests.cs` e
  `Tests.Integration/Endpoints/RedirectUriPolicyTests.cs`; estender
  `Tests.Storage/Configuration/ConfigurationModelPayloadTests.cs` para Server/Realm v1.

**Critérios de aceite:** o default só aceita string idêntica em comparação ordinal; alterar case falha; wildcard
só funciona quando explicitamente habilitado; nenhum fragment/HTTP é aceito; dois realms podem ter políticas
distintas sem compartilhar instâncias; Server/Realm v1 preservam a option e outras versões falham fechadas;
cada filtro obrigatório seleciona ao menos um teste.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~RedirectUriValidationTests"
dotnet test Tests.Storage --filter "FullyQualifiedName~ConfigurationModelPayloadTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~RedirectUriPolicyTests"
```

### Resultado da Fase 2

*a preencher*

---

## Fase 3 - Authorization Code, PKCE e remoção do front-channel legado

**Depende de:** Fases 1-2, DF10-DF12, DF15, DF18, DF21-DF22 e conclusão de
[plan-oauth21-token-error-responses.md](plan-oauth21-token-error-responses.md) e
[plan-oidc-session-management.md](plan-oidc-session-management.md).

**Escopo:** `PkceValidator`, `PkceMatchValidator`, `AuthorizeMainValidator`, `AuthorizeHandler`,
`AuthorizeResponseFactory` entregue pelo plano de Session Management, `DiscoveryOptions`, `DiscoveryHandler`,
response modes/results, constants apenas se ficarem sem consumidores, seeds, `Tests.Integration`.

**O que/como:** tornar authorization code + PKCE o único fluxo interativo suportado, fechar downgrade e remover
emissão/anúncio de tokens no front-channel sem opção legada.

**Tarefas:**

- [ ] Consumir a regressão entregue por `plan-oauth21-token-error-responses.md` para token request com
  `code_verifier` e code sem `code_challenge`; não reimplementar a classificação nem os helpers de erro nesta
  fase.
- [ ] Consumir a taxonomia e o writer entregues pelo plano OAuth 2.1 sem reintroduzir helpers paralelos.
- [ ] Preservar rejeição de verifier ausente/incorreto e comparação em tempo constante.
- [ ] Manter `S256` obrigatório e `plain` anunciado enquanto a option/path runtime existirem; tratar a metadata
  como capacidade, não preferência, e gerar finding para client com `AllowPlainTextPkce=true` conforme DF22.
- [ ] Remover `token`, `id_token` e combinações implicit/hybrid do discovery e da validação global.
- [ ] Remover branches de emissão de access/identity token da `AuthorizeResponseFactory`, do
  `AuthorizeHandler` e dos response results ainda envolvidos; preservar na factory o caminho de authorization
  code e a decoração de `session_state` entregue pelo predecessor.
- [ ] Remover response modes/resultados exclusivos do fluxo legado quando não houver consumidor remanescente.
- [ ] Garantir que `AllowedResponseTypes` legado não reabilita suporte removido.
- [ ] Adicionar regression test que prova a ausência do password grant.
- [ ] Emitir e anunciar `iss` na authorization response conforme RFC 9207, com testes realm-aware.
- [ ] Atualizar assessment para refletir comportamento removido e dados de configuração residuais.
- [ ] Reutilizar `Tests.Integration/Endpoints/PkceTokenTests.cs` — onde a Fase 3 do plano de erros entregou as
  linhas de PKCE, inclusive o downgrade — e `Tests.Integration/Endpoints/TokenErrorTests.cs`, e criar
  `Tests.Integration/Endpoints/AuthorizationCodeOnlyTests.cs` para runtime, metadata, password grant e `iss`;
  manter `Tests.Integration/Endpoints/CodeSingleUseTests.cs` como regressão do consumo.

**Critérios de aceite:** discovery anuncia somente response types realmente executáveis; nenhuma authorization
response contém access token/ID token; downgrade de PKCE retorna `invalid_request`; metadata PKCE anuncia
exatamente `S256` e `plain` enquanto ambos forem suportados; authorization code continua single-use e bound a
client/redirect; password grant retorna grant não suportado; cada filtro obrigatório seleciona ao menos um teste.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~TokenErrorTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~AuthorizationCodeOnlyTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~CodeSingleUseTests"
```

### Resultado da Fase 3

*a preencher*

---

## Fase 4 - Rotação e detecção de replay de refresh tokens

**Depende de:** Fase 1, Q1, DF13, DF15, DF18, DF21, conclusão de
[plan-oauth21-token-error-responses.md](plan-oauth21-token-error-responses.md) e semânticas vigentes de
`plan-data-operational-storage.md`/`plan-data-storage-matrix.md`.

**Escopo:** `Client.RefreshTokenPostConsumedTimeTolerance`, `RefreshToken`, factory/handler,
`IRefreshTokenStore` e resultados de transição, `Data.Configuration`, adapter Configuration, seeds/fixtures de
client e property coverage, `Data.Operational`, adapter Operational, payload serializers, migrations
SQLite/PostgreSQL, `Tests.Storage`, `Tests.Integration`, matriz de storage.

**O que/como:** substituir reutilização/consumo isolado por rotação atômica realm-bound com família. Separar
construção de tokens de persistência quando necessário para que nenhuma credencial seja emitida antes da decisão
atômica.

**Tarefas:**

- [ ] Fechar Q1 no histórico e transformar a resposta em DF antes do primeiro edit.
- [ ] Modelar `FamilyId`, geração, sucessor e revogação sem expor handles em claro.
- [ ] Definir na matriz a nova operação atômica de rotação e seus outcomes: sucesso, retry idempotente,
  replay/família revogada, conflito e ausência.
- [ ] Implementar transação que consome o token atual e registra um único sucessor na mesma decisão.
- [ ] Retornar o mesmo sucessor ainda ativo em retry dentro da tolerância, sem criar ramo.
- [ ] Revogar a família quando houver reutilização fora da tolerância ou de ancestral após avanço.
- [ ] Impedir uso de qualquer membro de família revogada.
- [ ] Aplicar a resposta de Q1 como novo default de `Client.RefreshTokenPostConsumedTimeTolerance` e atualizar
  materialização Configuration, seeds, fixtures e property-coverage sem migration relacional de Configuration.
- [ ] Remover os dois caminhos de `TimeSpan.MaxValue` em `RefreshTokenHandler`: aceitação ilimitada em
  `IsWithinTolerance` e reuso do mesmo handle em `IssueRefreshTokenAsync` para token sliding.
- [ ] Responder replay e família revogada com `invalid_grant` pela baseline OAuth 2.1, sem recriar writer/helper.
- [ ] Refatorar criação/persistência para não gravar access/identity/refresh tokens antes de vencer a rotação.
- [ ] Preservar downscoping, audiences, claims mode, expiração absoluta/sliding e revogação por subject.
- [ ] Criar migrations incrementais SQLite/PostgreSQL e atualizar cleanup/purge.
- [ ] Adicionar contratos sequenciais, concorrentes e de crash/retry nos dois providers.
- [ ] Criar `Tests.Storage/Storage/Contracts/RefreshTokenFamilyStoreContractTests.cs` e
  `Tests.Integration/Endpoints/RefreshTokenRotationTests.cs`; estender
  `ConfigurationModelClientCoverageTests` e `ConfigurationMaterializationClientTests` para o novo default.

**Critérios de aceite:** exatamente um sucessor existe por geração; duas rotações concorrentes não retornam
sucessores distintos; retry tolerado retorna o mesmo refresh handle sucessor; replay real revoga a família;
tokens de família revogada falham; nenhum caminho aceita reutilização ilimitada; regras atuais de claims/resources
continuam verdes; o default do client sobrevive à materialização Configuration; erros de replay usam a baseline
OAuth 2.1; cada filtro obrigatório seleciona ao menos um teste.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~ConfigurationModelClientCoverageTests"
dotnet test Tests.Storage --filter "FullyQualifiedName~ConfigurationMaterializationClientTests"
dotnet test Tests.Storage --filter "FullyQualifiedName~RefreshTokenFamilyStoreContractTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~RefreshTokenRotationTests"
./scripts/Test-OperationalPostgreSql.ps1
```

### Resultado da Fase 4

*a preencher*

---

## Fase 5 - Segurança HTTP, metadata, client authentication e logs

**Depende de:** Fases 1-3, DF6, DF10, DF14, DF17, DF19-DF22 e conclusão de
[plan-oidc-session-management.md](plan-oidc-session-management.md),
[plan-refactoring-debt-closure.md](plan-refactoring-debt-closure.md) e
[plan-localization.md](plan-localization.md).

**Escopo:** `CspOptions`, middleware/response helpers, `LoggingOptions`/`LoggerExtensions`,
discovery/mTLS routing, hosts, `RoyalIdentity.Razor`, `Tests.Host`, `Tests.WebApp`,
`Tests.Integration`, `Tests.Architecture`, assessment.

**O que/como:** aplicar proteções independentes de client como enforcement de host/runtime; alinhar metadata ao
suporte real e limitar o assessment ao que os modelos conseguem provar.

**Tarefas:**

- [ ] Aplicar CSP `frame-ancestors` nas páginas authorize/login/consent/logout/error e demais resultados
  browser-facing, excluindo nominalmente apenas `check_session_iframe` conforme DF19.
- [ ] Aplicar `X-Frame-Options` como fallback coerente sem contradizer `frame-ancestors` e sem injetar `DENY` no
  OP iframe.
- [ ] Reexecutar `CheckSessionEndpointTests` e provar simultaneamente que o iframe permanece frameable e que as
  páginas sensíveis continuam protegidas.
- [ ] Aplicar `Referrer-Policy: no-referrer` a todas as páginas/respostas sensíveis.
- [ ] Garantir que redirects após requests com credenciais nunca usem 307; preferir 303 onde aplicável.
- [ ] Confirmar por teste que authorization endpoint não recebe CORS.
- [ ] Redigir `state`, `nonce`, assertions e tokens dos logs de erro. Authorization code e `code_verifier` já
  estão no piso obrigatório `LoggingOptions.AlwaysRedacted`; acrescentar ali, e não em `SensitiveValuesFilter`,
  o que também não puder depender de configuração.
- [ ] Consumir `LoggingOptions` sem `UseLogService` e ampliar somente `SensitiveValuesFilter`/redaction; não
  reintroduzir option ou branch removido pelo plano predecessor.
- [ ] Preservar o alias mTLS de revocation já corrigido para `BuildMtlsRevocationUrl`; corrigir/mapear somente
  endpoints restantes antes de anunciá-los e omitir aliases/métodos indisponíveis.
- [ ] Preservar `private_key_jwt` com backing de replay declarado e incluir seu uso no assessment.
- [ ] Provar por teste que `tls_client_auth` e `self_signed_tls_client_auth` autenticam de fato, antes de
  mantê-los anunciados. Entregue pela Fase 4 do plano de erros: o discovery os anuncia quando
  `MutualTls.Enabled`, e `DiscoveryTests.Get_WithMutualTlsEnabled_Must_AnnounceTheTwoMtlsMethodsOnTop` fixa a
  composição, mas nenhum teste exercita a autenticação — o test server in-memory não apresenta certificado de
  cliente. Os outros três métodos anunciados já têm caso de sucesso.
- [ ] Adicionar validação de startup para issuer, `AllowedHosts`/host filtering,
  `ForwardedHeadersOptions.AllowedHosts` e trusted proxies/networks necessários ao host; diagnosticar o uso de
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` sem uma fronteira de rede equivalente e não alegar que
  `ClientSecurityAssessment` prova o deployment.
- [ ] Atualizar findings de client authentication assimétrica e sender constraint como recomendações.
- [ ] Criar `Tests.Integration/Security/BrowserSecurityHeadersTests.cs`,
  `Tests.Integration/Security/SensitiveLoggingTests.cs` e
  `Tests.Integration/Endpoints/AuthorizationEndpointCorsTests.cs`; estender
  `Tests.Integration/Endpoints/DiscoveryTests.cs` para aliases/capacidades exatos.
- [ ] Estender `Tests.Architecture/Rfc9700BoundaryTests.cs` para impedir reintrodução de `UseLogService`, aliases
  mTLS construídos pela rota errada e dependências proibidas do assessment.

**Critérios de aceite:** nenhuma página sensível pode ser enquadrada por origem não autorizada; a única exceção é
o OP iframe, cuja resposta não contém `X-Frame-Options: DENY` nem `frame-ancestors 'none'`; referrer não carrega
parâmetros; logs capturados não contêm segredos/handles; discovery não anuncia rota/método inexistente;
authorization endpoint continua fora do CORS; startup falha ou alerta de forma explícita para configuração de
proxy/issuer insegura conforme o contrato fechado na fase; `UseLogService` não reaparece, revocation mTLS não
volta à rota de token e cada filtro obrigatório seleciona ao menos um teste.

**Testes:**

```powershell
dotnet build Tests.Host
dotnet build Tests.WebApp
dotnet test Tests.Integration --filter "FullyQualifiedName~CheckSessionEndpointTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~BrowserSecurityHeadersTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~SensitiveLoggingTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~AuthorizationEndpointCorsTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~DiscoveryTests"
dotnet test Tests.Architecture --filter "FullyQualifiedName~Rfc9700BoundaryTests"
```

### Resultado da Fase 5

*a preencher*

---

## Fase 6 - Handoff administrativo, aceites e fechamento

**Depende de:** Fases 1-5, DF1-DF22 e baseline concluída de `plan-replay-protection.md`.

**Escopo:** testes amplos, documentação pública, roadmap/backlog, READMEs de hosts,
`plan-data-storage-matrix.md`, referências do futuro Admin.

**O que/como:** provar o conjunto final, registrar limites do assessment e entregar ao futuro Admin um contrato
estável e documentado.

**Tarefas:**

- [ ] Executar assessment sobre todos os clients de seeds/fixtures e corrigir findings não intencionais.
- [ ] Documentar cada `RuleId`, requisito RFC, severidade, condição e remediação.
- [ ] Documentar para o futuro Admin: calcular em leitura/após edição, localizar por `RuleId`, agrupar
  `Compliant`/`Warning`/`NonCompliant` e nunca persistir o resultado.
- [ ] Atualizar roadmap e backlog para marcar a entrega do core e manter somente a apresentação UI no plano Admin.
- [ ] Atualizar a matriz de storage com contratos/migrations finais de refresh.
- [ ] Confirmar Server/Realm Options v1 e que nenhum plano posterior recriou uma cadeia numérica pré-release.
- [ ] Confirmar que todas as classes de teste nomeadas foram criadas e cada filtro obrigatório seleciona testes.
- [ ] Executar migrations/aceites SQLite e PostgreSQL de Configuration e Operational.
- [ ] Executar solução completa e registrar warnings/desvios no resultado da fase.
- [ ] Reauditar os MUST/MUST NOT aplicáveis do RFC 9700 e listar requisitos externos/de deployment sem
  classificá-los como falha de client.

**Critérios de aceite:** todas as fases estão concluídas; Q1 está fechada; assessment possui documentação
consumível pelo Admin; nenhum status derivado é persistido; roadmap/backlog/plano têm links bidirecionais;
matriz reflete os contratos; payloads finais permanecem Server/Realm v1; nenhuma classe/filtro obrigatório está vazio;
suites e aceites obrigatórios estão verdes.

**Testes:**

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
./scripts/Test-ConfigurationPostgreSql.ps1
./scripts/Test-OperationalPostgreSql.ps1
./scripts/Test-ServerPostgreSql.ps1
```

### Resultado da Fase 6

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(es) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| Defaults/runtime RFC 9700 | 2-5 | DF7-DF15, DF18-DF22 | redirects, PKCE, front-channel, refresh e HTTP seguros | classes nomeadas Identity/Integration/Storage |
| Assessment puro | 1 | DF1-DF6, DF21 | determinístico, sem DI/I/O/persistência | `ClientSecurityAssessmentTests` |
| Findings estáveis | 1, 6 | DF5, DF16 | RuleIds únicos e documentados | unitários + auditoria Fase 6 |
| Redirect configurável | 2 | DF7-DF9, DF20-DF21 | Ordinal/sem wildcard; Server/Realm v1 fail-closed | `RedirectUriValidationTests`; `RedirectUriPolicyTests`; payload |
| Replay de refresh | 4 | DF13, DF18, DF21 + resposta Q1 | sucessor único; família revogada; default Configuration | `RefreshTokenFamilyStoreContractTests`; `RefreshTokenRotationTests` |
| Handoff ao Admin | 6 | DF16 | contrato/documentação e links, sem snapshot | revisão documental + solução |
| Framing com exceção protocolar | 5 | DF10, DF19 | páginas sensíveis bloqueadas; OP iframe frameable | CheckSessionEndpoint + browser/header tests |
| Metadata PKCE fiel | 1, 3, 5 | DF6, DF14, DF22 | `S256`/`plain` refletem runtime; plain opt-in gera finding | Assessment + AuthorizationCodeOnly + Discovery |
| Testes sem falso verde | 1-6 | DF21 | classes nomeadas; comandos separados; zero filtros vazios | Identity + Integration + Storage + Architecture |

---

## Invariantes a preservar

1. Toda configuração, avaliação e persistência de client/refresh token permanece realm-scoped.
2. `ClientSecurityAssessment` é diagnóstico derivado, nunca gate de autorização runtime.
3. PKCE permanece default-on e authorization codes permanecem single-use.
4. Access tokens continuam audience/resource/scope-restricted.
5. Tolerância de refresh continua existindo para retry, mas nunca permite reuso ilimitado ou ramificação.
6. Signing keys continuam disponíveis para validação após emissão.
7. `RoyalIdentity` não depende de Data, providers, hosts, UI ou módulos.
8. `Data.*` permanece puro; regras vivem no core/adapter owner.
9. Hosts não executam migrations nem seeds implicitamente.
10. Nenhum segredo, code, verifier, token, assertion ou handle aparece em log.
11. Não reintroduzir password grant ou implicit/hybrid por extension grant acidental.
12. Não criar `OAuthSecurityProfile`, assessor DI ou snapshot persistido.
13. O hardening de clickjacking não bloqueia `check_session_iframe` e não amplia essa exceção a outra rota.
14. Debt Closure não é revertido: `UseLogService` permanece ausente e revocation mTLS usa sua rota própria.
15. Server e Realm Options permanecem em v1 durante o pre-release; serializers rejeitam outras versões.
16. Enquanto o runtime aceitar PKCE `plain` por opt-in, a metadata o anuncia e o assessment o diagnostica.
17. Nenhum comando filtrado obrigatório pode fechar fase selecionando zero testes.

---

## Critérios globais de conclusão

- Todos os MUST/MUST NOT aplicáveis ao authorization server e implementáveis no produto foram mapeados para
  enforcement, finding ou limite externo documentado.
- Defaults de redirect são ordinal/sem wildcard/HTTPS; relaxamentos são explícitos e não aderentes.
- Authorization flow não emite tokens no front-channel e fecha PKCE downgrade.
- Refresh tokens usam rotação/família com replay detection e retry sem ramificação.
- Browser/host/logs/metadata passam os critérios da Fase 5.
- Payloads Configuration permanecem em Server/Realm v1, falham fechados para outras versões e não ganham
  migration relacional/JSON.
- `code_challenge_methods_supported` reflete o runtime conforme DF22; `plain` não é apresentado como preferência.
- Assessment é puro, não persistido e consumível pelo futuro Admin.
- Todas as classes nomeadas existem e cada filtro obrigatório seleciona ao menos um teste.
- Roadmap, backlog, plano e matriz têm rastreabilidade consistente.
- `dotnet test RoyalIdentity.sln` e os três scripts PostgreSQL obrigatórios estão verdes.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| Assessment promete mais que consegue provar | UI chama `Compliant` de “deployment certificado” | falsa segurança | DF6; texto explícito e diagnóstico de host separado | Aberto |
| Relaxamento de redirect vira open redirect | wildcard amplo ou ignore-case habilitado | exfiltração de code | validação de pattern, finding alto e default seguro | Aberto |
| Remoção de implicit/hybrid deixa código morto | response handlers/constants sem consumers | superfície confusa e manutenção incorreta | busca de callers + architecture/integration tests | Aberto |
| Retry de refresh cria dois sucessores | duas requests vencem operações separadas | família bifurcada e replay aceito | decisão transacional única + contract concorrente | Aberto |
| Revogação perde corrida com sucessor | replay e rotação chegam juntos | token da família permanece ativo | estado de família verificado/movido na mesma transação | Aberto |
| Mudança de factory persiste token antes de vencer | token factory grava durante construção | credencial órfã/emitida ao perdedor | separar construção e persistência; teste de falha | Aberto |
| RuleId muda após integração Admin | rename sem coordenação | localização/UX quebradas | constantes estáveis + documentação + testes de unicidade | Aberto |
| Hardening duplica replay protection concluída | novo cache/check-add contorna `IReplayProtectionStore` | duas semânticas de replay e backing inconsistente | DF17 + guards existentes do plano concluído | Aberto |
| Fase 3 duplica a taxonomia OAuth 2.1 | helpers/códigos são recriados no hardening | contratos divergentes para o mesmo erro | DF18; consumir o plano de erros concluído | Aberto |
| Hardening bloqueia o OP iframe | regra global trata todo resultado browser-facing igualmente | Session Management anunciado deixa de funcionar | DF19 + regressão `CheckSessionEndpointTests` | Aberto |
| Payload pré-release parte da baseline errada | executor ignora Debt Closure/Localization ou ADR-020 | perda de options ou bump indevido | DF20 + gate Server/Realm v1 | Aberto |
| Filtro amplo mascara teste ausente | OR ou nome incidental seleciona outra fixture | fase fecha sem provar o hardening | DF21 + classes/comandos separados | Aberto |
| Metadata omite capacidade PKCE ativa | `plain` continua runtime, mas some de discovery | metadata deixa de representar o servidor | DF22 + teste exato de metadata/runtime | Aberto |

---

## Diferidos e backlog

- **Apresentação administrativa dos findings** — destino: item “API e UI Administrativa” do
  [plans-roadmap-02.md](plans-roadmap-02.md) e “UI Administrativa” do
  [backlog-001.md](../backlogs/backlog-001.md).
- **DPoP / sender-constrained access tokens** — destino: novo item de backlog se a Fase 5 confirmar demanda além
  do mTLS existente.
- **Native apps e loopback redirect** — destino: backlog próprio quando existir `ApplicationType` e fluxo mobile.
- **RFC 9711 / EAT e attestation-based client authentication** — destino: plano próprio experimental.
- **PAR / RFC 9126** — permanece no backlog existente.
- **Diagnóstico contínuo de deployment** — destino: operação/health/startup; não entra no assessment do client.

---

## Referências

- [RFC 9700 — OAuth 2.0 Security Best Current Practice](https://www.rfc-editor.org/rfc/rfc9700.html).
- [RFC 8414 — OAuth 2.0 Authorization Server Metadata](https://www.rfc-editor.org/rfc/rfc8414.html).
- [RFC 7636 — Proof Key for Code Exchange](https://www.rfc-editor.org/rfc/rfc7636.html).
- [RFC 9207 — OAuth 2.0 Authorization Server Issuer Identification](https://www.rfc-editor.org/rfc/rfc9207.html).
- [RFC 8705 — OAuth 2.0 Mutual-TLS Client Authentication](https://www.rfc-editor.org/rfc/rfc8705.html).
- [RFC 9449 — OAuth 2.0 Demonstrating Proof of Possession](https://www.rfc-editor.org/rfc/rfc9449.html).
- [plan-realm-options-redesign.md](plan-realm-options-redesign.md).
- [plan-data-storage-matrix.md](plan-data-storage-matrix.md).
- [plan-data-configuration-storage.md](plan-data-configuration-storage.md).
- [plan-data-operational-storage.md](plan-data-operational-storage.md).
- [plan-replay-protection.md](plan-replay-protection.md).
- [plan-oauth21-token-error-responses.md](plan-oauth21-token-error-responses.md).
- [plan-oidc-session-management.md](plan-oidc-session-management.md).
- [plan-refactoring-debt-closure.md](plan-refactoring-debt-closure.md).
- [plan-localization.md](plan-localization.md).
- [plans-roadmap-02.md](plans-roadmap-02.md).
- [backlog-001.md](../backlogs/backlog-001.md).
