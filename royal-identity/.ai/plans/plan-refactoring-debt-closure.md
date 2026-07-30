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

### Estado atual do código (verificado em 2026-07-30)

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
- **Branches vazios:** `TokenEndpoint` intercepta `DeviceCode` e `TokenExchange`, produz `context=null` e impede
  que o fallback de `IExtensionsGrantsProvider` os trate.
- **Configuração sem efeito:** `LoggingOptions.UseLogService` tem setter interno e não possui configuração
  localizada; seus três branches não executam ação.
- **ACR sem política:** não existe catálogo realm-scoped nem `acr_values_supported`; claims `acr` só são
  propagadas quando já presentes no principal.
- **Documentação divergente:** `product.md`, `structure.md`, `AGENTS.md` e a matriz ainda chamam o modelo de
  resources de instável, embora o plano específico esteja concluído.
- **Bridge ainda volátil:** `ConfigurationResourceBridgeOptions`/`IConfigurationResourceSource` fornecem
  identity scopes e resource servers por realm sem persistir o catálogo.
- **Payloads atuais:** `ServerOptionsPayloadSerializer` e `RealmOptionsPayloadSerializer` estão em v1 no código
  atual; o plano de Session Management é o dono da passagem para v2.
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

### Superfícies impactadas a mapear

- `.ai/plans/plan-contexts-redesign.md`, `AGENTS.md`, foundations, matriz e macro — estado real das decisões.
- `RoyalIdentity/Contexts/Withs`, `RoyalIdentity/Contexts/Items/Token.cs` — desenho mantido, sem mudança de
  comportamento.
- `IClientSecretChecker`, `PkceHelper`, `AuthenticationPropertiesExtensions`, `AuthorizationContext` — remoção de
  marcadores e código morto.
- `EndpointsOptions`, `InputLengthRestrictions`, `DiscoveryHandler`, `TokenEndpoint` — superfícies inexistentes.
- `LoggingOptions`, `LoggerExtensions` — remoção do switch sem implementação.
- `AuthorizeMainValidator`, `AuthorizationContext`, claims/discovery — semântica de `acr_values`.
- serializers Configuration, seeds e `Tests.Storage` — payload v3 após Session Management.
- `Tests.Identity`, `Tests.Integration`, `Tests.Storage`, `Tests.Architecture` — regressão e guardas.

---

## Objetivo

1. Encerrar como decisões finais a herança de `IWith*` e a permanência de `Contexts.Items.Token`.
2. Remover marcadores já atendidos e declarações obsoletas sem consumidores.
3. Impedir que discovery/options anunciem introspection ou Device Authorization inexistentes.
4. Permitir que grants especiais registrados sejam resolvidos exclusivamente por `IExtensionsGrantsProvider`.
5. Remover `UseLogService` e os branches sem efeito, sem antecipar auditoria.
6. Fixar o comportamento atual de `acr_values` como preferência limitada por tamanho, sem catálogo fictício.
7. Corrigir a documentação do redesign de resources e desbloquear um plano futuro de persistência do catálogo.

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
- **DF7 — Discovery prova runtime:** endpoint/metadata/grant só são anunciados quando há implementação alcançável;
  introspection e Device Authorization desaparecem até seus planos próprios. Fonte: decisão humana + RFC 9700
  plan, decisão de metadata coerente com o runtime.
- **DF8 — Extension grants pelo provider:** remover branches vazios de `DeviceCode` e `TokenExchange`; o branch
  default consulta `IExtensionsGrantsProvider`, e ausência continua `unsupported_grant_type`. Fonte: arquitetura
  atual do token endpoint.
- **DF9 — Options mortas removidas:** remover `EnableIntrospectionEndpoint`,
  `EnableDeviceAuthorizationEndpoint` e `InputLengthRestrictions.DeviceCode`; constantes protocolares sem efeito
  podem permanecer. Fonte: decisão humana + superfícies verificadas.
- **DF10 — Logging único:** remover `UseLogService` e blocos TODO; `ILogger` permanece o único destino deste
  corte. Fonte: decisão humana nesta discussão.
