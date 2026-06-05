# Plan: Realm Hardening & Configuration

## Status: PENDING

## Progresso

`░░░░░░░░░░` **0%** — 0 de 8 fases concluídas

---

## Contexto

Este plano cobre o endurecimento do isolamento multi-tenant por realm e extensões de configuração.
As três grandes lacunas identificadas: tokens não carregam `RealmId` (risk de cross-realm), eventos não
têm contexto de realm, e branding por realm não existe. As demais fases são menores (typo, middleware
error, IRealmManager, testes de isolamento).

---

## Fase 1: Typo `DysplayName` → `DisplayName`

**Arquivo:** `RoyalIdentity/Users/IdentityUser.cs`

1. Renomear a propriedade `DysplayName` para `DisplayName` e atualizar o comment do `<summary>`.
2. Pesquisar `DysplayName` em todo o projeto para encontrar callers:
   ```
   rg "DysplayName" --type cs
   ```
3. Atualizar cada caller encontrado.
4. Build para confirmar zero erros.

> Fases seguintes são independentes entre si (1, 2, 4, 5, 6, 7). A Fase 3 é pré-requisito para a Fase 8.

---

## Fase 2: Error Handling no `RealmDiscoveryMiddleware`

**Arquivo:** `RoyalIdentity/Authentication/RealmDiscoveryMiddleware.cs`

Hoje o middleware retorna um bare 404 silencioso quando o realm não é encontrado. Corrigir para:

1. Resolver `IEventDispatcher` via `context.RequestServices` (scoped por request).
2. Despachar um evento de falha usando o dispatcher (se registrado — `GetService`, não `GetRequiredService`).
3. Retornar JSON `application/json` com body:
   ```json
   { "error": "realm_not_found", "error_description": "The realm '{realm}' was not found" }
   ```
4. Manter o status HTTP 404.

Implementação sugerida para o bloco `else`:
```csharp
else
{
    var dispatcher = context.RequestServices.GetService<IEventDispatcher>();
    if (dispatcher is not null)
        await dispatcher.DispatchAsync(new RealmNotFoundEvent(realm));

    context.Response.StatusCode = StatusCodes.Status404NotFound;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new
    {
        error = "realm_not_found",
        error_description = $"The realm '{realm}' was not found"
    });
}
```

5. Criar `RealmNotFoundEvent` em `RoyalIdentity/Events/` seguindo o padrão de `Event`:
   ```csharp
   public class RealmNotFoundEvent(string realm)
       : Event(EventCategories.Authentication, "Realm Not Found", EventTypes.Failure)
   {
       public string Realm { get; } = realm;
   }
   ```
6. Build + verificar que a resposta é retornada corretamente via teste manual ou `Tests.Integration`.

---

## Fase 3: Isolamento dos Token Stores por Realm

Esta é a maior fase. O objetivo é mover os 4 stores globais de `IStorage` para o padrão realm-aware já estabelecido para clients/users/keys/resources.

### 3.1 — Adicionar `RealmId` aos modelos

**`RoyalIdentity/Models/Tokens/TokenBase.cs`** — adicionar:
```csharp
/// <summary>
/// The realm this token belongs to.
/// </summary>
public string? RealmId { get; set; }
```

Isso cobre `AccessToken` e `RefreshToken` que herdam de `TokenBase`.

**`RoyalIdentity/Models/Tokens/AuthorizationCode.cs`** — adicionar:
```csharp
/// <summary>
/// The realm this authorization code belongs to.
/// </summary>
public string? RealmId { get; set; }
```

**`RoyalIdentity/Models/Consent.cs`** — adicionar:
```csharp
/// <summary>
/// The realm this consent belongs to.
/// </summary>
public required string RealmId { get; set; }
```

> `Consent` usa `required` para os campos de identidade — `RealmId` também deve ser `required` para forçar atribuição na criação.

### 3.2 — Atualizar `IStorage`

**`RoyalIdentity/Contracts/Storage/IStorage.cs`** — substituir as propriedades globais por métodos realm-aware:

Remover:
```csharp
IAccessTokenStore AccessTokens { get; }
IRefreshTokenStore RefreshTokens { get; }
IAuthorizationCodeStore AuthorizationCodes { get; }
IUserConsentStore UserConsents { get; }
```

