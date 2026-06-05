# Plan: Context Pattern Redesign

## Status: COMPLETED

## Progresso

`██████████` **100%** — 5 de 5 fases concluídas (Fase 2 adiada — ver "Decisão"; remoção de `Items/Token.cs` adiada na Fase 5 — ver nota)

## Ordem de execução (global)

Os três planos rodam **um por vez, cada um 100% completo antes do próximo**:

1. Constantes (`plan-constants-refactoring.md`)
2. **Contextos** ← este plano
3. UI (`plan-ui-screens-refactoring.md`)

## Context

Os contextos em `RoyalIdentity/Contexts/` são o contrato central do sistema. Todo o pipeline passa por eles. A modelagem atual tem problemas de consistência identificados via leitura do código:

1. `IWith*` interfaces herdam `IEndpointContextBase` — acoplamento de capacidade com infraestrutura
2. Hierarquia de herança de interfaces cria rigidez (diamond em `IAuthorizationContextBase`)
3. Dois mecanismos de dados sem regra explícita: propriedades diretas vs `Parameters/*`
4. Flags de validação (`redirectUriValidated`, `resourcesValidated`) como booleans privados implícitos
5. `IWithResources` mistura acesso a dados com sinalização de estado (`ResourcesValidated()`)
6. `Items/Token.cs` e `Items/Tokens.cs` marcados `[Redesign]` como desnecessários
7. `ClientParameters.ClientClaims` marcado `[Redesign]` para mover apenas para `ClientCredentialsContext`

---

## Diagnóstico por Elemento

### IWith* — problema de herança

**Estado atual:**
```
IWithAcr : IEndpointContextBase
IWithClient : IEndpointContextBase
IWithCodeChallenge : IWithClient
IWithPrompt : IWithClient, IWithAcr
IWithRedirectUri : IWithClient
IWithResources : IWithClient
IWithBearerToken : IEndpointContextBase
IWithRefreshToken  (sem herança — correto)
IWithAuthorizationCode (sem herança — verificado, já no estado-alvo)
```

> **Verificado**: `IWithAuthorizationCode` e `IWithRefreshToken` já não herdam de nada — nenhuma ação necessária nesses dois.

**Problema**: `IWithClient : IEndpointContextBase` significa que toda interface de capacidade herda o contrato de contexto de endpoint. Isso impede criar implementações de teste leves, e cria um acoplamento desnecessário entre "ter um client" e "ser um endpoint context".

**Regra atual implícita**: `IWith*` = "este contexto suporta X" — é uma interface de capacidade para constrainar decorators.

**Solução proposta**: `IWith*` não deve herdar de nada. A constraint do decorator deve ser expressa com múltiplos constraints do C#:

```csharp
// Antes:
public class LoadClient : IDecorator<IWithClient> { }
// (funciona porque IWithClient : IEndpointContextBase : IContextBase)

// Depois:
public class LoadClient<TContext> : IDecorator<TContext>
    where TContext : IEndpointContextBase, IWithClient { }
```

Ou manter `IWithClient : IEndpointContextBase` mas com justificativa explícita: "capacidades de endpoint sempre requerem contexto de endpoint". Avaliar se há algum caso de uso onde uma capability seria usada sem ser endpoint context. Se não houver, a herança é aceitável e o problema é menor do que parece.

### IWithResources — mistura de dados e estado

**Estado atual:**
```csharp
public interface IWithResources : IWithClient
{
    string? Scope { get; }
    RequestedScopes Scopes { get; }
    void ResourcesValidated();        // ← sinalização de estado
    void AssertResourcesValidated();  // ← sinalização de estado
}
```

**Problema**: Uma interface de dados (`Scopes`) misturada com método de sinalização de estado do pipeline. Quem chama `ResourcesValidated()`? Um decorator ou validator. Quem lê `AssertResourcesValidated()`? Um handler. Isso é um estado interno de progresso do pipeline exposto como contrato público.

**Solução proposta**: Remover os métodos de sinalização da interface — ela fica só com dados:

```csharp
public interface IWithResources : IWithClient
{
    string? Scope { get; }
    RequestedScopes Scopes { get; }
    // remove ResourcesValidated() e AssertResourcesValidated()
}
```

