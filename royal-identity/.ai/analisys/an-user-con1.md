# Consenso - Redesign Users M1

## Escopo

Este documento consolida um consenso a partir de:

- `an-users-m1.md`
- `an-users-m2.md`
- `an-user-m1-av1.md`
- `an-user-m1-av2.md`

Nao e ainda um plano de implementacao. E uma decisao de direcao para orientar a ADR e o plano posterior.

## Veredito

O consenso e seguir uma abordagem conservadora no escopo, mas correta no modelo.

A espinha dorsal deve ser a linha mais disciplinada da M2/AV2:

- fazer ADR antes de implementar;
- remover heranca rica de usuario;
- separar dado de comportamento;
- manter storage puro;
- corrigir `SubjectId`;
- unificar semantica de conta ativa e sessao valida;
- reduzir regra nos servicos de tela sem criar uma fachada gigante.

Da M1/AV1 devem ser incorporados os ativos duraveis:

- `UserClaim` persistivel;
- `SecurityStamp` reservado no modelo;
- separacao conceitual de credencial/safety state;
- criterios de aceite;
- testes de caracterizacao;
- cuidado com funcionalidades futuras como login externo, MFA, passwordless, sessoes por dispositivo e admin.

O objetivo nao e construir todo o sistema de usuario futuro agora. O objetivo e redesenhar o nucleo para que o futuro caiba sem nova refatoracao grande.

## Decisoes consensuais

### D1 - Criar nova ADR antes da implementacao

Decisao: criar uma nova ADR, provavelmente ADR-013, antes do plano/implementacao.

Ela deve superseder ou refinar ADR-005.

Conteudo minimo da ADR:

- RoyalIdentity continua com gerenciamento proprio de usuarios.
- Customizacao por realm continua sendo requisito.
- O mecanismo de customizacao deixa de ser entidade rica/heranca (`IdentityUser`) e passa a ser composicao por servicos substituiveis, stores e policies.
- `SubjectId` e identificador estavel OIDC e nao deve depender de `Username`.
- Usuario e sessao devem ser persistiveis e realm-scoped.

Justificativa: remover ou esvaziar `IdentityUser` contraria a forma atual implicada pela ADR-005. Implementar sem registrar essa virada deixaria o codigo contra uma decisao arquitetural vigente.

### D2 - O modelo persistido principal sera uma entidade concreta unica

Decisao: substituir o par `IdentityUser`/`UserDetails` por uma entidade persistida concreta e unica.

Nome recomendado: `UserAccount`.

Alternativa aceitavel: `User`, se o projeto decidir privilegiar nome curto.

Recomendacao final: usar `UserAccount`.

Justificativa:

- `User` e simples, mas ambiguo em um authorization server onde `HttpContext.User`, `ClaimsPrincipal`, subject e session aparecem o tempo todo.
- `UserAccount` deixa claro que o tipo e a conta persistida, nao o principal autenticado da request.
- Evita reforcar a confusao com ASP.NET Identity ou `IdentityUser`.

Campos nucleares:

- `SubjectId`
- `Username`
- `DisplayName`
- `IsActive`
- `Claims`
- `Roles`
- `SecurityStamp`
- `PasswordCredential`
- `SecurityState`

Campos/funcoes que devem ser costurados, mas nao necessariamente implementados agora:

- identidades externas;
- metadados de perfil mais ricos;
- phone/email verification;
- device/session metadata;
- password history;
- MFA/passwordless.

### D3 - `SubjectId` sera separado de `Username`

Decisao: introduzir `SubjectId` imutavel e usar esse valor como `sub`.

Recomendacao: fazer agora com IDs deterministas no seed/demo, nao `SubjectId = Username`.

Justificativa:

- OIDC espera `sub` estavel por issuer.
- `Username` e potencialmente mutavel, conforme as proprias opcoes de conta (`AllowChangeUsername`, `EmailAsUsername`, `LoginWithEmail`).
- Ainda nao ha backend persistente real; este e o momento barato para corrigir.
- Usar `SubjectId = Username` agora preservaria o acoplamento e exigiria outra migracao depois.

Impacto:

- Testes que assumem `sub = alice` devem ser atualizados.
- Consentimentos, tokens, refresh tokens, sessions e profile lookup devem trabalhar por `SubjectId`.
- Login continua buscando por username/login, mas emissao OIDC usa `SubjectId`.

