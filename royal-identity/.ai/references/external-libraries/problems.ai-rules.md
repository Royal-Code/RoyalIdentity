# SmartProblems — Regras para IA

Regras operacionais para gerar código com SmartProblems em **qualquer projeto .NET**.
Contexto conceitual: [`problems.md`](problems.md).

> **Verificado contra:** `RoyalCode.SmartProblems` **1.0.0-preview-7.0** — .NET 8 / 9 / 10.
> **Precedência das fontes:** documentação XML do pacote (no IDE) > este arquivo > `problems.md`.
> Com versão divergente, confirme a assinatura no IntelliSense antes de gerar.

## 1. Antes da primeira linha: pacote e `using`

Pacote e namespace **divergem**. Não deduza; consulte.

| Tipo / membro | `using` | Pacote NuGet |
|---|---|---|
| `Problem`, `Problems`, `Result`, `Result<T>` | `RoyalCode.SmartProblems` | `RoyalCode.SmartProblems` |
| `FindResult<>`, `Id<,>`, `FindCriterion`, `FindCriteria<>` | `RoyalCode.SmartProblems.Entities` | `RoyalCode.SmartProblems` |
| `TryFindAsync`, `TryFindByAsync`, `FindByCriteria`, `AddTo`, `SaveChanges`, `RemoveFromAsync` | `Microsoft.EntityFrameworkCore` | `RoyalCode.SmartProblems.EntityFramework` |
| `OkMatch`, `OkMatch<T>`, `CreatedMatch<T>`, `NoContentMatch` (tipos) | `RoyalCode.SmartProblems.HttpResults` | `RoyalCode.SmartProblems.ApiResults` |
| `.OkMatch()`, `.CreatedMatch()`, `.NoContentMatch()` (extensions) | `Microsoft.AspNetCore.Http` | `RoyalCode.SmartProblems.ApiResults` |
| `WithExceptionFilter` | `Microsoft.AspNetCore.Builder` | `RoyalCode.SmartProblems.ApiResults` |
| `ToActionResult` (MVC) | `Microsoft.AspNetCore.Mvc` | `RoyalCode.SmartProblems.ApiResults` |
| `ToResultAsync` | `System.Net.Http` | `RoyalCode.SmartProblems.Http` |
| `FailureTypeReader` | `RoyalCode.SmartProblems.Http` | `RoyalCode.SmartProblems.Http` |
| `ToProblemDetails(options)` | `RoyalCode.SmartProblems.Conversions` | `RoyalCode.SmartProblems.ProblemDetails` |
| `ProblemDetailsOptions`, `ProblemDetailsDescription` | `RoyalCode.SmartProblems.Descriptions` | `RoyalCode.SmartProblems.ProblemDetails` |
| `AddProblemDetailsDescriptions` | `Microsoft.Extensions.DependencyInjection` | `RoyalCode.SmartProblems.ProblemDetails` |
| `MapProblemDetailsDescriptionPage` | `Microsoft.AspNetCore.Builder` | `RoyalCode.SmartProblems.ProblemDetails` |
| `EnsureIsValid`, `ToProblems`, `HasProblems` | `FluentValidation` | `RoyalCode.SmartProblems.FluentValidation` |

Regras de namespace: tipos `OkMatch`/`CreatedMatch`/`NoContentMatch` usam `RoyalCode.SmartProblems.HttpResults`;
extensions `.OkMatch()`/`.CreatedMatch()`/`.NoContentMatch()` usam `Microsoft.AspNetCore.Http`;
`ToResultAsync` usa `System.Net.Http`; `ToProblemDetails` usa `RoyalCode.SmartProblems.Conversions`.

## 2. Regras invioláveis

1. **Nunca lance exceção para falha esperada.** Validação, regra de negócio e "não encontrado" retornam
   `Result` / `Result<T>` / `FindResult<T>`. Exceção é só para o inesperado.
2. **Nunca use `default(Result<T>)` nem `new Result<T>()`.** Inicialize por valor, `Problem`, `Problems` ou `Result.Ok()`.
3. **`Problems.InternalError` inverte `property` e `typeId`.** Sempre passe `property:` nomeado.
4. **Cada `out var` precisa de nome único no escopo** (CS0128): `inputProblems`, `validationProblems`.
5. **Em `CollectAsync`/`MapAsync`/`ContinueAsync`/`MatchAsync` com `TParam`**, a ordem é
   `(param, delegate, ct)`. O `CancellationToken` é sempre o último parâmetro do método.
   Nunca `(param, ct, delegate)`.