Adicionar:
```csharp
/// <summary>Gets the access token store for the given realm.</summary>
IAccessTokenStore GetAccessTokenStore(Realm realm);

/// <summary>Gets the refresh token store for the given realm.</summary>
IRefreshTokenStore GetRefreshTokenStore(Realm realm);

/// <summary>Gets the authorization code store for the given realm.</summary>
IAuthorizationCodeStore GetAuthorizationCodeStore(Realm realm);

/// <summary>Gets the user consent store for the given realm.</summary>
IUserConsentStore GetUserConsentStore(Realm realm);
```

> `IAuthorizeParametersStore AuthorizeParameters` permanece global — as chaves são GUIDs aleatórios; o risco de colisão cross-realm é insignificante e a migração não vale o custo neste momento.

### 3.3 — Atualizar `MemoryStorage`

**`RoyalIdentity.Storage.InMemory/RealmMemoryStore.cs`** — adicionar quatro dicionários:
```csharp
public ConcurrentDictionary<string, AccessToken> AccessTokens { get; } = new();
public ConcurrentDictionary<string, RefreshToken> RefreshTokens { get; } = new();
public ConcurrentDictionary<string, AuthorizationCode> AuthorizationCodes { get; } = new();
public ConcurrentDictionary<string, Consent> UserConsents { get; } = new();
```

**`RoyalIdentity.Storage.InMemory/MemoryStorage.Storage.cs`** — implementar os 4 novos métodos (usando `GetRealmMemoryStore`, o helper existente na linha 32):
```csharp
public IAccessTokenStore GetAccessTokenStore(Realm realm)
    => new AccessTokenStore(GetRealmMemoryStore(realm).AccessTokens);

public IRefreshTokenStore GetRefreshTokenStore(Realm realm)
    => new RefreshTokenStore(GetRealmMemoryStore(realm).RefreshTokens);

public IAuthorizationCodeStore GetAuthorizationCodeStore(Realm realm)
    => new AuthorizationCodeStore(GetRealmMemoryStore(realm).AuthorizationCodes);

public IUserConsentStore GetUserConsentStore(Realm realm)
    => new UserConsentStore(GetRealmMemoryStore(realm).UserConsents);
```

Remover as propriedades globais `AccessTokens`, `RefreshTokens`, `AuthorizationCodes`, `Consents` de `MemoryStorage.cs` (campos `ConcurrentDictionary` declarados no partial) após migrar todos os callers.

### 3.4 — Atualizar todos os callers dos stores

O rg deve cobrir todos os callers — não só handlers, mas também factories, services e decorators:

```
rg "\.AccessTokens\b|\.RefreshTokens\b|\.AuthorizationCodes\b|\.UserConsents\b" --type cs
```

Callers conhecidos (confirmar com rg — pode haver outros):

| Arquivo | Uso atual | Realm disponível via |
|---|---|---|
| `DefaultTokenFactory.cs` | `storage.AccessTokens.StoreAsync(token, ct)` | `request.Client.Realm` |
| `DefaultCodeFactory.cs` | `storage.AuthorizationCodes.StoreAuthorizationCodeAsync(code, ct)` | `context.Realm` |
| `DefaultConsentService.cs` | `storage.UserConsents.GetUserConsentAsync(...)` e `StoreUserConsentAsync(...)` | `client.Realm` |
| `LoadCode.cs` | `storage.AuthorizationCodes.GetAuthorizationCodeAsync(code, ct)` | `context.Realm` |
| `LoadRefreshToken.cs` | `storage.RefreshTokens.GetAsync(token, ct)` | `context.Realm` |
| `RefreshTokenHandler.cs` | `storage.AccessTokens.GetAsync(...)`, `StoreAsync(...)` e `storage.RefreshTokens.UpdateAsync(...)` | `context.Realm` |
| `RevocationHandler.cs` | `storage.AccessTokens.*` e `storage.RefreshTokens.*` (em helpers privados — ver nota abaixo) | `context.Realm` (passar para os helpers) |
| `DefaultTokenValidator.cs` | `storage.AccessTokens.GetAsync(jti, ct)` em `ValidateReferenceAccessTokenAsync` | recebe `Realm realm` como parâmetro — usar diretamente |

