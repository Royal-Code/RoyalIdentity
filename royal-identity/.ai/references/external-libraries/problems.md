# Documentação da API SmartProblems (Problems, Result, FindResult)

Esta documentação apresenta os conceitos, funcionalidades e exemplos práticos para usar a biblioteca SmartProblems em projetos .NET.
Serve também como referência para ferramentas de IA (ex.: GitHub Copilot) compreenderem e gerarem código de forma correta com base na API da biblioteca.

Projetos alvo: .NET 8, .NET 9 e .NET 10.

> **Para IA/agentes:** se você precisa apenas gerar código correto, use
> [`problems.ai-rules.md`](problems.ai-rules.md) — imperativo, autocontido, com tabela de pacotes,
> cheat-sheet de assinaturas e anti-padrões. Este arquivo é o guia longo, com o "porquê".
>
> **Verificado contra:** `RoyalCode.SmartProblems` **1.0.0-preview-7.0** (net8.0 / net9.0 / net10.0).
> Ao alterar a API pública, atualize esta linha junto com o exemplo afetado. Se a versão instalada no
> seu projeto for outra, a documentação XML do pacote é a fonte da verdade — este arquivo é o guia de
> uso e de padrões.

Nota para IA e IDEs: este arquivo cobre **quando** e **por que** usar cada API, além das armadilhas que
o IntelliSense não revela (§8). Para confirmar sobrecargas, genéricos, retorno exato e ordem de parâmetros,
consulte a documentação XML das bibliotecas no pacote/IDE — especialmente em `Result`, `Result<TValue>`,
`FindResult<TEntity>`, `FindResult<TEntity,TId>`, `FindCriteria<TEntity>` e `AsyncResultExtensions`.
Antes de escrever a primeira linha de código, resolva pacote e `using` pela tabela da §1.1: os nomes de
pacote e de namespace **divergem** em vários casos e não são dedutíveis.

## 1. Introdução

SmartProblems padroniza o tratamento de resultados e erros de operações em .NET, evitando exceções para fluxo normal e tornando o código previsível e composable.

Conceitos principais:
- `Problem`: representa um erro com categoria, detalhe, propriedade e extensões.
- `Problems`: coleção de `Problem` (encadeável, iterável, conversível para `Result`).
- `Result` / `Result<T>`: resultado de operação (sucesso/falha), com APIs de composição/transformação.
- `FindResult<T>` / `FindResult<T, TId>`: resultado de busca, com utilitários para continuar/mapear e converter para `Result`.
- Conversões para `ProblemDetails` (RFC 9457) para uso em APIs.
- Extensões para Entity Framework (métodos `TryFind*`).

### 1.1 Pacotes, namespaces e `using`

Resolva isto **antes** de gerar código. O nome do pacote NuGet e o nome do namespace divergem em
vários casos — as linhas marcadas com ⚠️ não são dedutíveis a partir do tipo.

| Tipo / membro | `using` (namespace) | Pacote NuGet |
|---|---|---|
| `Problem`, `Problems`, `Result`, `Result<T>` | `RoyalCode.SmartProblems` | `RoyalCode.SmartProblems` |
| `FindResult<>`, `Id<,>`, `FindCriterion`, `FindCriteria<>`, `DisplayNames` | `RoyalCode.SmartProblems.Entities` | `RoyalCode.SmartProblems` |
| `TryFindAsync`, `TryFindByAsync`, `FindByCriteria`, `AddTo`, `SaveChanges`, `RemoveFromAsync` | ⚠️ `Microsoft.EntityFrameworkCore` | `RoyalCode.SmartProblems.EntityFramework` |
| `OkMatch`, `OkMatch<T>`, `CreatedMatch<T>`, `NoContentMatch` (tipos) | ⚠️ `RoyalCode.SmartProblems.HttpResults` | ⚠️ `RoyalCode.SmartProblems.ApiResults` |
| `.OkMatch()`, `.CreatedMatch()`, `.NoContentMatch()` (extensions) | ⚠️ `Microsoft.AspNetCore.Http` | ⚠️ `RoyalCode.SmartProblems.ApiResults` |
| `WithExceptionFilter` | ⚠️ `Microsoft.AspNetCore.Builder` | `RoyalCode.SmartProblems.ApiResults` |
| `ToActionResult` e afins (MVC) | ⚠️ `Microsoft.AspNetCore.Mvc` | `RoyalCode.SmartProblems.ApiResults` |
| `ToResultAsync`, `FailureTypeReader` | ⚠️ `System.Net.Http` / `RoyalCode.SmartProblems.Http` | `RoyalCode.SmartProblems.Http` |
| `ToProblemDetails(options)`, `ProblemDetailsExtended` | `RoyalCode.SmartProblems.Conversions` | ⚠️ `RoyalCode.SmartProblems.ProblemDetails` |
| `ProblemDetailsOptions`, `ProblemDetailsDescription` | `RoyalCode.SmartProblems.Descriptions` | `RoyalCode.SmartProblems.ProblemDetails` |
| `AddProblemDetailsDescriptions` | ⚠️ `Microsoft.Extensions.DependencyInjection` | `RoyalCode.SmartProblems.ProblemDetails` |
| `MapProblemDetailsDescriptionPage` | ⚠️ `Microsoft.AspNetCore.Builder` | `RoyalCode.SmartProblems.ProblemDetails` |
| `EnsureIsValid`, `ToProblems`, `HasProblems` (validator) | ⚠️ `FluentValidation` | `RoyalCode.SmartProblems.FluentValidation` |

Pontos que causam erro de compilação ou pacote faltante com mais frequência:

- Os tipos `OkMatch` e família estão no pacote **`ApiResults`**, mas no namespace **`HttpResults`**.
- Os métodos `.OkMatch()`, `.CreatedMatch()` e `.NoContentMatch()` são extensions em **`Microsoft.AspNetCore.Http`**.
- `ToResultAsync` é injetado em **`System.Net.Http`** (namespace já implícito com `ImplicitUsings`),
  então "compila sem `using`" — mas exige o pacote `RoyalCode.SmartProblems.Http`. Os tipos auxiliares
  (`FailureTypeReader`) ficam em `RoyalCode.SmartProblems.Http`.
- `ToProblemDetails` está no namespace `...Conversions`, porém é entregue pelo pacote
  **`RoyalCode.SmartProblems.ProblemDetails`** (o pacote `...Conversions` traz a serialização e
  `ProblemDetailsExtended`, não a conversão a partir de `Problems`).
- As extensões de EF e de ASP.NET Core usam os namespaces da Microsoft de propósito: instalado o pacote,
  os métodos aparecem sem `using` novo.

`using` canônicos por cenário:

```csharp
// Serviço de domínio (sem EF, sem HTTP)
using RoyalCode.SmartProblems;

// Serviço com EF
using Microsoft.EntityFrameworkCore;      // TryFindAsync, FindByCriteria, AddTo, SaveChanges
using RoyalCode.SmartProblems;            // Result, Problems
using RoyalCode.SmartProblems.Entities;   // FindResult, Id, FindCriterion

// Minimal API
using Microsoft.AspNetCore.Http;                 // OkMatch/CreatedMatch/NoContentMatch extension methods
using Microsoft.AspNetCore.Builder;             // WithExceptionFilter, MapProblemDetailsDescriptionPage
using Microsoft.Extensions.DependencyInjection; // AddProblemDetailsDescriptions
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.HttpResults;      // OkMatch, CreatedMatch, NoContentMatch
using RoyalCode.SmartProblems.Descriptions;     // ProblemDetailsOptions, ProblemDetailsDescription

// Cliente HTTP
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Http;       // FailureTypeReader (ToResultAsync já vem de System.Net.Http)

// Validação com FluentValidation
using FluentValidation;                   // EnsureIsValid, ToProblems
using RoyalCode.SmartProblems;
```

