# Plan: Reference Tokens e Token Introspection RFC 7662 (`plan-reference-tokens-introspection`)

## Status: RASCUNHO - Q1/Q2 pendentes antes da Fase 5; 0 de 7 fases executadas

## Progresso

`░░░░░░░` **0%** - 0 de 7 fases

| Fase | Estado |
|---|---|
| Fase 1 - Tipo de access token no Client e persistência Configuration | Pendente |
| Fase 2 - Emissão opaca centralizada e segura | Pendente |
| Fase 3 - Emissão por authorization code, client credentials e refresh | Pendente |
| Fase 4 - Ciclo de vida, bearer, revogação e contratos Operational | Pendente |
| Fase 5 - Endpoint RFC 7662 e autenticação do ResourceServer | Bloqueada por Q1/Q2 |
| Fase 6 - Discovery, aceites multi-realm e paridade de providers | Bloqueada pela Fase 5 |
| Fase 7 - Documentação e fechamento do backlog | Bloqueada pela Fase 6 |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluída, `%` e `X de 7`). Antes de fechar uma fase, confirme que decisões,
> critérios de aceite, testes e invariantes relacionados foram aplicados.

---

## Contexto

### Fontes verificadas

- [backlog-001.md](../backlogs/backlog-001.md) — emissão de reference token continua pendente; store e bearer
  já possuem suporte parcial.
- [plan-refactoring-debt-closure.md](plan-refactoring-debt-closure.md) — remove metadata falsa de introspection
  e reserva “Introspection + reference tokens” para um plano próprio.
- [plan-data-operational-storage.md](plan-data-operational-storage.md) e
  [plan-data-storage-matrix.md](plan-data-storage-matrix.md) — semânticas AT-01..AT-04, digest de lookup,
  proteção do payload e ciclo de vida já fechados.
- [plan-resources-redesign.md](plan-resources-redesign.md) — `ResourceServer.Secrets` foi mantido
  explicitamente para autenticação futura no introspection.
- `RoyalIdentity/Contracts/Defaults/DefaultTokenFactory.cs` — emissão inicial fixa
  `AccessTokenType.Jwt`; só JWT passa por `IJwtFactory`.
- `RoyalIdentity/Handlers/RefreshTokenHandler.cs` — o caminho `Snapshot` também constrói JWT diretamente;
  o caminho `Current` delega ao token factory.
- `RoyalIdentity/Models/Tokens/AccessToken.cs` — `Id` e `Token` nascem iguais; o JWT substitui somente `Token`
  depois de assinado.
- `RoyalIdentity/Models/Client.cs` — não possui configuração de tipo/formato do access token.
- `RoyalIdentity.Storage.EntityFramework/Operational/Stores/EntityFrameworkAccessTokenStore.cs` — reference
  tokens são sempre persistidos, localizados pelo digest realm-scoped do bearer/JTI e nunca gravam o bearer
  bruto em coluna.
- `RoyalIdentity/Contracts/Defaults/DefaultTokenValidator.cs` e
  `RoyalIdentity/Contexts/Decorators/EvaluateBearerToken.cs` — bearer sem ponto segue para validação de
  reference token, com verificação explícita do tipo.
- `Tests.Integration/Endpoints/ReferenceTokenBearerTests.cs` — prova consumo de reference token semeado
  diretamente e documenta que a emissão atual produz apenas JWT.
- `Tests.Storage/Operational/SqliteOperationalAccessTokenTests.cs` e
  `Tests.Storage/Operational/OperationalPayloadTests.cs` — cobrem digest, isolamento, tipo, payload e ausência
  do handle bruto.
