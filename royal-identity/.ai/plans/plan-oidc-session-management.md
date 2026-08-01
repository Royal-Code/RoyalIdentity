# Plan: OpenID Connect Session Management, Check Session e atribuições Apache (`plan-oidc-session-management`)

## Status: EM EXECUÇÃO - Fases 1-3 concluídas; Fase 4 é a próxima; 3 de 7 fases concluídas

## Progresso

`███░░░░` **43%** - 3 de 7 fases

| Fase | Estado |
|---|---|
| Fase 1 - Contrato de estado do User Agent e opções | Concluida |
| Fase 2 - Ciclo de vida do estado no login, cookie e logout | Concluida |
| Fase 3 - Authentication Responses, `prompt=none` e payload operacional | Concluida |
| Fase 4 - Rota, discovery HTTPS e isolamento por realm | Pendente |
| Fase 5 - OP iframe moderno e hardening HTTP | Pendente |
| Fase 6 - Aceites HTTP, multi-realm e navegador real | Pendente |
| Fase 7 - Licenças, atribuições, documentação e fechamento | Pendente |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluída, `%` e `X de 7`). Antes de fechar uma fase, confirme que decisões,
> critérios de aceite, testes e invariantes relacionados foram aplicados.

---

## Contexto

### Fontes verificadas

