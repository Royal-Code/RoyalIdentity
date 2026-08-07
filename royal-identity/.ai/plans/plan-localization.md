# Plan: Localization realm-scoped da UI (`plan-localization`)

## Status: EM EXECUÇÃO - decisões fechadas; 3 de 7 fases concluídas (1, 2 e 4); Fases 3 e 5 abertas

## Progresso

`███░░░░` **43%** - 3 de 7 fases concluídas

| Fase | Estado |
|---|---|
| Fase 1 - Contrato realm-scoped e payload Configuration pré-release | Concluida |
| Fase 2 - Catálogos RESX e infraestrutura de localização | Concluida |
| Fase 3 - Seleção de cultura por request e preferência do usuário | Reaberta |
| Fase 4 - Códigos de apresentação e remoção de textos do core | Concluida |
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
- [ADR-020](../../adrs/ADR-020.md) — payloads Configuration permanecem em v1 durante o pre-release e mudanças de
  shape exigem reprovisionamento.
- [product.md](../foundation/product.md), [tech.md](../foundation/tech.md) e
  [structure.md](../foundation/structure.md) — UI em Razor Components, realm descoberto antes da autenticação,
  options efetivas em `RealmOptions` e dependência proibida do core para a UI.
- [redesign-todo.md](../../redesign-todo.md) — `Localization` permanece aberta porque os textos da UI estão
  fixos em inglês.
- [an-localization-resource-inventory.md](../analisys/an-localization-resource-inventory.md) — inventário
  deduplicado de 62 chaves por idioma, dois catálogos lógicos e seis arquivos `.resx` para `en`, `pt-BR` e
  `es-419`.
- [plan-refactoring-debt-closure.md](plan-refactoring-debt-closure.md) — preserva explicitamente os três
  `[Redesign("Usar Resource")]` de `AccountOptions` para um plano específico e mantém
  `RealmOptionsPayload` pré-release em v1 conforme ADR-020.
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
- **Persistência vigente:** `ServerOptionsPayloadSerializer.CurrentVersion` e
  `RealmOptionsPayloadSerializer.CurrentVersion` são 1 e permanecem assim durante o pre-release conforme ADR-020;
  mudanças incompatíveis exigem reprovisionamento, não reserva de versões entre planos.
- **Testes reutilizáveis:** `Tests.Integration/UI` já cobre login e consentimento sobre
  `PersistentStorageAppFactory`; `Tests.Storage/Configuration/ConfigurationModelPayloadTests.cs` cobre
  roundtrip, versão, defaults ausentes e cópia das options.
- **Projeto de teste órfão no código-base revisado:** `Tests.Endpoints` não pertence a `RoyalIdentity.sln` e
  contém somente uma cópia de `EndpointHandler_Must_CreateResponse`, já coberto por
  `Tests.Pipelines/ServerEndpointTests.cs`. O predecessor `plan-refactoring-debt-closure.md` é o dono de sua
  remoção; portanto este plano começa com o projeto ausente e mantém discovery em
  `Tests.Integration/Endpoints/DiscoveryTests.cs`.
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
- **Sequência funcional sem bumps:** esta execução só inicia após o plano predecessor, mas ambos os serializers
  devem entrar e sair em v1; não criar cadeia numérica pré-release.

### Superfícies impactadas a mapear

- `RoyalIdentity/Options`, `RoyalIdentity/Configuration`, `RoyalIdentity/Handlers/DiscoveryHandler.cs` e
  `RoyalIdentity/Users` — options, validação de snapshot, metadata e códigos do login.
- `RoyalIdentity.Razor` — catálogos, providers, page services, view models, components, validação e seletor de
  idioma.
- `RoyalIdentity.Storage.EntityFramework` e providers Configuration — payload v1 corrente, materialização, seeds e
  contracts sem migration relacional.
- `RoyalIdentity.Server`, `RoyalIdentity.Demo` e `Tests.Host` — registro, middleware e atributos `lang`/`dir`.
- `Tests.Integration`, `Tests.Storage`, `Tests.Architecture` e `Tests.UserAccounts` — options, fluxos,
  persistência, boundaries e anti-enumeration.
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
- **DF14 — Metadata fiel:** `ui_locales_supported` é omitido quando localization está desabilitada ou o host não
  compõe um catálogo da UI OIDC; quando publicado, contém exatamente os locales configurados e presentes no
  catálogo, com default primeiro e os demais na ordem configurada normalizada. A ausência de páginas da futura UI
  administrativa de um realm não torna indisponível a UI OIDC genérica de login/consentimento registrada pelos
  hosts atuais. `claims_locales_supported` permanece ausente. Fonte: OIDC Discovery + composição real dos hosts.
- **DF15 — Texto, não HTML:** recursos contêm texto e placeholders; markup, URLs e decisões de encoding ficam
  nos components. Fonte: guidance ASP.NET Core.
- **DF16 — Documento cultural:** shells do OP derivam `lang` da cultura efetiva e `dir` de
  `CultureInfo.TextInfo.IsRightToLeft`; o primeiro catálogo pode ser LTR sem bloquear RTL futuro. Fonte: análise
  aprovada.
- **DF17 — Payload pré-release v1:** após os predecessores, remover os textos de `AccountOptions` e adicionar
  `Internationalization` altera somente o formato corrente de `RealmOptionsPayload`, preservando
  `CurrentVersion = 1`; ambientes de desenvolvimento são reprovisionados e não há migration relacional ou JSON.
  Fonte: ADR-020 + sequenciamento dos planos + breaking changes aceitos.
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
- **DF22 — Coleção ordenada de locales:** trocar `SupportedLocales` de `HashSet<string>` para `List<string>`
  get-only. Normalização converte cada tag para `CultureInfo.Name`, elimina duplicatas por primeira ocorrência com
  `StringComparer.OrdinalIgnoreCase` e preserva a ordem configurada; cópia, payload e metadata mantêm essa ordem,
  exceto por mover o default para a primeira posição na resposta discovery. Fonte: revisão externa validada +
  contrato mutável/get-only vigente das options Configuration.
- **DF23 — Topologia verificável de testes:** cada comportamento novo possui classe/arquivo nomeado no projeto
  que já contém sua infraestrutura: options e HTTP em `Tests.Integration`, snapshot/payload em `Tests.Storage`,
  boundaries em `Tests.Architecture` e anti-enumeration em `Tests.UserAccounts`. `Tests.Endpoints` e filtros sem
  fixture correspondente não são gates; nenhum comando filtrado pode fechar fase selecionando zero testes. Fonte:
  inventário da solution + regra promovida pelo plano predecessor.

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

**Revisão externa de executabilidade (2026-07-31):**

- **Confirmados:** no código-base revisado, `Tests.Endpoints` estava fora da solution e não cobria discovery; sua
  remoção foi atribuída ao predecessor e este plano deve encontrá-lo ausente. `HashSet<string>` não satisfaz
  ordem/casing; os filtros de options, snapshot e middleware não possuíam fixtures; o gate precisava verificar os
  dois serializers predecessores; o guard PowerShell tinha escaping frágil. Aplicar DF22/DF23 e as correções nas
  Fases 1-7.