6. **`FindCriteria.By(selector, value)`**: o seletor deve ser membro **direto** do parâmetro
   (`c => c.Name`), e o valor deve ser o valor cru — nunca um `Id<,>` (use `id.Value`).
7. **Todo `typeId` customizado precisa de `ProblemDetailsDescription` registrada** antes de expor a API.
8. **Não use `try/catch` nem exception filter para validação, regra de domínio ou 404.**

## 3. Categorias → HTTP status

| Categoria | Status | Uso |
|---|---|---|
| `InvalidParameter` | 400 | entrada inválida do cliente (formato, range, obrigatório) |
| `ValidationFailed` | 422 | entrada sintaticamente válida, regra de domínio violada |
| `NotAllowed` | 403 | autorização, política, janela de operação |
| `InvalidState` | 409 | conflito de estado ou transição inválida |
| `NotFound` | 404 | recurso inexistente |
| `InternalServerError` | 500 | erro inesperado; nunca para erro de domínio |
| `CustomProblem` | conforme a descrição | erro de domínio fora das categorias; exige `typeId` |

## 4. Assinaturas (cheat-sheet)

Fábricas de `Problem`:

```csharp
Problems.InvalidParameter(string detail, string? property = null, string? typeId = null);
Problems.ValidationFailed(string detail, string? property = null, string? typeId = null);
Problems.NotAllowed      (string detail, string? property = null, string? typeId = null);
Problems.InvalidState    (string detail, string? property = null, string? typeId = null);
Problems.NotFound        (string detail, string? property = null, string? typeId = null);
Problems.InternalError   (string? detail, string? typeId = null, string? property = null); // invertido!
Problems.InternalError   (Exception? exception = null);
Problems.Custom          (string detail, string typeId, string? property = null);
```

`Problem` / `Problems`:

```csharp
Problem  With(string key, object? value);              // encadeável
Problem  With<TEnum>(string key, TEnum value) where TEnum : Enum;
Problem  ChainProperty(string? parentProperty);        // "User" + "name" => "User.name"
Problem  ChainProperty(string? parentProperty, int index); // => "User[0].name"
Result   AsResult();      Result<T> AsResult<T>();
InvalidOperationException ToException();
InvalidOperationException ToException(string messagePattern, string separator = "\n");
Problems operator +(Problem a, Problem b);             // agrega
// implícitos: Problem -> Problems; Problem/Problems -> Result
```

`Result<T>` — acesso e composição:

```csharp
bool IsSuccess;  bool IsFailure;
bool HasValue([NotNullWhen(true)] out T? value);
bool HasProblems([NotNullWhen(true)] out Problems? problems);
bool HasProblemsOrGetValue(out Problems? problems, out T? value);  // um teste só
bool HasValueOrGetProblems(out T? value, out Problems? problems);
void EnsureHasValue(out T value);                                  // LANÇA se IsFailure

Result<TOther> Map<TOther>(Func<T, TOther> map);
Result<TOther> Map<TOther>(Func<T, Result<TOther>> map);
Result<TOther> Map<TOther, TParam>(TParam param, Func<T, TParam, TOther> map);
Result<T>      Continue(Action<T> action);
Result<T>      Continue(Func<T, Result> action);
Result<T>      Continue<TParam>(TParam param, Func<T, TParam, Result> action);
TResult        Match<TResult>(Func<T, TResult> onSuccess, Func<Problems, TResult> onFailure);

// MatchAsync<TResult, TParam>(param, onSuccess, onFailure, ct = default)
//   onSuccess: (value, param, ct) => Task<TResult>
//   onFailure: (problems, param, ct) => Task<TResult> | (problems, ct) => Task<TResult> | (problems, param) => TResult
```

`Result` (sem valor): `Result.Ok()`, `IsSuccess`, `HasProblems(out problems)`.
Conversões implícitas: `T`, `Problem`, `Problems`, `Exception` e `FindResult<T>` → `Result<T>`;
`Result<T>` → `Result`.

`FindResult<TEntity>` e `FindResult<TEntity, TId>`:

```csharp
bool Found;
bool NotFound(out Problem? problem);                              // categoria NotFound (404)
bool HasInvalidParameter(out Problem? problem, string? parameterName = null); // 400
Result<TEntity> ToResult();
Result<TEntity> ToResult(string parameterName);                   // falha vira InvalidParameter
Result Collect(Action<TEntity> receiver);
Result<TEntity> Continue(Func<TEntity, Result> receiver);
Result<TValue> Map<TValue>(Func<TEntity, TValue> receiver);
// + CollectAsync / ContinueAsync / MapAsync, todos com (param, delegate, ct)

static FindResult<TEntity> Problem(string byName, string propertyName, object? propertyValue);
static FindResult<TEntity> Problem(ReadOnlySpan<FindCriterion> criteria);   // multi-critério
```

`FindCriterion` e `Id<,>`:

```csharp
new FindCriterion(string propertyName, object? value, string? byName = null); // ArgumentException se propertyName vazio
Id<TEntity, TId> id = rawValue;   // conversão implícita
TId value = id.Value;
```

Entity Framework:

```csharp
Task<FindResult<TEntity, TId>> TryFindAsync<TEntity, TId>(this DbContext db, Id<TEntity, TId> id, CancellationToken ct = default);
Task<FindResult<TEntity, TId>> TryFindAsync<TEntity, TId>(this DbSet<TEntity> set, TId id, CancellationToken ct = default);
Task<FindResult<TEntity, TId>> TryFindAsync<TEntity, TId>(this DbSet<TEntity> set, Id<TEntity, TId> id, CancellationToken ct = default);

Task<FindResult<TEntity>> TryFindByAsync<TEntity>(this DbContext db, Expression<Func<TEntity, bool>> filter, CancellationToken ct = default);
Task<FindResult<TEntity>> TryFindByAsync<TEntity>(this DbContext db, Expression<Func<TEntity, bool>> filter, string byName, string propertyName, object? propertyValue, CancellationToken ct = default);
Task<FindResult<TEntity>> TryFindByAsync<TEntity, TValue>(this DbContext db, Expression<Func<TEntity, TValue>> propertySelector, TValue filterValue, CancellationToken ct = default);
// as mesmas três sobrecargas existem sobre DbSet<TEntity>

FindCriteria<TEntity> FindByCriteria<TEntity>(this DbContext db);
FindCriteria<TEntity> FindByCriteria<TEntity>(this DbSet<TEntity> set);
FindCriteria<TEntity> FindByCriteria<TEntity>(this IQueryable<TEntity> query);   // após Include/AsNoTracking

// FindCriteria<TEntity> — imutável: reatribua a cada By
FindCriteria<TEntity> By<TValue>(Expression<Func<TEntity, TValue>> selector, TValue value);
FindCriteria<TEntity> By<TValue>(Expression<Func<TEntity, TValue>> selector, TValue value, string byName);
FindCriteria<TEntity> By(Expression<Func<TEntity, bool>> filter, string byName, string propertyName, object? value);
Task<FindResult<TEntity>> TryFindAsync(CancellationToken ct = default);

Result<TEntity> AddTo<TEntity>(this Result<TEntity> result, DbContext context);
ValueTask<Result<TEntity>> AddToAsync<TEntity>(this Result<TEntity> result, DbContext context, CancellationToken ct = default);
Task<Result<TEntity>> AddToAsync<TEntity>(this Task<Result<TEntity>> result, DbContext context, CancellationToken ct = default);
Task<Result<TEntity>> AddToAsync<TEntity>(this ValueTask<Result<TEntity>> result, DbContext context, CancellationToken ct = default);

Result SaveChanges(this Result result, DbContext context);
ValueTask<Result> SaveChangesAsync(this Result result, DbContext context, CancellationToken ct = default);
Task<Result> SaveChangesAsync(this Task<Result> result, DbContext context, CancellationToken ct = default);
Result<TEntity> SaveChanges<TEntity>(this Result<TEntity> result, DbContext context);
ValueTask<Result<TEntity>> SaveChangesAsync<TEntity>(this Result<TEntity> result, DbContext context, CancellationToken ct = default);
Task<Result<TEntity>> SaveChangesAsync<TEntity>(this Task<Result<TEntity>> result, DbContext context, CancellationToken ct = default);

Task<Result<TEntity>> RemoveFromAsync<TEntity>(this Task<FindResult<TEntity>> task, DbContext context, CancellationToken ct);
```