Para cada ocorrência, substituir pelo método realm-aware correspondente usando o realm indicado na coluna da direita:

```csharp
// antes (exemplo)
storage.AuthorizationCodes.GetAuthorizationCodeAsync(code, ct)
// depois
storage.GetAuthorizationCodeStore(context.Realm).GetAuthorizationCodeAsync(code, ct)
```

> Os nomes dos métodos dentro dos stores **não mudam** — apenas o acesso ao store passa a ser por realm.

> **RevocationHandler — helpers privados:** os métodos `RevokeAccessTokenAsync(string token, string clientId, ct)` e `RevokeRefreshTokenAsync(string token, string clientId, ct)` chamam `storage.AccessTokens` e `storage.RefreshTokens` diretamente, mas não recebem realm. A solução é adicionar `Realm realm` como parâmetro nesses helpers, ou resolver os stores no método público `Handle(context, ct)` e passá-los já resolvidos. Preferir a segunda opção para não poluir a assinatura dos helpers:
> ```csharp
> // No Handle público, resolver uma vez:
> var accessTokenStore = storage.GetAccessTokenStore(context.Realm);
> var refreshTokenStore = storage.GetRefreshTokenStore(context.Realm);
> // Passar para os helpers:
> await RevokeAccessTokenAsync(accessTokenStore, token, clientId, ct);
> await RevokeRefreshTokenAsync(refreshTokenStore, accessTokenStore, token, clientId, ct);
> ```

### 3.5 — Atribuir `RealmId` na criação dos tokens (nas factories)

A atribuição de `RealmId` deve acontecer nos **factories**, não nos handlers — é onde os tokens são instanciados e persistidos:

- **`DefaultTokenFactory.cs`** — acesso token e identity token criados aqui. Atribuir `token.RealmId = request.Client.Realm.Id` antes de `storage.GetAccessTokenStore(request.Client.Realm).StoreAsync(token, ct)`.
- **`DefaultCodeFactory.cs`** — authorization code criado aqui. Atribuir `code.RealmId = context.Realm.Id` no objeto `new AuthorizationCode(...)` antes de `StoreAuthorizationCodeAsync`.
- **`DefaultConsentService.cs`** — consent gravado aqui. Ao criar ou atualizar um `Consent`, garantir `consent.RealmId = client.Realm.Id`.

**`AccessToken.Renew` deve copiar `RealmId`:** o método `Renew` em `RoyalIdentity/Models/Tokens/AccessToken.cs` cria um novo `AccessToken` copiando campos do original via object initializer. `RealmId` (herdado de `TokenBase`) não é parâmetro do construtor, portanto não é copiado automaticamente. Adicionar `RealmId = RealmId` ao initializer:
```csharp
var newToken = new AccessToken(ClientId, Issuer, AccessTokenType, creationTime, lifetime, jti, TokenType)
{
    AllowedSigningAlgorithms = AllowedSigningAlgorithms,
    Confirmation = Confirmation,
    Audiences = Audiences,
    RealmId = RealmId,   // ← adicionar
};
```
Sem isso, o access token renovado via refresh token perde a informação de realm.

### 3.6 — Build + testes

```bash
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

---

## Fase 4: `IRealmStore` Write Methods + `IRealmManager`

Preparação da camada de domínio para CRUD de realms (sem endpoints ainda).

### 4.1 — Estender `IRealmStore`

**`RoyalIdentity/Contracts/Storage/IRealmStore.cs`** — adicionar:
```csharp
/// <summary>Saves (creates or updates) a realm.</summary>
ValueTask SaveAsync(Realm realm, CancellationToken ct = default);

/// <summary>
/// Deletes a realm by ID. Returns false if not found or if realm is Internal.
/// </summary>
ValueTask<bool> DeleteAsync(string realmId, CancellationToken ct = default);
```

### 4.2 — Implementar em `MemoryStorage`

**Problema estrutural:** `RealmStore` hoje recebe apenas `ConcurrentDictionary<string, Realm> realms`. Mas a memória por realm vive em `realmMemoryStore: ConcurrentDictionary<string, RealmMemoryStore>` gerenciado por `MemoryStorage.Storage.cs`. Para que `SaveAsync` inicialize o store de dados do novo realm, `RealmStore` precisa também de `realmMemoryStore`.

**Solução:** Passar ambos os dicionários no construtor:

```csharp
// RealmStore.cs — atualizar construtor
public class RealmStore : IRealmStore
{
    private readonly ConcurrentDictionary<string, Realm> realms;
    private readonly ConcurrentDictionary<string, RealmMemoryStore> realmDataStore;