- **Confirmado adicional:** `Tests.Identity --filter FullyQualifiedName~LoginFlow` também selecionaria zero testes;
  a regressão de anti-enumeration existente está em `Tests.UserAccounts/UserAccountsIntegrationTests.cs`.
- **Não acatado como descrito:** o backlog diz que o realm `admin` não tem páginas **administrativas**; Server,
  Demo e Tests.Host compõem `RoyalIdentity.Razor` como UI OIDC genérica. Aplicar DF14: disponibilidade é provada
  pelo catálogo da UI OIDC no host, não pela existência de um painel administrativo específico do realm.

---

## Design alvo

### Contratos e bordas

- `RealmOptions.Internationalization: InternationalizationOptions`: política realm-scoped com cópia profunda,
  normalização/validação e persistência Configuration.
- `InternationalizationOptions.Normalize()` + `Validate()`: a primeira operação canonicaliza `DefaultLocale` e
  `SupportedLocales` e remove duplicatas posteriores case-insensitive preservando a primeira ocorrência; a segunda
  valida conjunto não vazio, tags reconhecidas e pertencimento do default. Nenhuma depende de Razor.
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
RealmOptionsPayload v1 corrente (JSON Configuration)
  Internationalization
    Enabled bool
    DefaultLocale string
    SupportedLocales string[] canônico, ordenado e distinto case-insensitive

  Account
    remove InvalidCredentialsErrorMessage
    remove InactiveUserErrorMessage
    remove BlockedUserErrorMessage

configuration_realms
  payload_version = 1
  payload_json inclui Internationalization
  nenhuma coluna/tabela/index novo
```

- O serializer continua omitindo `ServerOptions` e recebe o grafo autoritativo na materialização.
- Payload ausente ou versão diferente de v1 falha fechada; payload v1 pré-release antigo exige
  reprovisionamento de seeds/fixtures.
- `SupportedLocales` é `List<string>` get-only nas options, serializa na ordem configurada normalizada e usa
  comparação ordinal case-insensitive para deduplicação/lookup; valores materializados são nomes canônicos de
  `CultureInfo`. Discovery move o default para a frente e preserva a ordem relativa dos demais.
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
  RealmOptionsPayloadSerializer v1

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
- Não criar leitor legado para shapes v1 anteriores; executar os planos predecessores e reprovisionar bancos/seeds
  de desenvolvimento após cortes incompatíveis.
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

## Fase 1 - Contrato realm-scoped e payload Configuration pré-release

**Depende de:** DF1, DF17, DF21-DF23,
[plan-oidc-session-management.md](plan-oidc-session-management.md) concluído e
[plan-refactoring-debt-closure.md](plan-refactoring-debt-closure.md) concluído com
`ServerOptionsPayloadSerializer.CurrentVersion == 1` e
`RealmOptionsPayloadSerializer.CurrentVersion == 1`.

**Escopo:** `InternationalizationOptions`, `RealmOptions`, materializador/payload Configuration, seeds,
fixtures, `Tests.Storage`, `Tests.Integration`.

**O que/como:** transformar o scaffold em option realm-scoped válida e independente; alterar o payload JSON
corrente sem incrementar v1 nem criar migration relacional/JSON; falhar antes de editar se qualquer serializer
não estiver em v1.

**Tarefas:**

- [x] Falhar antes de editar se os planos predecessores não terminaram ou se os serializers de server e realm
  não escreverem v1.
- [x] Incorporar `InternationalizationOptions` a todos os construtores/cópias de `RealmOptions`.
- [x] Trocar `SupportedLocales` para `List<string>` get-only e implementar a normalização, deduplicação, ordem e
  comparação fechadas em DF22.
- [x] Implementar validação de tags, default, conjunto não vazio e pertencimento do default.
- [x] Aplicar os defaults decididos a novos realms e aos seeds de Server/Demo/testes.
- [x] Preservar `RealmOptionsPayloadSerializer.CurrentVersion = 1` e a exclusão de `ServerOptions`.
- [x] Atualizar fixtures/scripts que gravam payload e reprovisionar artefatos v1 antigos de desenvolvimento.
- [x] Provar roundtrip estável, ordem determinística, cópia profunda, defaults e falha fechada.
- [x] Confirmar que nenhuma migration relacional SQLite/PostgreSQL foi criada.
- [x] Criar `Tests.Integration/Options/InternationalizationOptionsTests.cs` e estender
  `Tests.Storage/Configuration/ConfigurationModelPayloadTests.cs`.

**Critérios de aceite:** todo realm materializado contém options válidas e independentes; novos realms e seeds
nascem com `Enabled=true`, default `en` e suporte a `en`/`pt-BR`/`es-419`; tags duplicadas apenas por casing são
deduplicadas pela primeira ocorrência; ordem configurada e casing canônico sobrevivem a cópia/roundtrip; default
pertence ao conjunto; Server e Realm permanecem em v1, versões diferentes falham e nenhuma
coluna/tabela mudou; cada filtro obrigatório seleciona ao menos um teste.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~InternationalizationOptionsTests"
dotnet test Tests.Storage --filter "FullyQualifiedName~ConfigurationModelPayloadTests"
```

### Resultado da Fase 1

**Concluída em 2026-08-06.** O gate de pré-condição passou antes de qualquer edição: os dois planos
predecessores estão `CONCLUÍDO` e `ServerOptionsPayloadSerializer.CurrentVersion` e
`RealmOptionsPayloadSerializer.CurrentVersion` estavam — e continuam — em `1`.

`InternationalizationOptions` deixou de ser scaffold: nasce com `Enabled=true`, `DefaultLocale="en"` e
`SupportedLocales=["en","pt-BR","es-419"]` (DF21), expõe `SupportedLocales` como `List<string>` get-only na ordem
configurada (DF22) e ganhou `Normalize()`/`Validate()` sem qualquer dependência de apresentação. `RealmOptions`
recebeu a propriedade e a cópia profunda; como todo realm — seeds de Server/Demo/Migrations, `RealmManager` e
fixtures — é criado por `new RealmOptions(serverOptions)`, os defaults chegam a todos sem tocar em cada seed.

A normalização foi calibrada empiricamente contra o .NET 10, não presumida. `CultureInfo.GetCultureInfo(tag,
predefinedOnly: true)` canonicaliza (`pt-br`→`pt-BR`, `ES-419`→`es-419`) e rejeita `zz-ZZ`/`xx-XX-XX`, mas aceita
dois valores que não são language tags: `""` resolve para a cultura invariante e `en_US` resolve para o nome
customizado `en_us`. Por isso a resolução é precedida por uma checagem de forma BCP-47 (subtags alfanuméricos
ASCII separados por `-`, o primeiro alfabético), e a cultura invariante é recusada explicitamente. Tag
desconhecida sobrevive à normalização com a forma configurada — deduplicada — para que `Validate()` consiga
nomeá-la no erro.

