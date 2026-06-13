# Redesign de Usuários / Sessão / Identidade — Versão Final

> **Status:** especificação de redesign aprovada para virar ADR + plano.
> **Consolida e supersede** (como fonte de verdade do redesign de Users):
> [an-users-m1.md](an-users-m1.md), [an-users-m2.md](an-users-m2.md),
> [an-user-m1-av1.md](an-user-m1-av1.md), [an-user-m1-av2.md](an-user-m1-av2.md),
> [an-user-con2.md](an-user-con2.md).
>
> **Escopo:** seção *Users* de [redesign-todo.md](../../redesign-todo.md) — unificar
> `IdentityUser`/`UserDetails`/stores e revisar sessão/login/logout. Decisão base: [ADR-005](../../adrs/ADR-005.md).
>
> **Método:** consolidação das análises/avaliações/consenso anteriores + leitura do código atual.
> Nenhum build/teste executado nesta entrega (documental). As decisões firmes (§3) não estão em disputa
> entre as avaliações; as sub-decisões em aberto foram **resolvidas** nesta versão (§4).
>
> **Próximos passos formais:** (1) escrever **ADR-013** (§3 C1); (2) criar o **plano** em
> `.ai/plans/` no formato padrão (status + barra de progresso + ordem de execução), importando a
> matriz de testes/critérios (§10).

---

## 1. Problema (resumo executivo)

O subsistema de usuários acumula responsabilidades mal distribuídas:

- **Dois modelos paralelos**: `IdentityUser` (rico, com comportamento) × `UserDetails` (POCO), servidos por
  **dois stores** (`IUserStore`/`IUserDetailsStore`) que são **a mesma classe** — a separação não isola nada.
- **`IdentityUser` carrega serviços** (session store, details store, password protector, options, clock) →
  não serializável, não cacheável, difícil de testar; mistura autenticação + lockout + criação de sessão + montagem de principal.
- **`IdentitySession` referencia o `IdentityUser` vivo** → sessão não serializável.
- **Storage acoplado a HTTP**: `UserSessionStore.GetCurrentSessionAsync` lê `IHttpContextAccessor`.
- **`sub` colado ao `Username`** (com `AllowChangeUsername`/`EmailAsUsername` existentes) → trocar username quebra `sub`/tokens/consent. Correção protocolar latente.
- **Lockout "split-brain"** (política no `DefaultIdentityUser`, contadores no `UserDetails`, orquestração no `SignInManager`); **sessão criada como efeito colateral** da verificação de senha.
- **"Ativo" inconsistente** em 3 caminhos (`ProfileService` lenient com sessão nula × `ValidateUserSessionAsync` estrito × revalidating provider só-conta).
- **Telas conhecem demais**: `LoginPageService` carrega a máquina de estados do login.
- **Código quebrado**: `IdentityRevalidatingAuthenticationStateProvider` está registrado mas resolve `IUserStore` que **não está em DI** → lança exceção; ignora realm.

**Regra de negócio está aproveitável; o problema é a distribuição de responsabilidades.**

---

## 2. Objetivos do redesign

1. **Separar dado de comportamento**: um registro de usuário persistível e serializável; comportamento em serviços focados.
2. **Sessão serializável** (pré-requisito do backend SQL/Redis planejado), sem objeto rico.
3. **`SubjectId` imutável** separado de `Username`.
4. **Storage puro** (sem HttpContext); "sessão atual" na camada de aplicação.
5. **Unificar "ativo"** (conta × sessão) com semântica única.
6. **Telas como cola**: a UI consome uma camada de interação por fluxo, recebendo view models + resultados; nunca toca `IdentityUser`/sessão/stores/cookie.
7. **Preparar futuro por composição** (externo/MFA/passwordless/admin) sem construí-lo inteiro agora (anti-YAGNI).
8. **Preservar** todas as invariantes de negócio atuais (§8).

---

## 3. Decisões firmes (não em disputa)

