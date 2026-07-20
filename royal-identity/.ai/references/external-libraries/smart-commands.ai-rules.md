# SmartCommands — Regras para IA

Regras operacionais para gerar código com SmartCommands em projetos .NET. Para explicações, contexto e referência
completa, consulte [`smart-commands.md`](smart-commands.md).

> **Verificado contra:** `RoyalCode.SmartCommands` **0.1.0**, SmartProblems `1.0.0-preview-8.0`,
> SmartSelector `0.5.3` e Extensions.SourceGenerator `0.4.1` — .NET 8 / 9 / 10; generator
> `netstandard2.0` como analyzer.
> **Precedência das fontes:** documentação XML/IntelliSense da versão instalada > este arquivo >
> [`smart-commands.md`](smart-commands.md).
> Com versão divergente, confirme atributos, assinaturas geradas e diagnósticos no IDE.

## 1. Pacotes e `using`

```xml
<ItemGroup>
  <PackageReference Include="RoyalCode.SmartCommands" Version="0.1.0" />
  <PackageReference Include="RoyalCode.SmartCommands.Generators"
                    Version="0.1.0"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />

  <!-- instale os adapters necessários; cada command seleciona no máximo um -->
  <PackageReference Include="RoyalCode.SmartCommands.EntityFramework" Version="0.1.0" />
  <PackageReference Include="RoyalCode.SmartCommands.WorkContext" Version="0.1.0" />
</ItemGroup>
```

| API | `using` | Pacote |
|---|---|---|
| atributos, handlers e accessors | `RoyalCode.SmartCommands` | `RoyalCode.SmartCommands` |
| adapter EF | `RoyalCode.SmartCommands.EntityFramework.Extensions` | `.EntityFramework` |
| adapter WorkContext/retry | `RoyalCode.SmartCommands.WorkContext.Extensions` | `.WorkContext` |
| `Result`, `Problems`, `Id`, `FindResult` | `RoyalCode.SmartProblems[.Entities]` | SmartProblems |
| geração e `RCCMD*` | nenhum namespace consumidor | `.Generators` |

Nunca referencie o generator como assembly de runtime. Não adicione `SmartProblems.ApiResults` separadamente:
o runtime já o traz. Adicione `SmartProblems.Http` somente ao usar suas integrações opcionais.

## 2. Regras invioláveis

1. Gere uma classe por caso de uso e exatamente um método `[Command]` por classe.
2. Faça o método `[Command]` acessível ao handler gerado; não dependa de reflexão.
3. Prefira `Result`/`Result<T>` para falhas esperadas; não use exceção como fluxo de validação ou domínio.
4. Nunca gere `async void`. Propague `CancellationToken` apenas em método async.
5. Trate propriedades do command como payload e parâmetros comuns do método como dependências de DI.
6. Use `[WithParameter]` somente para valor externo ao payload. Ele não significa rota.
7. Não marque entidade, coleção de entidades, contexto ou serviço de DI com `[WithParameter]`.
8. Não combine `WithUnitOfWork<T>`, `WithDbContext` e `WithWorkContext` no mesmo command.
9. Use `[WithTransaction]` somente junto de uma UoW; ele força transação, não cria o accessor.
10. Use a forma genérica `[EditEntity<TEntity,TId>]`; a entidade deve ser o primeiro parâmetro.
11. Use `[WithRetryOnConcurrency]` somente com `[WithWorkContext]`; mantenha validações fora do retry.
12. Declare no máximo um map principal de endpoint por classe e endpoint name não vazio e globalmente único;
    `MapGroup` e os atributos auxiliares de resposta não contam como outro endpoint.
13. Não use simultaneamente `MapIdResultValue` e `MapResponseValues`.
14. Não combine `MapCreatedRoute` e `MapAcceptedRoute`.
15. Use placeholders nomeados (`{id}`), nunca posicionais (`{0}`), em routes de `Location`.
16. Use `WithResultStatus` somente em command maps; Find, FindBy e Search mantêm contratos próprios.
17. Não materialize entidade para projetar `MapFindBy`; a projeção deve ocorrer no provider.
18. Corrija todo `RCCMD`; não edite `.g.cs` nem contorne o generator.

## 3. Matriz de decisão

