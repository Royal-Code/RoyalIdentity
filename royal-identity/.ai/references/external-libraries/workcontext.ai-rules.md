# WorkContext, UnitOfWork e Repositories — Regras para IA

Regras operacionais para gerar código de persistência com as bibliotecas RoyalCode em projetos .NET com EF Core.
Para contexto conceitual e explicações, consulte [`workcontext.md`](workcontext.md).

> **Verificado contra:** `RoyalCode.Repositories.*`, `RoyalCode.UnitOfWork.*`, `RoyalCode.WorkContext.*` **0.9.0**
> — .NET 8 / 9 / 10 (EF Core 8/9/10).
> **Precedência das fontes:** documentação XML/IntelliSense da versão instalada > este arquivo > `workcontext.md`.
> Com versão divergente, confirme a assinatura no IDE antes de gerar código.

Relacionados: [`problems.ai-rules.md`](problems.ai-rules.md) (`Result`/`Problems`/`FindResult`),
[`smartsearch.ai-rules.md`](smartsearch.ai-rules.md) (`ICriteria<>`), [`domain.ai-rules.md`](domain.ai-rules.md).

## 1. Pacotes e `using`

Pacote principal para aplicações:

```xml
<PackageReference Include="RoyalCode.WorkContext.EntityFramework" Version="..." />
<!-- opcional, por provider -->
<PackageReference Include="RoyalCode.WorkContext.Sqlite" Version="..." />
<PackageReference Include="RoyalCode.WorkContext.SqlServer" Version="..." />
<PackageReference Include="RoyalCode.WorkContext.PostgreSql" Version="..." />
```

Namespaces **não** seguem os sufixos dos pacotes. Não deduza; consulte:

| Tipo / membro | `using` | Pacote NuGet |
|---|---|---|
| `IWorkContext` + extensões `Add`/`Find`/`Merge`/`Delete` | `RoyalCode.WorkContext` | `RoyalCode.WorkContext.Abstractions` |
| `ICommandRequest`, `ICommandHandler<,>`, `ICommandDispatcher` | `RoyalCode.WorkContext.Commands` | `RoyalCode.WorkContext.Abstractions` |
| `IQueryRequest<>`, `IAsyncQueryRequest<>`, `IQueryDispatcher` | `RoyalCode.WorkContext.Querying` | `RoyalCode.WorkContext.Abstractions` |
| `IQueryHandler<TDb,...>`, `IAsyncQueryHandler<TDb,...>` | `RoyalCode.WorkContext.EntityFramework.Querying` | `RoyalCode.WorkContext.EntityFramework` |
| `IWorkContextBuilder<>`, `IConfigureWorkContext<>` | `RoyalCode.WorkContext.EntityFramework.Configurations` | `RoyalCode.WorkContext.EntityFramework` |
| `IRepository<>`, `IAdder<>`, `IFinder<>`, `IUpdater<>`, `IRemover<>`, `IFinderByCode<,>`, `IFinderByGuid<>`, `IEntityManager` | `RoyalCode.Repositories` | `RoyalCode.Repositories.Abstractions` |
| `IUnitOfWork`, `ITransaction`, `SaveResult`, `ConcurrencyException` | `RoyalCode.UnitOfWork` | `RoyalCode.UnitOfWork.Abstractions` |
| `IUnitOfWork<TDb>`, `DefaultDbContext` | `RoyalCode.UnitOfWork.EntityFramework` | `RoyalCode.UnitOfWork.EntityFramework` |
| `AddWorkContext*`, `AddCommandDispatcher`, `AddSqliteWorkContext*`, `AddSqlServerWorkContext*`, `AddPostgreWorkContext*`, `EnsureDatabaseCreated`, `SeedDatabase`, `ConfigureSqliteOptions`, `ConfigureSqlServerOptions`, `ConfigureNpgsqlOptions`, `UseRelationalNulls` | ⚠️ `Microsoft.Extensions.DependencyInjection` | conforme o pacote |
| `Unwrap<TDbContext>()` | ⚠️ `RoyalCode.UnitOfWork.Abstractions` | `RoyalCode.UnitOfWork.EntityFramework` |
| `Result`, `Problems` | `RoyalCode.SmartProblems` | SmartProblems (transitivo) |
| `FindResult<>`, `Id<,>` | `RoyalCode.SmartProblems.Entities` | SmartProblems (transitivo) |
| `ICriteria<>`, `SearchOptions`, `Sorting` | `RoyalCode.SmartSearch` | SmartSearch (transitivo) |