## 2. Funcionalidades Principais

- Modelagem de erros
  - Categorias: `NotFound`, `InvalidParameter`, `ValidationFailed`, `NotAllowed`, `InvalidState`, `InternalServerError`, `CustomProblem`.
  - Campos: `Detail`, `Property`, `TypeId`, `Extensions`.
  - Utilitários: `With(key, value)`, `ChainProperty(parent[, index])`, `ReplaceProperty(newProp)`.

- Coleção de erros (`Problems`)
  - Operadores: implícito de `Problem` para `Problems`, `+` para agregar problemas.
  - Iteração, indexador, `Contains`, `CopyTo`, `Count`.
  - Conversão para `Result` e para `InvalidOperationException` (`ToException(...)`).

- Resultados (`Result`, `Result<T>`)
  - Construção implícita a partir de valor, `Problem`, `Problems`, `Exception`.
  - Consultas: `IsSuccess`, `HasProblems(out problems)`, `HasValue(out value)`.
  - Composição: `Match`, `Map`, `Continue`, `Collect`, variantes `Async`.
  - Soma de problemas entre resultados (`+=`).

- Busca segura (`FindResult<T>`, `FindResult<T, TId>`)
  - Avaliação: `Found`, `NotFound(out problem)`, `HasInvalidParameter(...)`.
  - Composição: `Collect`, `Continue`, `Map` (+ `Async`).
  - Conversão: `ToResult([parameterName])`.
  - Fábrica: `FindResult<TEntity>.Problem(byName, propertyName, propertyValue)`.
  - Fábrica multi-critério: `FindResult<TEntity>.Problem(ReadOnlySpan<FindCriterion>)`.

- Conversão para `ProblemDetails`
  - `Problems.ToProblemDetails(options)`: namespace `RoyalCode.SmartProblems.Conversions`,
    entregue pelo pacote `RoyalCode.SmartProblems.ProblemDetails` (ver §1.1).
  - `ProblemDetailsExtended` agrega múltiplos problemas (`errors`, `not_found`, `inner_details`).
  - Personalização via `ProblemDetailsOptions`, `ProblemDetailsDescriptor`, `ProblemDetailsDescription` e arquivos JSON.
  - Página HTML de catálogo via `MapProblemDetailsDescriptionPage()`.

- Integrações
  - Entity Framework: `SmartProblemsEFExtensions` com `TryFindAsync`/`TryFindByAsync`, `FindByCriteria` e `FindResult`.
  - FluentValidation: `ValidationsExtensions` (`ToProblems`, `HasProblems`, `EnsureIsValid`, `Validate`/`ValidateAsync`).
  - HTTP/ASP.NET: utilitários para converter para `ProblemDetails` e resultados de API.

- Tratamento de exceções
  - `Problems.InternalError(Exception?, ExceptionOptions?)` com controle de mensagem, tipo e stack trace.
  - `Problems.ExceptionHandler` para mapear exceções customizadas em `Problem`.

## 3. Exemplos de uso: Problems

Antes dos exemplos, é importante entender que os problemas são criados por categoria, cada uma mapeando para um HTTP Status Code e um cenário recomendado de uso. A seguir, as categorias suportadas, o status associado e quando usar:

- `InvalidParameter` → 400 Bad Request
  - Quando: entrada inválida do cliente (formato, range, campos obrigatórios, enum inválido). Ideal para validações de request e regras de entrada.
  - Dica: use `Property` para apontar o campo específico; agregue várias ocorrências em uma única resposta.

- `ValidationFailed` → 422 Unprocessable Entity
  - Quando: regras de domínio/negócio foram violadas embora a entrada seja sintaticamente válida (ex.: estado inconsistente, combinação inválida). Foca em validação semântica.

- `NotAllowed` → 403 Forbidden
  - Quando: operação proibida devido a autorização/política/regra (ex.: usuário sem permissão, janela de operação fechada).

- `InvalidState` → 409 Conflict
  - Quando: conflito de estado ou transição inválida (ex.: pedido já concluído, recurso bloqueado).

- `NotFound` → 404 Not Found
  - Quando: recurso não existe (ex.: ID inexistente, filtro não encontrou registro).

- `InternalServerError` → 500 Internal Server Error
  - Quando: erro inesperado no servidor (exceptions não tratadas, falha de infraestrutura). Não use para erros esperados de domínio.

- `CustomProblem` → definido pela descrição (ProblemDetails) do seu tipo
  - Quando: erro específico de domínio que não se encaixa nas categorias padrão; requer `typeId` e descrição via `ProblemDetailsOptions`.

Separação de Custom e Exception:
- Custom: use `Problems.Custom(detail, typeId, property)` para erros de domínio descritos pela sua API.
- Exception: use `Problems.InternalError(exception)` para exceptions inesperadas; configure `ExceptionOptions` e `ExceptionHandler` se necessário.

Exemplos por categoria:

```csharp
// 400 Bad Request – entrada inválida
var p400 = Problems.InvalidParameter("Name is required", "name");
var p400Range = Problems.InvalidParameter("Age must be greater than 18", "age");

// 422 Unprocessable Entity – regra de negócio violada
var p422 = Problems.ValidationFailed("Order total cannot be negative", "total");
var p422Combo = Problems.ValidationFailed("Payment method not compatible with plan", "paymentMethod");

// 403 Forbidden – não permitido
var p403 = Problems.NotAllowed("You do not have permission to cancel this order");
var p403Policy = Problems.NotAllowed("Action not allowed during maintenance window");

// 409 Conflict – estado inválido
var p409 = Problems.InvalidState("Order is already shipped");
var p409Lock = Problems.InvalidState("Resource is locked by another process");

// 404 Not Found – recurso inexistente
var p404 = Problems.NotFound("User not found", "userId");
var p404Filter = Problems.NotFound("No results for filter", "query");

// 500 Internal Server Error – erro inesperado
var p500 = Problems.InternalError(new Exception("Unexpected error"));
var p500Default = Problems.InternalError(); // usa mensagem padrão configurada

// Custom – descreva seu tipo em ProblemDetails
var pCustom = Problems.Custom("Order on hold", typeId: "order-on-hold", property: "status");
```

⚠️ **`InternalError` inverte a ordem de `property` e `typeId`** em relação às demais fábricas.
Não é um erro de compilação — é um erro silencioso que altera o `type` do `ProblemDetails`:

```csharp
// Demais fábricas: (detail, property, typeId)
Problems.InvalidParameter(string detail, string? property = null, string? typeId = null);
Problems.ValidationFailed(string detail, string? property = null, string? typeId = null);
Problems.NotAllowed      (string detail, string? property = null, string? typeId = null);
Problems.InvalidState    (string detail, string? property = null, string? typeId = null);
Problems.NotFound        (string detail, string? property = null, string? typeId = null);

// InternalError: (detail, typeId, property)  <-- invertido!
Problems.InternalError   (string? detail, string? typeId = null, string? property = null);

// Custom exige typeId, na segunda posição
Problems.Custom          (string detail, string typeId, string? property = null);
```

