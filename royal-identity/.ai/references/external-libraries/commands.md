# Documentação da API SmartCommands (Commands, Handlers, Decorators, Mapeamento HTTP)

Esta documentação apresenta os conceitos, funcionalidades e exemplos práticos para usar a família de bibliotecas SmartCommands em projetos .NET.
Serve também como referência para ferramentas de IA (ex.: GitHub Copilot) compreenderem e gerarem código correto com base na API da biblioteca.

Projetos alvo: .NET 8, .NET 9 e .NET 10.

Escopo desta documentação:
- `RoyalCode.SmartCommands` (runtime): atributos e contratos para comandos e handlers.
- `RoyalCode.SmartCommands.EntityFramework`: adapters para EF Core (UnitOfWork/Repositories accessor).
- `RoyalCode.SmartCommands.WorkContext`: adapters para WorkContext (UnitOfWork/Repositories accessor e integração DI).
- `RoyalCode.SmartCommands.Generators` (analyzer .NET Standard 2.0): Source Generator que cria handlers e integrações a partir de atributos.

## 1. Introdução

SmartCommands padroniza a maneira de declarar comandos (métodos) em classes de aplicação e gerar automaticamente handlers fortemente tipados, com suporte a:
- Validação do modelo (`HasProblems` / SmartValidations) antes da execução.
- Unit of Work e acesso a repositórios (EF Core e WorkContext).
- Resolução e carregamento de entidades por ID, incluindo coleções.
- Composição via Decorators (pipeline executado antes/depois do comando).
- Mapeamento para endpoints HTTP (Minimal APIs) com metadados (rota, resumo, autorização) e respostas consistentes (Created/Ok/NoContent) integradas a SmartProblems.

Os handlers são gerados pelo Source Generator com base em atributos aplicados ao método do comando e à classe. O runtime expõe atributos leves usados apenas em tempo de compilação.

Benefícios principais:
- Reduz boilerplate de criação de handlers, DI e carregamento de entidades.
- Integra com EF Core e WorkContext via adapters.
- Fluxo de erros padronizado com `Result`/`Problems` (vide `.docs/problems.md`).
- Fácil composição com validações (`RuleSet`) e Decorators.

## 2. Pacotes e responsabilidades

- `RoyalCode.SmartCommands`:
  - `CommandAttribute`: marca o método como um comando elegível para geração.
  - `AddHandlersServicesAttribute`: aplique em uma classe **`static partial` dedicada** (NÃO na classe de comando — gera o diagnóstico `RCCMD000` caso contrário). Recebe um título obrigatório que é inserido **literalmente** no nome do método gerado; use um único token PascalCase **sem espaços**. Gera `Add{Titulo}HandlersServices<TContext>(this IServiceCollection)` que registra **todos** os handlers de comando (`I{Cmd}Handler`) do assembly.

- `RoyalCode.SmartCommands.EntityFramework`:
  - `CommandsDbContextExtensions.AddUnitOfWorkAccessor<TContext>()`: registra adapters `IUnitOfWorkAccessor<T>` e `IRepositoriesAccessor<T>` usando EF Core (`DbContext`).
  - Versão base/impl: `AddUnitOfWorkAccessor<TContextBase, TContextImpl>()`.

- `RoyalCode.SmartCommands.WorkContext`:
  - `CommandsWorkContextExtensions.AddUnitOfWorkAccessor<TDbContext>(IUnitOfWorkBuilder|IWorkContextBuilder)`: registra adapters para `IWorkContext` com `UnitOfWorkAccessor` e `RepositoryAdapter`.
  - `AddUnitOfWorkAccessor<TWorkContext>(IServiceCollection)`: registro direto por serviços.
  - `ConfigureWorkContextAdapterOptions(...)`: configuração de `WorkContextAdapterOptions`.

- `RoyalCode.SmartCommands.Generators`:
  - Gera interface `I{CommandClass}Handler` e implementação `{CommandClass}Handler` com base em atributos.
  - Aplica pipeline de validação, unidade de trabalho, carregamento de entidades (Find), Decorators e mapeamento HTTP.
  - Cria parcial `{CommandClass}_WasValidated.g.cs` para anotação de *null-state* quando houver metadados de validação.

## 3. Conceitos centrais

