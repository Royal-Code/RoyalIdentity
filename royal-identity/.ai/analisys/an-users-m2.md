# Análise — Unificação de Usuários e Sessão (m2)

> Documento de **análise** (não é plano de execução). Objetivo: entender o que existe
> hoje no domínio de Usuários/Sessão, diagnosticar a complexidade e o acoplamento, e
> propor uma modelagem que isole responsabilidades e simplifique o uso pelos serviços
> de tela.
>
> Referência da tarefa: seção **Users** de [redesign-todo.md](../../redesign-todo.md) e
> decisão [ADR-005](../../adrs/ADR-005.md).

---

## 1. Escopo

A seção Users do `redesign-todo.md` pede:

> "Unificar a lógica de usuários. Existe IdentityUser, UserDetails, IUserStore e
> IUserDetailsStore. Há IdentitySession e IUserSessionStore. Há lógica confusa entre
> usuários e sessões. Precisa unificar o usuário e revisar a sessão e o login."

O ponto central levantado: **os serviços de tela precisam conhecer demais o
comportamento das classes de usuário e de sessão**. A análise foca em:

1. inventariar classes e responsabilidades atuais;
2. mapear os fluxos e regras existentes;
3. diagnosticar os acoplamentos e duplicações;
4. propor um modelo que separe **dado** de **comportamento** e concentre o login/sessão
   atrás de poucas fachadas;
5. avaliar quais regras podem mudar (com justificativa e impacto) e quais funcionalidades
   podem melhorar.

---

## 2. Inventário atual

### 2.1 Tipos de domínio

| Tipo | Local | Natureza | Papel |
|---|---|---|---|
| `IdentityUser` (abstract) | [RoyalIdentity/Users/IdentityUser.cs](../../RoyalIdentity/Users/IdentityUser.cs) | Objeto rico (comportamento) | Identidade + autenticação + criação de sessão + montagem de principal |
| `DefaultIdentityUser` (sealed) | [RoyalIdentity/Users/Defaults/DefaultIdentityUser.cs](../../RoyalIdentity/Users/Defaults/DefaultIdentityUser.cs) | Implementação | Carrega 4 serviços; valida senha, lockout, sessão, claims |
| `UserDetails` | [RoyalIdentity/Users/Contracts/UserDetails.cs](../../RoyalIdentity/Users/Contracts/UserDetails.cs) | POCO (dado) | Registro persistido do usuário |
| `IdentitySession` | [RoyalIdentity/Users/IdentitySession.cs](../../RoyalIdentity/Users/IdentitySession.cs) | Registro (dado + 1 ref rica) | Sessão; guarda `IdentityUser` vivo |
| `CredentialsValidationResult` (struct) | [RoyalIdentity/Users/CredentialsValidationResult.cs](../../RoyalIdentity/Users/CredentialsValidationResult.cs) | Resultado | Sucesso(user+session) \| falha(reason+msg) |
| `ValidateCredentialsResult` (struct) | [RoyalIdentity/Users/ValidateCredentialsResult.cs](../../RoyalIdentity/Users/ValidateCredentialsResult.cs) | Resultado | isValid + session (conversões implícitas) |
| `AuthorizationContext` | [RoyalIdentity/Users/Contexts/AuthorizationContext.cs](../../RoyalIdentity/Users/Contexts/AuthorizationContext.cs) | DTO de interação | Snapshot do pedido authorize para a UI |

### 2.2 Contratos de storage / serviços