| # | Decisão |
|---|---|
| C1 | **ADR-013** supersede/refina a ADR-005: extensibilidade por **composição** (serviços substituíveis), não por herança de entidade rica; registra `SubjectId` imutável. Escrever **antes** de codar. |
| C2 | Entidade persistida única **`UserAccount`**. |
| C3 | **`SubjectId` imutável** ≠ `Username`, introduzido **agora** (não há dado persistido a migrar); seeds com IDs **determinísticos**. |
| C4 | `UserAccount` compõe: perfil, `Roles`, **`UserClaim`** (não `System.Security.Claims.Claim` cru), **`UserSecurityState`** (com **`SecurityStamp`**), **`PasswordCredential`** (submodelo embutido, **sem** store separado). |
| C5 | **`IUserAccountStore`** único substitui `IUserStore`+`IUserDetailsStore`; devolve dados, não objeto rico. |
| C6 | **`UserSession`** persistível serializável (sem `IdentityUser`), clients **deduplicados**; **store puro** (sem HttpContext). |
| C7 | **`IUserSessionService`** (aplicação) concentra current session, validação, start/end, record client. |
| C8 | **`IUserAuthenticator`** + **`LockoutPolicy`** única; **não** criar sessão na verificação de senha. |
| C9 | **`ISubjectPrincipalFactory`** monta o `ClaimsPrincipal` (claims obrigatórias), reusando regras de claims do id_token. |
| C10 | **Camada de interação por fluxo** para a UI (orquestração-only sobre serviços de domínio). |
| C11 | Separar **"conta ativa"** de **"sessão válida"**; **sessão ausente ⇒ inválida** quando há `sid`. |
| C12 | **Remover** `IdentityRevalidatingAuthenticationStateProvider` (refazer correto só se houver área interativa autenticada). |
| C13 | **`SecurityStamp`**: campo **agora**; validação (invalidar cookie/sessão) em tarefa posterior. |
| C14 | **Resultado único** de autenticação (funde `CredentialsValidationResult` + `ValidateCredentialsResult`). |
| C15 | Remover/obsoletar: `IdentityUser`, `UserDetails`, `ValidateCredentialsResult`, `CredentialsValidationResult` (adapters temporários permitidos, com fase de remoção). |
| C16 | **Importar matriz de testes + critérios de aceite** (do m1) para o plano. |

---

## 4. Sub-decisões — resolvidas nesta versão final

| Sub | Resolução final | Justificativa |
|---|---|---|
| S1 — Camada de interação: serviço único × por fluxo | **Por fluxo**: manter `LoginPageService`/`ConsentPageService`/`EndSessionPageService` como as fachadas de interação, **reduzidas a orquestração**; mover a máquina de estados do login para um serviço de aplicação. **Não** criar um `IAccountInteractionService` único. | Os serviços por fluxo já existem e mapeiam 1:1 aos casos de uso; evita god-service (risco da av2) e atende a fronteira da UI (objetivo da av1) sem nova abstração. |
| S2 — Remover revalidating provider | **Remover agora.** A validação real de sessão já é feita no cookie (`OnValidatePrincipal` → `ValidateUserSessionAsync`); o provider é SSR-irrelevante e está quebrado. Reintroduzir correto só quando existir área `InteractiveServer` **autenticada** que precise de revalidação periódica. | É código que lança exceção; sua remoção não reduz a segurança do fluxo SSR. |
| S3 — Nome da entidade | **`UserAccount`** (confirmar na ADR-013). | Desfaz ambiguidade com `HttpContext.User`/`ClaimsPrincipal`/`Subject` num servidor OIDC. |
| S4 — Unicidade do `SubjectId` | **String opaca global** (`CryptoRandom.CreateUniqueId()` ou GUID "N"). | Único globalmente ⇒ satisfaz também o requisito por-realm; reusa utilitário já existente no projeto. |

---

## 5. Modelo-alvo (dados)