```csharp
// ❌ define typeId = "userId" sem querer
Problems.InternalError("Falha ao gravar", "userId");

// ✅ sempre use argumento nomeado em InternalError
Problems.InternalError("Falha ao gravar", property: "userId");
```

Extensões e propriedades encadeadas:

```csharp
p400.With("attempt", 1).ChainProperty("User", 0); // User[0].name
p422.With("policy", "minimum-total");
```

### Convertendo Problems para exceção

Quando for necessário atravessar uma fronteira que ainda espera exceptions, converta a coleção de problemas em exceção com `ToException(...)`:

```csharp
var ex = (p400 + p422).ToException("Validation errors: {0}");
throw ex;
```

### Validando classes de forma padronizada

Para validar as propriedades de uma classe pode ser criado um método HasProblems que retorna os problemas encontrados:

```csharp
public class User
{
    public string Name { get; set; }

    public int Age { get; set; }

    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        Problems errors = [];

        if (string.IsNullOrWhiteSpace(Name))
            errors += Problems.InvalidParameter("Name is required", "name");

        if (Age < 18)
            errors += Problems.InvalidParameter("Age must be at least 18", "age");

        if (errors.Count > 0)
        {
            problems = errors;
            return true;
        }

        problems = null;
        return false;
    }
}
```

DICA: Use a biblioteca RoyalCode.SmartValidation para validação fluente, com RuleSet, e integrada com Problems e Results.


### Custom, TypeId e RFC 9457 (type)

Em `ProblemDetails` (RFC 9457), o campo `type` deve ser um identificador do tipo de problema (preferencialmente uma URI).
Recomendações atualizadas do RFC 9457:
- Use um `type` estável, único e documentado, de preferência uma URI absoluta (ex.: `https://api.seu-dominio.com/problems/order-on-hold`).
- Inclua `title` humano-legível e `status` coerente ao tipo descrito. Evite títulos genéricos.
- Utilize `instance` (URI) para identificar a ocorrência específica do problema quando aplicável.
- As extensões devem usar nomes claros e estáveis; evite sobrescrever campos reservados (`type`, `title`, `status`, `detail`, `instance`).
- Evite `about:blank` para problemas customizados; descreva tipos próprios com documentação.

Impacto do `Problems.Custom(detail, typeId, property)` na conversão:
- O `typeId` do `Problem` é usado para localizar uma `ProblemDetailsDescription` em `ProblemDetailsOptions.Descriptor`.
- Se a descrição tem `Type` explícito, esse valor vira `ProblemDetails.Type`.
- Se a descrição não tem `Type`, a URI é gerada por `BaseAddress + TypeComplement + TypeId`.
- `ProblemDetails.Title` e `ProblemDetails.Status` vêm da descrição localizada.
- `ProblemDetails.Detail` vem do `detail` do problema ocorrido; já `ProblemDetailsDescription.Description` serve para documentação/catálogo do tipo.
- Sem descrição específica para o `typeId`, `CustomProblem` cai no tipo genérico `problem-occurred`, com status padrão 400. Para contrato de API estável, sempre registre uma descrição para cada `typeId` customizado.
- Quando há vários problemas customizados na mesma resposta, o tipo externo é agregado (`aggregate-problems-details`) e os problemas específicos aparecem nos detalhes agregados.

Exemplo de configuração do tipo no `ProblemDetailsOptions`:
```csharp
using System.Net;
using RoyalCode.SmartProblems.Descriptions;

var options = new ProblemDetailsOptions();
options.Descriptor.Add(new ProblemDetailsDescription(
    typeId: "order-on-hold",
    title: "Order on hold",
    description: "The order cannot move forward while risk analysis is pending.",
    status: HttpStatusCode.Conflict));

Problems problem = Problems.Custom("Order is on hold due to risk analysis", "order-on-hold");
var pd = problem.ToProblemDetails(options);
// Type: "tag:problemdetails/.problems#order-on-hold"
// Title: "Order on hold"
// Status: 409
// Detail: "Order is on hold due to risk analysis"
```

Quando o contrato público exige uma URI absoluta, informe o `type` explicitamente:
```csharp
options.Descriptor.Add(new ProblemDetailsDescription(
    typeId: "order-on-hold",
    type: "https://api.exemplo.com/problems/order-on-hold",
    title: "Order on hold",
    description: "The order cannot move forward while risk analysis is pending.",
    status: HttpStatusCode.Conflict));
```

### Catálogo e página de descrição dos problemas

Use o pacote `RoyalCode.SmartProblems.ProblemDetails`. Registre as descrições conhecidas pela aplicação com `AddProblemDetailsDescriptions`; elas alimentam a conversão para `ProblemDetails` e a página HTML de documentação.

```csharp
using System.Net;
using RoyalCode.SmartProblems.Descriptions;

builder.Services.AddProblemDetailsDescriptions(options =>
{
    options.BaseAddress = "https://api.exemplo.com/problems";
    options.TypeComplement = "/";

    options.Descriptor.Add(new ProblemDetailsDescription(
        typeId: "order-on-hold",
        title: "Order on hold",
        description: "The order cannot move forward while risk analysis is pending.",
        status: HttpStatusCode.Conflict));
});
```

Também é possível carregar descrições por arquivo JSON:

```csharp
builder.Services.AddProblemDetailsDescriptions(options =>
{
    options.DescriptionFiles = ["problem-details.json"];
});
```

Formato recomendado do arquivo:
```json
[
  {
    "typeId": "order-on-hold",
    "type": "https://api.exemplo.com/problems/order-on-hold",
    "title": "Order on hold",
    "description": "The order cannot move forward while risk analysis is pending.",
    "status": 409
  }
]
```

Depois de registrar as descrições, publique a página de catálogo:

```csharp
var app = builder.Build();

app.MapProblemDetailsDescriptionPage(); // GET /.problems
// ou:
app.MapProblemDetailsDescriptionPage("/docs/problems");
```

A página é opt-in e lista os tipos conhecidos pelo `ProblemDetailsDescriptor`: categorias padrão, descrições carregadas em JSON e descrições adicionadas em código. Ela mostra `TypeId`, URI final do `type`, título, status e descrição. Se uma descrição não tiver `Type` explícito, a página resolve a URI com `BaseAddress + TypeComplement + TypeId`; quando `BaseAddress` estiver no default da biblioteca, a própria rota da página é usada como base navegável.

Regras para IA ao criar problemas customizados:
- Use `typeId` curto, estável e válido como parte relativa de URI, preferencialmente em kebab-case (`order-on-hold`, `payment-required`).
- Registre uma `ProblemDetailsDescription` para cada `typeId` customizado antes de expor a API.
- Use `type` explícito quando a documentação pública mora em uma URL canônica; use `BaseAddress`/`TypeComplement` quando a própria API publica o catálogo.
- Escreva `description` como documentação do tipo: quando ocorre, por que ocorre e o que o consumidor pode fazer.
- Não dependa do fallback `problem-occurred` para erros de domínio públicos.

## 4. Exemplos de uso: Result

Construção e verificação:
```csharp
Result<string> ok = "Hello";
Result<string> fail = Problems.InvalidParameter("Invalid", "prop");

if (ok.HasValue(out var value)) { /* sucesso */ }
if (fail.HasProblems(out var errs)) { /* erros */ }
```

