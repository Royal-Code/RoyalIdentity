# SmartSearch — Regras para IA

Regras operacionais para gerar código com SmartSearch em projetos .NET. Para contexto conceitual e explicações, consulte [`smartsearch.md`](smartsearch.md).

> **Verificado contra:** `RoyalCode.SmartSearch` **0.11.0** — .NET 8 / 9 / 10.
> **Precedência das fontes:** documentação XML/IntelliSense da versão instalada > este arquivo > `smartsearch.md`.
> Com versão divergente, confirme a assinatura antes de gerar código.

## 1. Pacotes e `using`

| Necessidade | `using` principal | Pacote |
|---|---|---|
| criteria, filtros, atributos, opções, sortings e resultados | `RoyalCode.SmartSearch` | `RoyalCode.SmartSearch.Abstractions` |
| integração e execução EF Core | `Microsoft.Extensions.DependencyInjection` | `RoyalCode.SmartSearch.EntityFramework` |
| `DbContext.Criteria<TEntity>()` | `Microsoft.EntityFrameworkCore` | `RoyalCode.SmartSearch.EntityFramework` |
| `ISearchManager<TDbContext>` | `RoyalCode.SmartSearch.EntityFramework.Services` | `RoyalCode.SmartSearch.EntityFramework` |
| `ISpecifier<,>`, geradores de expressão | `RoyalCode.SmartSearch.Linq.Filtering` | `RoyalCode.SmartSearch.Linq` |
| selectors customizados | `RoyalCode.SmartSearch.Linq.Mappings` | `RoyalCode.SmartSearch.Linq` |
| `OrderByException` | `RoyalCode.SmartSearch.Exceptions` | `RoyalCode.SmartSearch.Abstractions` |
| `LIKE`/`ILIKE` PostgreSQL | `Microsoft.Extensions.DependencyInjection` | `RoyalCode.SmartSearch.EntityFramework.Npgsql` |
| helpers Minimal API | `Microsoft.AspNetCore.Routing` | `RoyalCode.SmartSearch.AspNetCore` |
| `MatchSearch<>`, `MatchList<>`, `MatchFirst<>` | `RoyalCode.SmartSearch.AspNetCore.HttpResults` | `RoyalCode.SmartSearch.AspNetCore` |
| `IHintsContainer`, `IHintPerformer` | `RoyalCode.OperationHint.Abstractions` | `RoyalCode.OperationHint.EntityFramework` |

Para EF Core, use como referência principal:

```xml
<PackageReference Include="RoyalCode.SmartSearch.EntityFramework" Version="..." />
```

Adicione `RoyalCode.SmartSearch.AspNetCore`, `RoyalCode.SmartSearch.EntityFramework.Npgsql` e `RoyalCode.OperationHint.EntityFramework` somente quando o cenário exigir.

## 2. Regras invioláveis

1. Crie uma nova `ICriteria<TEntity>` por consulta. Ela é mutável e não é thread-safe.
2. Não ramifique nem reutilize a mesma criteria para buscas independentes; filtros, sortings, limites e hints se acumulam.
3. `AsSearch()` e `Select<TDto>()` desativam tracking nas opções compartilhadas. Não volte a usar a mesma criteria esperando tracking.
4. `FilterBy` recebe um objeto filtro. Nunca gere `FilterBy(x => ...)`.
5. Use propriedades nullable em filtros opcionais. `null` deve significar “critério ausente”.
6. Para `In`, declare a propriedade como `IEnumerable<T>`, não `T[]` nem `List<T>`.
7. Em endpoints, aplique limite explícito com `WithOptions`, `UsePages` ou `Take`.
8. Em paginação, aplique sorting estável. Sem sorting, SmartSearch tenta `Id` ascendente.
9. Use `Select<TDto>()` para DTO. Não use `UseHints` esperando includes em projeção.
10. Use `UseHints` somente em terminais que materializam entidades. Hints não afetam `Exists`, contagem nem DTO.
11. Não use `IResultList.Projections` nem `GetProjection<T>()`; a implementação padrão ainda não é funcional.
12. Configure defaults, specifiers, factories, sortings e selectors no startup, antes da primeira consulta; os pares modelo/filtro são cacheados.
13. Propague `CancellationToken` para todos os terminais async.