- **DF11 — `acr_values` como preferência:** aceitar valores dentro do limite, preservar ordem de entrada no
  boundary que a consome, não exigir catálogo, não anunciar suporte e não emitir `acr` não estabelecido.
  Fonte: análise aceita pelo mantenedor + OIDC Core.
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
- `acr_values`: input preferencial parseado, com limite de tamanho; não se torna policy nem claim por si.
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
    somente metadata alcançável
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

**Depende de:** DF1-DF4, DF12-DF13, DF15.

**Escopo:** `plan-contexts-redesign.md`, `plan-resources-redesign.md`, `plan-data-storage-matrix.md`,
`plan-data-macro.md`, `product.md`, `structure.md`, `AGENTS.md`, `redesign-todo.md`, roadmap/backlog.

**O que/como:** transformar decisões adiadas em decisões finais mantidas; corrigir referências que ainda tratam o
modelo de resources como pendente; deixar somente a persistência do catálogo como diferida.

**Tarefas:**

- [ ] Atualizar o status/barra do plano de contexts removendo observações de trabalho adiado.
- [ ] Marcar a antiga Fase 2 de contexts como cancelada por decisão, sem alterar a contagem de fases executadas.
- [ ] Registrar `Contexts.Items.Token` como desenho mantido, não como remoção futura obrigatória.
- [ ] Remover do estado alvo/riscos as instruções para reavaliar automaticamente herança e eventos.
- [ ] Atualizar `product.md` com o modelo real de client/resources e tipos atuais (`Scope`, não `ApiScope`;
  `RequestedResources`, não `RequestedScopes`).
- [ ] Atualizar `structure.md` e `AGENTS.md` removendo o falso “scope hierarchy redesign in progress”.
- [ ] Preservar a regra de que o catálogo ainda usa bridge volátil e não deve ganhar persistência fora do plano
  futuro.
- [ ] Atualizar a matriz/macro para trocar “bloqueado pelo redesign” por “modelo concluído; persistência diferida”.
- [ ] Adicionar nota pós-conclusão ao plano de resources sem reescrever resultados históricos.
- [ ] Atualizar `redesign-todo.md` preservando alterações locais e mantendo Localization como item aberto.
- [ ] Não criar ainda `plan-data-resource-catalog-storage.md`; registrar apenas o destino.

**Critérios de aceite:** nenhuma fonte ativa chama `AllowedScopes`/`AllowOfflineAccess` de modelo antigo; contexts
não têm fase “adiada” contando como dívida; `Token` é descrito como mantido; resources são estáveis no domínio e
voláteis apenas na persistência; localization continua aberta.

**Testes:**

```powershell
if (rg -n "Fase 2 adiada|remoção.*Token.*adiada|scope hierarchy redesign in progress|AllowedScopes.*pending refactor" .ai AGENTS.md) { throw "Documentação de redesign obsoleta encontrada." }
rg -n "ConfigurationResourceBridgeOptions|persist.ncia.*diferid|Localization" .ai AGENTS.md redesign-todo.md
```

### Resultado da Fase 1

*a preencher*

---

## Fase 2 - Marcadores antigos e código obsoleto

**Depende de:** Fase 1, DF3, DF5-DF6, DF15.

**Escopo:** `IClientSecretChecker`, `Contexts.Items.Token`, `AuthenticationPropertiesExtensions`,
`AuthorizationContext`, `PkceHelper`, callers e testes.

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
- [ ] Adicionar guardas de arquitetura ou busca documental contra a reintrodução dos símbolos removidos.

**Critérios de aceite:** zero `[Obsolete]`/`[Redesign]` permanece nos símbolos fechados; nenhum shim delegador é
criado; eventos continuam recebendo tokens obfuscados; PKCE continua usando o helper correto em cada boundary;
build e testes focados passam.

**Testes:**

```powershell
if (rg -n "AuthenticationPropertiesExtensions|GenerateCodeChallengeS256|public string\? IdP|\[Redesign.*Troca o tipo de retorno|\[Redesign.*desnecess" RoyalIdentity Tests.Identity Tests.Integration Tests.Storage Tests.Architecture) { throw "Símbolo ou marker removido foi encontrado." }
dotnet build RoyalIdentity/RoyalIdentity.csproj
dotnet test Tests.Identity
dotnet test Tests.Architecture
```

### Resultado da Fase 2