## 2. Regras invioláveis

1. Serviços de aplicação usam `IWorkContext`; não injete `DbContext` neles. `DbContext` aparece só em
   query handlers e infraestrutura.
2. Toda escrita termina em `Save()`/`SaveAsync(ct)`; nunca chame `db.SaveChanges` por fora do contexto em
   fluxo de aplicação.
3. `SaveAsync` retorna `SaveResult`: converta para `Result` (implícito) ou `Result<T>` (`Map`/`MapAsync`).
   Nunca ignore o retorno.
4. `SaveAsync` **lança** `ConcurrencyException` em conflito de concorrência; `DbUpdateException` vira
   `SaveResult` de falha. Trate a exceção onde houver token de concorrência.
5. Handler não registrado faz `SendAsync`/`QueryAsync` **lançar** `InvalidOperationException` — é erro de
   configuração, não `Result` de falha.
6. Injeção de `IRepository<T>`/`ICriteria<T>` exige registro (`ConfigureRepositories`/`ConfigureSearches`);
   `context.Repository<T>()`/`context.Criteria<T>()` funcionam sem registro.
7. `ConfigureModel`/`ConfigureConventions` só têm efeito com `DefaultDbContext` ou com DbContext que chama
   `this.ConfigureModelWithServices(...)`/`this.ConfigureConventionsWithServices(...)` nos overrides.
8. `EnsureDatabaseCreated()`/`SeedDatabase(...)` são do pacote **Sqlite** e valem para o fluxo
   `AddSqliteInMemoryWorkContext*`; para outros providers, crie/migre o banco explicitamente.
9. `AddRepositories(assembly)` descobre entidades por `IEntityTypeConfiguration<>`;
   `ConfigureRepositories(assembly)` descobre por `IEntity`. Não os trate como sinônimos.
10. Command handlers recebem `IWorkContext`; query handlers recebem `TDbContext`. Não inverta.
11. Registre repositórios apenas para raízes de agregado; leitura vai por `Criteria<T>()` ou queries.
12. Propague `CancellationToken` em todas as operações async.
13. SQLite in-memory: a conexão é singleton e deve permanecer aberta; não crie conexão por operação.

## 3. Matriz de decisão

| Necessidade | Gere |
|---|---|
| operação de escrita com regra de negócio | `ICommandRequest` (+`<TResponse>`) + `ICommandHandler` |
| leitura tipada simples | `IQueryRequest<TEntity>` (+`,TModel>`) + handler |
| stream de leitura | `IAsyncQueryRequest<...>` + `AsyncHandle`/`IAsyncQueryHandler` |
| busca com filtros/paginação/sorting | `context.Criteria<TEntity>()` (SmartSearch) |
| CRUD direto por entidade | `context.Repository<TEntity>()` ou extensões `context.Add/Find/Delete` |
| buscar por id com 404 padronizado | `FindAsync(Id<TEntity,TId>)` → `FindResult` → `NotFound(out problem)` |
| buscar e projetar DTO por id | `FindAsync<TDto, TId>(id, ct)` (requer selector — SmartSearch/convenção) |
| buscar por código de negócio | `FindByCodeAsync<TEntity, TCode>` (entidade `IHasCode<TCode>`) |
| buscar por Guid global | `FindByGuidAsync<TEntity>` (entidade `IHasGuid`) |
| atualizar entidade a partir de DTO | `MergeAsync` com modelo `IHasId<TId>` de mesmo shape |
| excluir por id | `DeleteAsync(Id<TEntity,TId>)` → `FindResult` |
| múltiplos saves na mesma transação | `BeginTransactionAsync` + `Commit/RollbackAsync` |
| carregar navegações do agregado | Operation Hints (`AddHint` + `ConfigureOperationHints`) |
| enviar command fora do WorkContext | `AddCommandDispatcher()` + `ICommandDispatcher` |

