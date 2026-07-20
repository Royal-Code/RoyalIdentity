# Documentação da API SmartSearch

SmartSearch é um conjunto de bibliotecas .NET para construir consultas com filtros declarativos, ordenação dinâmica, paginação, projeção para DTO e execução sobre Entity Framework Core.

Este é o guia conceitual e prático. Para instruções objetivas destinadas a ferramentas de IA, consulte também [`smartsearch.ai-rules.md`](smartsearch.ai-rules.md).

> **Verificado contra:** `RoyalCode.SmartSearch` **0.11.0** — .NET 8, .NET 9 e .NET 10.
> **Precedência das fontes:** documentação XML/IntelliSense da versão instalada > `smartsearch.ai-rules.md` > este guia.
> Se a versão do pacote for diferente, confirme as assinaturas no IDE antes de gerar código.

Sumário

1. Visão geral e conceitos
2. Pacotes, namespaces e instalação
3. Configuração com Entity Framework Core
4. Escolhendo o fluxo de consulta
5. Filtros declarativos
6. Strings: `Like`, `Contains` e case-insensitive
7. AND, OR e filtros complexos
8. Customização de specifiers e expressões
9. Ordenação
10. Paginação, limites e `SearchOptions`
11. Projeção para DTO
12. Terminais, tracking e resultados
13. Operation Hints e carregamento de agregados
14. Helpers para ASP.NET Core
15. Referência rápida da API
16. Erros comuns
17. Boas práticas

## 1. Visão geral e conceitos

SmartSearch implementa o padrão **Filter-Specifier** sobre `IQueryable<T>`:

- **Filter:** objeto que transporta os valores da busca.
- **Criterion:** regra que liga uma propriedade do filtro a uma propriedade do modelo.
- **Specifier:** componente que transforma o filtro em operações sobre `IQueryable<T>`.
- **Criteria:** builder mutável que acumula filtros, sortings, limites e hints.
- **Search:** modo de leitura sem tracking, com suporte a `IResultList<T>`.
- **Selector:** expressão que projeta uma entidade para um DTO.
- **Sorting:** descrição dinâmica de uma ordenação.

Fluxo geral:

```text
filtro + opções + sortings
            │
            ▼
   ICriteria<TEntity>
            │
      specifiers LINQ
            │
            ▼
 IQueryable<TEntity>
      ├─ Collect / First / Single ──► entidade com tracking
      ├─ AsSearch ──────────────────► entidade sem tracking + metadados
      └─ Select<TDto> ──────────────► DTO sem tracking + metadados
```

`ICriteria<TEntity>` é **mutável**. Cada chamada acrescenta ou altera opções na mesma instância. Use uma nova criteria por consulta e não execute em paralelo nem ramifique a mesma instância para montar buscas independentes.

## 2. Pacotes, namespaces e instalação

Para o cenário comum com EF Core, instale:

```bash
dotnet add package RoyalCode.SmartSearch.EntityFramework
```

Pacotes e responsabilidades:

| Pacote | Responsabilidade |
|---|---|
| `RoyalCode.SmartSearch.Abstractions` | `ICriteria<>`, `ISearch<>`, atributos, sortings e result lists |
| `RoyalCode.SmartSearch.Core` | implementações padrão de criteria/search e pipeline abstrato |
| `RoyalCode.SmartSearch.Linq` | geração de specifiers, expressões, selectors e order-by |
| `RoyalCode.SmartSearch.EntityFramework` | DI, execução EF Core, tracking e `DbContext.Criteria<T>()` |
| `RoyalCode.SmartSearch.EntityFramework.Npgsql` | emissão PostgreSQL de `LIKE`/`ILIKE` |
| `RoyalCode.SmartSearch.AspNetCore` | helpers de Minimal API e resultados HTTP padronizados |

Namespaces que mais causam dúvida:

| Tipo ou método | `using` | Pacote |
|---|---|---|
| `ICriteria<>`, `ISearch<>`, `SearchOptions`, `Sorting`, atributos | `RoyalCode.SmartSearch` | `RoyalCode.SmartSearch.Abstractions` |
| `ISearchManager<TDbContext>` | `RoyalCode.SmartSearch.EntityFramework.Services` | `RoyalCode.SmartSearch.EntityFramework` |
| `AddEntityFrameworkSearches`, `AddEntityFrameworkLikeOperator` | `Microsoft.Extensions.DependencyInjection` | `RoyalCode.SmartSearch.EntityFramework` |
| `DbContext.Criteria<TEntity>()` | `Microsoft.EntityFrameworkCore` | `RoyalCode.SmartSearch.EntityFramework` |
| `ISpecifier<,>`, `ISpecifierExpressionGenerator` | `RoyalCode.SmartSearch.Linq.Filtering` | `RoyalCode.SmartSearch.Linq` |
| `ISelector<,>` | `RoyalCode.SmartSearch.Linq.Mappings` | `RoyalCode.SmartSearch.Linq` |
| `OrderByException` | `RoyalCode.SmartSearch.Exceptions` | `RoyalCode.SmartSearch.Abstractions` |
| `AddNpgsqlLikeOperators` | `Microsoft.Extensions.DependencyInjection` | `RoyalCode.SmartSearch.EntityFramework.Npgsql` |
| `MapSearch`, `MapList`, `MapFirst`, `MapSelectFirst` | `Microsoft.AspNetCore.Routing` | `RoyalCode.SmartSearch.AspNetCore` |
| `MatchSearch<>`, `MatchList<>`, `MatchFirst<>` | `RoyalCode.SmartSearch.AspNetCore.HttpResults` | `RoyalCode.SmartSearch.AspNetCore` |
| `IHintsContainer`, `IHintPerformer` | `RoyalCode.OperationHint.Abstractions` | `RoyalCode.OperationHint.EntityFramework` |

