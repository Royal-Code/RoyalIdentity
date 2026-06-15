
# Documentação da API WorkContext, UnitOfWork e Repositories

Esta documentação apresenta os conceitos, funcionalidades e exemplos práticos para usar as bibliotecas de persistência desta solução.
Serve também como referência para ferramentas de IA (ex.: GitHub Copilot) compreenderem e gerarem código correto com base na API.

Projetos alvo: .NET 8, .NET 9 e .NET 10.

Escopo principal:
- `RoyalCode.Repositories.Abstractions` (contratos Repository)
- `RoyalCode.UnitOfWork.Abstractions` (contratos Unit of Work)
- `RoyalCode.WorkContext.Abstractions` (contratos WorkContext)

Implementações baseadas em EF Core:
- `RoyalCode.Repositories.EntityFramework`
- `RoyalCode.UnitOfWork.EntityFramework`
- `RoyalCode.WorkContext.EntityFramework`

Providers (utilitários):
- `RoyalCode.WorkContext.SqlServer`
- `RoyalCode.WorkContext.PostgreSql`
- `RoyalCode.WorkContext.Sqlite`

---

## 1. Introdução

`RoyalCode.WorkContext` é a API recomendada para uso. Ela estende o padrão Unit of Work e agrega funcionalidades de Repositórios, Busca (SmartSearch), Commands e Queries, oferecendo uma experiência unificada, modular e compatível com EF Core.

Benefícios principais:
- Contratos limpos e tipados para Repository/UoW/WorkContext.
- Builder fluente para configurar DbContext, repositórios, buscas, commands e queries.
- Integração direta com EF Core e provedores (Sqlite, SqlServer, PostgreSQL via Npgsql).
- Suporte a comandos/consultas com registro por assembly.

---

## 2. Abstrações e Conceitos

WorkContext:
- `IWorkContext`: API unificada que expõe operações de Unit of Work, acesso a repositórios, buscas, envio de comandos e execução de consultas.
- `IWorkContextBuilder<TDbContext>`: builder fluente para configurar WorkContext, DbContext (EF Core), repositórios, buscas, commands e queries.

UnitOfWork:
- `IUnitOfWork`: controle transacional e persistência, com `Save()`/`SaveAsync()`.
- Integra com repositórios e pipelines de comandos/consultas.

Repositories:
- Contratos para CRUD e consulta por ID, orientados a entidades (`IRepository<TEntity>`).
- Suporte a ID fortemente tipado com integrações auxiliares.

---

## 3. Configuração com Entity Framework

Principais extensões de configuração:
- `ConfigureDbContextPool(...)`: registra o `DbContext` e configura o provider EF Core.
- `ConfigureModel(...)` e `ConfigureMappingsFromAssembly(...)`: aplica configurações de modelagem/mapeamentos.
- `EnsureDatabaseCreated()`: garante que o banco seja criado (dev/test).
- `SeedDatabase(...)`: semeia dados iniciais.
- `ConfigureRepositories(...)` e `AddRepositories(...)`: registra repositórios para entidades.
- `ConfigureSearches(...)`: registra pesquisas (SmartSearch).
- `ConfigureCommands(...)` e `AddCommandDispatcher()`: registra handlers de comandos.
- `ConfigureQueries(...)`: registra handlers de consultas.

Mapa de provedores EF Core:
- Sqlite in‑memory: `UseSqlite(new SqliteConnection("DataSource=:memory:"))` + `conn.Open()`
- Sqlite arquivo: `UseSqlite("Data Source=mydb.sqlite")`
- PostgreSQL (Npgsql): `UseNpgsql("Host=...;Database=...;Username=...;Password=...")`
- SQL Server: `UseSqlServer("Server=...;Database=...;Integrated Security=True")`

---

## 4. Operações principais em WorkContext

- `Repository<TEntity>()`: obtém o repositório da entidade.
- `Criteria<TEntity>()`: obtém critérios de pesquisa (SmartSearch).
- `Save()` / `SaveAsync()`: persiste alterações (Unit of Work).
- `SendAsync(command)`: executa comandos.
- `QueryAsync(request)`: executa consultas síncronas (lista) ou assíncronas (stream).