## 4. Configuração canônica

Produção (SQL Server / PostgreSQL / SQLite arquivo — connection string do `IConfiguration`, nome padrão `"Default"`):

```csharp
services.AddSqlServerWorkContext<AppDbContext>("MyConnection");
services.AddPostgreWorkContext<AppDbContext>();
services.AddSqliteWorkContext<AppDbContext>();
```

Ou explícito:

```csharp
services.AddWorkContext<AppDbContext>()
    .ConfigureDbContextPool((sp, builder) => builder.UseNpgsql(connectionString))
    .ConfigureRepositories(c => c.Add<Order>().Add<Customer>())
    .ConfigureSearches(c => c.Add<Order>())
    .ConfigureCommands(typeof(CreateOrderHandler).Assembly)
    .ConfigureQueries(typeof(GetOrdersHandler).Assembly);
```

Dev/test (SQLite in-memory + `DefaultDbContext`):

```csharp
services.AddSqliteInMemoryWorkContextDefault()
    .EnsureDatabaseCreated()
    .ConfigureModel(b => b.ApplyConfigurationsFromAssembly(typeof(OrderMapping).Assembly))
    .ConfigureRepositories(c => c.Add<Order>())
    .ConfigureSearches(c => c.Add<Order>())
    .SeedDatabase(async db =>
    {
        db.Add(new Order("N-1"));
        await db.SaveChangesAsync();
    });
```

Módulo reutilizável:

```csharp
public sealed class OrdersModule<TDb> : IConfigureWorkContext<TDb> where TDb : DbContext
{
    public IWorkContextBuilder<TDb> ConfigureWorkContext(IWorkContextBuilder<TDb> builder)
    {
        var asm = typeof(OrdersModule<TDb>).Assembly;
        return builder
            .ConfigureMappingsFromAssembly(asm, addRepositories: true)  // mappings EF + repositórios
            .ConfigureSearches(asm)      // entidades IEntity
            .ConfigureCommands(asm)
            .ConfigureQueries(asm);
    }
}

services.AddWorkContext<AppDbContext>()
    .ConfigureDbContextPool((sp, b) => b.UseNpgsql(cs))
    .Configure<AppDbContext, OrdersModule<AppDbContext>>();
```

Registro do `ConfigureRepositories(c => c.Add<TEntity>())` inclui automaticamente:
`IRepository<TEntity>`, `IRepository<TDb,TEntity>`, `IAdder/IFinder/IUpdater/IRemover<TEntity>`,
`IFinderByGuid<TEntity>` (se `IHasGuid`) e `IFinderByCode<TEntity,TCode>` (se `IHasCode<TCode>`).

## 5. Assinaturas (cheat-sheet)

`IWorkContext` (composição):