Os pacotes de nível superior trazem suas dependências SmartSearch transitivamente. Em aplicações, normalmente basta referenciar `EntityFramework`, mais `AspNetCore` e/ou `EntityFramework.Npgsql` quando necessários.

## 3. Configuração com Entity Framework Core

Registre o `DbContext`, as entidades pesquisáveis e as configurações globais no startup:

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

`cfg.Add<TEntity>()` registra `ICriteria<TEntity>` como serviço transient. Também é possível registrar tipos descobertos dinamicamente:

```csharp
builder.Services.AddEntityFrameworkSearches<AppDbContext>(cfg =>
{
    foreach (var entityType in discoveredEntityTypes)
        cfg.Add(entityType);
});
```

Há três formas usuais de obter uma criteria:

```csharp
// Entidade registrada com cfg.Add<Order>()
var criteria = serviceProvider.GetRequiredService<ICriteria<Order>>();

// Qualquer entidade do DbContext, pelo manager
var manager = serviceProvider
    .GetRequiredService<ISearchManager<AppDbContext>>();
var criteria2 = manager.Criteria<Order>();

// Extensão sobre um DbContext configurado no container
var criteria3 = db.Criteria<Order>();
```

`AddSearchManager<TDbContext>()` pode ser usado sem `cfg.Add<TEntity>()` quando a aplicação sempre cria criterias pelo manager ou por `DbContext.Criteria<TEntity>()`:

```csharp
builder.Services.AddSearchManager<AppDbContext>();
```

## 4. Escolhendo o fluxo de consulta

Use esta matriz como ponto de partida:

| Necessidade | Fluxo recomendado |
|---|---|
| editar entidades após consultar | `criteria.Collect()` / `FirstOrDefault()` / `Single()` |
| leitura de entidades sem tracking | `criteria.AsSearch().ToList()` |
| leitura de DTOs | `criteria.Select<TDto>().ToList()` |
| lista com paginação e metadados | `UsePages(...).AsSearch().ToList()` ou `Select<TDto>().ToList()` |
| lista simples sem metadados | `Collect()` para entidades; `AsSearch().ToList().Items` para no-tracking |
| verificar existência | `Exists()` / `ExistsAsync()` |
| primeiro item opcional | `FirstOrDefault()` / `FirstOrDefaultAsync()` |
| exatamente um item | `Single()` / `SingleAsync()` |
| carregar grafo de entidade | `UseHints(...)` antes de um terminal de entidade |
| projetar dados relacionados | `Select<TDto>()`; não use hints para DTO |

Exemplo canônico de leitura paginada:

```csharp
var result = await criteria
    .FilterBy(new OrderFilter { CustomerName = "Maria" })
    .OrderBy(new Sorting { OrderBy = "createdAt", Direction = ListSortDirection.Descending })
    .UsePages(itemsPerPage: 20, pageNumber: 1)
    .Select<OrderDto>()
    .ToListAsync(ct);
```

## 5. Filtros declarativos

### 5.1 Convenção básica

Toda propriedade pública do filtro é considerada um critério. Sem atributo, o SmartSearch procura no modelo uma propriedade com o mesmo nome e escolhe o operador automaticamente.

```csharp
public sealed class OrderFilter
{
    public int? Id { get; set; }

    public string? Number { get; set; }

    [Criterion("Customer.Name")]
    public string? CustomerName { get; set; }
}
```

Uso:

```csharp
var orders = await criteria
    .FilterBy(new OrderFilter { CustomerName = "Maria" })
    .CollectAsync(ct);
```

`FilterBy` recebe um **objeto filtro**, não uma expressão `Expression<Func<TEntity,bool>>`.

### 5.2 Operadores automáticos

Quando `CriterionAttribute.Operator` é `Auto`:

| Tipo da propriedade do filtro | Operador |
|---|---|
| `string` | `Like` por padrão |
| `IEnumerable<T>` | `In` |
| demais tipos | `Equal` |

Operadores disponíveis:

| Operador | Semântica |
|---|---|
| `Equal` | igualdade |
| `GreaterThan` | maior que |
| `GreaterThanOrEqual` | maior ou igual |
| `LessThan` | menor que |
| `LessThanOrEqual` | menor ou igual |
| `In` | valor do modelo pertence à coleção do filtro |
| `Like` | pattern com `%`, com wrap configurável |
| `Contains` | substring literal |
| `StartsWith` | prefixo |
| `EndsWith` | sufixo |

### 5.3 Caminho alvo, range e negação

```csharp
public sealed class OrderFilter
{
    [Criterion("CreatedAt", CriterionOperator.GreaterThanOrEqual)]
    public DateTime? CreatedAtFrom { get; set; }

    [Criterion("CreatedAt", CriterionOperator.LessThanOrEqual)]
    public DateTime? CreatedAtTo { get; set; }

    [Criterion("Status", Negation = true)]
    public OrderStatus? NotStatus { get; set; }

    [Criterion("Customer.Email", CriterionOperator.Equal)]
    public string? CustomerEmail { get; set; }
}
```

O caminho pode ser passado pelo construtor ou pela propriedade `TargetPropertyPath`:

```csharp
[Criterion(TargetPropertyPath = "Customer.Email", Operator = CriterionOperator.Equal)]
public string? Email { get; set; }
```

Propriedades principais de `[Criterion]`:

| Propriedade | Uso |
|---|---|
| `Operator` | escolhe o operador |
| `TargetPropertyPath` | redireciona para propriedade simples ou aninhada |
| `Negation` | nega a condição |
| `Ignore` | exclui a propriedade do filtro |
| `IgnoreIfIsEmpty` | ignora valores vazios; padrão `true` |
| `Case` | sensibilidade a maiúsculas/minúsculas em operadores de string |
| `Wrap` | controla `%valor%` em `Like` |
| `DisableOrFromName` | impede inferência de OR a partir do nome/caminho |

`[Criterion]` sem configurações é equivalente à convenção sem atributo.

### 5.4 Valores vazios

Por padrão, `IgnoreIfIsEmpty = true`. São ignorados, entre outros:

- `null` em referências e `Nullable<T>`;
- string nula, vazia ou apenas com espaços;
- coleção vazia;
- valores default de structs, como `Guid.Empty`;
- em `byte`, `short`, `int`, `long`, `float`, `double` e `decimal` não-nullable, a guarda atual exige valor maior que zero; portanto zero e negativos não filtram.

Para filtros opcionais, prefira `int?`, `decimal?`, `bool?`, enums nullable e datas nullable. Assim, `null` significa “não filtrar” e valores como `0`, `false` ou o primeiro enum continuam sendo critérios válidos.

Use `IgnoreIfIsEmpty = false` somente quando o valor default realmente precisar gerar condição:

```csharp
[Criterion(IgnoreIfIsEmpty = false)]
public bool Active { get; set; }
```

### 5.5 Operador `In`

Declare a propriedade do filtro como `IEnumerable<T>`:

```csharp
public sealed class OrderStatusesFilter
{
    [Criterion("Status")]
    public IEnumerable<OrderStatus>? Statuses { get; set; }
}

var orders = await criteria
    .FilterBy(new OrderStatusesFilter
    {
        Statuses = new[] { OrderStatus.Paid, OrderStatus.Shipped }
    })
    .CollectAsync(ct);
```

Na implementação atual, a emissão de `In` exige que o tipo declarado seja exatamente `IEnumerable<T>`. Não declare a propriedade como array ou `List<T>`.

## 6. Strings: `Like`, `Contains` e case-insensitive

### 6.1 `Like` e `Contains`

| Operador | Valor `jo%o` | Comportamento |
|---|---|---|
| `Like` | `%jo%o%` por padrão | `%` é curinga; o valor recebe wrap por padrão |
| `Contains` | `jo%o` literal | `%` é texto comum |

O operador automático de strings é `Like`. Sem curingas informados pelo usuário, o wrap padrão faz a busca se comportar como substring.

```csharp
public sealed class ProductFilter
{
    // Pattern como informado: "ABC%" funciona como prefixo.
    [Criterion(CriterionOperator.Like, Wrap = LikeWrap.None)]
    public string? Sku { get; set; }

    // Substring literal, sem interpretar %.
    [Criterion(CriterionOperator.Contains)]
    public string? Description { get; set; }
}
```

Defaults globais devem ser configurados no startup, antes da primeira busca:

```csharp
CriterionDefaults.DefaultStringOperator = CriterionOperator.Contains;
CriterionDefaults.WrapLikeValue = false;
```

Os specifiers gerados são cacheados por par `(modelo, filtro)`. Alterar defaults depois que um par já foi usado não regenera seu specifier.

### 6.2 Case-insensitive

```csharp
[Criterion(CriterionOperator.Contains, Case = CriterionCase.Insensitive)]
public string? Name { get; set; }
```

`CriterionCase.Insensitive` vale para `Like`, `Contains`, `StartsWith` e `EndsWith`. A emissão portável normaliza ambos os lados com `ToUpper()`. `Default` e `Sensitive` não normalizam; o resultado efetivo ainda depende da collation do provider.

Normalização por função costuma impedir o uso de índices comuns. Quando o schema é controlado, considere collation ou tipo de coluna apropriado no banco.

### 6.3 Emissão portável e emissão do provider

Sem configuração adicional, `Like` usa uma expressão portável com `StartsWith`, `EndsWith`, `Contains`, `IndexOf` e `Substring`.

Limitações do modo portável:

- suporta `%`, mas não `_` como curinga;
- depois de cinco fatiamentos, segmentos excedentes usam `Contains` sem garantia de ordem;
- a tradução final depende das capacidades do provider LINQ.

Para SQL relacional com EF Core:

```csharp
builder.Services.AddEntityFrameworkLikeOperator();
```

Isso emite `EF.Functions.Like`, honrando `%` e `_` conforme o provider.

Para PostgreSQL:

```csharp
builder.Services.AddNpgsqlLikeOperators();
```

O pacote Npgsql emite `EF.Functions.ILike` quando `Case = Insensitive` e `EF.Functions.Like` nos demais casos. Chame `AddNpgsqlLikeOperators()` no lugar de registrar primeiro `AddEntityFrameworkLikeOperator()`: a ordem importa, pois a primeira factory que produzir uma expressão vence.

Factories só afetam critérios gerados. Predicados manuais, specifiers registrados e métodos de filtro são responsabilidade do consumidor.

## 7. AND, OR e filtros complexos

### 7.1 AND por padrão

