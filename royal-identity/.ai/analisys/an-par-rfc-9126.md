# Análise — Pushed Authorization Requests (PAR / RFC 9126)

> **Status:** análise exploratória; não é ADR, plano de implementação nem decisão arquitetural.
>
> **Objetivo:** registrar os requisitos de PAR levantados durante a elaboração do
> [baseline de storage](../plans/plan-data-storage-baseline.md), avaliar o encaixe com os contratos atuais e preservar
> as alternativas para uma decisão futura.
>
> **Escopo desta análise:** RFC 9126, `IMessageStore`, `ProtectedDataMessageStore`, `IAuthorizeParametersStore` e a
> hipótese de um futuro `IAuthorizationRequestStore`/`IPushedAuthorizationRequestStore`.
>
> **Fora do escopo:** escolher a interface final, alterar contratos, implementar endpoint, persistência, migrations,
> discovery ou testes.

---

## 1. Resumo

PAR não é apenas uma nova forma de armazenar uma mensagem. É uma extensão de protocolo que introduz um endpoint
direto, autenticação do client, validação antecipada da authorization request, um `request_uri` curto e opaco,
expiração, vínculo ao client e semântica de uso único.

O `IMessageStore` atual pode legitimamente ter duas implementações para os casos de mensagens transitórias já
existentes:

- `ProtectedDataMessageStore`: payload autocontido, protegido e transportado no próprio identificador;
- uma futura `PersistentDataMessageStore`: identificador opaco que referencia um payload mantido no servidor.

Isso não torna o contrato atual suficiente para PAR. `IMessageStore` não expressa realm, client, expiração nem consumo
atômico. Fazer PAR depender apenas de `ReadAsync` seguido de `DeleteAsync` deixaria a regra de uso único vulnerável a
corridas.

Duas formas continuam viáveis para uma evolução futura:

1. criar um `IPushedAuthorizationRequestStore` específico para PAR; ou
2. evoluir `IAuthorizeParametersStore` para um `IAuthorizationRequestStore`, com operações distintas para a
   continuação interna de login/consentimento e para o consumo de PAR.

Não se recomenda transformar `IMessageStore` em uma facade genérica para todo estado de autorização e logout. O
contrato perderia as semânticas de cada fluxo. Ainda assim, implementações diferentes podem compartilhar uma camada
física de persistência de dados transitórios.

---

## 2. Requisitos relevantes da RFC 9126

### 2.1 Recepção da pushed authorization request

O authorization server expõe um endpoint HTTPS que recebe `POST` em
`application/x-www-form-urlencoded`. O client envia os parâmetros aplicáveis ao authorization endpoint, exceto
`request_uri`, e autentica-se conforme as regras do token endpoint. O servidor autentica o client e valida a request
antes de aceitá-la.