```csharp
interface IWorkContext : IUnitOfWork, IEntityManager, ISearchManager,
    IQueryDispatcher, ICommandDispatcher, IHintsContainer, IInfrastructureProvidesServices { }

IRepository<TEntity> Repository<TEntity>();
ICriteria<TEntity> Criteria<TEntity>();
SaveResult Save();
Task<SaveResult> SaveAsync(CancellationToken token = default);
ITransaction BeginTransaction();
Task<ITransaction> BeginTransactionAsync(CancellationToken token = default);
ITransaction? GetCurrentTransaction();
void CleanUp(bool force = true);
Task<Result> SendAsync(ICommandRequest request, CancellationToken ct = default);
Task<Result<TResponse>> SendAsync<TResponse>(ICommandRequest<TResponse> request, CancellationToken ct = default);
Task<IEnumerable<TEntity>> QueryAsync<TEntity>(IQueryRequest<TEntity> request, CancellationToken ct = default);
Task<IEnumerable<TModel>> QueryAsync<TEntity, TModel>(IQueryRequest<TEntity, TModel> request, CancellationToken ct = default);
IAsyncEnumerable<TEntity> QueryAsync<TEntity>(IAsyncQueryRequest<TEntity> request, CancellationToken ct = default);
IAsyncEnumerable<TModel> QueryAsync<TEntity, TModel>(IAsyncQueryRequest<TEntity, TModel> request, CancellationToken ct = default);
void AddHint<THint>(THint hint) where THint : Enum;
T GetService<T>();   // lança InvalidOperationException se ausente
```

`SaveResult` (`RoyalCode.UnitOfWork`, `readonly struct`):

```csharp
int Changes; Problems? Problems; Exception? Exception;
bool IsSuccess; bool IsFailure;
bool HasProblems([NotNullWhen(true)] out Problems? problems);
bool IsSuccessOrGetProblems([NotNullWhen(false)] out Problems? problems);
void EnsureSuccess();                            // lança se falhou
Result<TValue> Map<TValue>(TValue value);
// implícitos: int -> SaveResult; Exception -> SaveResult; SaveResult -> Result
Task<Result<TValue>> MapAsync<TValue>(this Task<SaveResult> task, TValue value);  // extensão
```

`IRepository<TEntity>` (= `IAdder` + `IFinder` + `IUpdater` + `IRemover`):

```csharp
void Add(TEntity entity);
ValueTask AddAsync(TEntity entity, CancellationToken ct = default);
void AddRange(IEnumerable<TEntity> entities);
Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);

TEntity? Find(object id);
ValueTask<TEntity?> FindAsync(object id, CancellationToken ct = default);
Task<FindResult<TEntity, TId>> FindAsync<TId>(Id<TEntity, TId> id, CancellationToken ct = default);
Task<FindResult<TDto, TId>> FindAsync<TDto, TId>(Id<TEntity, TId> id, CancellationToken ct = default); // exige selector
Task<FindResult<TEntity>> FindAsync(Expression<Func<TEntity, bool>> filter, CancellationToken ct = default);
Task<FindResult<TEntity>> FindAsync<TValue>(Expression<Func<TEntity, TValue>> propertySelector, TValue filterValue, CancellationToken ct = default);

bool Merge<TId>(IHasId<TId> model);
Task<bool> MergeAsync<TId>(IHasId<TId> model, CancellationToken ct = default);
Task<bool> MergeAsync<TId, TModel>(Id<TEntity, TId> id, TModel model, CancellationToken ct = default);

void Remove(TEntity entity);
void RemoveRange(IEnumerable<TEntity> entities);
TEntity? Delete(object id);
Task<TEntity?> DeleteAsync(object id, CancellationToken ct = default);
Task<FindResult<TEntity, TId>> DeleteAsync<TId>(Id<TEntity, TId> id, CancellationToken ct = default);
```

Handlers:

```csharp
// commands — recebem IWorkContext
interface ICommandHandler<TCommand> where TCommand : ICommandRequest
{ Task<Result> HandleAsync(TCommand request, IWorkContext context, CancellationToken ct = default); }

interface ICommandHandler<TCommand, TResponse> where TCommand : ICommandRequest<TResponse>
{ Task<Result<TResponse>> HandleAsync(TCommand request, IWorkContext context, CancellationToken ct = default); }

// queries — recebem TDbContext
interface IQueryHandler<TDb, TRequest, TEntity>
{ Task<IEnumerable<TEntity>> HandleAsync(TRequest request, TDb db, CancellationToken ct = default); }

interface IQueryHandler<TDb, TRequest, TEntity, TModel>
{ Task<IEnumerable<TModel>> HandleAsync(TRequest request, TDb db, CancellationToken ct = default); }

interface IAsyncQueryHandler<TDb, TRequest, TEntity>
{ IAsyncEnumerable<TEntity> HandleAsync(TRequest request, TDb db, CancellationToken ct = default); }
```