Propriedades comuns de um filtro e chamadas sucessivas de `FilterBy` são acumuladas na mesma consulta:

```csharp
criteria
    .FilterBy(new TenantFilter { TenantId = tenantId })
    .FilterBy(new OrderFilter { Status = OrderStatus.Paid });
```

### 7.2 OR agrupado com `[Disjunction]`

Propriedades com o mesmo alias são unidas por OR:

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

Se todos os membros do grupo estiverem vazios, nenhum `Where` é aplicado. Se apenas um tiver valor, há uma única condição. Com vários valores, as condições são combinadas por OR.

### 7.3 OR inferido pelo nome ou caminho

O token `Or` em nome ou `TargetPropertyPath` cria uma disjunção usando o mesmo valor:

```csharp
public sealed class CustomerFilter
{
    // Customer.Name LIKE value OR Customer.Email LIKE value
    public string? NameOrEmail { get; set; }

    [Criterion(TargetPropertyPath = "FirstNameOrLastName")]
    public string? PersonName { get; set; }
}
```

Se `Or` fizer parte do nome e não representar uma disjunção, desative a convenção:

```csharp
[Criterion("Number", DisableOrFromName = true)]
public string? NumberOrCode { get; set; }
```

### 7.4 Filtros complexos

Use `[ComplexFilter]` quando uma propriedade do filtro contém um subfiltro aplicado a um objeto complexo do modelo:

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

Os campos internos preenchidos são combinados por AND. Se o objeto for nulo ou todos os seus campos forem vazios, o filtro complexo não aplica condições.

O atributo pode ser colocado no tipo complexo ou diretamente na propriedade. Dentro do subfiltro, continuam válidos `[Criterion]`, caminhos, operadores e OR por nome.

## 8. Customização de specifiers e expressões

Quando convenções e atributos não forem suficientes, escolha o ponto de extensão mais simples que resolva o caso.

### 8.1 Predicate por propriedade

Configure uma propriedade específica sem abandonar o restante do filtro declarativo:

```csharp
public sealed class OrderProductFilter
{
    public int? ProductId { get; set; }
}

builder.Services.AddEntityFrameworkSearches<AppDbContext>(cfg =>
{
    cfg.Add<Order>();

    cfg.ConfigureSpecifierGenerator<Order, OrderProductFilter>(options =>
    {
        options.For(f => f.ProductId)
            .Predicate(productId => order =>
                order.Items.Any(item => item.ProductId == productId));
    });
});
```

O predicate substitui a resolução convencional apenas dessa propriedade. A regra de valor vazio ainda é aplicada.

### 8.2 Specifier registrado

Registre a função completa para o par modelo/filtro:

```csharp
cfg.AddSpecifier<Order, OrderTextFilter>((query, filter) =>
{
    if (!string.IsNullOrWhiteSpace(filter.Text))
    {
        query = query.Where(o =>
            o.Number.Contains(filter.Text) ||
            o.Customer.Name.Contains(filter.Text));
    }

    return query;
});
```

Também é possível implementar e registrar `ISpecifier<TModel,TFilter>`.

### 8.3 Método no próprio filtro

Um filtro pode declarar um método público com um parâmetro e retorno `IQueryable<TModel>`:

```csharp
public sealed class OrderTextFilter
{
    public string? Text { get; set; }

    public IQueryable<Order> Filter(IQueryable<Order> query)
    {
        if (!string.IsNullOrWhiteSpace(Text))
            query = query.Where(o => o.Number.Contains(Text));

        return query;
    }
}
```

O nome `Filter` é recomendado, embora a descoberta use a assinatura. Esse método representa o filtro inteiro; suas propriedades não são processadas novamente por convenção.

### 8.4 Gerador de expressão por atributo

Use `[FilterExpressionGenerator<TGenerator>]` para manter o filtro declarativo e gerar uma expressão especial:

```csharp
public sealed class OrderFilter
{
    [Criterion("CreatedAt")]
    [FilterExpressionGenerator<PeriodExpressionGenerator>]
    public Period Period { get; set; }
}

public sealed class PeriodExpressionGenerator : ISpecifierExpressionGenerator
{
    public static Expression GenerateExpression(ExpressionGeneratorContext context)
    {
        var startMethod = typeof(PeriodExpressionGenerator)
            .GetMethod(nameof(GetStart))!;
        var start = Expression.Call(startMethod, context.FilterMember);
        var body = Expression.GreaterThanOrEqual(context.ModelMember, start);
        var predicate = Expression.Lambda(body, context.Model);

        var where = ExpressionGenerator.CreateWhereCall(
            context.Model.Type,
            context.Query,
            predicate);

        return Expression.Assign(context.Query, where);
    }

    public static DateTime GetStart(Period period) => period switch
    {
        Period.Last7Days => DateTime.UtcNow.Date.AddDays(-7),
        Period.ThisMonth => new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1),
        _ => DateTime.UtcNow.Date
    };
}
```

O contexto fornece `Query`, `Filter`, `Model`, `ModelMember` e `FilterMember`. O retorno normalmente atribui um novo `Where(...)` a `context.Query`.

O gerador customizado controla toda a semântica dessa propriedade, inclusive o que fazer com valores vazios. A guarda de `IgnoreIfIsEmpty` não é adicionada automaticamente nesse caminho; se o critério for opcional, trate a ausência dentro da expressão gerada ou escolha outro ponto de extensão.

### 8.5 Precedência