### D4 - Extensibilidade por composicao, nao por heranca de usuario

Decisao: o modelo final nao deve manter `IdentityUser` como entidade rica.

Implementacao pode manter adapter temporario, mas o destino final e remover:

- `IdentityUser`
- `DefaultIdentityUser`
- `UserDetails`
- `IUserDetailsStore`
- `ValidateCredentialsResult`
- `CredentialsValidationResult` atual, substituido por resultado unico.

Justificativa:

- Stores devem devolver dados persistiveis, nao objetos com servicos.
- Entidade com `IUserSessionStore`, `IUserDetailsStore`, `IPasswordProtector`, options e clock dentro nao e adequada para SQL/Redis/cache.
- Composicao por servicos combina melhor com a arquitetura do projeto: contratos, defaults, pipeline e stores por realm.

### D5 - Store de usuario sera unico e orientado a dados

Decisao: criar `IUserAccountStore`.

Operacoes iniciais:

```csharp
Task<UserAccount?> FindBySubjectIdAsync(string subjectId, CancellationToken ct = default);
Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken ct = default);
Task<UserAccount?> FindByLoginAsync(string login, CancellationToken ct = default);
Task SaveAsync(UserAccount user, CancellationToken ct = default);
```

`FindByLoginAsync` deve ser o ponto para aplicar regras de login por username/email conforme `AccountOptions`.

Justificativa:

- Elimina a duplicidade `IUserStore` x `IUserDetailsStore`.
- Evita store-factory de objeto rico.
- Deixa profile, login, admin e token flow falando com a mesma fonte de verdade.

### D6 - Claims persistidas devem usar modelo proprio

Decisao: criar `UserClaim` persistivel, em vez de guardar `System.Security.Claims.Claim` como dado de dominio.

Modelo minimo:

```csharp
public class UserClaim
{
    public required string Type { get; init; }
    public required string Value { get; init; }
    public string? ValueType { get; init; }
}
```

Justificativa:

- Melhora persistencia futura.
- Controla igualdade e serializacao.
- Evita tipo de framework como contrato de storage.
- A conversao para `Claim` deve ocorrer na borda: principal factory, token claims service ou profile service.

### D7 - Senha e lockout nao devem ficar soltos no perfil

Decisao: usar submodelos dentro de `UserAccount`, sem criar store separado agora.

Modelo recomendado:

- `PasswordCredential`
- `UserSecurityState`

`PasswordCredential` deve conter, no minimo:

- `PasswordHash`
- `IsEnabled`
- `CreatedAt`
- `UpdatedAt`
- `ExpiresAt`, se necessario depois

`UserSecurityState` deve conter, no minimo:

- `FailedPasswordAttempts`
- `LastPasswordFailureAt`
- `LockoutEndAt`
- `MustChangePassword`
- `SecurityStamp`

Justificativa:

- Evita repetir o problema de `UserDetails`, onde perfil, senha e lockout ficam misturados.
- Nao cria complexidade de store extra.
- Deixa espaco para MFA, passwordless e password history depois.

### D8 - Autenticacao local deve ser servico, nao metodo do usuario

Decisao: extrair validacao de senha, lockout e resultado de autenticacao para servicos.

Servicos recomendados:

- `IUserAuthenticator`
- `LockoutPolicy`

`IUserAuthenticator` deve validar a credencial e devolver um resultado unico.

`LockoutPolicy` deve concentrar:

- checagem de bloqueio;
- incremento de falha;
- reset apos sucesso;
- calculo de `LockoutEndAt`;
- leitura de `AccountOptions.PasswordOptions`.

Regra importante: validar senha nao deve criar sessao como efeito colateral.

A sessao deve nascer apenas quando o fluxo de sign-in realmente concluir.

Justificativa:

- Corrige o split-brain atual do lockout.
- Abre caminho para 2FA/login externo.
- Evita sessoes orfas apos uma credencial primaria valida, mas fluxo incompleto.

### D9 - Criar factory unica para principal/claims de autenticacao

Decisao: criar factory para montar o `ClaimsPrincipal` de autenticacao.

Nome recomendado: `IUserClaimsFactory` ou `ISubjectPrincipalFactory`.

