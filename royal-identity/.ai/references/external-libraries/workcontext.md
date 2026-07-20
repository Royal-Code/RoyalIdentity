# Documentação da API WorkContext, UnitOfWork e Repositories

`RoyalCode.WorkContext` é a API unificada de persistência desta solução: um Unit of Work estendido com
repositórios, buscas (SmartSearch), commands, queries e operation hints, implementado sobre EF Core.

Este é o guia conceitual e prático. Para instruções objetivas destinadas a ferramentas de IA, consulte também
[`workcontext.ai-rules.md`](workcontext.ai-rules.md).

> **Verificado contra:** pacotes `RoyalCode.Repositories.*`, `RoyalCode.UnitOfWork.*` e `RoyalCode.WorkContext.*`
> **0.9.0** — .NET 8, .NET 9 e .NET 10 (EF Core 8/9/10).
> **Precedência das fontes:** documentação XML/IntelliSense da versão instalada > [`workcontext.ai-rules.md`](workcontext.ai-rules.md) > este guia.
> Se a versão do pacote for diferente, confirme as assinaturas no IDE antes de gerar código.

Relacionados: [`problems.md`](problems.md) (`Result`/`Problems`/`FindResult`), [`smartsearch.md`](smartsearch.md)
(`ICriteria<>`), [`domain.md`](domain.md) (entidades e agregados).

Sumário

1. Visão geral e conceitos
2. Pacotes, namespaces e instalação
3. Configuração com DI e EF Core
4. Providers (Sqlite, SQL Server, PostgreSQL)
5. Registro de repositórios, buscas e mapeamentos
6. Operações do `IWorkContext`
7. Unit of Work: `SaveResult`, transações e concorrência
8. Repositórios em detalhe
9. Commands
10. Queries
11. Operation Hints
12. Referência rápida da API
13. Erros comuns
14. Boas práticas

## 1. Visão geral e conceitos

`IWorkContext` compõe, em um único serviço, os papéis que uma aplicação normalmente resolve com vários:

```csharp
public interface IWorkContext :
    IUnitOfWork,                    // Save/SaveAsync, transações, CleanUp
    IEntityManager,                 // Repository<TEntity>()
    ISearchManager,                 // Criteria<TEntity>() (SmartSearch)
    IQueryDispatcher,               // QueryAsync(request)
    ICommandDispatcher,             // SendAsync(command)
    IHintsContainer,                // AddHint(hint) (Operation Hints)
    IInfrastructureProvidesServices // GetService<T>()
{ }
```

| Conceito | Papel |
|---|---|
| `IWorkContext` | fachada da unidade de persistência para os serviços de aplicação |
| `IUnitOfWork` | fim da unidade de trabalho: `Save()`/`SaveAsync()` retornam `SaveResult` |
| `IRepository<TEntity>` | CRUD orientado a entidade (`IAdder` + `IFinder` + `IUpdater` + `IRemover`) |
| `ICriteria<TEntity>` | consultas declarativas com filtros/ordenação/paginação (SmartSearch) |
| `ICommandRequest` (+ handler) | operação de escrita com regra de negócio, retorna `Result` |
| `IQueryRequest` (+ handler) | leitura tipada, lista (`Task<IEnumerable<T>>`) ou stream (`IAsyncEnumerable<T>`) |
| `IWorkContextBuilder<TDbContext>` | builder fluente de configuração (DbContext, repositórios, buscas, commands, queries) |

Não é um service locator: os serviços expostos pertencem à unidade de persistência. Para funcionalidades
extras, crie uma interface própria herdando `IWorkContext`.

Fluxo típico em um handler:

```text
request ─► valida (Problems) ─► IWorkContext
                                   ├─ Repository<T>() / FindAsync(...)   (escrita)
                                   ├─ Criteria<T>()                      (leitura)
                                   └─ SaveAsync() ─► SaveResult ─► Result
```

## 2. Pacotes, namespaces e instalação

Para o cenário comum com EF Core:

```bash
dotnet add package RoyalCode.WorkContext.EntityFramework
# opcional, por provider:
dotnet add package RoyalCode.WorkContext.Sqlite
dotnet add package RoyalCode.WorkContext.SqlServer
dotnet add package RoyalCode.WorkContext.PostgreSql
```

Pacotes e responsabilidades:

| Pacote | Responsabilidade |
|---|---|
| `RoyalCode.Repositories.Abstractions` | contratos `IRepository<>`, data services (`IAdder`, `IFinder`, ...) |
| `RoyalCode.Repositories.EntityFramework` | `Repository<TDbContext,TEntity>`, registro em DI |
| `RoyalCode.UnitOfWork.Abstractions` | `IUnitOfWork`, `ITransaction`, `SaveResult`, `ConcurrencyException` |
| `RoyalCode.UnitOfWork.EntityFramework` | `UnitOfWork<TDbContext>`, builder, `DefaultDbContext` |
| `RoyalCode.WorkContext.Abstractions` | `IWorkContext`, commands/queries (contratos), extensões |
| `RoyalCode.WorkContext.EntityFramework` | `WorkContext<TDbContext>`, `AddWorkContext`, configurers |
| `RoyalCode.WorkContext.Sqlite` / `.SqlServer` / `.PostgreSql` | atalhos de provider (`AddSqliteWorkContext`, etc.) |