Para cada par `(modelo, filtro)`, a resolução ocorre nesta ordem:

1. specifier já registrado/cacheado com `AddSpecifier`;
2. `ISpecifier<TModel,TFilter>` resolvido por DI;
3. método público no filtro com assinatura `IQueryable<TModel> -> IQueryable<TModel>`;
4. geração por propriedades, atributos e configurações.

O resultado é cacheado por processo. Faça configurações globais antes da primeira consulta.

## 9. Ordenação

### 9.1 Ordenação dinâmica

```csharp
criteria.OrderBy(new Sorting
{
    OrderBy = "CreatedAt",
    Direction = ListSortDirection.Descending
});
```

Vários critérios são aplicados na ordem informada:

```csharp
criteria.OrderBy(
[
    new Sorting { OrderBy = "Status" },
    new Sorting { OrderBy = "CreatedAt", Direction = ListSortDirection.Descending }
]);
```

O gerador padrão resolve propriedades e caminhos aninhados. Há fallback case-insensitive por segmento para caminhos com `.` ou `-`.

### 9.2 Nomes registrados

Registre nomes públicos estáveis, especialmente para navegações, expressões calculadas ou contratos HTTP:

```csharp
cfg.AddOrderBy<Order, string>("customer", o => o.Customer.Name);
cfg.AddOrderBy<Order, decimal>("total", o =>
    o.Items.Sum(i => i.Quantity * i.UnitPrice));
```

Uso:

```csharp
criteria.OrderBy(new Sorting { OrderBy = "customer" });
```

### 9.3 Parsing

`Sorting.TryParse` aceita:

- `Name`
- `Name asc`
- `Name desc`
- `Name-asc`
- `Name-desc`
- JSON no formato `{"orderBy":"Name","direction":1}`

`Sorting.ToString()` produz `Name` para ascendente e `Name-desc` para descendente.

### 9.4 Erros e ordenação default

Uma propriedade inexistente lança `OrderByNotSupportedException`, derivada de `OrderByException`. Uma ordenação que o provider não consegue traduzir também é encapsulada em `OrderByException` durante a materialização.

Quando há `Skip`, `Take` ou paginação e nenhuma ordenação foi aplicada, SmartSearch adiciona `Id` ascendente. Portanto, modelos paginados precisam de uma propriedade `Id` ordenável ou de uma ordenação explícita/registrada.

## 10. Paginação, limites e `SearchOptions`

### 10.1 API fluente

```csharp
criteria.UsePages(itemsPerPage: 20, pageNumber: 1);
criteria.FetchPage(2);
criteria.Skip(10);
criteria.Take(50);
criteria.SkipTake(skip: 10, take: 50);
criteria.UseCount();
criteria.UseCount(false);
criteria.UseLastCount(lastCount);
```

Quando `Page > 0`, a paginação tem precedência sobre `Skip`/`Take`.

Sem `UsePages`, `Take`, `Skip` ou `WithOptions`, `AsSearch().ToList()` não impõe limite: ele materializa todos os itens correspondentes e retorna `ItemsPerPage = 0`. Em endpoints, defina limites explicitamente.

### 10.2 `SearchOptions`

`SearchOptions` foi projetado para receber parâmetros de query string:

```csharp
var options = new SearchOptions
{
    Page = 2,
    ItemsPerPage = 25,
    Count = true
};

var result = await criteria
    .WithOptions(options)
    .FilterBy(filter)
    .AsSearch()
    .ToListAsync(ct);
```

`WithOptions` chama `AvoidEmpty()`. Se `Page`, `ItemsPerPage`, `Skip` e `Take` estiverem todos nulos, usa página 1 com 10 itens.

Outras operações úteis:

```csharp
var options = new SearchOptions()
    .OrderBy("name")
    .OrderByDesc("createdAt");

options.UpdateFromResult(previousResult);
options.AllItens(); // nome preservado pela API atual
```

`UseCount(false)` evita calcular o total e retorna `Count = 0`/`Pages = 0`. `UseLastCount(n)` reutiliza um total positivo conhecido e evita nova contagem.

### 10.3 Metadados de `IResultList<T>`

```csharp
var result = await criteria
    .UsePages(20, 1)
    .AsSearch()
    .ToListAsync(ct);

var items = result.Items;
var total = result.Count;
var currentPage = result.Page;
var pages = result.Pages;
var skipped = result.Skipped;
var taken = result.Taken;
var sortings = result.Sortings;
```

`Pages` usa arredondamento para cima. Por exemplo, 3 itens com 2 por página resultam em 2 páginas.

`ToAsyncListAsync()` retorna `IAsyncResultList<T>`, cujo `Items` é `IAsyncEnumerable<T>`. A contagem, quando habilitada, é obtida antes de expor o stream.

`Projections` e `GetProjection<T>()` estão reservados para agregados futuros. Os result lists padrão não preenchem `Projections`, e `ResultList<T>.GetProjection<T>()` lança `NotImplementedException`. Não use essa superfície em código atual.

## 11. Projeção para DTO

### 11.1 Selector por convenção

```csharp
var result = await criteria
    .FilterBy(filter)
    .Select<OrderDto>()
    .UsePages(20, 1)
    .ToListAsync(ct);
```

O gerador de selector tenta mapear propriedades correspondentes e suporta cenários como propriedades aninhadas, nullables, enums, subobjetos e coleções. Se não puder gerar o selector, a execução lança uma exceção de selector não encontrado.

Para contrato crítico ou mapeamento não trivial, prefira registrar a expressão:

```csharp
cfg.AddSelector<Order, OrderDto>(o => new OrderDto
{
    Id = o.Id,
    Number = o.Number,
    CustomerName = o.Customer.Name,
    Total = o.Items.Sum(i => i.Quantity * i.UnitPrice)
});
```

### 11.2 Selector inline

```csharp
var order = await criteria
    .FilterBy(new OrderByIdFilter { Id = 10 })
    .Select(o => new OrderDto
    {
        Id = o.Id,
        Number = o.Number
    })
    .FirstOrDefaultAsync(ct);
```

`Select<TDto>()` e `AsSearch()` desativam tracking nas opções da criteria. A ordenação é aplicada antes da projeção e preservada no resultado.

### 11.3 Como o selector é resolvido

Quando `Select<TDto>()` precisa de uma expressão de projeção, o `ISelectorFactory` procura por ela em quatro
níveis, do mais explícito ao mais implícito. O primeiro que responder vence, e o resultado fica em cache para o par
`(TEntity, TDto)`:

| # | Origem | Como |
|---|---|---|
| 1 | Selector já resolvido | cache interno do par `(TEntity, TDto)` |
| 2 | `ISelector<TEntity, TDto>` no DI | registro explícito da aplicação, inclusive via `cfg.AddSelector<TEntity, TDto>(expression)` |
| 3 | **Propriedade estática no DTO** | qualquer propriedade `public static` do DTO cujo tipo seja `Expression<Func<TEntity, TDto>>` |
| 4 | Geração em runtime | expressão construída por reflexão a partir da forma dos tipos |

O **nível 3** é um contrato por convenção, e é o que permite ao SmartSearch aproveitar uma expressão pronta sem
precisar conhecer quem a escreveu. Serve tanto para uma expressão escrita à mão:

```csharp
public class OrderDto
{
    // descoberta pelo nível 3 — o nome da propriedade não importa, só o tipo
    public static Expression<Func<Order, OrderDto>> Selector { get; } = o => new OrderDto
    {
        Id = o.Id,
        CustomerName = o.Customer.Name
    };
}
```

...quanto para a expressão **gerada pelo SmartSelector** (ver 11.4).

O **nível 4** é o fallback: resolve DTOs de forma "plana", casando propriedades por nome (com flattening), e cobre
nullables, enums, subobjetos e coleções. Exige que o DTO tenha construtor sem parâmetros. Quando não consegue montar a
projeção, a execução lança `SelectorNotFoundException` — o erro aparece **em runtime**, não em compilação.

### 11.4 Uso em conjunto com o SmartSelector

As duas bibliotecas foram feitas para trabalhar juntas **sem depender uma da outra**: o SmartSearch funciona sem o
SmartSelector (usando os níveis 2 e 4), e o SmartSelector funciona sem o SmartSearch (projetando com LINQ direto).

Quando as duas estão presentes, a integração é automática e não exige registro nenhum. O SmartSelector gera, no próprio
DTO, uma propriedade `public static Expression<Func<TEntity, TDto>> Select{Entity}Expression` — que é exatamente o que o
**nível 3** procura:

```csharp
[AutoSelect<Order>, AutoProperties]
public partial class OrderDetails
{
    public List<OrderItemDetails> Items { get; set; } = [];
}

// SelectOrderExpression é gerada pelo SmartSelector e encontrada pelo SmartSearch:
var result = await criteria.FilterBy(filter).Select<OrderDetails>().ToListAsync(ct);
```

Vale a pena, porque a expressão gerada é verificada **em tempo de compilação**: o SmartSelector reporta projeções
inseguras (nulabilidade, caminhos inválidos) como diagnósticos `RCSS*`, enquanto a geração em runtime (nível 4) apenas
projeta com o que encontra. Preferir o SmartSelector quando o DTO existe no código-fonte; deixar o nível 4 para os
casos em que não há DTO anotado.

## 12. Terminais, tracking e resultados

| Terminal | Retorno | Tracking EF | Hints |
|---|---|---|---|
| `Collect()` | `IReadOnlyList<TEntity>` | sim | sim |
| `Exists()` | `bool` | não materializa entidade | não |
| `FirstOrDefault()` | `TEntity?` | sim | sim |
| `Single()` | `TEntity` | sim | sim |
| `AsSearch().ToList()` | `IResultList<TEntity>` | não | sim |
| `Select<TDto>().ToList()` | `IResultList<TDto>` | não | não |
| `Select<TDto>().FirstOrDefault()` | `TDto?` | não | não |

Todos possuem variantes async quando aplicável.

`Single()` e `SingleAsync()` lançam `InvalidOperationException` se não houver exatamente um item. Use `FirstOrDefault` quando “não encontrado” fizer parte do fluxo esperado.

`Collect()` respeita filtros, ordenação, `Skip`, `Take` e paginação, mas retorna apenas itens, sem metadados.

Importante: `AsSearch()` e `Select<TDto>()` alteram as opções compartilhadas da criteria para no-tracking. Não faça isto:

```csharp
// Evite ramificar/reutilizar a mesma instância.
var search = criteria.AsSearch();
var tracked = criteria.Collect(); // também será no-tracking
```

Obtenha outra `ICriteria<TEntity>` para uma consulta independente.

## 13. Operation Hints e carregamento de agregados

SmartSearch não expõe `Include(...)` em `ICriteria<TEntity>`. Para carregar navegações quando o retorno é entidade, integre com Operation Hint.

