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

Critério de aceite: cada propriedade da tabela deve ter destino definido, **padrão de resolução de cada call site identificado**, e os call sites mapeados.

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

Critério de aceite: `ConfigureRealmCookieAuthenticationOptions` nao deve depender de `storage.ServerOptions.Authentication` para decisoes que variam por realm.

---

## Fase 3: CSP, Logging, Eventos e Limites

> Antes de implementar, mapear o **padrão de resolução** (1-4) de cada consumidor abaixo — ver "Impedimento Estrutural". O grosso do trabalho desta fase está em consumidores fora do Padrão 1.

Arquivos prováveis:

- `RoyalIdentity/Options/RealmOptions.cs`
- `RoyalIdentity/Options/CspOptions.cs`, `LoggingOptions.cs`, `InputLengthRestrictions.cs`
- **CSP (consumidores reais):** `RoyalIdentity/Responses/HttpResults/CheckSessionResult.cs` (Padrão 2), `RoyalIdentity/Responses/HttpResults/ResponseToFormPostResult.cs` (Padrão 3)
- **Logging (consumidor real):** `RoyalIdentity/Extensions/LoggerExtensions.cs` (Padrão 4 — lê de `ContextItems`)
- **Eventos:** `RoyalIdentity/Contracts/Defaults/DefaultEventDispatcher.cs`
- **InputLengthRestrictions (consumidores reais, além de validators):** `Endpoints/TokenEndpoint.cs`, `Contracts/Defaults/SecretsEvaluators/*`, decorators `LoadClient.cs`, `EvaluateBearerToken.cs`, `LoadCode.cs`, `LoadRefreshToken.cs`

Passos:

1. **CSP** (prioridade Alta) — resolver `CspOptions` por realm. Os consumidores são response handlers sem `context.Options`; resolver via `httpContext.GetCurrentRealm()`. Atenção: `CheckSessionResult` lê tanto `.Csp` quanto `.Authentication` do mesmo `ServerOptions` global — alinhar com a Fase 2.
2. **Logging** — `LoggerExtensions` lê `ServerOptions` do `ContextItems`. Para tornar per-realm: ou colocar o `LoggingOptions`/`RealmOptions` correto no `ContextItems` na criação do contexto, ou ler o realm diretamente. Não quebrar o logging de requests sem realm.
3. **DispatchEvents** — o overload `DispatchAsync(evt, realm)` **já existe**, mas delega para `DispatchAsync(evt)`, que checa o **global** `options.DispatchEvents`. Mover a propriedade para `RealmOptions` exige **mover o check para o caminho realm-aware** (o overload sem realm permanece no fallback global).
4. **InputLengthRestrictions** (prioridade Baixa) — consumido em ~14 sites (endpoints, secret evaluators, decorators e validators), não só em validators. Dado o custo/benefício, considerar **adiar** ou tratar como incremento isolado.
5. Adicionar testes focados no comportamento observavel, nao apenas em propriedades.

Critério de aceite: um realm deve conseguir ter CSP/event dispatch/input limits diferentes de outro sem recriar o servidor.

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

Critério de aceite: tokens emitidos por realm A e realm B refletem as opcoes de seus proprios realms.

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

Critério de aceite: requests cross-origin entre realms nao podem vazar permissoes; origem permitida em realm A nao deve ser aceita automaticamente em realm B.

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

Critério de aceite final:

```powershell
dotnet test RoyalIdentity.sln --no-restore
```

Se o ambiente local bloquear algum logger/plataforma, registrar a limitacao e rodar pelo menos o recorte:

```powershell
dotnet test Tests.Integration/Tests.Integration.csproj --no-restore --filter "Cors|RealmOptions|TokenFormat|AuthenticationOptions"
```