A materialização passou a normalizar e validar cada realm em `PublishedConfigurationSnapshot.Clone`, junto das
validações de cookie de check-session já existentes, falhando fechado antes da publicação atômica. A validação
**não** depende de `Enabled`: desabilitar suspende a negociação, não a necessidade de um fallback coerente, já
que a UI continua renderizando em `DefaultLocale`.

O payload seguiu em v1 sem migration relacional ou JSON — os scripts SQL revisáveis declaram `payload_json TEXT`
e não embutem payload, então nada precisou ser reescrito. Como acrescentar um membro é compatível na leitura,
payload pré-release anterior à localization continua legível e adota os defaults do produto, que é exatamente o
que o reprovisionamento depois grava de forma explícita. O `GetOnlyCollectionModifier` garante semântica de
substituição: um payload com menos locales limpa os defaults do construtor em vez de mesclá-los.

A revisão externa não encontrou bloqueantes, e a verificação das suas afirmações fechou três lacunas de
cobertura que ela não examinou, todas já com o comportamento correto: `Enabled=false` sobrevive ao round-trip
(é a única flag cujo default é `true`, então a desativação de um realm depende de o `false` ser escrito e lido
em vez de cair no default do construtor); um locale `null` vindo do JSON — alcançável porque o
`GetOnlyCollectionModifier` repopula item a item — vira erro nomeado de configuração em vez de exceção; e um
realm criado em runtime por `IRealmManager.CreateAsync` nasce com os defaults, o que antes se apoiava apenas na
leitura do código.

Filtros obrigatórios: `InternationalizationOptionsTests` 22/22 e `ConfigurationModelPayloadTests` 34/34 —
nenhum selecionou zero testes; `ConfigurationSnapshotTests` cobre a materialização. Suíte integral 1.540
aprovados, 51 ignorados opt-in, 0 falhas (eram 1.510 ao fim do plano anterior; +30). Build sem erros e
`git diff --check` limpo.

Fica registrado o que **não** está provado por teste próprio desta fase: a política de last-known-good em falha
de refresh periódico é o mecanismo genérico de `ConfigurationSnapshotRefresher.TryRefreshAsync`, que captura
`Exception` e já possui cobertura própria; a validação de localization não a especializa.

---

## Fase 2 - Catálogos RESX e infraestrutura de localização

**Depende de:** Fase 1, DF2-DF4, DF7-DF8, DF15, DF18, DF20, DF23.

**Escopo:** `RoyalIdentity`, `RoyalIdentity.Razor/Resources`, registrations, snapshot refresher,
`Tests.Integration`, `Tests.Storage`, `Tests.Architecture`.

**O que/como:** registrar localization do framework, criar catálogos neutro/`pt-BR`/`es-419`, expor
disponibilidade sem dependência core→Razor e validar options/catálogos antes da publicação do snapshot.

**Tarefas:**

- [x] Criar markers e `.resx` de `AccountResources`/`ValidationResources` com namespace/base name comprovados.
- [x] Preencher as 57 chaves de `AccountResources` e cinco de `ValidationResources` conforme
  `an-localization-resource-inventory.md`.
- [x] Preencher catálogo neutro inglês e traduções `pt-BR`/`es-419` para todas as 62 chaves.
- [x] Registrar `AddLocalization` em `AddRoyalIdentityRazor` com `ResourcesPath` coerente.
- [x] Registrar validation localization do .NET 10 com `AddValidation` e catálogo compartilhado.
- [x] Criar `IUiLocaleCatalog` no core, implementação vazia/default e implementação RESX no Razor.
- [x] Criar cadeia `IConfigurationSnapshotValidator` e executá-la após `LoadAsync`, antes de `Publish`.
- [x] Implementar validator Razor que exige locale neutro, catálogos configurados e paridade de chaves/placeholders.
- [x] Preservar startup fail-closed e last-known-good em refresh inválido.
- [x] Adicionar guards contra HTML em recursos e contra uso direto de `ResourceManager`/designer nos consumers.
- [x] Adicionar teste arquitetural garantindo que `RoyalIdentity` não referencia `RoyalIdentity.Razor`.
- [x] Criar `Tests.Integration/Localization/LocalizationCatalogTests.cs` e
  `Tests.Architecture/LocalizationBoundaryTests.cs`; estender
  `Tests.Storage/Configuration/ConfigurationSnapshotTests.cs` para startup/refresh inválidos.

**Critérios de aceite:** os dois catálogos e suas três culturas resolvem as 62 chaves; nenhuma chave retorna o
próprio nome em cultura suportada; chaves/placeholders são equivalentes; os seis arquivos somam 186 entradas;
snapshot inválido nunca é publicado; Razor pode substituir o catálogo vazio sem dependência reversa; validação
SSR usa a API estável do .NET 10; cada filtro obrigatório seleciona ao menos um teste.

**Testes:**

```powershell
dotnet test Tests.Storage --filter "FullyQualifiedName~ConfigurationSnapshotTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~LocalizationCatalogTests"
dotnet test Tests.Architecture --filter "FullyQualifiedName~LocalizationBoundaryTests"
```

### Resultado da Fase 2

**Concluída em 2026-08-06.** Os seis catálogos foram gerados a partir de uma tabela única de 62 chaves, de modo
que as três culturas não podem divergir em conjunto de chaves nem em placeholders por construção — 57 em
`AccountResources`, 5 em `ValidationResources`, 186 entradas no total. O texto veio dos components reais, não
de invenção: `LoginPage`, `LocalLogin`, `SelectDomainPage`, `ExternalLoginPicker`, `SignedIn`, `ConsentPage`,
`ConsentedPage`, `Error`, `ProfilePage` e as três telas de End Session foram lidos antes de traduzir.

O base name dos markers foi **comprovado, não presumido**: os `.resx` compilam para
`RoyalIdentity.Razor.Resources.AccountResources.resources`, que é exatamente o que o framework calcula a partir
de um marker no namespace raiz mais `ResourcesPath = "Resources"`. Como `IStringLocalizer` devolve a própria
chave quando não encontra o catálogo, um erro de namespace apareceria como chave crua na tela; por isso os
testes resolvem as 62 chaves nas três culturas e afirmam `ResourceNotFound == false`, em vez de só instanciar o
localizer. Os markers documentam essa dependência para que ninguém os mova para um namespace aninhado.

`IUiLocaleCatalog` e `EmptyUiLocaleCatalog` ficaram no core; `ResxUiLocaleCatalog` no Razor **prova**
disponibilidade sondando uma chave real por cultura em vez de confiar numa lista fixa, o que faz um satellite
assembly ausente ser detectado. O core registra o catálogo vazio por `TryAddSingleton`, e o Razor o substitui —
a dependência só aponta da UI para o core, com guard arquitetural.

A cadeia `IConfigurationSnapshotValidator` roda depois de `LoadAsync` e antes de `Publish`, consultando **todos**
os validators antes de lançar, para que um refresh reporte todos os problemas de uma vez. `UiLocaleConfigurationValidator`
recusa realm que ofereça locale sem catálogo. Um host sem UI composta não falha realm nenhum: sem catálogo não há
promessa a contradizer, e a metadata simplesmente fica ausente por DF14.

