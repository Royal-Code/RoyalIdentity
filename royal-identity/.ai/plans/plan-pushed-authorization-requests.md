# Plan: Pushed Authorization Requests RFC 9126 (`plan-pushed-authorization-requests`)

## Status: RASCUNHO - Q1/Q2/Q3 pendentes antes da Fase 1; 0 de 7 fases executadas

## Progresso

`░░░░░░░` **0%** - 0 de 7 fases

| Fase | Estado |
|---|---|
| Fase 1 - Decisões, contratos e configuração | Bloqueada por Q1/Q2/Q3 |
| Fase 2 - Persistência Operational e consumo atômico | Bloqueada pela Fase 1 |
| Fase 3 - Endpoint PAR e validação antecipada | Bloqueada pela Fase 2 |
| Fase 4 - Resolução no authorization endpoint | Bloqueada pela Fase 3 |
| Fase 5 - Política, discovery e separação de JAR | Bloqueada pela Fase 4 |
| Fase 6 - Aceites multi-realm e paridade dos providers | Bloqueada pela Fase 5 |
| Fase 7 - Documentação e fechamento do backlog | Bloqueada pela Fase 6 |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluída, `%` e `X de 7`). Antes de fechar uma fase, confirme que decisões,
> critérios de aceite, testes e invariantes relacionados foram aplicados.

---

## Contexto

### Fontes verificadas

- [an-par-rfc-9126.md](../analisys/an-par-rfc-9126.md) — inventário exploratório dos requisitos de PAR e das
  alternativas de armazenamento; não constitui decisão arquitetural.
- [plan-data-storage-matrix.md](plan-data-storage-matrix.md) — contratos Operational vigentes, incluindo
  AP-01..AP-03 para a continuação interna de authorization requests.
- [plan-data-operational-storage.md](plan-data-operational-storage.md) — persistência Operational,
  proteção de payload, TTL, cleanup e migrations SQLite/PostgreSQL entregues.
- [plan-replay-protection.md](plan-replay-protection.md) — `IReplayProtectionStore` atual é específico para a
  decisão atômica de replay de `private_key_jwt`; não armazena payload de authorization request.
- [plan-oauth21-token-error-responses.md](plan-oauth21-token-error-responses.md) — baseline de erros JSON e
  autenticação do token endpoint que este plano deve reutilizar.
- [plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md) — baseline de redirect URI, PKCE,
  remoção de front-channel token e política de segurança executada antes de PAR.
- [plan-reference-tokens-introspection.md](plan-reference-tokens-introspection.md) — predecessor imediato na
  ordem do roadmap; também altera `Client`, Configuration, endpoints e discovery.
- `RoyalIdentity/Contracts/Storage/IAuthorizeParametersStore.cs` e
  `RoyalIdentity.Storage.EntityFramework/Operational/Stores/EntityFrameworkAuthorizeParametersStore.cs` —
  continuação interna realm-bound, com TTL absoluto, leitura repetível e delete separado.
- `RoyalIdentity/Contracts/IMessageStore.cs` e
  `RoyalIdentity/Contracts/Defaults/ProtectedDataMessageStore.cs` — identificador self-contained protegido,
  sem registro server-side, binding de client, TTL persistido ou consumo atômico.
- `RoyalIdentity/Contexts/Decorators/ProcessRequestObject.cs` — processamento de Request Object/JAR ainda é um
  stub que apenas continua a pipeline.
- `RoyalIdentity/Handlers/DiscoveryHandler.cs` — anuncia `request_parameter_supported=true` quando authorize
  está ativo, apesar de o processamento de Request Object não estar implementado.
- `RoyalIdentity/Contracts/Defaults/DefaultClientSecretChecker.cs` e
  `RoyalIdentity/Contexts/Decorators/EvaluateClient.cs` — autenticação de client compartilhada pelos endpoints
  diretos, incluindo `client_secret_basic`, `client_secret_post`, `private_key_jwt` e client público conforme
  os evaluators registrados.
- `RoyalIdentity/Contracts/Defaults/DefaultAuthorizeRequestValidator.cs` e
  `RoyalIdentity/Contexts/AuthorizeValidateContext.cs` — validação reutilizável da authorization request sem
  iniciar interação do usuário.
