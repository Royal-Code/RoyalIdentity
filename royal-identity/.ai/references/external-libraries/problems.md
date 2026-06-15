# Documentação da API SmartProblems (Problems, Result, FindResult)

Esta documentação apresenta os conceitos, funcionalidades e exemplos práticos para usar a biblioteca SmartProblems em projetos .NET.
Serve também como referência para ferramentas de IA (ex.: GitHub Copilot) compreenderem e gerarem código de forma correta com base na API da biblioteca.

Projetos alvo: .NET 8, .NET 9 e .NET 10.

## 1. Introdução

SmartProblems padroniza o tratamento de resultados e erros de operações em .NET, evitando exceções para fluxo normal e tornando o código previsível e composable.

Conceitos principais:
- `Problem`: representa um erro com categoria, detalhe, propriedade e extensões.
- `Problems`: coleção de `Problem` (encadeável, iterável, conversível para `Result`).
- `Result` / `Result<T>`: resultado de operação (sucesso/falha), com APIs de composição/transformação.
- `FindResult<T>` / `FindResult<T, TId>`: resultado de busca, com utilitários para continuar/mapear e converter para `Result`.
- Conversões para `ProblemDetails` (RFC 9457) para uso em APIs.
- Extensões para Entity Framework (métodos `TryFind*`).

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

- Conversão para `ProblemDetails`
  - `RoyalCode.SmartProblems.Conversions`: `Problems.ToProblemDetails(options)`.
  - `ProblemDetailsExtended` agrega múltiplos problemas (`errors`, `not_found`, `inner_details`).
  - Personalização via `ProblemDetailsOptions` e `ProblemDetailsDescriptor`.

- Integrações
  - Entity Framework: `SmartProblemsEFExtensions` com `TryFindAsync`/`TryFindByAsync` e `FindResult`.
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

Extensões e propriedades encadeadas:

```csharp
p400.With("attempt", 1).ChainProperty("User", 0); // User[0].name
p422.With("policy", "minimum-total");
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


### Custom e RFC 9457 (type)

Em `ProblemDetails` (RFC 9457), o campo `type` deve ser um identificador do tipo de problema (preferencialmente uma URI).
Recomendações atualizadas do RFC 9457:
- Use um `type` estável, único e documentado, de preferência uma URI absoluta (ex.: `https://api.seu-dominio.com/problems/order-on-hold`).
- Inclua `title` humano-legível e `status` coerente ao tipo descrito. Evite títulos genéricos.
- Utilize `instance` (URI) para identificar a ocorrência específica do problema quando aplicável.
- As extensões devem usar nomes claros e estáveis; evite sobrescrever campos reservados (`type`, `title`, `status`, `detail`, `instance`).
- Evite `about:blank` para problemas customizados; descreva tipos próprios com documentação.

Impacto do `Problems.Custom(detail, typeId, property)` na conversão:
- O `typeId` é mapeado para `ProblemDetails.Type`.
- Se configurado em `ProblemDetailsOptions.Descriptor`, define `title` e `status` consistentes para o tipo.
- Sem descrição, será usado um tipo agregado ou padrão conforme contexto.

Exemplo de configuração do tipo no `ProblemDetailsOptions`:
```csharp
var options = new ProblemDetailsOptions();
options.Descriptor.Add(new ProblemDetailsDescription(
    typeId: "order-on-hold",
    title: "Order on hold",
    description: "Business rule violation",
    statusCode: System.Net.HttpStatusCode.Conflict));

Problems problem = Problems.Custom("Order is on hold due to risk analysis", "order-on-hold");
var pd = problem.ToProblemDetails(options); // Type será "tag:problemdetails/.problems#order-on-hold" ou uma URI absoluta se configurada
```

Converter coleção de problemas para exceção:
```csharp
var ex = (p400 + p422).ToException("Validation errors: {0}");
throw ex;
```

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

var async = await ok.MapAsync(v => Task.FromResult(v.Length));

// branch explícito: Match
var outRes = ok.Match(
    value => Result.Ok(),
    problems => problems.AsResult());

// branch assíncrono: MatchAsync
var outResAsync = await ok.MatchAsync(
    value => Task.FromResult(Result.Ok()),
    problems => Task.FromResult(problems.AsResult()));
