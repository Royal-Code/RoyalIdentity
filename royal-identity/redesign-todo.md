
# Redesign to do list

## Realm

Adicionar a funcionalidade de Realm, onde cada realm seja semelhante a um tenant.

Dever� haver configura��es por realm. O ServerOptions dever� ser substitu�do por RealmOptions.

O RealmOptions ter� um ServerOptions, com os atributos de configura��o do servidor.

As configura��es male�veis ser�o feitas para cada realm.

Para identificar o realm, as rotas ter�o o nome do realm no in�cio.

Exemplo:
- /{realm}/connect/authorize
- /{realm}/connect/token
- /{realm}/.well-known/openid-configuration

No mapeamento da rota, o realm ser� identificado, e a pipeline receber� o RealmOptions correspondente.

## Resources

O componente resources, o qual trata scopes, tem tr�s tipos de recursos ou scopes:
- IdentityResources
- ApiScopes
- ApiResources

Para evitar confus�es do que se tratam isso, ser� refatorado da seguinte forma:
- IdentityResources -> IdentityScopes
- ApiScopes -> Scopes
- ApiResources -> Resources

O pr�prio objeto Resources precisa ser refatorado para RequestedResources.

Ele ser� constru�do atrav�s dos scopes requisitados pelo cliente.

A restrutura mudar� tamb�m.

Haver� o **IdentityScope**, o qual ter� os claims que devem ser enviados ao cliente.

Ent�o, para controlar o acesso a recursos, ser� feito da seguinte forma:

- **ResourceServer**: um servi�o que disponibiliza recursos para os clientes consumirem, como um WebApi. 
O servidor de recursos tem o mesmo papel definido o OAuth2, que � fornecer de recursos protegidos aos clientes autorizados.
- **Resource**: um recurso � uma funcionalidade disponibilizada pelo ResourceServer.
Pode ser uma p�gina web, um documento ou arquivo, um grupo de endpoints de uma API, etc.
- **Scope**: � uma opera��o que pode ser executada sobre um recurso.
Pode estar relacionado ao verbo HTTP, como GET, POST, PUT, DELETE, etc.
Pode ser opera��es que podem ser executadas sobre um recurso, como leitura, escrita, atualiza��o, exclus�o, etc.

Cada um dos tipos de recursos, IdentityScope, ResourceServer, Resource, Scope, ter� um nome como recurso.
O *scope* solicitado pelo cliente, ser� o nome do recurso.

Quando selecionado o recurso, o cliente ter� acesso a todos os *scopes* do recurso.
Quando selecionado o *scope*, o cliente ter� acesso apenas a opera��o do *scope*.
Quando selecionado o *ResourceServer*, o cliente ter� acesso a todos os recursos do *ResourceServer*.

Na hora de adicionar os scopes ao token, apenas os *scopes* do recurso ser�o adicionados.

Para exibir o consentimento, todos os *scopes* do recurso ser�o exibidos.

## UI Services

Existe muita l�gica dentro dos componentes Razor.

� melhor criar servi�os de UI, como por exemplo: UILoginService, UIConsentService, etc.

Esses servi�os ter�o a l�gica de UI, e os componentes Razor apenas exibir�o os dados.

## Localization

Adicionar a funcionalidade de localiza��o.

Todos os textos e labels est�o fixos no c�digo em ingl�s.

Dever� ser poss�vel adicionar arquivos de localiza��o para cada idioma.