- [RFC 9126](https://www.rfc-editor.org/rfc/rfc9126.html) — endpoint, request/response, uso único, binding,
  metadata, políticas e segurança de PAR.
- [RFC 9101](https://www.rfc-editor.org/rfc/rfc9101.html) — semântica e erros de `request`/`request_uri`.
- [RFC 8414](https://www.rfc-editor.org/rfc/rfc8414.html) — metadata do authorization server.

### Estado atual do código (verificado em 2026-07-30)

- **PAR inexistente:** não há endpoint, context, pipeline, response, facade de storage, entidade ou tabela para
  Pushed Authorization Requests.
- **Constantes parciais:** `Constants.Oidc` já contém nomes de `request_uri`,
  `pushed_authorization_request_endpoint` e `require_pushed_authorization_requests`, mas nenhuma feature os
  implementa.
- **Continuação interna não é PAR:** `IAuthorizeParametersStore` é lido mais de uma vez durante
  login/consentimento e removido apenas no callback; mudar sua leitura para consumo destruiria o fluxo vigente.
- **`IMessageStore` não é backing:** o identificador contém o payload protegido e `DeleteAsync` é no-op; não há
  decisão server-side que possa expirar ou consumir atomicamente a referência.
- **Infraestrutura Operational disponível:** há digest realm/type-bound, proteção de payload versionada,
  migrations por provider, cleanup, clock injetável e contract tests reutilizáveis.
- **Replay protection não substitui PAR:** `IReplayProtectionStore.TryAddAsync` guarda somente a decisão de
  unicidade de um handle; não materializa parâmetros nem transfere seu modelo de retenção para PAR.
- **Validação antecipada reutilizável:** `IAuthorizeRequestValidator` despacha `AuthorizeValidateContext`, mas
  precisará receber somente parâmetros de autorização, sem credenciais de client do POST PAR.
- **Carga antecipada dos parâmetros:** `AuthorizeEndpoint` e `DefaultAuthorizeRequestValidator` chamam
  `AuthorizeContext.Load` antes da pipeline; um resolver de PAR precisa ocorrer antes dessa carga ou o context
  fica materializado com apenas `client_id`/`request_uri`.
- **JAR não implementado:** `ProcessRequestObject` não processa `request` nem `request_uri` remoto; a metadata
  atual afirma capacidade inexistente.
- **Configuration relacional:** novo scalar público em `Client` exige coluna, materializer, migrations
  SQLite/PostgreSQL, seeds e teste de cobertura; options de realm usam payload JSON versionado.
- **Sem compatibilidade obrigatória:** não existem consumidores de produção; contratos, migrations e defaults
  podem adotar diretamente o desenho correto.

### Lacunas, conflitos e restrições

- **Semânticas opostas de leitura:** continuação interna exige leitura repetível; PAR recomenda uso único e
  decisão atômica sob concorrência.
- **Client no front channel não é autenticado:** o `client_id` da chamada ao authorization endpoint deve
  coincidir com o client autenticado no POST PAR e com o client armazenado.
- **Validação em dois tempos:** validar no POST reduz requests inválidas, mas policy/client podem mudar antes do
  uso; o authorization endpoint precisa revalidar o estado vigente.
- **Erro antes de redirect confiável:** request URI ausente, inválido, expirado, consumido ou de outro client não
  fornece um `redirect_uri` confiável; o servidor não pode redirecionar esse erro.
- **JAR e PAR compartilham nomes:** `request_uri` de PAR não depende de
  `request_uri_parameter_supported`/`EnableJwtRequestUri`; tratar ambos como a mesma feature produz metadata e
  resolução incorretas.
- **Ordem de execução:** implementar depois de Reference Tokens/Introspection evita migrations e edições
  concorrentes em `Client`, endpoints, autenticação direta e discovery.

### Superfícies impactadas a mapear

- `RoyalIdentity/Models/Client.cs`, `Options/RealmOptions.cs` e `Options/EndpointsOptions.cs` — políticas e
  feature gate.
- `RoyalIdentity/Contexts`, `Endpoints`, `Handlers`, `Responses` e `Pipes.cs` — endpoint PAR e resolução no
  authorization endpoint.
- `RoyalIdentity/Contracts/Storage` e `IStorage` — facade realm-bound escolhida em Q1.
- `RoyalIdentity.Data.Configuration` e adapter EF — scalar `Client.RequirePushedAuthorizationRequests`.
- `RoyalIdentity.Data.Operational` e adapter EF — artefato transitório, payload, digest e consumo.
- Providers `.Sqlite`/`.PostgreSql` e `RoyalIdentity.Migrations` — migrations, snapshots e SQL revisável.
- `DiscoveryHandler` e `Constants.Oidc` — metadata e separação de JAR.
- `Tests.Identity`, `Tests.Pipelines`, `Tests.Storage`, `Tests.Integration` e `Tests.Architecture` — contratos,
  HTTP, concorrência, providers e boundaries.

---

## Objetivo

1. Implementar o endpoint PAR realm-scoped conforme RFC 9126, com autenticação de client e validação antecipada.
2. Emitir `request_uri` opaco, imprevisível, client-bound, curto e armazenado somente por digest.
3. Consumir a referência no authorization endpoint com decisão atômica e sem misturar seu lifecycle com a
   continuação interna de login/consentimento.
4. Permitir que PAR seja obrigatório globalmente por realm ou individualmente por client, com default `false`.
5. Publicar metadata fiel e distinguir PAR de JAR/request objects não implementados.
6. Preservar isolamento por realm, segurança sob concorrência, payload protegido e paridade
   SQLite/PostgreSQL.
7. Fechar a pendência no backlog e documentar configuração, operação e limitações.

## Fora de escopo

- Implementar JAR/Request Objects por valor ou por URI remoto — destino: plano próprio de JAR.
- Implementar JARM, Dynamic Client Registration, FAPI profiles, DPoP ou CIBA — destinos próprios.
- Relaxar redirect URI cadastrada por causa de client autenticado no PAR; continua valendo a policy entregue
  pelo plano RFC 9700.
- Persistir o catálogo de resources/scopes — destino:
  futuro `plan-data-resource-catalog-storage.md`.
- Criar UI/API administrativa para as novas opções — destino:
  futuro `plan-admin-api-ui.md`.
- Usar `IMessageStore`, cache distribuído ou `IReplayProtectionStore` como backing de PAR.
- Adicionar tolerância de reload fora da resposta escolhida em Q2.

---

## Perguntas ao humano

- **Q1 — Facade pública de storage:** qual contrato deve representar o artefato PAR?
  - **Opções:**
    - **A) Recomendada:** criar `IPushedAuthorizationRequestStore` e
      `IStorage.GetPushedAuthorizationRequestStore(realm)`, mantendo `IAuthorizeParametersStore` exclusivamente
      para continuação interna.
    - **B)** substituir ambos por um `IAuthorizationRequestStore` mais geral, com famílias de operações
      explicitamente separadas para continuação repetível e PAR consumível.
  - **Impacto se não decidir:** bloqueia contrato público, matriz de storage, entidade/adapters e a Fase 1.
  - **Status:** Aberta.

- **Q2 — Reload do user agent:** qual semântica aplicar depois do primeiro uso válido do `request_uri`?
  - **Opções:**
    - **A) Recomendada:** uso estritamente único; exatamente um consumidor recebe o payload e qualquer retry,
      reload ou corrida posterior recebe `invalid_request_uri`.
    - **B)** tolerância curta de reload, somente quando o retry puder ser vinculado ao mesmo fluxo/browser por
      um identificador server-side não controlado pelo client; duração e binding entram nas options e nos
      contratos atômicos.
  - **Impacto se não decidir:** bloqueia modelo de estado, operação atômica, cleanup e testes de concorrência.
  - **Status:** Aberta.

- **Q3 — Lifetime da referência:** qual janela default e limites administrativos usar?
  - **Opções:**
    - **A) Recomendada:** option própria com default de 90 segundos e faixa válida de 5 a 600 segundos, seguindo
      a janela curta típica indicada pelo RFC 9126.
    - **B)** outro default/faixa informados pelo autor; não reutilizar implicitamente
      `AuthorizationInteractionLifetime`, pois ela governa a continuação de UI e possui lifecycle diferente.
  - **Impacto se não decidir:** bloqueia contrato de options, TTL persistido, `expires_in` e testes de limite.
  - **Status:** Aberta.

---

## Decisões fechadas

- **DF1 — Ordem do roadmap:** executar depois de
  [plan-reference-tokens-introspection.md](plan-reference-tokens-introspection.md). Fonte: decisão humana na
  discussão que originou este plano.
- **DF2 — Endpoint direto:** mapear endpoint realm-scoped `/{realm}/connect/par`, somente HTTPS efetivo, método
  POST e `application/x-www-form-urlencoded`; sucesso retorna HTTP 201 JSON, `request_uri`, `expires_in` e
  `Cache-Control: no-store`. Fonte: RFC 9126 §§2, 2.2.