Referência rápida de API:
- Builder: `AddWorkContext<TDbContext>()` → `ConfigureDbContextPool(...)` → `ConfigureModel(...)` → `ConfigureRepositories(...)` → `ConfigureSearches(...)` → `ConfigureCommands(...)` → `ConfigureQueries(...)`
- Registros por assembly: `AddRepositories(assembly)`, `ConfigureSearches(assembly)`, `ConfigureCommands(assembly)`, `ConfigureQueries(assembly)`

---

## 5. Exemplos de uso

CRUD básico com repositório:
```csharp
var person = new Person { Name = "John" };
await context.Repository<Person>().AddAsync(person);
await context.SaveAsync();
var found = await context.Repository<Person>().FindAsync(person.Id);
```

Query síncrona:
```csharp
var result = await context.QueryAsync(new GetPersons { Name = "John" }, ct);
```

Query assíncrona (stream):
```csharp
await foreach (var p in context.QueryAsync(new StreamPersons { Name = "John" }, ct)) { /* ... */ }
```

Command:
```csharp
var res = await context.SendAsync(new CreatePerson { Name = "John" }, ct);
```

Exemplo de configuração EF em memória (SQLite):
```csharp
services.AddSqliteInMemoryWorkContextDefault()
    .EnsureDatabaseCreated()
    .ConfigureModel(b => b.ApplyConfigurationsFromAssembly(typeof(PersonMapping).Assembly))
    .ConfigureRepositories(c => c.Add<Person>())
    .ConfigureSearches(c => c.Add<Person>())
    .SeedDatabase(async db => { /* dados iniciais */ await db.SaveChangesAsync(); });
```

Módulo de configuração reutilizável:
```csharp
public static class MyModuleConfigureWorkContext
{
    public static IWorkContextBuilder<TDbContext> ConfigureMyModule<TDbContext>(this IWorkContextBuilder<TDbContext> builder)
        where TDbContext : DbContext
    {
        return builder.ConfigureModel(modelBuilder => modelBuilder.MapMyModule())
            .AddRepositories(typeof(MyModuleConfigureWorkContext).Assembly)
            .ConfigureSearches(typeof(MyModuleConfigureWorkContext).Assembly)
            .ConfigureCommands(typeof(MyModuleConfigureWorkContext).Assembly)
            .ConfigureQueries(typeof(MyModuleConfigureWorkContext).Assembly);
    }
}
```

Providers (uso rápido):
```csharp
// PostgreSQL
services.AddWorkContext<MyDbContext>()
    .ConfigureDbContextPool((sp, builder) => builder.UseNpgsql("Host=...;Database=...;Username=...;Password=..."))
    .ConfigureMyModule();

// SQL Server
services.AddWorkContext<MyDbContext>()
    .ConfigureDbContextPool((sp, builder) => builder.UseSqlServer("Server=...;Database=...;Integrated Security=True"))
    .ConfigureMyModule();

// SQLite (arquivo)
services.AddWorkContext<MyDbContext>()
    .ConfigureDbContextPool((sp, builder) => builder.UseSqlite("Data Source=mydb.sqlite"))
    .ConfigureMyModule();
```

---

## 6. Commands e Queries (uso básico)

Commands:
```csharp
public sealed class CreatePerson : ICommandRequest
{
    public string Name { get; set; } = null!;
}

public sealed class CreatePersonHandler : ICommandHandler<CreatePerson>
{
    public async Task<Result> HandleAsync(CreatePerson request, IWorkContext context, CancellationToken ct)
    {
        context.Add(new Person { Name = request.Name });
        return await context.SaveAsync(ct);
    }
}

var result = await context.SendAsync(new CreatePerson { Name = "John" }, default);
```

Queries (lista):
```csharp
public sealed class GetPersons : IQueryRequest<Person>
{
    public string? Name { get; set; }
}

builder.ConfigureQueries(c =>
{
    c.Handle<GetPersons, Person>(async (req, db, ct) =>
        await db.Set<Person>().Where(p => p.Name == req.Name).ToListAsync(ct));
});

var persons = await context.QueryAsync(new GetPersons { Name = "John" }, default);
```