> Esboços não-normativos quanto à sintaxe exata; normativos quanto a campos e responsabilidades.
> Tipos persistíveis (records/POCOs), **sem** dependências de serviço.

### 5.1 `UserAccount` (substitui `UserDetails` + parte de dados do `IdentityUser`)

```csharp
public sealed class UserAccount
{
    public required string SubjectId { get; init; }   // sub — imutável, opaco (S4)
    public required string Username { get; set; }      // login — mutável conforme policy
    public string? NormalizedUsername { get; set; }    // p/ busca case-insensitive / email-as-username
    public required string DisplayName { get; set; }   // name
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DisabledAt { get; set; }

    public HashSet<string> Roles { get; set; } = [];
    public List<UserClaim> Claims { get; set; } = [];

    public UserSecurityState Security { get; set; } = new();
    public PasswordCredential? Password { get; set; }          // null ⇒ sem login local por senha
    public List<ExternalIdentity> ExternalIdentities { get; set; } = []; // reservado (futuro)
}
```

Notas:
- `SubjectId` é a **chave primária** do store; `Username`/`NormalizedUsername` são índices de busca.
- `RealmId` não precisa estar no registro (store é realm-scoped), mas pode ser incluído para auditoria/proteção contra uso cruzado — decisão do plano.
- `ExternalIdentities` entra como **lista vazia reservada** (costura para login externo); não há fluxo agora.

### 5.2 `UserSecurityState`

```csharp
public sealed class UserSecurityState
{
    public int FailedPasswordAttempts { get; set; }
    public DateTimeOffset? LastPasswordFailureAt { get; set; }
    public DateTimeOffset? LockoutEndAt { get; set; }      // lockout calculado/persistido
    public string SecurityStamp { get; set; } = CryptoRandom.CreateUniqueId(); // C13 (campo agora)
    public DateTimeOffset? PasswordChangedAt { get; set; }
    public bool MustChangePassword { get; set; }
}
```

Substitui `LoginAttemptsWithPasswordErrors`/`LastPasswordError` soltos. `SecurityStamp` fica **reservado**:
a regra de invalidação (cookie/sessão com stamp divergente) é tarefa **posterior** (C13).

### 5.3 `PasswordCredential` (submodelo embutido — C4/S de credencial)

```csharp
public sealed class PasswordCredential
{
    public required string Hash { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsEnabled { get; set; } = true;
}
```

Separa credencial de perfil **sem** store extra. `UserAccount.Password == null` ou `IsEnabled == false`
⇒ login local por senha não permitido (preserva a semântica atual de `PasswordHash == null`).

### 5.4 `UserClaim` (substitui `HashSet<Claim>` na persistência — C4)

```csharp
public sealed class UserClaim
{
    public required string Type { get; init; }
    public required string Value { get; init; }
    public string? ValueType { get; init; }
}
```

Conversão para `System.Security.Claims.Claim` **só na borda** (factory de principal / profile service).

### 5.5 `ExternalIdentity` (reservado — futuro)

```csharp
public sealed class ExternalIdentity
{
    public required string Provider { get; init; }
    public required string ProviderSubjectId { get; init; }
    public string? DisplayName { get; set; }
    public List<UserClaim> Claims { get; set; } = [];
    public DateTimeOffset LinkedAt { get; init; }
}
```

Modelado mas **não implementado** agora — costura para `idp`/`amr` que já circulam.

### 5.6 `UserSession` (substitui `IdentitySession` — C6)

```csharp
public sealed class UserSession
{
    public required string SessionId { get; init; }   // sid
    public required string SubjectId { get; init; }    // NÃO guarda IdentityUser
    public required string RealmId { get; init; }
    public required string IdentityProvider { get; init; } // idp
    public HashSet<string> AuthenticationMethods { get; init; } = []; // amr
    public required DateTimeOffset AuthenticatedAt { get; init; }      // auth_time
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? EndedAt { get; set; }
    public HashSet<UserSessionClient> Clients { get; init; } = [];  // dedup
    public string? SecurityStamp { get; set; }   // reservado (futuro)
}

public sealed class UserSessionClient   // dedup por ClientId
{
    public required string ClientId { get; init; }
    public DateTimeOffset FirstSeenAt { get; init; }
    public DateTimeOffset LastSeenAt { get; set; }
}
```