Recomendacao final: `ISubjectPrincipalFactory`, por ser mais explicito sobre o resultado (`ClaimsPrincipal`) e sobre o conceito OIDC (`subject`).

Claims obrigatorias no cookie/principal:

- `sub`
- `name`
- `auth_time`
- `sid`
- `idp`
- `amr`

Justificativa:

- Remove `IdentityUser.CreatePrincipalAsync`.
- Reduz divergencia entre principal de login, id token, access token e userinfo.
- Permite converter `UserClaim` para `Claim` em um ponto controlado.

### D10 - Sessao deve ser dado puro e persistivel

Decisao: substituir/revisar `IdentitySession` para nao guardar `IdentityUser`.

Modelo recomendado: `UserSession`.

Campos nucleares:

- `SessionId`
- `RealmId`
- `SubjectId`
- `IdentityProvider`
- `AuthenticationMethods`
- `AuthenticatedAt`
- `CreatedAt`
- `LastSeenAt`
- `IsActive`
- `EndedAt`
- `Clients`
- `SecurityStamp`

`Clients` deve ser deduplicado. Pode iniciar como `HashSet<string>` ou evoluir para `UserSessionClient`.

Recomendacao: comecar com estrutura que permita evoluir:

```csharp
public class UserSessionClient
{
    public required string ClientId { get; init; }
    public DateTimeOffset FirstSeenAt { get; init; }
    public DateTimeOffset LastSeenAt { get; set; }
}
```

Se isso for considerado demais para a primeira fase, aceitar `HashSet<string>` temporariamente, mas nao `IList<string>`.

Justificativa:

- Logout SSO depende de clients registrados na sessao.
- Persistencia real nao deve serializar objeto com comportamento.
- Dedup evita multiplas entradas do mesmo client.

### D11 - Store de sessao nao tera "current session"

Decisao: remover `GetCurrentSessionAsync` do store.

Store de sessao deve ser puro:

```csharp
Task<UserSession> CreateAsync(UserSession session, CancellationToken ct = default);
Task<UserSession?> FindByIdAsync(string sessionId, CancellationToken ct = default);
Task RecordClientAsync(string sessionId, string clientId, CancellationToken ct = default);
Task EndAsync(string sessionId, CancellationToken ct = default);
```

"Sessao atual" deve ficar em servico de aplicacao:

- recebe `Realm`;
- recebe `ClaimsPrincipal` ou `sessionId`;
- extrai `sid` quando necessario;
- chama o store.

Nome recomendado: `IUserSessionService`.

Justificativa:

- Storage nao deve depender de HTTP.
- Facilita teste e backend persistente.
- Centraliza validacao de sessao ativa.

### D12 - Separar "conta ativa" de "sessao valida"

Decisao: corrigir a semantica atual.

Devem existir duas perguntas:

- conta esta ativa?
- sessao e valida?

Conta ativa:

- verifica `UserAccount.IsActive`;
- nao depende de sessao.

Sessao valida:

- exige sessao existente;
- exige `IsActive = true`;
- exige realm correto;
- quando aplicavel, valida `SecurityStamp`;
- sessao ausente e invalida para fluxos autenticados com `sid`.

Justificativa:

- Hoje existem tres caminhos divergentes de "ativo".
- Sessao nula como ativa enfraquece logout e TTL/cache.
- Cookie validation, token issuance e refresh devem ter comportamento previsivel.

### D13 - RevalidatingAuthenticationStateProvider deve ser removido ou refeito

Decisao: o provider atual nao deve permanecer como esta.

Recomendacao: remover no redesign inicial, salvo se for confirmado que existe area interativa autenticada que depende dele.

Se precisar manter:

- deve capturar/receber realm corretamente;
- deve usar `IStorage` ou `IUserAccountStore` realm-scoped;
- deve usar `IUserSessionService`;
- nao deve resolver `IUserStore` direto por DI.

Justificativa:

- Esta desalinhado com realm isolation.
- Tenta resolver contrato que nao e registrado diretamente.
- Ignora sessao.
- E mais seguro remover codigo quebrado do que manter uma revalidacao falsa.

### D14 - UI deve perder conhecimento de usuario/sessao interna