Instale no projeto de infraestrutura:

```bash
dotnet add package RoyalCode.OperationHint.EntityFramework
```

Registre os includes por `(entidade, tipo de hint)`:

```csharp
public enum OrderHints
{
    WithCustomer,
    WithItems
}

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

Hints locais pertencem somente à criteria:

```csharp
var order = await criteria
    .UseHints(OrderHints.WithCustomer, OrderHints.WithItems)
    .FilterBy(new OrderByIdFilter { Id = 10 })
    .FirstOrDefaultAsync(ct);
```

`UseHints` exige ao menos um valor. Array nulo lança `ArgumentNullException`; array vazio lança `ArgumentException`.

Hints ambientes valem para as consultas no mesmo escopo:

```csharp
var container = serviceProvider.GetRequiredService<IHintsContainer>();
container.AddHint(OrderHints.WithCustomer);

var orders = await criteria.CollectAsync(ct);
```

Hints locais e ambientes são combinados. Eles são aplicados somente ao materializar entidades e não afetam `Exists`, contagens ou DTOs. Sem Operation Hint registrado, são ignorados sem erro.

O mesmo `AddIncludesHandler` também registra o handler usado pelo fluxo pós-carga do Operation Hint. Em um repository que obteve a entidade por `Find`, é possível aplicar os hints ao objeto já carregado:

```csharp
var container = serviceProvider.GetRequiredService<IHintsContainer>();
var performer = serviceProvider.GetRequiredService<IHintPerformer>();

container.AddHint(OrderHints.WithItems);

var order = await db.Set<Order>().FindAsync([id], ct);
if (order is not null)
    performer.Perform(order, db);
```

Para DTOs, projete os dados necessários no selector:

```csharp
var dto = await criteria
    .Select(o => new OrderDto
    {
        Id = o.Id,
        CustomerName = o.Customer.Name
    })
    .FirstOrDefaultAsync(ct);
```

## 14. Helpers para ASP.NET Core

O pacote `RoyalCode.SmartSearch.AspNetCore` fornece endpoints GET para Minimal API.

```bash
dotnet add package RoyalCode.SmartSearch.AspNetCore
```

### 14.1 Matriz dos helpers

| Helper | Resultado 200 | Opções |
|---|---|---|
| `MapSearch<TEntity,TFilter>` | `IResultList<TEntity>` | filtro, sorting, paginação e contagem |
| `MapSearch<TEntity,TDto,TFilter>` | `IResultList<TDto>` | idem, com projeção |
| `MapList<TEntity,TFilter>` | `IReadOnlyList<TEntity>` | filtro e sorting, sem metadados |
| `MapList<TEntity,TDto,TFilter>` | `IReadOnlyList<TDto>` | idem, com projeção |
| `MapFirst<TEntity,TFilter>` | primeira entidade | filtro e sorting |
| `MapSelectFirst<TEntity,TDto,TFilter>` | primeiro DTO | filtro, sorting e projeção |

Todos retornam 204 quando não há resultado, 400 (`InvalidParameter`) para sorting inválido e 500 (`InternalError`) para erro inesperado. Os tipos `MatchSearch<>`, `MatchList<>` e `MatchFirst<>` também publicam metadados de endpoint.

### 14.2 Mapeamento básico

```csharp
var group = app.MapGroup("/api");

group.MapSearch<Order, OrderDto, OrderFilter>("/orders");
group.MapList<Product, ProductDto, ProductFilter>("/products");
group.MapFirst<Product, ProductFilter>("/products/first");
group.MapSelectFirst<Customer, CustomerDto, CustomerFilter>("/customers/first");
```

O filtro é ligado da query string com `[AsParameters]`. `MapSearch` também recebe `SearchOptions`; `orderby` é ligado separadamente como `Sorting[]?`.

Exemplos de URL:

```text
GET /api/orders?customerName=maria&page=1&itemsPerPage=20&orderby=createdAt-desc
GET /api/products?active=true&orderby=price
```

### 14.3 Configuração adicional e parâmetros de rota

O delegate de configuração pode acrescentar filtros, hints e outras opções:

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

Há overloads com identificadores de rota adicionais. A ordem dos tipos genéricos é sempre entidade, DTO quando houver, filtro e identificadores.

Os helpers executam o delegate depois de aplicar opções, sortings e o filtro recebido. Como a criteria é mutável, o delegate pode apenas chamar os métodos fluentes; não precisa retornar a instância.

### 14.4 Endpoint manual

Use `ICriteria<TEntity>` diretamente quando o contrato HTTP ou o tratamento de erro precisar ser customizado:

```csharp
app.MapGet("/orders", async Task<MatchSearch<OrderDto>> (
    [AsParameters] OrderFilter filter,
    [AsParameters] SearchOptions options,
    [FromQuery] Sorting[]? orderby,
    [FromServices] ICriteria<Order> criteria,
    CancellationToken ct) =>
{
    try
    {
        var result = await criteria
            .WithOptions(options)
            .OrderBy(orderby)
            .FilterBy(filter)
            .Select<OrderDto>()
            .ToListAsync(ct);

        return result.Count == 0
            ? TypedResults.NoContent()
            : TypedResults.Ok(result);
    }
    catch (OrderByException ex)
    {
        return Problems.InvalidParameter(ex.Message, "orderby");
    }
});
```

Esse exemplo pressupõe as integrações de SmartProblems usadas pelo pacote ASP.NET Core.

## 15. Referência rápida da API

Configuração global:

```csharp
cfg.Add<TEntity>();
cfg.Add(typeof(TEntity));
cfg.AddOrderBy<TEntity, TProperty>(name, expression);
cfg.AddSelector<TEntity, TDto>(expression);
cfg.AddSpecifier<TEntity, TFilter>(function);
cfg.ConfigureSpecifierGenerator<TEntity, TFilter>(configure);
```

Construção da criteria:

```csharp
criteria.FilterBy(filter);
criteria.OrderBy(sorting);
criteria.OrderBy(sortings);
criteria.UseHints(hints);
criteria.WithOptions(options);
criteria.UsePages(itemsPerPage, pageNumber);
criteria.FetchPage(pageNumber);
criteria.Skip(skip);
criteria.Take(take);
criteria.SkipTake(skip, take);
criteria.UseCount(useCount);
criteria.UseLastCount(lastCount);
```

Conversão e terminais:

```csharp
criteria.Collect();
criteria.CollectAsync(ct);
criteria.Exists();
criteria.ExistsAsync(ct);
criteria.FirstOrDefault();
criteria.FirstOrDefaultAsync(ct);
criteria.Single();
criteria.SingleAsync(ct);