| Necessidade | Gere |
|---|---|
| caso de uso simples | método `[Command]` retornando `Result`/`Result<T>` |
| validação de propriedades | `[WithValidateModel]` + `HasProblems(out Problems?)` |
| validação com serviço/I/O | `[CommandValidation]` |
| argumento fornecido pelo caller | `[WithParameter]` |
| dependência | parâmetro normal, registrado na DI |
| leitura de entidades sem save | `[WithFindEntities<TContext>]` |
| persistência por contrato genérico | `[WithUnitOfWork<TContext>]` |
| persistência EF direta | `[WithDbContext]` |
| persistência WorkContext | `[WithWorkContext]` |
| transação obrigatória | `[WithTransaction]` + UoW |
| edição pelo id | `[EditEntity<TEntity,TId>]` |
| entidade nova retornada | `[ProduceNewEntity]` + UoW |
| preocupação transversal | `[WithDecorators]` + `IDecorator<,>` |
| conflito otimista repetível | `[WithRetryOnConcurrency]` + WorkContext |
| command HTTP | `MapGet/Post/Put/Patch/Delete` |
| DTO por id | `MapFind` + `EntityReference<TEntity,TId>` |
| DTO por chave alternativa | `MapFindBy<TEntity>` |
| lista filtrada/paginada | `MapSearch` + `SearchReference` |
| 200/201/202/204 explícito | `WithResultStatus(HttpResultStatus.*)` |
| `Location` em 201/202 | `MapCreatedRoute` / `MapAcceptedRoute` |
| somente id no corpo | `MapIdResultValue` |
| subconjunto de propriedades | `MapResponseValues` |

## 4. Padrão canônico de command

```csharp
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace MyApp.Products;

public partial class CreateProduct
{
    public string? Name { get; init; }
    public decimal Price { get; init; }

    public bool HasProblems(out Problems? problems) =>
        Rules.Set<CreateProduct>()
            .NotEmpty(Name)
            .GreaterThanOrEqual(Price, 0)
            .HasProblems(out problems);

    [CommandValidation(Order = 5)]
    internal Task<Result> ValidateSkuAsync(
        ISkuRegistry registry,
        CancellationToken ct) =>
        registry.ValidateAsync(Name, ct);

    [Command, WithValidateModel, WithDbContext, ProduceNewEntity]
    internal Result<Product> Execute()
    {
        WasValidated();
        return new Product(Name, Price);
    }
}
```

Espere `ICreateProductHandler` e `CreateProductHandler`. Use a interface gerada na aplicação; não instancie a
implementação manualmente.

## 5. Retornos e assincronismo

Formas aceitas no command:

| Semântica | Síncrono | Assíncrono |
|---|---|---|
| sem valor | `void`, `Result` | `Task`, `ValueTask`, `Task<Result>`, `ValueTask<Result>` |
| com valor | `T`, `Result<T>` | `Task<T>`, `ValueTask<T>`, `Task<Result<T>>`, `ValueTask<Result<T>>` |

Gere:

```csharp
[Command]
internal Task<Result<Order>> ExecuteAsync(CancellationToken ct) =>
    service.CreateAsync(this, ct);
```

Não gere:

```csharp
[Command]
internal async void Execute() { } // nunca

[Command]
internal Result Execute(CancellationToken ct) => Result.Ok(); // token em método sync
```

Não adicione o modificador `async` quando puder retornar a task diretamente.

## 6. Parâmetros e binding

Classifique cada parâmetro antes de gerar:

| Pergunta | Papel |
|---|---|
| é `CancellationToken`? | cancelamento |
| caller do handler precisa fornecer? | `[WithParameter]` |
| é entidade carregada? | entidade / `[IsEntity]` quando necessário |
| é contexto da UoW? | contexto |
| restante | dependência de DI |

Valor externo:

```csharp
[Command]
internal Task<Result> ExecuteAsync(
    [WithParameter, FromHeader(Name = "X-Tenant")] string tenant,
    IProductService service,
    CancellationToken ct) =>
    service.ExecuteAsync(this, tenant, ct);
```

Regras HTTP:

- copie binding explícito para o delegate, não para a interface do handler;
- aceite placeholder vindo do `MapGroup` ou do map;
- exija que `[FromRoute(Name=...)]` exista no template combinado;
- não combine fontes de binding;
- não use `[AsParameters]` em `[WithParameter]`;
- não infira body em GET/DELETE;
- não gere dois bodies;
- preserve tipos especiais, `BindAsync` e `TryParse` válidos do ASP.NET Core.

## 7. Validação

### 7.1 Modelo

Ao usar `[WithValidateModel]`, exija:

```csharp
public bool HasProblems(out Problems? problems);
```

Use classe `partial` e chame `WasValidated()` dentro do command quando a validação torna propriedades nullable
seguras. Não invente outra assinatura de `HasProblems`.

### 7.2 Validators adicionais

