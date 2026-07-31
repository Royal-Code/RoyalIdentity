# Plan: Fechamento de dívidas de refatoração e superfícies inativas (`plan-refactoring-debt-closure`)

## Status: RASCUNHO - decisões fechadas; 0 de 5 fases executadas

## Progresso

`░░░░░` **0%** - 0 de 5 fases

| Fase | Estado |
|---|---|
| Fase 1 - Decisões encerradas e documentação de resources | Pendente |
| Fase 2 - Marcadores antigos e código obsoleto | Pendente |
| Fase 3 - Superfícies protocolares inativas, logging e options v3 | Pendente |
| Fase 4 - Contrato explícito de `acr_values` | Pendente |
| Fase 5 - Aceites transversais e fechamento | Pendente |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluída, `%` e `X de 5`). Antes de fechar uma fase, confirme que decisões,
> critérios de aceite, testes e invariantes relacionados foram aplicados.

---

## Contexto

### Fontes verificadas

- [template-ai-implementation-plan.md](../references/template-plan/template-ai-implementation-plan.md) — shape,
  rastreabilidade e regras de manutenção deste plano.
- [plan-contexts-redesign.md](plan-contexts-redesign.md) — plano concluído que ainda descreve a herança de
  `IWith*` e a remoção de `Contexts.Items.Token` como adiadas.
- [plan-resources-redesign.md](plan-resources-redesign.md) — redesign concluído; `AllowedScopes` foi
  deliberadamente reaproveitado para scopes individuais, `AllowOfflineAccess` foi mantido como flag e os demais
  eixos são `AllowedIdentityScopes`, `AllowedResourceServers` e `AllowAllResourceServers`.
- [plan-data-storage-matrix.md](plan-data-storage-matrix.md) e [plan-data-macro.md](plan-data-macro.md) — ainda
  tratam a persistência do catálogo de resources/scopes como bloqueada pelo redesign já concluído.
- [plan-oidc-session-management.md](plan-oidc-session-management.md) — remove opções do check-session cookie e
  promove `ServerOptionsPayload`/`RealmOptionsPayload` para v2; este plano deve executar depois dele.
- [plan-oauth21-token-error-responses.md](plan-oauth21-token-error-responses.md) — caracteriza forma,
  autenticação e erros do token endpoint antes deste plano remover branches vazios.
- [plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md) — verificará metadata e logs depois que
  as superfícies inativas e o switch de logging forem removidos.
- [plan-reference-tokens-introspection.md](plan-reference-tokens-introspection.md) — reintroduzirá introspection
  somente junto da rota/pipeline reais; deve consumir a remoção da option feita aqui e a versão Configuration
  vigente depois dos planos intermediários, sem assumir v3.
- [plan-pushed-authorization-requests.md](plan-pushed-authorization-requests.md) — é o dono da separação entre a
  referência PAR e Request Object/JAR e removerá a metadata `request_parameter_supported=true` enquanto
  `ProcessRequestObject` continuar sem implementação.
- `git show f3478412 -- RoyalIdentity/Contracts/IClientSecretChecker.cs` — a alteração marcada no
  `[Redesign]` já ocorreu: `ParsedSecret?`/`ParseAsync` viraram `EvaluatedClient?`/`EvaluateClientAsync`.
- `RoyalIdentity/Extensions/AuthenticationPropertiesExtensions.cs`,
  `RoyalIdentity/Users/Contexts/AuthorizationContext.cs` e `RoyalIdentity/Utils/PkceHelper.cs` — declarações
  `[Obsolete]` sem callers externos localizados.
- `RoyalIdentity/Endpoints/TokenEndpoint.cs`, `RoyalIdentity/Handlers/DiscoveryHandler.cs` e
  `RoyalIdentity/Options/EndpointsOptions.cs` — Device Authorization e introspection podem ser anunciados por
  options, mas não há endpoints correspondentes mapeados; `DeviceCode` e `TokenExchange` têm branches vazios.
- `RoyalIdentity/Options/LoggingOptions.cs` e `RoyalIdentity/Extensions/LoggerExtensions.cs` —
  `UseLogService` só alcança três blocos `TODO` sem efeito.
- `RoyalIdentity/Contexts/Validators/AuthorizeMainValidator.cs` — `acr_values` é parseado e limitado por tamanho,
  mas o TODO sugere validar contra options de realm inexistentes.