Configuração de commands/queries:

```csharp
.ConfigureCommands(c => c.AddHandler<THandler>());
.ConfigureCommands(c => c.AddHandlersFromAssembly(assembly));
.ConfigureCommands(assembly);
.ConfigureQueries(c => c.AddHandler<THandler>());
.ConfigureQueries(c => c.Handle<TRequest, TEntity>((request, db, ct) => ...));
.ConfigureQueries(c => c.Handle<TRequest, TEntity, TModel>((request, db, ct) => ...));
.ConfigureQueries(c => c.AsyncHandle<TRequest, TEntity>((request, db, ct) => ...));
.ConfigureQueries(assembly);
.AddCommandDispatcher();
```

## 6. Receitas canônicas

Command sem resposta:

```csharp
public sealed class CreatePerson : ICommandRequest
{
    public string Name { get; set; } = null!;
}

public sealed class CreatePersonHandler : ICommandHandler<CreatePerson>
{
    public async Task<Result> HandleAsync(CreatePerson request, IWorkContext context, CancellationToken ct = default)
    {
        context.Add(new Person(request.Name));
        return await context.SaveAsync(ct);                     // SaveResult -> Result
    }
}
```

Command com resposta:

```csharp
public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, OrderDetails>
{
    public async Task<Result<OrderDetails>> HandleAsync(CreateOrder request, IWorkContext context, CancellationToken ct = default)
    {
        if (request.HasProblems(out var problems))
            return problems;                                     // 400

        var order = new Order(request.Number);
        context.Add(order);
        return await context.SaveAsync(ct)
            .MapAsync(new OrderDetails(order.Id, order.Number)); // Task<SaveResult> -> Task<Result<OrderDetails>>
    }
}
```

Atualização com 404 padronizado (`FindResult`):

```csharp
public async Task<Result> HandleAsync(RenamePerson request, IWorkContext context, CancellationToken ct = default)
{
    Id<Person, int> id = request.PersonId;
    var entry = await context.FindAsync(id, ct);
    if (entry.NotFound(out var problem))
        return problem;                                          // 404

    entry.Entity.Rename(request.Name);
    return await context.SaveAsync(ct);
}
```

Exclusão por id:

```csharp
var entry = await context.DeleteAsync<Order, int>(id, ct);
if (entry.NotFound(out var problem))
    return problem;
return await context.SaveAsync(ct);
```

Query em classe (recebe o DbContext):

```csharp
public sealed class GetPersonsQuery : IQueryRequest<Person>
{
    public string? Name { get; set; }
}

public sealed class GetPersonsHandler : IQueryHandler<AppDbContext, GetPersonsQuery, Person>
{
    public async Task<IEnumerable<Person>> HandleAsync(GetPersonsQuery request, AppDbContext db, CancellationToken ct = default)
    {
        return await db.Set<Person>()
            .Where(p => request.Name == null || p.Name == request.Name)
            .ToListAsync(ct);
    }
}
```

Stream:

```csharp
builder.ConfigureQueries(c =>
    c.AsyncHandle<StreamPersons, Person>((request, db, ct) =>
        db.Set<Person>().Where(p => p.Name == request.Name).AsAsyncEnumerable()));

await foreach (var person in context.QueryAsync(new StreamPersons { Name = "John" }, ct))
{
    // consumir
}
```

Transação manual (múltiplos saves):

```csharp
var transaction = await context.BeginTransactionAsync(ct);
try
{
    // etapa 1 ... 
    (await context.SaveAsync(ct)).EnsureSuccess();
    // etapa 2 ...
    (await context.SaveAsync(ct)).EnsureSuccess();
    await transaction.CommitAsync(ct);
}
catch
{
    await transaction.RollbackAsync(ct);
    throw;
}
```

Concorrência:

```csharp
try
{
    return await context.SaveAsync(ct);
}
catch (ConcurrencyException)
{
    return Problems.InvalidState("The record was modified by another process");  // 409
}
```

## 7. Anti-padrões

```csharp
// ❌ DbContext no serviço de aplicação          // ✅ IWorkContext
public OrderService(AppDbContext db)             public OrderService(IWorkContext context)

// ❌ salvar por fora da unidade de trabalho     // ✅ fim da unidade de trabalho
await db.SaveChangesAsync(ct);                   await context.SaveAsync(ct);

// ❌ descartar o SaveResult                     // ✅ converter/propagar
await context.SaveAsync(ct);                     return await context.SaveAsync(ct);

// ❌ null check manual + exceção                // ✅ FindResult com problema padronizado
var e = await repo.FindAsync(id, ct);            var entry = await repo.FindAsync((Id<Order,int>)id, ct);
if (e is null) throw new NotFoundException();    if (entry.NotFound(out var problem)) return problem;

// ❌ command handler consultando lista          // ✅ leitura em query/criteria
class ListOrders : ICommandRequest { }           class ListOrders : IQueryRequest<Order> { }

// ❌ try/catch para handler ausente             // ✅ registrar o handler no startup
try { await context.SendAsync(cmd, ct); }        builder.ConfigureCommands(typeof(Handler).Assembly);
catch (InvalidOperationException) { ... }

// ❌ EnsureDatabaseCreated com SQL Server       // ✅ é do fluxo Sqlite in-memory
services.AddSqlServerWorkContext<Db>()           services.AddSqliteInMemoryWorkContextDefault()
    .EnsureDatabaseCreated();                        .EnsureDatabaseCreated();

// ❌ ConfigureModel com DbContext que não       // ✅ DefaultDbContext, ou override chamando
//    consome os serviços de configuração        //    this.ConfigureModelWithServices(modelBuilder)
```

Regras adicionais:

- Não use `Single`/`First` para "buscar por id"; use `Find*`/`FindResult`.
- Não injete `IRepository<T>` sem `ConfigureRepositories(c => c.Add<T>())`.
- Não implemente `IQueryRequest<TEntity>` e `IQueryRequest<TEntity,TModel>` no mesmo request — a sobrecarga
  de `QueryAsync` é resolvida pelo tipo estático.
- Não use `Merge` com DTO de shape divergente da entidade; alinhe nomes e tipos das propriedades.
- Não chame `Commit` sem `BeginTransaction` (lança `InvalidOperationException`).
- Não presuma dispatch automático de eventos de domínio; integre outbox/dispatcher explicitamente
  (ver [`domain.ai-rules.md`](domain.ai-rules.md)).
- Para respostas HTTP, converta `Result` com `OkMatch`/`CreatedMatch`/`NoContentMatch`
  (ver [`problems.ai-rules.md`](problems.ai-rules.md)).

## 8. Checklist antes de entregar o código

- [ ] `using` e pacotes conferidos na tabela da §1.
- [ ] Serviços de aplicação dependem de `IWorkContext`, não de `DbContext`.
- [ ] Escrita termina em `SaveAsync(ct)` com o `SaveResult` convertido/propagado.
- [ ] `ConcurrencyException` tratada onde há token de concorrência.
- [ ] Handlers registrados (`ConfigureCommands`/`ConfigureQueries`) no assembly correto.
- [ ] Repositórios/criterias registrados quando injetados diretamente.
- [ ] Buscas por id usam `Id<TEntity,TId>` + `FindResult` para 404 padronizado.
- [ ] `ConfigureModel` só com `DefaultDbContext` ou DbContext que consome os serviços de configuração.
- [ ] `EnsureDatabaseCreated`/`SeedDatabase` apenas no fluxo SQLite in-memory.
- [ ] `CancellationToken` propagado em todos os métodos async.
- [ ] Commands para escrita; queries/criteria para leitura; repositórios só para raízes de agregado.