## 3. Configuração canônica com EF Core

```csharp
using Microsoft.EntityFrameworkCore;
using RoyalCode.SmartSearch;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddEntityFrameworkSearches<AppDbContext>(cfg =>
{
    cfg.Add<Order>();
    cfg.Add<Customer>();

    cfg.AddOrderBy<Order, DateTime>("createdAt", o => o.CreatedAt);
    cfg.AddOrderBy<Order, string>("customer", o => o.Customer.Name);

    cfg.AddSelector<Order, OrderDto>(o => new OrderDto
    {
        Id = o.Id,
        Number = o.Number,
        CustomerName = o.Customer.Name
    });
});
```

Use `cfg.Add<TEntity>()` quando `ICriteria<TEntity>` será injetada diretamente. Use `AddSearchManager<TDbContext>()` quando todas as criterias serão criadas por manager ou `DbContext.Criteria<TEntity>()`.

Formas válidas de obter uma criteria:

```csharp
var criteria = serviceProvider.GetRequiredService<ICriteria<Order>>();

var manager = serviceProvider
    .GetRequiredService<ISearchManager<AppDbContext>>();
var criteria2 = manager.Criteria<Order>();

var criteria3 = db.Criteria<Order>();
```

## 4. Matriz de decisão

| Necessidade | Gere |
|---|---|
| entidade para alteração | `CollectAsync`, `FirstOrDefaultAsync` ou `SingleAsync` diretamente na criteria |
| entidade read-only | `AsSearch().ToListAsync` |
| DTO | `Select<TDto>().ToListAsync` ou `Select(expression)` |
| lista com metadados | `AsSearch().ToListAsync` / `Select<TDto>().ToListAsync` |
| lista de entidades sem metadados e com tracking | `CollectAsync` |
| existência | `ExistsAsync` |
| zero ou um item | `FirstOrDefaultAsync` |
| exatamente um item | `SingleAsync` |
| paginação por página | `UsePages(itemsPerPage, pageNumber)` |
| paginação offset | `SkipTake(skip, take)` |
| opções vindas da query string | `WithOptions(searchOptions)` |
| grafo de entidade | `UseHints(...).FirstOrDefaultAsync/CollectAsync/...` |
| dados relacionados no DTO | inclua-os na expressão de `Select` |

## 5. Filtro padrão

Use classes simples e propriedades nullable:

```csharp
public sealed class OrderFilter
{
    public int? Id { get; set; }

    [Criterion(CriterionOperator.Contains)]
    public string? Number { get; set; }

    [Criterion("Customer.Name", CriterionOperator.Contains,
        Case = CriterionCase.Insensitive)]
    public string? CustomerName { get; set; }

    [Criterion("CreatedAt", CriterionOperator.GreaterThanOrEqual)]
    public DateTime? CreatedAtFrom { get; set; }

    [Criterion("CreatedAt", CriterionOperator.LessThanOrEqual)]
    public DateTime? CreatedAtTo { get; set; }

    [Criterion("Status", Negation = true)]
    public OrderStatus? NotStatus { get; set; }
}
```

Uso:

```csharp
var result = await criteria
    .FilterBy(new OrderFilter
    {
        CustomerName = "Maria",
        CreatedAtFrom = start
    })
    .UsePages(20, 1)
    .Select<OrderDto>()
    .ToListAsync(ct);
```

## 6. Operadores e valores vazios

Operador `Auto`:

- `string` → `Like`.
- `IEnumerable<T>` → `In`.
- demais tipos → `Equal`.

Escolha explícita:

- igualdade → `Equal`.
- range inclusivo → `GreaterThanOrEqual` / `LessThanOrEqual`.
- range exclusivo → `GreaterThan` / `LessThan`.
- coleção de valores aceitos → `In`.
- pattern com `%` → `Like`.
- substring literal → `Contains`.
- prefixo/sufixo → `StartsWith` / `EndsWith`.
- negação → `Negation = true`.

