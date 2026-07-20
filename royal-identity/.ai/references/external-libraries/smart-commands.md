# Documentação da API SmartCommands

SmartCommands é uma família de bibliotecas .NET que transforma classes de caso de uso em handlers fortemente
tipados. Um incremental source generator cria interfaces, implementações, registro de DI e, quando solicitado,
endpoints Minimal API. A lógica do caso de uso continua independente de HTTP e da implementação concreta de
persistência.

Este é o guia conceitual e prático. Para instruções objetivas destinadas a ferramentas de IA, consulte também
[`smart-commands.ai-rules.md`](smart-commands.ai-rules.md).

> **Verificado contra:** `RoyalCode.SmartCommands` **0.1.0**, SmartProblems `1.0.0-preview-8.0`,
> SmartSelector `0.5.3` e Extensions.SourceGenerator `0.4.1` — runtimes .NET 8, 9 e 10; generator
> `netstandard2.0`, empacotado como analyzer.
> **Precedência das fontes:** documentação XML/IntelliSense da versão instalada >
> [`smart-commands.ai-rules.md`](smart-commands.ai-rules.md) > este guia.
> Se a versão instalada for diferente, confirme atributos, assinaturas geradas e diagnósticos no IDE.

Relacionados: [`problems.md`](problems.md) (`Problem`, `Result` e resultados HTTP),
[`validations.md`](validations.md) (`Rules` e `HasProblems`), [`selector.md`](selector.md) (projeção para DTO),
[`smartsearch.md`](smartsearch.md) (filtros e paginação) e [`workcontext.md`](workcontext.md) (persistência).

Sumário

1. Visão geral e conceitos
2. Pacotes, namespaces e instalação
3. Matriz de decisão
4. Início rápido
5. Comando e handler gerado
6. Parâmetros, serviços e binding HTTP
7. Validação do comando
8. Ordem do pipeline
9. Unit of Work e adapters
10. Carregamento, edição e criação de entidades
11. Decorators e mediator
12. Retry de concorrência
13. Registro dos handlers
14. Host e grupos de Minimal API
15. Mapeamento de commands
16. Status, corpo e `Location`
17. Leitura com `MapFind` e `MapFindBy`
18. Busca com `MapSearch`
19. Metadata, filtros e OpenAPI
20. Referência rápida da API
21. Diagnósticos
22. Erros comuns
23. Boas práticas

## 1. Visão geral e conceitos

Uma declaração curta:

```csharp
public sealed class VerificarSku
{
    public required string Sku { get; init; }

    [Command]
    internal Result<bool> Executar(IProdutos produtos) =>
        produtos.SkuDisponivel(Sku);
}
```

produz conceitualmente:

```csharp
public interface IVerificarSkuHandler
{
    Task<Result<bool>> HandleAsync(
        VerificarSku command,
        CancellationToken ct = default);
}

internal sealed class VerificarSkuHandler : IVerificarSkuHandler
{
    // dependências são recebidas por DI e o método delega ao comando
}
```

O contrato exato varia conforme retorno, parâmetros externos, persistência, entidades e decorators. Os conceitos
centrais são:

| Conceito | Papel |
|---|---|
| command | classe que contém os dados e a lógica de um caso de uso |
| método `[Command]` | ponto de execução; deve existir exatamente um por classe |
| handler gerado | interface `I{Command}Handler` e implementação `{Command}Handler` |
| parâmetro do payload | propriedade da classe do command |
| dependência | parâmetro do método resolvido por DI |
| `[WithParameter]` | valor externo ao payload, exposto no handler e no delegate HTTP |
| accessor | contrato neutro usado pelo handler para carregar e persistir entidades |
| map | atributo que expõe o caso de uso como Minimal API |
| host gerado | classe `partial` que registra handlers ou grupos de endpoints |

O handler é a unidade reutilizável. O endpoint HTTP é apenas uma borda opcional que instancia o command, chama o
handler e converte `Result`/`Result<T>` para a resposta apropriada.

## 2. Pacotes, namespaces e instalação

Referencie o runtime, o generator e os adapters de persistência necessários. Uma aplicação pode usar os dois
adapters em casos de uso diferentes, mas cada command seleciona no máximo um deles:

```xml
<ItemGroup>
  <PackageReference Include="RoyalCode.SmartCommands" Version="0.1.0" />
  <PackageReference Include="RoyalCode.SmartCommands.Generators"
                    Version="0.1.0"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />

  <!-- instale um ou ambos conforme os commands da aplicação -->
  <PackageReference Include="RoyalCode.SmartCommands.EntityFramework" Version="0.1.0" />
  <PackageReference Include="RoyalCode.SmartCommands.WorkContext" Version="0.1.0" />
</ItemGroup>
```

