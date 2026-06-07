# Plan: RealmOptions e CORS por Realm

## Status: PLANNED

## Progresso

`----------` **0%** - 0 de 6 fases concluidas

---

## Contexto

Este plano nasce da auditoria da Fase 7 do `plan-realm-hardening.md`.

O RoyalIdentity ja possui `RealmOptions`, mas algumas configuracoes continuam somente em `ServerOptions`.
Em um cenario multi-tenant real, realms diferentes podem precisar de politicas diferentes de autenticacao,
CSP, logging, eventos, formato de token e limites de entrada.

Além disso, CORS esta parcialmente iniciado no codigo, mas nao esta conectado ao pipeline.
Ha endpoints marcados como candidatos a CORS em `Constants.Oidc.Routes.CorsPaths` e ha deteccao de origem
em `HttpRequestExtensions.GetCorsOrigin()`, porem nao ha `AddCors`, `UseCors`, middleware proprio,
`ICorsPolicy` ou configuracao de origens permitidas.

---

## Objetivo

Transformar a auditoria `ServerOptions vs RealmOptions Gap` em uma refatoracao guiada:

1. Definir quais opcoes devem ser efetivamente resolvidas por realm.
2. Evitar que servicos capturem `storage.ServerOptions` no construtor quando o valor correto depende do realm da request.
3. Implementar ou preparar o modelo de CORS por realm e, quando necessario, por client.
4. Cobrir os comportamentos com testes de integracao que provem isolamento entre realms.
5. Definir um contrato unico de resolucao de opcoes, incluindo fallback global, criacao de realms por copia e comportamento para requests sem realm.

---

## Fora de Escopo

- Federation / external IdPs continua em `.ai/backlogs/backlog-001.md`.
- Redesign completo de politica de senha so deve entrar aqui se a auditoria encontrar configuracao ja existente em `AccountOptions`.
- Nao remover `ServerOptions` de uma vez. O servidor ainda deve manter defaults globais e compatibilidade de configuracao.

---

## Estado Atual Auditado

As seguintes propriedades existem em `ServerOptions` e precisam ser avaliadas como configuracoes efetivas por realm:

| Propriedade | Impacto | Prioridade |
|---|---|---|
| `AuthenticationOptions Authentication` | Lifetime de sessao, sliding expiration e politica de cookie. Bloqueador concreto: `ConfigureRealmCookieAuthenticationOptions` le `storage.ServerOptions.Authentication` e `storage.ServerOptions.UI.AccessDeniedPath`, entao hoje realm diferentes nao conseguem ter lifetime e redirect de access denied proprios. | Alta |
| `CspOptions Csp` | Content Security Policy depende do dominio do realm, branding e fontes externas permitidas. | Alta |
| `LoggingOptions Logging` | Verbosidade e sensibilidade de logs podem variar por realm por razoes operacionais ou de compliance. | Media |
| `string AccessTokenJwtType` | Header JWT `typ`; pode ser necessario por compatibilidade com clients especificos. | Baixa |
| `bool EmitScopesAsSpaceDelimitedStringInJwt` | Formato de scopes no JWT; pode variar por compatibilidade de client/ecossistema. | Baixa |
| `bool DispatchEvents` | Permitir desligar eventos em realms de teste/dev ou ajustar comportamento operacional. | Baixa |
| `InputLengthRestrictions InputLengthRestrictions` | Limites de entrada podem variar por requisito de seguranca/compliance. | Baixa |

Itens de prioridade alta representam gaps bloqueantes para deployments multi-tenant realistas, especialmente quando realms representam organizacoes diferentes.

### Achados Adicionais da Revisao do Plano

- Todos os realms seedados em `MemoryStorage` recebem `new RealmOptions(serverOptions)` usando a mesma instancia estatica de `ServerOptions`.
- `RealmManager.CreateAsync` tambem cria realms com `new RealmOptions(storage.ServerOptions)`.
- Portanto, a opcao A so e segura se houver copia real dos defaults globais no momento de criacao do realm. Adicionar propriedades a `RealmOptions` sem uma estrategia de clone/factory ainda pode deixar objetos compartilhados entre realms.
- `CheckSessionResult` resolve `ServerOptions` diretamente pelo container e `ResponseToFormPostResult` resolve `IOptions<ServerOptions>`. Esses caminhos devem ser tratados como divida de DI, nao apenas como call sites globais.
- **`CheckSessionEndpoint` está registrado em DI mas NÃO está mapeado** em `MapOpenIdConnectProviderEndpoints` (que mapeia só Discovery, JWK, Authorize, AuthorizeCallback, Token, UserInfo, Revocation, EndSession). Logo, `CheckSessionResult` é inalcançável por rota hoje, e **o único consumidor de CSP vivo é `ResponseToFormPostResult`** (response_mode=form_post). O teste de check-session (Fase 6, item 13) fica adiado até o endpoint ser mapeado.