*a preencher*

---

## Fase 3 - Superfícies protocolares inativas, logging e options v3

**Depende de:** Fases 1-2, conclusão de `plan-oauth21-token-error-responses.md`, conclusão de
`plan-oidc-session-management.md`, DF7-DF10, DF14, DF16.

**Escopo:** `EndpointsOptions`, `InputLengthRestrictions`, `LoggingOptions`, `DiscoveryHandler`,
`TokenEndpoint`, `LoggerExtensions`, serializers Configuration, seeds, fixtures, `Tests.Identity`,
`Tests.Integration`, `Tests.Storage`.

**O que/como:** remover flags e metadata de features inexistentes, deixar grants especiais seguirem para o
extension provider, retirar logging sem efeito e fazer um único bump de options v2 para v3.

**Tarefas:**

- [ ] Confirmar que não existe endpoint mapeado de introspection ou Device Authorization.
- [ ] Remover `EnableIntrospectionEndpoint` e `EnableDeviceAuthorizationEndpoint` de `EndpointsOptions` e cópias.
- [ ] Remover `InputLengthRestrictions.DeviceCode`, já que a extensão proprietária valida seus parâmetros.
- [ ] Remover metadata, aliases mTLS e grant anunciado condicionados às options removidas.
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

**Critérios de aceite:** discovery não contém introspection/Device Authorization nem os anuncia em mTLS/grants;
options e JSON v3 não contêm as propriedades removidas; extension grants alcançam o provider; ausência responde
conforme OAuth 2.1; não há branch de logging sem efeito; filtros sensíveis permanecem; payloads antigos falham
fechados.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~ConfigurationModelPayload|FullyQualifiedName~ConfigurationPayload"
dotnet test Tests.Identity --filter "FullyQualifiedName~Discovery|FullyQualifiedName~TokenEndpoint|FullyQualifiedName~ExtensionGrant|FullyQualifiedName~Logging"
dotnet test Tests.Integration --filter "FullyQualifiedName~Discovery|FullyQualifiedName~TokenError|FullyQualifiedName~Realm"
```

### Resultado da Fase 3

*a preencher*

---

## Fase 4 - Contrato explícito de `acr_values`

**Depende de:** Fase 2, DF6, DF11.

**Escopo:** `AuthorizeContext`, `AuthorizeMainValidator`, `AuthorizationContext`, discovery/claims,
`Tests.Identity`, `Tests.Integration`, foundations.

**O que/como:** substituir o TODO por contrato intencional: parse/tamanho e passagem como preferência, sem
catálogo, rejeição de valor desconhecido, HRD proprietário ou claim não comprovada.

**Tarefas:**

- [ ] Documentar `AuthorizeContext.AcrValues` como preferências recebidas em ordem.
- [ ] Confirmar que a coleção atual preserva ordem; trocar o tipo somente se o `HashSet` não satisfizer o contrato
  e atualizar consumidores/testes no mesmo corte.
- [ ] Manter rejeição de `acr_values` acima de `InputLengthRestrictions.AcrValues`.
- [ ] Remover o TODO de validação contra future realm options.
- [ ] Não interpretar prefixo proprietário `idp:` nem recriar `AuthorizationContext.IdP`.
- [ ] Não adicionar `SupportedAcrValues`, policy, validator DI ou options de realm.
- [ ] Garantir que discovery não publique `acr_values_supported`.
- [ ] Garantir que `DefaultTokenClaimsService` só emita `acr` já estabelecido no principal.
- [ ] Testar valor único, múltiplas preferências, desconhecido dentro do limite e excesso de tamanho.
- [ ] Testar que `acr_values` recebido não produz automaticamente claim `acr`.
- [ ] Documentar o handoff para futuros planos de MFA/federação.

**Critérios de aceite:** valores desconhecidos dentro do limite não falham por catálogo inexistente; excesso
falha como hoje; ordem chega ao boundary de interação; discovery não promete ACRs; tokens não contêm `acr`
derivado do request; não resta TODO sobre options futuras.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~Acr|FullyQualifiedName~AuthorizeMain"
dotnet test Tests.Integration --filter "FullyQualifiedName~Acr|FullyQualifiedName~Authorize|FullyQualifiedName~Discovery"
```

