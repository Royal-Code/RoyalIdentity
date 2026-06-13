# Analise Users M1

## Objetivo

Esta analise cobre a secao `Users` do `redesign-todo.md` e descreve o estado atual da modelagem de usuario, sessao, login, logout, profile claims e uso pelos servicos Razor.

O problema central confirmado no codigo e que cada classe tem uma intencao defensavel, mas as responsabilidades estao muito entrelacadas:

- `IdentityUser` representa o usuario autenticavel, mas tambem valida senha, inicia sessao e cria `ClaimsPrincipal`.
- `UserDetails` e o dado persistido, mas parte das regras de seguranca fica no wrapper `DefaultIdentityUser`.
- `IUserStore` e `IUserDetailsStore` separam usuario de detalhes, mas a implementacao atual e a mesma classe (`UserStore`).
- `IdentitySession` guarda um `IdentityUser` inteiro, alem do estado da sessao e clientes associados.
- `IUserSessionStore` tambem conhece o `HttpContext` para descobrir a sessao atual.
- `ISignInManager` funciona como fachada, mas ainda expoe `IdentityUser`, `IdentitySession` e `ClaimsPrincipal` aos servicos de tela.
- Os servicos Razor ficaram melhores depois da separacao UI Services, mas ainda precisam saber bastante sobre contexto de autorizacao, consentimento, sessao e retomada de fluxo.

O objetivo da nova modelagem deve ser reduzir o conhecimento exigido pela UI e tornar usuarios/sessoes persistiveis, realm-scoped e mais faceis de evoluir para registro, recuperacao de senha, troca de senha, MFA, login externo e administracao.

## Fontes analisadas

- `redesign-todo.md`, secao `Users`
- `adrs/ADR-005.md`
- `.ai/foundation/product.md`
- `.ai/foundation/tech.md`
- `.ai/foundation/structure.md`
- `RoyalIdentity/Users/*`
- `RoyalIdentity/Contracts/Defaults/DefaultProfileService.cs`
- `RoyalIdentity/Contracts/Defaults/DefaultTokenFactory.cs`
- `RoyalIdentity/Contracts/Defaults/DefaultCodeFactory.cs`
- `RoyalIdentity/Authentication/ConfigureRealmCookieAuthenticationOptions.cs`
- `RoyalIdentity/Extensions/AuthenticationExtensions.cs`
- `RoyalIdentity/Extensions/HttpContextExtensions.cs`
- `RoyalIdentity.Razor/Services/*`
- `RoyalIdentity.Storage.InMemory/UserStore.cs`
- `RoyalIdentity.Storage.InMemory/UserSessionStore.cs`
- `RoyalIdentity.Storage.InMemory/RealmMemoryStore.cs`
- Testes de login, consentimento, endsession, userinfo e realm isolation.

## Decisoes existentes

ADR-005 define que o RoyalIdentity tera gerenciamento proprio de usuarios, nao dependente do ASP.NET Identity. ASP.NET Identity pode servir como referencia, mas nao como dependencia estrutural.

As regras de usuario e dados devem ser configuraveis por realm. Isso e coerente com a arquitetura geral do produto: realm e o limite superior de isolamento para clientes, chaves, usuarios, sessoes, tokens, consentimentos e configuracao.

O estado atual em `redesign-todo.md` ja marca a area como divida de design: `IdentityUser`, `UserDetails`, stores de usuario/detalhes, `IdentitySession` e store de sessao precisam ser unificados/revistos.

## Estado atual do modelo

### UserDetails

`UserDetails` e o POCO persistido hoje:

- `Username`
- `PasswordHash`
- `DisplayName`
- `IsActive`
- `LoginAttemptsWithPasswordErrors`
- `LastPasswordError`
- `Roles`
- `Claims`

Ele mistura dados de perfil, credencial local, estado de lockout e claims emitidas. Isso funciona para demo/in-memory, mas fica limitado para persistencia real e para features futuras.

Pontos de atencao:

- `#nullable disable` reduz seguranca estatica.
- `Username` hoje tambem e usado como `sub`.
- `PasswordHash` nulo significa "nao aceita login por senha".
- Falhas de senha ficam no usuario/detalhe, nao em uma credencial local propria.
- `Roles` e `Claims` convivem, mas tambem existem claims `role` seedadas em `Claims`; isso pode duplicar conceito.

### IdentityUser

`IdentityUser` e uma abstracao de usuario autenticavel:

- Expoe `UserName`, `DisplayName`, `IsActive`.
- Valida senha e inicia sessao via `AuthenticateAndStartSessionAsync`.
- Verifica lockout via `IsLockoutAsync`.
- Cria cookie principal via `CreatePrincipalAsync`.

O nome sugere entidade de dominio, mas na pratica ela combina entidade, policy, credential validator, session starter e principal factory.

`DefaultIdentityUser` implementa:

- Verificacao de hash de senha.
- Incremento/reset de contador de falhas.
- Calculo de lockout com `AccountOptions.PasswordOptions`.
- Criacao de sessao no `IUserSessionStore`.
- Montagem do `ClaimsPrincipal` com `sub`, `name`, `auth_time`, `sid`, `idp`, `amr`, roles e claims.

Essa classe e o principal ponto de acoplamento. A UI nao chama tudo diretamente, mas `ISignInManager` depende dela e repassa seus tipos para fora.

### Stores de usuario

Hoje ha dois contratos:

- `IUserStore`: busca `IdentityUser` e verifica se o usuario esta ativo.
- `IUserDetailsStore`: busca/salva `UserDetails`.

Na implementacao in-memory, ambos sao a mesma classe (`UserStore`). O `IStorage` expõe os dois separadamente por realm:

- `GetUserStore(realm)`
- `GetUserDetailsStore(realm)`

Isso cria duplicidade conceitual:

- `IUserStore` devolve um objeto com comportamento (`IdentityUser`).
- `IUserDetailsStore` devolve os dados crus (`UserDetails`).
- Servicos de profile usam `IUserDetailsStore`.
- Login usa `IUserStore`.
- Revalidacao de auth state tenta resolver `IUserStore` direto por DI, embora o padrao arquitetural correto seja resolver via `IStorage.GetUserStore(realm)`.

### IdentitySession

`IdentitySession` guarda:

- `Id` como `sid`
- `User` como `IdentityUser`
- `Amr`
- `Clients`
- `IsActive`
- `StartedAt`

Ela representa a sessao SSO do usuario no realm, mas tem duas fragilidades:

- Guarda um objeto `IdentityUser`, o que dificulta persistencia e serializacao. Uma sessao persistida deveria guardar identificadores e snapshot minimo, nao um objeto de comportamento.
- `Clients` e `IList<string>` mutavel. `UserSessionStore.AddClientIdAsync` faz `session?.Clients.Add(clientId)`, sem deduplicacao ou controle de concorrencia.

### UserSessionStore

`IUserSessionStore` faz:

- `StartSessionAsync(IdentityUser user, string amr)`
- `EndSessionAsync(sessionId)`
- `AddClientIdAsync(sessionId, clientId)`
- `GetCurrentSessionAsync()`
- `GetUserSessionAsync(sessionId)`

Na implementacao in-memory, `GetCurrentSessionAsync` usa `IHttpContextAccessor`, le o usuario autenticado atual e extrai `sid`. Esse e um cheiro arquitetural: store deveria ser persistencia realm-scoped, nao servico dependente de HTTP.

### SignInManager

`ISignInManager` hoje e uma fachada parcialmente boa:

- Resolve contexto de autorizacao a partir de `returnUrl`.
- Autentica usuario local por realm.
- Faz sign-in no cookie.
- Verifica se consentimento e necessario.

Mas a interface ainda vaza tipos internos:

- Entrada/saida com `IdentityUser`.
- Entrada/saida com `IdentitySession`.
- Saida com `ClaimsPrincipal`.

Isso obriga `LoginPageService` e testes/host endpoints a conhecerem o modelo de usuario/sessao, quando o ideal seria lidar com resultados de caso de uso.

### SignOutManager

`ISignOutManager` esta melhor delimitado que sign-in:

- Cria logout id para usuario atual.
- Executa logout a partir de `LogoutMessage`.

Ele ainda faz muita coisa internamente, mas isso e aceitavel para uma fachada de caso de uso:

- Encerra sessao no store.
- Remove cookie do realm.
- Dispara evento.
- Calcula front-channel logout.
- Envia back-channel logout.
- Decide redirect automatico ou tela intermediaria.

O ponto a melhorar e depender de `IdentitySession.User.UserName` para `sub` no back-channel logout. Com a nova sessao, isso deveria vir de `SubjectId`.

### ProfileService

`DefaultProfileService` usa `IUserDetailsStore` para:

- Carregar claims de perfil.
- Emitir `name`, `preferred_username`, roles e claims solicitadas.
- Validar se usuario e sessao continuam ativos.

Esse comportamento e correto em termos de responsabilidade OIDC, mas depende dos dados crus de usuario e do store de sessao. Uma modelagem melhor manteria `IProfileService` como adaptador OIDC, mas delegaria carregamento de usuario/claims para um servico de dominio.

### Cookie e sessao ativa

`ConfigureRealmCookieAuthenticationOptions` configura cookie por realm e define `OnValidatePrincipal`.

`OnValidatePrincipal` chama `HttpContext.ValidateUserSessionAsync`, que:

- Extrai `sid` do `ClaimsPrincipal`.
- Resolve `IStorage`.
- Usa o realm atual.
- Busca sessao no `IUserSessionStore`.
- Aceita o cookie apenas se a sessao estiver ativa.

Essa regra e importante e deve ser preservada. Ela garante que logout invalida a sessao e que o cookie nao continua valido indefinidamente contra uma sessao encerrada.

### Authorize, login, consent e code

Fluxo atual:

1. `/connect/authorize` cria `AuthorizeContext`.
2. `PromptLoginDecorator` exige login quando:
   - `prompt=login` ou `prompt=select_account`
   - usuario nao autenticado
   - usuario/sessao inativa
   - `max_age` vencido
   - restricoes de IdP/local login nao batem
   - `UserSsoLifetime` do client venceu
3. `InteractionResponse(IsLogin=true)` redireciona para pagina de login.
4. `LoginPageService.LoginAsync` valida credenciais via `ISignInManager`.
5. `ISignInManager.AuthenticateUserAsync` carrega usuario, valida ativo/lockout/senha e inicia sessao.
6. `ISignInManager.SignInAsync` cria principal e grava cookie no scheme do realm.
7. Se houver authorize context, `LoginPageService` decide consentimento e redireciona para consent/callback.
8. `ConsentDecorator` e `ConsentPageService` tratam consentimento.
9. `AuthorizeCallbackEndpoint` recria `AuthorizeContext` com o usuario autenticado.
10. `AuthorizeHandler` emite code/token/id_token.
11. `DefaultCodeFactory.CreateCodeAsync` registra o `client_id` na sessao do usuario com `AddClientIdAsync`.

O passo 11 e essencial para logout SSO: a sessao sabe quais clients precisam receber front/back-channel logout.

### End session

Fluxo atual:

1. `/connect/endsession` aceita GET/POST.
2. `EndSessionDecorator` valida `id_token_hint` quando presente ou exige usuario autenticado.
3. `EndSessionHandler` cria `LogoutMessage`.
4. Se o client permite logout sem confirmacao e a sessao tem apenas esse client, `ShowSignoutPrompt=false`.
5. UI de logout chama `EndSessionPageService`.
6. `EndSessionPageService` le a mensagem e chama `ISignOutManager.SignOutAsync`.
7. `DefaultSignOutManager` encerra a sessao, remove cookie, dispara evento, notifica clients e calcula redirect final.

Esse fluxo e razoavel e deve ser preservado, mas a sessao precisa deixar de carregar `IdentityUser`.

## Regras existentes que devem ser preservadas

1. Usuario e sessao sao realm-scoped.
2. Login local so deve ocorrer se o realm permite `AllowLocalLogin`.
3. Login local em authorize tambem deve respeitar `Client.EnableLocalLogin`.
4. Restricoes de IdP do client devem continuar valendo.
5. Usuario inexistente, inativo, bloqueado ou senha invalida deve produzir mensagem generica por default para evitar enumeracao.
6. Motivo interno de falha ainda pode ser preservado para eventos/auditoria.
7. Falha de senha deve incrementar contador e registrar a ultima falha.
8. Sucesso de senha deve resetar contador de falhas.
9. Lockout deve respeitar `MaxFailedAccessAttempts` e `AccountLockoutDurationMinutes`.
10. Senha ausente/hash nulo deve significar que login por senha nao e permitido para aquele usuario.
11. Sessao deve ser criada apenas depois de autenticacao bem sucedida.
12. Cookie do realm deve conter claims suficientes para OIDC: `sub`, `sid`, `auth_time`, `amr`, `idp` e `name`.
13. Cookie deve ser validado contra sessao ativa.
14. Logout deve marcar sessao como inativa.
15. Emissao de authorization code deve registrar o client na sessao.
16. Front/back-channel logout devem usar os clients registrados na sessao.
17. `prompt`, `max_age`, `UserSsoLifetime` e restricoes de IdP devem continuar sendo avaliados antes de emitir tokens.
18. `IProfileService.IsActiveAsync` deve continuar validando usuario ativo e sessao ativa para emissao/validacao de tokens.
19. Claims de profile devem continuar sendo filtradas pelos identity scopes solicitados.
20. Consentimento segue separado: per user, client, scope e realm.

## Problemas atuais

### 1. Entidade de usuario com comportamento demais

`IdentityUser` e `DefaultIdentityUser` acumulam responsabilidades que deveriam estar separadas:

- Dados do usuario.
- Validacao de credencial.
- Politica de lockout.
- Criacao de sessao.
- Criacao de principal/cookie claims.

Isso torna dificil trocar uma parte sem afetar as outras.

### 2. Duplicidade entre UserDetails e IdentityUser

`UserDetails` e persistencia. `IdentityUser` e comportamento. Mas o resto do sistema as vezes precisa de um, as vezes de outro.

O resultado e que a modelagem nao deixa claro qual e a "verdadeira entidade de usuario".

### 3. Stores com responsabilidades misturadas

`IUserStore` devolve objeto com comportamento. `IUserDetailsStore` devolve dados. Ambos sao implementados pela mesma classe.

Para persistencia real, isso tende a piorar:

- Um store deveria salvar/carregar dados, nao fabricar objetos ricos com dependencias.
- A fabricacao de modelos de dominio deveria ficar em servicos/factories.

### 4. Store de sessao depende de HttpContext

`IUserSessionStore.GetCurrentSessionAsync` usa `IHttpContextAccessor` na implementacao.