### Impedimento Estrutural

Hoje o valor lido é sempre o `ServerOptions` global porque `RealmOptions.ServerOptions` aponta para a **mesma instância** em todos os realms (injetada via `storage.ServerOptions`). Migrar uma propriedade para `RealmOptions` não basta: é preciso mudar **cada consumidor** para resolver a opção efetiva pelo realm da request. E os consumidores usam **quatro padrões de resolução distintos** — o plano precisa tratar todos, não só o primeiro.

**Padrão 1 — captura de `storage.ServerOptions` no construtor (via DI).** Estes serviços guardam o `ServerOptions` global num campo e ficam cegos para o realm:

- `DefaultJwtFactory`
- `DefaultTokenValidator`
- `DefaultEventDispatcher`
- `EvaluateBearerToken`
- `LoadClient`
- `AuthorizeMainValidator`
- `RedirectUriValidator`
- `SecretEvaluatorBase`
- `ConfigureRealmCookieAuthenticationOptions`

**Padrão 2 — resolução via `IServiceProvider` no momento do uso (singleton).** Não capturam no construtor, mas resolvem o `ServerOptions` global a cada execução:

- `CheckSessionResult` — `httpContext.RequestServices.GetRequiredService<ServerOptions>()`, usa `.Csp` e `.Authentication.CheckSessionCookieName`.

**Padrão 3 — resolução via `IOptions<ServerOptions>`.**

- `ResponseToFormPostResult` — `GetRequiredService<IOptions<ServerOptions>>().Value.Csp`.

**Padrão 4 — resolução via `ContextItems`.** O `ServerOptions` é colocado no `ContextItems` (ex.: `AuthorizeCallbackEndpoint` faz `ContextItems.From(realm.Options.ServerOptions)`) e lido depois:

- `LoggerExtensions` — `context.Items.GetOrCreate<ServerOptions>()`, usa `.Logging.SensitiveValuesFilter` e `.Logging.UseLogService`.

> **Consequência prática:** corrigir apenas o Padrão 1 **não** torna CSP nem Logging per-realm — seus consumidores reais estão nos Padrões 2, 3 e 4. CSP vive em response handlers que só têm `HttpContext` (sem `context.Options`), portanto a resolução por realm aqui precisa passar por `httpContext.GetCurrentRealm()`, não por `context.Options`. Cada fase que migra uma propriedade deve mapear o padrão de cada consumidor antes de implementar.

### Contrato de Resolucao de Opcoes

A Fase 1 deve registrar a decisao final antes de qualquer mudanca de codigo. O contrato recomendado e:

- `ServerOptions` permanece como configuracao global do servidor e template inicial para novos realms.
- Toda opcao promovida para realm deve viver como instancia independente em `RealmOptions`.
- Criacao de realm copia os defaults via **construtores de copia de `RealmOptions`** (ver "Padrao de Copia de RealmOptions"). O construtor `RealmOptions(ServerOptions)` ja e o ponto unico de criacao hoje; um `RealmOptionsFactory` separado so sera necessario se a criacao passar a exigir DI.
- Requests com realm devem ler opcoes efetivas a partir de `RealmOptions`, via `context.Options`, `context.Realm.Options` ou `httpContext.GetCurrentRealm().Options`.
- Response handlers que so recebem `HttpContext` devem resolver o realm via `httpContext.GetCurrentRealm()` quando o endpoint exige realm.
- Requests sem realm podem continuar usando defaults globais, mas esse fallback deve ser explicito e limitado a fluxos realmente server-wide.
- `ContextItems` nao deve carregar `ServerOptions` como fonte de verdade para propriedades que variam por realm. Para logging/limites, carregar `RealmOptions`, a opcao especifica, ou resolver via `IEndpointContextBase.Options`.
- `IOptions<ServerOptions>` e `GetRequiredService<ServerOptions>()` nao devem ser adicionados ou preservados como atalhos para valores realm-specific.

Matriz minima esperada apos a decisao. **É o contrato-alvo, não o estado atual** — hoje os Padrões 2 e 3 (ver "Dívida de DI") leem de instâncias default, não de `storage.ServerOptions`; o código deve ser migrado para esta matriz:

| Opcao | Leitura recomendada em request com realm | Fallback sem realm |
|---|---|---|
| `Authentication` | `realm.Options.Authentication` | `storage.ServerOptions.Authentication` |
| `Csp` | `httpContext.GetCurrentRealm().Options.Csp` ou `context.Options.Csp` | `storage.ServerOptions.Csp` |
| `Logging` | `context.Options.Logging` ou item explicito de logging do realm | `storage.ServerOptions.Logging` |
| `DispatchEvents` | `realm.Options.DispatchEvents` no overload realm-aware | `storage.ServerOptions.DispatchEvents` |
| `AccessTokenJwtType` | `realm.Options.AccessTokenJwtType` | `storage.ServerOptions.AccessTokenJwtType` |
| `EmitScopesAsSpaceDelimitedStringInJwt` | `realm.Options.EmitScopesAsSpaceDelimitedStringInJwt` | `storage.ServerOptions.EmitScopesAsSpaceDelimitedStringInJwt` |
| `InputLengthRestrictions` | `context.Options.InputLengthRestrictions` | `storage.ServerOptions.InputLengthRestrictions` |