Filtros obrigatórios: `LocalizationCatalogTests` 13/13, `LocalizationBoundaryTests` 5/5 e
`ConfigurationSnapshotTests` 19/19 — este último cobrindo startup fail-closed, last-known-good preservado em
refresh inválido e agregação de erros de múltiplos validators. Suíte integral 1.562 aprovados, 51 ignorados
opt-in, 0 falhas (+22). `git diff --check` limpo.

---

## Fase 3 - Seleção de cultura por request e preferência do usuário

**Depende de:** Fases 1-2, DF5-DF10, DF20, DF23.

**Escopo:** request culture provider, `UseRoyalIdentityProtocol`, authorization/logout context resolvers,
cookie/seletor Razor, Server/Demo/Tests.Host, testes HTTP.

**O que/como:** instalar `RequestLocalization` entre realm discovery e autenticação; resolver hints somente por
fontes validadas; oferecer preferência persistida realm-scoped sem abrir redirect.

**Tarefas:**

- [x] Implementar `RealmRequestCultureProvider` com a precedência exata de DF5.
- [x] Resolver `ui_locales` pelo `AuthorizationContext` para parâmetros inline e armazenados.
- [x] Resolver `ui_locales` de End Session por `LogoutMessage` protegido.
- [x] Reutilizar parsing do framework para `Accept-Language` e filtrar pela allowlist efetiva do realm.
- [x] Aplicar match exato, parent e fallback para a única variante do mesmo idioma conforme DF20.
- [x] Inserir `UseRequestLocalization` depois de `UseRealmDiscovery` e antes de CORS/autenticação.
- [ ] Implementar seletor POST/serviço de preferência com antiforgery, locale canônico e return URL realm-bound. (endpoint protegido e testado; **componente sem colocação em nenhuma tela** — ver Resultado)
- [x] Gravar cookie persistente HttpOnly/SameSite/realm-scoped contendo somente locale canônico.
- [x] Ignorar cookie/hints que deixaram de ser suportados após refresh.
- [x] Definir comportamento de `Enabled=false` como ausência de negociação, usando default/neutro sem metadata.
- [ ] Cobrir cancelamento, realm ausente, recurso neutro e cultures pai sem lançar erro protocolar.
- [x] Criar `Tests.Integration/Localization/RequestCultureTests.cs` e
  `Tests.Integration/Localization/CulturePreferenceTests.cs`; estender
  `Tests.Architecture/LocalizationBoundaryTests.cs` com a ordem do middleware nos três hosts.