| Contrato | Local | Operações |
|---|---|---|
| `IUserStore` | [RoyalIdentity/Users/Contracts/IUserStore.cs](../../RoyalIdentity/Users/Contracts/IUserStore.cs) | `GetUserAsync(userName) → IdentityUser?`, `IsUserActive(userName)` |
| `IUserDetailsStore` | [RoyalIdentity/Users/Contracts/IUserDetailsStore.cs](../../RoyalIdentity/Users/Contracts/IUserDetailsStore.cs) | `GetUserDetailsAsync(userName) → UserDetails?`, `SaveUserDetailsAsync` |
| `IUserSessionStore` | [RoyalIdentity/Users/Contracts/IUserSessionStore.cs](../../RoyalIdentity/Users/Contracts/IUserSessionStore.cs) | `StartSessionAsync`, `EndSessionAsync`, `GetUserSessionAsync`, `GetCurrentSessionAsync`, `AddClientIdAsync` |
| `IPasswordProtector` | [RoyalIdentity/Users/Contracts/IPasswordProtector.cs](../../RoyalIdentity/Users/Contracts/IPasswordProtector.cs) | `HashPasswordAsync`, `VerifyPasswordAsync` |
| `ISignInManager` | [RoyalIdentity/Users/ISignInManager.cs](../../RoyalIdentity/Users/ISignInManager.cs) | `GetAuthorizationContextAsync`, `AuthenticateUserAsync`, `SignInAsync`, `ConsentRequired` |
| `ISignOutManager` | [RoyalIdentity/Users/ISignOutManager.cs](../../RoyalIdentity/Users/ISignOutManager.cs) | `CreateLogoutIdAsync`, `SignOutAsync` |
| `IProfileService` | [RoyalIdentity/Contracts/IProfileService.cs](../../RoyalIdentity/Contracts/IProfileService.cs) | `GetProfileDataAsync`, `IsActiveAsync` |

Implementações in-memory: `UserStore` implementa **simultaneamente** `IUserStore` **e**
`IUserDetailsStore` ([RoyalIdentity.Storage.InMemory/UserStore.cs](../../RoyalIdentity.Storage.InMemory/UserStore.cs)),
e `UserSessionStore` ([RoyalIdentity.Storage.InMemory/UserSessionStore.cs](../../RoyalIdentity.Storage.InMemory/UserSessionStore.cs)).

### 2.3 Consumidores na UI (serviços de tela)

- [SessionContextService](../../RoyalIdentity.Razor/Services/SessionContextService.cs) — fachada fina sobre `HttpContext` (realm + `AuthorizationContext`).
- [LoginPageService](../../RoyalIdentity.Razor/Services/LoginPageService.cs) — usa `ISignInManager` intensamente; **carrega a máquina de estados do login**.
- [ConsentPageService](../../RoyalIdentity.Razor/Services/ConsentPageService.cs) — usa `IConsentService` + `AuthorizationContext`.
- [EndSessionPageService](../../RoyalIdentity.Razor/Services/EndSessionPageService.cs) — usa `ISignOutManager`.
- [IdentityUserManager](../../RoyalIdentity.Razor/Services/IdentityUserManager.cs) — usa `storage.GetUserStore(...).GetUserAsync` (objeto rico).
- [IdentityRevalidatingAuthenticationStateProvider](../../RoyalIdentity.Razor/Services/IdentityRevalidatingAuthenticationStateProvider.cs) — resolve `IUserStore` via DI.

---

## 3. Fluxos atuais

### 3.1 Login interativo

1. **GET** `LoginPage` → `LoginPageService.BuildViewModelAsync` → `SessionContextService.GetAuthorizationContextAsync`
   (que valida o pedido authorize via `ISignInManager.GetAuthorizationContextAsync`) → monta `LoginViewModel`.
2. **POST** `LoginPage` → `LoginPageService.LoginAsync`:
   - `signInManager.AuthenticateUserAsync(realm, user, senha)`:
     - `IUserStore.GetUserAsync` → cria `DefaultIdentityUser` (injetando store, sessionStore, passwordProtector, options, clock);
     - checa `user.IsActive`;
     - checa `user.IsLockoutAsync()`;
     - `user.AuthenticateAndStartSessionAsync(senha)` → verifica senha, **muta e persiste** contadores de erro, e **inicia a sessão** (`sessionStore.StartSessionAsync`);
     - retorna `CredentialsValidationResult`.
   - dispara evento de login;
   - `signInManager.SignInAsync(user, session, remember)` → `user.CreatePrincipalAsync(session)` → `httpContext.SignInAsync(scheme, principal, props)`;
   - decide rota: consent requerido? página intermediária? redirect final? → devolve `LoginResult`.

### 3.2 Authorize (decorator de prompt/login)

[PromptLoginDecorator](../../RoyalIdentity/Contexts/Decorators/PromptLoginDecorator.cs) **não usa `IdentityUser`**:
usa `IProfileService.IsActiveAsync(principal, client, ...)` (que lê `UserDetails` + sessão), além de `prompt`,
`idp`, `max_age`, `UserSsoLifetime`.

### 3.3 Emissão de token