### Divida de DI a Resolver

**Estado atual — `ServerOptions` não é configurável e não tem fonte única.** Hoje existe **uma** instância: a estática `MemoryStorage.serverOptions = new()` (puros defaults). O ponto de extensão `AddOpenIdConnectProviderServices(Action<CustomOptions>)` é só hooks de pipeline — não configura `ServerOptions`. Não há `Configure<ServerOptions>`, `AddSingleton<ServerOptions>` nem binding de appsettings. Resultado: os caminhos de consumo resolvem **até quatro instâncias diferentes**, consistentes só porque tudo é default — viram bug latente assim que alguém configurar o `ServerOptions`:

| Caminho | Resolve hoje |
|---|---|
| `storage.ServerOptions` / `realm.Options.ServerOptions` | a estática do `MemoryStorage` (a real) |
| `ContextItems.GetOrCreate<ServerOptions>()` | a estática **se** semeada via `ContextItems.From(...)`; senão um default novo |
| `GetRequiredService<ServerOptions>()` | **não registrado → lançaria** (mascarado por código morto) |
| `GetRequiredService<IOptions<ServerOptions>>().Value` | default do framework — instância diferente |

**Decisão de resolução:**

- **Fonte única = storage.** Valores server-wide vêm de `storage.ServerOptions`; valores por realm, de `realm.Options` (via `context.Options` / `httpContext.GetCurrentRealm().Options`).
- **Não registrar `ServerOptions` em DI** — sempre obter do storage. (Avaliou-se uma factory storage-backed scoped; descartada por não ser necessária. Manter `ServerOptions` fora do DI ainda facilita o cache futuro — ver abaixo.)
- **Eliminar** os atalhos de DI, migrando cada consumidor para a fonte correta:
  - `GetRequiredService<ServerOptions>()` (`CheckSessionResult`) e `GetRequiredService<IOptions<ServerOptions>>()` (`ResponseToFormPostResult`) → leitura **realm-aware** via `httpContext.GetCurrentRealm()` (CSP é por realm).
  - campos capturados no construtor a partir de `storage.ServerOptions` → ler **em tempo de uso**.
  - `ContextItems.From(realm.Options.ServerOptions)` quando o item for usado para logging/CSP/limites/qualquer valor realm-specific → carregar a opção do realm, não o `ServerOptions` global.
- **Princípio geral:** ler options **em tempo de uso** (do realm/storage), nunca capturar na construção. `IStorage` é registrado como transient (orientado a sessão); serviços de vida longa que capturam `storage.ServerOptions` no construtor ficam stale se o storage passar a carregar por sessão.

> **Futuro (fora do escopo):** com storage de banco, os dados (incluindo `ServerOptions` e options de realm) devem passar por um **cache de TTL curto configurável**. Manter `ServerOptions` fora do DI deixa o cache num ponto único (o storage), em vez de espalhado por registros de container. Correlato: também falta um **entry point de configuração** do `ServerOptions` (binding/host) — sem ele, fallback global e copy-on-create são sempre defaults. Ambos ficam para depois.

### Padrao de Copia de RealmOptions (copy-on-create)

> Esta cópia é **higiene/correção** — impedir que realms compartilhem a instância estática de `ServerOptions` —, **não** a feature "Realm Templates". A feature (flag `IsTemplate`, deep-copy de clients/resources/scopes, CRUD via UI/API) permanece em `.ai/backlogs/backlog-001.md`. O construtor `RealmOptions(RealmOptions)` abaixo é o **groundwork** que a torna trivial depois.

Decisão fechada:

- **Dois construtores em `RealmOptions`, nenhum em `Realm`:**
  - `RealmOptions(ServerOptions server)` — copia o subconjunto **server-wide**: `Authentication`, `Csp`, `Logging`, `InputLengthRestrictions`, `Discovery`, `Endpoints`, `MutualTls`, `Keys`, `AccessTokenJwtType`, `EmitScopesAsSpaceDelimitedStringInJwt`, `DispatchEvents`. Cópia **parcial**: as options realm-only (`UI`/`RealmUIOptions`, `Caching`, `Account`, `Branding`) ficam em default — `ServerOptions.UI` é `ServerUIOptions` (tipo/conceito diferente de `RealmUIOptions`) e Caching/Account/Branding não têm contraparte no server.
  - `RealmOptions(RealmOptions other)` — copia **tudo** (todas as options têm o mesmo tipo nos dois lados). Cópia total, para clonar a partir de outro realm.
