# Plan: OpenID Connect Session Management, Check Session e atribuições Apache (`plan-oidc-session-management`)

## Status: RASCUNHO - desenho fechado; 0 de 7 fases executadas

## Progresso

`░░░░░░░` **0%** - 0 de 7 fases

| Fase | Estado |
|---|---|
| Fase 1 - Contrato de estado do User Agent e opções | Pendente |
| Fase 2 - Ciclo de vida do estado no login, cookie e logout | Pendente |
| Fase 3 - Authentication Responses, `prompt=none` e payload operacional | Pendente |
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

### Estado atual do código (verificado em 2026-07-30)

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
- **DF9 — Somente OIDC:** requisição OAuth sem identity scope `openid`, feature desabilitada ou redirect ainda
  não validado não recebe `session_state`. Fonte: OpenID Connect Core + desenho IS4.
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

---

## Design alvo

### Contratos e bordas

- `CheckSessionStateManager` (nome final pode seguir a convenção local, sem interface pública): serviço scoped
  do core que cria estado, grava/limpa cookie, sincroniza `AuthenticationProperties`, publica o valor canônico
  em `HttpContext.Items` e deriva o nome/path do realm.
- `ISessionStateGenerator.GenerateSessionStateValue(AuthorizeContext) -> string?`: mantém o seam existente,
  passa a retornar `null` quando DF2/DF9 não forem satisfeitas e usa o estado canônico da request.
- `AuthorizeResponse`: recebe o `session_state` calculado pelo handler independentemente do authorization code.
- `AuthorizeErrorResponse`: recebe `session_state?` quando a requisição já possui client, redirect e OIDC
  validados; serializa junto com `error`, `error_description` e `state`.
- `PromptLoginDecorator`: especializa o contexto de autorização necessário para emitir `login_required` em
  `prompt=none`; não mostra UI nesse modo.
- `ConsentDecorator`: emite `consent_required` em `prompt=none`; `access_denied` continua sendo a decisão
  explícita do usuário.
- `CheckSessionEndpoint`: continua GET-only, aplica realm, feature gate e HTTPS antes de produzir a resposta.
- `CheckSessionResult`: HTML por request, nonce CSP por request, JavaScript Web Crypto e dados dinâmicos
  serializados.

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

  Handlers/AuthorizeHandler.cs
    gera estado no boundary da resposta

  Contexts/Decorators/
    PromptLoginDecorator.cs
    ConsentDecorator.cs
      prompt=none sem UI + session_state no erro

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

O projeto de navegador não entra como dependência de runtime. Se for adicionado à solution, seus testes devem
permanecer ignorados sem opt-in explícito e nunca baixar Chromium durante `dotnet test RoyalIdentity.sln`.

### Segurança, concorrência e confiabilidade

- Gerar estado e salt somente com `RoyalIdentity.Security.Cryptography.CryptoRandom`.
- Comparar client, origem e hash sem normalização relaxada; usar igualdade ordinal.
- Usar formato canônico com campos length-prefixed coberto por vetores compartilhados C#/JS.
- Não logar OP User Agent State, cookie, `session_state` completo ou salt associado a client/usuário.
- O cookie recebido nunca substitui o valor protegido do ticket; divergência causa sobrescrita.
- Rejeição/invalidação da sessão limpa o cookie antes da resposta terminar.
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
- Reconciliar o hardening global de framing do plano RFC 9700 com a exceção do iframe antes de fechar este plano.
- A distribuição mantém `LICENSE` AGPLv3 e adiciona Apache-2.0 como licença de terceiro; não substituir o
  copyright original dos autores do IS4/IdentityModel.

---

## Ordem de execução

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

**Depende de:** DF1-DF5, DF8-DF10, DF18, DF20.

**Escopo:** `RoyalIdentity/Authentication`, `RoyalIdentity/Contracts`, `RoyalIdentity/Options`,
serializers Configuration, `Tests.Identity`, `Tests.Storage`.

**O que/como:** introduzir o manager concreto/scoped, constantes internas e formato v1; simplificar
`AuthenticationOptions`; versionar payloads de server/realm e fixar vetores C#/JavaScript antes da integração.

**Tarefas:**