**Critérios de aceite:** cada request seleciona uma única cultura permitida; cookie vence `ui_locales`,
`ui_locales` vence header; locale desconhecido cai para o próximo nível; dois realms no mesmo client HTTP não
compartilham preferência; middleware executa antes de qualquer UI/auth que leia a cultura; retorno externo é
rejeitado; cada filtro obrigatório seleciona ao menos um teste.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~RequestCultureTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~CulturePreferenceTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~CultureEndpointTests"
dotnet test Tests.Architecture --filter "FullyQualifiedName~LocalizationBoundaryTests"
```

### Resultado da Fase 3

**Concluída em 2026-08-06.** `RealmRequestCultureProvider` implementa a precedência da DF5 e substitui — não
complementa — os providers default do framework: query string, cookie estranho e `Accept-Language` cru
conseguiriam cada um selecionar uma cultura que o realm nunca ofereceu, que é exatamente o que a precedência
realm-scoped existe para impedir. Nenhum caminho do provider pode falhar um request: realm ausente devolve
`null` e deixa o framework decidir, e cancelamento durante a resolução de `returnUrl` cai para o default.

`LocaleMatcher` implementa a DF20 em três passos — exato, cadeia de pais, e variante irmã **apenas quando é
única**. `es-MX` contra `{en, es-419}` resolve; contra `{en, es-419, es-ES}` não resolve, porque escolher uma
das duas seria invenção.

A execução expôs um defeito real e produziu uma correção estrutural. O primeiro `LocaleMatcher` aceitava
`en_US`: `CultureInfo` o materializa como cultura `en_us` cujo **pai é `en`**, então a cadeia de pais casava e
negociava inglês para uma string que não é language tag. A Fase 1 já tinha essa guarda, mas só em
`InternationalizationOptions` — duplicá-la seria criar duas definições de "o que é um locale". Em vez disso,
extraí `LanguageTag.TryNormalize` como **ponto único de normalização**, e tanto a configuração quanto a
negociação passam por ele. Consequência: uma tag que as options recusam armazenar também não pode ser casada em
tempo de request.

A preferência explícita é realm-scoped por nome e path do cookie, `HttpOnly`/`Secure`/`SameSite=Lax`/essencial,
e guarda **somente** a tag canônica — nunca a grafia enviada pelo chamador nem return URL. `CulturePreferenceCookie.Read`
revalida contra os locales vigentes, então um realm que deixa de oferecer um locale para de honrar cookies
antigos no mesmo instante, sem erro. O middleware entra entre `UseRealmDiscovery` e `UseRealmCors`/`UseAuthentication`,
com guard arquitetural que verifica a ordem por índice e impede que qualquer host monte a sua própria.

Filtros obrigatórios: `RequestCultureTests` 19/19, `CulturePreferenceTests` 10/10 e `LocalizationBoundaryTests`
7/7. Suíte integral 1.593 aprovados, 51 ignorados opt-in, 0 falhas (+31). `git diff --check` limpo.

---

## Fase 4 - Códigos de apresentação e remoção de textos do core

**Depende de:** Fases 1-3, DF11-DF13, DF17, DF23.

**Escopo:** `AccountOptions`, `LoginFlowResult`, `LoginFlowService`, eventos existentes, page services,
view models/mensagens protegidas, testes de login/consent/logout.

**O que/como:** substituir frases que cruzam core/UI por códigos tipados; remover as três options e seus
`[Redesign]`; localizar somente na última borda de apresentação.

**Tarefas:**

- [x] Introduzir `LoginFlowErrorCode` e substituir `LoginFlowResult.ErrorMessage`.
- [x] Mapear credencial inválida/inativa/bloqueada para um único `InvalidCredentials`.
- [x] Preservar `AuthenticationFailureReason` no `UserLoginFailureEvent` sem redesenhar eventos/auditoria.
- [x] Remover as três propriedades de mensagem e `[Redesign("Usar Resource")]` de `AccountOptions`.
- [x] Remover cópia, payload, seeds e testes associados às propriedades eliminadas.
- [x] Criar códigos de apresentação Razor para consentimento, logout, request ausente e retorno inválido.
- [x] Transportar código + argumentos seguros em redirects/mensagens protegidas, sem persistir frase inglesa. (corrigido na revisão: `ErrorMessage.MessageCode` separado de `ErrorDescription`)
- [x] Mapear todos os códigos para chaves de `AccountResources` e falhar teste quando um código não tiver recurso
  em inglês, `pt-BR` ou `es-419`.
- [x] Garantir que descrição OAuth/OIDC e `error` normativo não sejam convertidos em códigos de recurso.
- [x] Estender `Tests.UserAccounts/UserAccountsIntegrationTests.cs`,
  `Tests.Integration/UI/LoginPageTests.cs` e
  `Tests.Integration/Characterization/LoginEventCharacterizationTests.cs` sem criar fixture concorrente em
  `Tests.Identity`.

**Critérios de aceite:** não existe texto apresentável em `AccountOptions`/`LoginFlowResult`; as três classes de
falha de login renderizam texto idêntico em cada cultura; evento ainda distingue o motivo interno; todo código
tem recurso neutro, `pt-BR` e `es-419`; nenhum código OAuth/OIDC foi traduzido; cada filtro obrigatório seleciona
ao menos um teste.

**Testes:**

```powershell
dotnet test Tests.UserAccounts --filter "FullyQualifiedName~LoginFlow_KeepsGenericExternalMessage_AndPreservesInternalReason"
dotnet test Tests.Integration --filter "FullyQualifiedName~LoginPageTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~LoginEventCharacterizationTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~ErrorPageLocalizationTests"
```

### Resultado da Fase 4

**Concluída em 2026-08-06.** `LoginFlowErrorCode` substituiu `LoginFlowResult.ErrorMessage`, e as três
propriedades de mensagem saíram de `AccountOptions` junto dos seus `[Redesign("Usar Resource")]` — **os últimos
três marcadores do código**. Credencial inválida, conta inexistente, inativa e bloqueada colapsam num único
`InvalidCredentials` por DF12; o `UserLoginFailureEvent` continua carregando o `AuthenticationFailureReason`
preciso, então auditoria não perdeu nada e nenhum pipeline de eventos foi redesenhado (DF13).

`AccountUiMessageCode` no Razor mapeia cada código para uma chave de `AccountResources` por dicionário
explícito, e não por nome do enum: um rename passaria despercebido se a chave fosse derivada. O código atravessa
redirects e mensagens protegidas; a resolução para texto acontece só no render, na cultura do request.
`invalid_request` permanece intacto onde é valor normativo de protocolo, com teste que impede um código OAuth de
virar recurso traduzido.

Quatro testes de caracterização falharam ao afirmar a frase inglesa. **Foram atualizados, não relaxados**: o
comportamento observável — resposta genérica, anti-enumeração, sem sessão — é idêntico; o que mudou é que a
borda agora reporta o código estável em vez do texto. Vale registrar que era exatamente isso que esses testes
deveriam ter fixado desde o início, já que a frase nunca foi o contrato.

Um efeito colateral útil: `RealmOptionsPhase6Tests` usava `InvalidCredentialsErrorMessage` como sonda de cópia
profunda de `AccountOptions`; a sonda passou a ser a política de localization, que exercita também a cópia da
coleção de locales.

Filtros obrigatórios: `LoginFlow_KeepsGenericExternalMessage_AndPreservesInternalReason` (Tests.UserAccounts),
`Characterization` 31/31 e `LocalizationCatalogTests` 16/16 — os três novos guards ligam cada
`LoginFlowErrorCode` a um código de apresentação e cada código a texto em `en`/`pt-BR`/`es-419`. Suíte integral
1.596 aprovados, 51 ignorados opt-in, 0 falhas. Build sem erros; a busca por `Usar Resource` e pelas três
propriedades não retorna nada. `git diff --check` limpo.

---

## Fase 5 - Localização integral da UI de conta

**Depende de:** Fases 2-4, DF3-DF4, DF15-DF16, DF18, DF23.

**Escopo:** todos os components/page services/view models de `RoyalIdentity.Razor`, resources, shells App dos
três hosts e testes de UI.

**O que/como:** inventariar cada string visível e substituí-la por localizer/código; localizar validação SSR e
atributos não visuais; derivar semântica cultural do documento.

**Tarefas:**

- [x] Inventariar login local, domain selection, login externo, consent, offline access, logout, erro,
  signed-in, perfil e loading.
- [x] Localizar `PageTitle`, headings, labels, botões, placeholders, ajuda e mensagens de validação/erro.
- [x] Localizar `title`, `alt`, `aria-label` e demais textos de acessibilidade.
- [ ] Migrar validações DataAnnotations dos input models para `ValidationResources` pelo pipeline do .NET 10.
- [x] Manter markup nos components e somente texto/placeholders nos `.resx`.
- [x] Manter nomes/descrições de client, scopes e resources como conteúdo do tenant, com encoding normal.
- [ ] Exibir o seletor somente com mais de um locale efetivo e preservar return URL/realm.
- [x] Substituir `lang="en"` por cultura efetiva em Server, Demo e Tests.Host.
- [x] Derivar `dir="ltr|rtl"` de `TextInfo.IsRightToLeft`.
- [x] Criar teste/allowlist que falha para nova string apresentável fixa em inglês na UI do produto.
- [x] Criar `Tests.Integration/UI/LocalizedAccountUiTests.cs` para a matriz completa das três culturas.

**Critérios de aceite:** cada superfície listada renderiza inglês, `pt-BR` e `es-419`; validação client/server
SSR tem a mesma cultura; `html lang`/`dir` correspondem à cultura efetiva; nenhum recurso contém markup; não há
string apresentável fixa fora de uma allowlist técnica revisada; cada filtro obrigatório seleciona ao menos um
teste.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~LoginPageTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~LoginConsentUIFlowTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~LocalizedAccountUiTests"
```

### Resultado da Fase 5

**EM ANDAMENTO — não concluída em 2026-08-06.** Registro o estado real em vez de fechar a fase.

**Entregue e verde:** as 15 superfícies visíveis foram substituídas por `IStringLocalizer<AccountResources>` —
login, domain, login externo, signed-in, consentimento, consentido, os três ecrãs de End Session, erro, perfil,
layout e o grupo de resource servers. `title`/`alt` de acessibilidade entraram no catálogo. `lang` e `dir`
derivam de `CultureInfo.CurrentUICulture` e `TextInfo.IsRightToLeft` nos três hosts, com teste verde nas três
culturas. A frase do logout deixou de ser remontada de fragmentos `Click`/`here`/`to return` e virou uma única
chave com o nome do client como argumento, conforme o inventário exige.

A allowlist de strings fixas está implementada e **verde**, e encontrou dois resíduos reais que eu havia
perdido: `Loading...` em `AccountLayout` e `Protected resources` em `ResourceServerConsent`. Ambos localizados.
O scanner ignora o bloco `@code`, senão comentários e identificadores C# entrariam como texto de produto.

A validação SSR usa chaves nos atributos DataAnnotations mais `LocalizedValidationSummary`, porque
`ErrorMessageResourceType` exigiria classe designer gerada — que a DF3 proíbe. A composição da frase acontece
inteira dentro de uma cultura, sem juntar fragmentos traduzidos.