- **Identidade sempre explícita — não criar `Realm(Realm)`.** Clonar realm = passar identidade nova no construtor existente do `Realm`, alimentando-o com options copiadas:
  ```csharp
  new Realm(null, novoDomain, novoPath, novoDisplayName, false, new RealmOptions(source.Options));
  ```
  Assim `Id`/`Domain`/`Path`, `Routes`, `Enabled`, `Internal` (e o futuro `IsTemplate`) nascem frescos pelo ctor do `Realm`.
- **`ServerOptions` continua compartilhado.** Nos dois construtores, `ServerOptions = ...ServerOptions` mantém a instância global (template/fallback). Só os sub-options são clonados; nunca clonar o `ServerOptions` por realm.
- **Sub-options:** um construtor de cópia por tipo, recebendo **o próprio tipo** (`new CspOptions(source.Csp)`), nunca o `ServerOptions` inteiro. **Cópia profunda onde houver coleção** — entre as promovidas, isso inclui pelo menos `LoggingOptions.SensitiveValuesFilter`, `DiscoveryOptions.CustomEntries`, os `HashSet<string>` de `DiscoveryOptions` e `KeyOptions.SigningCredentialsAlgorithms`; para o caminho realm-source, conferir também `RealmUIOptions`, `CacheOptions`, `AccountOptions`, `PasswordOptions` e `RealmBrandingOptions`.
- **Mudança de comportamento a registrar:** hoje `Discovery`/`Endpoints`/`MutualTls`/`Keys` em `RealmOptions` são `= new()` (defaults de tipo, não herdam valores configurados do server). Ao copiar de `ServerOptions`, passam a herdar os configurados. Hoje é invisível (o host nunca configura `ServerOptions` — ver "Dívida de DI"), mas é uma escolha semântica intencional.
- **Manutenção:** copy-ctor à mão derruba campo novo silenciosamente; por isso a Fase 6 inclui um teste de **propagação** (não só de não-compartilhamento).

### CORS Parcialmente Iniciado

Estado atual:

- `Constants.Oidc.Routes.CorsPaths` lista endpoints que devem suportar CORS: discovery, JWKS, token, userinfo e revocation.
- `HttpRequestExtensions.GetCorsOrigin()` detecta requests cross-origin.
- Nao existe wiring de `AddCors`, `UseCors`, middleware proprio de CORS, `ICorsPolicy`, `ICorsPolicyProvider` ou politica por realm.

Decisao de design a validar antes de implementar:

- A politica deve ser realm-scoped, mas provavelmente nao deve viver apenas em `RealmOptions`.
- O modelo de `Client` hoje tem `RedirectUris` e `PostLogoutRedirectUris`, mas nao tem `AllowedCorsOrigins`.
- Para OIDC/OAuth, origens CORS geralmente sao uma permissao propria do client. Nao inferir automaticamente de `RedirectUris` sem decisao explicita.
- Proposta inicial: criar `RealmOptions.Cors` para defaults/feature flags do realm e adicionar `Client.AllowedCorsOrigins` para permissoes de origem por client.

Pontos de wiring que precisam entrar na decisao:

- Preferir middleware/policy service executando depois de `UseRealmDiscovery` e antes de `UseAuthentication`, para que preflight `OPTIONS` possa ser respondido antes da autenticacao e antes dos endpoints.
- Nao depender apenas de handlers de endpoint para preflight: hoje as rotas OIDC mapeadas nao cobrem `OPTIONS`.
- Normalizar e comparar origem por scheme, host e porta; nao comparar apenas host.
- Quando refletir origem permitida, responder `Vary: Origin`.
- `AllowCredentials` deve ser falso por default; se for verdadeiro, wildcard de origem nao pode ser aceito.
- Preflight deve validar `Access-Control-Request-Method` e, quando houver headers solicitados, `Access-Control-Request-Headers`.
- A regra para preflight sem `client_id` deve ser explicitamente aceita ou rejeitada; nao deixar comportamento implicito.

---

## Ordem de Execução e Dependências

As fases não são todas independentes. A ordem obrigatória:

1. **Fase 1 (decisão de modelo)** — pré-requisito de **todas** as demais. Define como cada consumidor resolve a opção efetiva; sem ela, as Fases 2-5 não têm padrão a seguir.
2. **Fases 2, 3, 4 e 5** — independentes entre si; podem ser executadas em qualquer ordem (ou em paralelo), desde que a Fase 1 esteja concluída.
3. **Fase 6 (testes de isolamento)** — depende de 2-5; cada teste exige a propriedade correspondente já migrada.

Notas:

- A **Fase 5 (CORS)** é a única majoritariamente nova: não depende de migração de opção existente, apenas da decisão de modelo para onde guardar `CorsOptions`.
- Dentro da **Fase 3**, **CSP é o item mais caro** (consumido em response handlers que resolvem opções por caminhos distintos — ver "Impedimento Estrutural"), enquanto `DispatchEvents` e `InputLengthRestrictions` são baratos mas de prioridade Baixa. Recomendado: atacar CSP logo após a Fase 2 e tratar os itens Baixa como incremento opcional.

---

## Fase 1: Auditoria Final e Decisao de Modelo

> **Precedente já existente no código (ponto de partida da decisão):** `RealmOptions` **já** duplica `Discovery`, `Endpoints`, `MutualTls`, `Keys` e `UI` como instâncias próprias e independentes (`= new()`), enquanto `RealmOptions.ServerOptions` mantém apenas a referência global compartilhada. Ou seja, a **opção A já está em uso** para as opções promovidas até hoje. A decisão abaixo deve confirmar/estender esse precedente ou justificar romper com ele — não tratá-lo como página em branco.

1. Confirmar todos os usos atuais de `ServerOptions`, cobrindo os quatro padrões de resolução (ver "Impedimento Estrutural"), não só a captura no construtor:
   ```powershell
   rg "storage\.ServerOptions|GetRequiredService<ServerOptions>|IOptions<ServerOptions>|GetOrCreate<ServerOptions>" RoyalIdentity --type cs
   ```
2. Para cada uso, registrar **qual padrão** (1-4) ele usa, além de classificar em uma destas categorias:
   - global de servidor, nao deve variar por realm;
   - default global com override por realm;
   - realm-only, deve sair do fluxo global em requests com realm.
3. Decidir o padrao de fallback (recomendado: **opção A**, seguindo o precedente já existente):
   - opcao A: `RealmOptions` possui valores independentes e `ServerOptions` e usado apenas como template de criacao;
   - opcao B: `RealmOptions` possui overrides nullable e um resolvedor monta opcoes efetivas.
4. Registrar a decisao no proprio plano antes de implementar as fases seguintes.

Tarefas marcaveis:

- [ ] Rodar a busca de `ServerOptions` e ampliar manualmente para usos indiretos (`context.ServerOptions`, `realm.Options.ServerOptions`, `ContextItems.From(...)`).
- [ ] Montar matriz por propriedade: destino, fallback, padrao atual, novo padrao de leitura e arquivos afetados.
- [ ] Confirmar opcao A ou B; recomendacao atual: opcao A com copy-on-create.
- [ ] Definir o ponto unico de criacao/copia de `RealmOptions` a partir de `ServerOptions`.
- [ ] Definir como requests sem realm continuam usando defaults globais.
- [ ] Decidir se `CheckSessionEndpoint` sera mapeado nesta refatoracao ou se seus testes ficam adiados.
- [ ] Registrar a decisao final no proprio plano antes da Fase 2.

Critério de aceite: cada propriedade da tabela deve ter destino definido, **padrão de resolução de cada call site identificado**, call sites mapeados, contrato de fallback documentado e estratégia de cópia de `RealmOptions` definida.

---

## Fase 2: Autenticacao e UI por Realm

Arquivos prováveis:

- `RoyalIdentity/Options/RealmOptions.cs`
- `RoyalIdentity/Options/AuthenticationOptions.cs`
- `RoyalIdentity/Authentication/ConfigureRealmCookieAuthenticationOptions.cs`
- `RoyalIdentity/Options/RealmUIOptions.cs`
- `RoyalIdentity/Options/ServerUIOptions.cs`

Passos:

1. Adicionar `AuthenticationOptions` a `RealmOptions`, seguindo a decisão da Fase 1.
2. Promover ou resolver `AuthenticationOptions` por realm.
3. Garantir que cookie lifetime, sliding expiration, SameSite e nome de cookie usem o realm correto.
4. Revisar `AccessDeniedPath`: hoje vem de `ServerUIOptions` (`storage.ServerOptions.UI.AccessDeniedPath`), enquanto `LoginPath`/`LogoutPath` já vêm de `realm.Routes` e `LoginParameter` de `RealmUIOptions`. Decidir se `AccessDeniedPath` migra para `RealmUIOptions`/`realm.Routes`.
5. Adicionar testes com dois realms usando lifetimes/paths diferentes.

> **Nota de implementação:** em `ConfigureRealmCookieAuthenticationOptions`, o realm **já é resolvido** (`storage.Realms.GetByPath(realmPath)`), porém **depois** de o nome/SameSite/lifetime do cookie já terem sido atribuídos a partir do `ServerOptions` global. Para usar `AuthenticationOptions` por realm será preciso **reordenar**: resolver o realm primeiro e então derivar as opções de cookie de `realm.Options.Authentication`.

Tarefas marcaveis:

- [ ] Adicionar `AuthenticationOptions` a `RealmOptions` conforme o contrato da Fase 1.
- [ ] Atualizar a criacao/copia de `RealmOptions` para copiar defaults de autenticacao sem compartilhar a instancia global.
- [ ] Reordenar `ConfigureRealmCookieAuthenticationOptions` para resolver o realm antes de aplicar nome, SameSite, lifetime e sliding expiration.
- [ ] Decidir e implementar o destino de `AccessDeniedPath` (`RealmUIOptions`, `realm.Routes` ou fallback global documentado).
- [ ] Criar testes com dois realms usando lifetimes e paths diferentes.

Critério de aceite: `ConfigureRealmCookieAuthenticationOptions` nao deve depender de `storage.ServerOptions.Authentication` para decisoes que variam por realm, e os cookies de dois realms devem refletir configuracoes independentes.

---

## Fase 3: CSP, Logging, Eventos e Limites

> Antes de implementar, mapear o **padrão de resolução** (1-4) de cada consumidor abaixo — ver "Impedimento Estrutural". O grosso do trabalho desta fase está em consumidores fora do Padrão 1.

Arquivos prováveis:

- `RoyalIdentity/Options/RealmOptions.cs`
- `RoyalIdentity/Options/CspOptions.cs`, `LoggingOptions.cs`, `InputLengthRestrictions.cs`
- **CSP (consumidor vivo):** `RoyalIdentity/Responses/HttpResults/ResponseToFormPostResult.cs` (Padrão 3). `CheckSessionResult.cs` (Padrão 2) deve ser migrado/testado somente se `CheckSessionEndpoint` for mapeado nesta refatoração; caso contrário, registrar como dívida de código inalcançável.
- **Logging (consumidor real):** `RoyalIdentity/Extensions/LoggerExtensions.cs` (Padrão 4 — lê de `ContextItems`)
- **Eventos:** `RoyalIdentity/Contracts/Defaults/DefaultEventDispatcher.cs`
- **InputLengthRestrictions (consumidores reais, além de validators):** `Endpoints/TokenEndpoint.cs`, `Contracts/Defaults/SecretsEvaluators/*`, decorators `LoadClient.cs`, `EvaluateBearerToken.cs`, `LoadCode.cs`, `LoadRefreshToken.cs`

Passos:

1. **CSP** (prioridade Alta) — resolver `CspOptions` por realm. O consumidor HTTP observável hoje é `ResponseToFormPostResult`, um response handler sem `context.Options`; resolver via `httpContext.GetCurrentRealm()`. `CheckSessionResult` lê tanto `.Csp` quanto `.Authentication` do mesmo `ServerOptions` global, mas só deve entrar nesta fase se `CheckSessionEndpoint` for mapeado; se não for, registrar como dívida técnica.
2. **Logging** — `LoggerExtensions` lê `ServerOptions` do `ContextItems`. Para tornar per-realm: ou colocar o `LoggingOptions`/`RealmOptions` correto no `ContextItems` na criação do contexto, ou ler o realm diretamente. Não quebrar o logging de requests sem realm.
3. **DispatchEvents** — o overload `DispatchAsync(evt, realm)` **já existe**, mas delega para `DispatchAsync(evt)`, que checa o **global** `options.DispatchEvents`. Mover a propriedade para `RealmOptions` exige **mover o check para o caminho realm-aware** (o overload sem realm permanece no fallback global).
4. **InputLengthRestrictions** (prioridade Baixa) — consumido em ~14 sites (endpoints, secret evaluators, decorators e validators), não só em validators. Dado o custo/benefício, considerar **adiar** ou tratar como incremento isolado.
5. Adicionar testes focados no comportamento observavel, nao apenas em propriedades.

Tarefas marcaveis:

- [ ] Promover `CspOptions`, `LoggingOptions`, `DispatchEvents` e, se aprovado, `InputLengthRestrictions` para `RealmOptions`.
- [ ] Atualizar a criacao/copia de `RealmOptions` para copiar esses defaults sem compartilhar instancias globais.
- [ ] Alterar `ResponseToFormPostResult` para nao depender de `IOptions<ServerOptions>` em requests com realm.
- [ ] Se `CheckSessionEndpoint` for mapeado nesta refatoracao, alterar tambem `CheckSessionResult` para nao depender de `ServerOptions`; se nao for, registrar `CheckSessionResult` como divida de codigo inalcançavel.
- [ ] Confirmar se `CheckSessionEndpoint` sera mapeado agora; se nao for, nao usar check-session como teste HTTP obrigatorio desta fase.
- [ ] Alterar logging para usar `context.Options.Logging`, `RealmOptions` no `ContextItems`, ou outra fonte realm-aware definida na Fase 1.
- [ ] Alterar `DefaultEventDispatcher.DispatchAsync(evt, realm)` para aplicar o gate de eventos por realm antes de despachar aos observers.
- [ ] Ajustar testes de eventos para observar dispatch apos o gate; nao usar captura que registra evento antes do dispatcher interno aplicar `DispatchEvents`.
- [ ] Se `InputLengthRestrictions` for migrado, atualizar endpoints, decorators, validators e secret evaluators que hoje usam `ServerOptions`.