Namespaces que mais causam dúvida — os nomes **não** seguem o sufixo `.Abstractions`/`.EntityFramework` dos pacotes:

| Tipo / membro | `using` (namespace) | Pacote NuGet |
|---|---|---|
| `IRepository<>`, `IAdder<>`, `IFinder<>`, `IUpdater<>`, `IRemover<>`, `IFinderByCode<,>`, `IFinderByGuid<>`, `IEntityManager` | `RoyalCode.Repositories` | `RoyalCode.Repositories.Abstractions` |
| `IUnitOfWork`, `ITransaction`, `SaveResult`, `ConcurrencyException` | `RoyalCode.UnitOfWork` | `RoyalCode.UnitOfWork.Abstractions` |
| `IWorkContext`, extensões `Add`/`Find`/`Merge`/`Delete` | `RoyalCode.WorkContext` | `RoyalCode.WorkContext.Abstractions` |
| `ICommandRequest`, `ICommandRequest<T>`, `ICommandHandler<,>`, `ICommandDispatcher` | `RoyalCode.WorkContext.Commands` | `RoyalCode.WorkContext.Abstractions` |
| `IQueryRequest<>`, `IAsyncQueryRequest<>`, `IQueryDispatcher` | `RoyalCode.WorkContext.Querying` | `RoyalCode.WorkContext.Abstractions` |
| `IQueryHandler<TDb,...>`, `IAsyncQueryHandler<TDb,...>` | `RoyalCode.WorkContext.EntityFramework.Querying` | `RoyalCode.WorkContext.EntityFramework` |
| `IWorkContextBuilder<>`, `IConfigureWorkContext<>`, extensões de assembly | `RoyalCode.WorkContext.EntityFramework.Configurations` | `RoyalCode.WorkContext.EntityFramework` |
| `IUnitOfWork<TDbContext>`, `DefaultDbContext`, `UnitOfWork<TDbContext>` | `RoyalCode.UnitOfWork.EntityFramework` | `RoyalCode.UnitOfWork.EntityFramework` |
| `AddWorkContext`, `AddCommandDispatcher`, `AddSqliteWorkContext...`, `AddSqlServerWorkContext...`, `AddPostgreWorkContext...`, `EnsureDatabaseCreated`, `SeedDatabase` | ⚠️ `Microsoft.Extensions.DependencyInjection` | conforme o pacote |
| `Unwrap<TDbContext>()` (de `IUnitOfWork` para `DbContext`) | ⚠️ `RoyalCode.UnitOfWork.Abstractions` | `RoyalCode.UnitOfWork.EntityFramework` |
| `ISearchManager` / `ISearchManager<TDbContext>` | `RoyalCode.SmartSearch` / `RoyalCode.SmartSearch.EntityFramework.Services` | SmartSearch (transitivo) |
| `FindResult<>`, `Id<,>` | `RoyalCode.SmartProblems.Entities` | `RoyalCode.SmartProblems` (transitivo) |

Pontos que causam erro com mais frequência:

- Os métodos de DI e providers vivem em `Microsoft.Extensions.DependencyInjection` de propósito: instalado o
  pacote, aparecem sem `using` novo no `Program.cs`.
- `Result`/`Problems` vêm de SmartProblems ([`problems.md`](problems.md)); `SaveResult` vem de
  `RoyalCode.UnitOfWork` e converte implicitamente para `Result`.
- `ICriteria<>` e o comportamento de busca vêm de SmartSearch ([`smartsearch.md`](smartsearch.md)).

## 3. Configuração com DI e EF Core

### 3.1 Formas de registrar o WorkContext

```csharp
// DbContext próprio da aplicação
services.AddWorkContext<MyDbContext>()
    .ConfigureDbContextPool((sp, builder) => builder.UseSqlServer(connectionString));

// DefaultDbContext: contexto pronto, modelado só por configurações (ver 3.3)
services.AddWorkContextDefault();

// WorkContext customizado (interface e/ou implementação próprias)
services.AddWorkContext<MyWorkContext, MyDbContext>();
services.AddWorkContext<IMyWorkContext, MyWorkContext, MyDbContext>();
```

`AddWorkContext` registra (lifetime padrão **Scoped**): `IWorkContext`, `IWorkContext<TDbContext>`,
`IUnitOfWork`, `IUnitOfWork<TDbContext>`, `IEntityManager`, `IEntityManager<TDbContext>`, `ISearchManager`,
`ISearchManager<TDbContext>` — todos resolvendo para a **mesma instância** no escopo — e também os serviços
LINQ do SmartSearch.

### 3.2 Configurando o DbContext