Composição síncrona e assíncrona:
```csharp
var res = ok.Map(v => v.Length);           // Result<int>
var next = ok.Continue(v => Result.Ok());  // Result

var async = await ok.MapAsync(static v => Task.FromResult(v.Length));

// Async com TParam: param, delegate, ct por último.
var saved = await ok.ContinueAsync(
    repository,
    static async (value, repo, token) =>
    {
        await repo.SaveAsync(value, token);
    },
    ct);

// branch explícito: Match
var outRes = ok.Match(
    value => Result.Ok(),
    problems => problems.AsResult());

// branch assíncrono: MatchAsync
var outResAsync = await ok.MatchAsync(
    value => Task.FromResult(Result.Ok()),
    problems => Task.FromResult(problems.AsResult()));
```

Regra para sobrecargas `Async` com `TParam`:
- Quando o delegate retorna `Task` ou `Task<T>`, ele recebe `CancellationToken` como último parâmetro.
- O `CancellationToken` público do método fica por último e tem default: `.MapAsync(param, static (..., token) => ..., ct)`.
- Não use a forma antiga `.MapAsync(param, ct, delegate)` ou `.ContinueAsync(param, ct, delegate)`.
- Delegates síncronos com `TParam` continuam sem `CancellationToken`.

Exemplo de `MatchAsync` com `TParam` e `CancellationToken`:
```csharp
return await result.MatchAsync(
    logger,
    static (value, log, token) =>
    {
        token.ThrowIfCancellationRequested();
        log.LogInformation("Operation succeeded");
        return Task.FromResult(value);
    },
    static (problems, log, token) =>
    {
        token.ThrowIfCancellationRequested();
        log.LogWarning("Operation failed with {Count} problems", problems.Count);
        return Task.FromResult(string.Empty);
    },
    ct);
```

Casos de uso reais (serviços, handlers, repositórios):
```csharp
// Result sem valor
public readonly struct UserService
{
    private readonly IUserRepository _repo;
    private readonly AbstractValidator<UserInput> _validator; // EnsureIsValid é extensão de AbstractValidator<T>
    private readonly IUserPolicy _policy;

    public Result Create(UserInput input)
    {
        // validação de entrada
        // Atenção: cada `out var` precisa de um nome único no mesmo escopo (CS0128).
        if (input.HasProblems(out var inputProblems))
            return inputProblems; // 400.

        // regra de negócio
        if (_validator.EnsureIsValid(input).HasProblems(out var validationProblems))
            return validationProblems; // 400/422 etc.

        // regra de negócio
        if (!_policy.CanCreate(input))
            return Problems.NotAllowed("Not allowed to create user");

        // persistência
        _repo.Add(input);
        return Result.Ok();
    }

    public async Task<Result> DisableAsync(int id)
    {
        var findUser = await _repo.FindByIdAsync(id);
        if (findUser.NotFound(out var problem))
            return problem; // 404
        
        findUser.Entity.Disable();
        
        return Result.Ok();
    }
}

// Result com valor
public readonly struct OrderService
{
    private readonly IOrderRepository _repo;

    public Result<Order> Get(int id)
    {
        var found = _repo.TryFind(id); // retorna FindResult<Order,int>
        return found.ToResult();
    }
}
```

Por que `Result` favorece um ótimo tratamento de erros?
- Substitui exceções em fluxo esperado por um tipo explícito de sucesso/falha, tornando o controle de fluxo transparente.
- Padroniza mensagens e categorias via `Problems`, permitindo conversão consistente para `ProblemDetails` em APIs.
- Facilita composição funcional (Map, Continue, Match), reduzindo boilerplate e melhorando legibilidade.
- Integra com validação (`FluentValidation`) e persistência (EF `FindResult`).

Performance: `Result` é um `readonly struct`
- Structs evitam alocação de heap em cenários comuns e permitem passagem por valor eficiente.
- `readonly` garante imutabilidade e melhor otimização pelo JIT.
- Métodos marcados com `AggressiveInlining` reduzem overhead em chamadas frequentes.
- Em pipelines síncronos/assíncronos curtos, reduz GC pressure em comparação com exceções.

Agregação de problemas:

```csharp
Result<string> r1 = Problems.InvalidParameter("A");
Result r2 = Problems.InvalidParameter("B");
r1 += r2; // combina problemas
```

## 5. Exemplos para Entidades (Id, FindResult, TryFindAsync)

A extensão de Entity Framework fornece métodos `TryFindAsync`, `TryFindByAsync` e `FindByCriteria` que retornam um `FindResult<TEntity>`.
Esse tipo encapsula o resultado da busca: a entidade encontrada (`Entity`) ou um problema padronizado quando não encontrada.

- `TryFindAsync(DbContext, Id<TEntity,TId>)`, `TryFindAsync(DbSet<TEntity>, TId)` e `TryFindAsync(DbSet<TEntity>, Id<TEntity,TId>)`:
  - Quando a entidade não existe, gera um `Problem` com categoria `NotFound` (HTTP 404) e mensagem bem definida.
  - Ao receber `Id<TEntity,TId>`, o valor usado na busca e no problema é `id.Value`, não o wrapper `Id`.
  - Campos extras adicionados em `Extensions`: `id` e `entity`.

- `TryFindByAsync(DbContext/DbSet, Expression<Func<TEntity,bool>>)`:
  - Executa o filtro com `FirstOrDefaultAsync`.
  - Se não encontrar, tenta gerar uma mensagem rica analisando o predicado.
  - Só gera critérios automáticos para expressões `&&` compostas por igualdades (`==`) em que um lado é membro direto da entidade (`e => e.Name`) e o outro lado é valor constante/capturado.
  - Qualquer expressão ambígua ou potencialmente enganosa degrada para a mensagem genérica `The record for 'Entity' was not found`.

- `FindByCriteria()`:
  - Use quando a busca tem dois ou mais critérios, chave composta, filtros condicionais, ou quando você quer informar os valores diretamente sem depender da análise automática da expressão.
  - Pode começar em `DbContext`, `DbSet<TEntity>` ou `IQueryable<TEntity>` já customizado com `Include`, `AsNoTracking` etc.
  - Cada chamada `By` retorna uma nova instância; para montar filtros condicionais, reatribua a variável.

Uso típico por id:
```csharp
Id<TestEntity, int> id = 4;
var entry = await db.TestEntities.TryFindAsync(id, ct);

if (entry.NotFound(out var problem))
{
    // problem.Detail: "The record of 'The Entity for Tests' with id '4' was not found"
    // problem.Extensions: { id: 4, entity: "TestEntity" }
    return problem;
}
```

Uso por propriedade simples:
```csharp
var byName = await db.TestEntities.TryFindByAsync(e => e.Name == "Test4", ct);

if (byName.NotFound(out var problemByName))
{
    // problemByName.Detail: "The record of 'The Entity for Tests' with Name 'Test4' was not found"
    // problemByName.Extensions: { Name: "Test4", entity: "TestEntity" }
    return problemByName;
}
```

Busca composta recomendada com `FindByCriteria`:
```csharp
var city = await db.FindByCriteria<City>()
    .By(c => c.StateId, stateId)
    .By(c => c.Name, name)
    .TryFindAsync(ct);

if (city.NotFound(out var problem))
{
    // Detail: "The record of 'City' with StateId '42', Name 'Blumenau' was not found"
    // Extensions: { entity: "City", StateId: 42, Name: "Blumenau" }
    return problem;
}
```

