# Plan: Constants Refactoring

## Status: PENDING

## Context

`RoyalIdentity/Options/Constants.cs` contém duas realidades distintas:

1. **Estrutura alvo** — `Constants` (partial class, linha 10): hierarquia organizada com `Constants.Server`, `Constants.UI`, `Constants.Oidc`, etc.
2. **Classes legadas** — três top-level classes (`OidcConstants`, `JwtClaimTypes`, `ServerConstants`) marcadas ou identificadas para migração.

`OidcConstants` tem `[Redesign("Move all to Constants")]` explícito. `ServerConstants.ProfileIsActiveCallers` tem `[Redesign("Remover")]` explícito.

## Objetivo

Consolidar todas as constantes dentro da hierarquia `Constants`, eliminando as classes top-level legadas.

---

## Convenção de Nomenclatura para `Constants.Oidc`

Constantes agrupadas por **endpoint ou protocolo**, com inner class por função:

```
Constants.Oidc.Authorize.Request        ← parâmetros do authorize request
Constants.Oidc.Authorize.Errors         ← erros do authorize
Constants.Oidc.Authorize.Response       ← campos da authorize response
Constants.Oidc.Token.Request            ← parâmetros do token endpoint
Constants.Oidc.Token.Errors             ← erros do token endpoint
Constants.Oidc.Token.Response           ← campos da token response
Constants.Oidc.Token.Types              ← tipos de token (access_token, id_token, ...)
Constants.Oidc.Token.TypeIdentifiers    ← URNs dos tipos de token
Constants.Oidc.Token.RequestTypes       ← bearer, pop
Constants.Oidc.Revocation.Request       ← parâmetros do revocation endpoint
Constants.Oidc.Introspection.Request    ← parâmetros do introspection endpoint
Constants.Oidc.EndSession.Request       ← parâmetros do end_session endpoint
Constants.Oidc.Discovery               ← chaves do discovery document (flat — já é um dicionário de strings)
Constants.Oidc.ResponseTypes           ← code, token, id_token (flat — enum-like)
Constants.Oidc.ResponseModes           ← query, fragment, form_post
Constants.Oidc.PromptModes             ← none, login, consent, ...
Constants.Oidc.CodeChallenge.Methods   ← plain, S256
Constants.Oidc.SubjectTypes            ← pairwise, public
Constants.Oidc.DisplayModes            ← page, popup, touch, wap
Constants.Oidc.Endpoint.AuthMethods    ← client_secret_basic, private_key_jwt, ...
Constants.Oidc.AuthSchemes             ← Bearer, DPoP, PoP
Constants.Oidc.ClientAssertionTypes    ← jwt-bearer URN
Constants.Oidc.ProtectedResource.Errors ← invalid_token, expired_token, ...
Constants.Oidc.Backchannel.*           ← CIBA (baixa prioridade)
Constants.Oidc.Device.*               ← Device Authorization (baixa prioridade)
Constants.Oidc.Par.*                  ← PAR (baixa prioridade)
Constants.Oidc.Registration.*         ← Dynamic Registration (baixa prioridade)
Constants.Oidc.Events                 ← back-channel logout event
Constants.Oidc.HttpHeaders            ← DPoP, DPoP-Nonce
```

---

## Avaliação Prévia — O Que Realmente Precisa Ser Migrado

**Antes de criar qualquer constante nova**, avaliar cada grupo:

### Constantes que podem vir de pacotes MS

`Microsoft.IdentityModel.Protocols.OpenIdConnect` (`OpenIdConnectGrantTypes`, `OpenIdConnectParameterNames`, `OpenIdConnectScopes`) e `Microsoft.IdentityModel.JsonWebTokens` já expõem muitas dessas strings. Se o código já usa (ou poderia usar) as constantes MS diretamente, não vale duplicar.

**Ação por grupo**:

| Grupo | Avaliação |
|---|---|
| `OidcConstants.AuthorizeRequest` | Verificar overlap com `OpenIdConnectParameterNames` do pacote MS |
| `OidcConstants.TokenRequest` | Idem — `OpenIdConnectParameterNames` cobre vários |
| `JwtClaimTypes` | `Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames` cobre o núcleo. Avaliar quais claims são extensões do projeto (ex: `role`, `roles`, `idp`) vs padrão JWT/OIDC |
| `OidcConstants.ResponseTypes` | `OpenIdConnectResponseType` já existe no pacote MS |
| `OidcConstants.ResponseModes` | `OpenIdConnectResponseMode` já existe no pacote MS |
| `OidcConstants.PromptModes` | Verificar `OpenIdConnectPrompt` |
| `ServerConstants.StandardScopes` | `OpenIdConnectScope` no pacote MS |
| `OidcConstants.Discovery` | Strings do discovery document — verificar `OpenIdConnectConstants` ou similares |