Isso torna o store menos puro, mais dificil de testar e mais dificil de substituir. O store deveria receber `sessionId` explicitamente. Descobrir a sessao atual a partir do cookie e uma responsabilidade de `IUserSessionService` ou `IAuthenticationSessionService`.

### 5. IdentitySession guarda IdentityUser

Sessao deveria guardar `SubjectId`, `DisplayName` opcional e metadados de autenticacao, nao um objeto `IdentityUser`.

Guardar o objeto inteiro:

- Complica serializacao/persistencia.
- Mantem referencias a stores/options dentro da sessao.
- Cria risco de estado desatualizado.
- Faz logout depender de `session.User.UserName`.

### 6. SubjectId e Username estao colados

Hoje `UserName` e descrito como identificador e mapeado para `sub`.

Isso conflita com opcoes ja existentes:

- `AllowChangeUsername`
- `EmailAsUsername`
- `LoginWithEmail`
- `AllowDuplicateEmail`

Em OIDC, `sub` deve ser identificador estavel do usuario para o issuer. Username/email podem mudar. Portanto o modelo novo deve separar `SubjectId` de `Username`.

### 7. Servicos de UI ainda sabem demais

`LoginPageService` hoje precisa:

- Resolver authorize context.
- Montar providers externos.
- Aplicar restricoes de local login/IdP.
- Chamar autenticacao.
- Disparar eventos.
- Fazer sign-in.
- Verificar consentimento.
- Decidir redirects de signed-in/consent/callback/profile/error.

Isso e melhor do que componente Razor com regra, mas ainda nao e uma fronteira ideal. A UI deveria lidar com view model e resultado de caso de uso; a regra de fluxo deveria ficar em um servico de interacao/autenticacao.

### 8. RevalidatingAuthenticationStateProvider parece desalinhado

`IdentityRevalidatingAuthenticationStateProvider` tenta resolver `IUserStore` diretamente por DI:

```csharp
var userStore = scope.ServiceProvider.GetRequiredService<IUserStore>();
```

O padrao atual do projeto e resolver usuario por realm via `IStorage.GetUserStore(realm)`. Nao ha registro padrao de `IUserStore` direto em `ServiceCollectionExtensions`. Alem disso, em SSR essa classe pode ser pouco exercitada, mas o desenho esta desalinhado.

### 9. Claims de usuario sao pouco estruturadas

`UserDetails.Claims` e `HashSet<Claim>`. Isso e pratico, mas fraco como contrato de persistencia:

- `Claim` e tipo de framework, nao um modelo persistente do dominio.
- Igualdade default de `Claim` nao e necessariamente a desejada.
- Falta separar claims de perfil, roles, emails, phone, linked identities e claims administrativas.

### 10. Login externo esta apenas parcialmente preparado

A tela lista external providers e aplica restricoes, mas a modelagem de usuario nao tem conceito claro de identidade externa vinculada:

- provider
- provider user id
- amr/idp
- mapeamento para usuario local
- criacao de usuario via login externo

ADR-005 pede flexibilidade por realm; essa extensao deveria entrar na modelagem agora, mesmo que a implementacao venha depois.

## Modelo recomendado

### Principio geral

Separar quatro conceitos que hoje estao sobrepostos:

1. Conta do usuario: dados persistidos e estado da conta.
2. Credenciais/identidades: formas de autenticar aquela conta.
3. Sessao: evento duravel de autenticacao no realm.
4. Principal OIDC/cookie: snapshot de claims para a requisicao.

### Entidades principais

#### UserAccount

Substitui `UserDetails` como entidade persistida principal.

Campos sugeridos:

- `SubjectId`: identificador estavel OIDC (`sub`), imutavel dentro do realm.
- `Username`: nome de login, mutavel conforme policy.
- `DisplayName`
- `IsActive`
- `CreatedAt`
- `UpdatedAt`
- `DisabledAt`
- `SecurityState`
- `Profile`
- `Claims`
- `Roles`
- `ExternalIdentities`

Observacoes:

- Para compatibilidade inicial, `SubjectId = Username` na migracao dos dados atuais.
- `Username` pode continuar sendo chave de busca, mas nao deve ser o identificador primario do OIDC.
- `RealmId` pode ser redundante se o store ja e realm-scoped, mas vale considerar guardar para auditoria e protecao contra uso cruzado.

#### UserSecurityState

Agrupa estado de seguranca local:

- `FailedPasswordAttempts`
- `LastPasswordFailureAt`
- `LockoutEndAt`
- `PasswordChangedAt`
- `SecurityStamp`
- `MustChangePassword`

Isso substitui `LoginAttemptsWithPasswordErrors` e `LastPasswordError` soltos no usuario.

`SecurityStamp` prepara o terreno para invalidar cookies/sessoes depois de troca de senha, reset de credenciais ou acao administrativa.

#### UserCredential

Representa uma credencial local ou outro mecanismo autenticavel.

Campos para password:

- `Type = password`
- `PasswordHash`
- `CreatedAt`
- `UpdatedAt`
- `ExpiresAt`
- `IsEnabled`