```csharp
[CommandValidation(Order = 10)]
internal ValueTask<Result> ValidateAsync(
    IValidatorService service,
    [WithParameter] string tenant,
    CancellationToken ct) =>
    service.ValidateAsync(this, tenant, ct);
```

Exija:

- método de instância diferente do `[Command]`;
- retorno `Result`, `Task<Result>` ou `ValueTask<Result>`;
- `Order` default 10; menor primeiro;
- nenhum significado público para empate;
- DI em parâmetros comuns;
- `[WithParameter]` para valor externo;
- nenhuma entidade, UoW, contexto ou accessor;
- token somente em forma async;
- short-circuit no primeiro `Result` com problems.

## 8. Ordem obrigatória do pipeline

```text
RequireBody
  -> HasProblems
  -> CommandValidation (Order crescente)
  -> BeginAsync(requireTransaction)
  -> carregar entidades / EditEntity
  -> decorators
  -> [Command]
  -> AddEntityAsync (ProduceNewEntity)
  -> CompleteAsync
  -> resposta HTTP
```

Não mova validação para dentro da transação ou do retry. Não execute command após `NotFound` ou problems.

Com retry:

```text
validações (uma vez)
  -> retry [begin -> load -> decorators -> command -> complete]
```

## 9. Persistência

### 9.1 Escolha única

```csharp
[Command, WithUnitOfWork<AppContext>]
[Command, WithDbContext]
[Command, WithWorkContext]
```

Escolha uma linha, nunca combine.

Para leitura sem complete:

```csharp
[Command, WithFindEntities<AppContext>]
```

Para transação obrigatória:

```csharp
[Command, WithDbContext, WithTransaction]
```

### 9.2 Registro

EF direto:

```csharp
services.AddUnitOfWorkAccessor<AppDbContext>();
```

WorkContext:

```csharp
services.AddWorkContext<AppDbContext>()
    .AddUnitOfWorkAccessor();
```

Não presuma que `[AddHandlersServices]` registra adapters, repositórios, decorators ou dependências do command.

## 10. Entidades

### 10.1 Edição

```csharp
[MapPut("/{id:guid}", "edit-product")]
public sealed class EditProduct
{
    [Command, WithDbContext, EditEntity<Product, Guid>]
    internal Result Execute(Product product) => /* mutate */;
}
```

Exija a entidade como primeiro parâmetro. Resolva o id nesta ordem:

1. `RouteParameterName` explícito;
2. única variável da rota combinada;
3. `{parameterNameId}`;
4. `{parameterName}`.

Com ambiguidade, gere:

```csharp
[EditEntity<Product, Guid>(RouteParameterName = "productId")]
```

Não aceite variável opcional, catch-all ou constraint incompatível com o id.

### 10.2 Nova entidade

Use `[ProduceNewEntity]` somente com UoW e valor de sucesso contendo a entidade. Não confunda persistência da
entidade com projeção do corpo HTTP.

## 11. Decorators

```csharp
public sealed class AuditDecorator
    : IDecorator<UpdateProduct, Result>
{
    public async Task<Result> HandleAsync(
        UpdateProduct command,
        Func<Task<Result>> next,
        CancellationToken ct)
    {
        await audit.BeforeAsync(command, ct);
        var result = await next();
        await audit.AfterAsync(command, result, ct);
        return result;
    }
}
```

Ative `[WithDecorators]` e registre `IDecorator<TCommand,TResult>` na DI. O primeiro registrado é o mais externo.
Chamar `next()` duas vezes reexecuta o restante do pipeline; não faça isso por acidente.

## 12. Retry de concorrência

Formas válidas:

```csharp
[WithRetryOnConcurrency]
[WithRetryOnConcurrency(3)]
[WithRetryOnConcurrency("products.update")]
[WithRetryOnConcurrency("products.update", 3)]
```

Regras:

- exija `[WithWorkContext]`;
- use `maxAttempts >= 1`;
- conte a tentativa inicial;
- mantenha efeitos repetíveis dentro do laço;
- não trate como retry de conexão/transiente;
- propague cancelamento;
- customize o problema por operation quando o domínio precisar.

```csharp
services.AddConcurrencyRetryProblem<UpdateProduct>(
    "products.update",
    static (_, _) => Problems.InvalidState(
        "The product was changed by another process.",
        typeId: "products.concurrency_conflict"));
```

## 13. Registro dos handlers e host HTTP

```csharp
[AddHandlersServices("Catalog")]
public static partial class CatalogServices { }

[MapApiHandlers, WithOpenApi]
public static partial class ApiEndpoints { }
```