```csharp
builder
    .ConfigureDbContext(b => b.UseSqlServer(cs))            // AddDbContext (respeita lifetime)
    .ConfigureDbContextPool((sp, b) => b.UseNpgsql(cs))     // AddDbContextPool
    .ConfigureDbContextWithService()                        // provider vem de ConfigureOptions (ver providers §4)
    .ConfigureOptions((sp, b) => b.UseSqlite(...))          // ações acumuláveis de options
    .UseLazyLoadingProxies()
    .UseLoggerFactoryAndEnableSensitiveDataLogging(isDevelopment: true);
```

`ConfigureDbContextWithService` + `ConfigureOptions` é o par usado pelos pacotes de provider: as opções são
registradas como serviços e aplicadas quando o DbContext é criado — permitindo compor provider, logging e
outras opções em pontos diferentes da configuração.

### 3.3 `ConfigureModel`, `ConfigureConventions` e `DefaultDbContext`

```csharp
builder
    .ConfigureModel(mb => mb.ApplyConfigurationsFromAssembly(typeof(PersonMapping).Assembly))
    .ConfigureConventions(cb => cb.Properties<string>().HaveMaxLength(255));
```

⚠️ Essas ações são entregues via serviços `IConfigureModel<TDbContext>`/`IConfigureConventions<TDbContext>` e
**só têm efeito se o DbContext as consumir**. O `DefaultDbContext` já faz isso. Para um DbContext próprio,
chame as extensões nos overrides:

```csharp
public class MyDbContext : DbContext
{
    public MyDbContext(DbContextOptions<MyDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        this.ConfigureModelWithServices(modelBuilder);          // aplica ConfigureModel(...)
        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        this.ConfigureConventionsWithServices(configurationBuilder); // aplica ConfigureConventions(...)
        base.ConfigureConventions(configurationBuilder);
    }
}
```

Se o seu DbContext define o modelo no próprio `OnModelCreating` (sem `ConfigureModel`), nada disso é necessário.

### 3.4 Módulos de configuração reutilizáveis

Para bibliotecas/módulos, implemente `IConfigureWorkContext<TDbContext>` ou exponha uma extensão:

```csharp
public sealed class MyModuleConfigurer<TDbContext> : IConfigureWorkContext<TDbContext>
    where TDbContext : DbContext
{
    public IWorkContextBuilder<TDbContext> ConfigureWorkContext(IWorkContextBuilder<TDbContext> builder)
    {
        var assembly = typeof(MyModuleConfigurer<TDbContext>).Assembly;
        return builder
            .ConfigureMappingsFromAssembly(assembly, addRepositories: true)
            .ConfigureSearches(assembly)
            .ConfigureCommands(assembly)
            .ConfigureQueries(assembly);
    }
}

// consumo:
services.AddWorkContext<MyDbContext>()
    .ConfigureDbContextPool((sp, b) => b.UseNpgsql(cs))
    .Configure<MyDbContext, MyModuleConfigurer<MyDbContext>>();
```

## 4. Providers (Sqlite, SQL Server, PostgreSQL)

Os pacotes de provider adicionam atalhos que leem a connection string do `IConfiguration`
(`ConnectionStrings:<nome>`, padrão `"Default"`):

```csharp
// SQL Server
services.AddSqlServerWorkContext<MyDbContext>("MyConnection")
    .ConfigureSqlServerOptions(o => o.EnableRetryOnFailure())
    .UseRelationalNulls();

// PostgreSQL (Npgsql)
services.AddPostgreWorkContext<MyDbContext>()
    .ConfigureNpgsqlOptions(o => o.MigrationsHistoryTable("__migrations"));

// SQLite em arquivo
services.AddSqliteWorkContext<MyDbContext>()
    .ConfigureSqliteOptions(o => o.CommandTimeout(60));
```

Cada método tem a variante `...WorkContextDefault(...)` que usa o `DefaultDbContext`.

### 4.1 SQLite in-memory para dev/test

```csharp
services.AddSqliteInMemoryWorkContextDefault()
    .EnsureDatabaseCreated()
    .ConfigureModel(b => b.ApplyConfigurationsFromAssembly(typeof(PersonMapping).Assembly))
    .ConfigureRepositories(c => c.Add<Person>())
    .ConfigureSearches(c => c.Add<Person>())
    .SeedDatabase(async db =>
    {
        db.Add(new Person { Name = "Alice" });
        await db.SaveChangesAsync();
    });
```

Como funciona: uma única `SqliteConnection("DataSource=:memory:")` é registrada como singleton e mantida
aberta — o banco vive enquanto a conexão viver. Na primeira abertura, os hooks configurados executam em ordem:
`EnsureDatabaseCreated()` cria o schema e `SeedDatabase(...)` semeia os dados.

⚠️ `EnsureDatabaseCreated()` e `SeedDatabase(...)` pertencem ao pacote **`RoyalCode.WorkContext.Sqlite`** e são
acionados pelo hook de inicialização da conexão in-memory. Use-os com `AddSqliteInMemoryWorkContext*`; para
outros providers, crie/migre o banco explicitamente (`db.Database.EnsureCreated()`, `Migrate()`) na
inicialização da aplicação.

Se preferir configurar o SQLite in-memory manualmente, mantenha a conexão aberta:

```csharp
services.AddWorkContext<MyDbContext>()
    .ConfigureDbContextPool((sp, builder) =>
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open(); // fechar a conexão destrói o banco
        builder.UseSqlite(conn);
    });
```

## 5. Registro de repositórios, buscas e mapeamentos

### 5.1 Registro explícito

```csharp
builder
    .ConfigureRepositories(c => c.Add<Person>().Add<Order>())
    .ConfigureSearches(c => c.Add<Person>());
```

`ConfigureRepositories(c => c.Add<TEntity>())` registra:

- `IRepository<TEntity>` e `IRepository<TDbContext, TEntity>`;
- cada data service individual (`IAdder<TEntity>`, `IFinder<TEntity>`, `IUpdater<TEntity>`, `IRemover<TEntity>`);
- **automaticamente**, `IFinderByGuid<TEntity>` se a entidade implementa `IHasGuid`, e
  `IFinderByCode<TEntity, TCode>` se implementa `IHasCode<TCode>`.

`ConfigureSearches(c => c.Add<TEntity>())` registra `ICriteria<TEntity>` (SmartSearch).

### 5.2 Registro por assembly — dois critérios diferentes

```csharp
builder.ConfigureRepositories(assembly); // varre tipos que implementam IEntity
builder.ConfigureSearches(assembly);     // varre tipos que implementam IEntity
builder.AddRepositories(assembly);       // ⚠️ varre IEntityTypeConfiguration<TEntity> (mapeamentos EF)
builder.ConfigureMappingsFromAssembly(assembly, addRepositories: true);
    // aplica ApplyConfigurationsFromAssembly + AddRepositories(assembly)
```

- `ConfigureRepositories(assembly)`/`ConfigureSearches(assembly)`: a entidade precisa implementar
  `IEntity` (de `RoyalCode.Entities`) para ser descoberta.
- `AddRepositories(assembly)`: descobre entidades pelos **mapeamentos EF**
  (`IEntityTypeConfiguration<TEntity>`) — funciona para entidades que não implementam `IEntity`.

Escolha um critério por módulo e seja consistente; os dois podem registrar entidades diferentes do mesmo assembly.

### 5.3 Injeção direta vs. acesso pelo contexto

- `context.Repository<TEntity>()` (implementação EF) **cria o repositório sob demanda** — funciona para
  qualquer entidade mapeada no DbContext, registrada ou não.
- Injetar `IRepository<TEntity>` no construtor **exige** o registro via `ConfigureRepositories`.
- O mesmo vale para `ICriteria<TEntity>` (injeção exige `ConfigureSearches`; `context.Criteria<TEntity>()`
  funciona sempre).

## 6. Operações do `IWorkContext`

Membros principais e extensões (de `RoyalCode.WorkContext`):

```csharp
// repositórios e buscas
IRepository<TEntity> repo = context.Repository<TEntity>();
ICriteria<TEntity> criteria = context.Criteria<TEntity>();

// atalhos de CRUD (delegam ao repositório da entidade)
context.Add(entity);
await context.AddAsync(entity, ct);
context.AddRange(entities);
TEntity? e = context.Find<TEntity>(id);
var found = await context.FindAsync<TEntity, int>(id, ct);      // FindResult<TEntity,int>
var byName = await context.FindAsync<TEntity>(x => x.Name == name, ct);
await context.MergeAsync<TEntity, int>(model, ct);
context.Remove(entity);
await context.DeleteAsync<TEntity>(id, ct);

// buscas por Guid/Code (exigem repositório registrado — ver §5.1)
var byGuid = await context.FindByGuidAsync<TEntity>(guid, ct);          // TEntity : IHasGuid
var byCode = await context.FindByCodeAsync<TEntity, string>(code, ct);  // TEntity : IHasCode<string>

// unidade de trabalho
SaveResult result = await context.SaveAsync(ct);

// commands e queries
Result r = await context.SendAsync(command, ct);
IEnumerable<T> list = await context.QueryAsync(queryRequest, ct);
IAsyncEnumerable<T> stream = context.QueryAsync(asyncQueryRequest, ct);

// serviços da unidade de persistência
var finder = context.GetService<IFinderByCode<Product, string>>(); // lança se não registrado
```

CRUD básico completo:

```csharp
var person = new Person { Name = "John" };
await context.Repository<Person>().AddAsync(person, ct);
await context.SaveAsync(ct);

var found = await context.Repository<Person>().FindAsync(person.Id, ct);
```

## 7. Unit of Work: `SaveResult`, transações e concorrência

### 7.1 `Save()` / `SaveAsync()`

```csharp
SaveResult result = await context.SaveAsync(ct);

if (result.HasProblems(out var problems))
    return problems;          // falha de persistência (DbUpdateException) vira Problems

int changes = result.Changes; // entidades criadas/alteradas/excluídas
```

Comportamento da implementação EF:

- sucesso → `SaveResult` com `Changes`;
- `DbUpdateException` → `SaveResult` de **falha** (`Problems.InternalError(ex)`), sem lançar;
- `DbUpdateConcurrencyException` → **lança** `ConcurrencyException` (não vira `SaveResult`) — trate conflitos
  de concorrência com `try/catch` onde fizer sentido.