- [RFC 7662](https://www.rfc-editor.org/rfc/rfc7662.html) — request/response, autenticação obrigatória,
  `active=false`, privacidade e segurança do introspection.
- [RFC 8414](https://www.rfc-editor.org/rfc/rfc8414.html) — metadata do endpoint e métodos de autenticação.
- [RFC 7009](https://www.rfc-editor.org/rfc/rfc7009.html) — revogação e `token_type_hint`.
- [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700.html) — baseline de segurança executada antes deste plano.

### Estado atual do código (verificado em 2026-07-30)

- **Emissão somente JWT:** `DefaultTokenFactory.CreateAccessTokenAsync` e
  `RefreshTokenHandler.IssueFromSnapshotAsync` passam `AccessTokenType.Jwt`.
- **Configuração ausente:** `Client`, `ClientEntity` e `ClientMaterializer` não possuem `AccessTokenType`.
- **Store pronto para reference:** reference tokens ignoram `JwtAccessTokenPersistenceMode`, são sempre
  persistidos, usam `SHA-256` realm/type-bound e rematerializam `Id`/`Token` pelo argumento de lookup.
- **Bearer pronto parcialmente:** UserInfo aceita um reference token válido; JWT persistido não pode ser usado
  pelo seu `jti` como segundo bearer opaco.
- **Revogação pronta parcialmente:** RFC 7009 remove reference token pelo bearer e remove reference tokens
  relacionados ao revogar refresh token; faltam aceites com tokens realmente emitidos.
- **Introspection inexistente:** há constantes protocolares e metadata legada, mas nenhum endpoint, context,
  pipeline ou response RFC 7662. O plano predecessor remove a option/metadata falsa antes desta execução.
- **ResourceServer já modela credenciais:** `ResourceServer.Secrets` existe, mas `IResourceStore` não oferece
  lookup direto por nome e os evaluators atuais autenticam `Client`, não `ResourceServer`.
- **Configuration relacional:** cada scalar público de `Client` deve possuir coluna e decisão no
  `ClientMaterializer`; migrations SQLite/PostgreSQL e testes de cobertura protegem essa regra.
- **Testes focados verdes:** em 2026-07-30, `ReferenceTokenBearerTests` passou 3/3 e os testes focados de
  access-token storage passaram 28/28.

### Lacunas, conflitos e restrições

- **Bearer e identidade compartilham o valor:** no modelo atual o bearer reference coincide com `AccessToken.Id`;
  adicionar indiscriminadamente claim `jti` faria o handle secreto entrar no payload protegido.
- **Emissão duplicada no refresh:** o caminho `Snapshot` replica a construção do token e pode divergir do
  `DefaultTokenFactory`.
- **Introspection anunciado sem rota:** o predecessor precisa remover a superfície morta antes de este plano
  introduzir endpoint e metadata reais.
- **ResourceServer volátil:** o catálogo de resources/scopes ainda usa a bridge Configuration; este plano pode
  consumir o catálogo e seus secrets, mas não antecipará sua persistência relacional.
- **JWT opcionalmente não persistido:** introspection de JWT não pode depender de `IAccessTokenStore` quando
  `JwtAccessTokenPersistenceMode.None`; Q2 precisa fechar se JWT entra no primeiro corte.
- **Refresh possui semântica própria:** introspection de refresh token exigiria considerar consumo, tolerância,
  família/replay e autorização do caller; Q2 precisa fechar o escopo antes da Fase 5.
- **Sem compatibilidade obrigatória:** não existem clientes de produção; migrations, defaults e APIs públicas
  podem seguir diretamente o desenho correto.

### Superfícies impactadas a mapear

- `RoyalIdentity/Models/Client.cs` e `Models/Tokens` — configuração e identidade do access token.
- `RoyalIdentity/Contracts/Defaults/DefaultTokenFactory.cs` — construção e emissão comum.
- `RoyalIdentity/Handlers/RefreshTokenHandler.cs` — remover construção divergente do caminho `Snapshot`.
- `RoyalIdentity/Handlers/AuthorizationCodeHandler.cs` e `ClientCredentialsHandler.cs` — respostas/eventos.
- `RoyalIdentity/Contracts/Storage/IAccessTokenStore.cs` e `IResourceStore.cs` — semânticas consumidas e lookup
  do ResourceServer autenticador.
- `RoyalIdentity/Endpoints`, `Contexts`, `Handlers`, `Responses` e `Extensions` — endpoint RFC 7662.
- `RoyalIdentity/Options/Constants.cs` e `DiscoveryHandler` — parâmetros, erros e metadata normativos.
- `RoyalIdentity.Data.Configuration` e `RoyalIdentity.Storage.EntityFramework` — coluna do client,
  materialização e migrations.
- `Tests.Identity`, `Tests.Integration`, `Tests.Storage` e `Tests.Architecture` — unidade, HTTP, providers,
  isolamento e boundaries.

---

## Objetivo

1. Permitir que cada `Client` escolha `Jwt` ou `Reference`, com JWT como default persistido.
2. Emitir handles opacos com entropia criptográfica, sem assinatura e sem persistir/expor o valor bruto fora da
   resposta ao client.
3. Aplicar o tipo configurado em authorization code, client credentials e toda renovação por refresh token.
4. Preservar claims, scopes, audiences, `resource`, `cnf`, lifetime, eventos, revogação e isolamento por realm.
5. Implementar Token Introspection conforme RFC 7662, autenticado por `ResourceServer` e com disclosure mínimo.
6. Anunciar somente endpoint e métodos de autenticação realmente executáveis.
7. Validar o comportamento em SQLite e PostgreSQL sem alterar as semânticas fechadas do storage.

## Fora de escopo

- Persistir o catálogo de ResourceServers/scopes — destino:
  futuro `plan-data-resource-catalog-storage.md`.
- DPoP, access tokens certificate-bound novos ou proof-of-possession adicional — destino: planos próprios.
- Dynamic Client Registration e CRUD/Admin de clients/resources — destino: `plan-admin-api-ui.md`.
- Cache de respostas de introspection — proibido neste corte; reavaliar somente com requisito e política de
  liveness/revogação.
- PAR, JAR/JARM adicional, Device Authorization, CIBA e Token Exchange — destinos próprios.
- Alterar formatos de refresh token ou authorization code.
- Tornar JWT statefully revogável quando `JwtAccessTokenPersistenceMode.None`.

---

## Perguntas ao humano

- **Q1 — Métodos de autenticação no introspection:** quais credenciais de `ResourceServer` entram no primeiro
  corte?
  - **Opções:**
    - **A) Recomendada:** `client_secret_basic` apenas, usando `ResourceServer.Name` + shared secret com hash
      SHA-256/SHA-512 e comparação em tempo constante; `private_key_jwt`/mTLS ficam diferidos.
    - **B)** `client_secret_basic`, `private_key_jwt` e `tls_client_auth` já no primeiro corte, com replay
      protection e metadata/mTLS aliases coerentes.
  - **Impacto se não decidir:** bloqueia contrato de autenticação, pipeline, metadata e testes da Fase 5.
  - **Status:** Aberta.

- **Q2 — Categorias introspectáveis:** quais tokens o primeiro corte do RFC 7662 resolve?
  - **Opções:**
    - **A) Recomendada:** somente access tokens `Reference`; JWT continua validado localmente e refresh token
      continua restrito ao token endpoint.
    - **B)** reference access token, JWT access token e refresh token; exige regras próprias para JWT não
      persistido, refresh consumido/família e disclosure por ResourceServer.
  - **Impacto se não decidir:** bloqueia lookup, definição de `active`, `token_type_hint` e matriz de respostas.
  - **Status:** Aberta.