Por padrão, `IgnoreIfIsEmpty = true` ignora strings em branco, referências nulas, `Nullable<T>` sem valor, coleções vazias e valores default. Em `byte`, `short`, `int`, `long`, `float`, `double` e `decimal` não-nullable, a guarda atual exige valor maior que zero; por isso, use nullable em filtros opcionais, principalmente para zero e valores negativos.

Force o default somente quando necessário:

```csharp
[Criterion(IgnoreIfIsEmpty = false)]
public bool Active { get; set; }
```

Ignore uma propriedade auxiliar:

```csharp
[Criterion(Ignore = true)]
public string? UiLabel { get; set; }
```

## 7. `In`

Gere exatamente este formato:

```csharp
public sealed class StatusFilter
{
    [Criterion("Status")]
    public IEnumerable<OrderStatus>? Statuses { get; set; }
}
```

Não gere:

```csharp
public OrderStatus[]? Statuses { get; set; } // incorreto para a emissão atual
public List<OrderStatus>? Statuses { get; set; } // incorreto para a emissão atual
```

## 8. Strings e provider

Regras:

- `Like` interpreta `%` como curinga e, por padrão, envolve o valor em `%valor%`.
- `Contains` trata `%` como caractere literal.
- `Wrap = LikeWrap.None` usa o pattern como informado; sem curinga, o match é exato.
- `Case = CriterionCase.Insensitive` normaliza ambos os lados no modo portável.
- `Case = Default` ou `Sensitive` não força comparação case-sensitive; a collation ainda decide.

Exemplos:

```csharp
[Criterion(CriterionOperator.Like, Wrap = LikeWrap.None)]
public string? SkuPattern { get; set; } // "ABC%"

[Criterion(CriterionOperator.Contains, Case = CriterionCase.Insensitive)]
public string? Name { get; set; }
```

Defaults globais:

```csharp
CriterionDefaults.DefaultStringOperator = CriterionOperator.Contains;
CriterionDefaults.WrapLikeValue = false;
```

Defina-os antes de qualquer busca.

Para EF relacional:

```csharp
builder.Services.AddEntityFrameworkLikeOperator();
```

Para PostgreSQL:

```csharp
builder.Services.AddNpgsqlLikeOperators();
```

Chame `AddNpgsqlLikeOperators()` no lugar de registrar primeiro `AddEntityFrameworkLikeOperator()`, porque a primeira factory aplicável vence. O modo portável suporta `%`, não `_`, e aproxima patterns com mais de cinco fatiamentos. Use factory nativa quando precisar da semântica completa do provider.

## 9. OR e filtros complexos

Use `[Disjunction]` quando propriedades diferentes têm valores independentes:

```csharp
public sealed class ProductFilter
{
    [Disjunction("text")]
    [Criterion("Name", CriterionOperator.Contains)]
    public string? TextInName { get; set; }

    [Disjunction("text")]
    [Criterion("Sku", CriterionOperator.Contains)]
    public string? TextInSku { get; set; }
}
```

Use `Or` no nome/caminho quando o mesmo valor deve testar vários membros:

```csharp
public string? NameOrEmail { get; set; }

[Criterion(TargetPropertyPath = "FirstNameOrLastName")]
public string? PersonName { get; set; }
```

Desative a inferência quando `Or` for parte do nome:

```csharp
[Criterion("Number", DisableOrFromName = true)]
public string? NumberOrCode { get; set; }
```

Filtro complexo:

```csharp
[ComplexFilter]
public sealed class AddressFilter
{
    public string? City { get; set; }
    public string? State { get; set; }
}

public sealed class CustomerFilter
{
    [Criterion("MainAddress")]
    public AddressFilter? Address { get; set; }
}
```

Campos internos preenchidos são AND. Objeto nulo ou totalmente vazio não filtra.

## 10. Customização e precedência