`DefaultTokenFactory` → `DefaultTokenClaimsService` ([código](../../RoyalIdentity/Contracts/Defaults/DefaultTokenClaimsService.cs))
→ `IProfileService.GetProfileDataAsync` (lê `UserDetails`). O `sid` sai do principal.
`DefaultCodeFactory.CreateCodeAsync` chama `IUserSessionStore.AddClientIdAsync(sid, clientId)` para registrar o cliente na sessão.

### 3.4 Logout

`ISignOutManager.SignOutAsync` → `IUserSessionStore.EndSessionAsync(sid)` (marca `IsActive=false`) → `SignOutAsync` do cookie →
evento → front/back-channel logout iterando `session.Clients` → redirect/callback.

### 3.5 Verificação de "ativo"

Há **três** caminhos distintos:
- `DefaultProfileService.IsActiveAsync` — `UserDetails.IsActive` **e** (se existir sessão, `session.IsActive`). Se sessão for `null`, considera ativo.
- `HttpContextExtensions.ValidateUserSessionAsync` — exige sessão `{ IsActive: true }` (sessão `null` ⇒ inativo).
- `IdentityRevalidatingAuthenticationStateProvider` — só `IUserStore.IsUserActive` (ignora sessão).

---

## 4. Regras / invariantes existentes

1. **`sub` == `UserName`** — `DefaultIdentityUser.CreatePrincipalAsync` emite `sub = details.Username`; `ProfileService`
   consulta por `GetSubjectId()` (= username). Não existe id imutável separado.
2. **Sessão é criada na verificação de senha** — efeito colateral de `AuthenticateAndStartSessionAsync`.
3. **Lockout** = contadores em `UserDetails` (`LoginAttemptsWithPasswordErrors`, `LastPasswordError`) + política em
   `DefaultIdentityUser.IsLockoutAsync` lendo `AccountOptions.PasswordOptions` (`MaxFailedAccessAttempts`, `AccountLockoutDurationMinutes`).
4. **Sessão guarda a lista de `Clients`** para front/back-channel logout (`AddClientIdAsync` durante a emissão do code).
5. **Consentimento** é por subject+client+scope (fora do escopo desta análise, mas acoplado ao `subject = username`).
6. **Realm isolation** — todo store é obtido por realm (`storage.GetXStore(realm)`).
7. **`amr`/`auth_time`/`idp`/`sid`** entram no principal no login e são relidos para os tokens.

---

## 5. Diagnóstico — problemas e acoplamentos

### 5.1 Dois modelos paralelos para o mesmo usuário
Existe um objeto **rico** (`IdentityUser`, comportamento) e um **POCO** (`UserDetails`, dado), com **dois stores**
(`IUserStore` / `IUserDetailsStore`). Mas a **mesma** classe `UserStore` implementa os dois — a separação não compra
isolamento real, só dobra a superfície. O login usa `IdentityUser`; token/profile/active-check usam `UserDetails`
direto. **Não há fonte única de verdade**: "usuário ativo" é calculado de três formas diferentes (§3.5).

### 5.2 `IdentityUser` é um objeto rico que carrega serviços
Uma instância de usuário guarda `IUserSessionStore`, `IUserDetailsStore`, `IPasswordProtector`, `AccountOptions`,
`TimeProvider`. Consequências:
- buscar um usuário **materializa um agregado ligado a serviços** (não é leitura de dado);
- **não é serializável/cacheável** — problema direto para o backend SQL/Redis planejado;
- difícil de testar isoladamente;
- mistura, num só tipo: autenticação, política de lockout, ciclo de vida de sessão e montagem de claims.

### 5.3 `AuthenticateAndStartSessionAsync` mistura responsabilidades
Esse método (a) valida a senha, (b) muta+persiste contadores de falha e (c) **inicia a sessão** — tudo junto.
A sessão nasce como efeito colateral da checagem de credencial, **antes** de o `SignInManager` decidir efetivamente
logar. Logins externos/2FA não encaixam nesse formato sem reescrever o método.

### 5.4 Lockout "split-brain"
A verificação de lockout acontece **antes** da senha (`AuthenticateUserAsync`), mas o incremento do contador acontece
**depois**, **dentro** de `AuthenticateAndStartSessionAsync`. Política no `DefaultIdentityUser`, contadores em
`UserDetails`, orquestração no `SignInManager`: **três lugares** para uma regra.