| Necessidade | Namespace | Pacote |
|---|---|---|
| atributos, handlers, mediator e accessors | `RoyalCode.SmartCommands` | `RoyalCode.SmartCommands` |
| geração e diagnósticos `RCCMD*` | nenhum namespace consumidor | `RoyalCode.SmartCommands.Generators` |
| adapter direto para `DbContext` | `RoyalCode.SmartCommands.EntityFramework.Extensions` | `RoyalCode.SmartCommands.EntityFramework` |
| adapter para `IWorkContext` e retry | `RoyalCode.SmartCommands.WorkContext.Extensions` | `RoyalCode.SmartCommands.WorkContext` |
| `Result`, `Problems`, `Id` e `FindResult` | `RoyalCode.SmartProblems` e `.Entities` | dependências SmartProblems |

O generator é dependência de compilação, não assembly de runtime. O runtime traz
`RoyalCode.SmartProblems.ApiResults` transitivamente porque os endpoints gerados usam `OkMatch`, `CreatedMatch`,
`AcceptedMatch`, `NoContentMatch` e metadata `ProduceProblems`. Adicione `RoyalCode.SmartProblems.Http` somente
quando usar explicitamente seus filtros ou outras integrações HTTP.

## 3. Matriz de decisão

| Necessidade | Use |
|---|---|
| executar um caso de uso por handler | um método `[Command]` |
| validar propriedades do command | `[WithValidateModel]` + `HasProblems(out Problems?)` |
| consultar serviço durante validação | método `[CommandValidation]` |
| receber valor que não pertence ao JSON | parâmetro `[WithParameter]` |
| carregar entidades sem salvar | `[WithFindEntities<TContext>]` |
| salvar com abstração própria | `[WithUnitOfWork<TContext>]` |
| salvar diretamente com EF Core | `[WithDbContext]` + adapter EF |
| salvar via WorkContext | `[WithWorkContext]` + adapter WorkContext |
| exigir transação | `[WithTransaction]` junto de uma UoW |
| editar uma entidade pelo id da rota | `[EditEntity<TEntity,TId>]` |
| retornar e adicionar uma entidade nova | `[ProduceNewEntity]` |
| aplicar comportamento transversal | `[WithDecorators]` + `IDecorator<,>` |
| repetir conflito otimista | `[WithRetryOnConcurrency]` + `[WithWorkContext]` |
| expor command em HTTP | um `MapGet/Post/Put/Patch/Delete` |
| buscar DTO por id | `MapFind` + `EntityReference<TEntity,TId>` |
| buscar DTO por chave alternativa/composta | `MapFindBy<TEntity>` |
| listar, filtrar, ordenar e paginar | `MapSearch` + `SearchReference` |
| mudar status de sucesso | `WithResultStatus(HttpResultStatus.*)` |
| montar `Location` de 201/202 | `MapCreatedRoute` / `MapAcceptedRoute` |
| retornar somente id ou propriedades | `MapIdResultValue` / `MapResponseValues` |

Não selecione uma UoW apenas porque existe um endpoint. Commands somente de cálculo ou integração podem usar o
handler e o mapeamento HTTP sem persistência.

## 4. Início rápido

Command:

```csharp
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace MyApp.Products;

[MapGroup("products")]
[MapPost("/", "create-product")]
[WithSummary("Create a product")]
[WithResultStatus(HttpResultStatus.Created)]
[MapCreatedRoute("{id}", nameof(Product.Id))]
public partial class CreateProduct
{
    public string? Name { get; init; }
    public decimal Price { get; init; }

    public bool HasProblems(out Problems? problems) =>
        Rules.Set<CreateProduct>()
            .NotEmpty(Name)
            .GreaterThanOrEqual(Price, 0)
            .HasProblems(out problems);

    [Command, WithValidateModel, WithDbContext, ProduceNewEntity]
    internal Result<Product> Execute()
    {
        WasValidated();
        return new Product(Name, Price);
    }
}
```

Host:

```csharp
[AddHandlersServices("Application"), MapApiHandlers, WithOpenApi]
public static partial class ApplicationEndpoints { }
```

Registro e pipeline:

```csharp
builder.Services.AddApplicationHandlersServices<AppDbContext>();
builder.Services.AddUnitOfWorkAccessor<AppDbContext>();

var app = builder.Build();
app.MapProductsGroup();
```

O nome da extensão de DI é `Add{title}HandlersServices`; título vazio gera `AddHandlersServices`. Cada
`MapGroup("products")` gera uma extensão semelhante a `MapProductsGroup()`.

## 5. Comando e handler gerado

### 5.1 Declaração do command

Regras estruturais:

- declare exatamente um método `[Command]` por classe;
- mantenha o método acessível ao handler gerado; `internal` é uma escolha comum quando ele não faz parte da API pública;
- use uma classe `partial` quando precisar de `WasValidated()`;
- não coloque estado scoped ou mutável compartilhado em membros estáticos;
- propriedades da classe representam o modelo/payload; parâmetros do método têm papéis de infraestrutura,
  entidade, serviço ou valor externo.

### 5.2 Retornos suportados

O método pode ser síncrono ou assíncrono e trabalhar com:

| Forma do command | Resultado conceitual do handler |
|---|---|
| `void` / `Task` / `ValueTask` | operação sem valor |
| `T` / `Task<T>` / `ValueTask<T>` | operação com valor |
| `Result` / formas async | sucesso ou problemas sem valor |
| `Result<T>` / formas async | sucesso com valor ou problemas |

`ValueTask` é aguardado e normalizado pelo contrato gerado. Prefira `Result` e `Result<T>` quando o caso de uso
possui falhas esperadas. Exceções continuam reservadas para cancelamento, configuração inválida e falhas
inesperadas.

Um `CancellationToken` só é válido em método assíncrono. Nunca use `async void`.

### 5.3 Nomes e arquivos gerados

Para `CreateProduct`, espere `ICreateProductHandler` e `CreateProductHandler`. Arquivos físicos usam um prefixo
legível e um hash Base32 determinístico de oito caracteres, por exemplo:

```text
ICreateProductHandler.ABC234DE.g.cs
CreateProductHandler.XYZ567FG.g.cs
```

O hash usa a identidade completa para separar tipos homônimos. Não dependa do hash no código e não edite `.g.cs`.

## 6. Parâmetros, serviços e binding HTTP

### 6.1 Papéis dos parâmetros

O generator classifica parâmetros pelo uso:

| Forma | Papel |
|---|---|
| `CancellationToken ct` | cancelamento, propagado pelo handler e endpoint |
| `[WithParameter] T value` | argumento público adicional do handler/delegate |
| parâmetro reconhecido como entidade | carregado antes do command |
| contexto da UoW | fornecido pelo accessor quando aplicável |
| demais dependências | resolvidas pelo construtor do handler via DI |

`[IsEntity]` pode tornar explícito o papel de uma propriedade ou parâmetro quando a detecção automática não é
suficiente. `[WithParameter]` não pode marcar entidade, coleção de entidades ou contexto.

### 6.2 `WithParameter` não significa rota

```csharp
[Command]
internal Task<Result> ExecuteAsync(
    [WithParameter] string source,
    IImporter importer,
    CancellationToken ct) =>
    importer.ImportAsync(source, ct);
```

`source` entra na assinatura do handler e do delegate Minimal API. A fonte HTTP continua sendo determinada pelo
template e pelas regras do ASP.NET Core. Quando necessário, use um atributo de binding explícito:

```csharp
internal Task<Result> ExecuteAsync(
    [WithParameter, FromHeader(Name = "X-Source")] string source,
    CancellationToken ct);
```

O atributo ASP.NET é copiado para o delegate HTTP; a interface do handler permanece independente de HTTP.

### 6.3 Regras de binding do endpoint

- parâmetros da rota podem vir do `MapGroup` ou do map do command;
- `[FromRoute(Name = "...")]` deve apontar para placeholder existente;
- não combine mais de uma fonte (`FromRoute`, `FromQuery`, `FromBody`, `FromForm`, `FromServices`) no mesmo parâmetro;
- `[AsParameters]` não é suportado em parâmetros externos de command;
- GET e DELETE não podem inferir body;
- um command complexo costuma ser o body implícito de POST/PUT/PATCH;
- `BindAsync`, `TryParse` e tipos especiais de Minimal API seguem o binding do ASP.NET Core;
- não declare dois corpos, por exemplo command implícito e outro `[FromBody]`.

## 7. Validação do comando

### 7.1 `WithValidateModel` e `HasProblems`

```csharp
public partial class RenameProduct
{
    public string? Name { get; init; }

    public bool HasProblems(out Problems? problems) =>
        Rules.Set<RenameProduct>()
            .NotEmpty(Name)
            .HasProblems(out problems);

    [Command, WithValidateModel]
    internal Result Execute()
    {
        WasValidated();
        return Result.Ok();
    }
}
```

O método deve retornar `bool` e possuir `out Problems?`. Quando a classe é `partial`, o generator pode emitir
`WasValidated()` para informar ao analisador de nulabilidade que o fluxo passou pela validação. A chamada não
substitui `HasProblems`; ela apenas melhora o estado nullable dentro do método.

### 7.2 Validações adicionais

Use `[CommandValidation]` para regras que dependem de serviços ou I/O:

```csharp
[CommandValidation(Order = 5)]
internal Task<Result> ValidateSkuAsync(
    ISkuRegistry registry,
    [WithParameter] string tenant,
    CancellationToken ct) =>
    registry.ValidateAsync(tenant, Sku, ct);
```

Contratos:

- retorno: `Result`, `Task<Result>` ou `ValueTask<Result>`;
- default de `Order`: 10; menor valor executa primeiro;
- empate não tem precedência pública, apenas desempate determinístico;
- o primeiro resultado com problemas encerra o handler;
- dependências vêm da DI;
- valores externos exigem `[WithParameter]`;
- entidades, contextos e accessors não estão disponíveis nessa etapa;
- `CancellationToken` só aparece em validator assíncrono;
- validators executam uma vez, inclusive quando o corpo entra em retry.