Pode ficar dentro de `UserAccount` no primeiro passo, sem criar store separado. O ganho e conceitual: senha deixa de ser propriedade direta do perfil.

#### ExternalIdentity

Prepara login externo:

- `Provider`
- `ProviderSubjectId`
- `DisplayName`
- `Claims`
- `LinkedAt`

Nao precisa estar totalmente implementado no primeiro passo, mas o modelo deve prever.

#### UserSession

Substitui `IdentitySession` como sessao persistivel.

Campos sugeridos:

- `SessionId`: valor do `sid`
- `SubjectId`
- `RealmId`
- `IdentityProvider`
- `AuthenticationMethods`: lista ou conjunto de `amr`
- `AuthenticatedAt`
- `CreatedAt`
- `ExpiresAt`
- `LastSeenAt`
- `IsActive`
- `EndedAt`
- `Clients`: conjunto de `UserSessionClient`
- `SecurityStamp`

`UserSessionClient`:

- `ClientId`
- `FirstSeenAt`
- `LastSeenAt`

Mudancas importantes:

- Nao guardar `IdentityUser` dentro da sessao.
- Usar conjunto/deduplicacao de clients.
- Store nao depende de `HttpContext`.

#### AuthenticatedSubject

Modelo de aplicacao para representar o resultado de autenticacao antes de virar cookie/token.

Campos:

- `SubjectId`
- `DisplayName`
- `SessionId`
- `IdentityProvider`
- `AuthenticationMethods`
- `AuthenticatedAt`
- `Claims`

Ele evita passar `IdentityUser` e `IdentitySession` pela UI. Tambem deixa claro que o cookie principal e snapshot, nao a entidade.

### Contratos novos ou revisados

#### IUserAccountStore

Unifica `IUserStore` e `IUserDetailsStore`.

Metodos sugeridos:

```csharp
Task<UserAccount?> FindBySubjectIdAsync(string subjectId, CancellationToken ct = default);
Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken ct = default);
Task<UserAccount?> FindByLoginAsync(string login, CancellationToken ct = default);
Task SaveAsync(UserAccount user, CancellationToken ct = default);
```

`FindByLoginAsync` pode aplicar `EmailAsUsername`, `LoginWithEmail` e normalizacao de login.

#### IUserCredentialService

Responsavel por senha e lockout.

Metodos sugeridos:

```csharp
Task<PasswordVerificationResult> VerifyPasswordAsync(
    Realm realm,
    UserAccount user,
    string password,
    CancellationToken ct = default);
```

Ele deve:

- Rejeitar quando credencial local esta ausente/desabilitada.
- Verificar hash.
- Incrementar falhas.
- Resetar falhas no sucesso.
- Calcular lockout conforme `AccountOptions.PasswordOptions`.
- Preservar mensagem generica para UI.

#### IUserSessionStore

Revisar para ser storage puro:

```csharp
Task<UserSession> CreateAsync(UserSession session, CancellationToken ct = default);
Task<UserSession?> FindByIdAsync(string sessionId, CancellationToken ct = default);
Task RecordClientAsync(string sessionId, string clientId, CancellationToken ct = default);
Task EndAsync(string sessionId, CancellationToken ct = default);
```

Remover `GetCurrentSessionAsync` do store. O "current" vem de `ClaimsPrincipal`/`HttpContext` em outro servico.

#### IUserSessionService

Servico de dominio/aplicacao para sessoes:

```csharp
Task<UserSession?> GetCurrentAsync(Realm realm, ClaimsPrincipal principal, CancellationToken ct);
Task<bool> IsCurrentSessionActiveAsync(Realm realm, ClaimsPrincipal principal, CancellationToken ct);
Task<UserSession> StartAsync(Realm realm, UserAccount user, AuthenticationMethod method, CancellationToken ct);
Task RecordClientAsync(Realm realm, string sessionId, string clientId, CancellationToken ct);
Task<EndedSession?> EndAsync(Realm realm, string sessionId, CancellationToken ct);
```

Esse servico substitui a dependencia de HTTP no store e concentra regras de sessao.

#### ISubjectPrincipalFactory

Responsavel por montar `ClaimsPrincipal` do cookie:

```csharp
Task<ClaimsPrincipal> CreateAsync(
    Realm realm,
    UserAccount user,
    UserSession session,
    CancellationToken ct = default);
```

Claims obrigatorias:

- `sub = UserAccount.SubjectId`
- `name = DisplayName`
- `auth_time = session.AuthenticatedAt`
- `sid = session.SessionId`
- `idp = session.IdentityProvider`
- `amr = session.AuthenticationMethods`

Esse factory substitui `IdentityUser.CreatePrincipalAsync`.

#### IUserAuthenticationService

Caso de uso de autenticacao local:

```csharp
Task<UserAuthenticationResult> AuthenticateLocalAsync(
    Realm realm,
    string login,
    string password,
    bool rememberLogin,
    CancellationToken ct = default);
```

