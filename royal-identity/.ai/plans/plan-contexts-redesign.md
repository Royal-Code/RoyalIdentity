# Plan: Context Pattern Redesign

## Status: PENDING

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
IWithAuthorizationCode (não visto — verificar)
```

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

**Solução proposta**: Mover os métodos de sinalização para os `Parameters/*` objects, e remover da interface:

```csharp
public interface IWithResources : IWithClient
{
    string? Scope { get; }
    RequestedScopes Scopes { get; }
    // remove ResourcesValidated() e AssertResourcesValidated()
}
```

O `ResourcesDecorator` e o `AuthorizationResourcesValidator` que chamam esses métodos devem usar `Parameters` internamente ou uma propriedade booleana no contexto concreto — mas não via interface pública.

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

---

## Estado Alvo

### Hierarquia de interfaces limpa

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
5. Verificar se `IWithRefreshToken` e `IWithAuthorizationCode` herdam de algo (confirmar).

### Fase 2 — Remover herança de IWith* de IEndpointContextBase (SE for o caminho escolhido)

1. Remover `: IEndpointContextBase` de `IWithAcr` e `IWithClient`.
2. Atualizar todos os decorators para usar múltiplos constraints.
3. Verificar build.

> **Alternativa**: Se a decisão for manter `IWithClient : IEndpointContextBase` (argumento: capacidades de endpoint sempre pressupõem contexto de endpoint), pular esta fase e documentar a decisão.

### Fase 3 — Remover métodos de estado das interfaces

1. Remover `ResourcesValidated()` e `AssertResourcesValidated()` de `IWithResources`.
2. Remover `RedirectUriValidated()` e `AssertHasRedirectUri()` de `IWithRedirectUri`.
3. Os booleans privados nos contextos concretos (`redirectUriValidated`, `resourcesValidated`) continuam existindo — mas são estado interno, não contrato de interface.
4. Os callers que usavam os métodos via interface agora devem fazer cast para o tipo concreto, OU mover a assertiva para dentro do `Parameters` correspondente.
5. Verificar build.

### Fase 4 — Migrar ClientClaims

1. Adicionar `HashSet<Claim> ClientClaims { get; }` diretamente em `ClientCredentialsContext`.
2. Remover `ClientClaims` de `ClientParameters`.
3. Atualizar `ClientParameters.SetClient()` para não popular claims (mover essa lógica para `ClientCredentialsContext`).
4. Atualizar todos os callers de `ClientParameters.ClientClaims`.

### Fase 5 — Remover Items/Token e Items/Tokens

1. Confirmar zero callers ativos.
2. Deletar `RoyalIdentity/Contexts/Items/Token.cs`.
3. Deletar `RoyalIdentity/Contexts/Items/Tokens.cs`.
4. Verificar build.

### Fase 6 — Documentar a convenção Parameters/*

1. Adicionar comentário XML ou summary file em `Contexts/Parameters/` explicando o padrão.
2. Revisar todos os `Parameters/*` existentes para garantir conformidade (nenhum setter público).

---

## Arquivos Diretamente Afetados

| Arquivo | Ação |
|---|---|
| `Contexts/Withs/IWithAcr.cs` | Remover herança de `IEndpointContextBase` |
| `Contexts/Withs/IWithClient.cs` | Remover herança de `IEndpointContextBase`; remover `IEndpointContextBase` da assinatura |
| `Contexts/Withs/IWithResources.cs` | Remover `ResourcesValidated()` e `AssertResourcesValidated()` |
| `Contexts/Withs/IWithRedirectUri.cs` | Remover `RedirectUriValidated()` e `AssertHasRedirectUri()` |
| `Contexts/Withs/IWithCodeChallenge.cs` | Remover herança de `IWithClient` se não for mais necessária |
| `Contexts/Withs/IWithPrompt.cs` | Remover herança de `IWithClient` e `IWithAcr` |
| `Contexts/Parameters/ClientParameters.cs` | Remover `ClientClaims` |
| `Contexts/AuthorizeContext.cs` | Expor métodos de estado como membros privados/internos |
| `Contexts/AuthorizationCodeContext.cs` | Idem |
| `Contexts/Items/Token.cs` | Deletar |
| `Contexts/Items/Tokens.cs` | Deletar |
| Todos os decorators com constraint `IWith*` | Atualizar constraints |

---

## Decisão Pendente (Requer Input do Autor)

**Questão central**: `IWith*` deve ou não herdar de `IEndpointContextBase`?

- **Sim (manter atual)**: Capacidades de endpoint sempre pressupõem contexto de endpoint. Simplifica constraints de decorators. Custo: não dá para usar IWith* em contextos de teste leves.
- **Não (remover herança)**: Maior flexibilidade. Constraints de decorators ficam mais verbosas. Permite testes sem overhead de endpoint context.

Esta decisão impacta Fase 2 inteira. As demais fases são independentes desta decisão.

---

## Riscos

- Fase 2 (herança) tem blast radius alto — todos os decorators são afetados.
- Fases 3, 4, 5 são cirúrgicas e de baixo risco.
- Nenhuma fase altera comportamento em runtime — apenas contratos de tipo.