Consumo:

```csharp
services.AddCatalogHandlersServices<AppDbContext>();
app.MapProductsGroup();
```

Use título vazio para gerar `AddHandlersServices`. Declare apenas um `[MapApiHandlers]` por projeto. Commands sem
map continuam disponíveis por handler.

## 14. Minimal API — matriz

| Caso | Atributos |
|---|---|
| command GET | `MapGet(route,name)` |
| criação | `MapPost` + `WithResultStatus(Created)` |
| substituição | `MapPut` |
| alteração parcial | `MapPatch` |
| exclusão | `MapDelete` |
| DTO por id | `MapFind` + `EntityReference<TEntity,TId>` |
| DTO por chave | `MapFindBy<TEntity>(route,name,nameof(...))` |
| busca paginada | `MapSearch` + `SearchReference<TEntity[,TDto]>` |
| prefixo | `MapGroup` |
| documentação | `WithSummary`, `WithDescription`, `WithTags` |
| segurança | `WithAuthorization`, `WithPolicy` |
| filtro HTTP | `WithEndpointFilter<TFilter>` |

Não crie `MapGroup` implícito. Não aplique dois verbos/maps à mesma classe.

## 15. Status, corpo e `Location`

| Status | Gere | Corpo de sucesso |
|---:|---|---|
| 200 | `WithResultStatus(Ok)` | valor quando existe |
| 201 | `WithResultStatus(Created)` | valor opcional |
| 202 | `WithResultStatus(Accepted)` | nenhum em `Result`; valor em `Result<T>` |
| 204 | `WithResultStatus(NoContent)` | nenhum |

Preserve problems em todas as formas.

Created:

```csharp
[WithResultStatus(HttpResultStatus.Created)]
[MapCreatedRoute("{id}", nameof(Product.Id))]
```

Accepted:

```csharp
[WithResultStatus(HttpResultStatus.Accepted)]
[MapAcceptedRoute("jobs/{id}", nameof(JobTicket.Id))]
```

Sem valor, use rota estática ou omita `Location`:

```csharp
[WithResultStatus(HttpResultStatus.Accepted)]
[MapAcceptedRoute("jobs")]
```

Regras:

- use placeholders nomeados e propriedades públicas;
- faça cada placeholder casar com uma propriedade declarada;
- não use rota com placeholders quando não existe valor;
- não combine Created route e Accepted route;
- não exija `Location`: ela é opcional em ambos os status na API;
- não combine `NoContent` com projeção de corpo.

Corpo reduzido:

```csharp
[MapIdResultValue]
[MapResponseValues(nameof(Product.Id), nameof(Product.Name))]
```

Escolha uma forma. Não declare nomes vazios, duplicados ou propriedades inacessíveis.

## 16. Find, FindBy e Search

### 16.1 Find por id

```csharp
[MapGroup("products")]
[MapFind("/{id:guid}", "get-product")]
[EntityReference<Product, Guid>]
public partial class ProductDetails { }
```

Exija placeholder de id compatível no grupo/map. Preserve `NotFound`.

### 16.2 FindBy

```csharp
[MapFindBy<Product>("/by-sku/{sku}", "get-product-by-sku",
    nameof(Product.Sku))]
public partial class ProductBySku { }
```

Composta:

```csharp
[MapFindBy<OrderItem>("/{orderId}/items/{sku}", "get-order-item",
    nameof(OrderItem.OrderId), nameof(OrderItem.Sku))]
public partial class OrderItemDetails { }
```

Exija:

- ao menos uma propriedade direta, pública e legível;
- `nameof` sempre que possível;
- correspondência 1:1 propriedade ↔ placeholder, case-insensitive;
- nenhuma duplicação;
- tipo vinculável e igualdade válida;
- AND na ordem declarada;
- projeção `TEntity -> TDto` resolvível no provider;
- `NotFound` nomeando a entidade e contendo critérios.

Não valide regras gerais de route pattern que pertencem ao ASP.NET Core; valide apenas os vínculos interpretados
pelo generator.

### 16.3 Search

```csharp
[MapSearch("/", "search-products")]
[SearchReference<Product, ProductDetails>]
public sealed class ProductFilter { }
```

Use `[WithFilter]` para configurar `ICriteria<TEntity>`. Marque valores HTTP adicionais com `[WithParameter]`.
Não aplique `WithResultStatus` nem atributos de corpo de command em Find/Search.

## 17. Metadata, filtros e OpenAPI