Queries com `IQueryHandler`:
```csharp
public sealed class GetPersonsHandler : IQueryHandler<MyDbContext, GetPersons, Person>
{
    public async Task<IEnumerable<Person>> HandleAsync(GetPersons request, MyDbContext db, CancellationToken ct = default)
    {
        return await db.Set<Person>()
            .Where(p => request.Name == null || p.Name == request.Name)
            .ToListAsync(ct);
    }
}

services.AddWorkContext<MyDbContext>()
    .ConfigureDbContextPool((sp, b) => b.UseSqlite("DataSource=:memory:"))
    .ConfigureQueries(typeof(GetPersonsHandler).Assembly);
```

Queries com DTO (`IQueryRequest<TEntity, TModel>`):
```csharp
public sealed class PersonDto { public int Id { get; set; } public string Name { get; set; } = null!; }

public sealed class GetPersonsDto : IQueryRequest<Person, PersonDto>
{
    public string? Name { get; set; }
}

public sealed class GetPersonsDtoHandler : IQueryHandler<MyDbContext, GetPersonsDto, Person, PersonDto>
{
    public async Task<IEnumerable<PersonDto>> HandleAsync(GetPersonsDto request, MyDbContext db, CancellationToken ct = default)
    {
        return await db.Set<Person>()
            .Where(p => request.Name == null || p.Name == request.Name)
            .Select(p => new PersonDto { Id = p.Id, Name = p.Name })
            .ToListAsync(ct);
    }
}

var list = await context.QueryAsync(new GetPersons { Name = "John" }, ct);
var dtos = await context.QueryAsync(new GetPersonsDto { Name = "John" }, ct);
```

Stream assíncrono:
```csharp
public sealed class StreamPersons : IAsyncQueryRequest<Person>
{
    public string? Name { get; set; }
}

builder.ConfigureQueries(c =>
{
    c.AsyncHandle<StreamPersons, Person>((req, db, ct) =>
        db.Set<Person>().Where(p => p.Name == req.Name).AsAsyncEnumerable());
});

await foreach (var p in context.QueryAsync(new StreamPersons { Name = "John" }, default))
{
    // consumir stream
}
```

Troubleshooting:
- Handler não registrado: verifique `ConfigureCommands`/`ConfigureQueries` e assembly (`typeof(Handler).Assembly`).
- Banco não criado em dev/test: use `EnsureDatabaseCreated()` após configurar o provider.
- Conexão SQLite in‑memory: mantenha a conexão aberta no escopo do `DbContext`.
- DI: valide `AddWorkContext<TDbContext>()` e crie o `ServiceProvider` após configurar.

Nota: `Result`/`Result<T>` pertencem a outra biblioteca (SmartProblems). Consulte `.docs/problems.md`.

---

## 7. Boas Práticas

- Centralize regras de negócio em Commands; use Queries para leitura eficiente.
- Configure mapeamentos via assemblies para manter organização modular.
- Use `EnsureDatabaseCreated` e Sqlite in‑memory apenas em dev/test.
- Prefira `IWorkContext` e seus serviços ao acesso direto ao `DbContext`.
- Registre repositórios somente para raízes de agregado quando aplicável.
- Padroneize buscas com SmartSearch e DTOs com SmartSelector.

---

## 8. Resumo

WorkContext unifica UnitOfWork, Repositories, Search, Commands e Queries sobre EF Core. Os contratos nas bibliotecas de abstração estruturam operações e a implementação em `RoyalCode.*.EntityFramework` fornece integração pronta para uso com provedores comuns. Configure via builder fluente e use os serviços para CRUD, buscas e execução de pipelines de aplicação.

---

## 9. Instruções para Ferramentas de IA (GitHub Copilot)

Diretrizes de geração:
- Use `AddWorkContext<TDbContext>()` e encadeie `ConfigureDbContextPool`/`ConfigureModel`/`ConfigureRepositories`/`ConfigureSearches`/`ConfigureCommands`/`ConfigureQueries`.
- Gere comandos/queries com handlers e registre por assembly.
- Para CRUD, prefira `Repository<TEntity>()` e finalize com `SaveAsync()`.
- Ao usar Sqlite in‑memory, mantenha a conexão aberta.
- Para problemas de negócio, utilize `Result`/`Problems` (vide `.docs/problems.md`).

Padrões de prompt:
- "Configure WorkContext com SQLite in‑memory e crie o handler `CreatePerson` retornando `Result`."
- "Implemente query tipada por DTO e registre com `ConfigureQueries`."
- "Adicione repositórios e use `Repository<TEntity>()` para CRUD com `SaveAsync()`."