- [ ] Criar `CheckSessionStateManager` no core sem interface pública, storage ou dependência de módulo.
- [ ] Definir chaves internas de `AuthenticationProperties` e `HttpContext.Items` em `Constants.Server`.
- [ ] Implementar geração criptográfica de OP User Agent State com pelo menos 256 bits.
- [ ] Implementar derivação única do nome/path do cookie a partir de `AuthenticationOptions` e realm.
- [ ] Remover `CheckSessionCookieDomain` e `CheckSessionCookieSameSiteMode`.
- [ ] Validar nome-base vazio, caracteres de controle, separadores de cookie e colisões previsíveis.
- [ ] Implementar parser/formatter do `session_state` v1 sem espaço e com origem Base64Url.
- [ ] Implementar codificação canônica compartilhável com JavaScript e vetores determinísticos.
- [ ] Alterar `ISessionStateGenerator`/default para retorno anulável e gates DF2/DF9.
- [ ] Incrementar `ServerOptionsPayloadSerializer` e `RealmOptionsPayloadSerializer` para versão 2.
- [ ] Atualizar copy constructors, materialização, seeds e testes de property coverage das options.

**Critérios de aceite:** estado contém entropia suficiente e nenhum dado de usuário/sessão persistida; cookie
derivado é host-only/realm-scoped; options removidas não aparecem no JSON v2; parser rejeita versões, segmentos,
Base64Url e espaços inválidos; um mesmo vetor produz exatamente o mesmo hash esperado.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~SessionState|FullyQualifiedName~CheckSession"
dotnet test Tests.Storage --filter "FullyQualifiedName~ConfigurationModelPayload|FullyQualifiedName~ConfigurationPayload"
```

### Resultado da Fase 1

*a preencher*

---

## Fase 2 - Ciclo de vida do estado no login, cookie e logout

**Depende de:** Fase 1, DF3-DF7, DF14, DF19.

**Escopo:** `LoginFlowService`, `ConfigureRealmCookieAuthenticationOptions`, `DefaultSignOutManager`,
registro DI, testes de sessão/cookie.

**O que/como:** integrar o manager aos três pontos já donos do ciclo HTTP sem mover lógica para
`IUserSessionStore`, UserAccounts ou decorator global de autenticação.

**Tarefas:**

- [ ] Criar OP User Agent State ao preparar `AuthenticationProperties` no login bem-sucedido.
- [ ] Gravar o check-session cookie com as flags fixas de DF5 no mesmo response do sign-in.
- [ ] Preservar o estado em sliding/renovação do mesmo ticket.
- [ ] Gerar estado e marcar `ShouldRenew=true` quando um ticket autenticado válido ainda não possuir a propriedade.
- [ ] Publicar o valor canônico do ticket em `HttpContext.Items` durante `OnValidatePrincipal`.
- [ ] Sobrescrever cookie ausente/divergente a partir do ticket protegido.
- [ ] Limpar o cookie quando `OnValidatePrincipal` rejeitar sessão expirada, encerrada ou invalidada por estado.
- [ ] Limpar o cookie no `DefaultSignOutManager` usando exatamente o nome/path de emissão.
- [ ] Não gravar check-session cookie quando o endpoint estiver desabilitado para o realm; remover valor residual.
- [ ] Cobrir login repetido do mesmo usuário, troca de usuário, logout e dois realms.

**Critérios de aceite:** login válido produz ticket + cookie com o mesmo estado opaco; sliding não produz
`changed` espúrio; troca de usuário muda o estado; logout/rejeição removem o cookie; cookie de realm A não é
enviado nem aceito em realm B; nenhum valor aparece em logs.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~CheckSession|FullyQualifiedName~Cookie|FullyQualifiedName~LoginFlow|FullyQualifiedName~SignOut"
dotnet test Tests.Integration --filter "FullyQualifiedName~SessionLifecycle|FullyQualifiedName~UserSession|FullyQualifiedName~Realm"
```

### Resultado da Fase 2

*a preencher*

---

## Fase 3 - Authentication Responses, `prompt=none` e payload operacional

**Depende de:** Fases 1-2, DF1, DF8-DF10, DF20.

**Escopo:** `AuthorizeHandler`, `DefaultCodeFactory`, `AuthorizationCode`, `AuthorizeResponse`,
`AuthorizeErrorResponse`, `PromptLoginDecorator`, `ConsentDecorator`, payload/matriz Operational,
`Tests.Identity`, `Tests.Integration`, `Tests.Storage`.

**O que/como:** calcular `session_state` no boundary da resposta OIDC; implementar erros silenciosos; remover o
estado do authorization code e versionar seu payload sem alterar consumo atômico.

**Tarefas:**

- [ ] Remover `ISessionStateGenerator` de `DefaultCodeFactory`.
- [ ] Remover `SessionState` dos construtores/propriedades de `AuthorizationCode`.
- [ ] Remover `SessionState` de `AuthorizationCodePayload` e incrementar o serializer para versão 2.
- [ ] Atualizar `.ai/plans/plan-data-storage-matrix.md` para declarar que `session_state` não pertence ao code.
- [ ] Atualizar contratos SQLite/PostgreSQL, payload tests, parity e fixtures sem alterar binding/single-use.
- [ ] Gerar `session_state` no `AuthorizeHandler` para toda Authentication Response OIDC bem-sucedida suportada.
- [ ] Garantir que OAuth authorization sem `openid` não receba o parâmetro.
- [ ] Estender `AuthorizeErrorResponse` com `session_state?`.
- [ ] Retornar `login_required` sem página quando `prompt=none` exigir login, reautenticação, troca de IdP ou
  interação equivalente.