---

## Decisões fechadas

- **DF1 — Tipo por client:** adicionar `Client.AccessTokenType` usando o enum existente, com
  `AccessTokenType.Jwt` como default. Fonte: backlog + modelo existente.
- **DF2 — Tipo atual em cada emissão:** emissão inicial e renovação consultam o `Client` materializado naquele
  request; alteração administrativa afeta somente access tokens emitidos depois dela. Fonte: intenção do backlog
  e ausência de tipo capturado no refresh token.
- **DF3 — Handle opaco:** reference token usa 32 bytes aleatórios de `CryptoRandom`, codificados em Base64Url
  sem padding; o valor serve como `AccessToken.Id` e `Token`. Fonte: baseline criptográfica do projeto.
- **DF4 — Sem claim do bearer:** reference token não adiciona o handle como claim `jti`, ainda que
  `Client.IncludeJwtId=true`; `IncludeJwtId` continua controlando apenas JWT. Fonte: DF38 Operational +
  confidencialidade do bearer.
- **DF5 — Sem assinatura:** somente `AccessTokenType.Jwt` chama `IJwtFactory`; reference conserva os mesmos
  metadados/claims em memória e no payload protegido do store. Fonte: enum/modelo/store atuais.
- **DF6 — Construção única:** `DefaultTokenFactory` é o único dono da construção de access token; refresh
  `Snapshot` fornece as claims já resolvidas por uma entrada explícita do request/serviço, sem recriar manualmente
  o token. Fonte: duplicação verificada no código.
- **DF7 — Semântica de protocolo preservada:** tipo não altera lifetime, scopes, audiences, `ResourceUris`,
  `Confirmation`, `token_type=Bearer`, `at_hash`, eventos ou formato da resposta do token endpoint. Fonte:
  contratos atuais OAuth/OIDC.
- **DF8 — Persistência obrigatória de reference:** reference ignora
  `JwtAccessTokenPersistenceMode`, é sempre persistido antes da resposta e falha a emissão se o store falhar.
  Fonte: `plan-data-operational-storage` DF13/DF31/DF38.
- **DF9 — Handle nunca persistido bruto:** digest continua sendo a única chave física; handle não entra em
  coluna, payload, claim, log ou evento em claro. Fonte: Operational DF38 + eventos obfuscados.
- **DF10 — Validação fail-closed:** lookup ausente, tipo diferente, expirado, client ausente/desabilitado ou
  realm incorreto nunca produz principal; JWT `jti` não é bearer. Fonte: regressão de segurança vigente.
- **DF11 — Revogação idempotente:** RFC 7009 remove somente token do client autenticado; token ausente ou de
  outro client não cria oracle; revogar refresh preserva a remoção realm-scoped dos reference tokens
  relacionados. Fonte: RFC 7009 + AT-03/AT-04.
- **DF12 — Caller do introspection:** autenticação pertence a `ResourceServer`, usando
  `ResourceServer.Secrets`; não modelar o caller como OAuth `Client`. Fonte: decisão #7 de
  `plan-resources-redesign.md`.
- **DF13 — Autorização por audiência:** ResourceServer autenticado só recebe `active=true` quando o token está
  destinado a ele por audience/resource protegido; caso contrário recebe somente `{"active":false}`. Fonte:
  RFC 7662 §§2.2/4 + isolamento de dados.
- **DF14 — Inativo sem detalhes:** token ausente, expirado, revogado, de tipo não suportado, de outro realm ou
  não autorizado para o ResourceServer responde HTTP 200 com somente `active=false`. Fonte: RFC 7662 §2.2.
- **DF15 — Resposta ativa mínima:** resposta ativa usa somente campos RFC aplicáveis e disponíveis:
  `active`, `scope`, `client_id`, `token_type`, `exp`, `iat`, `sub`, `aud` e `iss`; omite `username`, handle e
  `jti`. Fonte: RFC 7662 §2.2 + minimização.
- **DF16 — Sem cache no OP:** endpoint aplica no-cache; o OP não mantém cache de introspection e a decisão de
  atividade consulta o estado vigente. Fonte: requisito de revogação/liveness.
- **DF17 — Metadata fiel:** discovery publica `introspection_endpoint` e
  `introspection_endpoint_auth_methods_supported` somente depois que rota/pipeline estão reais e conforme Q1;
  signing algorithms e mTLS aliases só aparecem quando o método correspondente existir. Fonte: RFC 8414.
- **DF18 — HTTPS obrigatório:** endpoint não processa introspection sobre request efetivamente HTTP e discovery
  não anuncia URL HTTP; forwarded headers precisam estar aplicados pelo host antes do pipeline. Fonte:
  RFC 7662 §§2/4.
- **DF19 — Migration incremental:** `ClientEntity` ganha coluna inteira não nula com default `Jwt`; migrations
  SQLite/PostgreSQL e SQL versionado são atualizados, sem auto-migrate nos hosts. Fonte: arquitetura de storage.
- **DF20 — Breaking change direto:** atualizar seeds, fixtures e migrations sem shim de API/configuração; manter
  JWT default para clientes não alterados. Fonte: AGENTS.md.

---

## Histórico de decisões

**Preparação do plano (escopo):**

