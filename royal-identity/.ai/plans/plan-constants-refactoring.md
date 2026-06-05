# Plan: Constants Refactoring

## Status: COMPLETED

## Progresso

`██████████` **100%** — **MIGRAÇÃO COMPLETA**: `JwtClaimTypes`, `OidcConstants` e `ServerConstants` **deletadas**. Todas as constantes consolidadas em `Constants.*`

### Concluído
- **Passo 0 (diretivas `using static`)**: estratégia decidida — Strategy B (manter `global using static Constants`; callers usam `Jwt.ClaimTypes.*` via o global já existente; sem novo global using static por subclasse)
- **`Constants.Jwt` criado** com:
  - `Constants.Jwt.ClaimTypes`: `IdentityProvider`, `Role`, `Roles`, `ReferenceTokenId`, `Scope`, `Confirmation`
  - `Constants.Jwt.ClaimTypes.JwtTypes`: `AccessToken`, `AuthorizationRequest`, `DPoPProofToken`
  - `Constants.Jwt.ConfirmationMethods`: `JsonWebKey`, `JwkThumbprint`, `X509ThumbprintSha256`
- **`Constants.OAuth` deletado** (estava vazio)
- **`[Redesign("Move all to Constants")]`** adicionado a `JwtClaimTypes` e `ServerConstants`
- **`global using System.IdentityModel.Tokens.Jwt`** adicionado a `RoyalIdentity/Global.Usings.cs`, `Tests.Integration/Global.Usings.cs`; `using System.IdentityModel.Tokens.Jwt` adicionado a `RealmMemoryStore.cs`
- **`JwtClaimTypes` esvaziado — removidos**:
  - Project-specific: `IdentityProvider`, `Role`, `Roles`, `ReferenceTokenId` + inner classes `JwtTypes`, `ConfirmationMethods`
  - Grupo 1 (→ `JwtRegisteredClaimNames`): `Subject` (Sub), `Audience` (Aud), `Issuer` (Iss), `NotBefore` (Nbf), `Expiration` (Exp), `IssuedAt` (Iat), `AuthenticationMethod` (Amr), `SessionId` (Sid), `AuthenticationContextClassReference` (Acr), `AuthenticationTime` (AuthTime), `AuthorizedParty` (Azp), `AccessTokenHash` (AtHash), `AuthorizationCodeHash` (CHash), `Nonce` (Nonce), `JwtId` (Jti)
  - → `Jwt.ClaimTypes`: `Scope`, `Confirmation`
- **Callers atualizados** (~25 arquivos): `ClientCredentialsContext`, `DefaultIdentityUser`, `ClaimsExtensions`, `PrincipalExtensions`, `DefaultProfileService`, `DefaultTokenClaimsService`, `SubjectFactory`, `Constants.Filters`, `DefaultBackChannelLogoutNotifier`, `DefaultJwtFactory`, `DefaultTokenFactory`, `DefaultSignInManager`, `DefaultSignOutManager`, `AuthorizeContext`, `UserInfoHandler`, `RefreshTokenHandler`, `TokenBase`, `RefreshToken`, `AccessToken`, `RequestedScopes`, `RealmMemoryStore`
- **Build**: 0 erros (ambas as sessões)

### `JwtClaimTypes` — CONCLUÍDA ✓

- Deletada por completo; `using static OidcConstants` e `using static Constants` permanecem nos projetos principais
- `RealmMemoryStore.cs` recebeu `using static RoyalIdentity.Options.Constants` explícito (projeto sem Global.Usings)
- Claims migradas para `JwtRegisteredClaimNames`: Name, GivenName, FamilyName, Email, BirthDate (Birthdate), WebSite (Website), TokenType (Typ)
- Claims migradas para `Constants.Jwt.ClaimTypes`: todos os OIDC-profile, DPoP, extensões OAuth2
- **Build**: 0 erros

## Ordem de execução (global)

Os três planos rodam **um por vez, cada um 100% completo antes do próximo**:

1. **Constantes** ← este plano
2. Contextos (`plan-contexts-redesign.md`)
3. UI (`plan-ui-screens-refactoring.md`)

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

## Destino de `JwtClaimTypes` e `ServerConstants`

```
Constants.Jwt.ClaimTypes              ← claims (sub, name, email, role, roles, idp, reference_token_id, ...)
Constants.Jwt.ClaimTypes.JwtTypes     ← at+jwt, oauth-authz-req+jwt, dpop+jwt (inner class atual)
Constants.Jwt.ConfirmationMethods     ← jwk, jkt, x5t#S256
Constants.Server.*                    ← já existe; absorve ServerConstants (cookies, StandardScopes, SecretTypes, HttpClients, LocalApi, ...)
```

**`JwtClaimTypes` será migrado** (não apenas avaliado). A avaliação de overlap com `Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames` define, **por claim**, se o caller passa a referenciar o pacote MS (valor idêntico e padrão) ou se a constante é recriada em `Constants.Jwt.ClaimTypes` (claims do projeto: `role`, `roles`, `idp`, `reference_token_id`, etc.). O resultado é o mesmo nos dois casos: a classe `JwtClaimTypes` deixa de existir.

> **Caso especial**: `Constants.Filters` (em `Constants.cs`) referencia `JwtClaimTypes.*` ~40 vezes. Ao migrar, esses arrays passam a referenciar `Constants.Jwt.ClaimTypes.*` (mesmo arquivo) — confirmar que compila sem ambiguidade com o `global using static` existente.

> **Nota `[Redesign]`**: ao contrário de `OidcConstants`, as classes `JwtClaimTypes` e `ServerConstants` **não** têm `[Redesign]` no código, embora a intenção de consolidá-las exista (CLAUDE.md). Marcá-las com `[Redesign("Move all to Constants")]` no início do trabalho deixa a intenção explícita e ativa as advertências do analisador.

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

### Constantes de baixa prioridade (features não implementadas) — migrar por último

CIBA (`Backchannel*`), Device Authorization (`DeviceAuthorization*`), PAR (`PushedAuthorizationRequest*`), Dynamic Registration (`Registration*`, `ClientMetadata`) — funcionalidades ainda não implementadas no sistema.

**Devem ser migradas também**, mas ficam **por último** na ordem. Como não há código chamador hoje, a migração é puramente mecânica (mover a classe para o lugar correto em `Constants`, sem refatorar callers) e não bloqueia o resto. A meta final é que **nenhuma** constante reste nas classes legadas, permitindo removê-las por completo.

### Candidatos a deleção direta (sem migrar)

- `ServerConstants.ProfileIsActiveCallers` — `[Redesign("Remover")]` explícito
- `Constants.OAuth` — vazio, deletar

---

## Pré-requisito Crítico: `global using static` e usos não-qualificados

As classes legadas são expostas via *global using static*, então **a maioria dos usos não menciona o nome da classe**, e um grep textual por `OidcConstants.` / `JwtClaimTypes.` / `ServerConstants.` os **subconta drasticamente**.

`RoyalIdentity/Global.Usings.cs`:
```csharp
global using static RoyalIdentity.Options.OidcConstants;
global using static RoyalIdentity.Options.Constants;
```
Há ainda `using static` por arquivo e em `_Imports.razor` (ex.: `RoyalIdentity.Razor/_Imports.razor`, `RoyalIdentity.Storage.InMemory/ResourceStore.cs` → `ServerConstants`, `Tests.Integration/Prepare/SubjectFactory.cs` → `OidcConstants`) e `Tests.Integration/Global.Usings.cs`.

**Consequências:**
- Um uso como `TokenErrors.InvalidGrant` ou `AuthorizeRequest.Scope` resolve via global using — **não há prefixo** para o grep encontrar.
- Remover/renomear uma constante quebra a compilação em arquivos que **nem citam** a classe legada.
- O grep com prefixo serve apenas para **estimativa inicial** e para localizar as diretivas `using static` a atualizar.