- [OpenID Connect Session Management 1.0](https://openid.net/specs/openid-connect-session-1_0.html) — define
  `session_state`, RP iframe, OP iframe, `check_session_iframe`, vínculo à origem, atualização do OP User Agent
  State e limitações impostas por bloqueio de cookies de terceiros.
- [OpenID Connect Core 1.0 incorporating errata set 2](https://openid.net/specs/openid-connect-core-1_0.html)
  — `prompt=none` não pode exibir UI e deve produzir `login_required`, `consent_required`,
  `interaction_required` ou outro erro aplicável quando a requisição não puder ser concluída silenciosamente.
- [OpenID Connect Discovery 1.0 incorporating errata set 2](https://openid.net/specs/openid-connect-discovery-1_0.html)
  — metadata do OP e publicação de endpoints.
- [Apache License 2.0 §4](https://www.apache.org/licenses/LICENSE-2.0.html) — permite termos diferentes para
  modificações/obra derivada, mas exige cópia da licença, aviso de arquivos modificados e retenção de notices
  pertinentes.
- [GNU License Compatibility and Relicensing](https://www.gnu.org/licenses/license-compatibility.en.html) —
  Apache-2.0 é compatível com licenças GNU versão 3; a licença permissiva ainda acompanha a distribuição combinada.
- `../foundation/product.md`, `../foundation/tech.md`, `../foundation/structure.md` e
  `../foundation/architecture.md` — realms são a fronteira de isolamento; o ciclo de sessão pertence ao core;
  endpoints usam Minimal API + pipeline; o core não depende de host, UI, storage provider ou módulo.
- `../../adrs/ADR-001.md` — código herdado do IS4 pode ser reestruturado, substituído ou removido; o endpoint
  continua no modelo de Minimal API/pipeline do RoyalIdentity.
- `../../adrs/ADR-009.md` — toda funcionalidade realm-scoped exige cobertura com pelo menos dois realms.
- `../../adrs/ADR-014.md` e `../../adrs/ADR-017.md` — a sessão é criada no sign-in pelo `LoginFlowService`,
  validada pelo `IUserSessionService`, persiste fora do cookie e é encerrada pelo `DefaultSignOutManager`;
  não reintroduzir `IdentitySession`, `IUserStore`, `DefaultUserSession` ou store que leia `HttpContext`.
- `plan-realm-options-redesign.md` — registrou deliberadamente o `CheckSessionEndpoint` como código
  inalcançável até existir plano próprio; opções efetivas são resolvidas pelo realm atual.
- [plan-oauth21-token-error-responses.md](plan-oauth21-token-error-responses.md) — predecessor obrigatório que
  remove os helpers ambíguos usados por `ConsentDecorator`, fixa o writer/contrato de erro e limita sua alteração
  no authorize ao campo `error` do `ResourcesValidator`; ele não entrega o transporte por redirect do authorize.
  Este plano consome a baseline de código/writer e assume o redirect somente para a fatia
  `prompt=none`/Authentication Error Response aqui enumerada.
- `plan-rfc9700-security-hardening.md` — adicionará proteção geral contra framing; o OP iframe é uma exceção
  protocolar que precisa continuar embutível por RPs e não pode receber `frame-ancestors 'none'` ou
  `X-Frame-Options: DENY`.
- `plan-data-storage-matrix.md` e `plan-data-operational-storage.md` — o authorization code é operacional,
  single-use e materializado por payload versionado; mudanças nesse payload atualizam matriz e contratos sem
  reabrir suas semânticas atômicas.
- `C:/git/RoyalCode/RoyalIdentity/old-is4/src/IdentityServer4/src/Services/Default/DefaultUserSession.cs` —
  o IS4 cria um `sid`, o guarda nas `AuthenticationProperties`, o espelha em cookie legível por JavaScript e o
  remove no logout.
- `C:/git/RoyalCode/RoyalIdentity/old-is4/src/IdentityServer4/src/Extensions/ValidatedAuthorizeRequestExtensions.cs`
  — o IS4 calcula `session_state` sobre client, origem, estado do User Agent e salt.
- `C:/git/RoyalCode/RoyalIdentity/old-is4/src/IdentityServer4/src/ResponseHandling/Default/AuthorizeResponseGenerator.cs`
  e `Endpoints/AuthorizeEndpointBase.cs` — o IS4 gera `session_state` na Authentication Response, inclusive em
  respostas de erro possíveis, sem armazená-lo no authorization code.
- `C:/git/RoyalCode/RoyalIdentity/old-is4/LICENSE` — a cópia local do IS4 está sob Apache-2.0 e não contém
  arquivo `NOTICE` na raiz verificada.

### Estado atual do código (verificado em 2026-07-31)

- **Endpoint registrado, mas não mapeado:** `CheckSessionEndpoint` está na DI em
  `RoyalIdentity/Extensions/ServiceCollectionExtensions.cs`, mas não aparece em
  `MapOpenIdConnectProviderEndpoints`.
- **Discovery anuncia endpoint inalcançável:** `EndpointsOptions.EnableCheckSessionEndpoint` é `true` por default
  e `DiscoveryHandler` publica `check_session_iframe`; a URL anunciada retorna 404.
- **Resultado herdado do IS4:** `RoyalIdentity/Responses/HttpResults/CheckSessionResult.cs` conserva praticamente
  todo o HTML/JavaScript do IS4, inclusive SHA-256 manual, cache estático por nome de cookie e hash CSP fixo.
- **Cookie inexistente:** `AuthenticationOptions` contém nome, domínio e `SameSite` do check-session cookie, mas
  não existe `Response.Cookies.Append/Delete` para esse cookie no core.
- **Cálculo incompatível:** `DefaultSessionStateGenerator` usa hash de `sid + claims`; o iframe usa o valor do
  cookie. Como o cookie não é gravado, o estado não pode resultar consistentemente em `unchanged`.
- **Estado persistido no artefato errado:** `DefaultCodeFactory` gera `session_state` e o construtor de
  `AuthorizationCode` o persiste no payload operacional versão 1; `AuthorizeHandler` só o devolve quando há code.
- **Erros sem estado:** `AuthorizeErrorResponse` não possui `session_state`.
- **`prompt=none` não aderente:** `PromptLoginDecorator` pode produzir página de login quando a requisição contém
  `prompt=none`; `ConsentDecorator` responde `invalid_request` quando deveria responder `consent_required`.
- **Validação de `prompt` chega tarde:** `PromptLoginDecorator` executa antes de `ConsentDecorator`; portanto
  `none` combinado com `login`, `consent` ou `select_account` pode produzir UI antes da validação que deveria
  rejeitar a combinação como `invalid_request`.
- **Matriz silenciosa incompleta:** existem constantes para `interaction_required`, `login_required`,
  `account_selection_required` e `consent_required`, mas não há uma classificação única das condições de
  interação nem garantia de que todo `InteractionResponse` seja bloqueado sob `prompt=none`.
- **Configuração não realm-aware no resultado:** `CheckSessionResult` lê `ServerOptions` do snapshot e mantém
  cache estático global; realms concorrentes podem compartilhar a superfície de renderização.
- **Validação de origem incompleta:** o iframe usa `e.origin` no hash, mas não carrega a origem esperada emitida
  pelo servidor e não exige `e.source === window.parent`.
- **Cobertura parcial:** existe teste de presença de `session_state`, roundtrip do campo no authorization code e
  releitura do nome do cookie; não há teste do protocolo `postMessage`, transições `changed/unchanged/error`,
  HTTPS, feature gate ou navegador real.
- **Arquitetura de sessão disponível:** `LoginFlowService` cria a sessão e o cookie; o evento
  `CookieAuthenticationOptions.Events.OnValidatePrincipal` chama `IUserSessionService.IsSessionValidAsync`; o
  `DefaultSignOutManager` encerra a sessão e executa `SignOutAsync`.
- **Callback de cookie cacheado:** `ConfigureRealmCookieAuthenticationOptions` cria `OnValidatePrincipal` ao
  configurar o named scheme. O delegate é reutilizado; o serviço scoped e a derivação do check-session cookie
  precisam ser resolvidos dentro da invocação via `HttpContext.RequestServices`/realm corrente, sem captura do
  configurador, snapshot, realm ou manager.
- **Configuração persistida:** `ServerOptions` e `RealmOptions` são payloads JSON versionados, ambos atualmente
  na versão 1.
- **Payload operacional versionado:** `AuthorizationCodePayloadSerializer` e `AuthorizationCodePayload` incluem
  `SessionState` na versão 1 e seus contratos exigem roundtrip.
- **Licença principal e atribuição parcial:** `C:/git/RoyalCode/RoyalIdentity/LICENSE` contém AGPLv3 e o
  `README.md` referencia IS4/IdentityModel e Apache-2.0, mas não há cópia de Apache-2.0 na distribuição principal
  nem `THIRD-PARTY-NOTICES.md`.
- **Aviso incorreto no arquivo derivado:** o HTML atual do `CheckSessionResult` diz para consultar a licença
  Apache no `LICENSE` da raiz, mas esse arquivo contém AGPLv3.
- **Sem consumidores de produção:** breaking changes em contratos, payloads, cookies, defaults e opções são
  aceitos; não criar compatibility shims.
- **Comandos de teste com falsos verdes:** `Tests.Host` é um host `Microsoft.NET.Sdk.Web`, não um projeto de
  testes; filtros de Session State/Check Session em `Tests.Identity` não selecionam testes hoje. Há classes
  existentes como `SessionLifecycleTests` e `PromptInteractionCharacterizationTests`, mas filtros amplos não
  provam os novos aceites de Check Session e `prompt=none`.

### Lacunas, conflitos e restrições

- **OP iframe depende de estado coerente:** servidor e JavaScript precisam usar o mesmo valor opaco sem expor
  `sid`, `sub`, claims, `SecurityStamp` ou chaves de lookup do storage.
- **Cookie precisa ser JavaScript-readable:** `HttpOnly=false` é necessário para o modelo local do iframe; por
  isso o valor não pode identificar usuário nem conceder acesso a sessão persistida.
- **Cookies de terceiros são instáveis:** navegadores podem ocultar o cookie do OP dentro do RP; o protocolo
  admite falsos `changed` e exige defesa do RP contra loops. O OP não consegue distinguir de forma confiável
  logout real de cookie bloqueado.
- **Framing é necessário:** o hardening de clickjacking do restante do produto não pode impedir o framing do
  `check_session_iframe`.
- **Metadata exige HTTPS:** discovery nunca pode publicar `check_session_iframe` com esquema HTTP, inclusive
  quando o host está atrás de proxy mal configurado.
- **Authorization code é efêmero:** remover `SessionState` altera o payload operacional; a versão anterior pode
  ser invalidada diretamente porque não há produção e o lifetime default é 60 segundos.
- **Licenças têm escopos distintos:** AGPLv3 rege o RoyalIdentity combinado e suas modificações; atribuições e
  condições Apache-2.0 permanecem para material incorporado.
- **Sequenciamento protocolar:** este plano só começa após o plano OAuth 2.1, pois ambos alteram
  `ConsentDecorator` e a resposta de authorize; executar em paralelo recriaria helpers ou produziria conflito
  de transporte/classificação.
- **Erro não alcança o handler terminal:** decorators podem construir `AuthorizeErrorResponse` e encerrar o
  pipeline antes de `AuthorizeHandler`; logo a geração de `session_state` não pode pertencer apenas ao handler.

### Superfícies impactadas a mapear

- `RoyalIdentity/Authentication` — sincronização do OP User Agent State com o ticket e cookie realm-scoped.
- `RoyalIdentity/Users/Defaults` — criação no login, renovação/rejeição do ticket e remoção no logout.
- `RoyalIdentity/Contracts`, `Handlers`, `Responses` — geração de `session_state` na resposta.
- `RoyalIdentity/Endpoints`, `Extensions`, `Handlers/DiscoveryHandler.cs` — rota, feature gate e metadata HTTPS.
- `RoyalIdentity/Options` — simplificação/validação das opções de cookie e constantes internas.
- `RoyalIdentity.Storage.EntityFramework/Configuration` — versões dos payloads de opções.
- `RoyalIdentity.Storage.EntityFramework/Operational` e `RoyalIdentity/Models/Tokens` — retirada de
  `SessionState` do authorization code e versão do payload.
- `Tests.Identity`, `Tests.Integration`, `Tests.Storage`, novo aceite de navegador e `Tests.Architecture` —
  unidade, HTTP, realm, payload, composição e JavaScript real.
- `C:/git/RoyalCode/RoyalIdentity/README.md`, `LICENSES/`, `THIRD-PARTY-NOTICES.md` e arquivos derivados —
  licenciamento e proveniência.

---

## Objetivo

1. Implementar o OP side do OpenID Connect Session Management 1.0 com `session_state`,
   `check_session_iframe` e respostas `changed`, `unchanged` e `error`.
2. Manter um OP User Agent State opaco, sem PII e distinto do `sid`, sincronizado com o ticket e isolado por realm.
3. Gerar `session_state` em Authentication Responses OIDC bem-sucedidas e, quando houver redirect validado,
   em Authentication Error Responses.
4. Implementar `prompt=none` sem UI e com os erros OIDC corretos, permitindo ao RP confirmar uma mudança.
5. Publicar e servir o endpoint somente quando habilitado e sob origem HTTPS efetiva.
6. Proteger o iframe contra mensagens malformadas, fontes inesperadas, XSS e cache, sem bloquear seu framing
   protocolar.
7. Preservar AGPLv3 como licença do projeto combinado e cumprir as condições Apache-2.0 do código derivado.

## Fora de escopo

- Implementar biblioteca RP ou funcionalidade de Session Management dentro de aplicações clientes; o RP criado
  neste plano é somente harness de teste.
- Alterar o protocolo de Front-Channel Logout, Back-Channel Logout ou RP-Initiated Logout; continuam mecanismos
  separados e combináveis.
- Prometer detecção instantânea, pelo iframe já carregado, de revogação administrativa remota sem mudança no
  User Agent; Back-Channel Logout continua sendo o mecanismo adequado.
- Implementar Storage Access API, CHIPS ou bypass de políticas de tracking dos navegadores.
- Adicionar compatibilidade com navegadores sem Web Crypto API.
- Concluir os demais itens do RFC 9700 ou do OAuth 2.1; permanecem nos planos próprios.
- Alterar a escolha entre `AGPL-3.0-only` e `AGPL-3.0-or-later`; este plano preserva a declaração AGPLv3 atual.
- Realizar parecer jurídico; o plano implementa os requisitos textuais verificados e a rastreabilidade técnica.
- Corrigir toda a taxonomia/transporte do authorization endpoint; este plano consome o writer do predecessor e
  implementa e testa o transporte por redirect somente para a fatia OIDC necessária a `prompt=none` e
  `session_state`, depois que o redirect URI estiver validado.

---

## Decisões fechadas

- **DF1 — Baseline normativa:** implementar OpenID Connect Session Management 1.0 final e OpenID Connect Core
  1.0 errata set 2 para `prompt=none` e Authentication Error Responses. Fonte: decisão humana + especificações.
- **DF2 — Feature gate por realm:** `RealmOptions.Endpoints.EnableCheckSessionEndpoint` controla runtime e
  discovery; desabilitado significa 404 e metadata ausente. Fonte: arquitetura de options + comportamento IS4.
- **DF3 — Estado distinto do `sid`:** o OP User Agent State é aleatório, opaco e separado de
  `UserSession.Id`; nunca contém `sub`, claims, `SecurityStamp`, timestamps identificáveis ou handle de storage.
  Fonte: análise aceita pelo mantenedor + Session Management §3.2.
- **DF4 — Ticket protegido + cookie legível:** o estado canônico fica em `AuthenticationProperties.Items`,
  protegido pelo cookie de autenticação; uma cópia é espelhada no check-session cookie para o iframe.
  Fonte: adaptação do ciclo do IS4 à ADR-014.
- **DF5 — Cookie seguro e realm-scoped:** manter configurável somente o nome-base; remover domínio e `SameSite`
  configuráveis. O cookie efetivo usa nome qualificado pelo realm, `Path=/{realm}`, `Secure=true`,
  `SameSite=None`, `HttpOnly=false` e `IsEssential=true`. Fonte: análise de segurança + breaking changes aceitos.
- **DF6 — Ciclo no core:** um manager concreto/scoped de Check Session é chamado por `LoginFlowService`,
  `OnValidatePrincipal` e `DefaultSignOutManager`; não criar store, tabela ou tipo no módulo `UserAccounts`.
  Fonte: ADR-014/017.
- **DF7 — Recuperação determinística:** ticket autenticado sem OP User Agent State recebe novo valor,
  `ShouldRenew=true` e cookie sincronizado; cookie ausente/divergente é reemitido a partir do ticket, nunca aceito
  como fonte canônica. Fonte: confiabilidade do ciclo do cookie.
- **DF8 — Resposta, não code:** `session_state` é calculado na construção da Authentication Response OIDC;
  `AuthorizationCode.SessionState` e o campo correspondente do payload operacional são removidos. Fonte:
  especificação + desenho IS4 + breaking changes aceitos.
- **DF9 — Somente OIDC e com origem de navegador:** requisição OAuth sem identity scope `openid`, feature
  desabilitada, redirect ainda não validado ou redirect sem origem HTTP(S) de navegador não recebe
  `session_state`. Redirects de app nativo continuam recebendo o authorization code sem erro. Fonte: OpenID
  Connect Core + desenho IS4 + regressão verificada na Fase 1.
- **DF10 — Formato versionado e origin-bound:** usar formato sem espaços
  `v1.<origin-base64url>.<hash-base64url>.<salt-base64url>`; o hash usa codificação canônica e sem ambiguidades de
  versão, client id, origem exata, OP User Agent State e salt aleatório. O RP trata o valor como opaco.
  Fonte: Session Management §§2/3.2 + hardening da análise.
- **DF11 — Origem esperada explícita:** o iframe decodifica a origem emitida, exige igualdade ordinal com
  `MessageEvent.origin`, exige `event.source === window.parent` e nunca retorna `unchanged` para client/origem
  divergente. Fonte: Session Management §§3.1/3.2/6.
- **DF12 — JavaScript moderno:** substituir SHA-256 legado por `crypto.subtle.digest`, remover cache estático do
  HTML e serializar dados dinâmicos com encoder JSON/HTML; não copiar novamente o script legado do IS4.
  Fonte: análise aceita pelo mantenedor.
- **DF13 — Sem rede por polling:** o OP iframe recalcula localmente; não criar endpoint auxiliar chamado a cada
  `postMessage`. Fonte: objetivo do Session Management §3.
- **DF14 — Falta de cookie significa mudança:** estado anterior autenticado com cookie agora ausente retorna
  `changed`, permitindo detectar logout. A documentação alerta que bloqueio de cookie de terceiro produz o mesmo
  resultado e que o RP deve impedir loops. Fonte: Session Management §§3.2/5.1.
- **DF15 — HTTPS efetivo:** discovery só publica URL HTTPS; endpoint não serve a página por HTTP. Forwarded
  headers precisam determinar corretamente o esquema antes do protocolo. Fonte: Session Management §3.3 +
  `plan-rfc9700-security-hardening`.
- **DF16 — Exceção de framing delimitada:** o OP iframe recebe CSP estrita para script/conteúdo, mas não recebe
  `frame-ancestors 'none'` nem `X-Frame-Options: DENY`; login, consent e demais páginas mantêm o hardening de
  clickjacking do plano RFC 9700. Fonte: necessidade protocolar do iframe.
- **DF17 — Teste real opt-in:** adicionar aceite Playwright com Kestrel/HTTPS e dois origins; manter a suíte
  default autocontida sem download implícito de browser. Fonte: política atual de testes opt-in para dependências
  externas.
- **DF18 — Licenciamento combinado:** o projeto permanece AGPLv3; a distribuição principal recebe cópia da
  Apache-2.0, notices de terceiros e avisos de modificação/proveniência nos arquivos verificados como derivados.
  Fonte: decisão humana + Apache-2.0 §4.
- **DF19 — Sem infraestrutura IS4 legada:** não portar `DefaultUserSession`, `IUserSession` antigo,
  `IdentityServerAuthenticationService`, roteador próprio ou middleware monolítico do IS4. Fonte: ADR-001/014.
- **DF20 — Breaking change direto:** versões anteriores de cookies e payloads de configuração/authorization code
  podem ser invalidadas; atualizar seeds, serializers, testes e documentação sem fallback. Fonte: `AGENTS.md` +
  decisão humana.
- **DF21 — Predecessor obrigatório:** nenhuma fase começa antes da conclusão de
  `plan-oauth21-token-error-responses.md`; este plano consome seus helpers/writer finais e não reintroduz
  overloads. O predecessor não fornece o transporte de erro do authorize: o redirect delimitado em DF24 pertence
  a este plano. Fonte: ordem do roadmap + arquivos compartilhados verificados.
- **DF22 — Topologia verificável de testes:** algoritmo/formato puro fica em `Tests.Identity`; lifecycle de
  cookie, authorize, rota, discovery, headers e realms ficam em `Tests.Integration`; boundaries ficam em
  `Tests.Architecture`; navegador fica em `Tests.Browser` opt-in. Cada aceite novo possui classe nomeada e nenhum
  comando obrigatório pode selecionar zero testes. `Tests.Host` não é executado como test project. Fonte:
  infraestrutura e listagem empírica dos testes atuais.
- **DF23 — Resolução por request:** `OnValidatePrincipal` resolve `CheckSessionStateManager` scoped por
  `context.HttpContext.RequestServices` em cada invocação e o manager deriva realm, opções, nome e path do cookie
  naquele request; o delegate cacheado não captura manager, realm ou opções efetivas. Fonte: lifetime de named
  options + padrão já usado por `ValidateUserSessionAsync`.
- **DF24 — Factory delimitada de Authentication Response:** um `AuthorizeResponseFactory` interno/estático
  calcula `session_state` via `ISessionStateGenerator` e constrói somente as respostas de sucesso/erro assumidas
  por este plano: sucesso no `AuthorizeHandler`, `access_denied` já emitido por `ConsentDecorator` e os novos erros
  da matriz de `prompt=none`, inclusive a combinação inválida de prompts. Esses erros usam
  `AuthorizeErrorResponse` e redirect somente depois da validação de client/redirect URI, preservando `state` e
  response mode. `RedirectUriValidator`, `ResourcesDecorator`, `ResourcesValidator`,
  `AuthorizationResourcesValidator` e os demais terminadores não enumerados não são migrados para a factory; seu
  transporte permanece diferido. Os response objects apenas transportam o valor imutável. Seus construtores são
  deliberadamente `internal`: extensões adicionadas por `CustomizeAuthorizeContext` continuam podendo produzir
  `InteractionResponse` ou os erros genéricos públicos, mas não constroem diretamente uma Authentication Response
  nem contornam a validação de redirect e o cálculo centralizado. Não há consumidor externo de produção e o
  projeto não possui compromisso de compatibilidade nessa superfície. O alvo final do
  roadmap é somente authorization code, conforme DF12 de `plan-rfc9700-security-hardening.md`; esta fase não cria
  branches nem fixtures exclusivas para implicit/hybrid que o sucessor removerá, embora o caminho genérico de
  sucesso permaneça correto enquanto esses response types ainda forem alcançáveis. Fonte: decorators encerram
  antes do handler + OpenID Connect Core §3.1.2.6 + Session Management §2.
- **DF25 — Taxonomia silenciosa explícita:** validar `none` combinado com qualquer outro prompt antes de qualquer
  interação e retornar `invalid_request`; sob `none`, autenticação/reauth retorna `login_required`, consentimento
  retorna `consent_required`, seleção real de sessão retorna `account_selection_required` e outra interação não
  classificável retorna `interaction_required`. `select_account` sozinho continua um pedido de UI, não um erro
  silencioso; o modelo atual de principal único não inventa uma condição de seleção apenas para emitir um código
  inalcançável. Como decisão de produto, quando a autenticação atual usa IdP/método vedado pelas restrições do
  client, usar `login_required`: a sessão corrente não satisfaz a Authentication Request e seria necessária nova
  autenticação por um método permitido. Reservar `interaction_required` para interação adicional que não seja
  autenticação, consentimento ou seleção de conta. Um decorator anterior aos produtores de interação envolve toda
  a continuação e converte qualquer `InteractionResponse` sobrevivente sob `prompt=none`; isso inclui decorators
  terminais adicionados por `CustomizeAuthorizeContext`, mesmo quando não chamam `next()`. Fonte: OpenID Connect
  Core §§3.1.2.1/3.1.2.6 + comportamento legado do IS4.
- **DF26 — Proveniência delimitada e reproduzível:** a auditoria compara os roots upstream locais
  `old-is4/src/IdentityServer4/src` e `old-is4/src/IdentityModel` com arquivos-fonte de produção rastreados da
  solução, excluindo `bin`, `obj` e assets de dependências. Candidatos, evidência e classificação são registrados
  em inventário versionado e validados por script; a análise humana inicial continua necessária. Fonte:
  Apache-2.0 §4 + escopo local verificável.
- **DF27 — Test seam interno delimitado:** `SessionStateFormat` permanece `internal`; o assembly concede
  `InternalsVisibleTo` somente a `Tests.Identity` para vetores e parser puros, sem tornar o formato API pública e
  sem conceder acesso aos demais projetos. Fonte: topologia DF22 + implementação da Fase 1.
- **DF28 — Contratos capability-based do pipeline são preservados:** adicionar uma necessidade exclusiva de um
  branch não autoriza estreitar um decorator/validator inteiro para um contexto concreto. `RequestedPromptModes`
  pertence a `IWithPrompt`; `PromptLoginDecorator` continua `IDecorator<IWithPrompt>` e somente o branch que
  produz uma Authentication Response usa `AuthorizeContext`. Decorators, validators e handlers do core
  permanecem publicamente componíveis. `AuthorizeResponseFactory` é uma factory estática interna para que os
  consumidores públicos dependam apenas de `ISessionStateGenerator`, sem expor uma implementação nem criar uma
  interface artificial. `Tests.Architecture/PipelineComponentContractTests.cs` fixa visibilidade e contratos
  reutilizáveis. Fonte: revisão de design posterior à Fase 3 + histórico do contrato (`4acc7e5`).

---

## Histórico de decisões

**Análise de Check Session e comparação com IS4:**

- **Estado do navegador:** foi considerado copiar o `sid` persistido do IS4 para o cookie JavaScript-readable.
  - **Resposta humana:** criar plano conforme a análise que recomenda estado separado e opaco.
  - **Conclusão:** DF3-DF7.
- **Iframe:** foi considerado manter o `CheckSessionResult` legado já copiado.
  - **Resposta humana:** implementar o restante conforme a análise, que identificou cache global, SHA antigo,
    interpolação insegura e origem incompleta.
  - **Conclusão:** DF10-DF16.
- **Authorization code:** o estado atual persiste `session_state` no code.
  - **Consideração:** o IS4 o gera na Authentication Response e a especificação o define como parâmetro de
    resposta/browser.
  - **Conclusão:** DF8-DF9.
- **Infraestrutura de sessão:** foi avaliado portar o `DefaultUserSession` e o decorator de
  `IAuthenticationService` do IS4.
  - **Consideração:** ADR-014 já fornece `LoginFlowService`, ticket por realm, `IUserSessionService` e
    `DefaultSignOutManager`.
  - **Conclusão:** DF6/DF19.
- **Licença:** foi questionado se material Apache-2.0 pode integrar projeto AGPLv3.
  - **Resposta humana:** manter AGPLv3 e ajustar a distribuição conforme a análise.
  - **Conclusão:** DF18.

**Revisão externa de 2026-07-31:**

- **Sequenciamento e ownership de resposta:** confirmou-se a sobreposição com o plano OAuth 2.1 e que erros
  emitidos por decorators não alcançam `AuthorizeHandler`.
  - **Conclusão:** DF21 e DF24.
- **Testes:** confirmou-se que `Tests.Host` não executa testes e que filtros vazios retornam sucesso neste SDK.
  A afirmação de que `SessionLifecycleTests` não existia foi rejeitada: a classe já existe, mas não cobre o novo
  cookie/protocolo; filtros amplos continuavam insuficientes.
  - **Conclusão:** DF22 e classes de aceite nomeadas por fase.
- **Named options:** confirmou-se que `OnValidatePrincipal` é instalado em options cacheadas; capturar serviço
  scoped ou derivar o cookie no momento da configuração produziria lifetime/realm incorreto.
  - **Conclusão:** DF23.
- **`prompt=none`:** a necessidade de uma matriz foi aceita, mas a afirmação “`select_account` pede
  `account_selection_required`” foi restringida ao fluxo silencioso. `select_account` sozinho solicita UI;
  `none select_account` é combinação inválida; `account_selection_required` vale quando uma requisição com
  `none` exige seleção de sessão.
  - **Conclusão:** DF25.
- **Proveniência:** aceitou-se tornar o gate delimitado e verificável, mas não limitar a auditoria somente aos
  arquivos editados neste plano, pois isso poderia omitir material Apache já incorporado e distribuído.
  - **Conclusão:** DF26.

**Revisão residual de 2026-07-31:**

- **Transporte dos erros silenciosos:** confirmou-se que o predecessor OAuth 2.1 entrega apenas a baseline de
  código/writer e o ajuste mínimo do campo `error`; o transporte geral redirect versus JSON continua diferido.
  Este plano passou a possuir e enumerar o redirect somente dos erros de `prompt=none` que introduz.
  - **Conclusão:** DF21 e DF24.
- **Estados das fases:** somente a dependência externa da Fase 1 é bloqueio; dependências entre fases representam
  sequência normal.
  - **Conclusão:** Fases 2-7 permanecem `Pendente` até ficarem elegíveis.
- **Seleção de conta e IdP/método:** a ausência atual de seleção multi-account deve ser documentada, não provada
  por teste vazio. O mapeamento de IdP/método incompatível para `login_required` foi mantido como decisão de
  produto justificada, e não apresentado como única leitura possível da especificação.
  - **Conclusão:** DF25.

**Revisão externa da implementação da Fase 3 em 2026-08-01:**

- **Redirect confiável:** a afirmação foi confirmada pela flag de validação, pela ordem dos dois pipes e por três
  regressões HTTP: host atacante, porta não registrada e client inexistente nunca recebem `Location`.
- **Fallback `interaction_required`:** o relatório identificou corretamente que o enum existia, mas não havia uma
  rede capaz de observar produtores presentes ou futuros. `PromptNoneInteractionDecorator` passou a envolver toda
  a continuação posterior, inclusive customizações terminais, e há regressão que tenta produzir custom redirect
  sem chamar `next()`. O caso agora é executável, não um braço morto.
- **Guard do gerador:** aceita a crítica ao nome fixo do receptor; o scan usa qualquer invocação de
  `.GenerateSessionStateValue(` e continua limitado à factory.
- **Construtores de resposta:** o estreitamento foi mantido, mas deixou de ser efeito colateral: DF24 registra a
  decisão e um guard por reflexão prova que nenhuma construção pública contorna a factory.

---

## Design alvo

### Contratos e bordas

- `CheckSessionStateManager` (nome final pode seguir a convenção local, sem interface pública): serviço scoped
  do core que cria estado, grava/limpa cookie, sincroniza `AuthenticationProperties`, publica o valor canônico
  em `HttpContext.Items` e deriva o nome/path do realm.
- `ISessionStateGenerator.GenerateSessionStateValue(AuthorizeContext) -> string?`: mantém o seam existente,
  passa a retornar `null` quando DF2/DF9 não forem satisfeitas e usa o estado canônico da request.
- `AuthorizeResponseFactory` (nome final pode seguir convenção local, sem interface pública): factory estática
  interna e ponto único que
  invoca `ISessionStateGenerator` para as Authentication Responses no escopo deste plano. É usada no sucesso do
  `AuthorizeHandler`, no `access_denied` existente do consentimento e nos erros novos da matriz de `prompt=none`;
  não captura todos os terminadores do authorize.
- `AuthorizeResponse`: recebe o `session_state` calculado pela factory independentemente do authorization code.
- `AuthorizeErrorResponse`: recebe o `session_state?` calculado pela mesma factory quando a requisição já possui
  client, redirect e OIDC validados; não resolve serviços durante `CreateResponseAsync`.
- `PromptLoginDecorator`: permanece `IDecorator<IWithPrompt>`; somente o branch que emite `login_required` como
  Authentication Response exige `AuthorizeContext`, sem estreitar o contrato inteiro e sem mostrar UI nesse modo.
- `ConsentDecorator`: emite `consent_required` em `prompt=none`; `access_denied` continua sendo a decisão
  explícita do usuário.
- `CheckSessionEndpoint`: continua GET-only, aplica realm, feature gate e HTTPS antes de produzir a resposta.
- `CheckSessionResult`: HTML por request, nonce CSP por request, JavaScript Web Crypto e dados dinâmicos
  serializados.

### Matriz normativa de `prompt=none`

| Condição observável | Resultado | Observação |
|---|---|---|
| `none` combinado com `login`, `consent`, `select_account` ou outro valor | `invalid_request` | validar antes de qualquer decorator produzir UI |
| `none` e usuário ausente/inativo | `login_required` | nenhuma página de login |
| `none` e reautenticação necessária por `max_age` ou `UserSsoLifetime` | `login_required` | sessão atual não satisfaz a Authentication Request |
| `none` e método/IdP atual não satisfaz restrições do client | `login_required` | não iniciar seleção/login externo |
| `none` e consentimento prévio insuficiente ou política exige consentimento | `consent_required` | nenhuma página de consentimento |
| `none` e uma seleção real entre sessões/contas é necessária | `account_selection_required` | normativo, mas o modelo atual de principal único não possui essa condição |
| `none` e outra interação/custom redirect seria necessária | `interaction_required` | fallback explícito, sem UI |
| `select_account` sem `none` | fluxo interativo | não retornar `account_selection_required` apenas pela presença do valor |
| nenhuma interação necessária | resposta OIDC normal | inclui `session_state` conforme DF8-DF9 |

Todo erro alcançável da matriz é transportado por redirect sob responsabilidade deste plano, via
`AuthorizeErrorResponse`/factory, preserva `state` e recebe `session_state` somente depois de client, redirect URI
e origem estarem confiáveis. Do predecessor OAuth 2.1 é consumida apenas a baseline de código/writer; os erros de
validação que não pertencem a esta matriz mantêm o transporte atual até o plano geral do authorization endpoint.
`account_selection_required` não justifica criar estado multi-account fictício: o plano documenta sua não
aplicabilidade atual, sem teste de ausência ou branch sintético, e o código só ganha o branch quando existir uma
condição real de seleção. Já
`interaction_required` é o fallback fail-closed para qualquer produtor presente/futuro de interação sob `none`
que não seja autenticação, consentimento ou seleção de sessão.

### Modelo, dados e persistência

```text
AuthenticationProperties.Items
  check_session_opuas string
    - aleatório criptograficamente
    - mínimo 256 bits antes de Base64Url
    - protegido pelo ticket de autenticação
    - não persistido em Operational/UserAccounts

Check-session cookie
  name <base-name>.<realm-qualifier>
  value cópia de check_session_opuas
  Path /<realm>
  Secure true
  SameSite None
  HttpOnly false
  IsEssential true
  Domain ausente (host-only)

session_state v1
  v1.<origin-base64url>.<hash-base64url>.<salt-base64url>
  hash = SHA-256(canonical(version, client_id, exact_origin, opuas, salt))
  canonical = cada campo em UTF-8, na ordem acima, precedido por comprimento Int32 big-endian;
              salt entra como 32 bytes crus, também precedidos pelo comprimento
  nenhuma parte contém espaço

AuthorizationCodePayload v2
  remove SessionState
  preserva Subject, Scopes, Nonce, StateHash, PKCE e Properties

ServerOptionsPayload / RealmOptionsPayload v2
  AuthenticationOptions remove CheckSessionCookieDomain
  AuthenticationOptions remove CheckSessionCookieSameSiteMode
  AuthenticationOptions preserva CheckSessionCookieName
```

Não criar tabela, coluna ou store para OP User Agent State. Authorization codes payload v1 e options payload v1
podem falhar fechados após a mudança de versão; atualizar todos os seeds/fixtures no mesmo corte.

### Arquitetura alvo

```text
RoyalIdentity/
  Authentication/
    CheckSessionStateManager.cs
      ciclo HTTP/ticket/cookie realm-aware

  Users/Defaults/
    LoginFlowService.cs
      cria estado junto ao sign-in
    DefaultSignOutManager.cs
      remove estado junto ao sign-out

  Authentication/ConfigureRealmCookieAuthenticationOptions.cs
    valida sessão
    sincroniza/renova ou remove estado

  Contracts/Defaults/DefaultSessionStateGenerator.cs
    produz session_state versionado

  Responses/AuthorizeResponseFactory.cs
    único owner da geração nas Authentication Responses

  Handlers/AuthorizeHandler.cs
    solicita resposta de sucesso à factory

  Contexts/Decorators/
    PromptLoginDecorator.cs
    ConsentDecorator.cs
      classificam prompt=none e solicitam erro à factory

  Endpoints/CheckSessionEndpoint.cs
  Responses/CheckSessionResponse.cs
  Responses/HttpResults/CheckSessionResult.cs
    OP iframe

RoyalIdentity.Storage.EntityFramework/
  Configuration/Materialization/
    payloads de options v2
  Operational/Materialization/
    AuthorizationCodePayload v2 sem SessionState

Tests.Browser/
  harness RP + OP em Kestrel HTTPS
  Playwright opt-in
```

Topologia mínima de testes criada por este plano:

```text
Tests.Identity/Authentication/SessionStateFormatTests.cs
Tests.Integration/Users/CheckSessionCookieLifecycleTests.cs
Tests.Integration/Endpoints/AuthorizeSessionStateTests.cs
Tests.Integration/Endpoints/CheckSessionEndpointTests.cs
Tests.Architecture/CheckSessionBoundaryTests.cs
Tests.Browser/CheckSessionBrowserTests.cs
```

O projeto de navegador não entra como dependência de runtime. Se for adicionado à solution, seus testes devem
permanecer ignorados sem opt-in explícito e nunca baixar Chromium durante `dotnet test RoyalIdentity.sln`.

### Segurança, concorrência e confiabilidade

- Gerar estado e salt somente com `RoyalIdentity.Security.Cryptography.CryptoRandom`.
- Comparar client, origem e hash sem normalização relaxada; usar igualdade ordinal.
- Usar formato canônico com campos length-prefixed coberto por vetores compartilhados C#/JS.
- Não logar OP User Agent State, cookie, `session_state` completo ou salt associado a client/usuário.
- O cookie recebido nunca substitui o valor protegido do ticket; divergência causa sobrescrita.
- Rejeição/invalidação da sessão limpa o cookie antes da resposta terminar.
- `OnValidatePrincipal` resolve o manager scoped dentro da invocação; nenhum delegate de options cacheado captura
  serviço scoped, realm, snapshot efetivo ou nome de check-session cookie.
- Nome/path/opções do check-session cookie são derivados no request corrente; publicação inicial de um named
  scheme não congela configuração de outro realm.
- Login de outro usuário gera novo OP User Agent State; sliding do mesmo ticket preserva o valor.
- Logout remove o cookie com exatamente o mesmo nome, path, domínio ausente e flags usados na emissão.
- O iframe responde somente ao `window.parent`, usa `event.origin` como `targetOrigin` e ignora mensagens de
  outras janelas.
- Mensagem sintaticamente inválida retorna `error`; estado válido com User Agent State diferente retorna
  `changed`; estado válido e correspondente retorna `unchanged`.
- Origem válida gravada no estado e `event.origin` divergente nunca recebem `unchanged`.
- Resposta do iframe usa `Cache-Control: no-store`, `Pragma: no-cache`, `Referrer-Policy: no-referrer`,
  `X-Content-Type-Options: nosniff` e CSP com nonce/script restrito.
- A exceção de framing vale somente para o endpoint de Check Session.

### Compatibilidade, migração e rollout

- Não preservar cookies de Check Session antigos; novo nome realm-qualified e novo formato invalidam o legado.
- Bump direto dos payloads de options e authorization code; atualizar fixtures/seeds e aceitar que artefatos
  efêmeros v1 não sejam materializados após o deploy.
- Nenhuma migration relacional é esperada para remover campos JSON; se o executor encontrar projeção/coluna,
  deve atualizar ambos os providers e a matriz antes de concluir a fase.
- Discovery não anuncia suporte até rota, HTTPS, cookie lifecycle e iframe estarem funcionais no mesmo deploy.
- O plano RFC 9700 declara explicitamente a exceção exclusiva do OP iframe e mantém uma regressão de ausência de
  `X-Frame-Options: DENY`/`frame-ancestors 'none'`; este plano estabelece a baseline que essa regressão consome.
- A distribuição mantém `LICENSE` AGPLv3 e adiciona Apache-2.0 como licença de terceiro; não substituir o
  copyright original dos autores do IS4/IdentityModel.

---

## Ordem de execução

**Gate global:** satisfeito em 2026-08-01 — `plan-oauth21-token-error-responses.md` está concluído. Consumir seus
helpers e writer finais durante toda a execução.

1. **Fase 1 (contrato/options)** — fixa o valor canônico, formato e configuração antes de tocar login/respostas.
2. **Fase 2 (ciclo de vida)** — torna o estado real e sincronizado antes de gerar `session_state`.
3. **Fase 3 (responses/payload)** — move a geração para o boundary correto e habilita `prompt=none`.
4. **Fase 4 (rota/discovery)** — só torna o endpoint alcançável depois que o estado e as respostas são coerentes.
5. **Fase 5 (iframe/hardening)** — substitui o legado e fecha os requisitos de origem/headers.
6. **Fase 6 (aceites)** — valida protocolo, realms e browser real.
7. **Fase 7 (licenças/docs)** — fecha proveniência, referências, planos relacionados e suíte ampla.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

---

## Fase 1 - Contrato de estado do User Agent e opções

**Depende de:** conclusão de `plan-oauth21-token-error-responses.md`, DF1-DF5, DF8-DF10, DF18, DF20-DF22, DF27.

**Escopo:** `RoyalIdentity/Authentication`, `RoyalIdentity/Contracts`, `RoyalIdentity/Options`,
snapshot/serializers Configuration, `Tests.Identity`, `Tests.Integration`, `Tests.Storage`.

**O que/como:** introduzir o manager concreto/scoped, constantes internas e formato v1; simplificar
`AuthenticationOptions`; versionar payloads de server/realm e fixar vetores C#/JavaScript antes da integração.

**Tarefas:**

- [x] Criar `CheckSessionStateManager` no core sem interface pública, storage ou dependência de módulo.
- [x] Definir chaves internas de `AuthenticationProperties` e `HttpContext.Items` em `Constants.Server`.
- [x] Implementar geração criptográfica de OP User Agent State com pelo menos 256 bits.
- [x] Implementar derivação única do nome/path do cookie a partir de `AuthenticationOptions` e realm.
- [x] Remover `CheckSessionCookieDomain` e `CheckSessionCookieSameSiteMode`.
- [x] Validar nome-base vazio, caracteres de controle, separadores de cookie e colisões previsíveis.
- [x] Implementar parser/formatter do `session_state` v1 sem espaço e com origem Base64Url.
- [x] Implementar codificação canônica compartilhável com JavaScript e vetores determinísticos.
- [x] Criar `Tests.Identity/Authentication/SessionStateFormatTests.cs` para formato, parser, gates e vetores
  puros; não montar `AuthorizeContext`/HTTP artificial nesse projeto para testar pipeline.
- [x] Alterar `ISessionStateGenerator`/default para retorno anulável e gates DF2/DF9.
- [x] Incrementar `ServerOptionsPayloadSerializer` e `RealmOptionsPayloadSerializer` para versão 2.
- [x] Atualizar copy constructors, materialização, seeds e testes de property coverage das options.
- [x] Rejeitar configuração inválida antes da publicação do snapshot, inclusive colisão do nome efetivo.
- [x] Cobrir redirects HTTP, custom-scheme e URN: somente o primeiro recebe `session_state`; todos emitem code.

**Critérios de aceite:** estado contém entropia suficiente e nenhum dado de usuário/sessão persistida; cookie
derivado é host-only/realm-scoped; options removidas não aparecem no JSON v2; parser rejeita versões, segmentos,
Base64Url e espaços inválidos; um mesmo vetor produz exatamente o mesmo hash esperado.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~SessionStateFormatTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~Get_Signed_WithAValidatedRedirectWithoutBrowserOrigin"
dotnet test Tests.Storage --filter "FullyQualifiedName~ConfigurationModelPayloadTests"
dotnet test Tests.Storage --filter "FullyQualifiedName~ConfigurationSnapshotTests"
```

### Resultado da Fase 1

- Criado `CheckSessionStateManager` concreto e `scoped`, sem store/módulo/interface adicional. O manager gera
  32 bytes criptográficos, mantém as chaves internas de ticket/request, deriva nome `<base>.<realm>` e
  `Path=/<realm>` e centraliza as flags fixas host-only/`Secure`/`SameSite=None`/JavaScript-readable.
- Substituído o hash legado de `sid + claims` por envelope
  `v1.<origin-base64url>.<hash-base64url>.<salt-base64url>`. A entrada do SHA-256 usa cinco campos
  length-prefixed em Int32 big-endian, UTF-8 para os textos e salt cru de 32 bytes; parser, origem canônica,
  Base64Url sem padding e tamanhos de hash/salt falham fechados. O vetor fixo é diretamente reproduzível em JS.
- `ISessionStateGenerator` agora é anulável e aplica os gates de endpoint, `openid` e redirect validado. Até a
  retirada de `SessionState` do authorization code na Fase 3, o slot legado não anulável recebe string vazia
  somente nas requisições inelegíveis; o valor não é exposto na response.
- Removidas `CheckSessionCookieDomain` e `CheckSessionCookieSameSiteMode`; o contrato específico do cookie de
  check-session recusa nome-base inválido ou igual ao cookie de autenticação antes da publicação do snapshot, e
  a derivação também recusa colisão entre o nome efetivo e o cookie-base. O gate não antecipou validações
  vizinhas de `AuthenticationOptions`, que continuam com seus donos atuais. Server/Realm Configuration passaram
  de v1 para v2, rejeitam v1 e preservam/copiam o único setting restante. Não havia migration relacional nem
  versão de seed hardcoded fora dos serializers.
- Revisão externa: redirects custom-scheme/URN deixaram de lançar e agora omitem `session_state`; redirects HTTP
  continuam emitindo-o. A origem canônica usa `IdnHost`/punycode e serialização estável de IPv6. O risco C#/JS
  permanece aberto até as Fases 5-6 executarem o mesmo vetor no navegador.
- `InternalsVisibleTo` foi delimitado a `Tests.Identity` por DF27; não expõe o formato como contrato público.
- Verificação: `SessionStateFormatTests` 35/35; `ConfigurationModelPayloadTests` 20/20;
  `ConfigurationSnapshotTests` 13/13; regressão de redirects 3/3; build sem erros; suíte completa com 1.411
  aprovados, 51 ignorados por opt-in e nenhuma falha.

---

## Fase 2 - Ciclo de vida do estado no login, cookie e logout

**Depende de:** Fase 1, DF3-DF7, DF14, DF19, DF22-DF23.

**Escopo:** `LoginFlowService`, `ConfigureRealmCookieAuthenticationOptions`, `DefaultSignOutManager`,
registro DI, testes de sessão/cookie.

**O que/como:** integrar o manager aos três pontos já donos do ciclo HTTP sem mover lógica para
`IUserSessionStore`, UserAccounts ou decorator global de autenticação.

**Tarefas:**

- [x] Criar OP User Agent State ao preparar `AuthenticationProperties` no login bem-sucedido.
- [x] Gravar o check-session cookie com as flags fixas de DF5 no mesmo response do sign-in.
- [x] Preservar o estado em sliding/renovação do mesmo ticket.
- [x] Gerar estado e marcar `ShouldRenew=true` quando um ticket autenticado válido ainda não possuir a propriedade.
- [x] Resolver `CheckSessionStateManager` por `context.HttpContext.RequestServices` dentro de cada invocação de
  `OnValidatePrincipal`; não injetar/capturar o scoped no configurador de named options.
- [x] Derivar realm, opções, nome e path do check-session cookie dentro do manager por request; não fechar esses
  valores no delegate cacheado.
- [x] Publicar o valor canônico do ticket em `HttpContext.Items` durante `OnValidatePrincipal`.
- [x] Remover o fallback transitório de `GetOrCreateRequestState` na geração: depois da integração, o generator
  deve consumir somente o valor publicado pelo ticket; testar igualdade ticket → item → cookie → `session_state`.
- [x] Sobrescrever cookie ausente/divergente a partir do ticket protegido.
- [x] Limpar o cookie quando `OnValidatePrincipal` rejeitar sessão expirada, encerrada ou invalidada por estado.
- [x] Limpar o cookie no `DefaultSignOutManager` usando exatamente o nome/path de emissão.
- [x] Não gravar check-session cookie quando o endpoint estiver desabilitado para o realm; remover valor residual.
- [x] Cobrir login repetido do mesmo usuário, troca de usuário, logout e dois realms.
- [x] Criar `Tests.Integration/Users/CheckSessionCookieLifecycleTests.cs` cobrindo login, sliding,
  `OnValidatePrincipal`, rejeição, logout e isolamento entre realms.

**Critérios de aceite:** login válido produz ticket + cookie com o mesmo estado opaco; sliding não produz
`changed` espúrio; troca de usuário muda o estado; logout/rejeição removem o cookie; cookie de realm A não é
enviado nem aceito em realm B; nenhum valor aparece em logs.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~CheckSessionCookieLifecycleTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~SessionLifecycleTests"
```

### Resultado da Fase 2

- `LoginFlowService` prepara o OP User Agent State dentro de `AuthenticationProperties` antes do sign-in e emite,
  no mesmo response, a cópia JavaScript-readable com nome/path realm-scoped e as flags fixas de DF5. Cada novo
  login cria estado independente, inclusive login repetido do mesmo usuário e troca de usuário.
- `OnValidatePrincipal` resolve `CheckSessionStateManager` scoped por `RequestServices` em cada request, sem
  capturá-lo no delegate de named options. Sessão válida publica o valor do ticket em `HttpContext.Items` e
  repara cookie ausente/divergente; ticket legado/sem estado recebe novo valor e `ShouldRenew=true`; sliding
  preserva o estado existente. Sessão rejeitada remove o cookie legível.
- O fallback aleatório de request foi removido: `DefaultSessionStateGenerator` retorna `null` sem estado publicado
  pelo ticket. Um teste black-box percorre login → ticket → request → cookie → Authentication Response e
  recomputa independentemente o hash anunciado em `session_state`.
- `DefaultSignOutManager` remove o cookie com o mesmo nome/path usados na emissão. Feature desabilitada remove
  estado residual do ticket/cookie e não emite novo valor. Dois realms usam nomes, paths, tickets e estados
  distintos; cookie/ticket de um não autentica o outro.
- O valor opaco não entra em claims, storage ou logs. A implementação permanece no core e não altera
  `IUserSessionStore` nem o módulo `UserAccounts`.
- Verificação: `CheckSessionCookieLifecycleTests` 11/11; `SessionLifecycleTests` 12/12;
  `SessionStateFormatTests` 35/35; build sem erros; suíte completa com 1.422 aprovados, 51 ignorados por opt-in e
  nenhuma falha; `git diff --check` limpo.

---

## Fase 3 - Authentication Responses, `prompt=none` e payload operacional

**Depende de:** Fases 1-2, conclusão de `plan-oauth21-token-error-responses.md`, DF1, DF8-DF10, DF20-DF25.

**Escopo:** `AuthorizeHandler`, `DefaultCodeFactory`, `AuthorizationCode`, `AuthorizeResponse`,
`AuthorizeErrorResponse`, `AuthorizeMainValidator` somente no branch de combinação de prompts (ou validator
dedicado equivalente), `PromptLoginDecorator`, `ConsentDecorator`, payload/matriz Operational, `Tests.Identity`,
`Tests.Integration`, `Tests.Storage`.

**O que/como:** calcular `session_state` na factory delimitada de Authentication Responses OIDC; implementar a
matriz de erros silenciosos antes de qualquer UI e transportar esses erros por redirect depois da validação do
redirect URI; remover o estado do authorization code e versionar seu payload sem alterar consumo atômico.

**Tarefas:**

- [x] Remover `ISessionStateGenerator` de `DefaultCodeFactory`.
- [x] Remover `SessionState` dos construtores/propriedades de `AuthorizationCode`.
- [x] Remover `SessionState` de `AuthorizationCodePayload` e incrementar o serializer para versão 2.
- [x] Atualizar `.ai/plans/plan-data-storage-matrix.md` para declarar que `session_state` não pertence ao code.
- [x] Atualizar contratos SQLite/PostgreSQL, payload tests, parity e fixtures sem alterar binding/single-use.
- [x] Criar `AuthorizeResponseFactory` interna/estática, mantendo `ISessionStateGenerator` como dependência
  pública dos componentes de pipeline; ela é o único ponto de cálculo para as Authentication Responses
  enumeradas em DF24.
- [x] Fazer o sucesso do `AuthorizeHandler`, o `access_denied` atual de `ConsentDecorator` e os novos erros de
  `prompt=none` de `PromptLoginDecorator`/`ConsentDecorator` construir respostas pela factory.
- [x] Encaminhar `none` combinado com outro prompt, hoje encerrado por `AuthorizeMainValidator`, à mesma factory
  (diretamente ou por validator dedicado), somente após client e redirect URI válidos.
- [x] Não migrar `RedirectUriValidator`, `ResourcesDecorator`, `ResourcesValidator`,
  `AuthorizationResourcesValidator` ou outros terminadores fora de DF24 para essa factory.
- [x] Gerar `session_state` pela factory no caminho genérico de Authentication Response OIDC bem-sucedida, com
  authorization code como alvo final; não adicionar cases ou testes exclusivos de `token`, `id_token` ou
  combinações implicit/hybrid que a DF12 do plano RFC 9700 removerá.
- [x] Garantir que OAuth authorization sem `openid` não receba o parâmetro.
- [x] Estender `AuthorizeErrorResponse` com `session_state?`.
- [x] Rejeitar `none` combinado com qualquer outro prompt como `invalid_request` antes de
  `PromptLoginDecorator`/`ConsentDecorator` produzirem UI.
- [x] Validar a lista bruta de valores de `prompt` antes que valores desconhecidos sejam ignorados por `Load`,
  pois `none` combinado com qualquer outro valor deve falhar mesmo quando esse valor não é suportado.
- [x] Implementar todas as linhas atualmente alcançáveis da matriz de `prompt=none`; manter
  `interaction_required` como fallback para interação não classificada e documentar a não aplicabilidade atual de
  `account_selection_required` sem inventar estado multi-account ou teste de ausência.
- [x] Envolver todos os produtores posteriores de interação, inclusive customizações terminais, e converter sob
  `prompt=none` qualquer `InteractionResponse` sobrevivente em `interaction_required`, sem renderizar UI.
- [x] Preservar `select_account` sem `none` como fluxo interativo; não convertê-lo automaticamente em
  `account_selection_required`.
- [x] Preservar `state`, response mode e redirect URI validado nos erros; assumir nesta fase o transporte por
  redirect de todas as condições alcançáveis da matriz, sem depender do predecessor para entregá-lo.
- [x] Incluir `session_state` nos erros OIDC quando client/origem/redirect já forem confiáveis.
- [x] Cobrir usuário anônimo, sessão ativa, sessão trocada e novo `prompt=none` após `changed`.
- [x] Criar `Tests.Integration/Endpoints/AuthorizeSessionStateTests.cs` com tabela condição → erro, ausência de
  UI, `state`, response mode, redirect e `session_state` em sucesso/erro aplicável.

**Critérios de aceite:** authorization code não contém/persiste `session_state`; somente a factory calcula o
valor das respostas enumeradas em DF24; o caminho genérico de sucesso OIDC contém valor sem espaços e não ganha
branches exclusivos para response types legados; authorization code é o alvo final do roadmap;
resposta OAuth pura não contém; cada linha alcançável da matriz silenciosa possui teste, a não aplicabilidade de
seleção de conta está explícita sem teste vazio e `prompt=none` nunca renderiza UI; erros corretos chegam ao
redirect com `state` e, quando possível, `session_state`; terminadores fora de DF24 não são migrados; consumo
atômico do code permanece verde.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~OperationalPayloadTests"
dotnet test Tests.Storage --filter "FullyQualifiedName~SqliteOperationalAuthorizationCodeTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~AuthorizeSessionStateTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~CodeAuthorizeTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~PromptInteractionCharacterizationTests"
dotnet test Tests.Architecture --filter "FullyQualifiedName~AuthorizeResponseBoundaryTests"
dotnet test Tests.Architecture --filter "FullyQualifiedName~PipelineComponentContractTests"
```

### Resultado da Fase 3

- `session_state` saiu de `DefaultCodeFactory`, de `AuthorizationCode` e do payload operacional. O serializer de
  authorization code passou para v2 e rejeita v1 fail-closed; os contratos SQLite, fixtures, paridade e consumo
  atômico single-use continuam verdes. A matriz de storage registra explicitamente que o valor pertence à
  Authentication Response, não ao code.
- `AuthorizeResponseFactory` é interna/estática e o único ponto que invoca `ISessionStateGenerator` ou constrói
  `AuthorizeResponse`/`AuthorizeErrorResponse`. Guards arquiteturais fixam também os cinco consumidores
  permitidos: `AuthorizeHandler`, `ConsentDecorator`, `PromptLoginDecorator`,
  `PromptNoneInteractionDecorator` e `AuthorizeMainValidator`. Os construtores das duas respostas são
  deliberadamente internos e cobertos por reflexão; terminadores anteriores à confiança em client/redirect
  permanecem no transporte JSON existente.
- O caminho genérico de sucesso emite estado apenas para OIDC elegível. OAuth sem `openid` não recebe o parâmetro;
  redirect sem origem de browser e request sem estado autenticado continuam fail-closed. `AuthorizeErrorResponse`
  preserva `state`, query/form_post e inclui o estado quando ele pode ser calculado.
- A lista bruta distinta de prompts é preservada antes do filtro de suporte: `none` com `login` ou mesmo com valor
  desconhecido responde `invalid_request`. Usuário anônimo, reautenticação (`max_age`), restrições de método/IdP e
  SSO vencido convergem em `login_required`; consentimento necessário responde `consent_required`; interação não
  classificada tem fallback executável `interaction_required`: `PromptNoneInteractionDecorator` envolve os
  produtores posteriores, inclusive customizações terminais, e impede que um `InteractionResponse` sobreviva. O
  modelo atual não possui seleção multi-account, portanto
  `account_selection_required` permanece explicitamente sem condição alcançável e sem teste vazio;
  `select_account` isolado continua interativo.
- `AuthorizeSessionStateTests` cobre sucesso OIDC/OAuth, erros silenciosos, ausência de UI, query/form_post,
  preservação de `state`, consentimento, sessão trocada, novo `prompt=none` e uma customização terminal que tenta
  produzir redirect interativo. O fluxo real de negação de consentimento prova `access_denied` com `session_state`.
- A primeira suíte transversal detectou que o nome inicial `responseFactory.Error(context, code, ...)` recriava a
  forma posicional ambígua proibida pelo plano OAuth 2.1. A operação foi renomeada para `CreateError`, e o guard
  protocolar voltou a ficar verde.
- A revisão posterior corrigiu um estreitamento indevido: `RequestedPromptModes` passou para `IWithPrompt`,
  `PromptLoginDecorator` voltou a `IDecorator<IWithPrompt>` e os quatro componentes alterados recuperaram sua
  visibilidade pública. A auditoria de todos os decorators, validators e handlers não encontrou outro contrato
  recentemente estreitado; corrigiu ainda a inconsistência antiga de `RedirectUriValidator`, agora público como
  os demais componentes. Um guard arquitetural fixa essas decisões (DF28).
- Verificação final: aceites focados com 13 `AuthorizeSessionStateTests`, 32 regressões authorize/UI, 67 testes
  diretos de payload/authorization code, 4 guards da factory e 13 guards de contratos/visibilidade do pipeline;
  build sem erros; suíte completa com 1.453
  aprovados, 51 ignorados por opt-in e nenhuma falha; `git diff --check` limpo.

---

## Fase 4 - Rota, discovery HTTPS e isolamento por realm

**Depende de:** Fases 1-3, DF2, DF9, DF15, DF22, ADR-009.

**Escopo:** `CheckSessionEndpoint`, `EndpointRouteBuilderExtensions`, `DiscoveryHandler`, options efetivas,
forwarded scheme, testes HTTP/realm.

**O que/como:** mapear a rota existente; aplicar gate no endpoint e no discovery; impedir anúncio/serviço HTTP;
usar apenas opções do realm corrente.

**Tarefas:**

- [ ] Mapear `CheckSessionEndpoint` em `MapOpenIdConnectProviderEndpoints`.
- [ ] Retornar 404 quando o endpoint estiver desabilitado no realm.
- [ ] Preservar GET como único método e retornar 405 para métodos diferentes quando habilitado/HTTPS.
- [ ] Recusar servir o iframe quando a origem pública efetiva não for HTTPS.
- [ ] Publicar `check_session_iframe` somente quando endpoint e HTTPS forem efetivos.
- [ ] Construir URL com path do realm e esquema/host/port externos após forwarded headers.
- [ ] Resolver nome do cookie e CSP por `HttpContext.GetCurrentRealm()`, não por `ServerOptions` global.
- [ ] Remover cache estático cross-realm do resultado.
- [ ] Adicionar testes com dois realms: habilitado/desabilitado e nomes de cookie diferentes.
- [ ] Adicionar regressão garantindo que discovery nunca anuncie URL HTTP ou morta.
- [ ] Criar `Tests.Integration/Endpoints/CheckSessionEndpointTests.cs` para rota, métodos, feature gate, HTTPS,
  metadata, headers e isolamento de realm.

**Critérios de aceite:** discovery HTTPS aponta para GET 200 no mesmo realm; desabilitado produz ausência de
metadata + 404; HTTP não produz página nem metadata; realm A não usa opções/HTML/cookie de B; POST produz 405
somente quando a rota está efetivamente disponível.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~CheckSessionEndpointTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~DiscoveryTests"
```

### Resultado da Fase 4

*a preencher*

---

## Fase 5 - OP iframe moderno e hardening HTTP

**Depende de:** Fases 1 e 4, DF10-DF16, DF18, DF22.

**Escopo:** `CheckSessionResult`, CSP helpers/middleware, JavaScript inline, testes de HTML/headers/vetores.

**O que/como:** substituir o script IS4 por implementação curta baseada em Web Crypto; serializar dados;
validar parent/origin/formato; aplicar headers sem bloquear framing.

**Tarefas:**

- [ ] Remover SHA-256 manual, cache estático e substituição textual de `{cookieName}`.
- [ ] Implementar hash assíncrono com `crypto.subtle.digest`.
- [ ] Serializar nome do cookie e constantes do formato com encoder JSON/HTML.
- [ ] Gerar nonce CSP criptográfico por response e aplicá-lo ao único script inline.
- [ ] Exigir `window.parent !== window` e `event.source === window.parent`.
- [ ] Rejeitar payload não string, espaços inválidos, client vazio, versão/segmentos/Base64Url/origem inválidos.
- [ ] Comparar a origem incorporada com `event.origin` usando igualdade exata antes de responder.
- [ ] Recalcular o hash com client, origem, cookie e salt; responder apenas `error`, `changed` ou `unchanged`.
- [ ] Usar `event.source.postMessage(result, event.origin)` sem target `*`.
- [ ] Aplicar `no-store`, `no-cache`, `no-referrer`, `nosniff` e content type HTML com charset UTF-8.
- [ ] Aplicar CSP `default-src 'none'` + nonce necessário, sem `frame-ancestors 'none'`.
- [ ] Adicionar teste HTTP que exija ausência de `X-Frame-Options: DENY` e de
  `frame-ancestors 'none'` na resposta do OP iframe, criando a regressão que o plano RFC 9700 deverá preservar.
- [ ] Adicionar aviso de derivação/modificação conforme DF18 ou documentar reescrita independente verificável.
- [ ] Criar testes de vetores que comparem o cálculo de C# com o algoritmo exposto ao JavaScript.

**Critérios de aceite:** o resultado não contém implementação SHA legada, interpolação crua ou estado estático;
o CSP permite somente o script com nonce; endpoint continua frameable; origem diferente nunca recebe
`unchanged`; mensagens inválidas retornam `error`; headers de cache/referrer/content estão presentes.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~SessionStateFormatTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~CheckSessionEndpointTests"
```

### Resultado da Fase 5

*a preencher*

---

## Fase 6 - Aceites HTTP, multi-realm e navegador real

**Depende de:** Fases 2-5, DF11-DF17, DF22, ADR-009.

**Escopo:** `Tests.Integration`, `Tests.Architecture`, novo projeto `Tests.Browser` com Playwright opt-in,
`Tests.WebApp` se reutilizado como RP, scripts.

**O que/como:** validar o protocolo completo em HTTP e em Chromium real com OP/RP em origins distintos; manter
browser fora da suíte default e fornecer um comando reprodutível.

**Tarefas:**

- [ ] Criar projeto/harness xUnit de browser sem dependência de runtime dos produtos.
- [ ] Subir OP e RP reais em Kestrel HTTPS com certificado efêmero e ports/origins distintos.
- [ ] Criar página RP mínima que carrega o OP iframe oculto e usa `postMessage` com target origin exato.
- [ ] Instalar Chromium somente pelo script opt-in `scripts/Test-CheckSessionBrowser.ps1`.
- [ ] Testar estado corrente retornando `unchanged`.
- [ ] Testar logout/troca de usuário retornando `changed`.
- [ ] Testar mensagem malformada retornando `error`.
- [ ] Testar client errado e origem diferente nunca retornando `unchanged`.
- [ ] Testar `event.source` que não seja parent sendo ignorado/rejeitado.
- [ ] Testar `prompt=none` após `changed`: mesmo usuário atualiza estado; usuário ausente retorna
  `login_required`; consentimento ausente retorna `consent_required`.
- [ ] Testar dois realms com cookies, paths, opções e estados independentes.
- [ ] Simular cookie indisponível e verificar `changed` sem loop infinito no harness RP.
- [ ] Garantir que `dotnet test RoyalIdentity.sln` não baixe nem exija browser.
- [ ] Criar `Tests.Architecture/CheckSessionBoundaryTests.cs` e adicionar guardas contra referência de
  Playwright nos projetos de runtime e contra captura de serviços scoped no configurador de cookie.
- [ ] Nomear a fixture opt-in `Tests.Browser/CheckSessionBrowserTests.cs`.

**Critérios de aceite:** script opt-in passa todos os cenários em Chromium; suite default passa sem browser
instalado; nenhum teste usa target origin `*`; dois realms não compartilham estado; `changed` conduz ao fluxo
silencioso correto.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~CheckSessionEndpointTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~AuthorizeSessionStateTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~CheckSessionCookieLifecycleTests"
dotnet test Tests.Architecture --filter "FullyQualifiedName~CheckSessionBoundaryTests"
./scripts/Test-CheckSessionBrowser.ps1
```

### Resultado da Fase 6

*a preencher*

---

## Fase 7 - Licenças, atribuições, documentação e fechamento

**Depende de:** Fases 1-6, DF18, DF20-DF22, DF26.

**Escopo:** raiz Git `C:/git/RoyalCode/RoyalIdentity`; roots upstream
`old-is4/src/IdentityServer4/src` e `old-is4/src/IdentityModel`; arquivos-fonte de produção rastreados da solução
com exclusão explícita de `bin`, `obj` e assets vendorizados; inventário de proveniência, README, fundações,
planos relacionados e suíte integral.

**O que/como:** tornar explícita a combinação AGPLv3 + material Apache-2.0, auditar proveniência conhecida,
documentar o recurso e fechar rastreabilidade/testes.

**Tarefas:**

- [ ] Criar `C:/git/RoyalCode/RoyalIdentity/LICENSES/Apache-2.0.txt` com cópia integral de
  `old-is4/LICENSE`.
- [ ] Criar `C:/git/RoyalCode/RoyalIdentity/THIRD-PARTY-NOTICES.md` com IS4, IdentityModel, autores/origens,
  licença e escopo de material incorporado.
- [ ] Atualizar o README para declarar AGPLv3 para a obra combinada e apontar para `LICENSE`,
  `LICENSES/Apache-2.0.txt` e `THIRD-PARTY-NOTICES.md`.
- [ ] Remover referências enganosas de arquivos Apache ao `LICENSE` AGPL da raiz.
- [ ] Criar inventário versionado com cada candidato, upstream, evidência, classificação
  (`derivado`, `independente` ou `a confirmar`) e ação; nenhum item `a confirmar` pode permanecer no fechamento.
- [ ] Inventariar candidatos dentro dos roots delimitados por DF26 usando basename, histórico e comparação de
  conteúdo; registrar proveniência no notice ou no próprio arquivo quando derivado.
- [ ] Preservar copyrights/atribuições pertinentes e marcar de forma proeminente os arquivos modificados.
- [ ] Não aplicar cabeçalho Apache a arquivos comprovadamente escritos de forma independente apenas com base em
  especificações públicas.
- [ ] Verificar novamente se a distribuição upstream local possui `NOTICE`; se surgir, transportar os notices
  pertinentes conforme Apache-2.0 §4(d).
- [ ] Documentar Session Management, limitações de third-party cookie, relação com os três logout specs e
  configuração por realm.
- [ ] Atualizar `product.md`, `tech.md` e `structure.md` para refletir endpoint realmente implementado,
  manager e testes opt-in.
- [ ] Atualizar `plan-realm-options-redesign.md` removendo a dívida de endpoint inalcançável sem reescrever seu
  histórico concluído.
- [ ] Confirmar que `plan-rfc9700-security-hardening.md` exclui nominalmente o OP iframe do hardening de framing
  e exige regressão de ausência dos headers bloqueadores; não aceitar apenas o verbo “reconciliar”.
- [ ] Registrar este plano no roadmap/backlog vigente se ainda não estiver relacionado no início da fase.
- [ ] Executar busca por `SessionState` no authorization code, opções removidas, cache/script IS4 e URLs HTTP.
- [ ] Criar `scripts/Test-ThirdPartyNotices.ps1` para validar licença Apache, notice, paths do inventário,
  ausência de candidatos pendentes e referências de licença dos arquivos classificados como derivados.
- [ ] Executar build e suíte integral.

**Critérios de aceite:** raiz mantém AGPLv3 e inclui cópia Apache-2.0 + notices; inventário cobre integralmente os
roots delimitados e não contém classificação pendente; README explica a combinação sem relicenciar copyright
alheio; nenhum arquivo derivado conhecido aponta incorretamente para a licença AGPL como se fosse Apache; script
de proveniência, documentação, planos relacionados e todos os testes obrigatórios passam.

**Testes:**

```powershell
Test-Path ../LICENSES/Apache-2.0.txt
Test-Path ../THIRD-PARTY-NOTICES.md
Select-String -Path ../README.md -Pattern "AGPL|Apache|THIRD-PARTY"
./scripts/Test-ThirdPartyNotices.ps1
rg "CheckSessionCookieDomain|CheckSessionCookieSameSiteMode|LastCheckSessionCookieName|AuthorizationCode.*SessionState|SessionState = code\.SessionState|payload\.SessionState" RoyalIdentity RoyalIdentity.Storage.EntityFramework Tests.Storage
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
./scripts/Test-CheckSessionBrowser.ps1
```

### Resultado da Fase 7

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(es) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| OP iframe funcional | 1, 4-6 | DF1-DF2, DF10-DF17, DF22 | `unchanged/changed/error`, origem e HTTPS | SessionStateFormat + CheckSessionEndpoint + browser |
| Estado opaco e realm-scoped | 1-2, 6 | DF3-DF7, DF23 | sem PII/`sid`; resolução por request e dois realms | CheckSessionCookieLifecycle + browser multi-realm |
| `session_state` nas responses | 1, 3 | DF8-DF10, DF24 | factory delimitada; caminho OIDC genérico sem branches legados; alvo final code; erros enumerados | AuthorizeSessionStateTests |
| `prompt=none` interoperável | 3, 6 | DF1, DF8-DF9, DF24-DF25 | condições alcançáveis redirecionadas/testadas; não aplicabilidade explícita; nenhuma UI | AuthorizeSessionState + browser flow |
| Rota/discovery coerentes | 4 | DF2, DF15, DF22 | HTTPS vivo ou metadata ausente; disabled 404 | CheckSessionEndpointTests |
| Hardening sem bloquear iframe | 5-6 | DF11-DF16 | CSP/headers/origem; framing preservado | CheckSessionEndpoint + Playwright |
| AGPL + Apache corretos | 5, 7 | DF18, DF26 | inventário delimitado, licença, notices e arquivos derivados coerentes | Test-ThirdPartyNotices + suíte integral |
| Sequenciamento entre planos | 1, 3, 7 | DF21 | writer/helpers consumidos; sem reintrodução | architecture + revisão documental |

---

## Invariantes a preservar

1. Toda resolução de estado, opção, cookie e endpoint é isolada pelo realm atual.
2. `UserSession.Id`/`sid`, `sub`, claims, `SecurityStamp` e handles de storage nunca entram no cookie legível pelo
   iframe.
3. O core não depende de `UserAccounts`, providers, hosts, UI ou Playwright.
4. `IUserSessionStore` permanece puro e não lê/escreve `HttpContext` ou cookies.
5. Authorization codes continuam single-use e consumidos atomicamente com vínculo a client/redirect.
6. `session_state` pertence somente a OpenID Connect Authentication Responses e nunca contém espaço.
7. `prompt=none` nunca exibe login, consentimento ou outra UI.
8. `none` combinado com outro prompt falha antes de qualquer decorator produzir interação; os quatro erros OIDC
   silenciosos seguem exclusivamente a matriz DF25.
9. Erros alcançáveis da matriz DF25 usam redirect somente após validação do redirect URI e preservam `state`;
   redirect URI inválido nunca recebe redirect. Os demais terminadores do authorize não mudam de transporte.
10. Discovery não anuncia endpoint desabilitado, HTTP ou inalcançável.
11. O OP iframe nunca usa `postMessage(..., "*")` nem retorna `unchanged` para origem/client divergente.
12. Hardening geral de clickjacking não bloqueia exclusivamente o endpoint que precisa ser iframe.
13. Delegate cacheado de cookie options não captura serviço scoped, realm ou opção efetiva.
14. A suíte default permanece autocontida; browser e PostgreSQL continuam opt-in explícitos.
15. O `LICENSE` AGPLv3 da raiz não é substituído e atribuições Apache pertinentes não são removidas.

---

## Critérios globais de conclusão

- Check Session funciona de ponta a ponta em Chromium com RP e OP em origins HTTPS diferentes.
- `plan-oauth21-token-error-responses.md` está concluído e seus helpers/writer são consumidos sem fork local.
- Login, logout, troca de usuário, invalidação do ticket e dois realms atualizam o estado conforme DF3-DF7.
- `session_state` aparece no caminho genérico de sucesso OIDC e nos erros enumerados em DF24, sem ser persistido
  no authorization code e sem criar cases exclusivos para implicit/hybrid que o plano RFC 9700 removerá.
- Cada linha alcançável da matriz de `prompt=none` possui teste exato de erro/redirect após redirect URI válido;
  condições não aplicáveis estão justificadas, sem teste de ausência, e nenhuma produz UI.
- Endpoint, discovery, feature gate e forwarded scheme são coerentes e nunca anunciam HTTP.
- Iframe usa Web Crypto, valida parent/origem, tem headers estritos e permanece frameable.
- Cookies/payloads/options legados não possuem shim; serializers, seeds, matriz e testes usam as novas versões.
- Licenças e notices na raiz satisfazem DF18 para todo material derivado conhecido.
- Inventário de proveniência não possui candidato pendente e `Test-ThirdPartyNotices.ps1` passa.
- Nenhum comando obrigatório com filtro seleciona zero testes; `Tests.Host` é usado como host/build, nunca como
  test project.
- `dotnet build RoyalIdentity.sln` passa.
- `dotnet test RoyalIdentity.sln` passa sem browser instalado.
- `./scripts/Test-CheckSessionBrowser.ps1` passa.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| Bloqueio de third-party cookie | iframe não vê cookie presente no first-party OP | falsos `changed`/loop no RP | DF14, harness defensivo, documentação e Back-Channel Logout | Aberto |
| Framing bloqueado pelo RFC 9700 | header global injeta DENY/`frame-ancestors 'none'` | endpoint publicado não carrega | exceção exata por endpoint + teste browser/header | Aberto |
| Esquema externo incorreto atrás de proxy | discovery vê HTTP | metadata não aderente ou ausente | forwarded headers antes do protocolo + teste de host | Aberto |
| Cookie de realm colide | nomes/path não incluem realm corretamente | vazamento ou `changed` entre tenants | derivação única/validação entregues na Fase 1; lifecycle multi-realm na Fase 2 | Mitigado |
| Estado vaza em token/log | principal/telemetria copia propriedade/cookie | correlação ou exposição | propriedade protegida, não claim; filtros e testes de log/token | Aberto |
| Hash diverge entre C# e JS | canonicalização/Unicode/porta diferentes | sempre `changed` | punycode/IPv6 e vetor C# entregues; comparação JS/browser nas Fases 5-6 | Aberto |
| Payload v1 permanece em banco dev | serializer sobe para v2 com artefato antigo | falha fechada temporária | breaking aceito; codes efêmeros; reprovisionar seeds/config | Aceito |
| Playwright entra na suíte default | projeto baixa/exige Chromium | CI/local deixa de ser autocontido | projeto/script opt-in + arquitetura test | Aberto |
| Auditoria de licença incompleta | arquivo derivado perdeu header/notice | não conformidade de redistribuição | inventário de similaridade/histórico + notice central + revisão manual | Aberto |
| `session_state` grande | origem longa + envelope v1 | callback/URL cresce | limites/testes e formato Base64Url compacto | Aberto |
| Planos concorrentes recriam helpers | Session Management inicia antes do plano OAuth 2.1 | conflito em `ConsentDecorator`/respostas | gate DF21 satisfeito antes da Fase 1 | Fechado |
| Callback captura scoped/realm | manager ou nome do cookie é fechado no delegate de named options | captive dependency ou realm congelado | DF23 + lifecycle multi-realm | Aberto |
| Erro enumerado perde `session_state` | cálculo fica somente no `AuthorizeHandler` | Authentication Error Response incompleta | factory delimitada DF24 + testes por caller | Aberto |
| Prompt silencioso cai em UI/código genérico | classificação continua espalhada | loop/interoperabilidade incorreta | matriz DF25 + rede sobre produtores posteriores + testes HTTP/customização terminal | Mitigado |
| Filtro executa zero testes | classe planejada não é criada ou nome diverge | fase fecha em falso verde | DF22 + nomes exatos | Aberto |
| Factory amplia silenciosamente o transporte do authorize | terminadores fora de DF24 passam a usar redirect | mudança de contrato fora do escopo | callers enumerados + regressão dos terminadores excluídos | Aberto |

---

## Diferidos e backlog

- Mitigações específicas com Storage Access API/CHIPS para browsers que bloqueiam third-party state — destino:
  plano futuro orientado por suporte real de navegadores.
- Administração de sessões por dispositivo — destino: item 4 de `plans-roadmap-02.md`; não confundir com OP User
  Agent State.
- Front-Channel Logout e Back-Channel Logout adicionais — destino: planos próprios se os comportamentos atuais
  precisarem evoluir.
- Teste cross-browser Firefox/WebKit — destino: expansão futura do harness após estabilizar Chromium.
- Auditoria jurídica externa da distribuição combinada — destino: processo de release/compliance, fora do código.
- Auditoria de dependências de terceiros fora dos roots IS4/IdentityModel definidos em DF26 — destino: processo
  geral de SBOM/compliance; não ampliar silenciosamente este gate.
- Taxonomia e transporte completos dos demais erros do authorization endpoint (`RedirectUriValidator`, recursos,
  PKCE e outros terminadores não enumerados em DF24) — destino: plano próprio já apontado pelo predecessor OAuth
  2.1; este plano entrega somente os redirects silenciosos necessários a Session Management.

---

## Referências

- [OpenID Connect Session Management 1.0](https://openid.net/specs/openid-connect-session-1_0.html).
- [OpenID Connect Core 1.0 incorporating errata set 2](https://openid.net/specs/openid-connect-core-1_0.html).
- [OpenID Connect Discovery 1.0 incorporating errata set 2](https://openid.net/specs/openid-connect-discovery-1_0.html).
- [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0.html).
- [GNU License Compatibility and Relicensing](https://www.gnu.org/licenses/license-compatibility.en.html).
- `C:/git/RoyalCode/RoyalIdentity/old-is4/src/IdentityServer4/src/Services/Default/DefaultUserSession.cs`.
- `C:/git/RoyalCode/RoyalIdentity/old-is4/src/IdentityServer4/src/Endpoints/Results/CheckSessionResult.cs`.
- `C:/git/RoyalCode/RoyalIdentity/old-is4/src/IdentityServer4/src/Extensions/ValidatedAuthorizeRequestExtensions.cs`.
- `C:/git/RoyalCode/RoyalIdentity/old-is4/src/IdentityServer4/src/ResponseHandling/Default/AuthorizeResponseGenerator.cs`.
- `../../adrs/ADR-001.md`, `../../adrs/ADR-009.md`, `../../adrs/ADR-014.md`, `../../adrs/ADR-017.md`.
- `plan-oauth21-token-error-responses.md`, `plan-realm-options-redesign.md`, `plan-rfc9700-security-hardening.md`,
  `plan-data-storage-matrix.md`, `plan-data-operational-storage.md`.
- `../references/template-plan/template-ai-implementation-plan.md`.