- **Alternativas consideradas:** implementar somente emissão ou entregar também validação remota.
  - **Fato verificado:** `plan-refactoring-debt-closure.md` agrupa “Introspection + reference tokens” como
    destino de um plano próprio; `ResourceServer.Secrets` já foi preservado para esse uso.
  - **Conclusão:** este plano inclui emissão, consumo local e RFC 7662; Q1/Q2 ficam abertas somente onde os
    artefatos anteriores não fecharam contrato público.

**Preparação do plano (identidade opaca):**

- **Alternativas consideradas:** separar `jti` interno do bearer ou manter o bearer como identidade do store.
  - **Fato verificado:** AT-01..AT-04 e DF38 já fecharam que o bearer reference coincide com `jti`/lookup e que
    somente o digest é persistido.
  - **Conclusão:** preservar a identidade única, mas aplicar DF4/DF9 para o bearer não reaparecer como claim ou
    payload.

---

## Design alvo

### Contratos e bordas

- `Client.AccessTokenType: AccessTokenType`: formato escolhido para novas emissões; default `Jwt`.
- `ITokenFactory.CreateAccessTokenAsync(AccessTokenRequest, ct)`: continua a única API pública de emissão;
  `AccessTokenRequest` ganha fonte explícita de claims pré-resolvidas para o caminho `Snapshot`, sem duplicar
  construção.
- `IAccessTokenStore`: mantém AT-01..AT-04; nomes/documentação trocam “jti” por “tokenIdOrHandle” quando isso
  tornar a semântica mais clara, sem alterar a decisão de digest.
- `IResourceStore.FindEnabledResourceServerByNameAsync(name, ct)`: lookup realm-bound para autenticar caller
  do introspection; registrar na matriz antes de implementar.
- `IResourceServerAuthenticator`: autentica o `ResourceServer` e devolve identidade/método sem reutilizar
  `EvaluatedClient`.
- `IntrospectionContext`: parâmetros `token` e `token_type_hint`, identidade do ResourceServer autenticado e
  response tipada.
- `IntrospectionEndpoint`: aceita somente POST form, HTTPS efetivo e limites de input.
- `IntrospectionHandler`: resolve o token conforme Q2, aplica DF13-DF16 e produz resposta RFC 7662.

### Modelo, dados e persistência

```text
configuration.clients
  access_token_type integer not null default 0   # Jwt=0, Reference=1
  check enum conhecido no materializer

operation.protocol_artifacts [existente]
  realm_id + artifact_type + lookup_digest PK
  access_token_type integer
  protected_payload sem bearer/jti reference
  created_at_utc / expires_at_utc
```

- Não criar tabela de introspection, cache ou sessão adicional.
- Não duplicar o handle opaco em `Client`, refresh token ou outra entidade.
- `AccessTokenPayloadSerializer` só muda de versão se a remoção do claim/novo dado realmente alterar seu
  contrato; não fazer bump mecânico.

### Arquitetura alvo

```text
RoyalIdentity/
  Models/Client.cs                         configuração Jwt|Reference
  Contracts/Defaults/DefaultTokenFactory  construção única
  Endpoints/IntrospectionEndpoint.cs       HTTP -> context
  Contexts/IntrospectionContext.cs         request tipada
  Contexts/Decorators|Validators/          autenticação e validação
  Handlers/IntrospectionHandler.cs         decisão active/metadata
  Responses/                               JSON RFC 7662

RoyalIdentity.Data.Configuration/
  ClientEntity.AccessTokenType             primitivo, sem referência ao core

RoyalIdentity.Storage.EntityFramework/
  ClientMaterializer + ResourceStore       adapter core <-> dados/bridge

RoyalIdentity.Storage.EntityFramework.Sqlite|PostgreSql/
  migrations Configuration incrementais
```

### Segurança, concorrência e confiabilidade

- Reference token tem 256 bits de entropia e nunca aparece em logs/eventos/persistência bruta.
- A escrita Operational termina antes da resposta; falha/colisão não devolve credencial sem backing.
- Comparação de shared secret usa hash e tempo constante; credencial ausente/inválida retorna HTTP 401 sem
  distinguir ResourceServer inexistente.
- Introspection não é endpoint público anônimo e não aceita HTTP.
- `active=false` é a única resposta para token desconhecido, inválido ou não autorizado.
- Token introspection nunca atravessa realm e nunca revela scopes/audiences de outro ResourceServer.
- `token_type_hint` é hint; quando incorreto, a busca cobre todos os tipos suportados definidos por Q2.
- Nenhum cache pode prolongar a atividade de token revogado/expirado.

### Compatibilidade, migração e rollout

- Executar depois de `plan-rfc9700-security-hardening.md` para não disputar remoção do front-channel ou rotação
  de refresh.
- Confirmar antes da Fase 1 que `plan-refactoring-debt-closure.md` removeu a metadata/option falsa.
- Migration de Configuration converte clientes existentes para `Jwt`; não alterar tokens já emitidos.
- Server continua externamente migrado; Demo continua self-provisioned SQLite.
- Ativar `Reference` somente nos seeds/testes específicos; não trocar silenciosamente todos os clients.
- ResourceServer/scopes permanecem na bridge até o plano de persistência do catálogo.

---

## Ordem de execução

1. **Fase 1 (Client/Configuration)** — cria a fonte de verdade persistida antes de qualquer emissão.
2. **Fase 2 (factory)** — centraliza identidade/claims e fecha a confidencialidade do handle.
3. **Fase 3 (grants/refresh)** — conecta todos os caminhos de emissão ao mesmo contrato.
4. **Fase 4 (lifecycle)** — prova store, bearer e revogação antes de expor validação remota.
5. **Fase 5 (introspection)** — depende das respostas Q1/Q2.
6. **Fase 6 (discovery/aceites)** — anuncia somente depois do endpoint real.
7. **Fase 7 (docs)** — fecha backlog após todos os testes obrigatórios.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