**Ferramentas — abordagem confiável (compiler-driven):**

1. O oráculo definitivo é o **próprio compilador**: ao remover (ou renomear) a constante da classe legada, `dotnet build` lista **todos** os usos a corrigir — qualificados ou não. Rodar build a cada ciclo é a forma 100% confiável de encontrar callers.
2. No VS Code com **C# Dev Kit** (Roslyn), o desenvolvedor humano pode usar **Find All References (Shift+F12)** e **Rename Symbol (F2)** sobre a constante: resolvem por análise semântica e capturam usos via `using static` (o que o grep não faz). **Não há tool de refatoração Roslyn exposta ao agente nesta sessão**; portanto o agente opera com **grep (estimativa) + build (verdade)**.
3. **Estratégia de diretiva (decidir no passo 0)**: ao consolidar em `Constants.Oidc.*`/`Constants.Jwt.*`, escolher entre (a) trocar `global using static OidcConstants` por referências qualificadas via o `global using static …Constants` já existente (ex.: `Oidc.Token.Errors.InvalidGrant`), ou (b) manter um global using static por subclasse migrada para minimizar churn. Fixar isso antes do primeiro ciclo evita retrabalho.

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
8. Grupos de baixa prioridade (CIBA, Device, PAR, Registration) — **por último**, migração mecânica (mover sem refatorar callers, pois não há callers)

---

## Conflitos Conhecidos

1. **`TokenTypes` duplicado**: `OidcConstants.TokenTypes` e `ServerConstants.TokenTypes` têm os mesmos valores. Consolidar em `Constants.Oidc.Token.Types`. Verificar semântica diferente antes.

2. **`ResponseTypes`**: `Constants.Server.SupportedResponseTypes` é uma coleção de runtime; `OidcConstants.ResponseTypes` são as constantes string. Não são a mesma coisa — manter separados.

3. **`using static` em `.razor`**: arquivos `.razor` que usam `using static OidcConstants` ou `using static JwtClaimTypes` precisam de atualização em `_Imports.razor` ou no próprio arquivo. Verificar durante cada ciclo de migração.

---

## Passos de Execução (Nível de Processo)

1. **Passo 0 — Diretivas `using static`** — tratar primeiro o `global using static` (ver "Pré-requisito Crítico"). Decidir a estratégia de diretiva **antes** de migrar a primeira constante.
2. **Grep geral (estimativa, não exaustivo)** — mapear volume aproximado. ⚠️ O grep com prefixo **subconta** os usos não-qualificados (global using static); a contagem real só é conhecida pelo compilador ao remover cada constante. Usar o grep apenas para ordenar o esforço.
3. **Decisão inicial** — verificar quais constantes de `JwtClaimTypes` e `OidcConstants.ResponseTypes` já têm equivalente nos pacotes MS. Determinar, por constante, se o caller passa a usar o pacote MS (sem recriar) ou se a constante é recriada em `Constants.*`.
4. **Ciclos de migração** — uma constante/grupo por ciclo conforme ordem sugerida acima. O `dotnet build` após remover a constante legada é o que revela todos os callers.
5. **Ao final de cada classe legada** — quando zero referências restarem, deletar a classe (`OidcConstants`, depois `JwtClaimTypes`, depois `ServerConstants`).

---

## Riscos

- **Risco baixo**: puramente de nomes — não altera comportamento em runtime.
- **Risco médio (global using static)**: `OidcConstants`/`Constants` são global-using-static; a busca textual é incompleta e remoções quebram arquivos sem referência textual à classe. Mitigar com ciclos compiler-driven (build após cada remoção). Ver "Pré-requisito Crítico".
- **Risco médio**: `using static` em `.razor` pode não compilar se o namespace não for reexportado — verificar `_Imports.razor` a cada ciclo que toque em constantes usadas no Razor.
- **Restrição**: o atributo `[Redesign]` está definido no projeto — verificar se precisa ser migrado junto ou se desaparece com as classes.