### 7.2 Membros de `SaveResult`

```csharp
readonly struct SaveResult
{
    int Changes { get; }
    Problems? Problems { get; }
    Exception? Exception { get; }
    bool IsSuccess { get; }
    bool IsFailure { get; }

    bool HasProblems([NotNullWhen(true)] out Problems? problems);
    bool IsSuccessOrGetProblems([NotNullWhen(false)] out Problems? problems);
    void EnsureSuccess();                       // lança InvalidOperationException se falhou
    Result<TValue> Map<TValue>(TValue value);   // sucesso → value; falha → Problems
}

// conversões implícitas
SaveResult r = changes;          // int
SaveResult r = exception;        // Exception
Result res  = saveResult;        // SaveResult -> Result

// extensão para Task
Task<Result<TValue>> MapAsync<TValue>(this Task<SaveResult> task, TValue value);
```

Handlers devolvem `Result` diretamente graças às conversões:

```csharp
public async Task<Result> HandleAsync(CreatePerson request, IWorkContext context, CancellationToken ct)
{
    context.Add(new Person { Name = request.Name });
    return await context.SaveAsync(ct);              // SaveResult -> Result
}

public async Task<Result<Person>> HandleAsync(CreatePersonWithResponse request, IWorkContext context, CancellationToken ct)
{
    var person = new Person { Name = request.Name };
    context.Add(person);
    return await context.SaveAsync(ct).MapAsync(person); // Task<SaveResult> -> Task<Result<Person>>
}
```

### 7.3 Transações manuais

O Unit of Work já envolve o `SaveChanges` em transação; use transação manual apenas quando precisar de
múltiplos saves na mesma transação:

```csharp
var transaction = await context.BeginTransactionAsync(ct);
try
{
    // ... primeira etapa
    (await context.SaveAsync(ct)).EnsureSuccess();

    // ... segunda etapa
    (await context.SaveAsync(ct)).EnsureSuccess();

    await transaction.CommitAsync(ct);
}
catch
{
    await transaction.RollbackAsync(ct);
    throw;
}
```

`GetCurrentTransaction()` retorna a transação corrente ou `null`. `CleanUp(force)` desanexa entidades do
change tracker (`force: false` desanexa apenas as não modificadas).

### 7.4 Acesso ao DbContext (borda de infraestrutura)

Prefira `IWorkContext` nos serviços de aplicação. Em código de infraestrutura que recebe `IUnitOfWork`:

```csharp
using RoyalCode.UnitOfWork.Abstractions; // ⚠️ namespace do Unwrap

DbContext db = unitOfWork.Unwrap<MyDbContext>(); // lança se o UoW não for do DbContext esperado
```

`IWorkContext<TDbContext>`/`IUnitOfWork<TDbContext>` também expõem a propriedade `Db` tipada.

## 8. Repositórios em detalhe

`IRepository<TEntity>` compõe quatro data services, todos com semântica de unidade de trabalho
(nada é persistido antes do `Save`):

```csharp
// IAdder<TEntity>
void Add(TEntity entity);
ValueTask AddAsync(TEntity entity, CancellationToken ct = default);
void AddRange(IEnumerable<TEntity> entities);
Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);

// IFinder<TEntity>
TEntity? Find(object id);
ValueTask<TEntity?> FindAsync(object id, CancellationToken ct = default);
Task<FindResult<TEntity, TId>> FindAsync<TId>(Id<TEntity, TId> id, CancellationToken ct = default);
Task<FindResult<TDto, TId>> FindAsync<TDto, TId>(Id<TEntity, TId> id, CancellationToken ct = default);
Task<FindResult<TEntity>> FindAsync(Expression<Func<TEntity, bool>> filter, CancellationToken ct = default);
Task<FindResult<TEntity>> FindAsync<TValue>(Expression<Func<TEntity, TValue>> propertySelector, TValue filterValue, CancellationToken ct = default);

// IUpdater<TEntity>
bool Merge<TId>(IHasId<TId> model);
Task<bool> MergeAsync<TId>(IHasId<TId> model, CancellationToken ct = default);
Task<bool> MergeAsync<TId, TModel>(Id<TEntity, TId> id, TModel model, CancellationToken ct = default);

// IRemover<TEntity>
void Remove(TEntity entity);
void RemoveRange(IEnumerable<TEntity> entities);
TEntity? Delete(object id);
Task<TEntity?> DeleteAsync(object id, CancellationToken ct = default);
Task<FindResult<TEntity, TId>> DeleteAsync<TId>(Id<TEntity, TId> id, CancellationToken ct = default);
```

Pontos importantes:

- As sobrecargas com `Id<TEntity, TId>` retornam `FindResult` (SmartProblems): `NotFound(out problem)` produz
  um 404 padronizado sem `if (entity is null)` manual. Ver [`problems.md`](problems.md) §5.