`Clients` vira **conjunto deduplicado** (resolve o `IList<string>` mutável atual). Campos de device
(UserAgent/IP) ficam **fora** desta fase (adiados; coleção/extensão preparada).

### 5.7 `AuthenticationResult` (resultado único — C14)

```csharp
public readonly struct AuthenticationResult
{
    public bool Success { get; }
    public string? Reason { get; }        // NotFound | Inactive | InvalidCredentials | Blocked
    public string? ErrorMessage { get; }  // mensagem genérica p/ UI (anti-enumeração)
    public UserAccount? User { get; }      // em sucesso
    // sessão NÃO entra aqui: criada em passo separado pelo sign-in (C8)
}
```

Funde `CredentialsValidationResult` + `ValidateCredentialsResult`. **Não** carrega sessão — a sessão é
criada deliberadamente no sign-in, não na verificação de credencial.

---

## 6. Modelo-alvo (contratos / serviços)

### 6.1 Storage (dados puros, realm-scoped)

```csharp
public interface IUserAccountStore   // substitui IUserStore + IUserDetailsStore (C5)
{
    Task<UserAccount?> FindBySubjectIdAsync(string subjectId, CancellationToken ct = default);
    Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken ct = default);
    Task<UserAccount?> FindByLoginAsync(string login, CancellationToken ct = default); // aplica EmailAsUsername/LoginWithEmail
    Task SaveAsync(UserAccount user, CancellationToken ct = default);
}

public interface IUserSessionStore   // puro: SEM HttpContext (C6)
{
    Task<UserSession> CreateAsync(UserSession session, CancellationToken ct = default);
    Task<UserSession?> FindByIdAsync(string sessionId, CancellationToken ct = default);
    Task RecordClientAsync(string sessionId, string clientId, CancellationToken ct = default); // dedup
    Task<UserSession?> EndAsync(string sessionId, CancellationToken ct = default);
}
```

Removido do store: `GetCurrentSessionAsync` (vai para `IUserSessionService`). `IStorage` troca
`GetUserStore`/`GetUserDetailsStore` por `GetUserAccountStore`; mantém `GetUserSessionStore`.

### 6.2 Aplicação / domínio

```csharp
public interface IUserSessionService   // current/validação/start/end (C7, C11)
{
    Task<UserSession?> GetCurrentAsync(Realm realm, ClaimsPrincipal principal, CancellationToken ct);
    Task<bool> IsSessionValidAsync(Realm realm, ClaimsPrincipal principal, CancellationToken ct); // ausente ⇒ false
    Task<UserSession> StartAsync(Realm realm, UserAccount user, AuthenticationMethod method, CancellationToken ct);
    Task RecordClientAsync(Realm realm, string sessionId, string clientId, CancellationToken ct);
    Task<UserSession?> EndAsync(Realm realm, string sessionId, CancellationToken ct);
}

public interface IUserAuthenticator   // credencial + lockout (C8); NÃO cria sessão
{
    Task<AuthenticationResult> AuthenticateLocalAsync(Realm realm, string login, string password, CancellationToken ct);
}

public sealed class LockoutPolicy      // política única (C8) — lê AccountOptions.PasswordOptions
{
    public bool IsLockedOut(UserAccount user, DateTimeOffset now);
    public void RegisterFailure(UserAccount user, DateTimeOffset now); // incrementa + calcula LockoutEndAt
    public void RegisterSuccess(UserAccount user);                     // zera contadores
}

public interface ISubjectPrincipalFactory   // monta ClaimsPrincipal do cookie (C9)
{
    ClaimsPrincipal Create(Realm realm, UserAccount user, UserSession session);
    // claims obrigatórias: sub=SubjectId, name, auth_time, sid, idp, amr  (+ roles, claims)
}
```