Começando a partir de um `IQueryable` customizado:
```csharp
var city = await db.Set<City>()
    .AsNoTracking()
    .Include(c => c.State)
    .FindByCriteria()
    .By(c => c.StateId, stateId, "State")
    .By(c => c.Name, name)
    .TryFindAsync(ct);
```

Filtros condicionais: como `FindCriteria<TEntity>` é imutável, sempre reatribua:
```csharp
var criteria = db.FindByCriteria<City>()
    .By(c => c.StateId, stateId);

if (!string.IsNullOrWhiteSpace(name))
    criteria = criteria.By(c => c.Name, name);

var city = await criteria.TryFindAsync(ct);
```

Quando um critério precisa de lógica além de igualdade (`StartsWith`, range, `OR`), use a sobrecarga com predicado e dados explícitos para o problema:
```csharp
var city = await db.FindByCriteria<City>()
    .By(c => c.StateId, stateId)
    .By(c => c.Name.StartsWith(prefix), byName: "Name", propertyName: "Name", value: prefix)
    .TryFindAsync(ct);
```

Também é possível criar um problema multi-critério diretamente com `FindCriterion`:
```csharp
FindCriterion[] criteria =
[
    new("StateId", stateId, "State"),
    new("Name", name)
];

FindResult<City> notFound = FindResult<City>.Problem(criteria);
```

Regras de `FindCriterion` e `FindResult<TEntity>.Problem(criteria)`:
- Critérios são listados na ordem recebida.
- Com um único critério, a mensagem mantém o formato legado de `Problem(byName, propertyName, value)`.
- Com múltiplos critérios, o detalhe lista todos: `State '42', Name 'Blumenau'`.
- `ByName` nulo ou em branco é resolvido por `DisplayNames`, respeitando `DisplayNameAttribute`.
- `FindCriterion` rejeita `propertyName` nulo/vazio; `default(FindCriterion)` é ignorado pela fábrica multi-critério.
- Se todos os critérios forem inválidos/ignorados, a mensagem cai para o `NotFound` genérico.
- Em `Extensions`, se a mesma `propertyName` aparecer mais de uma vez, a última vence.

Sobre a análise automática de `TryFindByAsync(predicate)`:
```csharp
// Gera mensagem rica multi-critério:
await db.TryFindByAsync<City>(c => c.StateId == stateId && c.Name == name, ct);

// Também funciona com a igualdade invertida:
await db.TryFindByAsync<City>(c => name == c.Name, ct);

// Degrada para mensagem genérica: "!=" não afirma que a entidade tem aquele valor.
await db.TryFindByAsync<City>(c => c.Name != "Blumenau", ct);

// Degrada para genérica: OR não pode ser descrito como lista simples de critérios AND.
await db.TryFindByAsync<City>(c => c.Name == "A" || c.Name == "B", ct);

// Degrada para genérica: cadeia profunda ou comparação entre membros da entidade.
await db.TryFindByAsync<City>(c => c.State.Name == "SC", ct);
await db.TryFindByAsync<State>(s => s.Name == s.Code, ct);
```

A análise do valor nunca compila a expressão nem invoca métodos do predicado, então `track("x")` não é chamado uma segunda vez apenas para montar a mensagem.
Ela pode, porém, ler novamente getters de objetos capturados, como `request.Name`, para obter o valor usado no detalhe. Se o getter tiver efeito colateral ou valor variável, prefira `FindByCriteria().By(c => c.Name, request.Name)` e capture o valor uma vez antes da busca.

Quando possui nomes customizados em uma chamada direta de `TryFindByAsync`, use a sobrecarga explícita:
```csharp
var entry2 = await db.TryFindByAsync<TestEntity>(
    e => e.Name == "Test4",
    byName: "Name",
    propertyName: "name",
    propertyValue: "Test4",
    ct);

if (entry2.NotFound(out var p))
{
    // p.Extensions: { name: "Test4", entity: "TestEntity" }
}
```

Persistência com helpers EF:

- `AddTo`/`AddToAsync` adiciona a entidade ao `DbContext` somente quando o `Result<TEntity>` tem valor.
- `SaveChanges`/`SaveChangesAsync` chama `DbContext.SaveChanges` somente quando o `Result` está em sucesso.
- `RemoveFromAsync` existe para `Task<FindResult<TEntity>>`; remove somente quando a entidade foi encontrada e retorna `Result<TEntity>`.
- Para `Result<TEntity>` já materializado, prefira `AddTo(db)` quando quiser encadear imediatamente com `SaveChangesAsync`.

Criação:
```csharp
return await Product.Create(command)
    .AddTo(db)
    .SaveChangesAsync(db, ct);
```

Criação quando a etapa anterior já é assíncrona:
```csharp
return await CreateProductAsync(command, ct)
    .AddToAsync(db, ct)
    .SaveChangesAsync(db, ct);
```

Remoção:
```csharp
return await db.Products
    .TryFindByAsync(p => p.Id == id, ct)
    .RemoveFromAsync(db, ct)
    .SaveChangesAsync(db, ct);
```

Composição com `FindResult`:
```csharp
var result = await entry.ContinueAsync(
    repository,
    static async (entity, repo, token) =>
    {
        await repo.SaveAsync(entity, token);
        return Result.Ok();
    },
    ct);
```

Com `CollectAsync<TParam>` o mesmo padrão vale: `param`, delegate, `ct`.

```csharp
var result = await entry.CollectAsync(
    dto,
    static async (entity, request, token) =>
    {
        entity.Update(request.Name);
        await Task.CompletedTask.WaitAsync(token);
    },
    ct);
```

Não use a ordem antiga `.CollectAsync(dto, ct, static ...)`; o `CancellationToken` do método deve ficar por último.

Além de `NotFound`, o `FindResult` também suporta retornar `InvalidParameter` em cenários onde o identificador/parâmetro informado é inválido para a operação atual.
Os métodos `HasInvalidParameter(out problem, parameterName)` e sobrecargas de `Continue/Map/ToResult(parameterName)` ajudam a padronizar essa resposta:
```csharp
var res = entry.ToResult("id");
// Se o parâmetro "id" for inválido, retorna Problem InvalidParameter com detail e property padronizados.
```

## 6. Resultados de API (OkMatch, NoContentMatch, CreatedMatch)

Os tipos `OkMatch`, `NoContentMatch` e `CreatedMatch` permitem mapear `Result`/`Result<T>` para respostas HTTP padronizadas, convertendo automaticamente problemas em `ProblemDetails` (RFC 9457) quando necessário.

Exemplos baseados em `MatchApi`:

```csharp
// POST: cria e retorna 201 com Location e corpo
private static async Task<CreatedMatch<PersonDetails>> CreatePerson(PersonCreate create)
{
    await Task.Delay(10); // simulação

    return _personService.CreatePerson(create)
        .Map(person => new PersonDetails
        {
            Id = person.Id,
            Name = person.Name,
            Age = person.Age
        })
        .CreatedMatch(p => $"/api/match/{p.Id}");
}

// GET: retorna 200 com corpo ou 404 ProblemDetails
private static async Task<OkMatch<PersonDetails>> GetPerson(int id)
{
    await Task.Delay(10);

    return _personService.GetPerson(id)
        .Map(person => new PersonDetails
        {
            Id = person.Id,
            Name = person.Name,
            Age = person.Age
        });
}

// PATCH: retorna 200 OK ou ProblemDetails (400/404)
private static async Task<OkMatch> UpdatePersonName(int id, PersonUpdateName model)
{
    await Task.Delay(10);
    return _personService.UpdatePersonName(id, model);
}

// PATCH: retorna 200 OK ou ProblemDetails (400/404)
private static async Task<OkMatch> UpdatePersonAge(int id, PersonUpdateAge model)
{
    await Task.Delay(10);
    return _personService.UpdatePersonAge(id, model);
}

// DELETE: retorna 204 ou 404 ProblemDetails
private static async Task<NoContentMatch> DeletePerson(int id)
{
    await Task.Delay(10);
    return _personService.DeletePerson(id);
}
```

Comportamento esperado (vide `MatchApiTests`):
- Sucesso: 201/200/204 com Location e/ou corpo conforme tipo.
- Falha: problemas convertidos para `ProblemDetails` com status coerente (404, 400, etc.).

### Filtro de exceções para Minimal API

Use `WithExceptionFilter` na borda HTTP para transformar exceções inesperadas em `ProblemDetails` 500 padronizado. Prefira aplicar o filtro em grupos, para que todas as rotas do grupo compartilhem a mesma política:

```csharp
var group = app.MapGroup("/api")
    .WithExceptionFilter();

group.MapGet("/produto/{id:int}", GetProduto);
group.MapPost("/produto", CriarProduto);
```

Quando quiser registrar também falhas esperadas retornadas por `OkMatch`, `CreatedMatch` ou `NoContentMatch`, informe um `LogLevel`. O parâmetro `loggerType` define a categoria do logger usado pelo filtro:

```csharp
var group = app.MapGroup("/api")
    .WithExceptionFilter(LogLevel.Error, typeof(Program));

group.MapGet("/produto/{id:int}", GetProduto);
```

Regras para IA ao gerar Minimal APIs:
- Use `WithExceptionFilter()` em `MapGroup` quando várias rotas compartilham a mesma borda de API.
- Use o filtro para exceptions inesperadas, falhas de infraestrutura e erros não previstos.
- Não use `try/catch` nem exception filter para validação esperada, regra de domínio ou recurso não encontrado; retorne `Result`/`Problems` e converta com `OkMatch`, `CreatedMatch` ou `NoContentMatch`.
- O filtro sempre registra exceptions capturadas como `LogLevel.Error`.
- O parâmetro `logLevel` controla apenas o log de respostas de erro já modeladas como `MatchErrorResult`.

Boas práticas (RFC 9457):
- Para `CreatedMatch`, forneça `Location` com URI absoluta ou relativa estável.
- Títulos (`title`) claros e condizentes com o `type`; descrição (`detail`) objetiva.
- Use `instance` quando aplicável para identificar o recurso/ocorrência.

## 7. Cliente HTTP: ToResultAsync

Métodos `HttpResultExtensions.ToResultAsync` desserializam respostas HTTP em `Result`/`Result<T>`:
- Em sucesso (2xx): retornam `Result.Ok()` ou `Result<T>` com o corpo JSON.
- Em falha (4xx/5xx): lê `application/problem+json` e converte para `Problems`; se não for ProblemDetails, tenta texto puro ou leitor customizado.

Assinaturas principais:
```csharp
Task<Result> ToResultAsync(this HttpResponseMessage response, CancellationToken ct = default);
Task<Result<T>> ToResultAsync<T>(this HttpResponseMessage response, JsonSerializerOptions? options = null, CancellationToken ct = default);
Task<Result<T>> ToResultAsync<T>(this HttpResponseMessage response, JsonTypeInfo<T> jsonTypeInfo, CancellationToken ct = default);
// Com FailureTypeReader para conteúdo de erro não-ProblemDetails
Task<Result<T>> ToResultAsync<T>(this HttpResponseMessage response, FailureTypeReader? failureTypeReader, JsonSerializerOptions? options = null, CancellationToken ct = default);
Task<Result<T>> ToResultAsync<T>(this HttpResponseMessage response, FailureTypeReader? failureTypeReader, JsonTypeInfo<T> jsonTypeInfo, CancellationToken ct = default);
```

Exemplos reais de consumo com `HttpClient`:

```csharp

var http = new HttpClient { BaseAddress = new Uri("https://api.exemplo.com") };

// 1) GET com corpo: sucesso → Result<T>, falha → Problems
var respGet = await http.GetAsync("/users/123");
var userResult = await respGet.ToResultAsync<UserDto>();
if (userResult.HasValue(out var user))
{
    Console.WriteLine($"User: {user.Name}");
}
else if (userResult.HasProblems(out var problems))
{
    // exibir problem details
    foreach (var p in problems) Console.WriteLine($"{p.Category}: {p.Detail}");
}

// 2) POST criação: sucesso (201) sem corpo → Result.Ok(), Location em headers
var createResp = await http.PostAsJsonAsync("/users", new { name = "John", age = 20 });
var createResult = await createResp.ToResultAsync();
if (createResult.IsSuccess)
{
    if (createResp.Headers.Location is Uri loc)
        Console.WriteLine($"Criado em: {loc}");
}
else if (createResult.HasProblems(out var problems))
{
    // entrada inválida (400) ou regra semântica (422)
    foreach (var p in problems) Console.WriteLine($"Erro: {p.Property} → {p.Detail}");
}

// 3) PATCH atualização: sucesso (200) sem corpo, falha padronizada
var patchResp = await http.PatchAsJsonAsync("/users/123/name", new { name = "Mary" });
var patchResult = await patchResp.ToResultAsync();
if (!patchResult.IsSuccess && patchResult.HasProblems(out var errs))
{
    // erros como NotFound(404) ou InvalidParameter(400)
    foreach (var p in errs) Console.WriteLine($"{p.Category}: {p.Detail}");
}

// 4) GET lista com `JsonTypeInfo` otimizado
var respList = await http.GetAsync("/users");
var listResult = await respList.ToResultAsync(UsersContext.Default.ListUserDto);
if (listResult.HasValue(out var users))
{
    Console.WriteLine($"Total: {users.Count}");
}

// 5) Falha com conteúdo não-ProblemDetails usando FailureTypeReader
var reader = new FailureTypeReader(async r =>
{
    var text = await r.Content.ReadAsStringAsync();
    return new FailureTypeReaderResult(true, Problems.InternalError(text));
});
var respOther = await http.GetAsync("/external/service");
var otherResult = await respOther.ToResultAsync(reader);
if (otherResult.HasProblems(out var ps))
{
    foreach (var p in ps) Console.WriteLine(p.Detail);
}
```

Boas práticas (RFC 9457):
- APIs devem retornar `application/problem+json` para falhas; clientes devem interpretar `type`, `title`, `status`, `detail`, `instance`.
- Use `type`/`instance` URIs estáveis; evite conflitar extensões com campos reservados.

## 8. Erros comuns (❌ / ✅)

Armadilhas que o IntelliSense não revela. Todas verificadas contra `1.0.0-preview-7.0`.

### 8.1 `TryFindAsync` tem duas semânticas sob o mesmo nome

