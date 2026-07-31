# Plan: Localization realm-scoped da UI (`plan-localization`)

## Status: RASCUNHO - decisões fechadas; 0 de 7 fases executadas

## Progresso

`░░░░░░░` **0%** - 0 de 7 fases

| Fase | Estado |
|---|---|
| Fase 1 - Contrato realm-scoped e payload Configuration v4 | Pendente |
| Fase 2 - Catálogos RESX e infraestrutura de localização | Pendente |
| Fase 3 - Seleção de cultura por request e preferência do usuário | Pendente |
| Fase 4 - Códigos de apresentação e remoção de textos do core | Pendente |
| Fase 5 - Localização integral da UI de conta | Pendente |
| Fase 6 - Discovery e aceites multi-realm ponta a ponta | Pendente |
| Fase 7 - Documentação, guards e fechamento da dívida | Pendente |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluída, `%` e `X de 7`). Antes de fechar uma fase, confirme que decisões,
> critérios de aceite, testes e invariantes relacionados foram aplicados.

---

## Contexto

### Fontes verificadas

- [AGENTS.md](../../AGENTS.md) — realms são a fronteira de isolamento; `RoyalIdentity.Razor` contém a UI de
  conta; components usam page services; breaking changes são aceitos durante o desenvolvimento.
- [ADR-002](../../adrs/ADR-002.md), [ADR-007](../../adrs/ADR-007.md),
  [ADR-009](../../adrs/ADR-009.md), [ADR-013](../../adrs/ADR-013.md) e
  [ADR-019](../../adrs/ADR-019.md) — configuração, SSR estático, isolamento multi-realm, limites entre projetos
  e composition roots independentes.
- [product.md](../foundation/product.md), [tech.md](../foundation/tech.md) e
  [structure.md](../foundation/structure.md) — UI em Razor Components, realm descoberto antes da autenticação,
  options efetivas em `RealmOptions` e dependência proibida do core para a UI.
- [redesign-todo.md](../../redesign-todo.md) — `Localization` permanece aberta porque os textos da UI estão
  fixos em inglês.
- [an-localization-resource-inventory.md](../analisys/an-localization-resource-inventory.md) — inventário
  deduplicado de 62 chaves por idioma, dois catálogos lógicos e seis arquivos `.resx` para `en`, `pt-BR` e
  `es-419`.
- [plan-refactoring-debt-closure.md](plan-refactoring-debt-closure.md) — preserva explicitamente os três
  `[Redesign("Usar Resource")]` de `AccountOptions` para um plano específico; promove
  `RealmOptionsPayload` para v3 antes deste plano.
- [plan-data-configuration-storage.md](plan-data-configuration-storage.md) e
  [plan-data-storage-matrix.md](plan-data-storage-matrix.md) — options de realm são Configuration, serializadas
  em payload JSON versionado, materializadas pelo adapter EF e publicadas em snapshot assíncrono.