- [ ] Retornar `consent_required` sem página quando `prompt=none` exigir consentimento.
- [ ] Preservar `state`, response mode e redirect URI validado nos erros.
- [ ] Incluir `session_state` nos erros OIDC quando client/origem/redirect já forem confiáveis.
- [ ] Cobrir usuário anônimo, sessão ativa, sessão trocada e novo `prompt=none` após `changed`.

**Critérios de aceite:** authorization code não contém/persiste `session_state`; toda resposta OIDC de sucesso
suportada contém valor sem espaços; resposta OAuth pura não contém; `prompt=none` nunca renderiza login/consent;
erros corretos chegam ao redirect com `state` e, quando possível, `session_state`; consumo atômico do code
permanece verde.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~Authorize|FullyQualifiedName~Prompt|FullyQualifiedName~SessionState"
dotnet test Tests.Storage --filter "FullyQualifiedName~AuthorizationCode|FullyQualifiedName~OperationalPayload"
dotnet test Tests.Integration --filter "FullyQualifiedName~CodeAuthorize|FullyQualifiedName~PromptInteraction|FullyQualifiedName~LoginConsent"
```

### Resultado da Fase 3

*a preencher*

---

## Fase 4 - Rota, discovery HTTPS e isolamento por realm

**Depende de:** Fases 1-3, DF2, DF9, DF15, ADR-009.

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

**Critérios de aceite:** discovery HTTPS aponta para GET 200 no mesmo realm; desabilitado produz ausência de
metadata + 404; HTTP não produz página nem metadata; realm A não usa opções/HTML/cookie de B; POST produz 405
somente quando a rota está efetivamente disponível.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~Discovery|FullyQualifiedName~CheckSession"
dotnet test Tests.Integration --filter "FullyQualifiedName~CheckSession|FullyQualifiedName~Discovery|FullyQualifiedName~Realm"
dotnet test Tests.Host
```

### Resultado da Fase 4

*a preencher*

---

## Fase 5 - OP iframe moderno e hardening HTTP

**Depende de:** Fases 1 e 4, DF10-DF16, DF18.

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
- [ ] Garantir que middleware/header global não injete `X-Frame-Options: DENY` nesse endpoint.
- [ ] Adicionar aviso de derivação/modificação conforme DF18 ou documentar reescrita independente verificável.
- [ ] Criar testes de vetores que comparem o cálculo de C# com o algoritmo exposto ao JavaScript.

**Critérios de aceite:** o resultado não contém implementação SHA legada, interpolação crua ou estado estático;
o CSP permite somente o script com nonce; endpoint continua frameable; origem diferente nunca recebe
`unchanged`; mensagens inválidas retornam `error`; headers de cache/referrer/content estão presentes.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~CheckSessionResult|FullyQualifiedName~SessionStateVector"
dotnet test Tests.Integration --filter "FullyQualifiedName~CheckSession"
```

### Resultado da Fase 5

*a preencher*

---

## Fase 6 - Aceites HTTP, multi-realm e navegador real

**Depende de:** Fases 2-5, DF11-DF17, ADR-009.

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
- [ ] Adicionar guardas de arquitetura contra referência de Playwright nos projetos de runtime.

**Critérios de aceite:** script opt-in passa todos os cenários em Chromium; suite default passa sem browser
instalado; nenhum teste usa target origin `*`; dois realms não compartilham estado; `changed` conduz ao fluxo
silencioso correto.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~CheckSession|FullyQualifiedName~PromptNone"
dotnet test Tests.Architecture
./scripts/Test-CheckSessionBrowser.ps1
```

### Resultado da Fase 6

*a preencher*

---

## Fase 7 - Licenças, atribuições, documentação e fechamento

**Depende de:** Fases 1-6, DF18, DF20.

**Escopo:** raiz Git `C:/git/RoyalCode/RoyalIdentity`, arquivos derivados, README, fundações, planos relacionados,
suíte integral.

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
- [ ] Inventariar arquivos diretamente copiados/adaptados do IS4/IdentityModel por basename, histórico e comparação
  de conteúdo; registrar proveniência no notice ou no próprio arquivo.
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
- [ ] Atualizar `plan-rfc9700-security-hardening.md` para registrar a exceção de framing exclusiva do OP iframe.
- [ ] Registrar este plano no roadmap/backlog vigente se ainda não estiver relacionado no início da fase.
- [ ] Executar busca por `SessionState` no authorization code, opções removidas, cache/script IS4 e URLs HTTP.
- [ ] Executar build e suíte integral.