Escolha nesta ordem de simplicidade:

1. `[Criterion]`.
2. `ConfigureSpecifierGenerator(...).For(...).Predicate(...)` para uma propriedade.
3. `[FilterExpressionGenerator<T>]` para uma propriedade que exige árvore de expressão customizada.
4. `AddSpecifier` ou `ISpecifier<,>` para controlar o filtro inteiro.
5. Método `Filter(IQueryable<T>)` no filtro quando esse estilo já for adotado pelo projeto.

Predicate por propriedade:

```csharp
public sealed class OrderProductFilter
{
    public int? ProductId { get; set; }
}

cfg.ConfigureSpecifierGenerator<Order, OrderProductFilter>(options =>
{
    options.For(f => f.ProductId)
        .Predicate(productId => order =>
            order.Items.Any(item => item.ProductId == productId));
});
```

Specifier completo:

```csharp
cfg.AddSpecifier<Order, OrderTextFilter>((query, filter) =>
{
    if (!string.IsNullOrWhiteSpace(filter.Text))
        query = query.Where(o => o.Number.Contains(filter.Text));

    return query;
});
```

Precedência real por `(modelo, filtro)`:

1. specifier registrado ou cacheado;
2. `ISpecifier<TModel,TFilter>` de DI;
3. método público com parâmetro/retorno `IQueryable<TModel>`;
4. gerador declarativo.

Um specifier completo ou método no filtro substitui o processamento convencional de todas as propriedades.

Um `[FilterExpressionGenerator<T>]` também é responsável por tratar valores vazios da sua propriedade; a guarda de `IgnoreIfIsEmpty` não é adicionada automaticamente nesse caminho.

## 11. Sorting

Prefira nomes registrados para contratos públicos:

```csharp
cfg.AddOrderBy<Order, DateTime>("createdAt", o => o.CreatedAt);
cfg.AddOrderBy<Order, string>("customer", o => o.Customer.Name);
```

Aplicação:

```csharp
criteria.OrderBy(new Sorting
{
    OrderBy = "createdAt",
    Direction = ListSortDirection.Descending
});
```

Formatos de `Sorting.TryParse`: `Name`, `Name asc`, `Name desc`, `Name-asc`, `Name-desc` ou JSON.

Regras:

- vários sortings são aplicados na ordem recebida;
- propriedade inválida lança `OrderByNotSupportedException`, que é um `OrderByException`;
- falha de tradução de sorting pelo provider também vira `OrderByException`;
- paginação/limite sem sorting adiciona `Id` ascendente;
- se não houver `Id`, gere sorting explícito antes do limite;
- em borda HTTP manual, capture `OrderByException` e converta para erro de parâmetro `orderby`.

## 12. Paginação, contagem e resultados

Página:

```csharp
var result = await criteria
    .UsePages(itemsPerPage: 20, pageNumber: 1)
    .AsSearch()
    .ToListAsync(ct);
```

Offset:

```csharp
var result = await criteria
    .SkipTake(skip, take)
    .UseCount(false)
    .AsSearch()
    .ToListAsync(ct);
```

Query string:

```csharp
var result = await criteria
    .WithOptions(options)
    .FilterBy(filter)
    .Select<OrderDto>()
    .ToListAsync(ct);
```

Regras:

- `WithOptions(new SearchOptions())` aplica página 1, 10 itens.
- `AsSearch().ToList()` sem `WithOptions`/limites retorna todos os itens; não há limite 10 implícito.
- quando `Page > 0`, `Skip`/`Take` são ignorados em favor da paginação.
- `UseCount(false)` retorna `Count = 0` e `Pages = 0`.
- `UseLastCount(n)` reutiliza somente total positivo.
- `Pages` usa `Ceiling`.
- `Collect` retorna itens, não metadados.
- `ToAsyncListAsync` retorna `IAsyncEnumerable<T>` em `Items`.
- não use `Projections`/`GetProjection<T>()`.

Metadados válidos:

```csharp
result.Items;
result.Count;
result.Page;
result.Pages;
result.ItemsPerPage;
result.Skipped;
result.Taken;
result.Sortings;
```

