
# Redesign to do list

## Realm (CONCLUÍDO)

Adicionar a funcionalidade de Realm, onde cada realm seja semelhante a um tenant.

Deverá haver configurações por realm. O ServerOptions deverá ser substituído por RealmOptions.

O RealmOptions terá um ServerOptions, com os atributos de configuração do servidor.

As configurações maleáveis serão feitas para cada realm.

Para identificar o realm, as rotas terão o nome do realm no início.

Exemplo:
- /{realm}/connect/authorize
- /{realm}/connect/token
- /{realm}/.well-known/openid-configuration

No mapeamento da rota, o realm será identificado, e a pipeline receberá o RealmOptions correspondente.

## Resources (CONCLUÍDO)

O componente resources, o qual trata scopes, tem três tipos de recursos ou scopes:
- IdentityResources
- ApiScopes
- ApiResources

Para evitar confusões do que se tratam isso, será refatorado da seguinte forma:
- IdentityResources -> IdentityScopes
- ApiScopes -> Scope
- ApiResources -> ResourceServer

O próprio objeto Resources precisa ser refatorado para RequestedResources.

Ele será construído através dos scopes requisitados pelo cliente.

A restrutura mudará também.

Haverá o **IdentityScope**, o qual terá os claims que devem ser enviados ao cliente.

Então, para controlar o acesso a recursos, será feito da seguinte forma:

- **ResourceServer**: um serviço que disponibiliza recursos para os clientes consumirem, como um WebApi. 
O servidor de recursos tem o mesmo papel definido o OAuth2, que é fornecer de recursos protegidos aos clientes autorizados.
- **Scope**: um recurso é uma funcionalidade disponibilizada pelo ResourceServer.
Pode ser uma página web, um documento ou arquivo, um grupo de endpoints de uma API, etc. Pode ser operações que podem ser executadas sobre um recurso, como leitura, escrita, atualização, exclusão, etc.

Cada um dos tipos de recursos, IdentityScope, ResourceServer, Scope, terá um nome como recurso.
O *scope* solicitado pelo cliente, será o nome do recurso.

Quando selecionado o *scope*, o cliente terá acesso apenas a operação do *scope*.
Quando selecionado o *ResourceServer*, o cliente terá acesso a todos os recursos do *ResourceServer*. (Isso é questionável, depende mais do como funciona a implementação do Resource Server do que pode ser decidido aqui).
O que melhor pode ser feito é ao adicionar um *ResourceServer* como permitido para o client, todos os *scope* daquele *ResourceServer* podem ser usados nas requisições.

Na hora de adicionar os scopes ao token, apenas os *scopes* requeridos serão adicionados

Para exibir o consentimento, todos os *scopes* do recurso serão exibidos.

## Users ✓ DONE (CONCLUÍDO)

Unificar a lógica de usuários.
Existe IdentityUser, UserDetails, IUserStore e IUserDetailsStore.
Há IdentitySession e IUserSessionStore.
Há lógica confusa entre usuários e sessões.
Precisa unificar o usuário e revisar a sessão e o login.

**Concluído** pelo plano `.ai/plans/plan-users-edge-session.md` (ADR-013/014). A borda virou facades
(`ISubjectStore`/`ILocalUserAuthenticator`/`IUserPropertyProvider` via `IUserDirectory`) + modelo enxuto
(`Subject`, `UserSession` serializável); `sub` = `SubjectId` ≠ username; sessão criada no sign-in pelo
`LoginFlowService`; "ativo" unificado; principal mínimo. Removidos `IdentityUser`/`DefaultIdentityUser`,
`UserDetails`, `IUserStore`/`IUserDetailsStore`, `IdentitySession`, `(Validate|Credentials)…Result` e o
`IdentityRevalidatingAuthenticationStateProvider` quebrado. In-memory = fake (`MemoryUserAccount`).
**Trabalho futuro** (módulo `UserAccounts` — ver [ADR-015](adrs/ADR-015.md) + `plan-users-accounts-module-v2`; persistência EFCore; KMS) está em `.ai/backlogs/backlog-001.md`.

## UI Services ✓ DONE (CONCLUÍDO)

`ILoginPageService`, `IConsentPageService`, `IEndSessionPageService`, `ISessionContextService` criados em `RoyalIdentity.Razor/Services/`.
ViewModels movidos para `RoyalIdentity.Razor/ViewModels/`.
Componentes Razor agora apenas exibem dados.

## Localization

Adicionar a funcionalidade de localização.

Todos os textos e labels estão fixos no código em inglês.

Deverá ser possível adicionar arquivos de localização para cada idioma.

## RFC 9700

Avaliar se a implementação atual está de acordo com **rfc9700**.