    public RealmStore(
        ConcurrentDictionary<string, Realm> realms,
        ConcurrentDictionary<string, RealmMemoryStore> realmDataStore)
    { ... }

    public ValueTask SaveAsync(Realm realm, CancellationToken ct = default)
    {
        realms.AddOrUpdate(realm.Id, realm, (_, _) => realm);
        realmDataStore.TryAdd(realm.Id, new RealmMemoryStore(realm, false));
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> DeleteAsync(string realmId, CancellationToken ct = default)
    {
        if (!realms.TryGetValue(realmId, out var realm) || realm.Internal)
            return ValueTask.FromResult(false);
        realms.TryRemove(realmId, out _);
        realmDataStore.TryRemove(realmId, out _);
        return ValueTask.FromResult(true);
    }
}
```

**`MemoryStorage.Storage.cs`** — atualizar a linha que instancia `RealmStore`:
```csharp
// antes
IRealmStore IStorage.Realms => new RealmStore(Realms);
// depois
IRealmStore IStorage.Realms => new RealmStore(Realms, realmMemoryStore);
```

### 4.3 — Criar `IRealmManager`

**Arquivo:** `RoyalIdentity/Contracts/IRealmManager.cs`
```csharp
public interface IRealmManager
{
    /// <summary>Creates a new realm with the given path, domain and display name.</summary>
    ValueTask<Realm> CreateAsync(
        string path,
        string domain,
        string displayName,
        CancellationToken ct = default);

    /// <summary>Updates configuration of an existing realm.</summary>
    ValueTask UpdateAsync(Realm realm, CancellationToken ct = default);

    /// <summary>Enables a disabled realm.</summary>
    ValueTask EnableAsync(string realmId, CancellationToken ct = default);

    /// <summary>Disables a realm. Internal realms cannot be disabled.</summary>
    ValueTask DisableAsync(string realmId, CancellationToken ct = default);
}
```

### 4.4 — Implementar `RealmManager`

**Arquivo:** `RoyalIdentity/Contracts/Defaults/RealmManager.cs`

Responsabilidades:
- `CreateAsync`: validar unicidade de `path` via `IRealmStore.GetByPathAsync`; criar `Realm` com `new RealmOptions(storage.ServerOptions)` para preservar a configuração global compartilhada do storage; chamar `IRealmStore.SaveAsync`; despachar evento.
- `UpdateAsync`: chamar `IRealmStore.SaveAsync`.
- `EnableAsync`/`DisableAsync`: buscar realm pelo id, modificar `Enabled`, chamar `SaveAsync`; rejeitar se `Internal == true`.

### 4.5 — Registrar no DI

Em `AddOpenIdConnectProviderServices()` (ou equivalente), adicionar:
```csharp
services.AddScoped<IRealmManager, RealmManager>();
```

---

## Fase 5: Eventos Realm-Scoped

### 5.1 — Adicionar `RealmId` à base `Event`

**`RoyalIdentity/Events/Event.cs`** — adicionar após `RemoteIpAddress`:
```csharp
/// <summary>
/// Gets or sets the realm identifier associated with this event.
/// Null for server-level events raised outside a realm context.
/// </summary>
public string? RealmId { get; set; }
```

### 5.2 — Adicionar overload a `IEventDispatcher`

**`RoyalIdentity/Contracts/IEventDispatcher.cs`**:
```csharp
/// <summary>
/// Raises the specified event scoped to a realm.
/// Sets <see cref="Event.RealmId"/> before dispatching.
/// </summary>
ValueTask DispatchAsync(Event evt, Realm realm);
```

> Adicionar `using RoyalIdentity.Models;` onde for necessário para resolver o tipo `Realm`.

### 5.3 — Implementar em `DefaultEventDispatcher`

**`RoyalIdentity/Contracts/Defaults/DefaultEventDispatcher.cs`** — adicionar o overload tanto no dispatcher público quanto no dispatcher genérico interno, pois ambos implementam `IEventDispatcher`:
```csharp
public ValueTask DispatchAsync(Event evt, Realm realm)
{
    evt.RealmId = realm.Id;
    return DispatchAsync(evt);
}
```

### 5.4 — Atualizar callers nos handlers

Pesquisar chamadas a `DispatchAsync` dentro de handlers/validators/decorators que têm `context.Realm` disponível:
```
rg "DispatchAsync" --type cs
```

Para cada caller em contexto de request, adicionar o realm:
```csharp
// antes
await dispatcher.DispatchAsync(new UserLoginSuccessEvent(...));
// depois
await dispatcher.DispatchAsync(new UserLoginSuccessEvent(...), context.Realm);
```

---

## Fase 6: Branding Básico por Realm

### 6.1 — Criar `RealmBrandingOptions`

**Arquivo:** `RoyalIdentity/Options/RealmBrandingOptions.cs`
```csharp
namespace RoyalIdentity.Options;

/// <summary>
/// Visual branding options configurable per realm.
/// </summary>
public class RealmBrandingOptions
{
    /// <summary>
    /// URI to the realm's logo image. If null, the default server logo is shown.
    /// </summary>
    public string? LogoUri { get; set; }