### 5.5 `IdentitySession` referencia o `IdentityUser` vivo
`IdentitySession.User` é o objeto pesado ligado a serviços. Isso impede um registro de sessão limpo e serializável —
de novo, bloqueia o backend persistente.

### 5.6 Storage acoplado ao HTTP
`UserSessionStore.GetCurrentSessionAsync` lê `IHttpContextAccessor`. Storage **não deveria** conhecer HTTP nem o
principal corrente. "Sessão atual" é conceito de aplicação/web, não de persistência. Além disso, `GetCurrentSessionAsync`
é redundante com `GetUserSessionAsync(principal.GetSessionId())`.

### 5.7 Dois tipos de resultado quase idênticos
`CredentialsValidationResult` e `ValidateCredentialsResult` modelam quase a mesma coisa em camadas adjacentes. As
conversões implícitas (`bool`/`IdentitySession` → resultado) escondem intenção (`return false` significando "credencial inválida").

### 5.8 `sub` acoplado ao `username` (correção latente)
Como `sub = username` e existe `AccountOptions.AllowChangeUsername`, **trocar o username muda o `sub`** — quebrando
continuidade de tokens e registros de consentimento chaveados por `sub`. O `sub` deve ser **estável e imutável** por OIDC.
É o problema de maior impacto correcional aqui.

### 5.9 Montagem de claims duplicada
O principal de autenticação é montado em `DefaultIdentityUser.CreatePrincipalAsync`; os claims de token são montados
em `DefaultTokenClaimsService` + `DefaultProfileService`, com regras próprias. Duas trilhas de "quais claims o usuário tem".

### 5.10 Código morto / quebrado
`IUserStore` **não está registrado no DI** (só `ISignInManager`, `ISignOutManager`, `IPasswordProtector` —
ver [ServiceCollectionExtensions](../../RoyalIdentity/Extensions/ServiceCollectionExtensions.cs)), mas
`IdentityRevalidatingAuthenticationStateProvider` faz `GetRequiredService<IUserStore>()` e **ignora o realm** —
lançaria exceção se esse caminho rodasse, e contradiz o isolamento por realm.

### 5.11 A complexidade vaza para a tela (o ponto central)
`LoginPageService.LoginAsync` orquestra autenticar → evento → sign-in → checar consent → rotear redirect, manipulando
`IdentityUser`, `IdentitySession`, `CredentialsValidationResult` e `AuthorizationContext`. **A máquina de estados do
login mora no serviço de tela.** É exatamente a queixa: comportamento demais nas telas.

### 5.12 Inconsistências menores
- `UserDetails` está em `Users/Contracts/`, enquanto `IdentityUser`/`IdentitySession` estão em `Users/`.
- `AuthorizationContext` está em `Users/Contexts/`, embora seja um contexto de **interação/UI**.
- `AddClientIdAsync` muta `IList<string>` sem dedupe/concorrência.

---

## 6. Modelagem proposta

Princípio condutor: **separar dado de comportamento** e **quebrar o `IdentityUser` rico em serviços focados**, deixando
as telas dependerem só de fachadas (`ISignInManager`/`ISignOutManager`) e de DTOs simples.

### 6.1 Dado: um único registro de usuário
Fundir `UserDetails` + a parte de **dados** do `IdentityUser` em **uma** entidade `User` (POCO serializável), fonte única
de verdade. Campos:
- `SubjectId` (**novo**, imutável, estável) — o `sub`;
- `Username` (mutável, login);
- `DisplayName`, `IsActive`, `Roles`, `Claims`;
- credencial/lockout: `PasswordHash`, `LoginAttemptsWithPasswordErrors`, `LastPasswordError`.

> Para preservar a extensibilidade por realm da ADR-005 (ver §9), o `User` pode permanecer extensível
> (ex.: `Claims`/`Roles` + ganchos de perfil), mas **sem** carregar serviços.

### 6.2 Um único store de usuário (dados)
`IUserStore` passa a ser **só dados**:
`FindBySubjectIdAsync`, `FindByUsernameAsync`, `SaveAsync`, e helpers de estado (`IsActiveAsync`).
**Elimina-se `IUserDetailsStore`** (fundido). O store devolve o registro, **não** um objeto ligado a serviços; deixa de
ser fábrica de objetos ricos.