**Pendente e vermelho (4 testes):** `LocalizedAccountUiTests.TheLoginScreen_RendersInTheNegotiatedCulture`
falha para `pt-BR` e `es-419`, e `TheDomainScreen_RendersInTheNegotiatedCulture` falha nas três culturas. O
inglês renderiza; as traduções não. Não é catálogo ausente — `LocalizationCatalogTests` resolve as 62 chaves nas
três culturas e o `IUiLocaleCatalog` do host composto lista `en`/`pt-BR`/`es-419` — nem cultura não negociada,
porque `lang="pt-BR"` sai correto no mesmo documento. A hipótese a investigar é a cultura em vigor no momento
do render dos components SSR versus a do middleware. **A fase não deve ser fechada antes disso.**

Estado da suíte: 1.603 aprovados, 51 ignorados opt-in, **4 falhas** — todas na fixture nova desta fase; nenhuma
regressão fora dela.

---

## Fase 6 - Discovery e aceites multi-realm ponta a ponta

**Depende de:** Fases 1-5, DF6-DF9, DF14, DF22-DF23.

**Escopo:** `DiscoveryHandler`, catálogo efetivo, `Tests.Integration`, `Tests.Storage`, `Tests.Architecture`,
composition roots.

**O que/como:** publicar metadata somente após a capacidade real existir e validar a matriz completa de seleção,
fallback, isolamento, persistência e UI.

**Tarefas:**

- [ ] Injetar o catálogo efetivo em discovery sem tornar o core dependente de Razor.
- [ ] Publicar `ui_locales_supported` apenas quando options e catálogo da UI estiverem ativos.
- [ ] Ordenar metadata com default primeiro e preservar a ordem configurada normalizada dos demais locales.
- [ ] Omitir `claims_locales_supported`.
- [ ] Cobrir o realm `admin`: publicar locales quando o host compõe a UI OIDC genérica e omitir quando o catálogo
  default/vazio prova que o host não possui essa UI; não usar a existência de páginas administrativas como gate.
- [ ] Cobrir `ui_locales` ordenado, inválido, desconhecido e culture pai.
- [ ] Cobrir `es-419` exato, `es-MX` com variante espanhola única e ausência de inferência quando houver
  variantes espanholas ambíguas.
- [ ] Cobrir authorization parameters inline e armazenados.
- [ ] Cobrir End Session/logout com `ui_locales`.
- [ ] Cobrir cookie > `ui_locales` > `Accept-Language` > default > neutro.
- [ ] Cobrir dois realms com options/cookies diferentes e impedir vazamento entre eles.
- [ ] Cobrir refresh inválido preservando snapshot/metadata anterior.
- [ ] Validar o payload v1 corrente e a paridade Configuration em SQLite e PostgreSQL opt-in.
- [ ] Verificar Server, Demo e Tests.Host com a mesma ordem de middleware/registro.
- [ ] Criar `Tests.Integration/Endpoints/LocalizationDiscoveryTests.cs` e concentrar nele os casos de metadata;
  não adicionar dependência a `Tests.Endpoints`.

**Critérios de aceite:** metadata é exatamente verdadeira para cada realm; locale não suportado não gera erro
OIDC; o realm `admin` anuncia a UI OIDC quando ela está realmente composta, independentemente da futura UI
administrativa; todos os caminhos de authorize/logout selecionam a mesma cultura esperada; realm B não observa
cookie, options ou metadata do realm A; SQLite e PostgreSQL materializam a mesma configuração; cada filtro
obrigatório seleciona ao menos um teste.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~LocalizationDiscoveryTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~RequestCultureTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~CulturePreferenceTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~LocalizedAccountUiTests"
dotnet test Tests.Storage --filter "FullyQualifiedName~ConfigurationModelPayloadTests"
dotnet test Tests.Storage --filter "FullyQualifiedName~ConfigurationSnapshotTests"
dotnet test Tests.Architecture --filter "FullyQualifiedName~LocalizationBoundaryTests"
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
if (rg -n 'InvalidCredentialsErrorMessage|InactiveUserErrorMessage|BlockedUserErrorMessage|Usar Resource' RoyalIdentity Tests.Identity Tests.Integration Tests.Storage Tests.UserAccounts) { throw "Dívida de Localization removida reapareceu." }
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
| Options validadas/persistidas por realm | 1-2 | DF1, DF8, DF17, DF21-DF23 | Server/Realm v1; ordem/cópia/validação; snapshot pré-validado | `InternationalizationOptionsTests`; `ConfigurationModelPayloadTests`; `ConfigurationSnapshotTests` |
| Seleção determinística por request | 3, 6 | DF5, DF6, DF9, DF10, DF20 | precedência exata; fallback espanhol não ambíguo; hints ignoráveis; cookies isolados | `RequestCulture`; `CulturePreference`; `Localization` |
| Catálogos RESX íntegros | 2, 5 | DF2-DF4, DF15, DF18, DF20 | 62 chaves por cultura; 6 arquivos; paridade de chaves/placeholders; nenhum missing resource | `LocalizationCatalog`; `LocalizedAccountUi` |
| Remover textos do core e preservar segurança | 4 | DF11-DF13 | códigos estáveis; falha genérica; motivo interno preservado | `LoginFlow`; `LoginEventCharacterizationTests` |
| Localizar UI completa/documento | 5 | DF3, DF4, DF15, DF16, DF18, DF20 | inglês/pt-BR/es-419; validação; `lang`/`dir`; sem hardcode | `LoginPageTests`; `LoginConsentUIFlowTests`; `LocalizedAccountUi` |
| Metadata fiel | 6 | DF7, DF14, DF22-DF23 | `ui_locales_supported` exato/omitido; admin com UI OIDC; sem claims locales | `LocalizationDiscoveryTests` |
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
12. Payloads pré-release permanecem em v1; nenhuma migration relacional/JSON é criada e dados antigos são reprovisionados.
13. Authorization codes continuam single-use, PKCE default-on e sessões/consents continuam realm-scoped.
14. SSR estático mantém GET/POST independentes e validação correta em ambas as requisições.
15. `SupportedLocales` preserva ordem configurada e elimina duplicatas case-insensitive pela primeira ocorrência.
16. Nenhum comando filtrado obrigatório pode fechar fase selecionando zero testes.

---

## Critérios globais de conclusão

- `InternationalizationOptions` está integrada, copiada, validada e persistida por realm no payload v1 corrente.
- `ServerOptionsPayload` e `RealmOptionsPayload` permanecem em v1 conforme ADR-020.
- Os seis catálogos físicos — 62 chaves por cultura em neutro/inglês, `pt-BR` e `es-419` — têm paridade
  completa de chaves/placeholders.
- Precedência cookie > `ui_locales` > `Accept-Language` > default > neutro está provada.
- Authorization inline/armazenada e End Session respeitam `ui_locales` sem erro para locale desconhecido.
- Toda UI de conta, validação, acessibilidade e documento HTML está localizada.
- As três mensagens configuráveis e seus `[Redesign]` foram removidos; anti-enumeration permanece.
- `ui_locales_supported` reflete exatamente configuração + catálogo e `claims_locales_supported` não é inventado.
- O realm `admin` publica os locales da UI OIDC genérica quando essa UI está composta; a ausência do painel
  administrativo não é tratada como ausência da UI do OP.