- [OpenID Connect Core 1.0 — `ui_locales`](https://openid.net/specs/openid-connect-core-1_0.html#AuthRequest) —
  `ui_locales` é uma lista ordenada de tags BCP 47; locales não suportados não tornam o request inválido.
- [OpenID Connect Discovery 1.0](https://openid.net/specs/openid-connect-discovery-1_0.html) —
  `ui_locales_supported` descreve os idiomas realmente suportados pela UI do OP.
- [ASP.NET Core 10 — conteúdo localizável](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization/make-content-localizable?view=aspnetcore-10.0),
  [Blazor globalization/localization](https://learn.microsoft.com/en-us/aspnet/core/blazor/globalization-localization?view=aspnetcore-10.0)
  e [Blazor forms validation](https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation?view=aspnetcore-10.0) —
  `IStringLocalizer<T>`, `.resx`, `RequestLocalization` e a integração de validação do .NET 10 são os mecanismos
  suportados para Razor Components.

### Estado atual do código (verificado em 2026-07-30)

- **Scaffold órfão:** `RoyalIdentity/Options/InternationalizationOptions.cs` contém `Enabled`,
  `DefaultLocale` e `SupportedLocales`, mas não é composto por `RealmOptions`, não possui cópia/validação e não
  é consumido.
- **`ui_locales` apenas transportado:** `AuthorizeContext`, `AuthorizationContext`, `EndSessionContext`,
  `LogoutMessage` e `LogoutCallbackMessage` carregam o valor; `AuthorizeMainValidator` limita o tamanho, mas
  nenhuma parte escolhe `CurrentCulture`/`CurrentUICulture`.
- **Sem infraestrutura do framework:** não existem `AddLocalization`, `UseRequestLocalization`,
  `IStringLocalizer` ou arquivos `.resx` no produto.
- **Ordem atual do middleware:** `UseRoyalIdentityProtocol()` instala `UseRealmDiscovery()` antes de CORS,
  autenticação e autorização; esse é o ponto obrigatório para inserir a seleção de cultura realm-aware.
- **UI fixa em inglês:** componentes de login, consentimento, logout, erro, domínio, perfil e signed-in contêm
  títulos, labels, placeholders, botões e mensagens em inglês.
- **Documento raiz fixo:** `RoyalIdentity.Server/Components/App.razor`,
  `RoyalIdentity.Demo/Components/App.razor` e `Tests.Host/Components/App.razor` usam `<html lang="en">`.
- **Mensagens no core:** `AccountOptions` contém `InvalidCredentialsErrorMessage`,
  `InactiveUserErrorMessage` e `BlockedUserErrorMessage`, todos marcados com `[Redesign("Usar Resource")]`;
  `LoginFlowService` escolhe e devolve esses textos.
- **Anti-enumeration vigente:** os três defaults de falha de login têm o mesmo texto, embora o evento retenha
  `AuthenticationFailureReason`.
- **Page services ainda devolvem frases:** `ConsentPageService`, `EndSessionPageService` e
  `LoginPageService` criam mensagens inglesas para validação/erro.
- **Persistência vigente:** `RealmOptionsPayloadSerializer.CurrentVersion` ainda é 1 no código verificado; os
  planos predecessores reservam v2 para Session Management e v3 para o fechamento das refatorações.
- **Testes reutilizáveis:** `Tests.Integration/UI` já cobre login e consentimento sobre
  `PersistentStorageAppFactory`; `Tests.Storage/Configuration/ConfigurationModelPayloadTests.cs` cobre
  roundtrip, versão, defaults ausentes e cópia das options.
- **Inventário de tradução:** a superfície atual foi deduplicada em 57 chaves de `AccountResources` e cinco de
  `ValidationResources`; com as três culturas do primeiro corte são 186 entradas em seis arquivos físicos.

### Lacunas, conflitos e restrições

- **Realm antes de cultura:** um provider global baseado apenas em `Accept-Language` não consegue aplicar
  `SupportedLocales`/`DefaultLocale` do realm e violaria o modelo multi-tenant.
- **`ui_locales` indireto:** nas páginas de interação, o hint pode estar dentro do `returnUrl` validado, em
  authorize parameters armazenados ou em `LogoutMessage`; ler apenas a query corrente perde esses casos.
- **Core não depende de Razor:** discovery e snapshot vivem no core, mas os catálogos embarcados pertencem a
  `RoyalIdentity.Razor`; a disponibilidade de locale exige um contrato estreito com implementação da UI.
- **Configuração não garante catálogo:** aceitar uma tag BCP 47 em `RealmOptions` não prova que o assembly da UI
  contém seus recursos; snapshot inicial/refresh não pode publicar configuração que metadata e UI não cumpram.
- **Protocolos são invariantes:** localizar `error`, parâmetros, claims ou constantes OAuth/OIDC quebraria
  interoperabilidade; somente apresentação humana entra nos catálogos.
- **SSR estático:** GET e POST são instâncias diferentes e scoped services têm lifetime de request; seleção de
  cultura e validação precisam estar corretas antes da renderização de cada request.
- **Sequência de payloads:** esta execução só inicia após o payload v3 do plano predecessor; não saltar
  diretamente de uma base divergente para v4.

### Superfícies impactadas a mapear

- `RoyalIdentity/Options`, `RoyalIdentity/Configuration`, `RoyalIdentity/Handlers/DiscoveryHandler.cs` e
  `RoyalIdentity/Users` — options, validação de snapshot, metadata e códigos do login.
- `RoyalIdentity.Razor` — catálogos, providers, page services, view models, components, validação e seletor de
  idioma.
- `RoyalIdentity.Storage.EntityFramework` e providers Configuration — payload v4, materialização, seeds e
  contracts sem migration relacional.
- `RoyalIdentity.Server`, `RoyalIdentity.Demo` e `Tests.Host` — registro, middleware e atributos `lang`/`dir`.
- `Tests.Identity`, `Tests.Integration`, `Tests.Storage` e `Tests.Architecture` — contrato, fluxos, persistência
  e boundaries.
- Futuro `plan-admin-api-ui.md` — reutiliza `IStringLocalizer<T>` e catálogos próprios para apresentar
  `ClientSecurityAssessment` por `RuleId`; não é implementado aqui.

---

## Objetivo

1. Tornar cultura e idiomas suportados configuração validada e persistida por realm.
2. Selecionar `CurrentCulture` e `CurrentUICulture` deterministicamente em cada request de UI, incluindo
   `ui_locales`, preferência do usuário, `Accept-Language` e fallback.
3. Entregar catálogos `.resx` neutro/inglês, `pt-BR` e `es-419`, consumidos por `IStringLocalizer<T>`, com
   paridade verificada.
4. Remover textos de apresentação do core e preservar falha genérica de autenticação sem enumeração de contas.
5. Localizar toda a UI de conta, validações, atributos de acessibilidade, títulos e documento HTML.
6. Publicar `ui_locales_supported` somente quando a UI correspondente estiver habilitada e disponível.
7. Encerrar a última dívida `Localization` de `redesign-todo.md` com testes multi-realm e documentação atualizada.

## Fora de escopo

- Armazenar/editar traduções em banco, permitir overrides de mensagens por realm ou criar UI administrativa de
  tradução — destino: necessidade futura do Admin.
- Substituir `IStringLocalizer<T>` por `ILocalizationService`, gerar `Resources.Designer.cs` ou adotar provider
  JSON/PO/ICU neste corte.
- Localizar códigos OAuth/OIDC, nomes de parâmetros/claims, logs, exceptions internas ou diagnósticos técnicos.
- Localizar `Client.Name`, display names/descrições de scopes, resources ou outro conteúdo cadastrado pelo
  tenant; conteúdo localizado do tenant exige modelo próprio.
- Implementar `claims_locales_supported` ou valores de claims localizados.
- Localizar a aplicação RP legada `Tests.WebApp`; os hosts do OP e `RoyalIdentity.Razor` são o escopo.
- Implementar API/UI administrativa; apenas deixar a infraestrutura reutilizável por recursos próprios do Admin.

---

## Perguntas ao humano

- Nenhuma questão aberta.
- **Q1 — Ativação default de localization para novos realms:** encerrada pela opção A:
  `Enabled=true`, `DefaultLocale="en"` e `SupportedLocales={"en","pt-BR","es-419"}`. Registrada em DF21.

---

## Decisões fechadas

- **DF1 — Configuração realm-scoped:** reaproveitar `InternationalizationOptions` como
  `RealmOptions.Internationalization`; não criar configuração global concorrente. Fonte: análise aprovada +
  ADR-002.
- **DF2 — Catálogo padrão RESX:** traduções fornecidas pelo produto vivem em `.resx` embarcado; o primeiro corte
  contém catálogo neutro em inglês, `pt-BR` e `es-419`. Fonte: decisão humana nesta discussão.
- **DF3 — API do framework:** components e services consomem `IStringLocalizer<T>`; não criar
  `ILocalizationService`, usar designer estático ou acessar `ResourceManager` diretamente. Fonte: decisão humana
  nesta discussão + ASP.NET Core 10.
- **DF4 — Chaves semânticas:** recursos usam chaves estáveis como `Login_Title` e
  `Consent_RequiredScopeNotGranted`, nunca a frase inglesa como chave. Fonte: análise aprovada.
- **DF5 — Precedência de cultura:** preferência explícita realm-scoped em cookie > `ui_locales` de contexto OIDC
  validado > `Accept-Language` > `DefaultLocale` do realm > locale neutro do catálogo. Fonte: decisão humana
  nesta discussão.
- **DF6 — Hints não causam erro:** locale inválido, desconhecido ou não suportado em `ui_locales`,
  `Accept-Language` ou cookie é ignorado; a resolução continua pela precedência. Fonte: OIDC Core + análise
  aprovada.
- **DF7 — Catálogo e configuração separados:** `InternationalizationOptions` define política do realm;
  `IUiLocaleCatalog` expõe somente locales realmente entregues pela UI. O core possui o contrato/default vazio e
  `RoyalIdentity.Razor` implementa o catálogo RESX, sem dependência reversa. Fonte: boundaries do repositório.
- **DF8 — Validação antes da publicação:** validadores extensíveis examinam `ConfigurationSnapshotData` antes do
  `Publish`; configuração inicial inválida falha startup e refresh inválido preserva o last-known-good. O
  validador Razor exige que locales configurados existam no catálogo. Fonte: semântica vigente do snapshot +
  análise aprovada.
- **DF9 — Middleware realm-aware:** `RequestLocalization` executa depois de `UseRealmDiscovery` e antes de CORS,
  autenticação e renderização; todos os composition roots usam a mesma extensão. Fonte: pipeline vigente +
  ASP.NET Core 10.
- **DF10 — Preferência explícita segura:** a UI oferece troca de idioma por POST com antiforgery; valida locale e
  return URL realm-bound antes de gravar cookie HttpOnly, SameSite e realm-scoped. O cookie contém somente a tag
  canônica e tem expiração persistente limitada. Fonte: análise aprovada + regras de UI/segurança.
- **DF11 — Boundary por códigos:** resultados do core e page services atravessam a borda com códigos estáveis,
  não frases inglesas. `LoginFlowResult` recebe código tipado; códigos exclusivos de apresentação permanecem em
  `RoyalIdentity.Razor`. Fonte: análise aprovada.
- **DF12 — Anti-enumeration:** credencial inválida, conta inativa e conta bloqueada produzem o mesmo código e a
  mesma mensagem localizada ao usuário; `AuthenticationFailureReason` continua disponível no evento interno.
  Fonte: regra atual + análise aprovada.
- **DF13 — Sem redesign de eventos:** remover mensagens configuráveis não cria novo pipeline, auditoria ou store;
  eventos existentes preservam o motivo interno e recebem texto diagnóstico invariável quando seu contrato ainda
  exigir texto. Fonte: decisão humana registrada em `plan-refactoring-debt-closure.md`.
- **DF14 — Metadata fiel:** `ui_locales_supported` é omitido quando localization/UI não está ativa; quando
  publicado, contém exatamente os locales configurados e presentes no catálogo, com default primeiro e os demais
  em ordem determinística. `claims_locales_supported` permanece ausente. Fonte: OIDC Discovery + análise aprovada.
- **DF15 — Texto, não HTML:** recursos contêm texto e placeholders; markup, URLs e decisões de encoding ficam
  nos components. Fonte: guidance ASP.NET Core.
- **DF16 — Documento cultural:** shells do OP derivam `lang` da cultura efetiva e `dir` de
  `CultureInfo.TextInfo.IsRightToLeft`; o primeiro catálogo pode ser LTR sem bloquear RTL futuro. Fonte: análise
  aprovada.
- **DF17 — Payload v4 direto:** após os predecessores entregarem v2/v3, remover os textos de `AccountOptions` e
  adicionar `Internationalization` promove somente `RealmOptionsPayload` para v4; versões anteriores falham
  fechadas e ambientes de desenvolvimento são reprovisionados. Não criar migration relacional. Fonte:
  sequenciamento dos planos + breaking changes aceitos.
- **DF18 — Validação moderna do .NET 10:** formulários SSR usam a integração
  `Microsoft.Extensions.Validation`/validation localization do .NET 10 com catálogo compartilhado; não adicionar
  o pacote experimental antigo de DataAnnotations para Blazor. Fonte: documentação .NET 10.
- **DF19 — Admin diferido:** o futuro Admin cria seus próprios recursos e localiza findings por `RuleId` sobre a
  mesma infraestrutura; este plano não adiciona telas administrativas. Fonte: roadmap/backlog.
- **DF20 — Espanhol latino-americano:** usar `es-419`, não uma variante nacional arbitrária. Após match
  exato/parents, o resolver pode selecionar a única variante configurada do mesmo idioma; não infere quando
  houver duas ou mais variantes candidatas. Fonte: decisão humana + inventário de recursos/CLDR/CultureInfo.
- **DF21 — Localization ativa por padrão:** novos realms e seeds nascem com `Enabled=true`,
  `DefaultLocale="en"` e `SupportedLocales={"en","pt-BR","es-419"}`. A negociação respeita imediatamente a
  preferência explícita, `ui_locales` e `Accept-Language`; realms ainda podem desabilitá-la explicitamente.
  Fonte: resposta humana à Q1 nesta discussão.

---

## Histórico de decisões

**Discussão preparatória (formato e provider):**

- **Alternativas consideradas:** `.resx`, JSON, PO/Gettext, banco e provider próprio.
  - **Resposta humana:** manter a análise anterior como base do plano após confirmar `.resx` como solução inicial.
  - **Considerações:** `.resx` é o backing nativo de `IStringLocalizer<T>`; JSON exigiria provider próprio; PO é
    útil para pluralização/workflow profissional; banco só se justifica para edição/override em runtime.
  - **Conclusão:** aplicar DF2/DF3 e diferir providers alternativos.

**Discussão preparatória (precedência):**

- **Alternativas consideradas:** `ui_locales` antes do cookie ou cookie antes de `ui_locales`.
  - **Resposta humana:** aprovou a análise original, na qual a escolha explícita persistida do usuário precede o
    hint do client OIDC.
  - **Conclusão:** aplicar DF5; sem cookie válido, o primeiro `ui_locales` suportado prevalece.

**Discussão preparatória (espanhol):**

- **Alternativas consideradas:** `es-419`, `es-MX` ou outra variante nacional.
  - **Resposta humana:** incluir espanhol das Américas no primeiro corte.
  - **Considerações:** CLDR e `CultureInfo` reconhecem `es-419` como espanhol da América Latina; variantes
    nacionais não herdam automaticamente de `es-419`.
  - **Conclusão:** aplicar DF20 e testar fallback de variante do mesmo idioma sem substituir match exato.

**Q1 (ativação default):**

- **Alternativas consideradas:** localization ativa ou desabilitada por padrão em novos realms.
  - **Resposta humana:** opção A, ativa por padrão.
  - **Conclusão:** aplicar DF21 nos defaults, seeds, payload esperado e testes de discovery/negociação.

---

## Design alvo

### Contratos e bordas

- `RealmOptions.Internationalization: InternationalizationOptions`: política realm-scoped com cópia profunda,
  normalização/validação e persistência Configuration.
- `InternationalizationOptions.Validate()`: valida `DefaultLocale`, conjunto não vazio, unicidade
  case-insensitive, tags reconhecidas/canônicas e pertencimento do default; não depende de Razor.
- `IUiLocaleCatalog`: contrato estreito no core para `NeutralLocale`, locales disponíveis e teste de
  disponibilidade; implementação RESX em `RoyalIdentity.Razor`, default vazio quando a UI não está composta.
- `IConfigurationSnapshotValidator.ValidateAsync(ConfigurationSnapshotData, CancellationToken)`: validators
  executam antes da publicação atômica; o validator de localization cruza cada realm habilitado com
  `IUiLocaleCatalog`.
- `RealmRequestCultureProvider`: resolve cultura efetiva pela DF5, somente entre options do realm e catálogo.
- `IStringLocalizer<AccountResources>`: catálogo de UI de conta.
- `IStringLocalizer<ValidationResources>`: catálogo compartilhado de DataAnnotations/validação SSR.
- `LoginFlowErrorCode`: código tipado no core; `InvalidCredentials` é comum a todas as falhas de autenticação
  observáveis pelo usuário.
- `AccountUiMessageCode` e mensagem protegida equivalente: códigos/argumentos seguros usados por page services e
  redirects internos da UI; a renderização resolve o texto na cultura do request.

### Modelo, dados e persistência

```text
RealmOptionsPayload v4 (JSON Configuration)
  Internationalization
    Enabled bool
    DefaultLocale string
    SupportedLocales string[] case-insensitive/canônico

  Account
    remove InvalidCredentialsErrorMessage
    remove InactiveUserErrorMessage
    remove BlockedUserErrorMessage

configuration_realms
  payload_version = 4
  payload_json inclui Internationalization
  nenhuma coluna/tabela/index novo
```

- O serializer continua omitindo `ServerOptions` e recebe o grafo autoritativo na materialização.
- Payload ausente ou versão diferente da v4 falha fechada após os planos predecessores; seeds/fixtures são
  regravados.
- `SupportedLocales` serializa em ordem determinística; comparação em runtime é ordinal case-insensitive e os
  valores materializados são nomes canônicos de `CultureInfo`.
- Traduções não entram em `Data.Configuration`, `RealmOptions`, tabelas ou snapshots.

### Arquitetura alvo

```text
RoyalIdentity/
  Options/
    InternationalizationOptions
    RealmOptions.Internationalization
  Configuration/
    IConfigurationSnapshotValidator
    valida todos antes de ConfigurationSnapshotHolder.Publish
  Contracts/Localization/
    IUiLocaleCatalog
    empty/default catalog
  Localization/
    RealmRequestCultureProvider
  Users/
    LoginFlowErrorCode em vez de mensagem de apresentação
  Handlers/
    DiscoveryHandler publica ui_locales_supported pelo catálogo efetivo

RoyalIdentity.Razor/
  Resources/
    AccountResources.resx
    AccountResources.pt-BR.resx
    AccountResources.es-419.resx
    ValidationResources.resx
    ValidationResources.pt-BR.resx
    ValidationResources.es-419.resx
  Localization/
    markers
    ResxUiLocaleCatalog
    LocalizationConfigurationSnapshotValidator
    seletor/cookie de preferência
  Components/Account/
    somente IStringLocalizer<T>/códigos; sem texto apresentável fixo

RoyalIdentity.Storage.EntityFramework/
  RealmOptionsPayloadSerializer v4

RoyalIdentity.Server|Demo|Tests.Host/
  html lang/dir derivados da cultura efetiva
```

### Seleção de cultura

```text
request roteado
  -> RealmDiscoveryMiddleware
  -> realm.Options.Internationalization + IUiLocaleCatalog
  -> se desabilitado: DefaultLocale/NeutralLocale
  -> cookie realm-scoped válido?
  -> ui_locales do AuthorizationContext/LogoutMessage validado?
  -> Accept-Language suportado?
  -> DefaultLocale disponível?
  -> NeutralLocale
  -> RequestLocalization define CurrentCulture + CurrentUICulture
  -> authentication/protocol/UI
```

- `ui_locales` preserva a ordem enviada e escolhe a primeira tag configurada/disponível.
- Match exato precede parent configurado; sem ambos, uma única variante configurada do mesmo idioma pode servir
  como fallback (`es-MX` → `es-419` enquanto `es-419` for a única variante espanhola).
- Authorization parameters inline e armazenados passam pelo `IAuthorizationContextResolver`; logout usa a
  mensagem protegida já existente, sem confiar em query arbitrária.
- O locale do cookie é revalidado em todo request; mudar options do realm não deixa preferência antiga furar a
  allowlist.
- A troca de idioma redireciona somente para URL local e pertencente ao mesmo realm.

### Segurança, concorrência e confiabilidade

- Nenhum valor de cookie/header/query seleciona cultura fora da allowlist do realm e do catálogo.
- Locale inválido nunca lança erro protocolar nem é refletido sem encoding na UI/log.
- Refresh de configuração valida o grafo completo antes do swap; falha mantém o snapshot anterior.
- Mensagens de autenticação não revelam existência, status, bloqueio ou inatividade da conta.
- Placeholders de cada chave são idênticos entre catálogos e argumentos são tratados como dados, não markup.
- O seletor de idioma exige antiforgery e não cria open redirect.
- Nenhuma cultura altera comparação de identificadores, URIs, claims, tokens ou valores normativos.

### Compatibilidade, migração e rollout

- Não criar shim para as três propriedades removidas de `AccountOptions`.
- Não ler payload v1/v2/v3 após v4; executar os planos predecessores e reprovisionar bancos/seeds de
  desenvolvimento.
- Não criar migration EF para mudança interna do JSON; atualizar serializers, fixtures, seed e scripts de
  verificação.
- Server, Demo e Tests.Host registram a mesma infraestrutura e aplicam os defaults definidos em DF21.
- Overrides em banco/realm podem ser adicionados futuramente por outra implementação/fallback de
  `IStringLocalizerFactory`, sem alterar consumers.

---

## Ordem de execução

1. **Fase 1 (options/payload)** — estabelece a política persistida e os defaults de DF21 antes de qualquer
   resolução.
2. **Fase 2 (catálogos/infraestrutura)** — entrega recursos, availability contract e validação pré-publicação.
3. **Fase 3 (request culture)** — aplica a política/catalogo no middleware e na preferência do usuário.
4. **Fase 4 (códigos/boundary)** — retira frases do core antes de localizar toda a UI.
5. **Fase 5 (UI)** — migra integralmente components, validações e shells.
6. **Fase 6 (discovery/aceites)** — só anuncia suporte depois que UI e resolução estão funcionais.
7. **Fase 7 (docs/fechamento)** — remove a dívida antiga após todos os guards e testes.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

---

## Fase 1 - Contrato realm-scoped e payload Configuration v4

**Depende de:** DF1, DF17, DF21,
[plan-oidc-session-management.md](plan-oidc-session-management.md) concluído e
[plan-refactoring-debt-closure.md](plan-refactoring-debt-closure.md) concluído com
`RealmOptionsPayloadSerializer.CurrentVersion == 3`.

**Escopo:** `InternationalizationOptions`, `RealmOptions`, materializador/payload Configuration, seeds,
fixtures, `Tests.Identity`, `Tests.Storage`, `Tests.Integration`.

**O que/como:** transformar o scaffold em option realm-scoped válida e independente; promover o payload JSON
para v4 sem migration relacional; falhar antes de editar se a sequência v2/v3 não estiver implementada.

**Tarefas:**

- [ ] Verificar que os planos predecessores terminaram e que o serializer de realm escreve v3.
- [ ] Incorporar `InternationalizationOptions` a todos os construtores/cópias de `RealmOptions`.
- [ ] Implementar normalização e `Validate()` para tags, default, conjunto e comparação case-insensitive.
- [ ] Aplicar os defaults decididos a novos realms e aos seeds de Server/Demo/testes.
- [ ] Promover `RealmOptionsPayloadSerializer` de v3 para v4 e preservar a exclusão de `ServerOptions`.
- [ ] Atualizar fixtures/scripts que gravam payload e remover artefatos v1/v2/v3 de desenvolvimento.
- [ ] Provar roundtrip estável, ordem determinística, cópia profunda, defaults e falha fechada.
- [ ] Confirmar que nenhuma migration relacional SQLite/PostgreSQL foi criada.

**Critérios de aceite:** todo realm materializado contém options válidas e independentes; novos realms e seeds
nascem com `Enabled=true`, default `en` e suporte a `en`/`pt-BR`/`es-419`; tags duplicadas apenas por casing são
rejeitadas/normalizadas conforme um único contrato; default pertence ao conjunto; payload v4 é determinístico e
v1-v3/versão futura falham; nenhuma coluna/tabela mudou.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~InternationalizationOptions"
dotnet test Tests.Storage --filter "FullyQualifiedName~ConfigurationModelPayloadTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~RealmOptions"
```

### Resultado da Fase 1

*a preencher*

---

## Fase 2 - Catálogos RESX e infraestrutura de localização

**Depende de:** Fase 1, DF2-DF4, DF7-DF8, DF15, DF18, DF20.

**Escopo:** `RoyalIdentity`, `RoyalIdentity.Razor/Resources`, registrations, snapshot refresher,
`Tests.Identity`, `Tests.Integration`, `Tests.Architecture`.

**O que/como:** registrar localization do framework, criar catálogos neutro/`pt-BR`/`es-419`, expor
disponibilidade sem dependência core→Razor e validar options/catálogos antes da publicação do snapshot.

**Tarefas:**

- [ ] Criar markers e `.resx` de `AccountResources`/`ValidationResources` com namespace/base name comprovados.
- [ ] Preencher as 57 chaves de `AccountResources` e cinco de `ValidationResources` conforme
  `an-localization-resource-inventory.md`.
- [ ] Preencher catálogo neutro inglês e traduções `pt-BR`/`es-419` para todas as 62 chaves.
- [ ] Registrar `AddLocalization` em `AddRoyalIdentityRazor` com `ResourcesPath` coerente.
- [ ] Registrar validation localization do .NET 10 com `AddValidation` e catálogo compartilhado.
- [ ] Criar `IUiLocaleCatalog` no core, implementação vazia/default e implementação RESX no Razor.
- [ ] Criar cadeia `IConfigurationSnapshotValidator` e executá-la após `LoadAsync`, antes de `Publish`.
- [ ] Implementar validator Razor que exige locale neutro, catálogos configurados e paridade de chaves/placeholders.
- [ ] Preservar startup fail-closed e last-known-good em refresh inválido.
- [ ] Adicionar guards contra HTML em recursos e contra uso direto de `ResourceManager`/designer nos consumers.
- [ ] Adicionar teste arquitetural garantindo que `RoyalIdentity` não referencia `RoyalIdentity.Razor`.

**Critérios de aceite:** os dois catálogos e suas três culturas resolvem as 62 chaves; nenhuma chave retorna o
próprio nome em cultura suportada; chaves/placeholders são equivalentes; os seis arquivos somam 186 entradas;
snapshot inválido nunca é publicado; Razor pode substituir o catálogo vazio sem dependência reversa; validação
SSR usa a API estável do .NET 10.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~ConfigurationSnapshot"
dotnet test Tests.Integration --filter "FullyQualifiedName~LocalizationCatalog"
dotnet test Tests.Architecture
```

### Resultado da Fase 2

*a preencher*

---

## Fase 3 - Seleção de cultura por request e preferência do usuário

**Depende de:** Fases 1-2, DF5-DF10, DF20.

**Escopo:** request culture provider, `UseRoyalIdentityProtocol`, authorization/logout context resolvers,
cookie/seletor Razor, Server/Demo/Tests.Host, testes HTTP.

**O que/como:** instalar `RequestLocalization` entre realm discovery e autenticação; resolver hints somente por
fontes validadas; oferecer preferência persistida realm-scoped sem abrir redirect.

**Tarefas:**

- [ ] Implementar `RealmRequestCultureProvider` com a precedência exata de DF5.
- [ ] Resolver `ui_locales` pelo `AuthorizationContext` para parâmetros inline e armazenados.
- [ ] Resolver `ui_locales` de End Session por `LogoutMessage` protegido.
- [ ] Reutilizar parsing do framework para `Accept-Language` e filtrar pela allowlist efetiva do realm.
- [ ] Aplicar match exato, parent e fallback para a única variante do mesmo idioma conforme DF20.
- [ ] Inserir `UseRequestLocalization` depois de `UseRealmDiscovery` e antes de CORS/autenticação.
- [ ] Implementar seletor POST/serviço de preferência com antiforgery, locale canônico e return URL realm-bound.
- [ ] Gravar cookie persistente HttpOnly/SameSite/realm-scoped contendo somente locale canônico.
- [ ] Ignorar cookie/hints que deixaram de ser suportados após refresh.
- [ ] Definir comportamento de `Enabled=false` como ausência de negociação, usando default/neutro sem metadata.
- [ ] Cobrir cancelamento, realm ausente, recurso neutro e cultures pai sem lançar erro protocolar.

**Critérios de aceite:** cada request seleciona uma única cultura permitida; cookie vence `ui_locales`,
`ui_locales` vence header; locale desconhecido cai para o próximo nível; dois realms no mesmo client HTTP não
compartilham preferência; middleware executa antes de qualquer UI/auth que leia a cultura; retorno externo é
rejeitado.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~RequestCulture"
dotnet test Tests.Integration --filter "FullyQualifiedName~CulturePreference"
dotnet test Tests.Architecture --filter "FullyQualifiedName~Middleware"
```

### Resultado da Fase 3

*a preencher*

---

## Fase 4 - Códigos de apresentação e remoção de textos do core

**Depende de:** Fases 1-3, DF11-DF13, DF17.

**Escopo:** `AccountOptions`, `LoginFlowResult`, `LoginFlowService`, eventos existentes, page services,
view models/mensagens protegidas, testes de login/consent/logout.

**O que/como:** substituir frases que cruzam core/UI por códigos tipados; remover as três options e seus
`[Redesign]`; localizar somente na última borda de apresentação.

**Tarefas:**

- [ ] Introduzir `LoginFlowErrorCode` e substituir `LoginFlowResult.ErrorMessage`.
- [ ] Mapear credencial inválida/inativa/bloqueada para um único `InvalidCredentials`.
- [ ] Preservar `AuthenticationFailureReason` no `UserLoginFailureEvent` sem redesenhar eventos/auditoria.
- [ ] Remover as três propriedades de mensagem e `[Redesign("Usar Resource")]` de `AccountOptions`.
- [ ] Remover cópia, payload, seeds e testes associados às propriedades eliminadas.
- [ ] Criar códigos de apresentação Razor para consentimento, logout, request ausente e retorno inválido.
- [ ] Transportar código + argumentos seguros em redirects/mensagens protegidas, sem persistir frase inglesa.
- [ ] Mapear todos os códigos para chaves de `AccountResources` e falhar teste quando um código não tiver recurso
  em inglês, `pt-BR` ou `es-419`.
- [ ] Garantir que descrição OAuth/OIDC e `error` normativo não sejam convertidos em códigos de recurso.

**Critérios de aceite:** não existe texto apresentável em `AccountOptions`/`LoginFlowResult`; as três classes de
falha de login renderizam texto idêntico em cada cultura; evento ainda distingue o motivo interno; todo código
tem recurso neutro, `pt-BR` e `es-419`; nenhum código OAuth/OIDC foi traduzido.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~LoginFlow"
dotnet test Tests.UserAccounts --filter "FullyQualifiedName~Login"
dotnet test Tests.Integration --filter "FullyQualifiedName~LoginPageTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~LoginEventCharacterizationTests"
```

### Resultado da Fase 4

*a preencher*

---

## Fase 5 - Localização integral da UI de conta

**Depende de:** Fases 2-4, DF3-DF4, DF15-DF16, DF18.

**Escopo:** todos os components/page services/view models de `RoyalIdentity.Razor`, resources, shells App dos
três hosts e testes de UI.

**O que/como:** inventariar cada string visível e substituí-la por localizer/código; localizar validação SSR e
atributos não visuais; derivar semântica cultural do documento.

**Tarefas:**

- [ ] Inventariar login local, domain selection, login externo, consent, offline access, logout, erro,
  signed-in, perfil e loading.
- [ ] Localizar `PageTitle`, headings, labels, botões, placeholders, ajuda e mensagens de validação/erro.
- [ ] Localizar `title`, `alt`, `aria-label` e demais textos de acessibilidade.
- [ ] Migrar validações DataAnnotations dos input models para `ValidationResources` pelo pipeline do .NET 10.
- [ ] Manter markup nos components e somente texto/placeholders nos `.resx`.
- [ ] Manter nomes/descrições de client, scopes e resources como conteúdo do tenant, com encoding normal.
- [ ] Exibir o seletor somente com mais de um locale efetivo e preservar return URL/realm.
- [ ] Substituir `lang="en"` por cultura efetiva em Server, Demo e Tests.Host.
- [ ] Derivar `dir="ltr|rtl"` de `TextInfo.IsRightToLeft`.
- [ ] Criar teste/allowlist que falha para nova string apresentável fixa em inglês na UI do produto.

**Critérios de aceite:** cada superfície listada renderiza inglês, `pt-BR` e `es-419`; validação client/server
SSR tem a mesma cultura; `html lang`/`dir` correspondem à cultura efetiva; nenhum recurso contém markup; não há
string apresentável fixa fora de uma allowlist técnica revisada.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~LoginPageTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~LoginConsentUIFlowTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~LocalizedAccountUi"
```

### Resultado da Fase 5

*a preencher*

---

## Fase 6 - Discovery e aceites multi-realm ponta a ponta

**Depende de:** Fases 1-5, DF6-DF9, DF14.

**Escopo:** `DiscoveryHandler`, catálogo efetivo, `Tests.Endpoints`, `Tests.Integration`, `Tests.Storage`,
composition roots.

**O que/como:** publicar metadata somente após a capacidade real existir e validar a matriz completa de seleção,
fallback, isolamento, persistência e UI.

**Tarefas:**

- [ ] Injetar o catálogo efetivo em discovery sem tornar o core dependente de Razor.
- [ ] Publicar `ui_locales_supported` apenas quando options e catálogo da UI estiverem ativos.
- [ ] Ordenar metadata com default primeiro e demais locales deterministicamente.
- [ ] Omitir `claims_locales_supported`.
- [ ] Cobrir `ui_locales` ordenado, inválido, desconhecido e culture pai.
- [ ] Cobrir `es-419` exato, `es-MX` com variante espanhola única e ausência de inferência quando houver
  variantes espanholas ambíguas.
- [ ] Cobrir authorization parameters inline e armazenados.
- [ ] Cobrir End Session/logout com `ui_locales`.
- [ ] Cobrir cookie > `ui_locales` > `Accept-Language` > default > neutro.
- [ ] Cobrir dois realms com options/cookies diferentes e impedir vazamento entre eles.
- [ ] Cobrir refresh inválido preservando snapshot/metadata anterior.
- [ ] Validar payload v4 e paridade Configuration em SQLite e PostgreSQL opt-in.
- [ ] Verificar Server, Demo e Tests.Host com a mesma ordem de middleware/registro.

**Critérios de aceite:** metadata é exatamente verdadeira para cada realm; locale não suportado não gera erro
OIDC; todos os caminhos de authorize/logout selecionam a mesma cultura esperada; realm B não observa cookie,
options ou metadata do realm A; SQLite e PostgreSQL materializam a mesma configuração.

**Testes:**

```powershell
dotnet test Tests.Endpoints --filter "FullyQualifiedName~Discovery"
dotnet test Tests.Integration --filter "FullyQualifiedName~Localization"
dotnet test Tests.Storage --filter "FullyQualifiedName~Configuration"
dotnet test Tests.Architecture
./scripts/Test-ServerPostgreSql.ps1
```

### Resultado da Fase 6

*a preencher*

---

## Fase 7 - Documentação, guards e fechamento da dívida

**Depende de:** Fases 1-6 e todas as DFs.

**Escopo:** `redesign-todo.md`, `AGENTS.md`, foundations, roadmap/backlog, este plano, documentação dos hosts e
suíte completa.

**O que/como:** tornar a implementação a nova baseline documental, remover referências obsoletas e fechar a
última dívida antiga somente após os aceites.

**Tarefas:**

- [ ] Marcar `Localization` como concluída em `redesign-todo.md` e apontar para este plano.
- [ ] Atualizar `product.md`, `tech.md`, `structure.md` e `AGENTS.md` com options, precedência, resources e
  limites de localização.
- [ ] Atualizar roadmap movendo este plano para concluídos e preservando a dependência do futuro Admin.
- [ ] Atualizar backlog do Admin para reutilizar infraestrutura e localizar `RuleId` sem persistir findings.
- [ ] Registrar explicitamente que overrides por realm, claims localizados e conteúdo multilíngue do tenant
  permanecem diferidos.
- [ ] Executar guards contra os três `[Redesign]`, mensagens removidas e strings fixas não permitidas.
- [ ] Executar suíte completa e registrar comandos/resultados no `Resultado da Fase`.
- [ ] Atualizar status, barra, tabela e matriz deste plano para concluído somente com todos os gates verdes.

**Critérios de aceite:** `redesign-todo.md` não contém dívida ativa de Localization; foundations descrevem o
runtime real; nenhum símbolo removido reaparece; diferidos têm destino; todos os testes obrigatórios estão
registrados e verdes.

**Testes:**

```powershell
if (rg -n "InvalidCredentialsErrorMessage|InactiveUserErrorMessage|BlockedUserErrorMessage|Redesign\\(\"Usar Resource\"\\)" RoyalIdentity Tests.Identity Tests.Integration Tests.Storage Tests.UserAccounts Tests.Endpoints) { throw "Dívida de Localization removida reapareceu." }
rg -n "IStringLocalizer|Internationalization|ui_locales_supported|RequestLocalization" RoyalIdentity RoyalIdentity.Razor .ai AGENTS.md
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

### Resultado da Fase 7

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(es) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| Options validadas/persistidas por realm | 1-2 | DF1, DF8, DF17 | v4 determinística; cópia/validação; snapshot pré-validado | `Tests.Identity`; `Tests.Storage`; `RealmOptions` |
| Seleção determinística por request | 3, 6 | DF5, DF6, DF9, DF10, DF20 | precedência exata; fallback espanhol não ambíguo; hints ignoráveis; cookies isolados | `RequestCulture`; `CulturePreference`; `Localization` |
| Catálogos RESX íntegros | 2, 5 | DF2-DF4, DF15, DF18, DF20 | 62 chaves por cultura; 6 arquivos; paridade de chaves/placeholders; nenhum missing resource | `LocalizationCatalog`; `LocalizedAccountUi` |
| Remover textos do core e preservar segurança | 4 | DF11-DF13 | códigos estáveis; falha genérica; motivo interno preservado | `LoginFlow`; `LoginEventCharacterizationTests` |
| Localizar UI completa/documento | 5 | DF3, DF4, DF15, DF16, DF18, DF20 | inglês/pt-BR/es-419; validação; `lang`/`dir`; sem hardcode | `LoginPageTests`; `LoginConsentUIFlowTests`; `LocalizedAccountUi` |
| Metadata fiel | 6 | DF7, DF14 | `ui_locales_supported` exato/omitido; sem claims locales | `Discovery`; `Localization` |
| Fechar dívida/documentação | 7 | DF19 | redesign/foundations/roadmap alinhados; guards verdes | `rg`; build; solution test |

---

## Invariantes a preservar

1. Toda política e preferência de localization é realm-scoped; nunca cruza realms.
2. `UseRealmDiscovery` continua antes de localization e autenticação.
3. O core não referencia `RoyalIdentity.Razor`, hosts, providers ou módulos.
4. `error`, parâmetros, claims e valores OAuth/OIDC permanecem invariáveis.
5. Locale nunca altera comparação de client ID, issuer, redirect URI, scope, token, claim ou chave.
6. Falhas de login não permitem enumerar conta ativa/inativa/bloqueada.
7. Eventos preservam `AuthenticationFailureReason`; este plano não cria auditoria/outbox.
8. Snapshot é publicado atomicamente somente após validação completa e mantém last-known-good em refresh falho.
9. UI localiza texto, nunca HTML; argumentos continuam encoded.
10. Conteúdo configurado pelo tenant não é confundido com recurso estático do produto.
11. Server nunca migra/seed; Demo continua self-provisioned; migrations/seeds externos preservam seus papéis.
12. Payload v4 só nasce depois de v2/v3; nenhuma migration relacional é criada para a troca do JSON.
13. Authorization codes continuam single-use, PKCE default-on e sessões/consents continuam realm-scoped.
14. SSR estático mantém GET/POST independentes e validação correta em ambas as requisições.

---

## Critérios globais de conclusão

- `InternationalizationOptions` está integrada, copiada, validada e persistida por realm no payload v4.
- Os seis catálogos físicos — 62 chaves por cultura em neutro/inglês, `pt-BR` e `es-419` — têm paridade
  completa de chaves/placeholders.
- Precedência cookie > `ui_locales` > `Accept-Language` > default > neutro está provada.
- Authorization inline/armazenada e End Session respeitam `ui_locales` sem erro para locale desconhecido.
- Toda UI de conta, validação, acessibilidade e documento HTML está localizada.
- As três mensagens configuráveis e seus `[Redesign]` foram removidos; anti-enumeration permanece.
- `ui_locales_supported` reflete exatamente configuração + catálogo e `claims_locales_supported` não é inventado.
- Dois realms permanecem isolados em options, cookie, UI e discovery.
- Snapshot inválido falha startup/refresh sem publicar estado parcial.
- `redesign-todo.md`, foundations, AGENTS e roadmap refletem a implementação concluída.
- `dotnet build RoyalIdentity.sln` e `dotnet test RoyalIdentity.sln` passam.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| Middleware executa antes do realm | cultura é escolhida sem `CurrentRealm` | config global/vazamento entre realms | ordem DF9 + teste arquitetural/HTTP | Aberto |
| `ui_locales` armazenado é perdido | login por handle cai no header/default | RP não controla idioma esperado | usar `IAuthorizationContextResolver` e cobrir inline/store | Aberto |
| Cookie cruza realms | path/nome amplo demais | preferência de um tenant afeta outro | path realm-scoped + dois realms no mesmo client | Aberto |
| Metadata anuncia catálogo ausente | locale configurado sem `.resx` | discovery mente e UI cai em inglês | catálogo efetivo + validator antes de publish | Aberto |
| Refresh publica config inválida | validator roda só no startup | runtime incoerente após alteração | validar `ConfigurationSnapshotData` em todo refresh | Aberto |
| Chave/placeholder diverge | tradução omite/renomeia `{0}` | erro em runtime ou texto incorreto | teste estrutural de paridade/placeholders | Aberto |
| Texto do core reaparece | service retorna frase por conveniência | boundary volta a misturar domínio/UI | códigos tipados + guards de source | Aberto |
| Tradução revela estado da conta | chaves diferentes por motivo | enumeração de usuário | um código/recurso para três motivos + testes | Aberto |
| Culture afeta protocolo | formatter/comparer usa cultura corrente | interoperabilidade ou vulnerabilidade | comparações ordinais/invariant + regressão protocolar | Aberto |
| Payload executado fora de ordem | base ainda é v1/v2 | salto de schema e planos inconsistentes | gate explícito `CurrentVersion == 3` na Fase 1 | Aberto |
| Validação SSR fica em inglês | apenas component text usa localizer | experiência parcialmente localizada | API .NET 10 + testes client/server | Aberto |
| Scan de hardcodes tem falsos positivos | nomes técnicos/test data em inglês | guard frágil | allowlist pequena, revisada e restrita ao produto UI | Aberto |

---

## Diferidos e backlog

- Overrides/editing de traduções por realm em runtime — destino: futuro plano do Admin quando houver requisito
  real; implementar provider/fallback sobre `IStringLocalizerFactory`, não trocar consumers.
- Catálogos PO/Gettext ou ICU para pluralização/gênero complexos — destino: revisão quando catálogo/workflow de
  tradução profissional exigir.
- Conteúdo localizado de clients/scopes/resources — destino: modelo próprio do catálogo/configuração do tenant.
- `claims_locales_supported` e valores de claims localizados — destino: plano OIDC específico.
- Localização da futura API/UI administrativa e findings de segurança — destino: `plan-admin-api-ui.md`;
  reutilizar infraestrutura e localizar por `RuleId`.
- Localização da RP de testes `Tests.WebApp` — fora do produto OP; revisar somente se virar aplicação distribuída.

---

## Referências

- [Inventário de recursos de Localization](../analisys/an-localization-resource-inventory.md).
- [redesign-todo.md](../../redesign-todo.md).
- [plans-roadmap-02.md](plans-roadmap-02.md).
- [backlog-001.md](../backlogs/backlog-001.md).
- [plan-refactoring-debt-closure.md](plan-refactoring-debt-closure.md).
- [plan-oidc-session-management.md](plan-oidc-session-management.md).
- [plan-data-configuration-storage.md](plan-data-configuration-storage.md).
- [ADR-002](../../adrs/ADR-002.md), [ADR-007](../../adrs/ADR-007.md),
  [ADR-009](../../adrs/ADR-009.md), [ADR-013](../../adrs/ADR-013.md) e
  [ADR-019](../../adrs/ADR-019.md).
- [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html).
- [OpenID Connect Discovery 1.0](https://openid.net/specs/openid-connect-discovery-1_0.html).
- [ASP.NET Core 10 localization](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization/make-content-localizable?view=aspnetcore-10.0).
- [Blazor globalization and localization](https://learn.microsoft.com/en-us/aspnet/core/blazor/globalization-localization?view=aspnetcore-10.0).
- [Blazor forms validation](https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation?view=aspnetcore-10.0).