Resultado deve esconder detalhes internos da UI:

- `Success`
- `Reason` interno
- `ErrorMessage`
- `Subject` ou `ClaimsPrincipal`
- `SessionId`

Opcionalmente, separar autenticacao e cookie sign-in:

- `AuthenticateLocalAsync`: valida e cria sessao.
- `SignInAsync`: grava cookie.

Mas para simplificar a UI, um caso de uso de pagina pode combinar os dois.

#### IAccountInteractionService

Fachada para telas de login/consent/logout.

Pode absorver parte do que hoje esta espalhado entre `ISessionContextService`, `ISignInManager` e `LoginPageService`.

Metodos possiveis:

```csharp
Task<LoginInteraction> BuildLoginAsync(string? returnUrl, CancellationToken ct);
Task<LoginFlowResult> LoginLocalAsync(LoginCommand command, CancellationToken ct);
Task<ConsentInteraction?> BuildConsentAsync(string? returnUrl, CancellationToken ct);
Task<ConsentFlowResult> SubmitConsentAsync(ConsentCommand command, CancellationToken ct);
Task<LogoutFlowResult> BeginLogoutAsync(string? logoutId, CancellationToken ct);
Task<LogoutFlowResult> ConfirmLogoutAsync(string logoutId, CancellationToken ct);
```

Os servicos Razor poderiam virar adaptadores finos ou ate desaparecer em favor dessa fachada.

## Como ficaria o fluxo simplificado

### Login local

1. UI chama `IAccountInteractionService.BuildLoginAsync(returnUrl)`.
2. Servico resolve realm e authorize context.
3. Servico calcula providers, local login permitido e login hint.
4. UI renderiza.
5. UI chama `LoginLocalAsync`.
6. Servico valida realm/client restrictions.
7. `IUserAuthenticationService` busca `UserAccount`, valida ativo/lockout/senha, cria `UserSession` e principal.
8. Servico grava cookie do realm.
9. Servico dispara evento de sucesso/falha.
10. Servico retorna resultado de fluxo: consent, signed-in page, callback, profile ou error.

A UI nao precisa conhecer `IdentityUser`, `UserSession`, `ClaimsPrincipal`, `IMessageStore`, `IConsentService` ou regras de redirect.

### Emissao de code

1. `DefaultCodeFactory` recebe `AuthorizeContext`.
2. Extrai `sid` do principal.
3. Cria authorization code.
4. Chama `IUserSessionService.RecordClientAsync(realm, sid, clientId)`.

Mesma regra, menos acoplamento com store.

### Validacao de cookie

1. Cookie middleware recebe principal.
2. Extrai `sid`.
3. Chama `IUserSessionService.IsCurrentSessionActiveAsync(realm, principal)`.
4. Rejeita principal se sessao inativa, inexistente, de outro realm ou com security stamp divergente.

Isso preserva o comportamento atual e permite endurecer a seguranca.

### Token/profile

1. `IProfileService` continua como contrato OIDC.
2. Internamente usa `IUserAccountStore` e `IUserSessionService`.
3. Profile claims saem de `UserAccount.Profile`, `Claims` e `Roles`, filtradas pelos scopes solicitados.
4. `IsActiveAsync` valida conta ativa e sessao ativa.

### Logout

1. `EndSessionHandler` cria `LogoutMessage` como hoje.
2. UI chama fachada de logout.
3. `IUserSessionService.EndAsync` encerra sessao e retorna snapshot com `SubjectId` e clients.
4. `SignOutManager` remove cookie, dispara evento e notifica clients.
5. Back-channel logout usa `session.SubjectId`, nao `session.User.UserName`.

## Regras que podem ser mudadas

### Separar SubjectId de Username

Recomendacao: mudar.

Justificativa:

- OIDC exige que `sub` seja identificador estavel para o issuer.
- O proprio `AccountOptions` ja tem opcoes que indicam username/email mutaveis.
- Permite trocar username/email sem quebrar tokens, consentimentos e relacoes existentes.

Implicacao:

- Consentimentos, tokens e sessoes devem usar `SubjectId`.
- Login pode buscar por username/email, mas nunca deve emitir `sub` com valor mutavel.
- Migracao inicial pode usar `SubjectId = Username` para compatibilidade.

### Sessao guardar SubjectId em vez de IdentityUser

Recomendacao: mudar.

Justificativa:

- Melhora persistencia.
- Evita grafo de objetos com dependencias.
- Reduz estado obsoleto.
- Simplifica logout e back-channel.

Implicacao:

- Onde hoje usa `session.User.UserName`, usar `session.SubjectId`.
- Onde precisar de display/profile, carregar `UserAccount` pelo store.

### Remover GetCurrentSessionAsync do store

Recomendacao: mudar.

Justificativa:

- Store nao deve depender de HTTP.
- Atual sessao e conceito de aplicacao, nao de persistencia.

Implicacao:

- `DefaultIdentityUser.CreatePrincipalAsync(session: null)` deve desaparecer.
- Codigo que precisa da sessao atual deve receber `ClaimsPrincipal` ou `sessionId`.