    /// <summary>
    /// URI to the realm's favicon. If null, the server default favicon is used.
    /// </summary>
    public string? FaviconUri { get; set; }

    /// <summary>
    /// CSS hex color used as the primary color in account pages (e.g. "#3B82F6").
    /// If null, the default theme color is used.
    /// </summary>
    public string? PrimaryColor { get; set; }
}
```

### 6.2 — Adicionar a `RealmOptions`

**`RoyalIdentity/Options/RealmOptions.cs`** — adicionar após `Account`:
```csharp
/// <summary>
/// Gets or sets the visual branding options for this realm.
/// </summary>
public RealmBrandingOptions Branding { get; set; } = new();
```

### 6.3 — Consumir no layout Razor

**Atenção:** `AccountLayout.razor` não contém `<head>` — apenas `<div class="account-page">`, `<header>`, `<main>`, `<footer>`. O `<head>` real está em `RoyalIdentity.Server/Components/App.razor`, que já tem `<HeadOutlet @rendermode="RenderModeForPage" />` (linha 19). Para injetar conteúdo no `<head>` a partir de componentes SSR, usar o componente `<HeadContent>` do Blazor — ele é renderizado pelo `<HeadOutlet>` no `App.razor`.

**`RoyalIdentity.Razor/Components/Layout/AccountLayout.razor`:**

1. Acessar `RealmOptions` via `HttpContext.TryGetCurrentRealm(out var realm)` (HttpContext disponível via `[CascadingParameter]` já presente no componente). Não usar `GetCurrentRealm()` aqui, porque o layout também atende páginas sem realm, como seleção de domínio, e esse método lança exception quando o realm não está no contexto.

2. **Logo** — substituir o `<img src="icon.png">` hardcoded pelo logo do realm (já está no `<header>` do layout, sem necessidade de `<HeadContent>`):
   ```html
   <header class="brand-header">
       @if (branding?.LogoUri is { } logo)
       {
           <img src="@logo" alt="@realm?.DisplayName" class="realm-logo" />
       }
       else
       {
           <img src="icon.png" alt="Royal Identity" />
       }
   </header>
   ```

3. **Favicon e PrimaryColor** — usar `<HeadContent>` para injetar no `<head>` via HeadOutlet:
   ```html
   <HeadContent>
       @if (branding?.FaviconUri is { } fav)
       {
           <link rel="icon" href="@fav" />
       }
       @if (branding?.PrimaryColor is { } color)
       {
           <style>:root { --primary-color: @color; }</style>
       }
   </HeadContent>
   ```

4. No bloco `@code`, resolver o branding a partir do `HttpContext`:
   ```csharp
   private Realm? realm =>
       HttpContext is not null && HttpContext.TryGetCurrentRealm(out var currentRealm)
           ? currentRealm
           : null;