```csharp
Id<Person, int> id = request.PersonId;
var entry = await repo.FindAsync(id, ct);
if (entry.NotFound(out var problem))
    return problem;                       // 404 padronizado

entry.Entity.Rename(request.Name);
return await context.SaveAsync(ct);
```

- `FindAsync<TDto, TId>` projeta a entidade para DTO **no banco**, usando o selector do SmartSearch
  (convenção ou `AddSelector` — ver [`smartsearch.md`](smartsearch.md) §11); sem selector possível, lança.
- `Merge` copia os valores do modelo sobre a entidade rastreada (`CurrentValues.SetValues`): os nomes das
  propriedades do modelo devem corresponder aos da entidade; retorna `false` se a entidade não existe.
- `Remove` exige a entidade carregada; `Delete(id)` busca e marca para exclusão, retornando `null` se não achou.
- `Find`/`FindAsync` por id usam `DbSet.Find` — resolvem pelo change tracker sem SQL quando a entidade já está
  rastreada. As buscas por filtro sempre vão ao banco.

## 9. Commands

Commands encapsulam operações de escrita. O handler recebe o `IWorkContext` e retorna `Result`/`Result<T>`:

```csharp
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext;
using RoyalCode.WorkContext.Commands;

public sealed class CreatePerson : ICommandRequest
{
    public string Name { get; set; } = null!;
}

public sealed class CreatePersonHandler : ICommandHandler<CreatePerson>
{
    public async Task<Result> HandleAsync(CreatePerson request, IWorkContext context, CancellationToken ct = default)
    {
        context.Add(new Person { Name = request.Name });
        return await context.SaveAsync(ct);
    }
}
```

Com resposta (`ICommandRequest<TResponse>` + `ICommandHandler<TCommand, TResponse>`):

```csharp
public sealed class CreateOrder : ICommandRequest<OrderCreatedDetails>
{
    public string Number { get; set; } = null!;
}

public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, OrderCreatedDetails>
{
    public async Task<Result<OrderCreatedDetails>> HandleAsync(CreateOrder request, IWorkContext context, CancellationToken ct = default)
    {
        var order = new Order(request.Number);
        context.Add(order);
        return await context.SaveAsync(ct).MapAsync(new OrderCreatedDetails(order.Id, order.Number));
    }
}
```

Registro e execução:

```csharp
services.AddWorkContext<MyDbContext>()
    .ConfigureCommands(c => c.AddHandler<CreatePersonHandler>())     // um a um
    .ConfigureCommands(typeof(CreatePersonHandler).Assembly);        // ou por assembly

var result = await context.SendAsync(new CreatePerson { Name = "John" }, ct);
```

⚠️ `SendAsync` para um command sem handler registrado **lança** `InvalidOperationException`
("The command handler for ... was not found") — é erro de configuração, não um `Result` de falha.

`AddCommandDispatcher()` registra `ICommandDispatcher` como serviço independente, para enviar commands sem
depender de `IWorkContext` (o dispatcher resolve o work context internamente):

```csharp
services.AddWorkContext<MyDbContext>()
    .ConfigureCommands(c => c.AddHandler<CreatePersonHandler>())
    .AddCommandDispatcher();

// em um endpoint/serviço:
var result = await dispatcher.SendAsync(new CreatePerson { Name = "John" }, ct);
```

## 10. Queries

Quatro formas de request, escolhidas pelo tipo de retorno:

| Request | Retorno de `QueryAsync` | Uso |
|---|---|---|
| `IQueryRequest<TEntity>` | `Task<IEnumerable<TEntity>>` | lista de entidades |
| `IQueryRequest<TEntity, TModel>` | `Task<IEnumerable<TModel>>` | lista projetada em DTO |
| `IAsyncQueryRequest<TEntity>` | `IAsyncEnumerable<TEntity>` | stream de entidades |
| `IAsyncQueryRequest<TEntity, TModel>` | `IAsyncEnumerable<TModel>` | stream projetado em DTO |

### 10.1 Handlers inline (delegates)

```csharp
builder.ConfigureQueries(c =>
{
    c.Handle<GetPersons, Person>(async (request, db, ct) =>
        await db.Set<Person>()
            .Where(p => p.Name == request.Name)
            .ToListAsync(ct));

    c.Handle<GetPersonsDto, Person, PersonDto>(async (request, db, ct) =>
        await db.Set<Person>()
            .Where(p => p.Name == request.Name)
            .Select(p => new PersonDto { Id = p.Id, Name = p.Name })
            .ToListAsync(ct));

    c.AsyncHandle<StreamPersons, Person>((request, db, ct) =>
        db.Set<Person>()
            .Where(p => p.Name == request.Name)
            .AsAsyncEnumerable());
});
```

### 10.2 Handlers em classes

O handler de query recebe o **DbContext** (não o `IWorkContext`) — queries são leitura pura:

```csharp
using RoyalCode.WorkContext.EntityFramework.Querying;

public sealed class GetPersonsHandler : IQueryHandler<MyDbContext, GetPersons, Person>
{
    public async Task<IEnumerable<Person>> HandleAsync(GetPersons request, MyDbContext db, CancellationToken ct = default)
    {
        return await db.Set<Person>()
            .Where(p => request.Name == null || p.Name == request.Name)
            .ToListAsync(ct);
    }
}

// variantes: IQueryHandler<TDb, TRequest, TEntity, TModel>,
//            IAsyncQueryHandler<TDb, TRequest, TEntity>, IAsyncQueryHandler<TDb, TRequest, TEntity, TModel>
```

Registro:

```csharp
builder.ConfigureQueries(c => c.AddHandler<GetPersonsHandler>());     // um a um
builder.ConfigureQueries(typeof(GetPersonsHandler).Assembly);         // por assembly
```

### 10.3 Execução

```csharp
var persons = await context.QueryAsync(new GetPersons { Name = "John" }, ct);
var dtos = await context.QueryAsync(new GetPersonsDto { Name = "John" }, ct);

await foreach (var p in context.QueryAsync(new StreamPersons { Name = "John" }, ct))
{
    // consumir stream
}
```

⚠️ Como nos commands, query sem handler registrado lança `InvalidOperationException`.

A sobrecarga de `QueryAsync` é escolhida pelo **tipo estático** do request: um request que implementa
`IQueryRequest<TEntity, TModel>` retorna `IEnumerable<TModel>`; não implemente as duas interfaces no mesmo
request.

Para buscas com filtros declarativos, paginação e ordenação dinâmica, prefira `context.Criteria<TEntity>()`
(SmartSearch) em vez de escrever queries manuais — ver [`smartsearch.md`](smartsearch.md).

## 11. Operation Hints

Hints carregam navegações de agregado sem espalhar `Include` pelos call sites (mesmo mecanismo do SmartSearch —
ver [`smartsearch.md`](smartsearch.md) §13):

```csharp
builder.ConfigureOperationHints(registry =>
{
    registry.AddIncludesHandler<Order, OrderHints>((hint, includes) =>
    {
        if (hint is OrderHints.WithItems)
            includes.IncludeCollection(o => o.Items);
    });
});
```

Uso com o work context:

```csharp
context.AddHint(OrderHints.WithItems);                 // hint ambiente (escopo atual)
var entry = await context.FindAsync<Order, int>(id, ct); // o repositório aplica os hints no Find
```

Os repositórios criados pelo `WorkContext` aplicam o `IHintPerformer` automaticamente após `Find`/`FindAsync`.
Sem registro de hints, `AddHint` é no-op silencioso.

## 12. Referência rápida da API

Registro (namespace `Microsoft.Extensions.DependencyInjection`):

```csharp
services.AddWorkContext<TDbContext>(lifetime = Scoped);
services.AddWorkContextDefault(lifetime = Scoped);
services.AddWorkContext<TDbWorkContext, TDbContext>();
services.AddWorkContext<TWorkContext, TDbWorkContext, TDbContext>();
services.AddUnitOfWork<TDbContext>();                        // só UoW, sem WorkContext
services.AddSqliteWorkContext<TDb>(connectionStringName = "Default");
services.AddSqliteInMemoryWorkContext<TDb>();
services.AddSqlServerWorkContext<TDb>(connectionStringName = "Default");
services.AddPostgreWorkContext<TDb>(connectionStringName = "Default");
// cada provider tem a variante ...WorkContextDefault()
```

Builder (`IWorkContextBuilder<TDbContext>`):

```csharp
.ConfigureDbContext(...); .ConfigureDbContextPool(...);
.ConfigureDbContextWithService(); .ConfigureDbContextPoolWithService();
.ConfigureOptions(...); .ConfigureModel(...); .ConfigureConventions(...);
.ConfigureMappingsFromAssembly(assembly, addRepositories);
.AddRepositories(assembly);                 // via IEntityTypeConfiguration<>
.ConfigureRepositories(cfg | assembly);     // cfg: c => c.Add<TEntity>(); assembly: via IEntity
.ConfigureSearches(cfg | assembly);
.ConfigureCommands(cfg | assembly);
.ConfigureQueries(cfg | assembly);
.ConfigureOperationHints(registry => ...);
.UseLazyLoadingProxies();
.UseLoggerFactoryAndEnableSensitiveDataLogging(isDevelopment);
.AddCommandDispatcher();
.Configure(configurer); .Configure<TDbContext, TConfigurer>();
// providers: .ConfigureSqliteOptions(...); .ConfigureSqlServerOptions(...);
//            .ConfigureNpgsqlOptions(...); .UseRelationalNulls();
//            .EnsureDatabaseCreated(); .SeedDatabase(...);   (Sqlite in-memory)
```

Operações (`IWorkContext` + extensões):

```csharp
context.Repository<TEntity>();  context.Criteria<TEntity>();
context.Save();  await context.SaveAsync(ct);
await context.SendAsync(command, ct);
await context.QueryAsync(request, ct);
context.Add(entity);  await context.FindAsync<TEntity, TId>(id, ct);
await context.FindByCodeAsync<TEntity, TCode>(code, ct);
await context.FindByGuidAsync<TEntity>(guid, ct);
context.BeginTransaction();  await context.BeginTransactionAsync(ct);
context.CleanUp(force);  context.AddHint(hint);  context.GetService<T>();
```