## 13. Tracking e terminais

| Chamada | Tracking | Hints |
|---|---|---|
| `criteria.Collect[Async]()` | sim | sim |
| `criteria.FirstOrDefault[Async]()` | sim | sim |
| `criteria.Single[Async]()` | sim | sim |
| `criteria.Exists[Async]()` | não materializa | não |
| `criteria.AsSearch().ToList[Async]()` | não | sim |
| `criteria.Select<TDto>().ToList[Async]()` | não | não |

Use `FirstOrDefault` para ausência esperada. Use `Single` apenas quando a cardinalidade exata for parte do contrato; ele lança se houver zero ou mais de um item.

Nunca gere ramificação assim:

```csharp
var search = criteria.AsSearch();
var entities = criteria.Collect(); // também ficou no-tracking
```

Resolva outra criteria.

## 14. Selectors e DTOs

Preferência:

1. selector registrado para contrato estável ou projeção complexa;
2. expressão inline para projeção local;
3. `Select<TDto>()` por convenção apenas quando o mapeamento for simples e verificado.

Registrado:

```csharp
cfg.AddSelector<Order, OrderDto>(o => new OrderDto
{
    Id = o.Id,
    Number = o.Number,
    CustomerName = o.Customer.Name,
    Total = o.Items.Sum(i => i.Quantity * i.UnitPrice)
});
```

Inline:

```csharp
var dto = await criteria
    .Select(o => new OrderDto
    {
        Id = o.Id,
        Number = o.Number
    })
    .FirstOrDefaultAsync(ct);
```

Não adicione `Include`/hints para satisfazer DTO. O provider traduz a projeção diretamente.

## 15. Operation Hints

Registre handlers uma vez:

```csharp
builder.Services.ConfigureOperationHints(registry =>
{
    registry.AddIncludesHandler<Order, OrderHints>((hint, includes) =>
    {
        if (hint is OrderHints.WithCustomer)
            includes.IncludeReference(o => o.Customer);

        if (hint is OrderHints.WithItems)
            includes.IncludeCollection(o => o.Items);
    });
});
```

Use localmente:

```csharp
var order = await criteria
    .UseHints(OrderHints.WithCustomer, OrderHints.WithItems)
    .FilterBy(new OrderByIdFilter { Id = id })
    .FirstOrDefaultAsync(ct);
```

Regras:

- `UseHints` requer ao menos um enum;
- `null` lança `ArgumentNullException`;
- array vazio lança `ArgumentException`;
- hints locais não vazam para outra criteria;
- hints ambientes e locais são combinados;
- sem Operation Hint registrado, o comportamento é no-op;
- hints não se aplicam a `Exists`, count ou DTO.

## 16. ASP.NET Core

Mapeamento canônico:

```csharp
var group = app.MapGroup("/api");

group.MapSearch<Order, OrderDto, OrderFilter>("/orders");
group.MapList<Product, ProductDto, ProductFilter>("/products");
group.MapFirst<Product, ProductFilter>("/products/first");
group.MapSelectFirst<Customer, CustomerDto, CustomerFilter>("/customers/first");
```

Escolha:

- `MapSearch` → `IResultList<T>` com filtro, sorting, paginação e count.
- `MapList` → `IReadOnlyList<T>` com filtro e sorting.
- `MapFirst` → primeira entidade.
- `MapSelectFirst` → primeiro DTO.

Status gerados pelos helpers:

- 200 com resultado;
- 204 sem itens;
- 400 para sorting inválido;
- 500 para erro inesperado.

Em endpoints manuais, anote obrigatoriamente o filtro e `SearchOptions` com `[AsParameters]`. Prefira também `[FromQuery]` para `Sorting[]?`, `[FromServices]` para `ICriteria<TEntity>` e `[FromRoute]` para identificadores de rota, deixando explícita a origem de cada parâmetro.

Rota com escopo adicional:

```csharp
group.MapSearch<Order, OrderDto, OrderFilter, int>(
    "/customers/{customerId:int}/orders",
    (customerId, criteria) =>
    {
        criteria.FilterBy(new OrdersByCustomerFilter
        {
            CustomerId = customerId
        });
    });
```

Não retorne uma nova criteria no delegate; os métodos mutam a instância recebida.

## 17. Receita completa

```csharp
using Microsoft.AspNetCore.Mvc;
using RoyalCode.SmartSearch;

app.MapGet("/orders", async (
    [AsParameters] OrderFilter filter,
    [AsParameters] SearchOptions options,
    [FromQuery] Sorting[]? orderby,
    [FromServices] ICriteria<Order> criteria,
    CancellationToken ct)
    => await criteria
        .WithOptions(options)
        .OrderBy(orderby)
        .FilterBy(filter)
        .Select<OrderDto>()
        .ToListAsync(ct));
```

Para retornar uma entidade rastreada:

```csharp
app.MapGet("/orders/{id:int}/edit", async (
    [FromRoute] int id,
    [FromServices] ICriteria<Order> criteria,
    CancellationToken ct)
    => await criteria
        .UseHints(OrderHints.WithItems)
        .FilterBy(new OrderByIdFilter { Id = id })
        .FirstOrDefaultAsync(ct));
```

Forma equivalente em um método de endpoint:

```csharp
public static async Task<IResultList<OrderDto>> SearchOrdersAsync(
    [AsParameters] OrderFilter filter,
    [AsParameters] SearchOptions options,
    [FromQuery] Sorting[]? orderby,
    [FromServices] ICriteria<Order> criteria,
    CancellationToken ct)
{
    return await criteria
        .WithOptions(options)
        .OrderBy(orderby)
        .FilterBy(filter)
        .Select<OrderDto>()
        .ToListAsync(ct);
}
```

## 18. Anti-padrões

- Não gere `FilterBy(entity => condition)`.
- Não reutilize `ICriteria<TEntity>` entre consultas.
- Não execute a mesma criteria em paralelo.
- Não chame `AsSearch()` e depois espere tracking na criteria original.
- Não use propriedades não-nullable para critérios opcionais sem avaliar `IgnoreIfIsEmpty`.
- Não declare `In` como array ou `List<T>`.
- Não use método manual no filtro esperando que as propriedades também sejam processadas.
- Não altere `CriterionDefaults` depois da primeira busca.
- Não use `UseHints` para DTO, `Exists` ou count.
- Não espalhe lógica de `Include` pelos call sites; registre hints quando o retorno for entidade.
- Não exponha nomes internos frágeis como sortings públicos; registre aliases.
- Não pagine sem sorting estável.
- Não assuma limite de 10 em `AsSearch()` sem `WithOptions`.
- Não use `GetProjection<T>()`.
- Não engula `OrderByException` como erro interno em endpoints manuais.
- Não omita `CancellationToken` em código async de aplicação.

## 19. Checklist antes de entregar o código

- [ ] Pacote e namespace conferidos na tabela da §1.
- [ ] Uma nova `ICriteria<TEntity>` é usada por consulta.
- [ ] Filtro é um objeto; não há lambda em `FilterBy`.
- [ ] Critérios opcionais usam tipos nullable.
- [ ] Propriedade `In` está declarada como `IEnumerable<T>`.
- [ ] `Like` versus `Contains`, wrap e case foram escolhidos conscientemente.
- [ ] OR usa `[Disjunction]` ou token `Or` conforme a semântica desejada.
- [ ] Consulta paginada tem limite e sorting estável.
- [ ] DTO usa selector registrado, expressão inline ou convenção verificada.
- [ ] Tracking foi preservado apenas quando necessário.
- [ ] Hints aparecem somente em terminais de entidade.
- [ ] `OrderByException` é tratado na borda HTTP manual.
- [ ] Não há uso de `Projections`/`GetProjection<T>()`.
- [ ] Configurações globais acontecem antes da primeira consulta.
- [ ] Todo terminal async recebe `CancellationToken`.