- **DF3 — Autenticação compartilhada:** aplicar as mesmas regras e métodos de autenticação aceitos pelo token
  endpoint, reutilizando `IClientSecretChecker`/evaluators; `private_key_jwt` aceita issuer, token endpoint ou
  PAR endpoint como audience. Fonte: RFC 9126 §2.1.
- **DF4 — Identidade do client:** `client_id` é obrigatório no payload PAR e deve coincidir com o client
  autenticado; public clients continuam sujeitos à identificação e à policy vigente de segredo. Fonte:
  RFC 9126 §§2.1, 3.
- **DF5 — Handle opaco:** usar o prefixo registrado
  `urn:ietf:params:oauth:request_uri:` seguido de 32 bytes aleatórios Base64Url; o valor integral é a referência
  pública e contém 256 bits de entropia. Fonte: RFC 9126 §§2.2, 7.1.
- **DF6 — Binding e confidencialidade:** persistir somente digest realm/type-bound da referência e vincular o
  registro a `Realm.Id` + `Client.Id`; nunca persistir o `request_uri` bruto em coluna, payload, log ou evento.
  Fonte: RFC 9126 §§2.2, 7 + baseline Operational.
- **DF7 — Validação em dois tempos:** validar a request completa no POST PAR e revalidar no authorization
  endpoint contra client, redirect URI, scopes/resources, PKCE e policy vigentes. Fonte: RFC 9126 §§2.1, 4,
  7.4.
- **DF8 — Redirect URI sem exceção PAR:** usar exatamente `IRedirectUriValidator` e as options fechadas pelo
  plano RFC 9700; não habilitar redirect URI dinâmica específica para PAR. Fonte: RFC 9126 §2.4 permite, mas não
  exige, o relaxamento; baseline segura do roadmap prevalece.
- **DF9 — Erros do POST:** responder no formato JSON do token endpoint; autenticação inválida segue
  `invalid_client`/401/challenge e validação usa o erro OAuth/OIDC aplicável, com `invalid_request` como fallback;
  nunca iniciar redirect ou erro dependente de interação. Fonte: RFC 9126 §2.3 + plano OAuth 2.1.
- **DF10 — Request Objects diferidos:** rejeitar `request` no PAR com `request_not_supported` enquanto JAR não
  existir e rejeitar `request_uri` no POST PAR com `invalid_request`; não armazenar JWT opaco sem validá-lo.
  Fonte: RFC 9126 §§2.1, 3 + estado atual de `ProcessRequestObject`.
- **DF11 — Front channel mínimo:** uma autorização que usa PAR aceita no front channel somente um
  `client_id` singular e um `request_uri` singular; parâmetros de autorização adicionais não sobrescrevem nem
  complementam o payload armazenado. Fonte: RFC 9126 §4 + RFC 9101 §5.
- **DF12 — Falha indistinguível da referência:** handle desconhecido, expirado, já consumido, de outro client ou
  de outro realm produz `invalid_request_uri` sem redirect confiável e sem revelar o motivo. Fonte: RFC 9101 §7
  + isolamento de dados.
- **DF13 — Continuação preservada:** depois de consumir PAR, login/consentimento continuam usando
  `IAuthorizeParametersStore` com leitura repetível e delete no callback; o handle interno nunca é exposto como
  `request_uri`. Fonte: AP-01..AP-03 e fluxo atual.
- **DF14 — Policy opt-in:** endpoint nasce habilitado; `RealmOptions` e `Client` têm exigência de PAR com default
  `false`. Realm obrigatório rejeita toda request direta; client obrigatório rejeita apenas a sua. Fonte:
  RFC 9126 §§4-6, cujas metadata usam default `false`.
- **DF15 — Configuração explícita:** criar grupo de options próprio de PAR em `ServerOptions`/`RealmOptions`,
  copy constructors, validação de snapshot e feature gate em `EndpointsOptions`; não reutilizar options de
  interação ou JAR. Fonte: arquitetura de options vigente.
- **DF16 — Persistência incremental:** persistir o scalar do client e o payload de realm pelos mecanismos
  Configuration vigentes; adicionar tabela Operational e migrations SQLite/PostgreSQL; hosts não migram nem
  seedam implicitamente. Fonte: arquitetura de storage.
- **DF17 — Metadata fiel:** discovery publica `pushed_authorization_request_endpoint` somente quando endpoint
  está executável e `require_pushed_authorization_requests` somente para policy global do realm; exigência por
  client não altera metadata global. Fonte: RFC 9126 §§5-6.
- **DF18 — Cleanup físico:** expiração é absoluta e capturada no POST; leitura/consumo falha fechado no limite
  exato e cleanup periódico remove abandonados sem ser condição de segurança. Fonte: baseline Operational.
- **DF19 — Breaking change direto:** atualizar contexts, pipelines, options, serializers, migrations, seeds e
  testes sem shim; não há consumidor de produção a preservar. Fonte: AGENTS.md.

---

## Histórico de decisões

**Preparação do plano (ordem):**

- **Alternativas consideradas:** criar o plano somente depois dos predecessores ou planejá-lo agora e executá-lo
  em série.
  - **Resposta humana:** criar agora.
  - **Conclusão:** DF1 registra a execução depois de Reference Tokens/Introspection, sem impedir o planejamento.

**Preparação do plano (armazenamento):**

- **Alternativas consideradas:** `IMessageStore`, `IAuthorizeParametersStore`, facade específica ou facade
  geral.
  - **Fato verificado:** os dois contratos existentes não representam simultaneamente binding, TTL e consumo
    atômico; `IAuthorizeParametersStore` precisa continuar repetível.
  - **Conclusão:** `IMessageStore` foi descartado; Q1 preserva somente as duas alternativas arquiteturalmente
    válidas.

**Preparação do plano (JAR):**

- **Alternativas consideradas:** fazer `ProcessRequestObject` stub passar a validar JAR junto com PAR ou manter
  protocolos separados.
  - **Fato verificado:** RFC 9126 torna Request Object opcional; o código atual não valida assinatura, encryption,
    claims ou URI remota, apesar da metadata.
  - **Conclusão:** DF10 mantém JAR fora e exige metadata honesta.

---

## Design alvo

> O desenho abaixo usa os nomes da opção recomendada em Q1 e a operação estrita de Q2-A. Se outra opção for
> escolhida, a IA executora deve primeiro registrar a resposta no histórico, promover a decisão para `DF<n>` e
> atualizar contratos, modelo, fases, critérios e testes antes do primeiro edit de código.

### Contratos e bordas