---

## Fase 1 - Tipo de access token no Client e persistência Configuration

**Depende de:** DF1, DF2, DF19, DF20 e conclusão de
[plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md).

**Escopo:** `Client`, `ClientEntity`, model builder, `ClientMaterializer`, seeds, fixtures, migrations
Configuration SQLite/PostgreSQL, SQL e `Tests.Storage`.

**O que/como:** adicionar o scalar enum ao core, persistir como inteiro no data model puro, validar valor
desconhecido fail-closed e gerar migrations incrementais nos dois providers.

**Tarefas:**

- [ ] Verificar que todos os planos predecessores da ordem do roadmap estão concluídos.
- [ ] Adicionar `Client.AccessTokenType` com default `Jwt` e documentação de emissão futura.
- [ ] Adicionar `ClientEntity.AccessTokenType` e mapping `access_token_type`.
- [ ] Mapear ida/volta em `ClientMaterializer` e rejeitar inteiro fora do enum.
- [ ] Atualizar o property-coverage test de `Client`.
- [ ] Gerar migrations Configuration SQLite/PostgreSQL com default `0` para linhas existentes.
- [ ] Atualizar snapshots, SQL revisável, seeds e fixtures.
- [ ] Provar roundtrip independente de `Jwt` e `Reference` em SQLite.
- [ ] Validar migration desde schema anterior e segunda execução idempotente.
- [ ] Executar aceite PostgreSQL real opt-in antes de concluir a fase.

**Critérios de aceite:** todo client materializado possui tipo conhecido; clientes anteriores viram JWT;
Reference roundtrips sem cast silencioso; valor inválido falha; os dois providers não têm pending model changes;
hosts não migram.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~ConfigurationMaterializationClientTests|FullyQualifiedName~ConfigurationModelClientCoverageTests|FullyQualifiedName~ConfigurationMigration"
./scripts/Test-ConfigurationPostgreSql.ps1
```

### Resultado da Fase 1

*a preencher*

---

## Fase 2 - Emissão opaca centralizada e segura

**Depende de:** Fase 1, DF3-DF9.

**Escopo:** `AccessTokenRequest`, `ITokenFactory`, `DefaultTokenFactory`, `AccessToken`,
`AccessTokenPayloadSerializer`, logs/eventos e testes unitários.

**O que/como:** fazer o factory escolher o tipo do client, gerar identidade adequada, construir claims uma vez e
assinar somente JWT. Oferecer entrada explícita para claims preexistentes sem permitir que callers reconstruam
manual e divergentemente o token.

**Tarefas:**

- [ ] Separar aquisição de claims da construção comum sem criar segundo token factory.
- [ ] Permitir que o caminho `Snapshot` forneça claims já resolvidas ao factory.
- [ ] Gerar JWT id conforme comportamento atual para JWT.
- [ ] Gerar handle reference com 32 bytes Base64Url e usá-lo como `Id`/`Token`.
- [ ] Aplicar `IncludeJwtId` somente a JWT; remover qualquer claim que replique o handle reference.
- [ ] Preencher scopes, audiences, `ResourceUris`, `Confirmation`, issuer, lifetime e realm igualmente nos dois
  tipos.
- [ ] Chamar `IJwtFactory` somente para JWT.
- [ ] Armazenar o token antes de retorná-lo e propagar falha do store.
- [ ] Garantir que logs e `AccessTokenIssuedEvent` continuem obfuscados.
- [ ] Adicionar testes determinísticos para tipo, assinatura, entropia/formato, claims e falha de store.
- [ ] Provar estruturalmente que payload reference não contém handle nem claim `jti`.

**Critérios de aceite:** factory emite JWT assinado ou handle opaco de 43 caracteres conforme client; reference
tem pelo menos 256 bits de entropia, não chama signer e não replica o bearer; store falho impede resposta; demais
metadados são equivalentes para o mesmo request.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~TokenFactory|FullyQualifiedName~ReferenceToken"
dotnet test Tests.Storage --filter "FullyQualifiedName~OperationalPayloadTests|FullyQualifiedName~SqliteOperationalAccessTokenTests"
```

### Resultado da Fase 2

*a preencher*

---

## Fase 3 - Emissão por authorization code, client credentials e refresh

**Depende de:** Fase 2, DF2, DF6, DF7 e refresh rotation concluída pelo plano RFC 9700.

**Escopo:** `AuthorizationCodeHandler`, `ClientCredentialsHandler`, `RefreshTokenHandler`, token responses,
`at_hash`, eventos e `Tests.Integration`.

**O que/como:** remover a construção manual do caminho `Snapshot`, passar todos os grants pelo factory e provar
que o access token retornado/eventado é exatamente o bearer persistido.

**Tarefas:**

- [ ] Fazer authorization code emitir o tipo configurado.
- [ ] Fazer client credentials emitir o tipo configurado.
- [ ] Fazer refresh `Current` continuar pelo factory e preservar recursos/scopes estreitados.
- [ ] Substituir `IssueFromSnapshotAsync` por chamada ao factory com claims snapshot explícitas.
- [ ] Aplicar o tipo atual do client em cada refresh, inclusive após mudança Jwt → Reference ou Reference → Jwt.
- [ ] Preservar `at_hash` do valor efetivamente devolvido, inclusive handle opaco.
- [ ] Preservar eventos obfuscados e resposta `Bearer`/`expires_in`/`scope`.
- [ ] Confirmar que nenhum access token é emitido no authorization endpoint após RFC 9700.
- [ ] Adicionar testes ponta a ponta para os três grants e os dois modos de claims do refresh.
- [ ] Adicionar regressão de mudança do tipo entre emissão do refresh e renovação.