### Resultado da Fase 4

*a preencher*

---

## Fase 5 - Aceites transversais e fechamento

**Depende de:** Fases 1-4, DF1-DF16.

**Escopo:** solution inteira, roadmap, plans relacionados, foundations, arquitetura, documentação.

**O que/como:** executar guardas e suíte ampla; reconciliar planos sem duplicar escopo; fechar progresso,
rastreabilidade e diferidos.

**Tarefas:**

- [ ] Executar busca final por markers, TODOs, options e metadata removidos.
- [ ] Confirmar que os únicos `[Redesign]` restantes têm destino ativo documentado.
- [ ] Confirmar que o plano OIDC continua dono de check-session e options v2.
- [ ] Confirmar que o plano RFC 9700 verifica metadata/logging já simplificados sem reintroduzir options.
- [ ] Confirmar que o plano OAuth 2.1 continua dono dos códigos/status/headers de token errors.
- [ ] Atualizar roadmap com o estado real deste plano quando sua execução terminar.
- [ ] Registrar a futura persistência de resources sem criar o plano antes da decisão do mantenedor.
- [ ] Executar build e suíte integral.
- [ ] Atualizar Status, Progresso, resultados das fases e matriz.

**Critérios de aceite:** nenhuma referência ativa contradiz as decisões; nenhuma metadata aponta para endpoint
inexistente; todos os payloads/options usam v3; resources são descritos como modelo concluído/bridge transitória;
testes integrais passam; o plano não deixa pergunta ou tarefa implícita.

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
| Remover metadata morta | 3, 5 | DF7, DF9, DF16 | discovery sem endpoints/grants inexistentes | Discovery multi-realm |
| Preservar extension grants | 3 | DF8 | provider alcançado; ausência padronizada | TokenEndpoint/Integration |
| Remover logging sem efeito | 3 | DF10, DF14 | option/branches ausentes; redaction preservada | Logging + payload v3 |
| Fechar `acr_values` | 4-5 | DF6, DF11 | preferência sem claim/metadata fictícia | ACR unit/integration |
| Preservar planos ativos | 3, 5 | DF1, DF14-DF15 | sem duplicação/version conflict | revisão + suíte integral |

---

## Invariantes a preservar

1. Realm continua a fronteira de options, discovery, clients e resources.
2. `IWith*` e os decorators mantêm sua hierarquia/constraints atuais.
3. Eventos continuam recebendo valores de token obfuscados.
4. Os três grants core mantêm seus contextos; demais grants pertencem ao extension provider.
5. Metadata nunca afirma suporte sem rota e runtime funcionais.
6. Filtros de secrets/assertions/tokens nos logs não são reduzidos.
7. `acr_values` não autentica, não seleciona IdP e não cria claim por si.
8. Authorization codes continuam single-use; PKCE usa os helpers não obsoletos.
9. O core não passa a depender de providers, hosts, UI ou módulos.
10. Resources permanecem realm-scoped e voláteis até o plano de persistência próprio.
11. Localization e Check Session não são apagados como dívida por esta limpeza.
12. Payload v3 só é introduzido depois que v2 de Session Management estiver concluído.

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
| ACR perde ordem | `HashSet`/parse não preserva preferência | interação futura escolhe valor errado | teste de ordem; trocar representação se necessário | Aberto |
| Logging perde redaction | limpeza remove filtro junto do switch | segredo em log | testes capturando valores sensíveis + RFC 9700 | Aberto |
| Histórico é reescrito | docs apagam motivo das decisões | perda de rastreabilidade | adendos e cancelamento explícito, sem apagar resultados | Aberto |
| Persistência entra por acidente | executor troca bridge nesta limpeza | escopo/storage sem plano | DF13 + guard documental/arquitetural | Aberto |

---

## Diferidos e backlog

- Persistência EF do catálogo de resources/scopes — destino futuro:
  `plan-data-resource-catalog-storage.md`, a criar somente quando autorizado.
- Localization de UI e mensagens de `AccountOptions` — destino: plano específico de localização.
- Introspection + reference tokens — destino: plano próprio relacionado ao item de backlog existente.
- Device Authorization e Token Exchange — destino: planos/extensões próprios quando priorizados.
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