- `IPushedAuthorizationRequestStore`: facade realm-bound dedicada ao artefato PAR.
- `CreateAsync(PushedAuthorizationRequest request, DateTime expiresAtUtc, ct)`:
  gera/insere a referência create-only e devolve `PushedAuthorizationRequestReference(RequestUri, ExpiresAtUtc)`;
  colisões regeneram sem sobrescrever.
- `ConsumeAsync(string requestUri, string expectedClientId, ct)`:
  decisão atômica que entrega o payload a no máximo um caller e devolve sucesso ou falha opaca.
- `PushedAuthorizationRequest`: `ClientId` + coleção imutável/cópia defensiva dos parâmetros de autorização
  normalizados; não contém parâmetros de autenticação do POST.
- `PushedAuthorizationRequestConsumeResult`: payload presente somente no sucesso; falhas não expõem motivo à
  camada HTTP.
- `PushedAuthorizationRequestContext`: context do POST, implementa a borda necessária para `EvaluateClient` e
  mantém parâmetros de autenticação separados dos parâmetros que serão armazenados.
- `AuthorizationRequestSource`: estado interno `Direct|Pushed`, definido somente pelo resolver antes da carga
  dos parâmetros; não é derivado de input mutável depois do consumo.
- `ResolvePushedAuthorizationRequest`: primeiro decorator das pipelines `AuthorizeContext` e
  `AuthorizeValidateContext`; resolve somente URNs emitidas pelo servidor e substitui a fonte crua antes da
  materialização.
- `LoadAuthorizeRequest`: realiza uma única carga dos parâmetros depois da resolução, eliminando a carga
  antecipada dos callers atuais.
- `RequirePushedAuthorizationRequestValidator`: depois de `LoadClient`, aplica policy de realm/client usando
  `AuthorizationRequestSource`.

### Modelo, dados e persistência

```text
Client
  RequirePushedAuthorizationRequests bool default false

RealmOptions
  PushedAuthorizationRequests PushedAuthorizationRequestOptions
    Lifetime TimeSpan/seconds             # Q3
    Required bool default false

EndpointsOptions
  EnablePushedAuthorizationRequestEndpoint bool default true

operational.pushed_authorization_requests
  realm_id uuid/text not null
  handle_digest varchar not null
  client_id varchar not null
  created_at_utc timestamp not null
  expires_at_utc timestamp not null
  payload_version integer not null
  protected_payload text/blob not null
  primary key (realm_id, handle_digest)
  index (realm_id, expires_at_utc)
```

- Q2-B adiciona somente os campos/binding necessários à tolerância escolhida; não manter payload reutilizável
  apenas por conveniência.
- `handle_digest` usa `OperationalLookupDigest` com novo
  `OperationalRecordTypes.PushedAuthorizationRequest`.
- O payload versionado preserva parâmetros repetidos, ordem quando semanticamente relevante e valores vazios,
  como o serializer atual de authorize parameters, mas possui purpose/nome próprios.
- `client_id` em coluna permite consumo condicional sem descriptografar payload de outro client.
- O payload protegido não contém o `request_uri`, credenciais de autenticação, client secret/assertion ou
  headers.
- Registrar novas operações PAR-01 (create), PAR-02 (consume) e PAR-03 (cleanup) na matriz antes do adapter.

### Arquitetura alvo

```text
RoyalIdentity/
  Models/Client.cs
  Options/PushedAuthorizationRequestOptions.cs
  Contracts/Storage/IPushedAuthorizationRequestStore.cs
  Contexts/PushedAuthorizationRequestContext.cs
  Contexts/Decorators/ResolvePushedAuthorizationRequest.cs
  Contexts/Decorators/LoadAuthorizeRequest.cs
  Contexts/Validators/RequirePushedAuthorizationRequestValidator.cs
  Endpoints/PushedAuthorizationRequestEndpoint.cs
  Handlers/PushedAuthorizationRequestHandler.cs
  Responses/PushedAuthorizationRequestResponse.cs

RoyalIdentity.Data.Configuration/
  ClientEntity.RequirePushedAuthorizationRequests

RoyalIdentity.Data.Operational/
  PushedAuthorizationRequestEntity

RoyalIdentity.Storage.EntityFramework/
  Configuration materializer
  Operational store, serializer, protection e cleanup

RoyalIdentity.Storage.EntityFramework.Sqlite|PostgreSql/
  migrations Configuration e Operational incrementais
```

### Fluxo alvo

```text
POST /{realm}/connect/par
  parse form + HTTPS
  -> autenticar client como no token endpoint
  -> separar credenciais dos parâmetros de autorização
  -> rejeitar request_uri e JAR não suportado
  -> conferir client_id == client autenticado
  -> validar authorization request sem UI
  -> persistir payload protegido/TTL
  -> 201 { request_uri, expires_in }

GET|POST /{realm}/connect/authorize?client_id=...&request_uri=...
  -> reconhecer URN PAR
  -> consumir atomicamente por realm + client
  -> substituir Raw pelo payload armazenado
  -> marcar source=Pushed
  -> carregar AuthorizeContext
  -> carregar client e aplicar policy PAR
  -> revalidar redirect/resources/PKCE/client vigente
  -> seguir fluxo normal
  -> se houver UI, usar IAuthorizeParametersStore existente
```

### Segurança, concorrência e confiabilidade

- A referência tem 256 bits de entropia, lifetime curto e uso conforme Q2.
- Apenas o digest realm/type-bound chega ao banco; logs e eventos usam redaction/obfuscation.
- Create é create-only e nunca sobrescreve uma colisão.
- Consume é uma decisão única do banco; nenhuma implementação pode fazer `ReadAsync` seguido de
  `DeleteAsync` como decisão concorrente.
- Expiração usa `ExpiresAtUtc <= now` como inválido; cleanup atrasado não torna o registro aceitável.
- Client/realm mismatch nunca descriptografa nem retorna o payload.
- `request_uri` inválido não fornece redirect confiável e não gera oracle de existência/estado.
- Client assertion, secret, Authorization header e demais credenciais nunca entram no payload armazenado.
- Policy e configuração são reavaliadas depois do consumo; alteração administrativa pode invalidar uma request
  ainda não usada.
- PKCE, state e nonce permanecem responsabilidade do client e das pipelines vigentes; PAR não os gera.

### Compatibilidade, migração e rollout

- Executar somente depois de todos os predecessores de DF1 estarem concluídos e de Q1/Q2/Q3 fechadas.
- Antes da Fase 1, reler versões correntes dos payloads Configuration; fazer exatamente um bump sequencial
  quando o novo grupo de options exigir e adicionar leitura explícita das versões suportadas, sem presumir um
  número antigo do momento de criação deste plano.