   private RealmBrandingOptions? branding =>
       realm?.Options.Branding;
   ```

### 6.4 — Dados de demo

Em `RealmMemoryStore` (inicialização do DemoRealm), adicionar branding de exemplo para validação visual:
```csharp
options.Branding.LogoUri = "/images/demo-logo.png";
options.Branding.PrimaryColor = "#6366F1";
```

---

## Fase 7: Auditoria de Configurações por Realm

Fase analítica — identificar o que está no nível de servidor mas deveria ser por realm.

### 7.1 — Revisar `ServerOptions` vs `RealmOptions`

Ler `RoyalIdentity/Options/ServerOptions.cs` e comparar com `RealmOptions`. Para cada item em `ServerOptions`, questionar: "faz sentido ser diferente por realm?". Registrar gaps.

Itens prováveis de serem realm-level mas hoje não são:
- Configuração de external IdPs (federation) — não existe modelo ainda
- Política de senha (complexidade mínima, expiração) — verificar se está em `AccountOptions`
- Configuração CORS — verificar onde está

### 7.2 — Documentar gaps em `product.md`

Na seção de "Active Design Debt" do arquivo `.ai/foundation/product.md`, adicionar os gaps encontrados como itens pendentes de design.

### 7.3 — Sem alterações de código obrigatórias

Se forem encontrados campos pequenos que claramente deveriam ser por realm (sem impacto em pipeline), mover neste momento. Gaps maiores (federation, external IdPs) ficam documentados para o backlog.

---

## Fase 8: Testes de Isolamento de Realm

**Projeto:** `Tests.Integration`
**Arquivo sugerido:** `Tests.Integration/Realm/RealmIsolationTests.cs`

Cada teste deve usar dois realms distintos (podem ser os `DemoRealm` e um segundo realm criado na fixture).

### 8.1 — Isolamento de clients

```
ClientIsolation_ClientInRealmA_NotFoundInRealmB
```
- Setup: realm A com client "app-a", realm B sem esse client.
- Act: `storage.GetClientStore(realmB).FindClientByIdAsync("app-a")` → `null`.

```
ClientIsolation_SameClientId_DifferentRealms_ReturnDifferentClients
```
- Setup: realm A e realm B com client "shared-app" com `RedirectUris` distintas.
- Act: lookup em cada store retorna o client do próprio realm.

### 8.2 — Isolamento de sessão / cookie

```
SessionIsolation_LoginInRealmA_DoesNotAuthenticateRealmB
```
- Setup: usuário "alice" registrado apenas no realm A.
- Act: completar login HTTP em realm A (obter cookie); fazer request autenticado para `/{realmB}/account/...` com esse cookie.
- Assert: 401 ou redirect para login do realm B.

### 8.3 — Isolamento de authorization code (requer Fase 3)

```
AuthCodeIsolation_CodeFromRealmA_NotRedeemableInRealmB
```
- Setup: completar authorize flow em realm A → obter `code`.
- Act: tentar `POST /{realmB}/connect/token` com esse code.
- Assert: resposta de erro (invalid_grant ou equivalent).

### 8.4 — Isolamento de consent (requer Fase 3)

```
ConsentIsolation_ConsentInRealmA_NotVisibleInRealmB
```
- Setup: gravar consent `{SubjectId="alice", ClientId="app", RealmId=realmA.Id}` via `storage.GetUserConsentStore(realmA)`.
- Act: `await storage.GetUserConsentStore(realmB).GetUserConsentAsync("alice", "app", ct)` → `null`.

### 8.5 — Error response para realm desconhecido (requer Fase 2)

```
UnknownRealm_Returns404_WithJsonErrorBody
```
- Act: `GET /nonexistent-realm/connect/authorize`.
- Assert: 404, `Content-Type: application/json`, body com `"error": "realm_not_found"`.

### 8.6 — `RealmId` preenchido nos tokens (requer Fase 3)

```
TokenCreation_AccessToken_HasCorrectRealmId
```
- Act: completar fluxo de authorization code em realm A.
- Assert: `accessToken.RealmId == realmA.Id`.

---

## Checklist de Build/Test entre fases

Após cada fase executar:
```bash
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

Não avançar para a próxima fase com build quebrado ou testes falhando.