Decisao: reduzir `LoginPageService`, `ConsentPageService` e `EndSessionPageService` para adaptadores finos.

Consenso sobre forma:

- nao criar uma fachada unica gigante que misture tudo;
- nao deixar a maquina de estados no `LoginPageService`;
- mover fluxo para managers/fachadas coesas por caso de uso.

Recomendacao:

- `ISignInManager` ou novo servico de login deve devolver resultado com enum de desfecho e URL.
- `ISignOutManager` pode continuar como fachada de logout, mas usando `UserSession`.
- `IConsentService`/servico de consentimento continua separado.

Evitar inicialmente:

- `IAccountInteractionService` unico para login + consent + logout, pois pode virar god-service.

Manter a ideia da M1 como criterio, nao necessariamente como nome: a UI nao deve conhecer `IdentityUser`, `UserSessionStore`, detalhes de cookie, mensagem store ou regras de redirecionamento profundo.

## Modelo consensual recomendado

### Entidades

```text
UserAccount
  SubjectId
  Username
  DisplayName
  IsActive
  Claims: ICollection<UserClaim>
  Roles: ISet<string>
  PasswordCredential
  SecurityState
  SecurityStamp

UserClaim
  Type
  Value
  ValueType

PasswordCredential
  PasswordHash
  IsEnabled
  CreatedAt
  UpdatedAt

UserSecurityState
  FailedPasswordAttempts
  LastPasswordFailureAt
  LockoutEndAt
  MustChangePassword
  SecurityStamp

UserSession
  SessionId
  RealmId
  SubjectId
  IdentityProvider
  AuthenticationMethods
  AuthenticatedAt
  CreatedAt
  LastSeenAt
  IsActive
  EndedAt
  Clients
  SecurityStamp
```

### Servicos

```text
IUserAccountStore
IUserAuthenticator
LockoutPolicy
ISubjectPrincipalFactory
IUserSessionStore
IUserSessionService
ISignInManager revisado ou LoginFlowService
ISignOutManager revisado
IProfileService revisado usando UserAccount + UserSession
```

### Tipos a remover ou adaptar temporariamente

```text
IdentityUser
DefaultIdentityUser
UserDetails
IUserDetailsStore
ValidateCredentialsResult
CredentialsValidationResult
IdentitySession atual
IUserStore atual, se permanecer devolvendo IdentityUser
```

## Funcionalidades futuras: costurar, nao implementar

Nao implementar agora:

- login externo completo;
- MFA;
- passwordless;
- password history;
- sessoes por dispositivo/admin UI;
- recuperacao de senha;
- registro;
- verificacao de email/phone.

Mas o modelo deve deixar costuras para:

- multiplos autenticadores;
- credenciais alem de senha;
- identidades externas vinculadas;
- security stamp;
- claims persistiveis;
- sessao com metadados.

## Sequencia consensual

### Passo 0 - ADR

Criar ADR-013 antes do plano.

Decisoes da ADR:

- composicao em vez de heranca rica;
- `SubjectId` imutavel;
- usuario/sessao como dados persistiveis;
- stores puros;
- extensibilidade por realm via servicos.

### Passo 1 - Testes de caracterizacao

Criar testes antes de mudar comportamento:

- login valido cria cookie e sessao ativa;
- login invalido incrementa falhas e nao cria sessao;
- login valido reseta falhas;
- usuario inativo nao autentica;
- usuario bloqueado nao autentica;
- sessao encerrada invalida cookie;
- code emitido registra client na sessao;
- logout encerra sessao e preserva front/back-channel logout;
- realm isolation de usuario/sessao;
- `SubjectId` permanece estavel quando `Username` muda.

### Passo 2 - Novo modelo de usuario

Introduzir `UserAccount`, `UserClaim`, `PasswordCredential`, `UserSecurityState`.

Seeds devem usar `SubjectId` deterministico e diferente de username.

### Passo 3 - Store unico de usuario

Criar `IUserAccountStore`.

Adaptar in-memory.

Manter adapters temporarios se necessario, mas com destino de remocao.

### Passo 4 - Autenticacao e principal

Extrair:

- `IUserAuthenticator`
- `LockoutPolicy`
- `ISubjectPrincipalFactory`

Sessao nao deve mais nascer na validacao de senha.

### Passo 5 - Sessao