- [OpenID Connect Core 1.0 incorporating errata set 2](https://openid.net/specs/openid-connect-core-1_0.html) —
  `acr_values` expressa valores preferidos em ordem; `acr` só representa contexto de autenticação efetivamente
  satisfeito.

### Estado atual do código (verificado em 2026-07-31)

- **Context redesign com falsos diferidos:** o plano está `COMPLETED`, mas sua barra, estado alvo, tabela e riscos
  ainda apresentam a Fase 2 e `Token` como trabalho futuro.
- **Wrapper de evento ativo:** `Contexts.Items.Token` obfusca o valor e é criado por handlers de code, access,
  identity e refresh token para eventos de emissão; removê-lo exigiria outro contrato de eventos.
- **Marcador já atendido:** `IClientSecretChecker` já retorna `EvaluatedClient?`; somente o atributo e o XML
  “parsed secret” ficaram desatualizados.
- **Obsoletos sem callers:** `AuthenticationPropertiesExtensions`, `AuthorizationContext.IdP` e
  `PkceHelper.GenerateCodeChallengeS256` não possuem consumidor localizado fora de sua própria declaração.
- **Metadata morta:** `EnableIntrospectionEndpoint=true` por default publica `introspection_endpoint` sem rota;
  Device Authorization pode publicar endpoint, alias mTLS e grant sem implementação.
- **Exceção JAR conhecida:** discovery também publica `request_parameter_supported=true`, embora
  `ProcessRequestObject` seja um stub. Este plano não antecipa a separação semântica de `request_uri`; a correção
  e seu guard pertencem ao plano posterior de PAR/JAR.
- **Alias mTLS de revocation incorreto:** `DiscoveryHandler` constrói os aliases mTLS de token, revocation,
  introspection e Device Authorization com `BuildMtlsTokenUrl`; remover as duas superfícies mortas elimina seus
  aliases incorretos, mas deixa revocation apontando para a rota de token. `BuildMtlsRevocationUrl` já existe e não
  possui caller.
- **Branches vazios:** `TokenEndpoint` intercepta `DeviceCode` e `TokenExchange`, produz `context=null` e impede
  que o fallback de `IExtensionsGrantsProvider` os trate.
- **Configuração sem efeito:** `LoggingOptions.UseLogService` tem setter interno e não possui configuração
  localizada; seus três branches não executam ação.
- **ACR sem política:** não existe catálogo realm-scoped nem `acr_values_supported`; claims `acr` só são
  propagadas quando já presentes no principal.
- **Documentação divergente:** `product.md`, `structure.md`, `AGENTS.md` e a matriz ainda chamam o modelo de
  resources de instável, embora o plano específico esteja concluído. `tech.md` acrescenta a única afirmação que
  inverte a causa: descreve resources/scopes como “deliberately volatile bridge pending their redesign”, quando o
  redesign terminou e apenas a persistência do catálogo continua diferida.
- **Bridge ainda volátil:** `ConfigurationResourceBridgeOptions`/`IConfigurationResourceSource` fornecem
  identity scopes e resource servers por realm sem persistir o catálogo.
- **Payloads atuais:** `ServerOptionsPayloadSerializer` e `RealmOptionsPayloadSerializer` estão em v1 no código
  atual; o plano de Session Management é o dono da passagem para v2.
- **Projeto de teste órfão:** `Tests.Endpoints` não está em `RoyalIdentity.sln` e seu único teste duplica
  `Tests.Pipelines/ServerEndpointTests.cs`; discovery já pertence a `Tests.Integration`.
- **Breaking changes permitidos:** não há clientes de produção; opções, payloads, APIs e defaults podem mudar
  diretamente quando o alvo fica mais correto.

### Lacunas, conflitos e restrições

- **Planos concorrendo pela versão de payload:** remover opções neste plano antes de Session Management faria
  ambos reivindicarem v2; a ordem e a versão precisam ser determinísticas.
- **Constante não é suporte:** manter constantes de protocolos futuros não autoriza anunciá-los no discovery.
- **Grant extension não cria endpoint:** `IExtensionsGrantsProvider` pode tratar um grant no token endpoint, mas
  não implementa o Device Authorization Endpoint do RFC 8628.
- **ACR é preferência, não promessa:** não inventar catálogo, policy ou erro para valores desconhecidos enquanto
  o produto não implementa métodos de autenticação que estabeleçam ACR.
- **Histórico não é backlog:** decisões canceladas devem permanecer explicadas, mas não contar como fase pendente.
- **Persistência de resources é plano próprio:** este plano desbloqueia documentação; não desenha entidades,
  migrations ou CRUD do catálogo.
- **Auditoria não nasce de um boolean morto:** remover `UseLogService` não autoriza criar sink, outbox ou store.
- **Localization permanece ativa:** os `[Redesign("Usar Resource")]` de `AccountOptions` não entram nesta limpeza.
- **Handoff de introspection:** a remoção de `EnableIntrospectionEndpoint` e seu payload v3 é temporária até o
  endpoint real. O plano de Reference Tokens/Introspection precisa restaurar gate, runtime e discovery no mesmo
  corte e versionar a partir dos payloads Configuration vigentes então, não da v3 deste plano.
- **Filtros vazios são falsos verdes:** `dotnet test --filter` retorna sucesso neste SDK mesmo quando nenhuma
  fixture corresponde; classes e filtros novos precisam ser nomeados explicitamente.

### Superfícies impactadas a mapear

- `.ai/plans/plan-contexts-redesign.md`, `AGENTS.md`, `CLAUDE.md`, foundations (`product.md`, `structure.md`,
  `tech.md`), matriz e macro — estado real das decisões.
- `RoyalIdentity/Contexts/Withs`, `RoyalIdentity/Contexts/Items/Token.cs` — desenho mantido, sem mudança de
  comportamento.
- `IClientSecretChecker`, `PkceHelper`, `AuthenticationPropertiesExtensions`, `AuthorizationContext` — remoção de
  marcadores e código morto.
- `EndpointsOptions`, `InputLengthRestrictions`, `DiscoveryHandler`, routes mTLS e `TokenEndpoint` — superfícies
  inexistentes e correção do alias vivo de revocation.
- `LoggingOptions`, `LoggerExtensions` — remoção do switch sem implementação.
- `IWithAcr`, `AuthorizeContext`, `AuthorizeMainValidator`, `AuthorizationContext`, claims/discovery — semântica e
  ordem de `acr_values`.
- serializers Configuration, seeds e `Tests.Storage` — payload v3 após Session Management.
- `Tests.Identity`, `Tests.Integration`, `Tests.Storage`, `Tests.Architecture`, `Tests.Pipelines` e o projeto órfão
  `Tests.Endpoints` — regressão, guardas e remoção de duplicata fora da solution.

---

## Objetivo

1. Encerrar como decisões finais a herança de `IWith*` e a permanência de `Contexts.Items.Token`.
2. Remover marcadores já atendidos e declarações obsoletas sem consumidores.
3. Impedir que discovery/options anunciem introspection ou Device Authorization inexistentes e corrigir o alias
   mTLS vivo de revocation.
4. Permitir que grants especiais registrados sejam resolvidos exclusivamente por `IExtensionsGrantsProvider`.
5. Remover `UseLogService` e os branches sem efeito, sem antecipar auditoria.
6. Fixar `acr_values` como preferência ordenada/distinta e limitada por tamanho, sem catálogo fictício.
7. Corrigir a documentação do redesign de resources e desbloquear um plano futuro de persistência do catálogo.
8. Remover o projeto de teste órfão `Tests.Endpoints` sem perder a cobertura já presente em `Tests.Pipelines`.

## Fora de escopo

- Implementar introspection, reference tokens, Device Authorization, Token Exchange ou novos extension grants.
- Redesenhar eventos, auditoria, sinks, outbox ou persistência de eventos.
- Remover a herança de `IWith*` ou o wrapper `Contexts.Items.Token`.
- Implementar localização; os marcadores de `AccountOptions` permanecem ativos.
- Implementar Check Session; pertence a `plan-oidc-session-management.md`.
- Implementar o catálogo persistente de resources/scopes ou criar seu plano executável neste corte.
- Implementar MFA, federação, autenticação por ACR ou publicar `acr_values_supported`.
- Alterar taxonomia OAuth 2.1 ou hardening RFC 9700 já pertencentes aos planos próprios.

---

## Decisões fechadas

- **DF1 — Um plano de fechamento:** agrupar somente dívidas pequenas, cancelamentos documentais e superfícies
  inativas verificadas; features futuras continuam em planos próprios. Fonte: decisão humana nesta discussão.
- **DF2 — Herança mantida:** `IWith*` continua herdando das interfaces atuais; a antiga Fase 2 deixa de ser
  “adiada” e passa a “cancelada por decisão”. Fonte: decisão humana nesta discussão.
- **DF3 — Wrapper mantido:** `Contexts.Items.Token` continua como envelope obfuscado dos eventos; remover seu
  `[Redesign]` e não alterar os eventos. Fonte: decisão humana nesta discussão + callers verificados.
- **DF4 — Eventos/auditoria sem dívida ativa:** uma evolução futura deve nascer de requisito e plano novos; não
  criar backlog obrigatório neste fechamento. Fonte: decisão humana nesta discussão.
- **DF5 — Marker de client secret já satisfeito:** `IClientSecretChecker` não muda de retorno; remover apenas
  `[Redesign]` e corrigir XML. Fonte: histórico Git `f3478412`.
- **DF6 — Obsoletos removidos diretamente:** apagar `AuthenticationPropertiesExtensions`,
  `AuthorizationContext.IdP` e `GenerateCodeChallengeS256` depois de confirmar zero callers; não criar shims.
  Fonte: inventário local + breaking changes permitidos.
- **DF7 — Discovery prova runtime e rota exata, com corte delimitado:** nesta fase, introspection e Device
  Authorization desaparecem até seus planos próprios, e o alias mTLS de revocation permanece usando
  `BuildMtlsRevocationUrl`, nunca a rota de token. A metadata falsa `request_parameter_supported=true` é uma
  exceção temporária explicitamente inventariada, não um precedente: sua remoção pertence ao plano de PAR, junto
  da separação entre referência PAR e Request Object/JAR. Fonte: decisão humana + RFC 9700 plan + código
  verificado de `DiscoveryHandler`/`Constants`.
- **DF8 — Extension grants pelo provider:** remover branches vazios de `DeviceCode` e `TokenExchange`; o branch
  default consulta `IExtensionsGrantsProvider`, e ausência continua `unsupported_grant_type`. Fonte: arquitetura
  atual do token endpoint.
- **DF9 — Options mortas removidas:** remover `EnableIntrospectionEndpoint`,
  `EnableDeviceAuthorizationEndpoint` e `InputLengthRestrictions.DeviceCode`; constantes protocolares sem efeito
  podem permanecer. Fonte: decisão humana + superfícies verificadas.
- **DF10 — Logging único:** remover `UseLogService` e blocos TODO; `ILogger` permanece o único destino deste
  corte. Fonte: decisão humana nesta discussão.
- **DF11 — `acr_values` como preferência ordenada:** substituir `HashSet<string>` por uma representação exposta
  como `IReadOnlyList<string>` em `IWithAcr`, `AuthorizeContext` e `AuthorizationContext`. O parse preserva a ordem
  de entrada, remove duplicatas mantendo a primeira ocorrência com comparação ordinal e aceita valores dentro do
  limite; não exigir catálogo, não anunciar suporte e não emitir `acr` não estabelecido. Fonte: análise aceita
  pelo mantenedor + OIDC Core.
- **DF12 — Modelo de resources concluído:** documentar `AllowedScopes` individual,
  `AllowedIdentityScopes`, `AllowedResourceServers`, `AllowAllResourceServers` e `AllowOfflineAccess` como desenho
  vigente. Fonte: `plan-resources-redesign.md` concluído.
- **DF13 — Persistência diferida em plano próprio:** reclassificar a bridge como transição de persistência
  desbloqueada; não criar entidades/migrations nem o plano do catálogo nesta execução. Fonte: decisão humana.
- **DF14 — Payload v3 sequencial:** executar a Fase 3 somente após Session Management concluir options v2; este
  plano remove as options restantes e grava v3 sem ler v1/v2. Fonte: ordem entre planos + breaking changes aceitos.
- **DF15 — Localization preservada:** não remover os três `[Redesign]` de mensagens em `AccountOptions`; a dívida
  já documentada permanece. Fonte: decisão humana.
- **DF16 — Realm e providers:** alterações de options/discovery devem cobrir dois realms; payload v3 deve manter
  paridade SQLite/PostgreSQL sem migration relacional desnecessária. Fonte: ADR-009 + baseline de storage.
- **DF17 — Topologia verificável de testes:** payloads ficam em `Tests.Storage`; discovery, aliases mTLS, extension
  grants e ACR ficam em fixtures nomeadas de `Tests.Integration`; ausência de superfícies removidas fica em
  `Tests.Architecture`. Nenhum comando obrigatório pode selecionar zero testes. A execução deste plano também
  promove essa regra para a orientação persistente do repositório. Fonte: infraestrutura e execução empírica dos
  filtros atuais.
- **DF18 — Handoff explícito de introspection:** este plano remove `EnableIntrospectionEndpoint` nos payloads v3.
  `plan-reference-tokens-introspection.md` restaura a option realm-scoped somente junto do endpoint real, com
  runtime/discovery coerentes, e incrementa cada payload Configuration afetado a partir de sua versão vigente
  depois de Localization/RFC 9700, sem assumir que ambos ainda estão em v3. Fonte: ordem do roadmap + DF7/DF14.
- **DF19 — Projeto de teste órfão removido:** excluir `Tests.Endpoints`, sem movê-lo para a solution, depois de
  confirmar que seu único teste é duplicado por `Tests.Pipelines`; discovery permanece em `Tests.Integration`.
  Fonte: inventário da solution durante a revisão de `plan-localization.md`.

---

## Histórico de decisões

**Revisão das refatorações abertas:**

- **Planos OAuth/OIDC/RFC:** o mantenedor confirmou que já estão documentados.
  - **Conclusão:** não duplicar seu escopo; DF1.
- **`IWith*` e eventos:** o mantenedor decidiu abortar as mudanças, manter eventos/auditoria como estão e apenas
  registrar que não serão executadas.
  - **Conclusão:** DF2-DF4.
- **Resources/scopes:** o mantenedor confirmou que o redesign terminou; pediu correção documental e um plano de
  persistência posterior, mas não sua criação neste momento.
  - **Conclusão:** DF12-DF13.
- **Outros sinais:** o mantenedor decidiu que metadata morta, logging sem efeito e `acr_values` precisam ser
  resolvidos.
  - **Conclusão:** DF7-DF11.
- **Alternativa descartada — implementar auditoria agora:** rejeitada; `UseLogService` não define contrato,
  durabilidade ou semântica.
  - **Conclusão:** DF4/DF10.
- **Alternativa descartada — criar options de ACR agora:** rejeitada; não existe método de autenticação/policy que
  possa satisfazê-las.
  - **Conclusão:** DF11.

**Revisão externa de 2026-07-31:**

- **Guardas e testes:** confirmou-se que o guard documental da Fase 1 casava com o próprio plano e que os filtros
  de `Tests.Identity` das Fases 3-4 retornavam sucesso sem selecionar teste.
  - **Conclusão:** guardas com paths explícitos e DF17.
- **Discovery mTLS:** confirmou-se que revocation, introspection e Device Authorization reutilizam incorretamente
  a rota mTLS de token; a remoção planejada não corrige o alias vivo de revocation.
  - **Conclusão:** DF7 e aceite específico na Fase 3.
- **Introspection futura:** o plano vizinho já conhecia esta limpeza e possuía regressão contra URL errada, mas não
  declarava a restauração da option nem a baseline dos payloads Configuration.
  - **Conclusão:** DF18 e handoff bilateral entre os planos.
- **Resources e guias:** `structure.md` também contém nomes de arquivos removidos, e `product.md` ainda descreve
  terminologia/pendências anteriores ao redesign. `CLAUDE.md` não repete hoje a afirmação obsoleta, mas deve ser
  verificado junto de `AGENTS.md` para evitar orientação divergente.
  - **Conclusão:** escopo ampliado da Fase 1.
- **Ordem de ACR:** `HashSet<string>` não oferece contrato de ordem; a tarefa condicional não poderia provar DF11.
  - **Conclusão:** troca fechada para lista ordenada e distinta por primeira ocorrência em DF11.
- **Revisão do plano de Localization:** confirmou-se que `Tests.Endpoints` está fora da solution e seu único teste
  duplica byte a byte, salvo namespace, o cenário de `Tests.Pipelines/ServerEndpointTests.cs`.
  - **Conclusão:** remover o projeto órfão nesta limpeza conforme DF19; Localization usa discovery integration.

---

## Design alvo

### Contratos e bordas

- `IWith*`: hierarquia vigente passa a ser decisão final documentada, não alvo intermediário.
- `Contexts.Items.Token`: envelope interno obfuscado dos eventos existentes, sem `[Redesign]`.
- `IClientSecretChecker.EvaluateClientAsync(...) -> Task<EvaluatedClient?>`: assinatura mantida e documentação
  corrigida.
- `IExtensionsGrantsProvider`: único seam para grants não implementados pelos três contextos core
  (`authorization_code`, `refresh_token`, `client_credentials`).
- `EndpointsOptions`: contém somente endpoints realmente implementados/mapeados ou já cobertos por plano ativo.
- `LoggingOptions`: conserva filtros sensíveis; não contém seleção de serviço inexistente.
- `acr_values`: input preferencial parseado em `IReadOnlyList<string>`, ordenado e distinto por primeira
  ocorrência; mantém limite de tamanho e não se torna policy nem claim por si.
- `IResourceStore`: contrato atual permanece; a troca do source volátil por EF pertence ao plano futuro.

### Modelo, dados e persistência

```text
ServerOptionsPayload v3
  remove Endpoints.EnableIntrospectionEndpoint
  remove Endpoints.EnableDeviceAuthorizationEndpoint
  remove InputLengthRestrictions.DeviceCode
  remove Logging.UseLogService

RealmOptionsPayload v3
  mesmas remoções
  preserva referência aos ServerOptions efetivos conforme materializador atual

Resource catalog
  modelo de domínio estável
  source ainda ConfigurationResourceBridgeOptions neste plano
  persistência EF diferida para plan-data-resource-catalog-storage futuro
```

Não criar migration relacional apenas pela remoção de propriedades JSON. Seeds, fixtures e payload coverage passam
a escrever v3; payloads v1/v2 falham fechados após o corte.

### Arquitetura alvo

```text
RoyalIdentity/
  Contexts/Withs/
    hierarquia mantida e documentada
  Contexts/Items/Token.cs
    envelope de eventos mantido
  Endpoints/TokenEndpoint.cs
    3 grants core + fallback IExtensionsGrantsProvider
  Handlers/DiscoveryHandler.cs
    introspection/device somente com runtime; exceção JAR inventariada para PAR
  Options/
    sem switches de features inexistentes ou logging sem efeito
  Contexts/Validators/AuthorizeMainValidator.cs
    acr_values: forma/tamanho, sem policy fictícia

RoyalIdentity.Storage.EntityFramework/
  Configuration/Materialization/
    options payload v3

.ai/
  decisões canceladas encerradas
  resources descritos como modelo concluído e persistência diferida
```

### Segurança, concorrência e confiabilidade

- Discovery nunca anuncia endpoint sem rota/runtime correspondente.
- Um grant registrado não é bloqueado por `case` vazio antes de chegar ao extension provider.
- Falha/ausência de extension grant continua respondendo `unsupported_grant_type` conforme plano OAuth 2.1.
- `acr` nunca é copiado de `acr_values`; somente contexto de autenticação estabelecido pode gerar a claim.
- Filtros de valores sensíveis de `LoggingOptions` permanecem; remover `UseLogService` não reduz redaction.
- Nenhuma remoção reintroduz valores de token em claro nos eventos.
- Options e testes permanecem realm-scoped.

### Compatibilidade, migração e rollout

- Ordem obrigatória: OAuth 2.1 Token Errors → OIDC Session Management (options v2) → este plano (options v3) →
  RFC 9700.
- Não fornecer shims para membros obsoletos ou options removidas.
- Reprovisionar configuração de desenvolvimento após o bump para v3; não materializar payload v1/v2.
- Manter constantes de protocolos futuros quando não produzem comportamento/metadata; removê-las somente se
  estiverem sem uso e fora de contratos compartilhados.
- Atualizar plans/foundations sem reescrever o registro histórico das fases já executadas.
- O futuro plano de Reference Tokens/Introspection não reutiliza a v3 como baseline fixa: ele parte das versões
  Configuration vigentes após os planos intermediários e restaura o gate somente com runtime real.

---

## Ordem de execução

1. **Fase 1 (decisões/documentação)** — elimina falsos pendentes e fecha o estado do modelo de resources.
2. **Fase 2 (código obsoleto)** — remove apenas símbolos comprovadamente mortos ou markers satisfeitos.
3. **Fase 3 (superfícies/options)** — depende de OIDC Session Management v2 e faz um único corte v3.
4. **Fase 4 (`acr_values`)** — fixa comportamento e testes sem introduzir policy.
5. **Fase 5 (aceites)** — confirma metadata, payload, realms, arquitetura e documentação.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

---

## Fase 1 - Decisões encerradas e documentação de resources

**Depende de:** DF1-DF4, DF12-DF13, DF15, DF17.

**Escopo:** `plan-contexts-redesign.md`, `plan-resources-redesign.md`, `plan-data-storage-matrix.md`,
`plan-data-macro.md`, `product.md`, `structure.md`, `tech.md`, `AGENTS.md`, `CLAUDE.md`, `redesign-todo.md`,
roadmap/backlog.

**O que/como:** transformar decisões adiadas em decisões finais mantidas; corrigir referências que ainda tratam o
modelo de resources como pendente; deixar somente a persistência do catálogo como diferida.

**Tarefas:**

- [ ] Atualizar o status/barra do plano de contexts removendo observações de trabalho adiado.
- [ ] Marcar a antiga Fase 2 de contexts como cancelada por decisão, sem alterar a contagem de fases executadas.
- [ ] Registrar `Contexts.Items.Token` como desenho mantido, não como remoção futura obrigatória.
- [ ] Remover do estado alvo/riscos as instruções para reavaliar automaticamente herança e eventos.
- [ ] Atualizar `product.md` com o modelo real de client/resources e tipos atuais (`Scope`, não `ApiScope`;
  `RequestedResources`, não `RequestedScopes`).
- [ ] Atualizar o mapa de arquivos e a seção de áreas instáveis de `structure.md`: remover `ApiScope.cs`,
  `ApiResource.cs` e `RequestedScopes.cs`; registrar `Scope.cs`, `ResourceServer.cs` e `RequestedResources.cs`.
- [ ] Atualizar `tech.md` trocando “resources/scopes remain a deliberately volatile bridge pending their redesign”
  por a causa correta: modelo de domínio concluído e somente a persistência do catálogo diferida.
- [ ] Atualizar `AGENTS.md` removendo o falso redesign pendente e verificar/ajustar `CLAUDE.md` para que os dois
  guias não contradigam o modelo concluído.
- [ ] Promover para `AGENTS.md` — e espelhar em `CLAUDE.md` quando aplicável — a regra de que comando filtrado de
  teste não pode fechar uma fase quando seleciona zero testes.
- [ ] Preservar a regra de que o catálogo ainda usa bridge volátil e não deve ganhar persistência fora do plano
  futuro.
- [ ] Atualizar a matriz/macro para trocar “bloqueado pelo redesign” por “modelo concluído; persistência diferida”.
- [ ] Adicionar nota pós-conclusão ao plano de resources sem reescrever resultados históricos.
- [ ] Atualizar `redesign-todo.md` preservando alterações locais e mantendo Localization como item aberto.
- [ ] Não criar ainda `plan-data-resource-catalog-storage.md`; registrar apenas o destino.

**Critérios de aceite:** nenhuma fonte ativa chama `AllowedScopes`/`AllowOfflineAccess` de modelo antigo; contexts
não têm fase “adiada” contando como dívida; `Token` é descrito como mantido; resources são estáveis no domínio e
voláteis apenas na persistência; `structure.md` lista somente os tipos/arquivos atuais; nenhuma foundation —
inclusive `tech.md` — atribui a volatilidade de resources a um redesign pendente; `AGENTS.md` e `CLAUDE.md`
não divergem sobre o estado do redesign e registram, quando aplicável, a regra contra filtros vazios; localization
continua aberta.

**Testes:**

```powershell
if (rg -n "Fase 2 adiada|remoção.*Token.*adiada" .ai/plans/plan-contexts-redesign.md) { throw "Decisão antiga de contexts encontrada." }
if (rg -n "scope hierarchy redesign in progress|Planned further redesign|to be replaced by .*AllowedResources|(?:AllowedScopes|AllowOfflineAccess).*pending|AllowedScopes.*need replacement|volatile bridge pending|pending (?:their|the) redesign|ApiScope\.cs|ApiResource\.cs|RequestedScopes\.cs|\*\*ApiScope\*\*|\*\*ApiResource|\*\*RequestedScopes\*\*" .ai/foundation AGENTS.md CLAUDE.md) { throw "Documentação ativa de resources obsoleta encontrada." }
rg -n "ConfigurationResourceBridgeOptions|persist.ncia.*diferid|Localization" .ai/foundation AGENTS.md CLAUDE.md redesign-todo.md
```

### Resultado da Fase 1

*a preencher*

---

## Fase 2 - Marcadores antigos e código obsoleto

**Depende de:** Fase 1, DF3, DF5-DF6, DF15, DF19.

**Escopo:** `IClientSecretChecker`, `Contexts.Items.Token`, `AuthenticationPropertiesExtensions`,
`AuthorizationContext`, `PkceHelper`, `Tests.Endpoints`, `Tests.Pipelines`, callers e testes.

**O que/como:** remover markers que já não representam trabalho e deletar membros sem consumidores, sem alterar
eventos, client authentication ou algoritmo PKCE vigente.

**Tarefas:**

- [ ] Confirmar por `rg` e compilação os callers de cada símbolo antes da remoção.
- [ ] Remover `[Redesign]` de `IClientSecretChecker.EvaluateClientAsync`.
- [ ] Corrigir XML de `IClientSecretChecker` para `EvaluatedClient`/avaliação de client.
- [ ] Remover `[Redesign]` de `Contexts.Items.Token` e documentar obfuscação/uso por eventos.
- [ ] Excluir `AuthenticationPropertiesExtensions.cs` inteiro.
- [ ] Remover `AuthorizationContext.IdP` e seu XML legado de HRD.
- [ ] Remover `PkceHelper.GenerateCodeChallengeS256`.
- [ ] Manter `GenerateS256CodeChallenge`, `GenerateStoredS256CodeChallengeHash` e
  `HashCodeChallengeForStorage` com seus significados distintos.
- [ ] Não remover os `[Redesign]` de Localization ou do check-session cookie pertencente ao plano OIDC.
- [ ] Confirmar que `Tests.Endpoints/ServerEndpointTests.cs` duplica o teste vigente de `Tests.Pipelines` e
  excluir o projeto órfão inteiro, sem adicioná-lo à solution.
- [ ] Adicionar guardas de arquitetura ou busca documental contra a reintrodução dos símbolos removidos.

**Critérios de aceite:** zero `[Obsolete]`/`[Redesign]` permanece nos símbolos fechados; nenhum shim delegador é
criado; eventos continuam recebendo tokens obfuscados; PKCE continua usando o helper correto em cada boundary;
`Tests.Endpoints` não existe e a cobertura equivalente continua em `Tests.Pipelines`; build e testes focados passam.

**Testes:**

```powershell
if (rg -n "AuthenticationPropertiesExtensions|GenerateCodeChallengeS256|public string\? IdP|\[Redesign.*Troca o tipo de retorno|\[Redesign.*desnecess" RoyalIdentity Tests.Identity Tests.Integration Tests.Storage Tests.Architecture) { throw "Símbolo ou marker removido foi encontrado." }
if (Test-Path Tests.Endpoints) { throw "Projeto de teste órfão Tests.Endpoints ainda existe." }
dotnet build RoyalIdentity/RoyalIdentity.csproj
dotnet test Tests.Pipelines --filter "FullyQualifiedName~EndpointHandler_Must_CreateResponse"
dotnet test Tests.Identity
dotnet test Tests.Architecture
```

### Resultado da Fase 2

*a preencher*

---

## Fase 3 - Superfícies protocolares inativas, logging e options v3

**Depende de:** Fases 1-2, conclusão de `plan-oauth21-token-error-responses.md`, conclusão de
`plan-oidc-session-management.md`, DF7-DF10, DF14, DF16-DF18.

**Escopo:** `EndpointsOptions`, `InputLengthRestrictions`, `LoggingOptions`, `DiscoveryHandler`,
`TokenEndpoint`, `LoggerExtensions`, serializers Configuration, seeds, fixtures, `Tests.Integration`,
`Tests.Storage`, `Tests.Architecture`.

**O que/como:** remover flags e metadata de features inexistentes, deixar grants especiais seguirem para o
extension provider, retirar logging sem efeito e fazer um único bump de options v2 para v3.

**Tarefas:**

- [ ] Confirmar que não existe endpoint mapeado de introspection ou Device Authorization.
- [ ] Remover `EnableIntrospectionEndpoint` e `EnableDeviceAuthorizationEndpoint` de `EndpointsOptions` e cópias.
- [ ] Remover `InputLengthRestrictions.DeviceCode`, já que a extensão proprietária valida seus parâmetros.
- [ ] Remover metadata, aliases mTLS e grant anunciado condicionados às options removidas.
- [ ] Corrigir o alias mTLS vivo de revocation para `BuildMtlsRevocationUrl`; não alterar o alias correto de token.
- [ ] Registrar no handoff de `plan-reference-tokens-introspection.md` que eventual alias de introspection usa
  `BuildMtlsIntrospectionUrl`, nunca a rota de token ou revocation.
- [ ] Preservar o handoff para `plan-pushed-authorization-requests.md`: não tratar
  `request_parameter_supported=true` como capacidade válida nem removê-lo isoladamente antes de separar
  referência PAR de Request Object/JAR.
- [ ] Remover os `case DeviceCode` e `case TokenExchange` vazios do token endpoint.
- [ ] Garantir que o branch default consulte `IExtensionsGrantsProvider` para ambos quando registrados.
- [ ] Garantir `unsupported_grant_type` exato quando nenhuma extensão possuir o grant.
- [ ] Remover `LoggingOptions.UseLogService` e sua cópia.
- [ ] Remover os três blocos TODO de `LoggerExtensions` sem alterar filtros/redaction.
- [ ] Promover `ServerOptionsPayloadSerializer` e `RealmOptionsPayloadSerializer` de v2 para v3.
- [ ] Atualizar seeds, fixtures, payload coverage e testes de versões não suportadas.
- [ ] Não criar migration relacional; documentar reprovisionamento/fail-closed de payload v1/v2.
- [ ] Testar dois realms com discovery sem endpoints mortos.
- [ ] Testar extension grant registrado e não registrado sem duplicar a taxonomia do plano OAuth 2.1.
- [ ] Estender `Tests.Integration/Endpoints/DiscoveryTests.cs` com omissões exatas e alias mTLS de revocation.
- [ ] Criar `Tests.Integration/Endpoints/ExtensionGrantRoutingTests.cs` para grants registrados/não registrados.
- [ ] Criar `Tests.Architecture/RefactoringDebtBoundaryTests.cs` para ausência de options/branches/markers removidos
  e preservação dos filtros sensíveis de logging.

**Critérios de aceite:** discovery não contém introspection/Device Authorization nem os anuncia em mTLS/grants;
o alias mTLS de revocation aponta exatamente para a rota mTLS de revocation e o de token permanece correto;
options e JSON v3 não contêm as propriedades removidas; extension grants alcançam o provider; ausência responde
conforme OAuth 2.1; não há branch de logging sem efeito; filtros sensíveis permanecem; payloads antigos falham
fechados; cada filtro obrigatório seleciona ao menos um teste.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~ConfigurationModelPayloadTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~DiscoveryTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~ExtensionGrantRoutingTests"
dotnet test Tests.Architecture --filter "FullyQualifiedName~RefactoringDebtBoundaryTests"
```

### Resultado da Fase 3

*a preencher*

---

## Fase 4 - Contrato explícito de `acr_values`

**Depende de:** Fase 3, DF6, DF11, DF17.

**Escopo:** `IWithAcr`, `AuthorizeContext`, `AuthorizeMainValidator`, `AuthorizationContext`, discovery/claims,
`Tests.Integration`, foundations.

**O que/como:** substituir o TODO por contrato intencional: parse/tamanho e passagem como preferência, sem
catálogo, rejeição de valor desconhecido, HRD proprietário ou claim não comprovada.

**Tarefas:**

- [ ] Trocar `AcrValues` de `HashSet<string>` para `IReadOnlyList<string>` em `IWithAcr`, `AuthorizeContext` e
  `AuthorizationContext`, atualizando os consumidores no mesmo corte.
- [ ] Fazer o parse preservar ordem de entrada e remover duplicatas pela primeira ocorrência com comparação
  ordinal/case-sensitive.
- [ ] Documentar `AuthorizeContext.AcrValues` como preferências recebidas em ordem.
- [ ] Manter rejeição de `acr_values` acima de `InputLengthRestrictions.AcrValues`.
- [ ] Remover o TODO de validação contra future realm options.
- [ ] Não interpretar prefixo proprietário `idp:` nem recriar `AuthorizationContext.IdP`.
- [ ] Não adicionar `SupportedAcrValues`, policy, validator DI ou options de realm.
- [ ] Garantir que discovery não publique `acr_values_supported`.
- [ ] Garantir que `DefaultTokenClaimsService` só emita `acr` já estabelecido no principal.
- [ ] Criar `Tests.Integration/Endpoints/AcrValuesTests.cs`.
- [ ] Testar valor único, múltiplas preferências em ordem, duplicata preservando a primeira ocorrência,
  desconhecido dentro do limite e excesso de tamanho.
- [ ] Testar que `acr_values` recebido não produz automaticamente claim `acr`.
- [ ] Documentar o handoff para futuros planos de MFA/federação.

**Critérios de aceite:** valores desconhecidos dentro do limite não falham por catálogo inexistente; excesso
falha como hoje; a representação pública não é `HashSet` e a ordem distinta por primeira ocorrência chega ao
boundary de interação; discovery não promete ACRs; tokens não contêm `acr` derivado do request; não resta TODO
sobre options futuras; cada filtro obrigatório seleciona ao menos um teste.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~AcrValuesTests"
```

### Resultado da Fase 4

*a preencher*

---

## Fase 5 - Aceites transversais e fechamento

**Depende de:** Fases 1-4, DF1-DF19.

**Escopo:** solution inteira, roadmap, plans relacionados, foundations, arquitetura, documentação.

**O que/como:** executar guardas e suíte ampla; reconciliar planos sem duplicar escopo; fechar progresso,
rastreabilidade e diferidos.

**Tarefas:**

- [ ] Executar busca final por markers, TODOs, options e metadata removidos.
- [ ] Confirmar que os únicos `[Redesign]` restantes têm destino ativo documentado.
- [ ] Confirmar que o plano OIDC continua dono de check-session e options v2.
- [ ] Confirmar que o plano RFC 9700 verifica metadata/logging já simplificados sem reintroduzir options.
- [ ] Confirmar que o plano OAuth 2.1 continua dono dos códigos/status/headers de token errors.
- [ ] Confirmar que `plan-reference-tokens-introspection.md` consome a remoção da option/payload v3, restaura o
  gate somente com runtime real e não reutiliza `BuildMtlsTokenUrl` para introspection.
- [ ] Confirmar que `plan-pushed-authorization-requests.md` possui nominalmente a remoção da metadata JAR falsa e
  seu guard depois de separar a referência PAR.
- [ ] Atualizar roadmap com o estado real deste plano quando sua execução terminar.
- [ ] Registrar a futura persistência de resources sem criar o plano antes da decisão do mantenedor.
- [ ] Executar build e suíte integral.
- [ ] Atualizar Status, Progresso, resultados das fases e matriz.

**Critérios de aceite:** nenhuma referência ativa contradiz as decisões; nenhuma metadata aponta para endpoint
inexistente; todos os payloads/options usam v3; resources são descritos como modelo concluído/bridge transitória;
testes integrais passam; todos os comandos filtrados obrigatórios selecionam testes; o plano não deixa pergunta ou
tarefa implícita.

**Testes:**

```powershell
rg -n "\[Redesign|\[Obsolete|TODO:" RoyalIdentity
if (rg -n "EnableIntrospectionEndpoint|EnableDeviceAuthorizationEndpoint|UseLogService|InputLengthRestrictions\.DeviceCode" RoyalIdentity Tests.Identity Tests.Integration Tests.Storage Tests.Architecture) { throw "Superfície removida foi encontrada." }
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

### Resultado da Fase 5

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(es) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| Encerrar falsos redesigns | 1-2 | DF2-DF6 | contexts/token/client markers coerentes | `rg` + build + Architecture |
| Corrigir resources docs | 1, 5 | DF12-DF13 | modelo concluído; só persistência diferida | buscas documentais |
| Remover metadata morta | 3, 5 | DF7, DF9, DF16-DF18 | discovery sem endpoints/grants inexistentes; revocation mTLS exata | Discovery multi-realm |
| Preservar extension grants | 3 | DF8 | provider alcançado; ausência padronizada | TokenEndpoint/Integration |
| Remover logging sem efeito | 3 | DF10, DF14 | option/branches ausentes; redaction preservada | Logging + payload v3 |
| Fechar `acr_values` | 4-5 | DF6, DF11, DF17 | lista ordenada/distinta sem claim/metadata fictícia | AcrValues integration |
| Preservar planos ativos | 3, 5 | DF1, DF14-DF15 | sem duplicação/version conflict | revisão + suíte integral |
| Testes sem falso verde | 1, 3-5 | DF17 | classes nomeadas; nenhum filtro vazio | Integration + Storage + Architecture |
| Remover projeto de teste órfão | 2, 5 | DF19 | duplicata removida; cobertura preservada na solution | `Tests.Pipelines` + `Test-Path` |

---

## Invariantes a preservar

1. Realm continua a fronteira de options, discovery, clients e resources.
2. `IWith*` e os decorators mantêm sua hierarquia/constraints atuais.
3. Eventos continuam recebendo valores de token obfuscados.
4. Os três grants core mantêm seus contextos; demais grants pertencem ao extension provider.
5. Introspection e Device Authorization não afirmam suporte sem rota/runtime; a metadata JAR falsa permanece
   somente como exceção temporária inventariada e entregue nominalmente ao plano PAR.
6. Filtros de secrets/assertions/tokens nos logs não são reduzidos.
7. `acr_values` não autentica, não seleciona IdP e não cria claim por si.
8. Authorization codes continuam single-use; PKCE usa os helpers não obsoletos.
9. O core não passa a depender de providers, hosts, UI ou módulos.
10. Resources permanecem realm-scoped e voláteis até o plano de persistência próprio.
11. Localization e Check Session não são apagados como dívida por esta limpeza.
12. Payload v3 só é introduzido depois que v2 de Session Management estiver concluído.
13. Alias mTLS de endpoint vivo usa sempre seu próprio route builder; revocation nunca aponta para token.
14. `acr_values` preserva ordem de preferência e unicidade pela primeira ocorrência; não usa `HashSet` na borda.
15. Nenhum comando filtrado obrigatório pode fechar fase selecionando zero testes.
16. Discovery permanece em `Tests.Integration`; `Tests.Endpoints` não volta à solution nem ao filesystem.

---

## Critérios globais de conclusão

- O plano de contexts não contém trabalho adiado tratado como refatoração futura obrigatória.
- Markers antigos e símbolos obsoletos listados em DF5/DF6 não existem.
- Discovery omite introspection e Device Authorization em todos os realms.
- Extension grants registrados alcançam `IExtensionsGrantsProvider`.
- `UseLogService` e seus blocos vazios não existem.
- Options Configuration são v3 e não contêm propriedades removidas.
- `acr_values` tem comportamento e testes explícitos, sem catálogo/metadata/claim fictícia.
- Foundations/matriz descrevem corretamente o redesign concluído de resources.
- Alias mTLS de revocation aponta para a rota correta e o handoff de introspection parte da baseline Configuration
  vigente nos planos posteriores.
- Classes de teste nomeadas existem e nenhum filtro obrigatório seleciona zero testes.
- `Tests.Endpoints` foi removido e seu cenário equivalente permanece coberto por `Tests.Pipelines`.
- `dotnet build RoyalIdentity.sln` passa.
- `dotnet test RoyalIdentity.sln` passa.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| Payload version conflita com OIDC | este plano usa v2 ou roda primeiro | serializers/fixtures incompatíveis | DF14 + dependência explícita | Aberto |
| Extension grant muda erro | remoção do `case` alcança provider inesperado | comportamento diferente | concluir OAuth 2.1 antes + testes registrado/ausente | Aberto |
| Metadata futura some sem registro | consumidor esperava feature inexistente | descoberta deixa de anunciar falso suporte | breaking aceito; plano futuro reintroduz conjunto completo | Aceito |
| Remoção obsoleta quebra caller oculto | reflection/source externo | build/consumer falha | `rg`, build integral; sem consumidores de produção | Aceito |
| ACR perde ordem | parse/lista não preserva preferência | interação futura escolhe valor errado | DF11 + teste de ordem/duplicata | Aberto |
| Logging perde redaction | limpeza remove filtro junto do switch | segredo em log | testes capturando valores sensíveis + RFC 9700 | Aberto |
| Histórico é reescrito | docs apagam motivo das decisões | perda de rastreabilidade | adendos e cancelamento explícito, sem apagar resultados | Aberto |
| Persistência entra por acidente | executor troca bridge nesta limpeza | escopo/storage sem plano | DF13 + guard documental/arquitetural | Aberto |
| Alias mTLS sobrevivente aponta para token | remoção apaga branches mortos, mas não corrige revocation | metadata conduz ao endpoint errado | DF7 + teste exato de URL | Aberto |
| Filtro executa zero testes | classe planejada não existe ou filtro é amplo/incorreto | fase fecha em falso verde | DF17 + fixtures e filtros nomeados | Aberto |
| Introspection futura assume payload v3 | plano ignora Localization/planos intermediários | colisão de versão ou option perdida | DF18 + handoff bilateral | Aberto |
| Exceção JAR vira precedente | DF7 é lida como se metadata falsa fosse aceitável em geral | novas capabilities sem runtime | delimitação de DF7 + handoff nominal ao plano PAR | Aberto |

---

## Diferidos e backlog

- Persistência EF do catálogo de resources/scopes — destino futuro:
  `plan-data-resource-catalog-storage.md`, a criar somente quando autorizado.
- Localization de UI e mensagens de `AccountOptions` — destino: plano específico de localização.
- Introspection + reference tokens — destino:
  [plan-reference-tokens-introspection.md](plan-reference-tokens-introspection.md), que reintroduz
  `EnableIntrospectionEndpoint` somente com endpoint real e parte das versões Configuration vigentes naquele
  momento; não copiar o alias mTLS incorreto removido/corrigido aqui.
- Device Authorization e Token Exchange — destino: planos/extensões próprios quando priorizados.
- Metadata de Request Object/JAR anunciada sem implementação — destino:
  [plan-pushed-authorization-requests.md](plan-pushed-authorization-requests.md), que separa a URN PAR antes de
  omitir `request_parameter_supported` e `request_uri_parameter_supported` falsos.
- MFA/federação e catálogo realm-scoped de ACR — destino:
  `plan-auth-methods-mfa-passwordless.md`/`plan-federation-identity-brokering.md`.
- Evolução de eventos, auditoria durável e outbox — destino: nova necessidade e novo plano; não é dívida ativa.

---

## Referências

- [template-ai-implementation-plan.md](../references/template-plan/template-ai-implementation-plan.md).
- [plan-contexts-redesign.md](plan-contexts-redesign.md).
- [plan-resources-redesign.md](plan-resources-redesign.md).
- [plan-data-storage-matrix.md](plan-data-storage-matrix.md).
- [plan-data-macro.md](plan-data-macro.md).
- [plan-oauth21-token-error-responses.md](plan-oauth21-token-error-responses.md).
- [plan-oidc-session-management.md](plan-oidc-session-management.md).
- [plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md).
- [plan-reference-tokens-introspection.md](plan-reference-tokens-introspection.md).
- [plan-pushed-authorization-requests.md](plan-pushed-authorization-requests.md).
- [plans-roadmap-02.md](plans-roadmap-02.md).
- [OpenID Connect Core 1.0 incorporating errata set 2](https://openid.net/specs/openid-connect-core-1_0.html).
- `../../adrs/ADR-009.md`, `../../adrs/ADR-010.md`, `../../adrs/ADR-014.md`.
- `RoyalIdentity/Contracts/IClientSecretChecker.cs`.
- `RoyalIdentity/Contexts/Items/Token.cs`.
- `RoyalIdentity/Endpoints/TokenEndpoint.cs`.
- `RoyalIdentity/Handlers/DiscoveryHandler.cs`.
- `RoyalIdentity/Options/EndpointsOptions.cs`.
- `RoyalIdentity/Options/LoggingOptions.cs`.
- `RoyalIdentity/Contexts/Validators/AuthorizeMainValidator.cs`.