## 8. Ordem do pipeline

Ordem semântica completa:

```text
construção/binding do command
  -> RequireBody, quando aplicável
  -> HasProblems
  -> CommandValidation (Order crescente)
  -> BeginAsync da Unit of Work
  -> carregamento de entidades / EditEntity
  -> decorators
  -> método [Command]
  -> AddEntityAsync, quando ProduceNewEntity
  -> CompleteAsync da Unit of Work
  -> composição do resultado HTTP
```

Qualquer `Problems` de validação, carregamento ou conclusão encerra o fluxo apropriado. Cancelamento e exceções
inesperadas atravessam a borda; os adapters não devem convertê-las silenciosamente em sucesso.

Com retry de concorrência, validações ficam fora do laço. `BeginAsync`, carregamento, decorators, command e
`CompleteAsync` podem ser repetidos.

## 9. Unit of Work e adapters

### 9.1 Escolhendo a forma

| Atributo | Serviço gerado | Implementação típica |
|---|---|---|
| `[WithUnitOfWork<TContext>]` | `IUnitOfWorkAccessor<TContext>` | adapter próprio ou qualquer implementação registrada |
| `[WithDbContext]` | accessor do `DbContext` | `RoyalCode.SmartCommands.EntityFramework` |
| `[WithWorkContext]` | accessor do `IWorkContext` | `RoyalCode.SmartCommands.WorkContext` |
| `[WithFindEntities<TContext>]` | `IRepositoriesAccessor<TContext>` | leitura sem ciclo begin/complete |

As três seleções de UoW são mutuamente exclusivas. `WithFindEntities<T>` é a alternativa de leitura quando o
command precisa carregar entidades, mas não salvar.

### 9.2 Contrato do accessor

```csharp
public interface IUnitOfWorkAccessor<out T> : IRepositoriesAccessor<T>
{
    ValueTask BeginAsync(bool requireTransaction, CancellationToken ct);
    Task<Result> CompleteAsync(CancellationToken ct);
}
```

`IRepositoriesAccessor<T>` expõe o contexto, busca entidades por id e adiciona novas entidades. O adapter abre
transação quando sua opção global está habilitada ou quando `requireTransaction` é `true`.

### 9.3 Adapter direto de EF Core

```csharp
builder.Services.AddUnitOfWorkAccessor<AppDbContext>();
```

Também existe a forma base/implementação:

```csharp
builder.Services.AddUnitOfWorkAccessor<BaseDbContext, AppDbContext>();
```

`DbContextAdapterOptions.BeginTransactions` é `false` por default. Sem transação explícita, o adapter ainda usa
tracking e `SaveChangesAsync`; `[WithTransaction]` força a transação daquele command.

### 9.4 Adapter WorkContext

```csharp
builder.Services.AddWorkContext<AppDbContext>()
    .AddUnitOfWorkAccessor();
```

Também é possível registrar diretamente um `TWorkContext` já configurado. Consulte
[`workcontext.md`](workcontext.md) para providers, repositórios e selectors.

## 10. Carregamento, edição e criação de entidades

### 10.1 Carregamento por convenção

Com `[WithFindEntities<TContext>]` ou uma UoW, parâmetros de entidade e coleções podem ser carregados a partir de
ids correspondentes no command. Se a entidade não existir, o handler retorna `NotFound` e não executa o método.

Use `[IsEntity]` somente quando precisar tornar o papel explícito. Mantenha nomes de ids e entidades claros para
que o vínculo seja inequívoco.

### 10.2 `EditEntity<TEntity,TId>`

```csharp
[MapGroup("products")]
[MapPut("/{id:guid}", "edit-product")]
public sealed class EditProduct
{
    public string? Name { get; init; }

    [Command, WithDbContext, EditEntity<Product, Guid>]
    internal Result Execute(Product product) =>
        product.Rename(Name);
}
```

A entidade carregada deve ser o primeiro parâmetro. A variável de rota é resolvida nesta ordem:

1. `RouteParameterName` explícito;
2. única variável disponível no grupo + map;
3. `{nomeDoParametroId}`;
4. `{nomeDoParametro}`.

Com múltiplas variáveis sem correspondência única, declare:

```csharp
[Command, WithDbContext,
 EditEntity<Product, Guid>(RouteParameterName = "productId")]
```

Variável opcional, catch-all ou constraint incompatível com o id produz diagnóstico.

### 10.3 `ProduceNewEntity`

`[ProduceNewEntity]` informa que o valor de sucesso é uma entidade nova a ser adicionada pelo accessor antes da
conclusão. Ele exige UoW e, quando o command retorna `Result`, a forma deve carregar a entidade (`Result<TEntity>`).

O atributo controla persistência; `MapIdResultValue`, `MapResponseValues` e os status HTTP controlam somente a
resposta.

## 11. Decorators e mediator

Ative o pipeline:

```csharp
[Command, WithDecorators]
internal Task<Result<Order>> ExecuteAsync(CancellationToken ct) => /* ... */;
```

Implemente e registre:

```csharp
public sealed class LoggingDecorator
    : IDecorator<CreateOrder, Result<Order>>
{
    public async Task<Result<Order>> HandleAsync(
        CreateOrder command,
        Func<Task<Result<Order>>> next,
        CancellationToken ct)
    {
        // antes
        var result = await next();
        // depois
        return result;
    }
}

services.AddTransient<IDecorator<CreateOrder, Result<Order>>, LoggingDecorator>();
```

Decorators executam na ordem de registro; o primeiro é o mais externo. O `Mediator<TModel,TResult>` compõe a
cadeia uma vez por handler invocation, sem enumerador retido nem posição mutável compartilhada. Chamar `next`
mais de uma vez reexecuta deterministicamente o restante do pipeline — faça isso somente quando essa for a
semântica desejada.

## 12. Retry de concorrência

`[WithRetryOnConcurrency]` é exclusivo do adapter WorkContext:

```csharp
[Command, WithWorkContext, WithRetryOnConcurrency]
internal Result Execute(Product product) => /* ... */;
```

Formas:

```csharp
[WithRetryOnConcurrency]                    // opções; default 3 tentativas
[WithRetryOnConcurrency(5)]                 // máximo explícito
[WithRetryOnConcurrency("products.rename")]
[WithRetryOnConcurrency("products.rename", 5)]
```

`MaxAttempts` conta a execução inicial e os retries. Valores menores que 1 são inválidos no atributo. Não existe
backoff automático: este é retry curto de concorrência otimista, não retry de falha transitória.

Registre o comportamento ao esgotar:

```csharp
services.AddConcurrencyRetryProblem<RenameProduct>(
    "products.rename",
    static (command, context) => Problems.InvalidState(
        "The product changed while it was being edited.",
        typeId: "products.concurrency_conflict"));
```

Há overloads com delegates, serviços e providers. Sem customização, `IConcurrencyRetryProblemFactory` usa
`RetryOnConcurrencyOptions.ExhaustedProblemTypeId` e `ExhaustedProblemDetail`. Cancelamento não vira conflito.

## 13. Registro dos handlers

Declare uma classe estática parcial:

```csharp
[AddHandlersServices("Catalog")]
public static partial class CatalogServices { }
```

O generator produz:

```csharp
services.AddCatalogHandlersServices<TContext>(); // quando handlers exigem contexto
```

Título vazio produz `AddHandlersServices`. A extensão registra cada interface e implementação geradas. Ainda é
responsabilidade da aplicação registrar:

- o adapter de UoW/repositório;
- dependências usadas pelo command e validators;
- decorators;
- filtros de endpoint;
- serviços de SmartSearch/selector;
- factories de problema de concorrência.

## 14. Host e grupos de Minimal API

Declare um único host por projeto:

```csharp
[MapApiHandlers, WithOpenApi]
public static partial class ApiEndpoints { }
```

O host reúne todos os maps de endpoint válidos. Commands sem `MapGet`, `MapPost`, `MapPut`, `MapPatch` ou
`MapDelete` continuam gerando handlers, mas não endpoints.

Com grupo:

```csharp
[MapGroup("products")]
[MapGet("/{id}", "get-product")]
public sealed class GetProduct { }
```

é gerada uma classe/extensão semelhante a:

```csharp
RouteGroupBuilder group = app.MapProductsGroup();
```

Sem `MapGroup`, o endpoint é mapeado diretamente no host e não recebe prefixo implícito. Placeholders do grupo
participam do binding e da resolução de ids.

Cada classe aceita no máximo um atributo principal de endpoint (`MapGet/Post/Put/Patch/Delete`, `MapFind`,
`MapFindBy` ou `MapSearch`), e cada endpoint name deve ser não vazio e globalmente único. Atributos auxiliares
como `MapGroup`, `MapCreatedRoute`, `MapAcceptedRoute`, `MapIdResultValue` e `MapResponseValues` complementam
esse map principal e não contam como outro endpoint.

## 15. Mapeamento de commands

Verbos disponíveis:

```csharp
[MapGet("/preview/{id}", "preview-product")]
[MapPost("/", "create-product")]
[MapPut("/{id}", "replace-product")]
[MapPatch("/{id}", "rename-product")]
[MapDelete("/{id}", "delete-product")]
```

Escolha o verbo conforme a semântica HTTP, não conforme o nome do método C#. O ASP.NET Core continua responsável
por interpretar o route pattern; o generator valida somente os vínculos que precisa compreender para emitir
código seguro.

Inferência de body:

- POST/PUT/PATCH normalmente recebem o command como body;
- GET/DELETE não inferem body;
- tipos com `BindAsync`/`TryParse` válidos e tipos especiais seguem o binder do ASP.NET Core;
- `[FromBody]` ou `[FromForm]` explícito deve ser a única fonte de body.