**Critérios de aceite:** todos os caminhos suportados devolvem o tipo atual do client; nenhum helper constrói
`AccessToken` fora do factory; refresh preserva claims/resources conforme o modo e não emite antes da transição
atômica; `at_hash` continua válido.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~ClientToken|FullyQualifiedName~CodeToken|FullyQualifiedName~RefreshToken|FullyQualifiedName~ReferenceToken"
dotnet test Tests.Identity --filter "FullyQualifiedName~AccessToken|FullyQualifiedName~AtHash"
```

### Resultado da Fase 3

*a preencher*

---

## Fase 4 - Ciclo de vida, bearer, revogação e contratos Operational

**Depende de:** Fase 3, DF8-DF11 e AT-01..AT-04.

**Escopo:** `IAccessTokenStore`, `DefaultTokenValidator`, `EvaluateBearerToken`, `RevocationHandler`,
`Tests.Storage`, `Tests.Integration` e matriz de storage.

**O que/como:** preservar as semânticas já fechadas e trocar testes semeados manualmente por credenciais emitidas
quando o cenário for de protocolo. Não redesenhar o store sem evidência de lacuna.

**Tarefas:**

- [ ] Atualizar documentação de `IAccessTokenStore` para distinguir JWT id de reference handle.
- [ ] Manter digest realm/type-bound e escrita create-only.
- [ ] Manter reference persistido independentemente da policy de JWT.
- [ ] Provar que handle bruto não aparece em entidade, payload, log ou evento.
- [ ] Provar expiração no limite exato e cleanup sem aceitar token expirado.
- [ ] Provar rejeição de JWT `jti` e de artifact com tipo desconhecido.
- [ ] Provar isolamento do mesmo handle entre realms e impossibilidade de leitura cross-realm.
- [ ] Exercitar UserInfo com reference realmente emitido.
- [ ] Exercitar revogação com/sem hint, client correto/incorreto e token ausente.
- [ ] Exercitar revogação de refresh removendo somente reference tokens do mesmo subject/client/realm.
- [ ] Atualizar AT-01..AT-04 na matriz apenas se assinatura/documentação pública mudar.
- [ ] Executar os contratos contra SQLite e PostgreSQL opt-in.

**Critérios de aceite:** reference emitido funciona no bearer pipeline até expirar/revogar; JWT id continua
rejeitado; revogação é idempotente e não cria oracle; raw handle permanece ausente; contratos provider-neutral
continuam verdes.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~ReferenceTokenBearerTests|FullyQualifiedName~RevocationTests"
dotnet test Tests.Storage --filter "FullyQualifiedName~AccessTokenStoreContractTests|FullyQualifiedName~SqliteOperationalAccessTokenTests|FullyQualifiedName~OperationalCleanup"
./scripts/Test-OperationalPostgreSql.ps1
```

### Resultado da Fase 4

*a preencher*

---

## Fase 5 - Endpoint RFC 7662 e autenticação do ResourceServer

**Depende de:** Fase 4, Q1, Q2, DF12-DF18 e conclusão de
[plan-refactoring-debt-closure.md](plan-refactoring-debt-closure.md).

**Escopo:** `IResourceStore`, matriz de storage, authenticator/evaluators de ResourceServer,
`IntrospectionEndpoint`, context, decorators, validators, handler, responses, constants, DI/routes e testes.

**O que/como:** implementar POST form sobre HTTPS, autenticar ResourceServer conforme Q1, resolver os tipos
decididos em Q2 e retornar `active=false` como resposta indistinguível para qualquer token inativo/não autorizado.

**Tarefas:**

- [ ] Registrar na matriz o lookup realm-bound de ResourceServer por nome.
- [ ] Adicionar `FindEnabledResourceServerByNameAsync` e implementar na bridge/store vigente.
- [ ] Criar identidade/result de autenticação próprios de ResourceServer.
- [ ] Implementar os métodos decididos em Q1 reutilizando primitives de hash/chave/replay, não
  `EvaluatedClient`.
- [ ] Rejeitar credenciais múltiplas, malformadas, ausentes ou inválidas com HTTP 401 e challenge coerente.
- [ ] Criar endpoint somente POST, `application/x-www-form-urlencoded` e HTTPS efetivo.
- [ ] Rejeitar `token` ausente, vazio, repetido ou acima de `TokenHandle`/`Jwt` conforme tipo possível.
- [ ] Tratar `token_type_hint` como otimização e ampliar busca quando o hint estiver errado.
- [ ] Implementar resolvers somente para as categorias fechadas em Q2.
- [ ] Aplicar expiração, revogação, client ativo, realm e autorização por audience/resource.
- [ ] Responder token inativo/não autorizado com HTTP 200 e somente `active=false`.
- [ ] Produzir resposta ativa mínima conforme DF15, sem `jti`, username ou custom claims.
- [ ] Aplicar no-cache e redaction integral de token/credencial.
- [ ] Registrar endpoint/context/handler/pipeline/DI seguindo o padrão do repositório.
- [ ] Adicionar testes unitários da matriz request × auth × token × audience × realm.