- Classe de Comando: classe que contém um método anotado com `CommandAttribute`. Opcionalmente parcial para habilitar geração de membros auxiliares.
- Método de Comando: implementa a lógica de domínio/aplicação; pode ser síncrono ou assíncrono. Pode retornar `void`, `Task`, `Result`, `Result<T>` ou `T`.
- Validação: método `HasProblems(out Problems?)` opcional na classe, integrado via atributo `WithValidateModel` (no método de comando) e gerando `WasValidated` quando aplicável.
- Unit of Work / Repositórios: habilitado via `WithUnitOfWork<TContext>` (ou `WithDbContext`/`WithWorkContext`), expondo a infraestrutura por `IUnitOfWorkAccessor<T>` e `IRepositoriesAccessor<T>`.
- Find Entities: vincula parâmetros de entidade/coleção às propriedades de ID da classe para carregamento automático antes do comando (`WithFindEntities<TContext>` ou implicitamente via UoW/Repo Accessor).
- Edit Entity: edita uma entidade existente informada pelo primeiro parâmetro do método e o ID passado ao handler (`EditEntity(entityType)`), com validação do tipo e binding.
- Produce New Entity: comando que cria uma nova entidade retornando `Result<T>`/`T` (o gerador valida tipos e retorna corretamente).
- Decorators: `WithDecorators` permite envolver a execução do comando com interceptores (pré/pós).
- Mapeamento HTTP: atributos `MapPost`/`MapPut`/`MapPatch`/`MapDelete`/`MapGet` na classe definem rota e nome do endpoint. Outros atributos agregam metadados: `WithDescription`, `WithSummary`, `WithAuthorization`, `WithPolicy`, `MapGroup`, `MapCreatedRoute`, `MapIdResultValue`, `MapResponseValues`.

Categorias de problemas (vide `.docs/problems.md`): `InvalidParameter`, `ValidationFailed`, `NotAllowed`, `InvalidState`, `NotFound`, `InternalServerError`, `CustomProblem`. Atributo `ProduceProblems(...)` nos métodos/validações lista categorias esperadas.

## 4. Fluxos e atributos suportados (overview funcional)

Principais atributos em métodos de comando:
- `Command`: marca o método como comando.
- `WithValidateModel`: aciona validação via `HasProblems` antes da execução.
- `WithDecorators`: compõe execução via `IDecorator<TCommand, TResult>`; requer retorno não-`void` (gerador valida).
- `WithUnitOfWork<TContext>`: injeta `IUnitOfWorkAccessor<TContext>` e habilita begin/complete no handler.
- `WithDbContext`: similar a UoW mas baseado em `DbContext` genérico.
- `WithWorkContext`: similar a UoW mas baseado em `IWorkContext`.
- `WithFindEntities<TContext>`: habilita lookup de entidades por ID/coleções quando não há UoW.
- `ProduceNewEntity`: comando retorna entidade nova (gera fluxo de `Result<T>` quando aplicável).
- `EditEntity(entityType)`: comando edita entidade existente; primeiro parâmetro deve ser do tipo da entidade.
- `MapIdResultValue`: mapeia campo `Id` do valor retornado para rota/resposta.
- `MapResponseValues(params string[])`: seleciona propriedades do valor retornado para compor resposta.
- `ProduceProblems(params ProblemCategory[])`: declara problemas esperados.

Principais atributos na classe de comando (mapeamento HTTP):
- `MapPost(routePattern, endpointName)` e equivalentes para outros verbos.
- `MapGroup(groupName)`: agrupa endpoints.
- `MapCreatedRoute(routePattern, params string[] idProperties)`: constrói `Location` em 201.
- `WithDescription(text)` / `WithSummary(text)`: metadados de documentação.
- `WithAuthorization` / `WithPolicy(params string[] policies)`: exigem autenticação/política.

Validações automáticas do Generator:
- Proíbe generics na classe/método de comando.
- Valida retorno quando `WithDecorators` (não `void` puro).
- Incompatibilidades: `WithDbContext` não pode coexistir com `WithUnitOfWork`; `WithWorkContext` não pode coexistir com `WithUnitOfWork`/`WithDbContext`.
- `ProduceNewEntity` requer UoW.
- `EditEntity` requer UoW e primeiro parâmetro do tipo indicado.
- `CancellationToken` só em métodos assíncronos.
- Binding obrigatório para parâmetros de entidade/coleção às propriedades `Id`/`Ids` na classe.

## 5. Adapters e DI (EntityFramework e WorkContext)