A flag de validação (`resourcesValidated`) e a assertiva passam a ser **membros do tipo concreto** (`AuthorizeContext` etc.), não da interface nem dos `Parameters`. O `ResourcesDecorator`/`AuthorizationResourcesValidator` sinalizam fazendo cast para o tipo concreto (ou recebendo o tipo concreto via constraint), e o handler chama a assertiva no tipo concreto.

> **Decisão fixada (aprovada)**: a assertiva fica no **tipo concreto que possui as propriedades**, *não* nos `Parameters/*`. Motivo técnico: a versão de `redirect_uri` usa `[MemberNotNull(nameof(RedirectUri))]` (ver próxima seção), e `[MemberNotNull]` só pode referenciar um membro do **mesmo tipo**. Movida para um objeto `Parameters`, ela não conseguiria garantir o não-nulo de uma propriedade que vive no contexto — perderia a análise de null-state. Portanto: dados na interface; flag + assertiva no concreto.

### IWithRedirectUri — mesmo problema

```csharp
public interface IWithRedirectUri : IWithClient
{
    string? RedirectUri { get; }
    void RedirectUriValidated();        // ← estado
    void AssertHasRedirectUri();        // ← assertiva
}
```

Mesma solução: `RedirectUriValidated()` e `AssertHasRedirectUri()` saem da interface. A flag fica privada na classe concreta. O decorator `RedirectUriValidator` não precisa conhecer essa flag via interface.

> **Atenção ao `[MemberNotNull]`**: no código atual `AssertHasRedirectUri()` é anotado com `[MemberNotNull(nameof(RedirectUri))]`. Essa anotação **deve permanecer no tipo concreto** que declara `RedirectUri` — é o único lugar onde o compilador a aceita e onde ela continua informando ao handler que `RedirectUri` é não-nulo após a chamada. Não mover para `Parameters`.

### Parameters/* — contrato a formalizar

**Estado atual**: convenção implícita funciona, mas não está documentada.

**Regra a formalizar explicitamente** (documentar em comentário no topo de cada Parameters class ou em um README da pasta):