`IProfileService` **permanece** como contrato OIDC, mas passa a consumir `IUserAccountStore`
(busca por `SubjectId`) e `IUserSessionService` (validação). `IPasswordProtector` permanece (hash/verify),
agora usado pelo `IUserAuthenticator`/troca de senha.

### 6.3 Conta de "ativo" unificada (C11)

Duas perguntas, duas APIs, uma implementação cada:
- **"Conta habilitada?"** → `UserAccount.IsActive` (revalidação de conta, userinfo de dados).
- **"Sessão válida?"** → `IUserSessionService.IsSessionValidAsync` (existe **e** `IsActive`; **ausente ⇒ false**).

Consumidores e regra:

| Consumidor | Hoje | Alvo |
|---|---|---|
| `DefaultProfileService.IsActiveAsync` | conta && (sessão nula ⇒ ativo) | conta `IsActive` **&&** (se há `sid`) sessão válida |
| `HttpContextExtensions.ValidateUserSessionAsync` (cookie) | sessão `{IsActive:true}` | `IUserSessionService.IsSessionValidAsync` |
| `ActiveUserValidator` (token endpoint) | via ProfileService | idem (regra unificada) |
| `PromptLoginDecorator` (authorize) | via ProfileService | idem |
| revalidating provider | só conta (quebrado) | **removido** (S2) |

### 6.4 Camada de interação para a UI (C10 / S1)

Mantêm-se os **três serviços por fluxo** já existentes, **reduzidos a orquestração**:
- `LoginPageService` → constrói view model; processa login chamando `IUserAuthenticator` → (sucesso) `IUserSessionService.StartAsync` → `ISubjectPrincipalFactory` → cookie sign-in → decide **flow-result (enum)**. A **decisão de roteamento** (consent / signed-in page / callback / profile / error) vira um método de aplicação que devolve enum; a tela só converte enum→redirect.
- `ConsentPageService` / `EndSessionPageService` → permanecem como fachadas finas (já estão próximas disso).

A UI **não** vê `UserAccount`, `UserSession`, `ClaimsPrincipal`, stores ou cookie.

---

## 7. De → Para (mapa de migração)

| Atual | Destino | Ação |
|---|---|---|
| `IdentityUser` / `DefaultIdentityUser` | — | **Remover** (comportamento → serviços). Adapter temporário opcional (C15). |
| `UserDetails` | `UserAccount` (+ submodelos) | **Substituir.** |
| `IUserStore` + `IUserDetailsStore` | `IUserAccountStore` | **Fundir.** `IStorage.GetUserStore`/`GetUserDetailsStore` → `GetUserAccountStore`. |
| `IdentitySession` (guarda `IdentityUser`) | `UserSession` (guarda `SubjectId`) | **Substituir.** |
| `IUserSessionStore.GetCurrentSessionAsync` | `IUserSessionService.GetCurrentAsync` | **Mover** (tira HttpContext do store). |
| `IUserSessionStore.AddClientIdAsync` | `RecordClientAsync` (dedup) | **Renomear + dedup.** |
| `AuthenticateAndStartSessionAsync` (no usuário) | `IUserAuthenticator` + `IUserSessionService.StartAsync` | **Separar** (sem efeito colateral). |
| lockout em `DefaultIdentityUser`/`UserDetails`/`SignInManager` | `LockoutPolicy` | **Unificar.** |
| `CreatePrincipalAsync` (no usuário) | `ISubjectPrincipalFactory` | **Mover.** |
| `CredentialsValidationResult` + `ValidateCredentialsResult` | `AuthenticationResult` | **Fundir** (C14). |
| `UserDetails.Claims : HashSet<Claim>` | `List<UserClaim>` | **Substituir** (conversão na borda). |
| `IdentityRevalidatingAuthenticationStateProvider` | — | **Remover** (S2). |
| `IdentityUserManager` (Razor) | usa `IUserAccountStore` | **Ajustar** (devolve `UserAccount`). |
| `ISignInManager`/`ISignOutManager` | decompor/afinar | sign-in vira orquestrador fino; sign-out praticamente igual (usa `SubjectId`, não `User.UserName`). |