- Gerar migrations Configuration e Operational separadas para SQLite/PostgreSQL e atualizar scripts SQL.
- `RoyalIdentity.Server` continua PostgreSQL e externamente provisionado; `RoyalIdentity.Demo` continua SQLite
  in-memory self-provisioned.
- Não introduzir fallback em memória, auto-migrate, dual-write ou compat shim.
- Endpoint pode ser desabilitado por realm; configuração `Required=true` com endpoint desabilitado falha na
  validação do snapshot/startup, em vez de produzir realm impossível de usar.

---

## Ordem de execução

1. **Fase 1 (decisões, contratos e configuração)** — fecha Q1/Q2/Q3 e estabiliza a superfície pública.
2. **Fase 2 (Operational)** — entrega create/consume/TTL antes de expor endpoint.
3. **Fase 3 (endpoint PAR)** — autentica, valida e emite referências persistidas.
4. **Fase 4 (authorization endpoint)** — consome a referência e entra no fluxo normal.
5. **Fase 5 (policy/discovery/JAR)** — publica capacidade real e aplica obrigatoriedade.
6. **Fase 6 (aceites/providers)** — prova concorrência, isolamento e PostgreSQL real.
7. **Fase 7 (documentação)** — fecha backlog e registra o resultado.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

---

## Fase 1 - Decisões, contratos e configuração

**Depende de:** Q1, Q2, Q3, DF1-DF5, DF14-DF16 e conclusão de
[plan-reference-tokens-introspection.md](plan-reference-tokens-introspection.md).

**Escopo:** perguntas/histórico deste plano, contratos core, `Client`, options, Configuration data/adapter,
migrations Configuration, seeds e testes de materialização.

**O que/como:** registrar as três respostas como decisões fechadas, atualizar o design condicional e só então
introduzir a API pública e as options. Persistir o scalar do client relacionalmente e o grupo de realm no payload
versionado vigente.

**Tarefas:**

- [ ] Registrar as respostas Q1/Q2/Q3 no histórico e criar DFs correspondentes.
- [ ] Remover os trechos condicionais do design, fases e critérios depois das respostas.
- [ ] Criar o contrato de storage escolhido em Q1 com `CancellationToken` obrigatório.
- [ ] Definir create/consume e seus resultados sem expor motivo de falha à camada HTTP.
- [ ] Adicionar `Client.RequirePushedAuthorizationRequests=false`.
- [ ] Criar `PushedAuthorizationRequestOptions` com policy e lifetime conforme Q3.
- [ ] Adicionar cópia independente das options a `ServerOptions` e `RealmOptions`.
- [ ] Adicionar `EnablePushedAuthorizationRequestEndpoint=true` e seu copy constructor.
- [ ] Validar lifetime e incompatibilidade entre `Required=true` e endpoint desabilitado.
- [ ] Adicionar o scalar a `ClientEntity`, model builder e `ClientMaterializer`.
- [ ] Fazer bump sequencial e compatibilidade explícita do payload de realm vigente.
- [ ] Gerar migrations Configuration SQLite/PostgreSQL e SQL revisável.
- [ ] Atualizar seeds/fixtures e coverage tests de scalars do client.
- [ ] Provar snapshot last-known-good para configuração PAR inválida.
- [ ] Executar o aceite PostgreSQL Configuration.

**Critérios de aceite:** Q1/Q2/Q3 estão fechadas; contrato não conflita com
`IAuthorizeParametersStore`; client anterior materializa `false`; options anteriores materializam defaults
decididos; configuração inválida não é publicada; providers não possuem pending model changes.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~ConfigurationModelClientCoverageTests|FullyQualifiedName~ConfigurationMaterializationClientTests|FullyQualifiedName~ConfigurationModelPayloadTests|FullyQualifiedName~ConfigurationMigration"
dotnet test Tests.Integration --filter "FullyQualifiedName~RealmOptions"
./scripts/Test-ConfigurationPostgreSql.ps1
```

### Resultado da Fase 1

*a preencher*

---

## Fase 2 - Persistência Operational e consumo atômico

**Depende de:** Fase 1, decisões originadas por Q1/Q2/Q3, DF5, DF6, DF16 e DF18.

**Escopo:** matriz, `IStorage`, Data.Operational, adapter Operational, serializer/protection, cleanup, migrations
SQLite/PostgreSQL e `Tests.Storage`.

**O que/como:** implementar a referência como artefato Operational próprio, com create-only, digest, payload
protegido, TTL absoluto e consumo atômico conforme Q2. Reutilizar primitives, não a semântica de leitura do
authorize-parameters store.

**Tarefas:**

- [ ] Registrar PAR-01/PAR-02/PAR-03 e ownership na matriz antes de implementar.
- [ ] Adicionar accessor realm-bound escolhido em Q1 ao `IStorage` e gateway EF.
- [ ] Criar gerador injetável de referência com prefixo registrado e 32 bytes aleatórios.
- [ ] Criar entidade/tabela com PK realm+digest, client binding, timestamps, versão e payload protegido.
- [ ] Criar serializer próprio que preserve parâmetros repetidos/vazios e faça cópia defensiva.
- [ ] Excluir credenciais, headers, `request_uri` e handle do payload antes da serialização.
- [ ] Usar `OperationalLookupDigest` com record type próprio.
- [ ] Implementar create-only com regeneração de colisão e limite explícito de tentativas.
- [ ] Implementar consume em uma transação/statement cuja decisão de vencedor seja atômica no banco.
- [ ] Implementar a semântica de retry/reload exatamente conforme Q2.
- [ ] Aplicar expiração fail-closed no limite exato sem depender de cleanup.
- [ ] Integrar purge/worker por expiração e purge de realm.
- [ ] Gerar migrations Operational e SQL para SQLite/PostgreSQL.
- [ ] Adicionar contracts provider-neutral de roundtrip, duplicidade, expiração, client/realm e concorrência.
- [ ] Provar que a referência bruta não aparece na entidade nem no payload desprotegido.
- [ ] Executar os contratos no PostgreSQL real.

**Critérios de aceite:** somente um consumidor concorrente obtém o payload conforme Q2; mismatch/expiração são
falhas opacas; handle nunca é persistido bruto; colisão não sobrescreve; cleanup remove abandonados; SQLite e
PostgreSQL satisfazem os mesmos contratos.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~PushedAuthorizationRequest|FullyQualifiedName~OperationalPayload|FullyQualifiedName~OperationalCleanup|FullyQualifiedName~OperationalMigration"
./scripts/Test-OperationalPostgreSql.ps1
```

### Resultado da Fase 2

*a preencher*