### Trocar IdentityUser por UserAccount/AuthenticatedSubject

Recomendacao: mudar em fases.

Justificativa:

- `IdentityUser` hoje e abstracao ampla demais.
- `UserAccount` representa dados.
- `AuthenticatedSubject` representa resultado de autenticacao.
- `ClaimsPrincipal` vira detalhe de borda HTTP/OIDC.

Implicacao:

- Pode manter adaptadores temporarios para nao quebrar tudo.
- `IProfileService` pode continuar existindo como contrato externo.

### Generalizar claims e roles

Recomendacao: mudar parcialmente.

Justificativa:

- Roles sao claims no token, mas no modelo administrativo vale manter `Roles` como colecao propria.
- Claims persistidas deveriam ser um modelo do RoyalIdentity, nao `System.Security.Claims.Claim` cru.

Implicacao:

- Criar `UserClaim` simples com `Type`, `Value`, `ValueType`.
- Converter para `Claim` apenas na borda.

## Funcionalidades que podem melhorar

### Melhorar lockout

Estado atual e funcional, mas basico.

Melhoria:

- Calcular e persistir `LockoutEndAt`.
- Expor `IsLockedOut` como resultado de policy, nao contador direto.
- Diferenciar lockout temporario de lockout administrativo.

Justificativa:

- Simplifica auditoria.
- Evita recalcular lockout toda vez a partir de `LastPasswordError`.

### Security stamp

Adicionar `SecurityStamp` em `UserSecurityState` e `UserSession`.

Uso:

- Quando senha muda, usuario e desativado, MFA muda ou credenciais externas mudam, atualizar stamp.
- Cookie/session validation rejeita sessoes com stamp antigo.

Justificativa:

- E uma protecao comum em sistemas de identidade.
- Ajuda a invalidar sessoes depois de eventos sensiveis.

### Sessoes por dispositivo

Modelo novo pode preparar:

- `UserAgent`
- `IpAddress`
- `DeviceName`
- `LastSeenAt`

Nao precisa implementar UI agora, mas ajuda administracao futura: "encerrar outras sessoes".

### Login externo

Adicionar `ExternalIdentity` ao modelo, mesmo que fluxo venha depois.

Justificativa:

- A UI ja lista providers externos.
- Clients ja tem `IdentityProviderRestrictions`.
- `idp` e `amr` ja entram nos tokens/cookie.

### Registro e recuperacao de senha

`AccountOptions` ja possui opcoes como `AllowRegistration`, `AllowForgotPassword`, `AllowChangePassword`, `VerifyEmail`, etc.

A nova modelagem deve separar:

- Account/profile.
- Credentials.
- Verification tokens.
- Password history.

Isso evita ampliar `UserDetails` ate virar uma classe gigante.

## Proposta de fases

### Fase 1 - Documento e testes de caracterizacao

Criar testes antes de alterar comportamento:

- Login valido cria sessao ativa realm-scoped.
- Login invalido incrementa falha e nao cria sessao.
- Login valido reseta falhas.
- Usuario inativo nao autentica.
- Usuario bloqueado nao autentica.
- Cookie com sessao encerrada e rejeitado.
- Code emitido registra client na sessao.
- Logout encerra sessao e notifica clients registrados.
- Sessao de realm A nao autentica realm B.
- `sub` atual continua igual ao username durante compatibilidade.

### Fase 2 - Introduzir novos modelos lado a lado

Adicionar:

- `UserAccount`
- `UserSecurityState`
- `UserCredential`
- `ExternalIdentity`
- `UserSession` novo ou `IdentitySession` revisado com compatibilidade
- `AuthenticatedSubject`
- `UserClaim`

Manter adaptadores para os contratos atuais.

### Fase 3 - Unificar store de usuario

Adicionar `IUserAccountStore` e implementar no in-memory.

Manter `IUserStore` e `IUserDetailsStore` como adaptadores temporarios, marcados para remocao.

### Fase 4 - Extrair servicos de autenticacao e principal

Adicionar:

- `IUserCredentialService`
- `IUserAuthenticationService`
- `ISubjectPrincipalFactory`

Mover de `DefaultIdentityUser` para esses servicos:

- Verificacao de senha.
- Lockout.
- Criacao de sessao.
- Criacao de principal.

### Fase 5 - Revisar sessao

Remover dependencia de `HttpContext` do store.

Adicionar `IUserSessionService` para:

- Current session.
- Validacao de sessao ativa.
- Registro de client.
- Encerramento de sessao.

Atualizar:

- `ConfigureRealmCookieAuthenticationOptions`
- `HttpContextExtensions.ValidateUserSessionAsync`
- `DefaultCodeFactory`
- `DefaultProfileService`
- `DefaultSignOutManager`

### Fase 6 - Simplificar UI services

Criar ou consolidar `IAccountInteractionService`.

Reduzir `LoginPageService`, `ConsentPageService` e `EndSessionPageService` para adaptadores finos de view model/resultado, ou mover totalmente a orquestracao para a fachada.