### 6.3 Autenticação como serviço (não no usuário)
`IUserAuthenticator` (ou `ICredentialValidator`): recebe `User` + senha, verifica via `IPasswordProtector`, aplica a
**política de lockout** (incrementa/zera contadores via store) e retorna **um único** resultado discriminado
(`Authenticated(user)` | `Failed(reason, message)` com reasons `NotFound/Inactive/InvalidCredentials/Blocked`).
Abre caminho para 2FA / externo / passwordless sem inchar o `User`.

### 6.4 Lockout como política única
Pequena política (`LockoutPolicy`) que lê `AccountOptions.PasswordOptions` e opera sobre os contadores do `User` —
função pura + atualização no store. **Um lugar só** para a regra (resolve §5.4).

### 6.5 Sessão: dado puro + contexto de aplicação
- `IUserSessionStore` fica **só dados**: `StartSessionAsync`, `EndSessionAsync`, `GetByIdAsync`, `AddClientIdAsync`.
  **Remove `GetCurrentSessionAsync`** (sai do storage).
- `IdentitySession` vira **registro serializável**: guarda `SubjectId` (não o objeto `User` vivo). Resolve §5.5.
- "Sessão atual" passa para a camada web (ex.: um `ICurrentSession`/extensão sobre `HttpContext` que lê o `sid` do
  principal e chama `GetByIdAsync`). Resolve §5.6.

### 6.6 Claims em um lugar só
`IUserClaimsFactory` único monta o `ClaimsPrincipal` de autenticação no login, reaproveitando as regras de claims já
usadas para o id_token onde fizer sentido. Remove `CreatePrincipalAsync` do usuário (resolve §5.9).

### 6.7 Fachadas finas para a tela
- `ISignInManager` = orquestrador fino: `authenticator → sessionStore.Start → claimsFactory → cookie sign-in`.
- `ISignOutManager` = consome `IUserSessionStore` + notifiers (praticamente como hoje).
- **A máquina de estados do login** (consent? página intermediária? redirect?) sai do `LoginPageService` e vira um
  método do manager que devolve um **enum de desfecho** + URL. A tela vira **cola** (resolve §5.11).

### 6.8 "Ativo" unificado
Uma só semântica de ativo/sessão consumida por `ProfileService`, `PromptLoginDecorator`, `ActiveUserValidator` e o
revalidating provider; decidir explicitamente a semântica de **sessão nula** (recomendado: sessão ausente ⇒ não há
sessão válida para checagens que exigem sessão; checagens de "conta ativa" olham só `User.IsActive`). Resolve §3.5/§5.1.

### 6.9 Resultado único
Um único tipo de resultado de autenticação (§6.3), eliminando `ValidateCredentialsResult` **e**
`CredentialsValidationResult` (resolve §5.7).

### Esboço (apenas direção, não plano)

```
Camada web (telas)        ISignInManager / ISignOutManager / IConsentService  +  AuthorizationContext (interação)
                              │
Serviços de usuário        IUserAuthenticator ──► LockoutPolicy
                           IUserClaimsFactory
                           ISessionService (start/end/current na app)
                              │
Storage (dados puros)      IUserStore (User)      IUserSessionStore (IdentitySession)
```

---

## 7. Regras que podem mudar (com justificativa e impacto)

| # | Mudança | Justificativa | Impacto / risco |
|---|---|---|---|
| R1 | **`sub` ≠ `username`**: introduzir `SubjectId` imutável | OIDC exige `sub` estável; hoje trocar username quebra tokens/consent (§5.8) | **Alto.** Mexe em claims, `ProfileService`, chaves de consent. Migração de dados existentes. **Requer ADR.** |
| R2 | **Sessão criada só no sign-in** (não na verificação de senha) | Evita sessão órfã; esclarece ciclo de vida; encaixa 2FA/externo (§5.3) | Médio. Reordena `SignInManager`. |
| R3 | **Storage não lê HttpContext** ("sessão atual" sai do store) | Respeita as camadas; SQL/Redis não pode depender de HTTP (§5.6) | Baixo/médio. Move `GetCurrentSessionAsync` p/ web. |
| R4 | **Fundir os dois stores e os dois resultados** | Pura simplificação, sem mudança de comportamento (§5.1, §5.7) | Baixo. |
| R5 | **Lockout numa política única** | Hoje a regra está em 3 lugares (§5.4) | Baixo. |
| R6 | **Remover/registrar corretamente o revalidating provider** | Hoje resolve `IUserStore` não registrado e ignora realm (§5.10) | Baixo. Decidir se a UI SSR precisa dele. |

