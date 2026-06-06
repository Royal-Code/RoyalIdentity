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

Varios servicos resolvem `storage.ServerOptions` uma vez no construtor, via DI, e ficam cegos para overrides por realm:

- `DefaultJwtFactory`
- `DefaultTokenValidator`
- `DefaultEventDispatcher`
- `EvaluateBearerToken`
- `LoadClient`
- `AuthorizeMainValidator`
- `RedirectUriValidator`
- `SecretEvaluatorBase`
- `ConfigureRealmCookieAuthenticationOptions`

Quando uma propriedade migrar para `RealmOptions`, esses servicos devem ler a opcao efetiva a partir do contexto da request, por exemplo `context.Options`, `context.ServerOptions` enquanto ainda for fallback global, ou um resolvedor explicito de opcoes efetivas.

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

## Fase 1: Auditoria Final e Decisao de Modelo

1. Confirmar todos os usos atuais de `storage.ServerOptions`:
   ```powershell
   rg "storage\.ServerOptions|ServerOptions" RoyalIdentity --type cs
   ```
2. Classificar cada uso em uma destas categorias:
   - global de servidor, nao deve variar por realm;
   - default global com override por realm;
   - realm-only, deve sair do fluxo global em requests com realm.
3. Decidir o padrao de fallback:
   - opcao A: `RealmOptions` possui valores independentes e `ServerOptions` e usado apenas como template de criacao;
   - opcao B: `RealmOptions` possui overrides nullable e um resolvedor monta opcoes efetivas.
4. Registrar a decisao no proprio plano antes de implementar as fases seguintes.

Critério de aceite: cada propriedade da tabela deve ter destino definido e call sites mapeados.

---

## Fase 2: Autenticacao e UI por Realm

Arquivos prováveis:

- `RoyalIdentity/Options/RealmOptions.cs`
- `RoyalIdentity/Options/AuthenticationOptions.cs`
- `RoyalIdentity/Authentication/ConfigureRealmCookieAuthenticationOptions.cs`
- `RoyalIdentity/Options/RealmUIOptions.cs`
- `RoyalIdentity/Options/ServerUIOptions.cs`

Passos:

1. Promover ou resolver `AuthenticationOptions` por realm.
2. Garantir que cookie lifetime, sliding expiration, SameSite e nome de cookie usem o realm correto.
3. Revisar `AccessDeniedPath`: se for uma rota de UI por realm, deve vir de `RealmOptions.UI` ou de um resolvedor de UI efetiva.
4. Adicionar testes com dois realms usando lifetimes/paths diferentes.

Critério de aceite: `ConfigureRealmCookieAuthenticationOptions` nao deve depender de `storage.ServerOptions.Authentication` para decisoes que variam por realm.

---

## Fase 3: CSP, Logging, Eventos e Limites

Arquivos prováveis:

- `RoyalIdentity/Options/RealmOptions.cs`
- `RoyalIdentity/Options/CspOptions.cs`
- `RoyalIdentity/Options/LoggingOptions.cs`
- `RoyalIdentity/Options/InputLengthRestrictions.cs`
- `RoyalIdentity/Contracts/Defaults/DefaultEventDispatcher.cs`
- validators em `RoyalIdentity/Contexts/Validators/`

Passos:

1. Resolver `CspOptions` por realm, considerando dominio, branding e assets externos.
2. Avaliar `LoggingOptions` por realm sem quebrar o logging global da aplicacao.
3. Avaliar `DispatchEvents` por realm no `DefaultEventDispatcher`.
4. Avaliar `InputLengthRestrictions` nos validators que dependem de limites.
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

1. Resolver `AccessTokenJwtType` por realm no momento de criar JWT.
2. Resolver `EmitScopesAsSpaceDelimitedStringInJwt` por realm no momento de serializar scopes.
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