---

## Fase 3 - Endpoint PAR e validação antecipada

**Depende de:** Fase 2, DF2-DF10 e baseline concluída de
[plan-oauth21-token-error-responses.md](plan-oauth21-token-error-responses.md).

**Escopo:** endpoint, context, parsing, pipeline, autenticação do client, handler, responses, constants, DI,
routes, `IAuthorizeRequestValidator`, limits e testes unitários/pipeline.

**O que/como:** criar um endpoint direto que separe credenciais de autenticação dos parâmetros armazenáveis,
reutilize os evaluators do token endpoint e valide a authorization request completa antes de criar a referência.

**Tarefas:**

- [ ] Adicionar rota/constants PAR sem reutilizar nomes/options de JAR.
- [ ] Criar endpoint somente HTTPS efetivo, POST e form UTF-8.
- [ ] Responder 405 para método incorreto, 415 para media type incorreto e respeitar limite de body.
- [ ] Criar context/pipeline no padrão `IEndpointHandler` → decorators/validators → handler.
- [ ] Reutilizar `EvaluateClient`/`IClientSecretChecker`, inclusive client público conforme sua configuração.
- [ ] Aceitar issuer, token endpoint e PAR endpoint como audiences de `private_key_jwt`.
- [ ] Rejeitar mecanismos/credenciais múltiplos conforme a baseline OAuth 2.1.
- [ ] Exigir `client_id` singular e igualdade com o client autenticado.
- [ ] Separar e descartar todos os parâmetros usados apenas na autenticação do client.
- [ ] Rejeitar `request_uri` e Request Object conforme DF10.
- [ ] Despachar `IAuthorizeRequestValidator` com os parâmetros de autorização limpos.
- [ ] Garantir que validação antecipada cubra redirect, response type, scopes/resources e PKCE aplicáveis.
- [ ] Mapear falhas para JSON/status/header conforme DF9, sem redirect.
- [ ] Persistir somente depois de toda validação e responder 201 com lifetime restante positivo.
- [ ] Aplicar `Cache-Control: no-store`, `Pragma: no-cache` e redaction de body/credentials.
- [ ] Adicionar testes de request, autenticação, erros, resposta e falha do store.

**Critérios de aceite:** endpoint HTTP/anônimo/form inválido não cria registro; client e request inválidos falham
antes da persistência; sucesso retorna 201 com URN e `expires_in`; payload não contém credenciais; erro segue a
taxonomia do protocolo e nunca redireciona.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~PushedAuthorizationRequest|FullyQualifiedName~ClientSecret|FullyQualifiedName~ClientAssertion"
dotnet test Tests.Pipelines
```

### Resultado da Fase 3

*a preencher*

---

## Fase 4 - Resolução no authorization endpoint

**Depende de:** Fase 3, DF7, DF11-DF13 e decisão originada por Q2.

**Escopo:** `AuthorizeEndpoint`, `AuthorizeContext`, `AuthorizeValidateContext`,
`DefaultAuthorizeRequestValidator`, decorators/validators, pipelines, responses de erro e fluxos de
login/consentimento.

**O que/como:** resolver a referência antes de materializar `AuthorizeContext`, marcar a origem internamente e
deixar todo o restante do fluxo percorrer as mesmas validações e handlers da request direta.

**Tarefas:**

- [ ] Remover a carga antecipada de `AuthorizeContext` do endpoint e do validator interno.
- [ ] Criar decorator de resolução como primeiro passo das duas pipelines de autorização.
- [ ] Criar carga única/idempotente dos parâmetros depois da resolução.
- [ ] Reconhecer somente URN PAR emitida pelo servidor; não buscar URIs remotas.
- [ ] Exigir `client_id` e `request_uri` singulares no front channel.
- [ ] Rejeitar parâmetros adicionais sem mesclar/sobrescrever o payload.
- [ ] Consumir por realm + expected client e mapear toda falha para `invalid_request_uri`.
- [ ] Não redirecionar falha de resolução, pois o redirect armazenado não foi autenticado/materializado.
- [ ] Substituir os parâmetros crus por uma cópia do payload somente no sucesso.
- [ ] Marcar `AuthorizationRequestSource.Pushed` por API interna não controlada pelo input.
- [ ] Reexecutar load-client, redirect, resources/scopes, response type e PKCE sobre estado vigente.
- [ ] Confirmar que policy/client alterados depois do POST invalidam a autorização quando aplicável.
- [ ] Preservar o uso posterior de `IAuthorizeParametersStore` para login/consentimento.
- [ ] Provar authorization code, login, consent, `prompt=none`, state e nonce pelo caminho PAR.
- [ ] Provar concorrência e reload conforme Q2 no nível HTTP.

**Critérios de aceite:** payload só entra na pipeline depois de consumo válido; não há merge com front channel;
client/realm mismatch não vaza dados; policy vigente é revalidada; interação continua funcionando pelo store
interno; concorrência observa exatamente Q2.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~Authorize|FullyQualifiedName~PushedAuthorizationRequest"
dotnet test Tests.Integration --filter "FullyQualifiedName~Authorize|FullyQualifiedName~Login|FullyQualifiedName~Consent|FullyQualifiedName~PushedAuthorizationRequest"
```

### Resultado da Fase 4

*a preencher*

---

## Fase 5 - Política, discovery e separação de JAR

**Depende de:** Fase 4, DF10, DF14, DF15 e DF17.

**Escopo:** policy validators, options/snapshot, discovery, constants, `ProcessRequestObject`,
`EnableJwtRequestUri`, tests de metadata e integração.

**O que/como:** aplicar obrigatoriedade somente depois de conhecer o client e publicar apenas capacidades
executáveis. Distinguir a URN de PAR de Request Object por valor/URI e retirar a alegação falsa de JAR.

**Tarefas:**

- [ ] Rejeitar request direta com `invalid_request` quando o realm exige PAR.
- [ ] Rejeitar request direta com `invalid_request` quando somente o client exige PAR.
- [ ] Permitir request direta quando ambas as policies são `false`.
- [ ] Falhar snapshot/startup para policy obrigatória com endpoint desabilitado.
- [ ] Publicar `pushed_authorization_request_endpoint` somente com endpoint ativo e URL HTTPS realm-scoped.
- [ ] Publicar `require_pushed_authorization_requests=true` somente para policy global.
- [ ] Omitir ou publicar `false` conforme convenção vigente quando policy global está desativada.
- [ ] Não condicionar PAR a `EnableJwtRequestUri`, `request_uri_parameter_supported` ou registro de URI JAR.
- [ ] Corrigir `request_parameter_supported` para não anunciar Request Object enquanto o stub existir.
- [ ] Corrigir `request_uri_parameter_supported`/`EnableJwtRequestUri` para não prometer fetch JAR inexistente.
- [ ] Responder `request_not_supported` para JAR por valor e `request_uri_not_supported` para URI não PAR.
- [ ] Adicionar testes exatos de discovery para endpoint ativo/inativo e policy realm/client.
- [ ] Adicionar guard que falha se metadata de JAR voltar sem implementação real.