> R1 é o único com impacto correcional/protocolar relevante; deve ser confirmado e
> registrado em ADR antes de implementar. As demais são refatorações estruturais.

---

## 8. Funcionalidades que podem melhorar (com justificativa)

1. **Checagem de "ativo" consistente** — uma fonte só, comportamento previsível em token/authorize/userinfo/UI.
2. **Autenticador plugável** — habilita 2FA, login externo e passwordless sem inchar o usuário (extensibilidade da ADR-005 por **estratégia**, não por herança).
3. **Registros serializáveis** (User e Session) — pré-requisito real para o backend SQL/Redis planejado.
4. **Claims montados uma vez** — menos divergência entre principal de login e claims de token.
5. **Telas como cola** — `LoginPageService`/`ConsentPageService`/`EndSessionPageService` deixam de carregar regra de negócio; a localização (próxima seção do todo) fica mais simples por haver menos pontos de texto.

---

## 9. Tensão com a ADR-005 (honestidade técnica)

A [ADR-005](../../adrs/ADR-005.md) decidiu: gerenciamento de usuários próprio, **configurável por realm**, com
possibilidade de **"criar novas entidades de usuário e regras específicas para cada realm"**. O `IdentityUser` abstrato
de hoje materializa essa intenção via **herança** (entidade rica por realm).

A proposta de **colapsar `IdentityUser` num registro de dados** contraria a *forma* (herança rica), mas **preserva a
intenção** (extensibilidade por realm) trocando-a por **estratégia/composição**: `IUserAuthenticator`,
`IUserClaimsFactory`, `IProfileService` e o próprio `User` extensível (claims/roles/ganchos) são os pontos de
customização por realm. Isso é, na minha avaliação, mais aderente ao resto da arquitetura (pipeline + contratos +
defaults substituíveis) do que herança de entidade.

**Recomendação:** tratar isto como mudança de decisão — atualizar/superseder a ADR-005 (ou abrir nova ADR) deixando
explícito que a extensibilidade por realm passa a ser por composição de serviços, não por herança de `IdentityUser`.
Não implementar silenciosamente contra a ADR vigente.

---

## 10. Pontos de decisão (com recomendação)

Os 5 pontos abaixo precisam de decisão antes de um plano. O veredito de cada um está
resumido aqui; a justificativa e a abordagem estão em **§12**.

1. **R1 (`SubjectId` imutável)** — introduzir e estratégia de migração do seed. → **Recomendação: fazer agora.**
2. **ADR-005** — virada de herança → composição (§9). → **Recomendação: nova ADR-013 que supersede a 005.**
3. **Semântica de sessão nula** nas checagens de "ativo" (§6.8). → **Recomendação: separar "conta ativa" de "sessão válida"; sessão ausente ⇒ inválida.**
4. **Destino do `IdentityUser` abstrato** — eliminar ou manter como extensão mínima? → **Recomendação: eliminar.**
5. **Revalidating provider** (§5.10). → **Recomendação: remover agora; reintroduzir correto só quando houver área interativa autenticada.**

---

## 11. Resumo

O subsistema de usuários hoje sofre de **duplicação de modelo** (rico vs POCO, dois stores, dois resultados),
**objeto de usuário sobrecarregado** (carrega serviços, mistura autenticação/lockout/sessão/claims), **storage
acoplado ao HTTP**, **`sub` preso ao username** (correção latente) e **vazamento da máquina de estados do login para a
tela**. A direção proposta — **dado puro + serviços focados + fachadas finas** — unifica o usuário, torna sessão/usuário
serializáveis (pré-requisito do backend persistente), concentra a regra de login fora das telas e mantém a
extensibilidade por realm via composição. As mudanças estruturais (R2–R6) são de baixo/médio risco; a única com
impacto protocolar (R1, `sub` imutável) e a virada da ADR-005 devem ser decididas e registradas antes de qualquer
implementação.

---

## 12. Recomendações (decisões propostas)

Respostas recomendadas para os 5 pontos da §10, com justificativa e abordagem.

### 12.1 `SubjectId` imutável → fazer agora
`sub` tem de ser imutável por OIDC e hoje quebra ao trocar o username (§5.8). O argumento
decisivo é de **timing**: o storage é só in-memory/seed, **não há dado persistido para
migrar**. Adiar até o backend SQL existir transforma uma mudança trivial numa migração de
dados real.