- EF Core (`RoyalCode.SmartCommands.EntityFramework`):
  - Registro:
    - `services.AddUnitOfWorkAccessor<MyDbContext>();`
    - ou `services.AddUnitOfWorkAccessor<MyBaseContext, MyContextImpl>();`
  - Expõe `DbContextAccessor<TContext>` como implementação de `IUnitOfWorkAccessor<TContext>` e `IRepositoriesAccessor<TContext>`.

- WorkContext (`RoyalCode.SmartCommands.WorkContext`):
  - Registro em `IUnitOfWorkBuilder<TDbContext>` ou `IWorkContextBuilder<TDbContext>`:
    - `builder.AddUnitOfWorkAccessor<TDbContext>(opt => { /* WorkContextAdapterOptions */ });`
  - Registro direto:
    - `services.AddUnitOfWorkAccessor<IWorkContext>();` (ou genérico para tipo de `IWorkContext<TDbContext>`)
  - Configuração:
    - `services.ConfigureWorkContextAdapterOptions(opt => { /* opções */ });`

## 6. Exemplos reais de uso

### 6.1 Classe de comando com validação e UoW
```csharp
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

public partial class CreateProduct
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    // Validação de entrada
    public bool HasProblems(out Problems? problems)
    {
        return Rules.Set<CreateProduct>()
            .NotEmpty(Name)
            .GreaterThanOrEqual(Price, 0)
            .HasProblems(out problems);
    }

    // Método de comando
    [Command, WithValidateModel, WithUnitOfWork<MyDbContext>]
    public Result<Product> Execute()
    {
        var entity = new Product(Name, Price);
        // persistência via repositório pelo handler gerado
        return entity; // `ProduceNewEntity` não é necessário quando retorno é Result<T>
    }
}
```

O handler gerado irá:
- Validar `HasProblems` e retornar `Problems` se houver.
- Iniciar `UnitOfWork`, adicionar a entidade, salvar e retornar `Result<Product>`.

### 6.2 Editando entidade existente e carregando por ID
```csharp
public partial class UpdateProduct
{
    public int ProductId { get; set; }
    public string? Name { get; set; }

    // Validação de entrada (Name obrigatório e sem espaços)
    public bool HasProblems(out Problems? problems)
    {
        return Rules.Set<UpdateProduct>()
            .NullOrNotEmpty(Name)
            .NullOrNotMatches(Name, "\s")
            .HasProblems(out problems);
    }

    // O generator criará um método parcial WasValidated que marca membros como not-null quando aplicável
    // Chame WasValidated no início do comando para ajudar o analisador de null-state
    [Command, WithValidateModel, WithUnitOfWork<MyDbContext>, EditEntity(typeof(Product))]
    public Result Execute(Product product)
    {
        WasValidated();
        product.Rename(Name!);
        return Result.Ok();
    }
}
```

O handler requererá um parâmetro `productId` (injetado como primeiro parâmetro do handler), buscará `Product` pelo ID, tratará `NotFound` com `Problems`, executará `Execute(product)` e finalizará o UoW.

### 6.3 Decorators
```csharp
public partial class PublishOrder
{
    [Command, WithDecorators]
    public Result Execute()
    {
        // lógica principal
        return Result.Ok();
    }
}

public interface IDecorator<TCommand, TResult>
{
    Task<TResult> InvokeAsync(TCommand command, Func<TCommand, CancellationToken, Task<TResult>> next, CancellationToken ct);
}
```

Com `WithDecorators`, o handler criará um mediador que invoca `next` com pipeline de decorators registrados.

### 6.4 Mapeamento HTTP com Created
```csharp
[MapPost("/api/products", "CreateProduct")]
[MapGroup("Products")]
[WithSummary("Create product")]
[WithDescription("Creates a new product")]
[MapCreatedRoute("/api/products/{id}")]
public partial class CreateProduct
{
    [Command, WithUnitOfWork<MyDbContext>]
    public Result<Product> Execute() { /* ... */ }
}
```

Quando a resposta for `Result<Product>`, `MapIdResultValue` pode ser usado para garantir que o campo `Id` exista no valor retornado e construir `Location` em 201 com `MapCreatedRoute`.

### 6.5 Integração com WorkContext (receita validada)

Recomendado para módulos sobre `IWorkContext`. Use `WithWorkContext` no método de comando. Os 4 passos abaixo
foram validados (SmartCommands 0.0.9 + SmartCommands.WorkContext 0.0.6 + WorkContext 0.8.9, net10.0).