**Critérios de aceite:** endpoint anônimo/HTTP não processa tokens; auth inválida é 401; token inválido ou fora da
audiência é `active=false`; token reference ativo e autorizado retorna somente campos permitidos; nenhum erro
revela existência, motivo ou outro realm.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~Introspection"
dotnet test Tests.Storage --filter "FullyQualifiedName~ResourceStore"
dotnet test Tests.Pipelines
```

### Resultado da Fase 5

*a preencher*

---

## Fase 6 - Discovery, aceites multi-realm e paridade de providers

**Depende de:** Fase 5, DF17-DF20.

**Escopo:** `DiscoveryHandler`, routes/constants, aliases conforme Q1, hosts, seeds, `Tests.Integration`,
`Tests.Storage`, `Tests.Architecture` e scripts PostgreSQL.

**O que/como:** anunciar o endpoint real, provar os fluxos externos em dois realms e validar que a configuração
Reference e as migrations funcionam nos dois providers.

**Tarefas:**

- [ ] Publicar `introspection_endpoint` HTTPS realm-scoped.
- [ ] Publicar `introspection_endpoint_auth_methods_supported` exatamente conforme Q1.
- [ ] Publicar signing algorithms/mTLS aliases somente quando exigidos pela resposta Q1.
- [ ] Confirmar que discovery nunca aponta introspection para token/revocation por engano.
- [ ] Criar ResourceServers de teste com secrets hashados e audiences/resources distintos.
- [ ] Emitir reference token por authorization code e client credentials e introspectá-lo pelo ResourceServer
  autorizado.
- [ ] Provar `active=false` para outro ResourceServer, realm, client desabilitado, expirado e revogado.
- [ ] Provar que mudar client para JWT altera apenas emissões futuras.
- [ ] Provar que JWT default permanece funcional e validável localmente.
- [ ] Validar migrations/seeds em SQLite e PostgreSQL 17 real.
- [ ] Adicionar guards de arquitetura para endpoint/persistência e ausência de handle bruto.
- [ ] Executar a solution inteira.

**Critérios de aceite:** discovery é fiel; reference funciona ponta a ponta em dois realms; disclosure segue
audience; JWT não regrediu; SQLite/PostgreSQL têm schema equivalente; todos os hosts compõem; solution verde.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~ReferenceToken|FullyQualifiedName~Introspection|FullyQualifiedName~Discovery"
dotnet test Tests.Storage --filter "FullyQualifiedName~Configuration|FullyQualifiedName~AccessToken|FullyQualifiedName~ResourceStore"
dotnet test Tests.Architecture
./scripts/Test-ServerPostgreSql.ps1
dotnet test RoyalIdentity.sln
```

### Resultado da Fase 6

*a preencher*

---

## Fase 7 - Documentação e fechamento do backlog

**Depende de:** Fases 1-6, DF1-DF20 e Q1/Q2 fechadas.

**Escopo:** backlog, roadmap, foundations, AGENTS, READMEs, matriz e plano.

**O que/como:** alinhar documentação com o comportamento comprovado e fechar somente itens realmente entregues.

**Tarefas:**

- [ ] Marcar o item Reference Token como `✅ CONCLUÍDO` no backlog.
- [ ] Registrar Q1/Q2 e decisões finais neste plano.
- [ ] Atualizar roadmap para mover o plano à seção concluída.
- [ ] Atualizar product/tech/structure com endpoint, client option e persistência.
- [ ] Atualizar matriz AT/ResourceStore com assinaturas e testes finais.
- [ ] Documentar configuração Jwt/Reference e autenticação de ResourceServer sem segredos reais.
- [ ] Documentar limitação do catálogo volátil e destino de sua persistência.
- [ ] Executar guards contra metadata falsa, handle bruto e construção de access token fora do factory.
- [ ] Preencher o resultado de todas as fases e conferir rastreabilidade.
- [ ] Executar build e solution test finais.

**Critérios de aceite:** documentação não diz que emissão/introspection estão ausentes; backlog/roadmap refletem
conclusão; diferidos continuam explícitos; nenhum segredo aparece nos exemplos; plano tem 7/7 resultados e testes
finais verdes.

**Testes:**