Introduzir `UserSession`, `IUserSessionStore` puro e `IUserSessionService`.

Atualizar:

- cookie validation;
- `DefaultCodeFactory`;
- `DefaultProfileService`;
- `DefaultSignOutManager`;
- token/profile active checks.

### Passo 6 - UI services

Reduzir conhecimento dos servicos Razor.

Mover maquina de estados de login para manager/fachada coesa com resultado tipado:

- sucesso;
- erro;
- requer consentimento;
- signed-in page;
- redirect final;
- profile;
- error page.

### Passo 7 - Limpeza

Remover/adaptar:

- `IdentityUser`;
- `DefaultIdentityUser`;
- `UserDetails`;
- stores antigos;
- resultados duplicados;
- revalidating provider atual.

## Criterios de aceite consensuais

1. Existe uma unica entidade persistida principal de usuario.
2. `SubjectId` e separado de `Username` e usado como `sub`.
3. Username pode mudar sem mudar `sub`.
4. Stores de usuario e sessao nao dependem de `HttpContext`.
5. Store de usuario nao devolve objeto com servicos.
6. Sessao nao referencia usuario rico.
7. Sessao registra clients sem duplicidade.
8. Login invalido nao cria sessao.
9. Login valido cria sessao e cookie com claims OIDC obrigatorias.
10. Lockout fica concentrado em uma policy/servico.
11. Mensagens de login continuam genericas por default.
12. Cookie validation rejeita sessao ausente/inativa.
13. `ProfileService` diferencia conta ativa de sessao valida.
14. Authorization code continua registrando client na sessao.
15. Logout continua encerrando sessao e notificando clients.
16. UI services nao conhecem stores/modelos internos de usuario/sessao.
17. Revalidating provider atual nao permanece quebrado.
18. Testes de login, consentimento, token, userinfo, endsession e realm isolation continuam passando.

## Pontos que ainda podem ser ajustados no plano

### Nome final: `UserAccount` ou `User`

Consenso recomenda `UserAccount`, mas e aceitavel uma decisao final por `User` se o projeto preferir nomes curtos.

O importante e nao manter `IdentityUser` como destino final.

### `UserSessionClient` agora ou depois

Consenso prefere estrutura extensivel.

Se o plano precisar reduzir escopo, pode iniciar com `HashSet<string>` para client ids, desde que:

- dedupe exista;
- nao seja `IList<string>`;
- haja tarefa futura para metadados por client se necessario.

### Security stamp ativo agora ou depois

Consenso:

- campo entra agora;
- validacao pode entrar depois, quando houver troca de senha/admin ou quando a fase de sessao estiver pronta.

## Riscos

### R1 - Big bang

Risco: tentar trocar usuario, sessao, UI e tokens numa unica fase.

Mitigacao: adapters temporarios e testes de caracterizacao.

### R2 - Quebra de testes por `sub`

Risco: testes assumem `sub = username`.

Mitigacao: atualizar seeds com IDs deterministicos e ajustar asserts.

### R3 - Semantica de sessao mais estrita

Risco: fluxos com principals fabricados sem sessao podem falhar.

Mitigacao: criar helpers de teste que criem `UserSession` real ou separar testes de conta ativa.

### R4 - UI flow virar god service

Risco: centralizar tudo em uma fachada unica.

Mitigacao: manter fachadas coesas por caso de uso e resultados tipados.

### R5 - ADR esquecida

Risco: implementar contra ADR-005.

Mitigacao: ADR-013 antes do plano.

## Conclusao

O consenso e: corrigir o nucleo agora, sem construir o produto inteiro de usuarios antes da hora.

A rota mais segura e:

1. ADR primeiro.
2. `SubjectId` real agora.
3. `UserAccount` persistivel e unico.
4. Stores puros.
5. Auth/lockout/principal em servicos.
6. Sessao persistivel sem usuario rico.
7. UI fina por resultados de fluxo.
8. Testes de caracterizacao antes de mexer.

Assim o redesign resolve a complexidade atual e deixa caminho aberto para features futuras sem carregar complexidade especulativa no primeiro corte.

## Validacao

Este consenso foi produzido por leitura estatica das analises e avaliacoes citadas. Nenhum build ou teste foi executado, pois a entrega e documental.