## 16. Status, corpo e `Location`

### 16.1 Status de sucesso

Sem seleção explícita, o generator preserva a inferência baseada no verbo e no retorno. Para tornar o contrato
intencional, use:

| Atributo | Sucesso | Corpo |
|---|---:|---|
| `WithResultStatus(Ok)` | 200 | valor de `Result<T>`/`T`, quando existe |
| `WithResultStatus(Created)` | 201 | valor opcional; `Location` somente com `MapCreatedRoute` |
| `WithResultStatus(Accepted)` | 202 | sem corpo para `Result`; valor para `Result<T>` |
| `WithResultStatus(NoContent)` | 204 | nenhum; descarta somente o valor de sucesso |

Problems nunca são descartados: continuam usando seus status e `ProblemDetails` correspondentes.

DELETE sem valor infere 204; DELETE com valor infere 200. `WithResultStatus(Ok)` pode tornar essa decisão explícita.

### 16.2 `Location` de Created e Accepted

```csharp
[WithResultStatus(HttpResultStatus.Created)]
[MapCreatedRoute("{id}", nameof(Product.Id))]
```

```csharp
[WithResultStatus(HttpResultStatus.Accepted)]
[MapAcceptedRoute("jobs/{id}", nameof(JobTicket.Id))]
```

Regras:

- placeholders são nomeados (`{id}`), não posicionais (`{0}`);
- nomes casam com as propriedades declaradas sem diferenciar caixa;
- uma rota com placeholders exige valor de sucesso do qual ler as propriedades;
- `Result` sem valor só pode usar rota estática, como `MapAcceptedRoute("jobs")`;
- grupo e rota são unidos normalizando a barra;
- `MapCreatedRoute` e `MapAcceptedRoute` são mutuamente exclusivos;
- `Created` e `Accepted` podem existir sem `Location`.

### 16.3 Forma do corpo

```csharp
[MapIdResultValue]
```

retorna o `Id` do valor de sucesso. Para múltiplas propriedades:

```csharp
[MapResponseValues(nameof(Product.Id), nameof(Product.Name))]
```

Os dois atributos são mutuamente exclusivos. Propriedades devem ser públicas, existir no tipo retornado e não
se repetir. `NoContent` não pode ser combinado com projeção de corpo.

## 17. Leitura com `MapFind` e `MapFindBy`

### 17.1 Busca por id

```csharp
[MapGroup("products")]
[MapFind("/{id:guid}", "get-product")]
[EntityReference<Product, Guid>]
public partial class ProductDetails { }
```

O endpoint recebe `Id<Product,Guid>`, usa `IRepositoryAccessor<Product>`, projeta para `ProductDetails` e retorna
`NotFound` quando necessário. A rota deve oferecer um id compatível no map ou no grupo.

### 17.2 Chave alternativa ou composta

Chave simples:

```csharp
[MapGroup("products")]
[MapFindBy<Product>("/by-sku/{sku}", "get-product-by-sku",
    nameof(Product.Sku))]
public partial class ProductBySku { }
```

Chave composta:

```csharp
[MapGroup("orders")]
[MapFindBy<OrderItem>("/{orderId:guid}/items/{productSku}", "get-order-item",
    nameof(OrderItem.OrderId),
    nameof(OrderItem.ProductSku))]
public partial class OrderItemDetails { }
```

Regras:

- declare uma ou mais propriedades diretas, públicas e legíveis da entidade;
- prefira `nameof`;
- cada propriedade casa com exatamente um placeholder de mesmo nome, sem diferenciar caixa;
- propriedades e placeholders não podem se repetir;
- parâmetros são tipados a partir das propriedades da entidade;
- critérios são combinados com `AND`, na ordem declarada;
- a projeção `TEntity -> TDto` acontece no provider, sem materializar/rastrear a entidade;
- duplicidades usam a semântica `FirstOrDefault` do repositório;
- `NotFound` nomeia a entidade e inclui os critérios;
- o generator não substitui o parser/validador geral de rotas do ASP.NET Core.

O DTO precisa possuir uma projeção resolvível pelo ecossistema, normalmente a expressão gerada pelo
SmartSelector. `MapFindBy` não materializa a entidade para depois mapear.

## 18. Busca com `MapSearch`

Retornando entidade:

```csharp
[MapGroup("products")]
[MapSearch("/", "search-products")]
[SearchReference<Product>]
public sealed class ProductFilter { }
```

Projetando DTO:

```csharp
[MapSearch("/", "search-products")]
[SearchReference<Product, ProductDetails>]
public sealed class ProductFilter { }
```

O endpoint integra filtro, `SearchOptions`, sorting, `ICriteria<TEntity>` e resultados paginados do SmartSearch.
Para ajustar critérios antes da execução, declare método `[WithFilter]` na classe de filtro. Seus parâmetros
externos precisam de `[WithParameter]`; bindings explícitos são copiados ao delegate.