**Storage in-memory** ([MemoryStorage.Storage.cs](../../RoyalIdentity.Storage.InMemory/MemoryStorage.Storage.cs),
[UserStore.cs](../../RoyalIdentity.Storage.InMemory/UserStore.cs),
[UserSessionStore.cs](../../RoyalIdentity.Storage.InMemory/UserSessionStore.cs), `RealmMemoryStore`):
dicionário de usuários passa a ser chaveado por `SubjectId` com índice por `NormalizedUsername`; sessão
deixa de referenciar usuário; seeds recebem `SubjectId` determinístico.

---

## 8. Invariantes a preservar (não regridir)

1. Usuário e sessão são **realm-scoped**; nada cruza realm.
2. Login local só com `AccountOptions.AllowLocalLogin`; no authorize, também `Client.EnableLocalLogin`.
3. Restrições de IdP do client continuam valendo.
4. Usuário inexistente/inativo/bloqueado/senha inválida ⇒ **mensagem genérica** por default (anti-enumeração); motivo interno preservado p/ evento/auditoria.
5. Falha de senha **incrementa** contador + registra última falha; sucesso **zera**.
6. Lockout respeita `MaxFailedAccessAttempts` e `AccountLockoutDurationMinutes`.
7. Senha ausente/credencial desabilitada ⇒ login por senha não permitido.
8. **Sessão criada só após autenticação bem-sucedida** (agora explicitamente, no sign-in).
9. Cookie do realm contém `sub`, `sid`, `auth_time`, `amr`, `idp`, `name`.
10. Cookie é **validado contra sessão ativa** (`OnValidatePrincipal`).
11. Logout marca sessão inativa; emissão de code **registra client na sessão**; front/back-channel logout usa os clients da sessão (via `SubjectId`).
12. `prompt`/`max_age`/`UserSsoLifetime`/restrições de IdP avaliados antes de emitir tokens.
13. `IProfileService.IsActiveAsync` valida conta **e** sessão (regra unificada — C11).
14. Claims de profile filtradas pelos identity scopes solicitados.
15. Consentimento por **subject (SubjectId) + client + scope + realm**.

---

## 9. Fluxos redesenhados

### 9.1 Login local
1. UI (GET) → `LoginPageService.BuildViewModel` (realm, authorize context, providers, local-login permitido).
2. UI (POST) → `LoginPageService.Login`:
   a. `IUserAuthenticator.AuthenticateLocalAsync` → busca `UserAccount`, checa `IsActive`, `LockoutPolicy.IsLockedOut`, verifica senha (`IPasswordProtector`), `RegisterFailure/Success`, salva. Devolve `AuthenticationResult` (**sem** sessão).
   b. evento de sucesso/falha.
   c. `IUserSessionService.StartAsync(realm, user, method)` → `UserSession`.
   d. `ISubjectPrincipalFactory.Create(realm, user, session)` → cookie sign-in.
   e. roteamento (consent / signed-in / callback / profile / error) via método de aplicação → **enum**; tela converte para redirect.

### 9.2 Emissão de code
`DefaultCodeFactory` extrai `sid` do principal, cria o code, e chama `IUserSessionService.RecordClientAsync(realm, sid, clientId)` (dedup). Mesma regra, sem acoplar ao store.

### 9.3 Validação de cookie
`OnValidatePrincipal` → `IUserSessionService.IsSessionValidAsync(realm, principal)` (sessão inexistente/inativa/outro realm ⇒ rejeita). Futuro: comparar `SecurityStamp` (C13).