```powershell
rg -n "Reference Token|Introspection|AccessTokenType" AGENTS.md .ai README.md RoyalIdentity.Server
if (rg -n "AccessTokenType\\.Jwt" RoyalIdentity/Handlers/RefreshTokenHandler.cs) { throw "Refresh ainda fixa JWT." }
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

### Resultado da Fase 7

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(es) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| Tipo persistido por client | 1 | DF1, DF2, DF19, DF20 | roundtrip; default JWT; enum inválido falha | Configuration materialization/migrations |
| Handle opaco seguro | 2, 4 | DF3-DF5, DF8-DF10 | 256 bits; sem signer/raw handle/jti | TokenFactory; OperationalPayload; AccessTokenStore |
| Todos os grants respeitam o tipo | 3 | DF2, DF6, DF7 | code/client_credentials/refresh usam factory | ClientToken; CodeToken; RefreshToken |
| Lifecycle e revogação | 4 | DF8-DF11 | expiração/revogação/realm/client fail-closed | ReferenceTokenBearer; Revocation; cleanup |
| Introspection autenticado | 5 | DF12-DF18, Q1, Q2 | 401 para auth; false indistinguível; resposta mínima | Introspection unit/pipeline |
| Metadata e providers fiéis | 6 | DF17-DF20 | discovery exato; dois realms; SQLite/PostgreSQL | Discovery; integration; provider scripts |
| Fechar dívida/documentação | 7 | DF1-DF20 | backlog/roadmap/foundations alinhados | `rg`; build; solution test |

---

## Invariantes a preservar

1. Todo client, token, ResourceServer e lookup é realm-scoped.
2. JWT continua default e permanece validável sem lookup Operational.
3. Reference token nunca é devolvido sem persistência concluída.
4. Bearer/handle reference não aparece bruto em banco, payload, claim, log ou evento.
5. `jti` de JWT nunca é aceito como bearer reference.
6. Authorization code continua single-use e PKCE default-on.
7. Rotação/replay de refresh definida pelo RFC 9700 não é enfraquecida.
8. Scopes, audiences, `resource`, `cnf`, issuer, lifetime e `at_hash` independem do formato do access token.
9. Revogação não informa se token existia ou pertencia a outro client.
10. Introspection exige autenticação e HTTPS.
11. Token inativo/não autorizado responde somente `active=false`.
12. ResourceServer não vê token fora de sua audience/resource.
13. `Data.Configuration` não referencia o core; somente o adapter converte o enum.
14. Server nunca migra/seed; Demo continua self-provisioned SQLite.
15. Cache não participa de validação, revogação ou introspection.
16. Catálogo de resources permanece volátil até plano próprio; este plano não cria persistência incidental.

---

## Critérios globais de conclusão

- `Client.AccessTokenType` possui default JWT e roundtrip SQLite/PostgreSQL.
- Reference token é emitido por code, client credentials e refresh com handle de 256 bits.
- Nenhum access token é construído manualmente em handler.
- Reference token emitido funciona em UserInfo, expira e revoga corretamente.
- Handle bruto não é persistido nem registrado.
- Introspection cumpre Q1/Q2, RFC 7662 e minimização por ResourceServer.
- Discovery anuncia endpoint/métodos reais e URLs HTTPS realm-scoped.
- Dois realms e dois ResourceServers permanecem isolados.
- Migrations incrementais e SQL dos providers estão atualizados.
- Backlog, roadmap, foundations, matriz e READMEs refletem o resultado.
- `dotnet build RoyalIdentity.sln` e `dotnet test RoyalIdentity.sln` passam.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| Handle entra no payload como `jti` | `IncludeJwtId` aplicado antes do tipo | roubo do banco/protector expõe bearer | DF4/DF9 + guard estrutural | Aberto |
| Snapshot diverge do factory | handler continua construindo `AccessToken` | refresh ignora configuração/claims | DF6 + scan por construtores | Aberto |
| Token devolvido sem backing | store falha após response/event | bearer irrecuperável | persistir antes de retornar/eventar | Aberto |
| Introspection vira oracle | erros distinguem ausente/expirado/audience | enumeração de tokens | `active=false` único | Aberto |
| ResourceServer vê audiência alheia | só valida token ativo | vazamento de claims/scopes | DF13 + matriz multi-RS | Aberto |
| JWT `jti` volta a ser bearer | type check removido | bypass de assinatura | regressão existente obrigatória | Aberto |
| Migration perde default | coluna non-null sem default | upgrade falha/client inválido | DF19 + teste upgrade | Aberto |
| Metadata mente | métodos/aliases publicados sem evaluator | integração quebrada/falsa capacidade | derivar de Q1 + testes exatos | Aberto |
| HTTP aceita secrets/tokens | endpoint ignora scheme efetivo | interceptação de credenciais | DF18 + testes ForwardedHeaders | Aberto |
| Hint restringe busca | implementação confia em hint errado | token ativo responde false incorretamente | fallback entre tipos Q2 | Aberto |
| Resource catalog volátil limita produção | secrets só em bridge | restart/configuração externa necessária | documentar limite; persistência em plano próprio | Aceito |
| Mudança de tipo no refresh surpreende | admin altera Client durante grant | novo formato após renovação | DF2 + documentação/teste | Aberto |

---

## Diferidos e backlog

- Persistência do catálogo de resources/scopes/secrets — destino:
  futuro `plan-data-resource-catalog-storage.md`.
- Métodos de autenticação de introspection não escolhidos em Q1 — destino decidido ao fechar Q1.
- Categorias de token fora da resposta Q2 — destino: extensão deste plano ou plano RFC 7662 posterior.
- Cache de introspection — destino: novo requisito com política explícita de liveness/invalidação.
- DPoP e outros sender-constrained tokens — destino: plano próprio.
- Administração de `Client.AccessTokenType` e ResourceServer secrets — destino:
  `plan-admin-api-ui.md`.
- Persistência/revogação stateful de JWT — destino: novo plano somente se houver requisito.

---

## Referências

- [backlog-001.md](../backlogs/backlog-001.md).
- [plans-roadmap-02.md](plans-roadmap-02.md).
- [plan-refactoring-debt-closure.md](plan-refactoring-debt-closure.md).
- [plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md).
- [plan-data-operational-storage.md](plan-data-operational-storage.md).
- [plan-data-storage-matrix.md](plan-data-storage-matrix.md).
- [plan-resources-redesign.md](plan-resources-redesign.md).
- [ADR-010](../../adrs/ADR-010.md), [ADR-011](../../adrs/ADR-011.md) e
  [ADR-013](../../adrs/ADR-013.md).
- [RFC 6749](https://www.rfc-editor.org/rfc/rfc6749.html).
- [RFC 7009](https://www.rfc-editor.org/rfc/rfc7009.html).
- [RFC 7662](https://www.rfc-editor.org/rfc/rfc7662.html).
- [RFC 8414](https://www.rfc-editor.org/rfc/rfc8414.html).
- [RFC 8705](https://www.rfc-editor.org/rfc/rfc8705.html).
- [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700.html).