Fonte: [RFC 9126, seções 2 e 2.1](https://www.rfc-editor.org/rfc/rfc9126.html#section-2.1).

### 2.2 Criação do handle

Em caso de sucesso, o endpoint responde com HTTP `201`, `request_uri` e `expires_in`. O `request_uri`:

- referencia os dados mantidos pelo authorization server;
- contém uma parte gerada com aleatoriedade criptograficamente forte e deve ser impraticável de adivinhar;
- é vinculado ao client que enviou a request;
- possui vida curta definida pelo servidor.

Fonte: [RFC 9126, seção 2.2](https://www.rfc-editor.org/rfc/rfc9126.html#section-2.2).

### 2.3 Uso no authorization endpoint

O client inicia a authorization request no user agent usando `client_id` e o `request_uri`. O client deve usar o
handle uma única vez. O servidor deve rejeitar handles expirados e deveria tratá-los como uso único, embora a RFC
permita tolerar uma repetição causada por reload do user agent. A request recuperada ainda precisa ser validada contra
a request e a política vigentes.

Fonte: [RFC 9126, seção 4](https://www.rfc-editor.org/rfc/rfc9126.html#section-4).

### 2.4 Metadata e política

O discovery do authorization server pode anunciar:

- `pushed_authorization_request_endpoint`;
- `require_pushed_authorization_requests`, cujo default é `false` quando omitido.

A exigência de PAR também pode ser configurada por client.

Fonte: [RFC 9126, seções 5 e 6](https://www.rfc-editor.org/rfc/rfc9126.html#section-5).

---

## 3. Estado atual do RoyalIdentity

### `IMessageStore`

O contrato oferece apenas `WriteAsync<TModel>`, `ReadAsync<TModel>` e `DeleteAsync`. Hoje ele transporta mensagens do
fluxo de logout, como `LogoutMessage` e `LogoutCallbackMessage`.

`ProtectedDataMessageStore` serializa a mensagem, protege o payload com ASP.NET Core Data Protection e devolve o
conteúdo protegido em Base64Url como identificador. A leitura desfaz esse processo e `DeleteAsync` é um no-op. Portanto:

- não existe registro server-side;
- o tamanho do identificador cresce com o payload;
- não há TTL próprio do contrato;
- não há consumo atômico nem estado de consumo;
- não há vínculo explícito com realm, client ou propósito.

### `IAuthorizeParametersStore`

Armazena `NameValueCollection` durante a continuação interna do authorization flow. Login e consentimento escrevem os
parâmetros; resolvers podem lê-los mais de uma vez; o callback lê e depois apaga. Esse ciclo é semanticamente mais
próximo de uma authorization request que `IMessageStore`, mas ainda não atende PAR:

- não vincula o handle ao client autenticado;
- não representa expiração;
- não oferece consumo condicional/atômico;
- não distingue continuação interna de pushed request;
- não define a tolerância a reload permitida pela RFC.

### `IReplayCache`

O replay cache atual é usado por `private_key_jwt` para registrar `jti`. Ele representa prevenção de replay, não o
payload completo de uma authorization request. Além disso, seu contrato `ExistsAsync` + `AddAsync` também não é uma
operação atômica. Reutilizá-lo como store de PAR confundiria responsabilidades e não resolveria o armazenamento do
payload.

---

## 4. `PersistentDataMessageStore`

Uma implementação persistente de `IMessageStore` é coerente para mensagens transitórias de UI. Em vez de carregar o
payload no identificador, `WriteAsync` geraria um handle opaco e armazenaria o conteúdo; `ReadAsync` recuperaria por
handle; `DeleteAsync` removeria ou marcaria o registro.

Persistência e proteção do conteúdo são decisões ortogonais. Gravar no banco não implica armazenar JSON em claro. Uma
implementação pode proteger ou criptografar o payload antes da gravação, especialmente quando ele contiver dados
sensíveis.

Um desenho futuro precisaria decidir, no mínimo:

- handle aleatório e armazenamento apenas de seu hash, quando apropriado;
- propósito/tipo permitido da mensagem, evitando desserialização arbitrária;
- realm e demais vínculos de contexto;
- `CreatedAt`, `ExpiresAt` e política de limpeza;
- payload protegido, versão do formato e estratégia de rotação de chaves;
- semântica de read, delete e eventual consumo;
- comportamento idempotente e concorrente.

Esses requisitos são úteis mesmo sem PAR, mas não devem ser introduzidos pelo baseline de storage sem uma decisão
própria.

---

## 5. Alternativas para o store de PAR

### Alternativa A — `IPushedAuthorizationRequestStore`

Contrato específico, realm-bound, com operações equivalentes a armazenar e consumir uma pushed request. O consumo
receberia o handle e o client esperado e produziria um resultado que diferenciasse sucesso, inexistência, expiração,
client incorreto e uso anterior.

**Vantagens:** semântica explícita, contract tests diretos e menor risco de acoplar ciclos de vida distintos.

**Custo:** mais uma facade e possível duplicação de infraestrutura com o store da continuação interna.

### Alternativa B — `IAuthorizationRequestStore`

Evolução conceitual de `IAuthorizeParametersStore`, reunindo estados ligados a authorization requests, mas sem fingir
que todos possuem o mesmo ciclo de vida. O contrato poderia separar famílias de operação, por exemplo:

- continuação interna: armazenar, ler e apagar, permitindo múltiplas leituras entre GET e POST;
- PAR: armazenar e consumir condicionalmente, com TTL, realm e vínculo ao client.

**Vantagens:** ownership semântico único para authorization-request state e possibilidade de compartilhar modelo e
infraestrutura.

**Custo:** contrato mais amplo; exige impedir que a semântica flexível da continuação enfraqueça o uso único de PAR.

### Alternativa C — usar `IMessageStore` diretamente para PAR

Exigiria ampliar `IMessageStore` com contexto, expiração e consumo condicional. Isso afetaria também as mensagens de
logout e tornaria o contrato genérico responsável por regras específicas de OAuth.

**Avaliação atual:** não recomendada como facade pública. Um repositório interno comum ou uma implementação física
compartilhada continua possível sem unificar os contratos semânticos.

---

## 6. Persistência conceitual possível

Sem decidir schema ou projeto, um registro persistente de PAR provavelmente precisaria representar:

| Dado | Motivo |
|---|---|
| Realm | isolamento obrigatório do IdP |
| Hash do handle | lookup sem armazenar o bearer handle em claro |
| Client id | vínculo obrigatório definido pela RFC |
| Payload da authorization request | reconstrução da request no authorization endpoint |
| Criação e expiração | cálculo de `expires_in` e rejeição após TTL |
| Estado/instante de consumo | uso único, auditoria técnica e concorrência |
| Versão do payload | evolução segura da serialização |

O armazenamento físico tem perfil operacional: alto churn, TTL curto e limpeza frequente. Isso aponta para um backing
em `Data.Operational`, mas não decide qual facade o expõe. Ownership semântico do contrato e localização física dos
dados devem ser avaliados separadamente.

---

## 7. Operação crítica de concorrência

Um contrato futuro não deveria modelar PAR como `ReadAsync` seguido de `DeleteAsync`. Duas requests concorrentes
poderiam ler o mesmo payload antes da remoção. A operação precisa ser condicional e atômica no provider, por exemplo:

```text
TryConsume(realm, handle, expectedClient, now)
    -> Success(payload)
     | NotFound
     | Expired
     | ClientMismatch
     | AlreadyConsumed
```

A eventual tolerância a reload não deve acontecer por acidente. Ela exige uma política explícita, provavelmente com
janela curta e critérios que não permitam trocar client ou request context.

---

## 8. Trabalho necessário antes da implementação

Uma futura ADR/plano deverá decidir:

1. facade específica de PAR ou `IAuthorizationRequestStore` com operações separadas;
2. ciclo de vida da continuação interna e compatibilidade com o `IAuthorizeParametersStore` atual;
3. política de uso único e eventual tolerância a reload;
4. modelo de payload, serialização e proteção em repouso;
5. geração/formato do `request_uri` e armazenamento do handle;
6. TTL, cleanup e observabilidade sem vazamento de dados;
7. endpoint realm-aware, autenticação de client e reaproveitamento da validação do authorization endpoint;
8. metadata global/por realm e política `require_pushed_authorization_requests` global/por client;
9. erros de protocolo e testes de isolamento, expiração, concorrência e replay;
10. relação futura com JAR/request objects, sem tornar JAR requisito para a primeira entrega de PAR.

Até essas decisões serem tomadas, PAR permanece fora do `plan-data-storage-baseline.md` e registrado no backlog.

---

## 9. Referências

- [RFC 9126 — OAuth 2.0 Pushed Authorization Requests](https://www.rfc-editor.org/rfc/rfc9126.html)
- [`IMessageStore`](../../RoyalIdentity/Contracts/Storage/IMessageStore.cs)
- [`ProtectedDataMessageStore`](../../RoyalIdentity/Contracts/Defaults/ProtectedDataMessageStore.cs)
- [`IAuthorizeParametersStore`](../../RoyalIdentity/Contracts/Storage/IAuthorizeParametersStore.cs)
- [`IReplayCache`](../../RoyalIdentity/Contracts/Storage/IReplayCache.cs)
- [ADR-013 — arquitetura modular e storages como facades](../../adrs/ADR-013.md)
- [Plano macro de dados](../plans/plan-data-macro.md)