### 9.4 Token / profile
`IProfileService` consome `IUserAccountStore` (por `SubjectId`) e `IUserSessionService`. `IsActiveAsync` aplica a regra unificada (C11). Claims de `UserAccount.Claims`/`Roles`, filtradas por scope.

### 9.5 Logout
`EndSessionHandler` cria `LogoutMessage` (igual). `IUserSessionService.EndAsync` encerra e devolve snapshot com `SubjectId` + clients. `DefaultSignOutManager` remove cookie, dispara evento e notifica clients usando **`SubjectId`** (não `session.User.UserName`).

---

## 10. Testes e critérios de aceite (importados — C16)

### 10.1 Testes de caracterização (escrever **antes** de mudar login/sessão)
Login válido cria sessão ativa realm-scoped; login inválido incrementa falha e **não** cria sessão; sucesso zera falhas; usuário inativo/bloqueado não autentica; cookie com sessão encerrada é rejeitado; code registra client na sessão; logout encerra sessão e notifica clients; sessão de realm A não autentica realm B.

### 10.2 Novos testes
- Unidade/domínio: senha ausente rejeita; falha incrementa/sucesso zera; lockout temporário expira; `SubjectId` **estável quando username muda**; `ISubjectPrincipalFactory` emite claims obrigatórias; `LockoutPolicy` isolada.
- Integração: login com `RememberLogin`; `EnableLocalLogin=false` no authorize; `prompt=login`/`max_age`/`UserSsoLifetime` forçam interação; sessão encerrada invalida cookie; back-channel logout usa `SubjectId`; UserInfo não retorna claims p/ inativo; token endpoint falha com sessão inativa; realm isolation.
- UI flow: erro genérico no login inválido; login sem authorize → profile; com authorize sem consent → callback; com consent → tela de consent; consent denial → `access_denied`; logoutId inválido → erro.

### 10.3 Critérios de aceite
1. Entidade persistida única (`UserAccount`). 2. `sub` = `SubjectId` estável, separado de username/email. 3. Sessão persistível sem objeto de comportamento. 4. Stores de usuário/sessão sem `HttpContext`. 5. Login mantém mensagens genéricas. 6. Lockout funciona. 7. Cookie mantém `sub/sid/auth_time/amr/idp/name`. 8. Validação de cookie rejeita sessão inativa. 9. Profile/token validam conta e sessão. 10. Code registra client na sessão. 11. Logout encerra sessão e notifica clients. 12. Telas não conhecem stores/modelos internos de sessão. 13. Testes existentes (login, consent, endsession, userinfo, realm isolation) continuam passando. 14. Novos testes cobrem lockout, sessão e separação `SubjectId`/username.

---

## 11. Roadmap (vira plano em `.ai/plans/`)

1. **ADR-013** (C1/S3) — composição-sobre-herança + `SubjectId` imutável.
2. **Testes de caracterização** (§10.1).
3. **Modelo de dados** (C2/C3/C4) — `UserAccount` + `SubjectId` + `UserClaim` + `UserSecurityState{SecurityStamp}` + `PasswordCredential` (+ `ExternalIdentity` reservado).
4. **Store** (C5) — `IUserAccountStore` + in-memory chaveado por `SubjectId`; ajustar `IStorage`; seeds determinísticos.
5. **Serviços** (C8/C9) — `IUserAuthenticator` + `LockoutPolicy` + `ISubjectPrincipalFactory`; sign-in sem efeito colateral de sessão.
6. **Sessão + "ativo"** (C6/C7/C11) — `UserSession` puro, `IUserSessionService`; atualizar `ProfileService`, `ValidateUserSessionAsync`, `DefaultCodeFactory`, `DefaultSignOutManager`, `ActiveUserValidator`, `PromptLoginDecorator`.
7. **UI** (C10/S1) — afinar `LoginPageService`/`ConsentPageService`/`EndSessionPageService`.
8. **Limpeza** (C12/C15/S2) — remover revalidating provider, `IdentityUser`, `UserDetails`, resultados antigos.