Meta: servico de tela nao deve conhecer `IdentityUser`, `IdentitySession`, stores de usuario/sessao ou detalhes de cookie.

### Fase 7 - Remover tipos antigos

Depois de adaptar todos os consumidores:

- Remover ou marcar obsoleto `IdentityUser`.
- Remover `UserDetails`.
- Remover `IUserStore` e `IUserDetailsStore`.
- Remover `ValidateCredentialsResult`.
- Consolidar `CredentialsValidationResult` em resultado novo de autenticacao.

## Testes recomendados

### Unidade/dominio

- Password ausente rejeita login local.
- Senha invalida incrementa falhas.
- Senha valida reseta falhas.
- Lockout temporario expira.
- Lockout indefinido permanece.
- Usuario inativo sempre falha.
- `SubjectId` permanece estavel quando username muda.
- `SubjectPrincipalFactory` emite claims obrigatorias.

### Integracao

- Login local completo com cookie por realm.
- Login com `RememberLogin` respeita `AllowRememberLogin`.
- Login em client com `EnableLocalLogin=false` nao permite local login no fluxo authorize.
- `prompt=login` força nova interacao.
- `max_age` vencido força nova interacao.
- `UserSsoLifetime` vencido força nova interacao.
- Sessao encerrada invalida cookie.
- Code registra client na sessao.
- Logout sem confirmacao funciona quando apenas um client esta na sessao e o client permite.
- Back-channel logout usa `SubjectId`.
- UserInfo nao retorna claims para usuario inativo.
- Token endpoint falha quando sessao esta inativa.
- Realm isolation para usuarios/sessoes.

### UI flow

- Login invalido renderiza erro generico.
- Login valido sem authorize vai para profile.
- Login valido com authorize volta para callback quando consentimento nao e exigido.
- Login valido com consentimento vai para tela de consent.
- Consent denial volta `access_denied`.
- Logout com logoutId invalido mostra erro.

## Criterios de aceite para a refatoracao

1. Existe uma entidade persistida unica para usuario (`UserAccount` ou nome equivalente).
2. `sub` e representado como `SubjectId` estavel e separado de username/email.
3. Existe um modelo de sessao persistivel que nao referencia objeto de usuario com comportamento.
4. Stores de usuario/sessao nao dependem de `HttpContext`.
5. Login local continua preservando mensagens genericas por default.
6. Lockout atual continua funcionando.
7. Cookie continua contendo `sub`, `sid`, `auth_time`, `amr`, `idp` e `name`.
8. Validacao de cookie continua rejeitando sessoes inativas.
9. Profile/token continuam validando usuario e sessao ativos.
10. Authorization code continua registrando client na sessao.
11. Logout continua encerrando sessao e notificando clients.
12. Servicos Razor nao conhecem stores de usuario/sessao nem modelos internos de sessao.
13. Testes de login, consentimento, endsession, userinfo e realm isolation continuam passando.
14. Novos testes cobrem lockout, sessao e separacao `SubjectId`/username.

## Nomeacao recomendada

Nomes preferidos:

- `UserAccount` para a entidade principal persistida.
- `UserSession` para a sessao persistida.
- `AuthenticatedSubject` para o resultado autenticado usado na aplicacao.
- `ISubjectPrincipalFactory` para criar `ClaimsPrincipal`.
- `IUserAccountStore` para persistencia de usuarios.
- `IUserSessionService` para regras de sessao.
- `IUserAuthenticationService` para login local/externo.
- `IAccountInteractionService` para fluxo das telas.

Evitar:

- `IdentityUser` como nome principal, porque conflita mentalmente com ASP.NET Identity e hoje carrega comportamento demais.
- `UserDetails`, porque parece DTO secundario, nao entidade de dominio.
- `GetCurrentSessionAsync` em store, porque "current" depende de contexto HTTP.

## Conclusao

A regra de negocio atual esta aproveitavel: realm isolation, lockout, sessao por `sid`, cookie por realm, consentimento, active user validation, registro de clients na sessao e logout SSO fazem sentido e devem ser preservados.

O problema nao e a regra, e a distribuicao das responsabilidades. A melhor direcao e transformar usuario e sessao em modelos persistiveis simples, mover comportamento para servicos de dominio/aplicacao, e expor para a UI uma fachada de interacao que devolve view models/resultados sem vazar `IdentityUser`, `IdentitySession`, stores ou detalhes de cookie.

A mudanca de maior impacto conceitual recomendada e separar `SubjectId` de `Username`. Ela e justificavel por OIDC e pelas proprias opcoes de conta ja existentes. Com migracao inicial `SubjectId = Username`, a compatibilidade pode ser preservada enquanto o modelo fica preparado para username/email mutaveis e administracao real de usuarios.

## Validacao desta analise

Validacao feita por leitura estatica do codigo e dos testes existentes. Nenhum build ou teste foi executado, porque esta entrega adiciona apenas documentacao de analise.