**Resolução de parâmetros do `Execute`** (feita pelo handler gerado — este é o ponto que mais confunde):
- parâmetro `IWorkContext` → recebe `accessor.Context` (a UoW corrente);
- parâmetro `CancellationToken` → repassado;
- **qualquer outro parâmetro — serviços E um `DbContext` concreto — vira injeção no construtor do handler gerado**
  (resolvido do DI). Logo, um `DbContext` recebido é a instância **escopada do DI**, não algo derivado de `accessor`.
  Com `WithWorkContext`, mutações feitas via esse `DbContext` só são persistidas por `CompleteAsync` se ele for o
  **mesmo `DbContext` escopado** que o `IWorkContext` encapsula. (Se o módulo usa um `DbContext` base + subclasse por
  provider, faça `services.AddScoped<BaseDbContext>(sp => sp.GetRequiredService<TProviderDbContext>())` para alinhar
  as instâncias e poder consultar com `Include` de forma agnóstica de provider.)

**1) Classe de comando** (`partial`): propriedades de entrada + `HasProblems` + `Execute`.
```csharp
public partial class CreateProduct
{
    public string Name { get; set; } = string.Empty;

    public bool HasProblems(out Problems? problems)
        => Rules.Set<CreateProduct>().NotEmpty(Name).HasProblems(out problems);

    [Command, WithValidateModel, WithWorkContext]
    public async Task<Result<Product>> Execute(IWorkContext context, IClock clock, CancellationToken ct)
    {
        var product = new Product(Name, clock.UtcNow); // IClock vem por injeção no ctor do handler
        context.Add(product);                          // CompleteAsync (no handler) persiste
        return product;
    }
}
```

**2) Agregador de DI** — classe `static partial` separada (uma por módulo):
```csharp
[AddHandlersServices("MyModule")] // gera AddMyModuleHandlersServices<TContext>(IServiceCollection)
public static partial class MyModuleCommandServices { }
```

**3) Registro** (o `TContext` fechado é `IWorkContext`):
```csharp
services.ConfigureWorkContextAdapterOptions(_ => { }); // OBRIGATÓRIO: registra IOptions<WorkContextAdapterOptions>
services.AddUnitOfWorkAccessor<IWorkContext>();         // overload de IServiceCollection (o builder usa IUnitOfWorkBuilder, não IWorkContextBuilder)
services.AddMyModuleHandlersServices<IWorkContext>();   // gerado pelo [AddHandlersServices]
```

**4) Execução** — resolva a interface gerada e chame `HandleAsync`:
```csharp
var handler = sp.GetRequiredService<ICreateProductHandler>();
var result = await handler.HandleAsync(new CreateProduct { Name = "X" }, ct);
```

O gerador emite `ICreateProductHandler` + `CreateProductHandler<TContext> where TContext : IWorkContext`; pipeline:
validar `HasProblems` → `BeginAsync` → `Execute(...)` → `CompleteAsync` (salva). Retorne `Result`/`Result<T>` para os
`Problems` de validação propagarem.

> **Gotcha — carga de agregado:** `ICriteria<T>`/SmartSearch **não** tem `Include` (ver `search.md`). Para carregar o
> grafo do agregado (entidades relacionadas), receba o `DbContext` concreto no `Execute` e use `db.Set<T>().Include(...)`
> (inclusive include por string para navegações com backing field), ou um `IQueryHandler` (`ConfigureQueries`).

Módulo de configuração reutilizável (mapeamentos/repos/searches/queries):
```csharp
public static class MyModuleConfigureWorkContext
{
    public static IWorkContextBuilder<TDbContext> ConfigureMyModule<TDbContext>(this IWorkContextBuilder<TDbContext> builder)
        where TDbContext : DbContext
    {
        return builder
            .ConfigureRepositories(typeof(MyModuleConfigureWorkContext).Assembly)
            .ConfigureSearches(typeof(MyModuleConfigureWorkContext).Assembly)
            .ConfigureQueries(typeof(MyModuleConfigureWorkContext).Assembly);
    }
}
```

> **Dois mecanismos distintos de comando — não confunda:** os handlers gerados por `[Command]`/`AddHandlersServices`
> são resolvidos **diretamente** como `I{Cmd}Handler` (acima). `ConfigureCommands(assembly)` + `context.SendAsync(cmd)`
> é o **dispatcher** do WorkContext (`ICommandHandler<T>`), um caminho separado. Para comandos gerados, use a receita acima.

## 7. Comportamento do Handler Gerado (pipeline)