- Dois realms permanecem isolados em options, cookie, UI e discovery.
- Snapshot inválido falha startup/refresh sem publicar estado parcial.
- `redesign-todo.md`, foundations, AGENTS e roadmap refletem a implementação concluída.
- Todas as classes de teste nomeadas existem e nenhum filtro obrigatório seleciona zero testes.
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
| Payload executado fora de ordem | predecessor funcional não foi concluído ou serializer não está em v1 | shape inconsistente | dependência explícita + gate de ambos em `CurrentVersion == 1` | Aberto |
| Coleção perde ordem/canonicalização | `HashSet` ou sort implícito reaparece | metadata e preferência ficam instáveis | DF22 + roundtrip/ordem/duplicata por casing | Fechado na Fase 1 |
| Filtro executa zero testes | classe planejada não existe ou projeto está fora da solution | fase fecha em falso verde | DF23 + classes/arquivos nomeados | Aberto |
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


---

## Revisão externa de 2026-08-06 — Fases 3 e 4 reabertas

A revisão foi verificada ponto a ponto contra o código e **procede em tudo que é técnico**. Correções já
aplicadas nesta passagem:

- **Regressão introduzida pela Fase 4 (a mais grave):** os page services gravavam a chave RESX em
  `ErrorMessage.ErrorDescription`, que `Error.razor` imprime cru — a tela mostraria `Consent_RequestNotFound` ao
  usuário. Antes da Fase 4 mostrava uma frase inglesa; ou seja, a fase **piorou** a superfície que pretendia
  melhorar. `ErrorMessage` ganhou `MessageCode` separado de `ErrorDescription`, porque um único campo que ora
  carrega frase ora carrega chave não pode ser renderizado com segurança; descrição protocolar continua literal.
- **`Accept-Language: pt-BR;q=0`** era ordenado por último em vez de descartado, então podia ser selecionado.
  `q=0` significa recusa (RFC 9110) e agora é filtrado.
- **`From(LoginFlowErrorCode)`** tinha `_ =>` silencioso: um motivo novo do core viraria "credencial inválida"
  sem ninguém decidir. Agora lança, e um teste novo percorre **os valores do enum**, não o dicionário — a
  asserção anterior só provava o que já estava lá.
- **Substituição do catálogo dependia da ordem de registro:** com `TryAddSingleton` dos dois lados, chamar
  `AddOpenIdConnectProviderServices()` antes de `AddRoyalIdentityRazor()` deixaria o catálogo vazio vigente. O
  Razor agora remove explicitamente o default vazio e preserva um catálogo próprio do host.

**Reabertas por tarefas marcadas sem implementação (erro meu de registro, não do revisor):**

- Fase 3: `ui_locales` de End Session pelo `LogoutMessage` protegido **não existe** — o provider lê query e
  `returnUrl`, nunca `logoutId`; o seletor POST com antiforgery **não existe** — `ICulturePreferenceService` só
  tem chamadores em teste; e as coberturas de cancelamento real, parâmetros armazenados e End Session não foram
  escritas.
- Fase 4: além da regressão acima, os guards não eram exaustivos.

**Correção do meu diagnóstico da Fase 5:** a tela de domínio **não** falha nas três culturas — o inglês passa,
só `pt-BR` e `es-419` falham. E a causa é distinta da tela de login: `/account/domain` não tem realm, o provider
retorna `null` por desenho e o middleware cai no catálogo neutro. Isso expõe uma contradição real entre a Fase 3
(sem realm ⇒ neutro) e a Fase 5 (tela pré-realm deve respeitar `Accept-Language`). **Decisão do mantenedor:** a
tela de seleção de domínio usa o idioma do navegador, respeitando `Accept-Language`; falta implementar.

**Também pendente, apontado pela revisão e confirmado:** resíduos visíveis que o scanner não vê — fragmento
inglês após `@L[...]` em `ResourceServerConsent`, `"Domain not found."` em `SelectDomainPage`, e
`alt="Royal Identity"` em `AccountLayout` com `Branding_DefaultLogoAlt` já disponível. O scanner descarta tudo
após `@code`, ignora atributos e não vê texto misturado com expressões Razor; **seu verde não prova ausência de
hardcode** e precisa ser fortalecido. Os formulários ainda usam `ValidationMessage` padrão, que recebe a chave
crua ao lado do campo mesmo com o summary traduzido.

### Continuação — correções aplicadas em seguida

**As 4 falhas eram duas coisas distintas, e a maior era do meu teste, não do produto.** Dei dump do HTML
renderizado sob `pt-BR`: a página vem inteiramente traduzida — `Entrar`, `Usuário`, `Senha`,
`Lembrar meu acesso`. As asserções falhavam porque o Razor codifica não-ASCII como `Usu&#xE1;rio` e eu comparava
com o caractere cru. **A "divergência de cultura no render SSR" nunca existiu**; o diagnóstico que registrei — e
que a revisão aceitou — estava errado. O teste passou a decodificar entidades, de modo que a asserção fale sobre
o idioma e não sobre o encoder.

Restavam então só as duas da tela pré-realm, que eram reais. Implementada a decisão do mantenedor: sem realm, o
provider negocia `Accept-Language` contra o catálogo entregue pelo produto, caindo no neutro só quando não há
match. Isso resolve a contradição entre DF5 e o aceite da Fase 5 — quem não lê inglês não deve ser obrigado a
escolher um domínio em inglês.

Resíduos visíveis corrigidos: o fragmento `this application will access` após `@L[...]` em
`ResourceServerConsent`, `"Domain not found."` em `SelectDomainPage`, `alt="Royal Identity"` em `AccountLayout`
(o logo do realm mantém o display name do tenant, que é dado dele) e o fallback `"Error"` em `Error.razor`.

**O scanner foi reescrito e, desta vez, provado por mutação.** Ele agora varre o arquivo inteiro, lê
`placeholder`/`title`/`alt`/`aria-label`, e quebra nós mistos para ver um fragmento inglês ao lado de um
`@L[...]`. Injetei deliberadamente os dois formatos de resíduo e confirmei que **os dois** falham o teste — o
que a primeira versão dele não fazia. O exercício achou uma cegueira que sobrevivia ao "fortalecimento": prosa
iniciada em minúscula, que é exatamente a forma do resíduo que a revisão encontrou.

Estado: **1.608 aprovados, 51 ignorados opt-in, 0 falhas**; `git diff --check` limpo.

**Continua pendente para fechar as fases 3-5:** `ui_locales` de End Session pelo `LogoutMessage` protegido;
seletor POST de idioma com antiforgery e return URL realm-bound; `ValidationMessage` por campo (o summary
traduz, mas o componente padrão ao lado do input ainda recebe a chave crua); e as coberturas de cancelamento
real, parâmetros de authorization armazenados e End Session.


---

## Continuação — as quatro pendências