API e cliente HTTP:

```csharp
OkMatch<T>      OkMatch<T>(this Result<T> result);
CreatedMatch<T> CreatedMatch<T>(this Result<T> result, Func<T, string> createdPathFunction);
CreatedMatch<T> CreatedMatch<T>(this Result<T> result, string createdPath, bool formatPathWithValue = false);
// implícitos: Result -> OkMatch e NoContentMatch;
//             Result<T>, FindResult<T>, T, Problem, Problems -> OkMatch<T>
// => um handler pode retornar FindResult<T> direto como OkMatch<T>, sem ToResult()

Task<Result>    ToResultAsync(this HttpResponseMessage response, CancellationToken ct = default);
Task<Result<T>> ToResultAsync<T>(this HttpResponseMessage response, JsonSerializerOptions? options = null, CancellationToken ct = default);
Task<Result<T>> ToResultAsync<T>(this HttpResponseMessage response, JsonTypeInfo<T> jsonTypeInfo, CancellationToken ct = default);
```

FluentValidation:

```csharp
Result<T> EnsureIsValid<T>(this AbstractValidator<T> validator, T model);
Task<Result<T>> EnsureIsValidAsync<T>(this AbstractValidator<T> validator, T model);
bool HasProblems<T>(this AbstractValidator<T> validator, T model, out Problems? problems);
bool HasProblems(this ValidationResult result, out Problems? problems);
Problems ToProblems(this IList<ValidationFailure> errors);
```

## 5. Receitas canônicas

Serviço de domínio:

```csharp
public async Task<Result<Order>> ConfirmAsync(int id, ConfirmRequest request, CancellationToken ct = default)
{
    if (request.HasProblems(out var requestProblems))
        return requestProblems;                                   // 400

    var find = await db.Set<Order>().TryFindAsync(id, ct);
    if (find.NotFound(out var notFound))
        return notFound;                                          // 404

    var order = find.Entity;
    if (order.IsShipped)
        return Problems.InvalidState("Order is already shipped")   // 409
            .With("orderId", id);

    order.Confirm();
    await db.SaveChangesAsync(ct);
    return order;
}
```

Criação com EF helpers:

```csharp
return await Product.Create(command)
    .AddTo(db)
    .SaveChangesAsync(db, ct);
```

Criação com etapa anterior assíncrona:

```csharp
return await CreateProductAsync(command, ct)
    .AddToAsync(db, ct)
    .SaveChangesAsync(db, ct);
```

Remoção com EF helpers:

```csharp
return await db.Set<Product>()
    .TryFindByAsync(p => p.Id == id, ct)
    .RemoveFromAsync(db, ct)
    .SaveChangesAsync(db, ct);
```

Busca composta (dois ou mais critérios, chave composta, filtro condicional):

```csharp
var criteria = db.FindByCriteria<City>()
    .By(c => c.StateId, stateId.Value);      // Id<,> => .Value

if (!string.IsNullOrWhiteSpace(name))
    criteria = criteria.By(c => c.Name, name);   // imutável: reatribua

var find = await criteria.TryFindAsync(ct);
// Detail: "The record of 'City' with StateId '42', Name 'Blumenau' was not found"
// Extensions: { entity: "City", StateId: 42, Name: "Blumenau" }
```

Critério que não é igualdade simples (`StartsWith`, range, `OR`, navegação):

```csharp
criteria = criteria.By(c => c.Name.StartsWith(prefix), byName: "Name", propertyName: "name", value: prefix);
```

Minimal API:

```csharp
var group = app.MapGroup("/api").WithExceptionFilter();   // só exceptions inesperadas

group.MapGet("/orders/{id:int}", async (int id, OrderService svc, CancellationToken ct)
    => await svc.GetAsync(id, ct));                       // Task<OkMatch<OrderDto>>

group.MapPost("/orders", async (CreateOrder cmd, OrderService svc, CancellationToken ct)
    => (await svc.CreateAsync(cmd, ct)).CreatedMatch(o => $"/api/orders/{o.Id}"));

group.MapDelete("/orders/{id:int}", async (int id, OrderService svc, CancellationToken ct)
    => await svc.DeleteAsync(id, ct));                    // Task<NoContentMatch>
```

Cliente HTTP:

```csharp
var response = await http.GetAsync("/users/123", ct);
var result = await response.ToResultAsync<UserDto>(ct: ct);

if (result.HasProblemsOrGetValue(out var problems, out var user))
    return problems;
return user;
```

Problema customizado (RFC 9457):

```csharp
builder.Services.AddProblemDetailsDescriptions(options =>
{
    options.BaseAddress = "https://api.exemplo.com/problems";
    options.Descriptor.Add(new ProblemDetailsDescription(
        typeId: "order-on-hold",
        title: "Order on hold",
        description: "The order cannot move forward while risk analysis is pending.",
        status: HttpStatusCode.Conflict));
});

return Problems.Custom("Order is on hold due to risk analysis", "order-on-hold");
```

## 6. Anti-padrões

```csharp
// ❌ default: sucesso com valor nulo          // ✅ construa explicitamente
Result<Order> r = default;                      Result<Order> r = order;

// ❌ typeId = "userId" silenciosamente        // ✅ nomeie o parâmetro
Problems.InternalError("Falha", "userId");      Problems.InternalError("Falha", property: "userId");

// ❌ CS0128                                   // ✅ nomes distintos
if (a.HasProblems(out var problems)) ...        if (a.HasProblems(out var aProblems)) ...
if (b.HasProblems(out var problems)) ...        if (b.HasProblems(out var bProblems)) ...

// ❌ ArgumentException: membro indireto       // ✅ membro direto, ou sobrecarga de predicado
criteria.By(c => c.State.Name, "SC");           criteria.By(c => c.State.Name == "SC", "State", "stateName", "SC");

// ❌ compila e lança em runtime               // ✅ valor cru
criteria.By(c => c.StateId, stateId);           criteria.By(c => c.StateId, stateId.Value);

// ❌ ct antes do delegate                     // ✅ ct por último
result.MapAsync(param, ct, fn);                 result.MapAsync(param, fn, ct);

// ❌ exceção para fluxo esperado              // ✅ problema tipado
throw new NotFoundException(id);                return Problems.NotFound("Order not found", "orderId");

// ❌ InvalidParameter para regra de domínio   // ✅ 422
Problems.InvalidParameter("Total negativo");    Problems.ValidationFailed("Total negativo", "total");
```

Regras adicionais:

- Use `EnsureHasValue` somente depois de tratar falhas. Não use `EnsureHasValue` para validar `default(Result<T>)`.
- Use `TryFindAsync(id)` para chave primária e considere o change tracker.
- Use `TryFindByAsync(predicado)` ou `FindByCriteria(...).TryFindAsync(ct)` quando a busca deve consultar o banco.
- Use `AddTo(db).SaveChangesAsync(db, ct)` para `Result<TEntity>` já materializado.
- Use `AddToAsync(db, ct).SaveChangesAsync(db, ct)` apenas quando a etapa anterior é `Task<Result<TEntity>>`
  ou `ValueTask<Result<TEntity>>`.
- Use `RemoveFromAsync(db, ct)` somente sobre `Task<FindResult<TEntity>>`.
- Espere mensagem multi-campo automática em `TryFindByAsync(predicado)` apenas para `&&` de igualdades (`==`)
  com membro direto da entidade.
- Para `!=`, `>`, `<`, `||`, `e.State.Name` e `e.A == e.B`, use `FindByCriteria` ou informe
  `byName`, `propertyName` e `value`.
- Capture valores de getters antes do predicado quando houver efeito colateral: `var name = request.Name;`.

## 7. Checklist antes de entregar o código

- [ ] `using` e `PackageReference` conferidos na tabela da §1.
- [ ] Nenhum `throw` em fluxo esperado; nenhum `default(Result<T>)`.
- [ ] `CancellationToken` como último parâmetro, em métodos e em delegates `Async`.
- [ ] `property:` nomeado em `Problems.InternalError`.
- [ ] `FindByCriteria` quando há dois ou mais critérios; `By` reatribuído em filtro condicional.
- [ ] `AddTo`/`SaveChanges`/`RemoveFromAsync` usados somente em fluxos `Result`/`FindResult`.
- [ ] `Id<,>` convertido com `.Value` ao entrar em `By`.
- [ ] Todo `typeId` customizado tem `ProblemDetailsDescription` registrada.
- [ ] Categoria coerente com o status esperado (§3).