`WithResultStatus`, `MapIdResultValue`, `MapResponseValues` e rotas de Created/Accepted pertencem a command maps,
não a Find/Search.

Consulte [`smartsearch.md`](smartsearch.md) para operadores, ordenação, paginação, operation hints e resolução de
selectors.

## 19. Metadata, filtros e OpenAPI

Metadata comum aos maps:

```csharp
[WithSummary("Create product")]
[WithDescription("Creates a product in the catalog")]
[WithAuthorization]
[WithPolicy("catalog.write")]
[WithTags("Products", "Catalog")]
[WithEndpointFilter<AuditFilter>]
[WithEndpointFilter<IdempotencyFilter>]
```

Regras:

- summary, description, policies e tags não podem ser nulos/vazios;
- tags preservam a ordem declarada;
- filtros são repetíveis e preservam a ordem;
- `TFilter` deve ser classe concreta acessível que implementa `IEndpointFilter`;
- tipos aninhados acessíveis e genéricos construídos são aceitos;
- `[WithOpenApi]` é colocado na classe `[MapApiHandlers]` e exige a integração OpenAPI disponível no consumidor;
- metadata de problemas é derivada dos fluxos possíveis do handler e validators, sem duplicatas.

## 20. Referência rápida da API

### 20.1 Atributos do command

| Atributo | Alvo | Efeito |
|---|---|---|
| `Command` | método | seleciona a operação do caso de uso |
| `WithValidateModel` | método | chama `HasProblems` |
| `CommandValidation(Order=10)` | método auxiliar | adiciona validação síncrona/assíncrona |
| `WithParameter` | parâmetro | expõe valor externo no handler/delegate |
| `IsEntity` | parâmetro/propriedade | força papel de entidade |
| `WithFindEntities<T>` | método | carrega entidades sem ciclo completo de UoW |
| `WithUnitOfWork<T>` | método | usa accessor genérico |
| `WithDbContext` | método | usa adapter EF direto |
| `WithWorkContext` | método | usa adapter WorkContext |
| `WithTransaction` | método | força transação na UoW |
| `EditEntity<TEntity,TId>` | método | carrega entidade editada pelo id |
| `ProduceNewEntity` | método | adiciona a entidade retornada |
| `WithDecorators` | método | ativa decorators registrados |
| `WithRetryOnConcurrency` | método | repete trecho transacional em conflito |

### 20.2 Atributos HTTP

| Grupo | Atributos |
|---|---|
| host | `MapApiHandlers`, `AddHandlersServices`, `WithOpenApi` |
| rota | `MapGroup`, `MapGet`, `MapPost`, `MapPut`, `MapPatch`, `MapDelete` |
| leitura | `MapFind`, `EntityReference`, `MapFindBy`, `MapSearch`, `SearchReference` |
| corpo/status | `WithResultStatus`, `MapIdResultValue`, `MapResponseValues` |
| location | `MapCreatedRoute`, `MapAcceptedRoute` |
| metadata | `WithSummary`, `WithDescription`, `WithAuthorization`, `WithPolicy`, `WithTags` |
| extensibilidade | `WithEndpointFilter<TFilter>`, `WithFilter` |

### 20.3 Contratos runtime

| Contrato | Papel |
|---|---|
| `IUnitOfWorkAccessor<T>` | begin/complete e repositórios de uma UoW |
| `IRepositoriesAccessor<T>` | contexto, find por id e add |
| `IRepositoryAccessor<TEntity>` | find de entidade/DTO por id ou filtro |
| `IDecorator<TCommand,TResult>` | comportamento ao redor do command |
| `Mediator<TCommand,TResult>` | composição interna dos decorators |
| `IConcurrencyRetryProblemFactory` | problema default ao esgotar retry |
| `IConcurrencyRetryProblemProvider<T>` | customização tipada do problema |

## 21. Diagnósticos

Entradas inválidas produzem diagnóstico `RCCMD` localizado e bloqueiam somente a fonte relacionada. O generator
não deve emitir código parcial inválido nem falhar com `CS8785`.

Grupos mais comuns:

| Faixa/ID | Tema |
|---|---|
| `RCCMD000-RCCMD011` | command, retorno, validação e entidades |
| `RCCMD012-RCCMD023` | host, respostas, adapters, Find e Search |
| `RCCMD024-RCCMD032` | retry, multiplicidade, nomes reservados e EditEntity |
| `RCCMD033-RCCMD042` | binding, validators e conflitos de parâmetros |
| `RCCMD043-RCCMD050` | transação, completude dos endpoints, response e Created route |
| `RCCMD051-RCCMD056` | endpoint filters, status explícito, Accepted route e MapFindBy |

Nomes como `command`, `ct`, `accessor`, `decorators` e `retryOptions` são reservados quando o generator precisa
emiti-los no mesmo escopo. O generator diagnostica a colisão em vez de renomear silenciosamente.

Consulte o [catálogo RCCMD000-RCCMD056](../diagnostics.md) para mensagem, causa e correção de cada ID.