criteria.AsSearch().ToList();
criteria.AsSearch().ToListAsync(ct);
criteria.AsSearch().ToAsyncListAsync(ct);

criteria.Select<TDto>().ToListAsync(ct);
criteria.Select(expression).FirstOrDefaultAsync(ct);
```

## 16. Erros comuns

### 16.1 Passar lambda para `FilterBy`

```csharp
// ❌ FilterBy espera um objeto filtro.
criteria.FilterBy(o => o.Status == OrderStatus.Paid);

// ✅ Modele o critério.
criteria.FilterBy(new OrderFilter { Status = OrderStatus.Paid });
```

Para lógica não declarativa, use predicate configurado, specifier, método no filtro ou gerador de expressão.

### 16.2 Reutilizar a mesma criteria

```csharp
// ❌ Os filtros se acumulam na mesma instância.
var paid = criteria.FilterBy(new OrderFilter { Status = OrderStatus.Paid });
var cancelled = criteria.FilterBy(new OrderFilter { Status = OrderStatus.Cancelled });

// ✅ Obtenha duas criterias transientes.
var paidCriteria = provider.GetRequiredService<ICriteria<Order>>();
var cancelledCriteria = provider.GetRequiredService<ICriteria<Order>>();
```

### 16.3 Usar tipo não-nullable para filtro opcional

```csharp
// ❌ false/0/default ficam ambíguos com “não informado”.
public bool Active { get; set; }
public int Minimum { get; set; }

// ✅ null significa “não filtrar”.
public bool? Active { get; set; }
public int? Minimum { get; set; }
```

### 16.4 Declarar `In` como array ou lista

```csharp
// ❌ A emissão atual exige IEnumerable<T> como tipo declarado.
public OrderStatus[]? Statuses { get; set; }

// ✅
public IEnumerable<OrderStatus>? Statuses { get; set; }
```

### 16.5 Esperar hints em DTO ou `Exists`

```csharp
// ❌ UseHints não executa Include depois da projeção.
criteria.UseHints(OrderHints.WithCustomer).Select<OrderDto>();

// ✅ Projete o campo relacionado no selector.
criteria.Select(o => new OrderDto { CustomerName = o.Customer.Name });
```

### 16.6 Paginar sem ordenação estável

Sem sorting explícito, consultas limitadas usam `Id` ascendente. Se a entidade não tiver `Id`, a execução falha. Mesmo quando existe, defina uma ordenação pública estável quando a paginação fizer parte do contrato.

### 16.7 Assumir que `AsSearch()` pagina automaticamente

```csharp
// ⚠️ Sem opções, não há limite implícito.
var all = await criteria.AsSearch().ToListAsync(ct);

// ✅ Endpoint paginado explicitamente.
var page = await criteria.UsePages(20, 1).AsSearch().ToListAsync(ct);

// ✅ SearchOptions vazio aplica o default 10/1 via WithOptions.
var defaultPage = await criteria
    .WithOptions(new SearchOptions())
    .AsSearch()
    .ToListAsync(ct);
```

### 16.8 Usar `GetProjection<T>()`

Essa API ainda não está implementada. Use somente os metadados atuais de `IResultList<T>`.

## 17. Boas práticas

- Crie uma nova `ICriteria<TEntity>` para cada consulta.
- Modele filtros como classes pequenas, com propriedades nullable para critérios opcionais.
- Use atributos apenas quando a convenção não expressar operador, alvo ou composição.
- Prefira selectors registrados para contratos DTO importantes.
- Registre nomes de sorting estáveis em vez de expor detalhes internos da entidade.
- Defina limite explícito em endpoints e ordenação estável em consultas paginadas.
- Use `Collect`/`FirstOrDefault`/`Single` somente quando precisar de entidades com tracking.
- Use `AsSearch` ou `Select<TDto>` para leitura.
- Use hints para grafos de entidade; use projeção para DTOs.
- Configure defaults, specifiers, factories, sortings e selectors antes da primeira busca.
- Capture `OrderByException` em bordas manuais de API e converta para erro de entrada.
- Propague `CancellationToken` em todos os terminais async.

Para geração de código por IA, use [`smartsearch.ai-rules.md`](smartsearch.ai-rules.md), que contém regras imperativas, matrizes de decisão, receitas e checklist.