Ordem típica no método `{CommandClass}Handler.Handle(Async)`:
1) `WithValidateModel` → chama `command.HasProblems(...)` e retorna `Problems` se houver.
2) `WithUnitOfWork`/`WithDbContext`/`WithWorkContext` → cria/acessa accessor e executa `BeginUnitOfWork`.
3) `EditEntity`/Find de entidades e coleções → resolve parâmetros vinculados a propriedades `Id`/`Ids` na classe.
4) `WithDecorators` → constrói mediador e invoca pipeline.
5) Invoca método do comando com parâmetros resolvidos.
6) `CompleteUnitOfWork` → persiste e retorna `Result`/`Result<T>`; compõe `Created` quando `ProduceNewEntity`/`MapCreatedRoute` aplicáveis.

Erros esperados são convertidos em `Problems` conforme atributos `ProduceProblems` e regras mencionadas.

## 8. Boas práticas

- Declare comandos em classes parciais quando precisar de `WasValidated`.
- Retorne `Result`/`Result<T>` e evite exceções no fluxo esperado.
- Use `Rules.Set` (SmartValidations) em `HasProblems` para mensagens consistentes e metadados (`rule/current/expected`).
- Prefira `WithUnitOfWork<TContext>` quando envolver persistência; use `EditEntity`/`ProduceNewEntity` conforme cenário.
- Para coleções, forneça propriedade `Ids` correspondente ao parâmetro do método para binding.
- Configure adapters EF/WorkContext via extensões fornecidas antes de executar comandos.
- Em endpoints, utilize `Map*` e metadados para documentação/segurança.

## 9. Limitações e diagnósticos (do Generator)

- Classe/método com argumentos genéricos não são suportados.
- `CancellationToken` só em métodos assíncronos.
- `WithDbContext` e `WithUnitOfWork` não podem coexistir; idem `WithWorkContext` com ambos.
- `ProduceNewEntity` e `EditEntity` exigem UoW; não podem coexistir entre si no mesmo método.
- `WithDecorators` exige tipo de retorno (não-`void`).
- `MapIdResultValue` exige que o tipo retornado tenha propriedade `Id`.
- Diagnósticos são emitidos durante a geração com mensagens indicando o ponto de falha.

## 10. Resumo

SmartCommands automatiza a criação de handlers para comandos, integrando validação, Unit of Work, carregamento de entidades, Decorators e mapeamento HTTP. Com adapters para EF Core e WorkContext, os comandos podem executar persistência e buscas de forma padronizada, retornando `Result`/`Problems` consistentes e prontos para APIs.

## 11. Instruções para Ferramentas de IA (GitHub Copilot)

Diretrizes de geração alinhadas às seções anteriores:
- Comandos e atributos
  - Anote métodos com `Command` e, conforme necessidade, `WithValidateModel`, `WithUnitOfWork<TDbContext>`, `WithDecorators`, `EditEntity`, `ProduceNewEntity`.
  - Vincule parâmetros de entidade/coleção a propriedades `Id`/`Ids` na classe para Find automático.
  - Declare problemas esperados com `ProduceProblems(...)` quando aplicável.
- Resultados e validação
  - Retorne `Result`/`Result<T>`; evite exceções no fluxo esperado.
  - Use `Rules.Set<T>()` em `HasProblems(out Problems?)`.
- Integração EF/WorkContext
  - Registre `AddUnitOfWorkAccessor<TContext>()` (EF) ou `AddUnitOfWorkAccessor<TDbContext>()` (WorkContext builder).
  - Configure `RepositoryAdapter` e opções de `WorkContextAdapterOptions` conforme necessidade.
- Mapeamento HTTP
  - Use `MapPost`/`MapPut`/etc., `MapGroup`, `WithSummary`/`WithDescription`, `WithAuthorization`/`WithPolicy`.
  - Utilize `MapCreatedRoute` e `MapIdResultValue` para construção de Location em 201.
- Padrões de prompt
  - “Implemente um comando que valida entrada com `HasProblems`, usa `WithUnitOfWork<TDbContext>` e retorna `Result<T>` com `MapPost` + `MapCreatedRoute`.”
  - “Crie um comando com `EditEntity(typeof(Entity))` e parâmetro `entity` correspondente; gere handler que carrega por ID e retorna `Result`.”
  - “Habilite `WithDecorators` e componha pipeline para `Result` com interceptores (logging, métricas).”
  - “Configure adapters com `AddUnitOfWorkAccessor<MyDbContext>` e execute comandos via WorkContext.”