Critério de aceite: um realm deve conseguir ter CSP/event dispatch/input limits diferentes de outro sem recriar o servidor, e nenhum consumidor migrado deve depender de `ServerOptions` global para decisoes realm-specific.

---

## Fase 4: Formato de Token por Realm

Arquivos prováveis:

- `RoyalIdentity/Contracts/Defaults/DefaultJwtFactory.cs`
- `RoyalIdentity/Contracts/Defaults/DefaultTokenValidator.cs`
- `RoyalIdentity/Contracts/Defaults/DefaultTokenFactory.cs`
- `RoyalIdentity/Options/RealmOptions.cs`

Passos:

1. Resolver `AccessTokenJwtType` por realm no momento de criar JWT. **Facilitador:** `DefaultJwtFactory.CreateHeaderAsync(Realm realm, ...)` já recebe o realm exatamente onde `AccessTokenJwtType` é usado — basta ler `realm.Options...` em vez do campo `options` capturado.
2. Resolver `EmitScopesAsSpaceDelimitedStringInJwt` por realm no momento de serializar scopes. **Atenção:** isso é usado em `CreatePayloadAsync`, que **não recebe** o realm hoje — será preciso propagar o `Realm` (ou o valor já resolvido) até esse método.
3. Remover capturas de `storage.ServerOptions` onde o valor usado depende do realm da request.
4. Criar testes com dois realms emitindo tokens com formatos diferentes.

Tarefas marcaveis:

- [ ] Adicionar `AccessTokenJwtType` e `EmitScopesAsSpaceDelimitedStringInJwt` a `RealmOptions`, conforme a decisao da Fase 1.
- [ ] Atualizar a criacao/copia de `RealmOptions` para copiar os defaults de formato de token sem compartilhar estado global.
- [ ] Alterar `DefaultJwtFactory.CreateHeaderAsync` para ler `realm.Options.AccessTokenJwtType`.
- [ ] Propagar `Realm` ou valor resolvido ate `CreatePayloadAsync`/`CreateJwtPayload` para serializacao de scopes.
- [ ] Alterar `DefaultTokenValidator` para validar o `typ` esperado usando a opcao do realm que esta validando o token.
- [ ] Remover capturas de `storage.ServerOptions` usadas apenas para formato de token ou limites migrados.
- [ ] Criar testes com dois realms emitindo JWTs com `typ` e formato de `scope` diferentes.

Critério de aceite: tokens emitidos por realm A e realm B refletem as opcoes de seus proprios realms, e a validacao de token usa o `typ` esperado do realm em que a validacao esta ocorrendo.

---

## Fase 5: CORS por Realm e por Client

Arquivos prováveis:

- `RoyalIdentity/Options/CorsOptions.cs` (novo)
- `RoyalIdentity/Options/RealmOptions.cs`
- `RoyalIdentity/Models/Client.cs`
- `RoyalIdentity/Options/Constants.cs`
- `RoyalIdentity/Extensions/HttpRequestExtensions.cs`
- novo middleware ou policy service em `RoyalIdentity/Authentication/` ou `RoyalIdentity/Contracts/`

Modelo sugerido:

```csharp
public class CorsOptions
{
    public bool Enabled { get; set; }
    public HashSet<string> AllowedOrigins { get; } = [];
    public HashSet<string> AllowedHeaders { get; } = [];
    public HashSet<string> AllowedMethods { get; } = [];
    public bool AllowCredentials { get; set; }
}
```

Adicionar ao client:

```csharp
public HashSet<string> AllowedCorsOrigins { get; } = [];
```

Regras sugeridas:

1. CORS so deve ser avaliado para endpoints presentes em `Constants.Oidc.Routes.CorsPaths`.
2. Preflight `OPTIONS` deve ser respondido antes do endpoint quando a origem for permitida.
3. Uma origem e permitida quando:
   - esta em `realm.Options.Cors.AllowedOrigins`; ou
   - esta em `client.AllowedCorsOrigins`, quando o client puder ser identificado; ou
   - esta em algum client do realm para preflight sem `client_id`, se essa regra for explicitamente aceita.
4. Nao usar `RedirectUris` como substituto automatico para `AllowedCorsOrigins` sem uma decisao documentada.
5. Responder com `Vary: Origin` quando refletir a origem.

Tarefas marcaveis:

- [ ] Criar `CorsOptions` e adicionar `RealmOptions.Cors`, com `Enabled = false` por default salvo decisao contraria registrada.
- [ ] Adicionar `Client.AllowedCorsOrigins` sem inferir valores de `RedirectUris`.
- [ ] Criar policy service ou middleware CORS realm-aware no projeto `RoyalIdentity`, sem dependencia do storage in-memory.
- [ ] Posicionar o wiring depois de `UseRealmDiscovery` e antes de `UseAuthentication`.
- [ ] Responder preflight `OPTIONS` para paths presentes em `Constants.Oidc.Routes.CorsPaths`.
- [ ] Validar origem por scheme/host/porta e validar `Access-Control-Request-Method`.
- [ ] Validar headers solicitados quando `Access-Control-Request-Headers` estiver presente.
- [ ] Emitir `Vary: Origin` quando refletir origem permitida.
- [ ] Bloquear wildcard quando `AllowCredentials = true`.
- [ ] Documentar e testar a decisao para preflight sem `client_id`.

Critério de aceite: requests cross-origin entre realms nao podem vazar permissoes; origem permitida em realm A nao deve ser aceita automaticamente em realm B; preflight permitido deve retornar os headers CORS esperados; preflight negado nao deve refletir a origem.

---

## Fase 6: Testes de Isolamento e Regressao

Adicionar testes em `Tests.Integration`, preferencialmente em uma classe dedicada:

1. `AuthenticationOptions_RealmA_DoesNotAffectRealmB`
2. `CspOptions_UsesRealmSpecificPolicy`
3. `TokenFormat_RealmSpecificJwtType`
4. `TokenFormat_RealmSpecificScopeSerialization`
5. `Events_DispatchEventsFalse_DisablesOnlyThatRealm`
6. `Cors_Preflight_WhenOriginAllowedByRealm_ReturnsCorsHeaders`
7. `Cors_Preflight_WhenOriginAllowedOnlyInOtherRealm_IsRejected`
8. `Cors_ActualRequest_WhenOriginAllowedByClient_ReturnsCorsHeaders`
9. `Cors_DoesNotInferRedirectUris_AsAllowedCorsOrigins`
10. `RealmOptions_CopyOnCreate_DoesNotSharePromotedOptions` — mutar uma option promovida no realm A nao afeta realm B nem os defaults globais (sem referencia compartilhada).
11. `RealmOptions_CopyFromServer_PropagatesConfiguredValues` — setar valor nao-default em `ServerOptions`, criar realm e provar que o realm recebeu o valor (pega campo esquecido no copy-ctor).
12. `RealmOptions_CopyFromRealm_IsIndependent` — criar realm B via `new RealmOptions(a.Options)`; mutar A e provar que B nao muda (groundwork de Realm Templates).
13. `CheckSession_UsesRealmSpecificCspAndCookieName` — somente se `CheckSessionEndpoint` for mapeado nesta refatoracao.

Notas para testes:

- Testes de eventos devem observar observers ou resultado apos o gate de `DispatchEvents`; nao devem depender de captura que registra o evento antes do dispatcher interno decidir se despacha.
- Testes de CORS devem cobrir preflight e actual request, origem permitida por realm, origem permitida por client e origem permitida apenas em outro realm.
- Testes de copy-on-create devem alterar uma opcao promovida em realm A e provar que realm B e os defaults globais nao mudaram por referencia compartilhada.

Tarefas marcaveis:

- [ ] Criar classe dedicada em `Tests.Integration` para os testes de RealmOptions/CORS.
- [ ] Adicionar helper de criacao de realm com opcoes independentes.
- [ ] Adicionar helpers para token request, preflight CORS e actual CORS request quando houver duplicacao.
- [ ] Executar o recorte focado de testes.
- [ ] Executar `dotnet test RoyalIdentity.sln --no-restore` antes de concluir o plano.

Critério de aceite final:

```powershell
dotnet test RoyalIdentity.sln --no-restore
```

Se o ambiente local bloquear algum logger/plataforma, registrar a limitacao e rodar pelo menos o recorte:

```powershell
dotnet test Tests.Integration/Tests.Integration.csproj --no-restore --filter "Cors|RealmOptions|TokenFormat|AuthenticationOptions"
```

---

## Como Marcar Progresso

- Uma fase so pode ser marcada como concluida quando todas as suas tarefas marcaveis estiverem feitas e o criterio de aceite da fase estiver satisfeito.
- Fase 1 pode ser concluida com documentacao/decisao, sem alteracao de codigo, desde que a matriz e o contrato de resolucao estejam registrados.
- Fases 2 a 5 exigem implementacao e teste focado correspondente.
- Fase 6 exige os testes de isolamento/regressao e o comando final, ou registro claro do motivo pelo qual o comando final nao pode rodar localmente.
- Ao concluir uma fase, atualizar `Progresso` no topo de `0 de 6` para a contagem real e substituir um `-` por `█` na barra visual (mesmo caractere usado em `plan-realm-hardening.md`).