> **Propriedades diretas** no contexto = dados parseados do request bruto via `Load()`.  
> **Parameters/* objects** = dados carregados de stores por decorators, mutáveis durante o pipeline.

`Parameters/*` sempre têm:
- Propriedades `{ get; private set; }` (mutabilidade controlada)
- Método(s) `Set*()` chamados por decorators
- `Assert*()` com `[MemberNotNull]` chamados por handlers e validators tardios

**Ação**: formalizar isso como padrão escrito no código, e garantir que nenhum novo decorator escreva estado diretamente em propriedades públicas do contexto.

### ClientParameters.ClientClaims — remoção

```csharp
[Redesign("Use only in ClientCredentialsContext -- remove")]
public HashSet<Claim> ClientClaims { get; } = [];
```

Mover `ClientClaims` para `ClientCredentialsContext` diretamente, como propriedade local. Remover de `ClientParameters`. Verificar onde é lido e garantir que os callers passem a ler do contexto concreto.

### Items/Token.cs e Items/Tokens.cs — remoção

Ambos marcados `[Redesign("Acredito que o uso destes seja desnecessário")]`. Verificar os únicos callers antes de deletar. Se não forem usados ou forem usados apenas para logging/obfuscação, remover.

> **Resultado da execução (Fase 5):**
> - `Items/Tokens.cs` (a **coleção** de tokens) — **removido**: nenhum caller ativo; era armazenamento morto (escrito em `AuthorizeHandler` via `Items.AddToken`, sem nenhuma leitura). As 3 chamadas de escrita foram removidas junto.
> - `Items/Token.cs` (o **wrapper** de um único token, com obfuscação) — **mantido (remoção adiada)**. O guard "confirmar zero callers ativos" **falha**: `Token` é usado ativamente pelos eventos de emissão `CodeIssuedEvent`, `AccessTokenIssuedEvent` e `IdentityTokenIssuedEvent` (criados em `AuthorizeHandler`), que carregam o valor obfuscado para auditoria. Removê-lo exige redesenhar o contrato desses eventos — mudança com blast radius no subsistema de eventos, fora do escopo cirúrgico/baixo-risco deste plano. Adiado para um futuro redesign de eventos, na mesma linha da Fase 2.

---

## Estado Alvo

> **Ressalva (Fase 2 adiada)**: a hierarquia "sem herança" abaixo é o alvo *de longo prazo*, condicionado à Fase 2. **Neste ciclo, as `IWith*` mantêm `: IEndpointContextBase`.** O que muda agora é a **remoção dos métodos de estado** das interfaces (Fases 3–5); a estrutura de herança permanece.

### Hierarquia de interfaces limpa (alvo de longo prazo — depende da Fase 2)

```
IContextBase (Pipelines)
  └── IEndpointContextBase (RoyalIdentity.Contexts)
        └── ITokenEndpointContextBase
        └── IAuthorizationContextBase

IWithClient           (sem herança — só propriedades)
IWithResources        (sem herança — só propriedades/dados)
IWithCodeChallenge    (sem herança)
IWithRedirectUri      (sem herança — apenas string? RedirectUri)
IWithPrompt           (sem herança)
IWithAcr              (sem herança)
IWithBearerToken      (sem herança)
IWithRefreshToken     (sem herança)
IWithAuthorizationCode (sem herança)
```

Constraints de decorators usam múltiplos bounds:

```csharp
public class LoadClient<TContext> : IDecorator<TContext>
    where TContext : class, IEndpointContextBase, IWithClient
```

### Contextos concretos implementam interfaces explicitamente

```csharp
public class AuthorizeContext 
    : EndpointContextBase, 
      IAuthorizationContextBase, 
      IWithCodeChallenge
// IAuthorizationContextBase : IWithRedirectUri, IWithResources, IWithPrompt
// Sem métodos de estado nas interfaces
```

### Parameters/* têm regra única de mutação

Apenas métodos `Set*()` dentro do objeto alteram estado. Nenhuma propriedade tem setter público.

---

## Passos de Execução

### Fase 1 — Auditoria (sem alterar código)

1. Mapear todos os decorators e validators que têm `where TContext : IWith*`.
2. Mapear todos os callers de `ResourcesValidated()`, `AssertResourcesValidated()`, `RedirectUriValidated()`, `AssertHasRedirectUri()`.
3. Mapear todos os callers de `ClientParameters.ClientClaims`.
4. Verificar se `Items/Token.cs` e `Items/Tokens.cs` têm callers ativos.
5. ~~Verificar se `IWithRefreshToken` e `IWithAuthorizationCode` herdam de algo~~ — **já confirmado: nenhum herda; ambos já estão no estado-alvo.**

### Fase 2 — Remover herança de IWith* de IEndpointContextBase — **ADIADA (não executar agora)**

> **Decisão**: manter `IWith*` herdando de `IEndpointContextBase` por ora. Justificativa em "Decisão" abaixo. Esta fase fica registrada para o futuro, mas **não entra neste ciclo de trabalho**. As Fases 3–6 são independentes dela.

Se um dia for executada:
1. Remover `: IEndpointContextBase` de `IWithAcr` e `IWithClient`.
2. Atualizar todos os decorators para usar múltiplos constraints.
3. Verificar build.

### Fase 3 — Remover métodos de estado das interfaces

1. Remover `ResourcesValidated()` e `AssertResourcesValidated()` de `IWithResources`.
2. Remover `RedirectUriValidated()` e `AssertHasRedirectUri()` de `IWithRedirectUri`.
3. Os booleans privados nos contextos concretos (`redirectUriValidated`, `resourcesValidated`) continuam existindo — mas são estado interno, não contrato de interface.
4. A flag e a assertiva (incl. `[MemberNotNull(nameof(RedirectUri))]`) ficam **no tipo concreto** que declara a propriedade. Os callers que usavam os métodos via interface fazem cast para o tipo concreto (ou recebem o concreto via constraint). **Não** mover a assertiva para `Parameters` (quebra o `[MemberNotNull]`).
5. Verificar build.

### Fase 4 — Migrar ClientClaims

1. Adicionar `HashSet<Claim> ClientClaims { get; }` diretamente em `ClientCredentialsContext`.
2. Remover `ClientClaims` de `ClientParameters`.
3. Atualizar `ClientParameters.SetClient()` para não popular claims (mover essa lógica para `ClientCredentialsContext`).
4. Atualizar todos os callers de `ClientParameters.ClientClaims`.

### Fase 5 — Remover Items/Token e Items/Tokens

1. Confirmar zero callers ativos.
2. ~~Deletar `RoyalIdentity/Contexts/Items/Token.cs`~~ — **adiado**: tem callers ativos (eventos de emissão de token). Ver nota em "Items/Token.cs e Items/Tokens.cs — remoção".
3. Deletar `RoyalIdentity/Contexts/Items/Tokens.cs` — **feito** (era armazenamento morto).
4. Verificar build.

### Fase 6 — Documentar a convenção Parameters/*

1. Adicionar comentário XML ou summary file em `Contexts/Parameters/` explicando o padrão. **Feito**: a convenção completa está documentada no XML doc de `ClientParameters`; `CodeParameters`, `BearerParameters` e `RefreshParameters` têm um doc conciso que referencia (`<see cref="ClientParameters"/>`) a descrição canônica.
2. Revisar todos os `Parameters/*` existentes para garantir conformidade (nenhum setter público). **Feito**: as 4 classes usam `{ get; private set; }` + `Set*()` + `Assert*()` com `[MemberNotNull]`.

---

## Arquivos Diretamente Afetados

| Arquivo | Ação |
|---|---|
| `Contexts/Withs/IWithResources.cs` | **Fase 3** — Remover `ResourcesValidated()` e `AssertResourcesValidated()` |
| `Contexts/Withs/IWithRedirectUri.cs` | **Fase 3** — Remover `RedirectUriValidated()` e `AssertHasRedirectUri()` (assertiva + `[MemberNotNull]` vão para o tipo concreto) |
| `Contexts/AuthorizeContext.cs` | **Fase 3** — Mover flag `resourcesValidated`/`redirectUriValidated` + assertivas (com `[MemberNotNull]`) para membros privados/internos do concreto |
| `Contexts/AuthorizationCodeContext.cs` | **Fase 3** — Idem |
| `Contexts/Parameters/ClientParameters.cs` | **Fase 4** — Remover `ClientClaims` |
| `Contexts/ClientCredentialsContext.cs` | **Fase 4** — Receber `ClientClaims` como propriedade local |
| `Contexts/Items/Token.cs` | **Fase 5** — ~~Deletar~~ **adiado**: callers ativos nos eventos de emissão de token (ver nota) |
| `Contexts/Items/Tokens.cs` | **Fase 5** — Deletar (feito — armazenamento morto) |
| `Contexts/Withs/IWithAcr.cs` | ~~Remover herança de `IEndpointContextBase`~~ — **Fase 2 adiada** |
| `Contexts/Withs/IWithClient.cs` | ~~Remover herança de `IEndpointContextBase`~~ — **Fase 2 adiada** |
| `Contexts/Withs/IWithCodeChallenge.cs` | ~~Remover herança de `IWithClient`~~ — **Fase 2 adiada** |
| `Contexts/Withs/IWithPrompt.cs` | ~~Remover herança de `IWithClient` e `IWithAcr`~~ — **Fase 2 adiada** |
| Todos os decorators com constraint `IWith*` | ~~Atualizar constraints~~ — **Fase 2 adiada** |

---

## Decisão (Resolvida) — manter herança de `IWith*`

**Questão central**: `IWith*` deve ou não herdar de `IEndpointContextBase`?

**Decisão: manter a herança (não executar a Fase 2 agora).**

- **Sim (escolhido)**: Hoje **todas** as `IWith*` são usadas exclusivamente em endpoint contexts, e os decorators dependem dessa herança para o constraint simples (`IDecorator<IWithClient>`). Manter a herança preserva os decorators como estão.
- **Não (remover herança)**: traria flexibilidade para testes leves, mas adiciona verbosidade em **todos** os decorators (`where TContext : IEndpointContextBase, IWithClient`) em troca de uma testabilidade que ninguém exerce hoje.

**Custo/benefício**: o ganho real do redesign está nas Fases 3–5 (tirar sinalização de estado do contrato público), que são cirúrgicas e de baixo risco. A Fase 2 tem blast radius alto e benefício atualmente teórico → **adiada**. Reavaliar quando/se surgir necessidade de usar `IWith*` fora de endpoint contexts (ex.: testes unitários de decorators sem montar um endpoint context completo).

---

## Riscos

- Fase 2 (herança) tem blast radius alto — todos os decorators seriam afetados. **Por isso foi adiada** (ver "Decisão").
- Fases 3, 4, 5 são cirúrgicas e de baixo risco — o foco deste ciclo.
- Nenhuma fase altera comportamento em runtime — apenas contratos de tipo.
- `[MemberNotNull]` deve permanecer no tipo concreto que declara a propriedade (`RedirectUri`); movê-lo para `Parameters` quebraria a análise de null-state. Ver Fase 3.