## 13. Erros comuns

### 13.1 `ConfigureModel` ignorado com DbContext próprio

```csharp
// ❌ MyDbContext não consome IConfigureModel: o mapping nunca é aplicado.
services.AddWorkContext<MyDbContext>()
    .ConfigureModel(b => b.ApplyConfigurationsFromAssembly(asm));

// ✅ Use DefaultDbContext, ou chame this.ConfigureModelWithServices(modelBuilder)
//    no OnModelCreating do seu DbContext (ver §3.3).
```

### 13.2 `EnsureDatabaseCreated`/`SeedDatabase` fora do SQLite in-memory

Esses métodos são do pacote Sqlite e executam no hook de abertura da conexão in-memory.
Com SQL Server/PostgreSQL/SQLite-arquivo, crie/migre o banco explicitamente na inicialização.

### 13.3 Confundir `AddRepositories(assembly)` com `ConfigureRepositories(assembly)`

```csharp
// AddRepositories: descobre entidades por IEntityTypeConfiguration<TEntity> (mapeamentos EF).
// ConfigureRepositories: descobre entidades por IEntity (RoyalCode.Entities).
```

Se a entidade não implementa `IEntity` e não tem mapping class, nenhum dos dois a registra — use
`ConfigureRepositories(c => c.Add<TEntity>())`.

### 13.4 Esperar `Result` de falha quando o handler não existe

```csharp
// ❌ Não é falha de negócio: SendAsync/QueryAsync lançam InvalidOperationException
//    quando o handler não está registrado.
// ✅ Registre com ConfigureCommands/ConfigureQueries e confira o assembly:
builder.ConfigureCommands(typeof(CreatePersonHandler).Assembly);
```

### 13.5 Ignorar a exceção de concorrência

```csharp
// ❌ result.HasProblems não cobre concorrência: SaveAsync LANÇA ConcurrencyException.
var result = await context.SaveAsync(ct);

// ✅ Trate o conflito quando a entidade usa token de concorrência.
try { (await context.SaveAsync(ct)).EnsureSuccess(); }
catch (ConcurrencyException) { /* recarregar/reaplicar/retornar 409 */ }
```

### 13.6 Injetar `IRepository<T>` sem registrar

`context.Repository<T>()` funciona sem registro (a implementação EF cria sob demanda), mas a injeção de
`IRepository<T>` no construtor falha sem `ConfigureRepositories(c => c.Add<T>())`. O mesmo vale para
`ICriteria<T>` e `ConfigureSearches`.

### 13.7 `FindByCode`/`FindByGuid` sem os contratos

`FindByCodeAsync` exige `TEntity : IHasCode<TCode>` e `FindByGuidAsync` exige `TEntity : IHasGuid`; os
finders são registrados automaticamente pelo `ConfigureRepositories` **somente** quando a entidade implementa
esses contratos. Sem registro, `GetService` lança `InvalidOperationException`.

### 13.8 Fechar a conexão SQLite in-memory

O banco in-memory vive na conexão. Não crie/feche conexões por operação; use o builder in-memory (singleton)
ou mantenha a conexão aberta pelo tempo de vida dos testes.

### 13.9 Modelo de `Merge` com shape diferente da entidade

`Merge` usa `CurrentValues.SetValues(model)`: propriedades do modelo que não existem na entidade são
ignoradas e propriedades da entidade ausentes no modelo ficam intactas — mas tipos incompatíveis com mesmo
nome falham em runtime. Mantenha o DTO de merge alinhado à entidade (`IHasId<TId>` + mesmos nomes).

## 14. Boas práticas

- Prefira `IWorkContext` nos serviços de aplicação; deixe `DbContext` para handlers de query e infraestrutura.
- Centralize escrita em commands (`ICommandHandler`) retornando `Result`; leitura em queries ou
  `Criteria<TEntity>()`.
- Registre repositórios apenas para raízes de agregado; use `Criteria` para consultas de leitura.
- Organize módulos com `IConfigureWorkContext<TDbContext>` ou extensões de builder, registrando mapeamentos,
  repositórios, buscas, commands e queries por assembly.
- Use as sobrecargas com `Id<TEntity, TId>`/`FindResult` para respostas 404 padronizadas sem boilerplate.
- Converta `SaveResult` para `Result` (implícito) ou `Result<T>` (`Map`/`MapAsync`) na saída dos handlers.
- Trate `ConcurrencyException` explicitamente onde houver token de concorrência.
- Use SQLite in-memory (`AddSqliteInMemoryWorkContext*` + `EnsureDatabaseCreated` + `SeedDatabase`) em testes;
  migrações nos ambientes reais.
- Propague `CancellationToken` em todas as operações async.
- Para carregar navegações em fluxos de entidade, use Operation Hints em vez de espalhar `Include`.

Para geração de código por IA, use [`workcontext.ai-rules.md`](workcontext.ai-rules.md), que contém regras
imperativas, matriz de decisão, receitas e checklist.