**Critérios de aceite:** policy global/client aceita apenas origem PAR quando exigida; discovery anuncia endpoint
real e policy global correta; metadata de JAR não é usada para PAR nem afirma suporte inexistente; desligar PAR
não deixa configuração obrigatória silenciosamente inválida.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~Discovery|FullyQualifiedName~PushedAuthorizationRequest|FullyQualifiedName~RequestObject"
dotnet test Tests.Integration --filter "FullyQualifiedName~Discovery|FullyQualifiedName~PushedAuthorizationRequest"
```

### Resultado da Fase 5

*a preencher*

---

## Fase 6 - Aceites multi-realm e paridade dos providers

**Depende de:** Fase 5, DF1-DF19 e Q1/Q2/Q3 fechadas.

**Escopo:** `Tests.Integration`, `Tests.Storage`, `Tests.Architecture`, hosts, migrations/scripts e PostgreSQL
real.

**O que/como:** executar a feature ponta a ponta com clients públicos/confidenciais, dois realms, concorrência
real e os dois providers. Testes de protocolo devem criar o handle pelo endpoint, não semear a tabela.

**Tarefas:**

- [ ] Executar code flow completo usando PAR com client confidencial e `client_secret_basic`.
- [ ] Executar code flow completo usando PAR com client público + PKCE S256.
- [ ] Executar PAR com `private_key_jwt` e cada audience normativa aceita.
- [ ] Provar que audience/client assertion inválidos não criam registro.
- [ ] Provar que a mesma string não atravessa realm nem client.
- [ ] Provar expiração nos instantes anterior, exato e posterior ao limite.
- [ ] Provar vencedor único/tolerância decidida em Q2 sob concorrência real.
- [ ] Provar que redirect URI, scopes/resources e PKCE alterados depois do push falham fechado.
- [ ] Provar policy global, por client e endpoint desabilitado.
- [ ] Provar ausência de handle/credenciais em banco, payload, logs e eventos.
- [ ] Validar cleanup/purge e migrations desde o schema predecessor.
- [ ] Validar Configuration e Operational no PostgreSQL 17 real.
- [ ] Atualizar guards de arquitetura para boundaries Data/core/adapter.
- [ ] Executar todos os composition roots e a solution inteira.

**Critérios de aceite:** fluxo PAR funciona nos clients suportados; concorrência, expiração, client e realm são
fail-closed; PostgreSQL/SQLite têm paridade; discovery é fiel; nenhum secret/handle bruto é persistido; solution
verde.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~PushedAuthorizationRequest|FullyQualifiedName~Discovery|FullyQualifiedName~AuthorizationCode"
dotnet test Tests.Storage --filter "FullyQualifiedName~PushedAuthorizationRequest|FullyQualifiedName~OperationalCleanup|FullyQualifiedName~Configuration"
dotnet test Tests.Architecture
./scripts/Test-ConfigurationPostgreSql.ps1
./scripts/Test-OperationalPostgreSql.ps1
./scripts/Test-ServerPostgreSql.ps1
dotnet test RoyalIdentity.sln
```

### Resultado da Fase 6

*a preencher*

---

## Fase 7 - Documentação e fechamento do backlog

**Depende de:** Fases 1-6, DF1-DF19 e Q1/Q2/Q3 fechadas.

**Escopo:** backlog, roadmap, plano, foundations, matriz, AGENTS e READMEs de hosts/migrations.

**O que/como:** registrar comportamento comprovado, mover o plano para concluído e manter JAR/Admin/demais
extensões explicitamente diferidos.

**Tarefas:**

- [ ] Marcar o item PAR como `✅ CONCLUÍDO` no backlog.
- [ ] Registrar respostas Q1/Q2/Q3 e remover perguntas abertas.
- [ ] Atualizar roadmap para mover o plano à seção concluída.
- [ ] Atualizar `product.md`, `tech.md` e `structure.md` com endpoint, policies e ownership do store.
- [ ] Atualizar a matriz com assinaturas e testes finais de PAR-01..PAR-03.
- [ ] Atualizar AGENTS.md se a nova facade/artefato criar regra persistente de trabalho.
- [ ] Documentar options, defaults, endpoint e discovery sem credenciais reais.
- [ ] Documentar provisionamento das migrations novas e ausência de auto-migrate no Server.
- [ ] Registrar JAR e UI/Admin como diferidos, sem metadata de capacidade.
- [ ] Executar scans contra metadata falsa, uso indevido dos stores e segredos/handles em claro.
- [ ] Preencher o resultado de todas as fases e conferir a matriz de rastreabilidade.
- [ ] Executar build e solution test finais.

**Critérios de aceite:** backlog/roadmap/foundations descrevem a feature entregue; matriz possui ownership e
semânticas finais; JAR continua explicitamente separado; nenhum exemplo contém segredo; plano tem 7/7 fases e
verificação final verde.

**Testes:**