`TryFindAsync(id)` usa `FindAsync` do EF: **consulta o change tracker primeiro** e, se a entidade já
estiver rastreada, retorna sem emitir SQL. `TryFindByAsync(predicado)` e `FindByCriteria()...TryFindAsync(ct)`
usam `FirstOrDefaultAsync`: **sempre** emitem SQL, e alterações ainda não salvas não afetam o filtro.

```csharp
// ✅ chave primária: pode resolver pelo change tracker, sem ida ao banco
var byId = await db.Set<City>().TryFindAsync(cityId, ct);

// ⚠️ sempre emite SQL; não enxerga entidades adicionadas/alteradas e ainda não salvas
var byName = await db.FindByCriteria<City>().By(c => c.Name, "Nova").TryFindAsync(ct);
```

Regra: busca por chave primária → `TryFindAsync(id)`. Busca por outros campos → `TryFindByAsync` /
`FindByCriteria`, assumindo roundtrip.

### 8.2 `By` exige membro direto da entidade

O seletor precisa acessar uma propriedade **do parâmetro da lambda**. Qualquer outra coisa lança
`ArgumentException` em tempo de execução.

```csharp
// ❌ ArgumentException: não é membro do parâmetro `c`
criteria.By(c => outroObjeto.Nome, valor);

// ❌ ArgumentException: cadeia profunda (o display name seria resolvido no tipo errado)
criteria.By(c => c.State.Name, "SC");

// ✅ membro direto
criteria.By(c => c.Name, valor);

// ✅ para navegar ou usar lógica além de igualdade, use a sobrecarga de predicado
criteria.By(c => c.State.Name == "SC", byName: "State", propertyName: "stateName", value: "SC");
```

### 8.3 `Id<TEntity,TId>` não entra direto em `By`

Cuidado: isto **compila**. Como existe conversão implícita de `int` para `Id<State,int>`, o compilador
infere `TValue = Id<State,int>` e insere um `Convert` no seletor. O erro só aparece em tempo de execução,
como `ArgumentException` do próprio builder.

```csharp
Id<State, int> stateId = 42;

// ❌ compila, mas lança ArgumentException em tempo de execução:
//    "Cannot filter 'StateId' (of type Int32) by an Id<,> wrapper. Pass the underlying value instead..."
db.FindByCriteria<City>().By(c => c.StateId, stateId);

// ✅ use o valor
db.FindByCriteria<City>().By(c => c.StateId, stateId.Value);
```

Em `TryFindAsync`, ao contrário, o wrapper é aceito nas três sobrecargas e o `id.Value` é usado
internamente — tanto em `db.TryFindAsync(id, ct)` quanto em `db.Set<T>().TryFindAsync(id, ct)`.

### 8.4 `default(FindCriteria<T>)` e `default(Result<T>)`

`FindCriteria<T>` é struct e detecta o uso não inicializado, lançando `InvalidOperationException`
com mensagem explicativa em `By` e em `TryFindAsync`. Sempre comece por `FindByCriteria(...)`.

`Result<T>` **não** tem essa guarda: o `default` se comporta como **sucesso com valor nulo**.

```csharp
// ❌ IsSuccess == true e HasValue devolve true com value == null
Result<Order> r = default;
if (r.HasValue(out var order)) { order.Total(); /* NullReferenceException */ }

// ✅ construa explicitamente
Result<Order> ok = order;
Result<Order> fail = Problems.NotFound("Order not found", "orderId");
```

Nunca declare `Result<T>` sem inicializar, nem use `new Result<T>()` sem argumentos.

### 8.5 Acessando o valor: `HasValue`, `HasProblemsOrGetValue` e `EnsureHasValue`

Além de `HasProblems`/`HasValue`, existem duas formas que evitam checagem dupla — e uma que **lança
exceção**, contrariando a filosofia da biblioteca se usada no fluxo esperado.

```csharp
// ✅ um único teste, devolve problemas OU valor
if (result.HasProblemsOrGetValue(out var problems, out var order))
    return problems;
// aqui `order` não é nulo

// ✅ variação com a ordem invertida
if (result.HasValueOrGetProblems(out var value, out var errors)) { /* sucesso */ }

// ⚠️ EnsureHasValue LANÇA InvalidOperationException se houver problemas.
// Use apenas quando a falha já foi tratada antes e é logicamente impossível aqui.
result.EnsureHasValue(out var entity);

// ❌ EnsureHasValue não protege contra o `default`: ele só lança quando IsFailure é true.
// Em default(Result<T>) não há problemas, então `bad` volta nulo silenciosamente (ver §8.4).
Result<Order> o = default;
o.EnsureHasValue(out var bad); // bad == null, sem exceção
```

### 8.6 Ordem de parâmetros de `Problems.InternalError`

Ver §3: `InternalError` é `(detail, typeId, property)`, invertido em relação às demais fábricas.
Sempre passe `property:` nomeado.

### 8.7 `out var` repetido no mesmo escopo

`HasProblems(out var problems)` duas vezes no mesmo método é erro de compilação (CS0128).
Dê nomes distintos: `inputProblems`, `validationProblems`.

## 9. Boas Práticas

- Padronize categorias e status HTTP:
  - 404 NotFound, 400 InvalidParameter (entrada), 422 ValidationFailed (semântica), 403 NotAllowed, 409 InvalidState, 500 Internal.
  - Defina tipos customizados com `Problems.Custom` e descreva em `ProblemDetailsOptions`.
- Siga o RFC 9457:
  - Prefira URIs absolutas para `type` e `instance`, títulos claros (`title`) e `status` coerente.
  - Não sobrescreva campos reservados; use `Extensions` com nomes estáveis e significativos.
- Use `Result`/`Result<T>` como fluxo de sucesso/falha:
  - Componha com `Map`, `Continue`, `Match`/`MatchAsync` para reduzir boilerplate.
  - Em sobrecargas `Async` com `TParam` e delegate `Task`, passe `param`, depois o delegate, e `CancellationToken` por último.
  - Evite exceções para casos esperados; retorne problemas nas falhas.
- Valide entrada e regras de domínio:
  - Modelo com `HasProblems(out Problems?)` ou FluentValidation (`EnsureIsValid`, `ToProblems`).
  - Em APIs, converta problemas para `ProblemDetails` automaticamente via `OkMatch`/`CreatedMatch`/`NoContentMatch`.
- Documente problemas expostos pela API:
  - Registre descrições com `AddProblemDetailsDescriptions`, em código ou JSON.
  - Publique `MapProblemDetailsDescriptionPage()` quando consumidores precisarem consultar o catálogo de tipos.
  - Para `Problems.Custom`, garanta que todo `typeId` público tenha `title`, `description`, `status` e URI de `type` estáveis.
- Persistência e buscas:
  - Use `TryFindAsync`/`TryFindByAsync` (EF) e trate `FindResult` com `NotFound`/`HasInvalidParameter`/`ToResult([param])`.
  - Para buscas com dois ou mais campos, chave composta ou filtros condicionais, prefira `FindByCriteria().By(...).TryFindAsync(ct)`.
  - Para mensagens `NotFound` multi-critério fora do EF, use `FindCriterion` e `FindResult<TEntity>.Problem(criteria)`.
  - Propague campos extras (`id`, `entity`, `property/value`) em `Extensions` para rastreabilidade.
- Cliente HTTP:
  - Consuma com `ToResultAsync` (valor ou problemas) e trate `application/problem+json` corretamente.
  - Para erros não-ProblemDetails, considere `FailureTypeReader`.