**Abordagem:**
- `User.SubjectId` (string, gerada por `CryptoRandom`/GUID), imutável;
- seed/demo com IDs **determinísticos** (constantes) para os testes ficarem estáveis;
- `IUserStore` ganha `FindBySubjectIdAsync` + `FindByUsernameAsync` — login busca por
  username, o principal emite `sub = SubjectId`;
- `ProfileService` passa a consultar por `SubjectId`;
- `Consent.SubjectId` e `RefreshToken` já gravam `GetSubjectId()` → passam a carregar o id
  estável sem mudança estrutural;
- escopo de unicidade: por realm bastaria (sub único por issuer), mas GUID já é global — mais simples.

### 12.2 ADR-005 → nova ADR-013 que supersede
ADRs são registro append-only; não editar a 005 no lugar. Escrever **ADR-013** ("modelo de
usuário por composição"), marcando *Supersedes ADR-005*. Preserva a intenção da 005 (gestão
própria de usuários, customização por realm, sem ASP.NET Identity) e muda só o **mecanismo**:
extensão por composição de serviços em vez de herança rica. Registrar **antes** de implementar.

### 12.3 Semântica de sessão nula → separar "conta ativa" de "sessão válida"
Hoje o `ProfileService` é leniente (sessão `null` ⇒ ativo). Recomendo apertar e dividir em
duas perguntas, cada uma com **uma** implementação:
- **"Conta habilitada?"** → só `User.IsActive` (revalidating provider, userinfo);
- **"Sessão válida?"** → exige sessão existente **e** `IsActive`; **ausente ⇒ inválida**.
  Usada sempre que o principal traz `sid` (grants de usuário: re-uso do authorize, refresh).

**Justificativa:** correção/segurança e alinhamento com o backend de cache com TTL planejado,
que pode **evictar** a sessão — aí `null` *deve* falhar, não passar. Hoje o logout só seta
`IsActive=false` (não remove), então a leniência ainda não morde; ela morde no cache evictável.
Impacto baixo agora, correto para o futuro.

### 12.4 `IdentityUser` abstrato → eliminar
Manter um tipo abstrato com comportamento reconvida o padrão objeto-rico e o problema dos dois
modelos (§5.1, §5.2). Com a virada para composição (§12.2), a extensão vem dos serviços
(`IUserAuthenticator`, `IUserClaimsFactory`, `IProfileService`) e do **saco de `Claims`/`Roles`**
do `User` — que é exatamente como o `ProfileService` já projeta atributos. Atributos custom por
realm vivem nos `Claims`, não em subclasses (documentar na ADR-013). Resultado: um único `User`
concreto e serializável.

### 12.5 Revalidating provider → remover agora
Fatos verificados: está **registrado** como `AuthenticationStateProvider`
([RoyalIdentityRazorServiceCollectionExtensions.cs](../../RoyalIdentity.Razor/Extensions/RoyalIdentityRazorServiceCollectionExtensions.cs));
as páginas account são SSR, mas as **não-account são `InteractiveServer`**
([App.razor](../../RoyalIdentity.Server/Components/App.razor)); e `IUserStore` **não está
registrado em DI nenhum** → o provider **lança exceção** na primeira revalidação de circuito.
Está quebrado de fato, não só "morto". Além disso ignora o realm, e a [ADR-007](../../adrs/ADR-007.md)
avisa que `HttpContext` não é fonte estável em `InteractiveServer`.

**Recomendação:** remover como parte do redesign. Quando existir de fato uma área interativa
autenticada que precise de revalidação periódica, reintroduzir versão correta: dependência
registrada + realm capturado no início do circuito + `storage.GetUserStore(realm)` + a checagem
unificada do §12.3. Vale confirmar se alguma página `InteractiveServer` não-account já exige
autenticação: se nenhuma exige, a remoção é trivialmente segura; se exige, perde-se só a
revalidação periódica (o cookie continua autenticando) — aceitável e melhor que crashar.

### 12.6 Sequência sugerida
Há dependência entre os pontos:

**§12.2** (ADR-013, fixa a direção) → **§12.1 + §12.4** (modelo de dado novo, juntos) →
**§12.3** (checagem unificada) → **§12.5** (limpeza).