**Critérios de aceite:** raiz mantém AGPLv3 e inclui cópia Apache-2.0 + notices; README explica a combinação sem
relicenciar copyright alheio; nenhum arquivo derivado conhecido aponta incorretamente para a licença AGPL como
se fosse Apache; documentação descreve comportamento/limites reais; planos relacionados não contradizem a
exceção de framing; todos os testes obrigatórios passam.

**Testes:**

```powershell
Test-Path ../LICENSES/Apache-2.0.txt
Test-Path ../THIRD-PARTY-NOTICES.md
Select-String -Path ../README.md -Pattern "AGPL|Apache|THIRD-PARTY"
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
| OP iframe funcional | 1, 4-6 | DF1-DF2, DF10-DF17 | `unchanged/changed/error`, origem e HTTPS | `Tests.Identity`, `Tests.Integration`, browser script |
| Estado opaco e realm-scoped | 1-2, 6 | DF3-DF7 | sem PII/`sid`; lifecycle e dois realms | Session/cookie tests + browser multi-realm |
| `session_state` nas responses | 1, 3 | DF8-DF10 | sucesso OIDC e erros possíveis; OAuth puro ausente | Authorize/Prompt/SessionState tests |
| `prompt=none` interoperável | 3, 6 | DF1, DF8-DF9 | nenhuma UI; erros OIDC corretos | PromptNone + browser flow |
| Rota/discovery coerentes | 4 | DF2, DF15 | HTTPS vivo ou metadata ausente; disabled 404 | Discovery/CheckSession HTTP tests |
| Hardening sem bloquear iframe | 5-6 | DF11-DF16 | CSP/headers/origem; framing preservado | Result tests + Playwright |
| AGPL + Apache corretos | 5, 7 | DF18 | licença, notices e arquivos derivados coerentes | comandos documentais + suíte integral |

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
8. Discovery não anuncia endpoint desabilitado, HTTP ou inalcançável.
9. O OP iframe nunca usa `postMessage(..., "*")` nem retorna `unchanged` para origem/client divergente.
10. Hardening geral de clickjacking não bloqueia exclusivamente o endpoint que precisa ser iframe.
11. A suíte default permanece autocontida; browser e PostgreSQL continuam opt-in explícitos.
12. O `LICENSE` AGPLv3 da raiz não é substituído e atribuições Apache pertinentes não são removidas.

---

## Critérios globais de conclusão

- Check Session funciona de ponta a ponta em Chromium com RP e OP em origins HTTPS diferentes.
- Login, logout, troca de usuário, invalidação do ticket e dois realms atualizam o estado conforme DF3-DF7.
- `session_state` aparece em sucesso/erro OIDC aplicável e não é persistido no authorization code.
- `prompt=none` retorna os erros OIDC corretos sem UI.
- Endpoint, discovery, feature gate e forwarded scheme são coerentes e nunca anunciam HTTP.
- Iframe usa Web Crypto, valida parent/origem, tem headers estritos e permanece frameable.
- Cookies/payloads/options legados não possuem shim; serializers, seeds, matriz e testes usam as novas versões.
- Licenças e notices na raiz satisfazem DF18 para todo material derivado conhecido.
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
| Cookie de realm colide | nomes/path não incluem realm corretamente | vazamento ou `changed` entre tenants | derivação única, host-only, path e testes ADR-009 | Aberto |
| Estado vaza em token/log | principal/telemetria copia propriedade/cookie | correlação ou exposição | propriedade protegida, não claim; filtros e testes de log/token | Aberto |
| Hash diverge entre C# e JS | canonicalização/Unicode/porta diferentes | sempre `changed` | vetores compartilhados e browser real | Aberto |
| Payload v1 permanece em banco dev | serializer sobe para v2 com artefato antigo | falha fechada temporária | breaking aceito; codes efêmeros; reprovisionar seeds/config | Aceito |
| Playwright entra na suíte default | projeto baixa/exige Chromium | CI/local deixa de ser autocontido | projeto/script opt-in + arquitetura test | Aberto |
| Auditoria de licença incompleta | arquivo derivado perdeu header/notice | não conformidade de redistribuição | inventário de similaridade/histórico + notice central + revisão manual | Aberto |
| `session_state` grande | origem longa + envelope v1 | callback/URL cresce | limites/testes e formato Base64Url compacto | Aberto |

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
- `plan-realm-options-redesign.md`, `plan-rfc9700-security-hardening.md`,
  `plan-data-storage-matrix.md`, `plan-data-operational-storage.md`.
- `../references/template-plan/template-ai-implementation-plan.md`.