- Observabilidade e contexto:
  - Use `With(key, value)` para anexar dados relevantes (ids, política aplicada, limites).
  - Encadeie propriedades com `ChainProperty(parent[, index])` para apontar origem precisa.
- Performance:
  - `Result` é `readonly struct`; aproveite composição leve e evite alocações desnecessárias.
- Assinaturas:
  - Consulte a documentação XML das libs no IDE/pacote para confirmar overloads, nomes de parâmetros e tipos de retorno antes de gerar código em APIs menos usadas.

## Resumo

SmartProblems fornece uma abordagem uniforme e eficiente para tratar sucesso e falha em operações .NET.
Com `Problem`/`Problems` você modela erros com categorias e contexto; com `Result`/`Result<T>` você compõe fluxos sem lançar exceções em casos esperados.
A biblioteca integra-se a APIs via `ProblemDetails` (RFC 9457), ao EF via `FindResult`/`TryFind*` e ao cliente HTTP com `ToResultAsync`.
Tipos como `OkMatch`, `CreatedMatch` e `NoContentMatch` simplificam respostas HTTP consistentes.
O uso de `readonly struct` e APIs inlinadas favorece performance, e as extensões (`With`, `ChainProperty`) melhoram rastreabilidade.

## Instruções para Ferramentas de IA (GitHub Copilot)

Diretrizes de geração alinhadas às seções 1–8. **Antes de gerar código, leia a §1.1 (pacote e `using`)
e a §8 (erros comuns)** — juntas elas cobrem os erros que não aparecem no IntelliSense.

- Pacotes e `using`
  - Resolva o `using` pela tabela da §1.1; pacote e namespace divergem (`OkMatch` → pacote `ApiResults`,
    tipos em `HttpResults`, extensions em `Microsoft.AspNetCore.Http`; `ToResultAsync` → pacote `Http`,
    namespace `System.Net.Http`; `ToProblemDetails` → pacote `ProblemDetails`, namespace `...Conversions`).
  - Extensões de EF e ASP.NET Core vivem em namespaces da Microsoft: instalado o pacote, não há `using` novo.
- Armadilhas obrigatórias (§8)
  - `Problems.InternalError` é `(detail, typeId, property)`, invertido em relação às demais fábricas:
    sempre passe `property:` nomeado.
  - Nunca produza `default(Result<T>)` nem `new Result<T>()`: reporta sucesso com valor nulo.
  - Em `FindCriteria.By`, o seletor deve ser membro **direto** do parâmetro, e o valor deve ser o valor
    cru (`id.Value`), nunca um `Id<,>`.
  - Cada `out var` precisa de nome único no escopo (CS0128).
- Problemas e categorias
  - Gere problemas com a categoria correta: `InvalidParameter` (400 entrada), `ValidationFailed` (422 semântica), `NotFound` (404), `InvalidState` (409), `NotAllowed` (403), `InternalServerError` (500), `CustomProblem` (typeId descrito).
  - Use `Property` para apontar o campo e `With(key, value)` para contexto adicional; `ChainProperty(parent[, index])` para caminhos.
- Resultados e composição
  - Retorne `Result`/`Result<T>` em serviços/handlers; componha com `Map`, `Continue`, `Match` e `MatchAsync`.
  - Para métodos `Async` com `TParam` e callbacks que retornam `Task`/`Task<T>`, gere chamadas no formato `MetodoAsync(param, static (..., token) => ..., ct)`.
  - Evite exceções para fluxos esperados; converta para problemas e propague via `Result`.
- APIs Web (servidor)
  - Converta `Result`/`Result<T>` em `OkMatch`, `CreatedMatch` (com `Location`) e `NoContentMatch`.
  - Para Minimal APIs, prefira `app.MapGroup("/group-route").WithExceptionFilter()` e mapeie as rotas no grupo; use o filtro apenas para exceptions inesperadas.
  - Configure `ProblemDetailsOptions`, registre descrições com `AddProblemDetailsDescriptions` e exponha `MapProblemDetailsDescriptionPage()` quando a API deve documentar seus tipos.
  - Descreva todo `typeId` customizado; respeite RFC 9457 (`type`, `title`, `status`, `detail`, `instance`).
- Entity Framework
  - Use `TryFindAsync`/`TryFindByAsync` para obter `FindResult`; converta para `Result` com `ToResult([param])`.
  - Para `Id<TEntity,TId>`, pode chamar tanto `db.TryFindAsync(id, ct)` quanto `db.Set<TEntity>().TryFindAsync(id, ct)`; o valor real usado é `id.Value`.
  - Para busca por um campo simples, prefira `TryFindByAsync(e => e.Property == value, ct)` ou a sobrecarga de seletor `TryFindByAsync(e => e.Property, value, ct)`.
  - Para dois ou mais critérios, chave composta ou filtros condicionais, gere `db.FindByCriteria<TEntity>().By(...).By(...).TryFindAsync(ct)`.
  - Lembre que `FindCriteria<TEntity>.By` retorna nova instância; em filtros condicionais, reatribua `criteria = criteria.By(...)`.
  - Use a sobrecarga `By(predicate, byName, propertyName, value)` quando o critério tiver `StartsWith`, range, `OR` ou outra lógica que não seja igualdade simples.
  - Ao gerar mensagens manuais de não encontrado com múltiplos campos, use `FindCriterion[]` e `FindResult<TEntity>.Problem(criteria)`.
  - Não tente documentar `!=`, `>`, `<`, `||`, membro profundo (`e.State.Name`) ou comparação membro-a-membro (`e.A == e.B`) como se fossem critérios `AND` simples; nesses casos o `TryFindByAsync(predicate)` degrada para `NotFound` genérico ou deve receber dados explícitos.
  - Ao não encontrar, retorne `NotFound` padronizado com `Extensions` (`id`, `entity`, `property/value`).
- Cliente HTTP
  - Consuma com `ToResultAsync` (valor ou problemas); trate `application/problem+json` e use `FailureTypeReader` para conteúdos não-ProblemDetails.
- Validação
  - Implemente `HasProblems(out Problems?)` ou use FluentValidation (`EnsureIsValid`, `ToProblems`) para criar `Problems`.
- Performance e observabilidade
  - Prefira `Result` (`readonly struct`) para menor alocação; use `With`/`Extensions` para dados de diagnóstico.

Padrões de prompt para Agentes:
- “Implemente um serviço que valide entrada com FluentValidation, retorne `Result` e mapeie para `CreatedMatch` com Location.”
- “Crie uma consulta EF com `TryFindByAsync` por `Name`; retorne `OkMatch<T>` quando encontrado e `ProblemDetails 404` quando não.”
- “Crie uma busca EF composta com `FindByCriteria`: filtre por `StateId` e `Name`, retorne `FindResult<City>` e documente o `NotFound` com os dois critérios.”
- “Compose um `Result<Order>` em `Result<OrderDto>` usando `Map` e trate falhas com `Match` → `Problems.AsResult()`.”
- “Defina um `Problems.Custom` com `typeId` e configure `ProblemDetailsOptions` seguindo RFC 9457 (URI absoluta em `type`).”
- “Consuma um endpoint com `HttpClient` e `ToResultAsync<T>`; em falha, itere `Problems` e exiba `category`/`detail`.”