```powershell
rg -n "Pushed Authorization|request_uri|RequirePushedAuthorizationRequests|IPushedAuthorizationRequestStore" AGENTS.md .ai README.md RoyalIdentity.Server
if (rg -n "RequestParameterSupported, true" RoyalIdentity/Handlers/DiscoveryHandler.cs) { throw "Discovery ainda anuncia JAR sem implementação." }
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

### Resultado da Fase 7

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(es) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| Endpoint PAR autenticado | 1, 3 | DF2-DF4, DF9, Q3 | POST/HTTPS/form; 201; erros diretos | PushedAuthorizationRequest; ClientAssertion |
| Handle seguro e consumível | 2, 4 | DF5, DF6, DF11-DF13, Q1, Q2 | digest-only; binding; decisão atômica | storage contracts; concorrência HTTP |
| Validação vigente | 3, 4 | DF7, DF8 | valida no push e no authorize | Authorize; PKCE; resources; redirect |
| Policy realm/client | 1, 5 | DF14, DF15 | direct rejeitada apenas quando exigida | policy integration; snapshot |
| Metadata fiel e JAR separado | 5 | DF10, DF17 | endpoint/policy reais; sem capability falsa | Discovery; RequestObject guard |
| Providers e isolamento | 2, 6 | DF16, DF18, DF19 | SQLite/PostgreSQL; realm/client fail-closed | storage; provider scripts; integration |
| Fechamento documental | 7 | DF1-DF19 | backlog/roadmap/matriz alinhados | `rg`; build; solution test |

---

## Invariantes a preservar

1. Toda operação e todo registro de PAR são realm-scoped.
2. O `request_uri` é client-bound e nunca autoriza outro client.
3. O handle bruto não aparece em banco, payload, log ou evento.
4. Credenciais do POST PAR nunca entram nos parâmetros armazenados.
5. Consume é atômico; `read` seguido de `delete` não implementa a decisão.
6. Expiração é absoluta e fail-closed no instante exato.
7. `IAuthorizeParametersStore` continua sendo a continuação interna repetível de login/consentimento.
8. PAR não depende de `IMessageStore`, `IReplayProtectionStore`, JAR ou cache.
9. A authorization request é validada no POST e revalidada contra policy vigente no authorize.
10. Redirect URI segue a policy do RFC 9700 sem relaxamento implícito.
11. PKCE permanece default-on e S256 continua o caminho seguro.
12. Authorization code permanece single-use.
13. Falha de referência não usa redirect não validado e não revela motivo.
14. Discovery não anuncia endpoint, método ou protocolo inexistente.
15. `Data.Configuration`/`Data.Operational` não referenciam o core; somente o adapter conhece ambos.
16. Server não migra/seed; Demo continua SQLite self-provisioned.
17. Resources/scopes permanecem no owner vigente; PAR não cria persistência incidental do catálogo.

---

## Critérios globais de conclusão

- Q1/Q2/Q3 foram respondidas e promovidas a decisões fechadas.
- Endpoint PAR cumpre POST/HTTPS/form, autenticação, validação e resposta RFC 9126.
- `request_uri` possui 256 bits de entropia, TTL decidido e binding realm/client.
- Handle e credenciais não são persistidos ou registrados em claro.
- Consumo observa atomicamente a semântica escolhida em Q2.
- Authorization endpoint não mescla parâmetros e revalida o estado vigente.
- Policy global/client e feature gate possuem defaults/validação/documentação coerentes.
- Discovery anuncia PAR real e não anuncia JAR inexistente.
- Migrations Configuration/Operational e SQL dos dois providers estão atualizados.
- SQLite/PostgreSQL, clients público/confidencial e dois realms estão cobertos.
- Backlog, roadmap, foundations, matriz e READMEs refletem o resultado.
- `dotnet build RoyalIdentity.sln` e `dotnet test RoyalIdentity.sln` passam.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| Continuação interna vira single-use | reutilizar `IAuthorizeParametersStore` sem operações separadas | login/consent falha após primeira leitura | Q1 + DF13 + regressões de callback | Aberto |
| Dois consumidores recebem payload | implementação faz read/delete em chamadas distintas | replay/corrida de autorização | decisão atômica + teste concorrente real | Aberto |
| Handle vaza no banco/payload | entidade/serializer inclui URI pública | roubo do banco permite iniciar autorização | digest-only + guard estrutural | Aberto |
| Credencial entra no payload | form inteiro é persistido | secret/assertion recuperável | separação allowlist + inspeção de payload | Aberto |
| Client swapping | front-channel client não é comparado com registro | request de outro client é utilizada | consume realm+expected client | Aberto |
| Policy muda entre push e uso | validação inicial é tratada como definitiva | scope/redirect/client antigo é aceito | DF7 + revalidação completa | Aberto |
| Erro vira open redirect | servidor usa redirect do payload antes de resolver | redirecionamento para alvo não confiável | falha local sem redirect | Aberto |
| PAR conflita com JAR | todo `request_uri` usa o mesmo branch/option | fetch indevido ou metadata falsa | prefixo próprio + DF10/DF17 | Aberto |
| Lifetime reaproveita interação | usa `AuthorizationInteractionLifetime` | referência fica válida além da policy esperada | Q3 + option própria | Aberto |
| Tolerância de reload cria replay | Q2-B sem binding server-side | múltiplos fluxos válidos | exigir binding atômico ou escolher Q2-A | Aberto |
| Migration diverge por provider | tabela/índice/default diferem | comportamento distinto em produção | contracts + scripts PostgreSQL | Aberto |
| Client assertion rejeita audience PAR | evaluator aceita apenas token endpoint | clients `private_key_jwt` não interoperam | DF3 + matriz de três audiences | Aberto |
| Metadata afirma JAR | stub continua com flag true | clientes enviam formato não processado | correção/guard da Fase 5 | Aberto |

---

## Diferidos e backlog

- JAR por valor, URI remoto, assinatura e encryption — destino: novo plano de JAR/RFC 9101.
- JARM — destino: plano próprio.
- Tolerância de reload não escolhida em Q2 — destino definido ao fechar Q2.
- Dynamic redirect URI específica de PAR — diferida indefinidamente; reabrir somente com requisito explícito e
  nova análise de segurança.
- UI/API para `RequirePushedAuthorizationRequests`, lifetime e feature gate — destino:
  `plan-admin-api-ui.md`.
- Rate limiting específico do PAR/HTTP 429 — destino: plano de rate limiting transversal; o endpoint deve
  continuar compatível com middleware futuro.
- Persistência do catálogo de resources/scopes — destino:
  futuro `plan-data-resource-catalog-storage.md`.
- Cache/distributed store alternativo — destino: novo adapter somente quando houver deployment/requisito que o
  justifique, preservando o mesmo contrato atômico.

---

## Referências

- [an-par-rfc-9126.md](../analisys/an-par-rfc-9126.md).
- [backlog-001.md](../backlogs/backlog-001.md).
- [plans-roadmap-02.md](plans-roadmap-02.md).
- [plan-data-storage-matrix.md](plan-data-storage-matrix.md).
- [plan-data-operational-storage.md](plan-data-operational-storage.md).
- [plan-replay-protection.md](plan-replay-protection.md).
- [plan-oauth21-token-error-responses.md](plan-oauth21-token-error-responses.md).
- [plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md).
- [plan-reference-tokens-introspection.md](plan-reference-tokens-introspection.md).
- [ADR-013](../../adrs/ADR-013.md) e [ADR-018](../../adrs/ADR-018.md).
- [RFC 6749](https://www.rfc-editor.org/rfc/rfc6749.html).
- [RFC 8414](https://www.rfc-editor.org/rfc/rfc8414.html).
- [RFC 9101](https://www.rfc-editor.org/rfc/rfc9101.html).
- [RFC 9126](https://www.rfc-editor.org/rfc/rfc9126.html).
- [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700.html).