> Cada etapa fecha contra os critérios de aceite (§10.3). Adapters temporários permitidos, **com fase de
> remoção marcada** — sem big bang.

---

## 12. Arquivos afetados (referência)

- Domínio: [RoyalIdentity/Users/](../../RoyalIdentity/Users/) (todo), [Contexts/AuthorizationContext.cs](../../RoyalIdentity/Users/Contexts/AuthorizationContext.cs).
- Contratos/serviços: [IProfileService](../../RoyalIdentity/Contracts/IProfileService.cs), [DefaultProfileService](../../RoyalIdentity/Contracts/Defaults/DefaultProfileService.cs), [DefaultTokenClaimsService](../../RoyalIdentity/Contracts/Defaults/DefaultTokenClaimsService.cs), [DefaultCodeFactory](../../RoyalIdentity/Contracts/Defaults/DefaultCodeFactory.cs), [IStorage](../../RoyalIdentity/Contracts/Storage/IStorage.cs).
- Auth/cookie: [ConfigureRealmCookieAuthenticationOptions](../../RoyalIdentity/Authentication/ConfigureRealmCookieAuthenticationOptions.cs), [HttpContextExtensions](../../RoyalIdentity/Extensions/HttpContextExtensions.cs), [AuthenticationExtensions](../../RoyalIdentity/Extensions/AuthenticationExtensions.cs), [PrincipalExtensions](../../RoyalIdentity/Extensions/PrincipalExtensions.cs), [ClaimsExtensions](../../RoyalIdentity/Extensions/ClaimsExtensions.cs).
- Pipeline: [ActiveUserValidator](../../RoyalIdentity/Contexts/Validators/ActiveUserValidator.cs), [PromptLoginDecorator](../../RoyalIdentity/Contexts/Decorators/PromptLoginDecorator.cs).
- DI/options: [ServiceCollectionExtensions](../../RoyalIdentity/Extensions/ServiceCollectionExtensions.cs), [AccountOptions](../../RoyalIdentity/Options/AccountOptions.cs).
- Storage in-memory: [MemoryStorage.Storage.cs](../../RoyalIdentity.Storage.InMemory/MemoryStorage.Storage.cs), [UserStore.cs](../../RoyalIdentity.Storage.InMemory/UserStore.cs), [UserSessionStore.cs](../../RoyalIdentity.Storage.InMemory/UserSessionStore.cs).
- Razor: [RoyalIdentity.Razor/Services/](../../RoyalIdentity.Razor/Services/), [RoyalIdentityRazorServiceCollectionExtensions](../../RoyalIdentity.Razor/Extensions/RoyalIdentityRazorServiceCollectionExtensions.cs).
- Modelos relacionados: [Consent.cs](../../RoyalIdentity/Models/Consent.cs) (passa a chavear por `SubjectId`).

---

## 13. Conclusão

O redesign troca **dois modelos paralelos + objeto rico ligado a serviços** por **um registro
serializável (`UserAccount`) com submodelos** (`UserSecurityState`/`PasswordCredential`/`UserClaim`,
`ExternalIdentity` reservado) e **serviços focados** (`IUserAuthenticator`, `LockoutPolicy`,
`IUserSessionService`, `ISubjectPrincipalFactory`), com **storage puro** e **telas reduzidas a cola**.
As correções de maior valor — **`SubjectId` imutável**, **semântica única de "ativo"**, **fim da sessão
como efeito colateral**, **remoção do provider quebrado** — são feitas agora, no momento mais barato
(sem dados persistidos). A extensibilidade por realm passa a ser por **composição** (ADR-013),
preservando a intenção da ADR-005. O futuro (externo/MFA/admin/device-sessions) é **costurado, não
construído**. Todas as invariantes de negócio (§8) são preservadas, governadas pela matriz de
testes/critérios (§10). Pronto para **ADR-013 + plano**.