```csharp
[WithSummary("Create product")]
[WithDescription("Creates a product")]
[WithAuthorization]
[WithPolicy("catalog.write")]
[WithTags("Products", "Catalog")]
[WithEndpointFilter<AuditFilter>]
[WithEndpointFilter<IdempotencyFilter>]
```

Exija:

- strings/tags/policies não vazios;
- ordem declarada de tags e filtros;
- filtro concreto, acessível e implementando `IEndpointFilter`;
- `[WithOpenApi]` no host, não no command;
- dependência OpenAPI instalada no consumidor.

Não mova regra de domínio para endpoint filter.

## 18. Diagnósticos — ação obrigatória

| Faixa/ID | Ação |
|---|---|
| `RCCMD000-RCCMD011` | corrija command, retorno, validação, entidade ou UoW |
| `RCCMD012-RCCMD023` | corrija host, result mapping, adapter, Find/Search |
| `RCCMD024-RCCMD032` | corrija retry, multiplicidade, nome reservado ou EditEntity |
| `RCCMD033-RCCMD042` | corrija binding, validator ou conflito de parâmetros |
| `RCCMD043-RCCMD050` | corrija transação, endpoint, response ou Created route |
| `RCCMD051-RCCMD056` | corrija filtro, status, Accepted route ou MapFindBy |

Consulte [`../diagnostics.md`](../diagnostics.md) antes de alterar a arquitetura para contornar um diagnóstico.

Trate como reservados quando usados no mesmo escopo gerado:

```text
command, ct, accessor, decorators, retryOptions
```

Não renomeie silenciosamente esses parâmetros e não suprima `RCCMD`.

## 19. Anti-padrões

- Não coloque mais de um `[Command]` na classe.
- Não use `async void`.
- Não use exceção para validação esperada.
- Não marque serviço de DI com `[WithParameter]`.
- Não presuma que `[WithParameter]` significa rota.
- Não coloque `CancellationToken` em método sync.
- Não combine adapters de UoW.
- Não use `WithTransaction` sem UoW.
- Não carregue manualmente a mesma entidade de `EditEntity`.
- Não coloque entidade em resposta HTTP quando um DTO é suficiente.
- Não execute validators dentro de transação/retry.
- Não coloque efeito externo não idempotente dentro de retry.
- Não chame `next` duas vezes por acidente em decorator.
- Não declare mais de um map principal de endpoint por classe.
- Não infira body em GET/DELETE.
- Não declare duas fontes de body.
- Não combine `MapIdResultValue` e `MapResponseValues`.
- Não combine `MapCreatedRoute` e `MapAcceptedRoute`.
- Não use `NoContent` com corpo.
- Não use `WithResultStatus` em Find/Search.
- Não materialize entidade antes da projeção de FindBy/Search.
- Não valide semântica geral de rota que pertence ao ASP.NET Core.
- Não edite `.g.cs`.
- Não ignore `RCCMD` ou `CS8785`.

## 20. Checklist antes de entregar o código

- [ ] Runtime e generator usam versões compatíveis.
- [ ] Existe uma classe por caso de uso e um `[Command]` por classe.
- [ ] O método é acessível ao handler gerado.
- [ ] Retorno e forma async são suportados.
- [ ] `CancellationToken` é propagado e não aparece em método sync.
- [ ] Falhas esperadas usam `Result`/`Result<T>`.
- [ ] `HasProblems` possui assinatura exata quando `WithValidateModel` é usado.
- [ ] Validators retornam `Result`, estão ordenados e não dependem de entidades/UoW.
- [ ] Payload, DI, entidades e `[WithParameter]` foram classificados corretamente.
- [ ] Binding explícito não conflita e toda rota referenciada existe.
- [ ] Apenas um adapter foi escolhido e registrado.
- [ ] `WithTransaction`, `EditEntity`, `ProduceNewEntity` e retry atendem pré-requisitos.
- [ ] Decorators, dependências e problem factories estão registrados.
- [ ] Host de DI e host HTTP são `static partial`.
- [ ] Existe no máximo um map principal de endpoint por classe e endpoint name é único.
- [ ] GET/DELETE não inferem body e não há dois bodies.
- [ ] Status, corpo, `Location` e problems formam contrato coerente.
- [ ] Find/FindBy/Search projetam para DTO no provider quando necessário.
- [ ] Metadata, filters, autorização e OpenAPI foram aplicados no nível correto.
- [ ] Todo `RCCMD` foi corrigido conscientemente.
- [ ] Nenhum `.g.cs` foi editado manualmente.
- [ ] Build, testes de generator e testes HTTP relevantes passaram.