```

Casos de uso reais (serviços, handlers, repositórios):
```csharp
// Result sem valor
public readonly struct UserService
{
    private readonly IUserRepository _repo;
    private readonly IUserValidator _validator;
    private readonly IUserPolicy _policy;

    public Result Create(UserInput input)
    {
        // validação de entrada
        if (input.HasProblems(out var problems))
            return problems; // 400.

        // regra de negócio
        if (_validator.EnsureIsValid(input).HasProblems(out var problems))
            return problems; // 400/422 etc.

        // regra de negócio
        if (!_policy.CanCreate(input))
            return Problems.NotAllowed("Not allowed to create user");

        // persistência
        _repo.Add(input);
        return Result.Ok();
    }

    public Task<Result> DisableAsync(int id)
    {
        var findUser = _repo.FindByIdAsync(id);
        if (findUser.NotFound(out var problem))
            return Task.FromResult((Result)problem); // 404
        
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

A extensão de Entity Framework fornece métodos `TryFindAsync` e `TryFindByAsync` que retornam um `FindResult<TEntity>`.
Esse tipo encapsula o resultado da busca: a entidade encontrada (`Entity`) ou um problema padronizado quando não encontrada.

- `TryFindAsync(DbContext, Id<TEntity,TId>)` e `TryFindAsync(DbSet<TEntity>, Id<TEntity,TId>)`:
  - Quando a entidade não existe, gera um `Problem` com categoria `NotFound` (HTTP 404) e mensagem bem definida.
  - Campos extras adicionados em `Extensions`:
    - `id`: o valor do identificador usado na busca.
    - `entity`: o nome da entidade (ex.: `TestEntity`).

- `TryFindByAsync(DbContext/DbSet, Expression<Func<TEntity,bool>>)` e sobrecargas com nomes:
  - Quando o filtro não encontra a entidade, gera `Problem` `NotFound` com detalhe incluindo o display name da entidade e o nome/valor do campo.
  - Campos extras em `Extensions`:
    - `<PropertyName>` ou alias informado (ex.: `Name` ou `name`): valor usado no filtro.
    - `entity`: nome da entidade.

Uso típico:
```csharp
// Buscar por Id
Id<TestEntity,int> id = 4;
var entry = await db.TestEntities.TryFindAsync(id);
if (entry.NotFound(out var problem))
{
    // problem.Detail: "The record of 'The Entity for Tests' with id '4' was not found"
    // problem.Extensions: { id: 4, entity: "TestEntity" }
    return problem; // como Result/Problems ou ProblemDetails
}

// Buscar por propriedade
var byName = await db.TestEntities.TryFindByAsync(e => e.Name == "Test4");
if (byName.NotFound(out var problemByName))
{
    // problemByName.Detail: "The record of 'The Entity for Tests' with Name 'Test4' was not found"
    // problemByName.Extensions: { Name: "Test4", entity: "TestEntity" }
    return problemByName;
}
```

Quando possui nomes customizados (ex.: título do campo, alias e valor), use a sobrecarga:
```csharp
var entry2 = await db.TryFindByAsync<TestEntity>(e => e.Name == "Test4", displayName: "Name", alias: "name", value: "Test4");
if (entry2.NotFound(out var p))
{
    // p.Extensions: { name: "Test4", entity: "TestEntity" }
}
```

Composição com `FindResult`:
```csharp
await entry.ContinueAsync(async entity =>
{
    // prossiga com a entidade
    return Result.Ok();
});
```

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
Task<Result> ToResultAsync(this HttpResponseMessage response, CancellationToken token = default);
Task<Result<T>> ToResultAsync<T>(this HttpResponseMessage response, JsonSerializerOptions? options = null, CancellationToken token = default);
Task<Result<T>> ToResultAsync<T>(this HttpResponseMessage response, JsonTypeInfo<T> jsonTypeInfo, CancellationToken ct = default);
// Com FailureTypeReader para conteúdo de erro não-ProblemDetails
Task<Result<T>> ToResultAsync<T>(this HttpResponseMessage response, FailureTypeReader? reader, JsonSerializerOptions? options = null, CancellationToken token = default);
Task<Result<T>> ToResultAsync<T>(this HttpResponseMessage response, FailureTypeReader? reader, JsonTypeInfo<T> jsonTypeInfo, CancellationToken ct = default);
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

## 8. Boas Práticas

- Padronize categorias e status HTTP:
  - 404 NotFound, 400 InvalidParameter (entrada), 422 ValidationFailed (semântica), 403 NotAllowed, 409 InvalidState, 500 Internal.
  - Defina tipos customizados com `Problems.Custom` e descreva em `ProblemDetailsOptions`.
- Siga o RFC 9457:
  - Prefira URIs absolutas para `type` e `instance`, títulos claros (`title`) e `status` coerente.
  - Não sobrescreva campos reservados; use `Extensions` com nomes estáveis e significativos.
- Use `Result`/`Result<T>` como fluxo de sucesso/falha:
  - Componha com `Map`, `Continue`, `Match`/`MatchAsync` para reduzir boilerplate.
  - Evite exceções para casos esperados; retorne problemas nas falhas.
- Valide entrada e regras de domínio:
  - Modelo com `HasProblems(out Problems?)` ou FluentValidation (`EnsureIsValid`, `ToProblems`).
  - Em APIs, converta problemas para `ProblemDetails` automaticamente via `OkMatch`/`CreatedMatch`/`NoContentMatch`.
- Persistência e buscas:
  - Use `TryFindAsync`/`TryFindByAsync` (EF) e trate `FindResult` com `NotFound`/`ToResult([param])`.
  - Propague campos extras (`id`, `entity`, `property/value`) em `Extensions` para rastreabilidade.
- Cliente HTTP:
  - Consuma com `ToResultAsync` (valor ou problemas) e trate `application/problem+json` corretamente.
  - Para erros não-ProblemDetails, considere `FailureTypeReader`.
- Observabilidade e contexto:
  - Use `With(key, value)` para anexar dados relevantes (ids, política aplicada, limites).
  - Encadeie propriedades com `ChainProperty(parent[, index])` para apontar origem precisa.
- Performance:
  - `Result` é `readonly struct`; aproveite composição leve e evite alocações desnecessárias.

## Resumo

SmartProblems fornece uma abordagem uniforme e eficiente para tratar sucesso e falha em operações .NET.
Com `Problem`/`Problems` você modela erros com categorias e contexto; com `Result`/`Result<T>` você compõe fluxos sem lançar exceções em casos esperados.
A biblioteca integra-se a APIs via `ProblemDetails` (RFC 9457), ao EF via `FindResult`/`TryFind*` e ao cliente HTTP com `ToResultAsync`.
Tipos como `OkMatch`, `CreatedMatch` e `NoContentMatch` simplificam respostas HTTP consistentes.
O uso de `readonly struct` e APIs inlinadas favorece performance, e as extensões (`With`, `ChainProperty`) melhoram rastreabilidade.

## Instruções para Ferramentas de IA (GitHub Copilot)

Diretrizes de geração alinhadas às seções 1–7:
- Problemas e categorias
  - Gere problemas com a categoria correta: `InvalidParameter` (400 entrada), `ValidationFailed` (422 semântica), `NotFound` (404), `InvalidState` (409), `NotAllowed` (403), `InternalServerError` (500), `CustomProblem` (typeId descrito).
  - Use `Property` para apontar o campo e `With(key, value)` para contexto adicional; `ChainProperty(parent[, index])` para caminhos.
- Resultados e composição
  - Retorne `Result`/`Result<T>` em serviços/handlers; componha com `Map`, `Continue`, `Match` e `MatchAsync`.
  - Evite exceções para fluxos esperados; converta para problemas e propague via `Result`.
- APIs Web (servidor)
  - Converta `Result`/`Result<T>` em `OkMatch`, `CreatedMatch` (com `Location`) e `NoContentMatch`.
  - Configure `ProblemDetailsOptions` e descreva `typeId` para problemas customizados; respeite RFC 9457 (`type`, `title`, `status`, `detail`, `instance`).
- Entity Framework
  - Use `TryFindAsync`/`TryFindByAsync` para obter `FindResult`; converta para `Result` com `ToResult([param])`.
  - Ao não encontrar, retorne `NotFound` padronizado com `Extensions` (`id`, `entity`, `property/value`).
- Cliente HTTP
  - Consuma com `ToResultAsync` (valor ou problemas); trate `application/problem+json` e use `FailureTypeReader` para conteúdos não-ProblemDetails.
- Validação
  - Implemente `HasProblems(out Problems?)` ou use FluentValidation (`EnsureIsValid`, `ToProblems`) para criar `Problems`.
- Performance e observabilidade
  - Prefira `Result` (`readonly struct`) para menor alocação; use `With`/`Extensions` para dados de diagnóstico.

Padrões de prompt para Copilot:
- “Implemente um serviço que valide entrada com FluentValidation, retorne `Result` e mapeie para `CreatedMatch` com Location.”
- “Crie uma consulta EF com `TryFindByAsync` por `Name`; retorne `OkMatch<T>` quando encontrado e `ProblemDetails 404` quando não.”
- “Compose um `Result<Order>` em `Result<OrderDto>` usando `Map` e trate falhas com `Match` → `Problems.AsResult()`.”
- “Defina um `Problems.Custom` com `typeId` e configure `ProblemDetailsOptions` seguindo RFC 9457 (URI absoluta em `type`).”
- “Consuma um endpoint com `HttpClient` e `ToResultAsync<T>`; em falha, itere `Problems` e exiba `category`/`detail`.”