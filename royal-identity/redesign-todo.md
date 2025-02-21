
# Redesign to do list

## Realm

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

## Resources

O componente resources, o qual trata scopes, tem três tipos de recursos ou scopes:
- IdentityResources
- ApiScopes
- ApiResources

Para evitar confusões do que se tratam isso, será refatorado da seguinte forma:
- IdentityResources -> IdentityScopes
- ApiScopes -> Scopes
- ApiResources -> Resources

O próprio objeto Resources precisa ser refatorado para RequestedResources.

Ele será construído através dos scopes requisitados pelo cliente.

A restrutura mudará também.

Haverá o **IdentityScope**, o qual terá os claims que devem ser enviados ao cliente.

Então, para controlar o acesso a recursos, será feito da seguinte forma:

- **ResourceServer**: um serviço que disponibiliza recursos para os clientes consumirem, como um WebApi. 
O servidor de recursos tem o mesmo papel definido o OAuth2, que é fornecer de recursos protegidos aos clientes autorizados.
- **Resource**: um recurso é uma funcionalidade disponibilizada pelo ResourceServer.
Pode ser uma página web, um documento ou arquivo, um grupo de endpoints de uma API, etc.
- **Scope**: é uma operação que pode ser executada sobre um recurso.
Pode estar relacionado ao verbo HTTP, como GET, POST, PUT, DELETE, etc.
Pode ser operações que podem ser executadas sobre um recurso, como leitura, escrita, atualização, exclusão, etc.

Cada um dos tipos de recursos, IdentityScope, ResourceServer, Resource, Scope, terá um nome como recurso.
O *scope* solicitado pelo cliente, será o nome do recurso.

Quando selecionado o recurso, o cliente terá acesso a todos os *scopes* do recurso.
Quando selecionado o *scope*, o cliente terá acesso apenas a operação do *scope*.
Quando selecionado o *ResourceServer*, o cliente terá acesso a todos os recursos do *ResourceServer*.

Na hora de adicionar os scopes ao token, apenas os *scopes* do recurso serão adicionados.

Para exibir o consentimento, todos os *scopes* do recurso serão exibidos.

## UI Services

Existe muita lógica dentro dos componentes Razor.

É melhor criar serviços de UI, como por exemplo: UILoginService, UIConsentService, etc.

Esses serviços terão a lógica de UI, e os componentes Razor apenas exibirão os dados.

## Localization

Adicionar a funcionalidade de localização.

Todos os textos e labels estão fixos no código em inglês.

Deverá ser possível adicionar arquivos de localização para cada idioma.