**Regra**: Se o pacote MS já tem a constante com o mesmo valor, **não migrar** — refatorar o código chamador para usar diretamente o pacote MS. Só criar em `Constants` o que é genuinamente específico do RoyalIdentity.

### Constantes de baixa prioridade (não implementadas ainda)

CIBA (`Backchannel*`), Device Authorization (`DeviceAuthorization*`), PAR (`PushedAuthorizationRequest*`), Dynamic Registration (`Registration*`, `ClientMetadata`) — funcionalidades não implementadas no sistema hoje. **Não migrar agora**. Manter nas classes legadas até que o código que as usa exista.

### Candidatos a deleção direta (sem migrar)

- `ServerConstants.ProfileIsActiveCallers` — `[Redesign("Remover")]` explícito
- `Constants.OAuth` — vazio, deletar
- Constantes de CIBA/Device/PAR/Registration — só migrar quando a feature for implementada

---

## Estratégia de Implementação — Uma Constante por Vez

A migração **não** é feita em lote. O ciclo por constante é:

```
1. Escolher UMA constante (ou sub-grupo coeso pequeno)
2. Grep — encontrar todos os usos no codebase
3. Avaliar: já existe no pacote MS? → usar pacote MS e remover a constante
         : é específica do projeto? → criar em Constants.* no lugar correto
4. Substituir todos os usos pela nova referência
5. Build (deve passar)
6. Testes (devem passar)
7. Remover a constante da classe legada
8. Próxima
```

**Ordem sugerida** (do mais usado para o menos, do mais simples para o mais complexo):

1. `JwtClaimTypes` — claims JWT mais usados (sub, aud, iss, exp, iat, jti, scope, sid) → avaliar quais vêm do pacote MS
2. `OidcConstants.ResponseTypes` — alto uso, verificar `OpenIdConnectResponseType`
3. `OidcConstants.AuthorizeErrors` — alto uso em validators
4. `OidcConstants.TokenErrors` — alto uso em handlers
5. `OidcConstants.AuthorizeRequest` (parâmetros) — verificar overlap com `OpenIdConnectParameterNames`
6. `OidcConstants.TokenRequest` (parâmetros) — idem
7. `ServerConstants` (partes em uso) — conforme surgir necessidade
8. Grupos de baixa prioridade — adiar

---

## Conflitos Conhecidos

1. **`TokenTypes` duplicado**: `OidcConstants.TokenTypes` e `ServerConstants.TokenTypes` têm os mesmos valores. Consolidar em `Constants.Oidc.Token.Types`. Verificar semântica diferente antes.

2. **`ResponseTypes`**: `Constants.Server.SupportedResponseTypes` é uma coleção de runtime; `OidcConstants.ResponseTypes` são as constantes string. Não são a mesma coisa — manter separados.

3. **`using static` em `.razor`**: arquivos `.razor` que usam `using static OidcConstants` ou `using static JwtClaimTypes` precisam de atualização em `_Imports.razor` ou no próprio arquivo. Verificar durante cada ciclo de migração.

---

## Passos de Execução (Nível de Processo)

1. **Grep geral** — mapear volume: quantos usos de `OidcConstants`, `JwtClaimTypes`, `ServerConstants` existem no codebase total. Serve para estimar esforço.
2. **Decisão inicial** — verificar quais constantes de `JwtClaimTypes` e `OidcConstants.ResponseTypes` já têm equivalente nos pacotes MS. Determinar o que será deletado vs migrado.
3. **Ciclos de migração** — uma constante/grupo por ciclo conforme ordem sugerida acima.
4. **Ao final de cada classe legada** — quando zero referências restarem, deletar a classe.

---

## Riscos

- **Risco baixo**: puramente de nomes — não altera comportamento em runtime.
- **Risco médio**: `using static` em `.razor` pode não compilar se namespace não for reexportado — verificar `_Imports.razor` a cada ciclo que toque em constantes usadas no Razor.
- **Restrição**: o atributo `[Redesign]` está definido no projeto — verificar se precisa ser migrado junto ou se desaparece com as classes.