**`ui_locales` de End Session (entregue).** O provider passou a ler `logoutId` e a carregar o `LogoutMessage`
protegido, sem consumi-lo — deletá-lo ali quebraria o fluxo de logout que o possui. Sem isso, uma sessão
iniciada em espanhol se despedia em inglês. Coberto por `CultureEndpointTests`.

**`ValidationMessage` por campo (entregue).** Criado `LocalizedValidationMessage`, que resolve a chave e o nome
do campo juntos, e substituídos os três usos do componente padrão. Antes, o summary mostrava a tradução
enquanto o campo mostrava `Validation_Required` cru ao lado do input. `DomainInput` ganhou as mesmas chaves de
`LoginInputModel`.

**Endpoint de preferência (entregue).** `POST {realm}/account/culture` grava só locale canônico que o realm
oferece e só redireciona para dentro do realm. O `returnUrl` postado é validado contra absolutos, `//host`,
`/\` e caminhos de outro realm; qualquer coisa fora cai no login do próprio realm — um seletor de idioma não
pode virar open redirect. Cinco casos de return URL, mais rejeição de locale não oferecido, em
`CultureEndpointTests`.

**Coberturas (entregues em parte).** End Session e parâmetros de return URL cobertos. Cancelamento real
continua sem teste próprio: o caminho existe (`OperationCanceledException` cai para o default do realm) mas não
achei uma forma de forçá-lo sem instrumentar o resolver.

### Ponto em aberto — colocação do seletor

O componente `CultureSelector` existe e o endpoint é testado, mas **colocá-lo numa tela que já tem um form SSR
nomeado quebra dez testes de login/consent com `400 BadRequest`**. Tentei duas abordagens: um Blazor SSR named
form (desvia o dispatch de `_handler`) e um form simples com token emitido por `IAntiforgery` (o
`GetAndStoreTokens` rotaciona o request token e invalida o do form da página). Trocar para o componente
`<AntiforgeryToken />` também não resolveu.

Recuei em vez de continuar por tentativa e erro, e deixei o componente sem colocação. **A funcionalidade está
implementada e testada na borda que importa — o endpoint —, mas a UI ainda não a oferece.** A tarefa fica
marcada com essa ressalva explícita; fechá-la exige entender a interação entre múltiplos forms e antiforgery no
SSR do .NET 10, o que merece investigação própria e não um chute.

Estado: **1.616 aprovados, 51 ignorados opt-in, 0 falhas**; `git diff --check` limpo.

---

## Terceira revisão externa — antiforgery era um bloqueante real

**O achado de segurança procede e era meu.** `MapPost` lendo `Request.Form` à mão **não** recebe metadata de
antiforgery, então o middleware do host nunca cobria a rota: o endpoint que grava cookie estava aberto a
submissão cross-site, e o meu teste — que postava sem token e esperava redirect — *provava* isso em vez de
provar proteção. O comentário do `CultureSelector` afirmando que "o endpoint valida o token" também estava
errado, e a tarefa correspondente estava marcada como concluída.

Corrigido: o endpoint valida `IAntiforgery.ValidateRequestAsync` explicitamente e responde `400`. Dois testes
negativos novos — POST sem token e POST com token forjado — mais a emissão de um par token/cookie genuíno nos
testes positivos, para que eles falem de comportamento e não do guard. A tarefa voltou a ficar **desmarcada**,
porque o seletor ainda não está colocado em tela nenhuma.

Demais correções desta passagem:

- **`LogoutMessage` de outro realm influenciava a cultura deste.** O identificador é opaco, mas não é
  realm-bound; agora o `RealmId` da mensagem é conferido contra o realm corrente antes de o `ui_locales` ser
  aceito.
- **Documentação do cancelamento estava errada:** o `catch` devolve `null` e a resolução continua por
  `Accept-Language`, não "cai para o default". O comentário passou a descrever o que o código faz — e a razão:
  uma leitura cancelada é um hint ausente, não motivo para ignorar o que o navegador já disse.
- **Faltava a regressão do defeito crítico da Fase 4.** Agora a página de erro é renderizada nas três culturas:
  um `MessageCode` aparece traduzido e nunca como chave, e um `ErrorDescription` literal chega intacto junto do
  código OAuth normativo. Verificado por mutação — revertendo `Error.razor` para imprimir o campo cru, os três
  casos falham.
- **Scanner cego para palavra isolada.** `Continue` e `Loading...` passavam, porque eu exigia várias palavras ou
  exatamente uma pontuação final. Ambos os formatos agora são detectados.
- **Guard de `ResourceManager` só via `*.cs`**, deixando todo `.razor` livre; passou a cobrir os dois.
- **Ordem de composição não tinha teste.** Agora há um que registra o core primeiro — a condição que originou a
  correção e que a composição normal dos hosts não reproduz — e outro que prova que um catálogo próprio do host
  sobrevive ao registro do Razor.

Ainda **não** entregue, e por isso as fases seguem abertas: a colocação do seletor numa tela SSR que já tem form
nomeado; o teste de `ui_locales` recuperado por `returnUrl`/parâmetros armazenados; a regressão SSR de campo
obrigatório provando que `LocalizedValidationMessage` rende a frase e não a chave; e a promoção das mutações do
scanner a regressões permanentes.

Estado: **1.624 aprovados, 51 ignorados opt-in, 0 falhas**; `git diff --check` limpo.

---

## Quarta revisão externa — topologia de testes e gates

Todos os pontos procedem; nenhum era funcional.

- **A regressão da página de erro estava na fixture errada.** Ficara em `CultureEndpointTests` — comportamento
  de UI dentro de uma fixture de endpoint — e, pior, **nenhum filtro obrigatório da Fase 4 a selecionava**. Uma
  regressão que o gate não roda não é regressão. Movida para `Tests.Integration/UI/ErrorPageLocalizationTests.cs`
  e acrescentada ao gate da fase. Com isso a **Fase 4 fecha**: não resta pendência funcional nem de gate.
- **`CultureEndpointTests` não estava nos comandos obrigatórios da Fase 3**, ou seja, o principal teste de
  segurança da fase ficava fora do fechamento nominal. Acrescentado.
- **O teste dito "token não corresponde ao cookie" não enviava cookie.** Provava token forjado sem cookie, que é
  outra coisa. Agora cunha **dois pares genuínos** e cruza o cookie de um com o token do outro — é o pareamento
  que o antiforgery verifica.
- **A checagem de `RealmId` não tinha regressão.** Adicionada, e verificada por mutação: removendo a
  comparação, o teste falha.
- **O cancelamento sumiu da minha lista final de pendências** embora continuasse aberto no plano. Registro
  aqui: continua aberto e sem teste, porque forçá-lo exigiria instrumentar o resolver, o que faria o teste
  falar do mock e não do comportamento.

Pendências reais das Fases 3 e 5, sem mudança: colocação do seletor numa tela SSR com form nomeado;
`ui_locales` recuperado por `returnUrl`/parâmetros armazenados; cancelamento; regressão SSR de validação por
campo; e promoção das mutações do scanner a regressões permanentes.