## 22. Erros comuns

### 22.1 Colocar dois métodos `[Command]` na mesma classe

Cada método seria um handler concorrente para o mesmo tipo. Separe os casos de uso em classes distintas.

### 22.2 Interpretar `WithParameter` como `FromRoute`

`WithParameter` só separa o valor do payload. Para binding explícito, use o atributo ASP.NET correspondente e
garanta que o placeholder exista.

### 22.3 Usar `CancellationToken` em método síncrono

Torne o método assíncrono ou remova o token. Não adicione `async` sem operação aguardada apenas para silenciar o
diagnóstico.

### 22.4 Misturar adapters

Não combine `WithUnitOfWork<T>`, `WithDbContext` e `WithWorkContext`. Escolha o contrato que corresponde ao
registro real da aplicação.

### 22.5 Usar `WithTransaction` sem UoW

O atributo não cria accessor. Adicione uma forma de UoW; ele apenas passa `requireTransaction: true` ao begin.

### 22.6 Colocar serviço em `[WithParameter]`

Dependência de aplicação deve ser parâmetro normal e vir da DI. Marque apenas valores que o chamador do handler
precisa fornecer.

### 22.7 Carregar entidade manualmente dentro de `EditEntity`

O handler já carrega e retorna `NotFound`. Use o primeiro parâmetro recebido e mantenha a resolução de rota
inequívoca.

### 22.8 Executar regra de negócio em endpoint filter

Filtros são borda HTTP. Regras reutilizáveis pertencem ao command, `HasProblems`, `CommandValidation` ou decorator.

### 22.9 Usar `MapFindBy` sem projeção de DTO

O contrato exige projeção no provider. Gere uma expressão com SmartSelector ou forneça selector compatível; não
materialize a entidade como fallback.

### 22.10 Combinar forma de corpo e status incompatíveis

`NoContent` não possui corpo; `MapIdResultValue` e `MapResponseValues` são exclusivos; Created/Accepted routes
exigem status coerente e não podem coexistir.

### 22.11 Editar arquivos `.g.cs`

Altere atributos, command, DTO ou configuração. Arquivos gerados são derivados e serão sobrescritos.

### 22.12 Tratar todo erro como `Result`

Validação, estado inválido e NotFound são falhas esperadas. Cancelamento, DI ausente, bug e indisponibilidade
inesperada continuam sendo exceções tratadas na borda apropriada.

## 23. Boas práticas

- Modele uma classe por caso de uso e um método `[Command]` por classe.
- Use nomes de command no imperativo e nomes de endpoint estáveis e únicos.
- Coloque dados do request em propriedades; use `[WithParameter]` somente para valores externos reais.
- Prefira `Result`/`Result<T>` e SmartValidations para falhas esperadas.
- Faça validators baratos primeiro; deixe I/O em `CommandValidation` assíncrono com `CancellationToken`.
- Mantenha validação fora de UoW e retry.
- Escolha um único adapter de persistência por command.
- Use `[WithTransaction]` somente quando atomicidade entre múltiplas operações exigir transação explícita.
- Carregue e altere agregados pelo handler; não exponha entidades em respostas HTTP.
- Projete Find, FindBy e Search no provider para DTOs enxutos.
- Use decorators para preocupações transversais reutilizáveis, sem esconder binding ou regra de domínio.
- Mantenha retry otimista curto e idempotente; não coloque efeitos externos não repetíveis dentro do trecho repetido.
- Declare status e `Location` explicitamente quando fazem parte do contrato público.
- Teste endpoints gerados por HTTP real e valide OpenAPI/ProblemDetails relevantes.
- Corrija todo `RCCMD`; não o suprima nem contorne o generator.

Checklist antes de entregar:

- [ ] Runtime e generator usam versões compatíveis.
- [ ] Existe exatamente um método `[Command]` por classe.
- [ ] Retorno síncrono/assíncrono e `CancellationToken` são coerentes.
- [ ] `HasProblems` e validators adicionais possuem contratos válidos.
- [ ] Parâmetros foram classificados corretamente entre payload, DI, entidade e `WithParameter`.
- [ ] Apenas um adapter de UoW foi selecionado e registrado.
- [ ] `WithTransaction`, `EditEntity`, `ProduceNewEntity` e retry atendem seus pré-requisitos.
- [ ] Decorators e dependências foram registrados.
- [ ] Cada classe possui no máximo um map principal de endpoint e endpoint name único.
- [ ] Rotas do grupo e do endpoint resolvem todos os parâmetros interpretados pelo generator.
- [ ] Status, corpo, `Location` e Problems são coerentes.
- [ ] Find/FindBy/Search projetam para DTO no provider quando aplicável.
- [ ] Metadata, filtros e OpenAPI foram verificados.
- [ ] Nenhum `RCCMD`, `CS8785` ou edição manual de `.g.cs` permanece.
- [ ] Build e testes relevantes foram executados.
