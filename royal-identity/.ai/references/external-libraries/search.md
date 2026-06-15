# Documentação da API SmartSearch (Filters, Specifiers, Selectors, Sorting, Criteria)

Esta documentação apresenta os conceitos, funcionalidades e exemplos práticos para usar a família de bibliotecas SmartSearch em projetos .NET 
(alvo: .NET 8, .NET 9 e .NET 10). Também serve como referência para ferramentas de IA (ex.: GitHub Copilot)
para gerar código correto com base na API e nos padrões desta solução.

Sumário
1. Introdução
2. Pacotes e responsabilidades
3. Conceitos centrais
4. Como o filtro é resolvido em expressão
5. Disjunção (OR) por grupo e por nome/caminho
6. Sorting e paginação com ResultList
7. Projeção para DTO (Selector)
8. Orquestração com Criteria e EF Core
9. Exemplos de uso
10. Pontos de extensão e configuração
11. Boas práticas
12. Resumo
13. Instruções para Ferramentas de IA (GitHub Copilot)

## 1. Introdução

SmartSearch implementa o padrão Specification/Filter-Specifier para compor filtros declarativos em `IQueryable`, gerar expressões LINQ traduzíveis pelo EF Core, aplicar ordenação e paginação, e projetar entidades em DTOs através de `Selector`. O objetivo é criar componentes de busca reutilizáveis, desacoplados, testáveis e extensíveis, evitando lógica manual e repetitiva.

Benefícios principais:
- Filtros declarativos via atributos e convenções, com ignorância de valores vazios.
- Composição automática de predicados (Equal, Like, In, Range etc.).
- OR-disjunction por grupos e por nomes/caminhos contendo `Or`.
- Ordenação dinâmica por nomes de propriedades (inclusive caminhos aninhados).
- Projeção expressiva para DTO (nested, coleções, enums, nullables).
- Integração com EF Core via `ISearchManager<TDbContext>` e `ICriteria<TEntity>`.
- Extensões para fábricas de predicados e geradores de expressão customizados.

## 2. Pacotes e responsabilidades

- `RoyalCode.SmartSearch.Core`: Abstrações para filtro, specifier, resultado (inclui paginação), sorting e seleção.
- `RoyalCode.SmartSearch.Linq`: Resolução de propriedades, geração de expressões, `Selector` e sorting dinâmico.
- `RoyalCode.SmartSearch.EntityFramework`: Integração EF Core, DI e `ISearchManager<TDbContext>`/`ICriteria<TEntity>`.
- `RoyalCode.SmartSearch.Abstractions`: Contratos e interfaces compartilhadas.

## 3. Conceitos centrais

- `Filter`: objeto com propriedades que representam critérios de busca. Atributos nas propriedades controlam operador, negação, caminho de destino (`TargetPropertyPath`), e regras de ignorar valores vazios.
- `Specifier`: aplica o filtro em um `IQueryable<TModel>` construindo a expressão-predicado.
- `Selector`: mapeia entidades para DTOs via expressão gerada automaticamente.
- `Sorting`: define ordenação dinâmica por propriedades, usado também na construção de `ResultList` com metadados.
- `Criteria`: orquestra filtro, ordenação, paginação, projeção e coleta dos resultados.

Principais atributos e opções (exemplos):
- `Criterion`: define o alvo e operador; `TargetPropertyPath` pode apontar caminho aninhado.
- `Disjuction("alias")`: agrupa propriedades em OR.
- `ComplexFilter`: elege um membro para tratamento de filtro complexo (nested/owned types).
- `FilterExpressionGenerator<TGenerator>`: delega a criação da expressão para gerador customizado.
- `DisableOrFromName` (em `Criterion`): desativa a inferência automática de OR quando o nome da propriedade (ou caminho) contém o token `Or`; trata o membro como critério único.

## 4. Como o filtro é resolvido em expressão

A pipeline de resolução constrói uma lista de resoluções (`ICriterionResolution`) por propriedade do filtro e compõe uma única expressão final.

Etapas (alto nível):
1) Fábricas de predicado configuradas: propriedades mapeadas manualmente geram resoluções dedicadas.
2) Disjunção por `[Disjuction]`: membros do mesmo grupo se tornam uma única resolução com OR.
3) `Or` em nome/caminho: propriedades ou paths com `Or` são divididos em partes e resolvidos como OR.
4) ComplexFilter: propriedades com `[ComplexFilter]` (no tipo ou no membro) são tratadas como filtro composto.
5) Padrão: `DefaultOperatorCriterionResolution` escolhe o operador conforme o tipo (strings → Like; coleções → In; numéricos/datas → Equal), com guardas para ignorar valores vazios.

Ignorar valores vazios ("IgnoreIfIsEmpty") cobre: `string` em branco, `Nullable<T>` não definido, coleções vazias, structs default.

## 5. Disjunção (OR) por grupo e por nome/caminho

- `[Disjuction("g1")]`: quando múltiplas propriedades compartilham o mesmo alias, aplicam OR entre elas; entradas vazias são ignoradas.
- `Or` em propriedade/caminho: nomes como `FirstNameOrMiddleNameOrLastName` ou paths como `FirstNameOrLastName` dividem em vários destinos, combinados com OR.
  - Opt-out por propriedade: use `[Criterion(DisableOrFromName = true)]` quando o token `Or` fizer parte do nome mas não indicar disjunção (ex.: `ColorOrSizePreference`).

Casos esperados:
- Todos vazios → nenhum `Where` aplicado.
- Um valor presente → condição única.
- Múltiplos valores → OR entre as condições.

## 6. Sorting e paginação com ResultList

`Sorting` permite ordenar por nome de propriedade e direção. A paginação é modelada em `ResultList<T>` com campos: `Page`, `ItemsPerPage`, `Count`, `Pages`, `Sortings`, e `Items`.

Compatibilidade: ordenações podem ser fornecidas via JSON/string e são serializáveis.

## 7. Projeção para DTO (Selector)

`DefaultSelectorExpressionGenerator.Generate<TEntity, TDto>()` mapeia:
- Propriedades de mesmo nome.
- Caminhos aninhados (ex.: `Complex.Value` → `ComplexValue`).
- Normalização de `Nullable<T>` para tipos não-nulos em DTO.
- Enum ↔ enum com conversão de valor.
- Sub-selects aninhados e coleções (element mapping), inclusive multi-nível.

## 8. Orquestração com Criteria e EF Core

Registre as entidades e configurações no DI e use `ISearchManager<TDbContext>` para obter `ICriteria<TEntity>`:

```csharp
services.AddEntityFrameworkSearches<MyDbContext>(cfg =>
{
    cfg.Add<MyEntity>();
    cfg.AddOrderBy<MyEntity, string>("Name", x => x.Name.First);
    cfg.AddSelector<MyEntity, MyDto>(x => new MyDto { Id = x.Id, Name = x.Name.First });
});

var manager = provider.GetRequiredService<ISearchManager<MyDbContext>>();
var criteria = manager.Criteria<MyEntity>();
```

`ICriteria<TEntity>` fornece:
- `FilterBy<TFilter>(filter)`
- `OrderBy(ISorting)`/`OrderBy(IEnumerable<ISorting>)`
- `Select<TDto>()`
- `Collect()`/`CollectAsync()` e modo busca `AsSearch().ToList()/ToListAsync()`
- `Exists()`, `FirstOrDefault()`, `Single()` com validação de cardinalidade

Diferenças entre `Collect/CollectAsync` e `AsSearch().ToList/ToListAsync`:
- `Collect/CollectAsync`: retorna a coleção de itens já filtrados/ordenados/projetados, sem metadados de paginação. Em EF Core, mantém as entidades anexadas ao `ChangeTracker` (rastreadas), permitindo atualizações subsequentes e detecção de mudanças.
- `AsSearch().ToList/ToListAsync`: retorna `ResultList<T>` com metadados de busca (Page, ItemsPerPage, Count, Pages, Sortings) aplicando defaults configurados. Use quando precisa de paginação, ordenação serializável e informações agregadas para UI/APIs.

Quando usar:
- Use `Collect`/`CollectAsync` para rotinas internas, processamento batch, cenários em que você pretende modificar entidades (EF Core tracking) ou precisa manter o estado rastreado após a consulta. Evite em endpoints públicos se não precisar de tracking para reduzir overhead.
- Use `AsSearch().ToList/ToListAsync` em APIs/UI que exibem páginas e ordenações, quando não precisa manter entidades rastreadas pelo EF Core, priorizando resultados materializados com metadados e menor custo de tracking.

## 9. Exemplos de uso

### 9.1. Busca simples com filtro
```csharp
public sealed class SimpleModel { public int Id { get; set; } public string Name { get; set; } = string.Empty; }
public sealed class SimpleFilter { [Criterion] public string? Name { get; set; } }

var criteria = provider.GetRequiredService<ICriteria<SimpleModel>>();
criteria.FilterBy(new SimpleFilter { Name = "B" });
var results = criteria.Collect(); // retorna apenas registros com Name semelhante a "B" (Like)
```

### 9.2. OR por nome de propriedade
```csharp
public sealed class PersonFilter
{
    [Criterion] // string → operador Like
    public string? FirstNameOrMiddleNameOrLastName { get; set; }
}

var res = criteria.FilterBy(new PersonFilter { FirstNameOrMiddleNameOrLastName = "Ann" }).Collect();
// Aplica OR entre os paths FirstName, MiddleName e LastName ignorando campos vazios
```

### 9.3. OR por TargetPropertyPath
```csharp
public sealed class QueryFilter
{
    [Criterion(TargetPropertyPath = "FirstNameOrLastName")] public string? Query { get; set; }
}

var res = criteria.FilterBy(new QueryFilter { Query = "Jo" }).Collect();
// Aplica OR entre FirstName e LastName
```

### 9.4. Disjunção por grupo
```csharp
public sealed class DisjunctionFilter
{
    [Disjuction("g1")] public string? Email { get; set; }
    [Disjuction("g1")] public string? Phone { get; set; }
}

var res = criteria.FilterBy(new DisjunctionFilter { Email = "@domain" }).Collect();
// Apenas Email gerará condição; Phone vazio é ignorado; múltiplos valores criam OR
```

### 9.5. ComplexFilter (tipos complexos/owned)
```csharp
public readonly record struct Email(string Value);
public sealed class User { public Email? Email { get; set; } }

public sealed class UserFilter
{
    [Criterion("Email.Value")] public string? Email { get; set; } // mapeia para caminho aninhado
}

criteria.FilterBy(new UserFilter { Email = "@royalcode" });
var list = criteria.Collect();
```

### 9.6. Sorting e paginação
```csharp
criteria.OrderBy(new Sorting { OrderBy = "Name", Direction = ListSortDirection.Ascending });
var page1 = criteria.AsSearch().ToList();
// page1 contém metadados (Page, ItemsPerPage, Count, Pages, Sortings)
```

### 9.7. Projeção para DTO
```csharp
public sealed class PersonDto { public int Id { get; set; } public string Name { get; set; } = string.Empty; }

criteria.Select<PersonDto>();
var dtos = criteria.AsSearch().ToList();
// Projeta conforme configurado (por nome ou configurador AddSelector)
```

### 9.8. Cardinalidade e existência
```csharp
criteria.FilterBy(new SimpleFilter { Name = "A" });
var exists = criteria.Exists(); // true/false
var first = criteria.FirstOrDefault();
var single = criteria.Single(); // lança se houver 0 ou >1; use filtros/ordenadores adequados
```

## 10. Pontos de extensão e configuração

Via `ISearchConfigurations`:
- `Add<TEntity>()`: registra entidade para buscas.
- `AddOrderBy<TEntity, TKey>(name, keySelector)`: registra ordenação por nome.
- `AddSelector<TEntity, TDto>(expr)`: define seleção para DTO.
- `ConfigureSpecifierGenerator<TEntity, TFilter>(opt => opt.For(f => f.Prop).Predicate(val => e => ...))`: mapeia fábrica de predicado customizada.

`FilterExpressionGenerator<T>`: implemente `ISpecifierExpressionGenerator` para cenários complexos (ex.: períodos):
```csharp
public sealed class PeriodSpecifierExpressionGenerator : ISpecifierExpressionGenerator
{
    public static Expression GenerateExpression(ExpressionGeneratorContext ctx)
    {
        // constrói a expressão Where com range calculado
        // retorno é uma atribuição no `ctx.Query` com Queryable.Where(...)
        // ver exemplos em README
        throw new NotImplementedException();
    }
}
```

## 11. Boas práticas

- Prefira filtros declarativos com atributos e caminhos aninhados claros.
- Use OR por grupos (`Disjuction`) ou por `Or` em nomes/caminhos quando fizer sentido de negócio.
- Configure ordenações nomeadas via `AddOrderBy` para evitar strings mágicas espalhadas.
- Projeção por `Selector` mantém consultas traduzíveis pelo EF Core; evite lógica não traduzível.
- Ignore vazios para não poluir o `Where` com condições inúteis; valide entrada com SmartValidations.
- Em APIs, componha com SmartProblems: converta falhas para `ProblemDetails` (RFC 9457).

## 12. Resumo

SmartSearch fornece uma forma padronizada, declarativa e performática de compor buscas em `IQueryable` e EF Core. Com `Filter` + `Specifier`, você gera predicados automaticamente; com `Sorting` e `ResultList`, você controla ordenação e paginação; com `Selector`, projeta DTOs complexos de forma segura. A integração via `ICriteria<TEntity>` orquestra todas as etapas (filtro, ordenação, seleção e coleta), e pontos de extensão permitem adaptar o comportamento a regras avançadas.

## 13. Instruções para Ferramentas de IA (GitHub Copilot)

Objetivo: gerar buscas seguindo o contrato da API (Filter + Specifier + Criteria) e produzir consultas EF Core traduzíveis.

Princípios obrigatórios
- Modele filtros como classes com propriedades e anote com `Criterion`/`Disjuction`/`ComplexFilter` quando necessário.
- Use `ICriteria<TEntity>` para aplicar `FilterBy`, `OrderBy`, `Select` e `Collect`/`AsSearch`.
- Evite construir `Expression` manualmente quando um atributo cobre o caso; use `FilterExpressionGenerator<T>` apenas para cenários avançados.
- Preserve nomes/caminhos de propriedades usando `TargetPropertyPath` e convenções com `Or` para OR.

Padrões de implementação
- Filtro simples: `class Filter { [Criterion] public string? Name { get; set; } }` e `criteria.FilterBy(filter)`.
- Disjunção: `[Disjuction("g1")]` em múltiplos membros do filtro para OR.
- OR por caminho: `[Criterion(TargetPropertyPath = "FirstNameOrLastName")]`.
- Projeção: `criteria.Select<Dto>()` usando mapeamento por nome ou configurado.
- Ordenação: `criteria.OrderBy(new Sorting { OrderBy = "Name" })`.
- Coleta vs Busca paginada: para lista simples, prefira `Collect/CollectAsync`; para UI/APIs com paginação e metadados, use `AsSearch().ToList/ToListAsync` e leia `ResultList`.
- EF Core Tracking: `Collect` mantém entidades rastreadas no `ChangeTracker`; `AsSearch().ToList` não anexa entidades. Escolha conforme necessidade de atualização subsequente vs desempenho.

Integrações
- EF Core: obtenha `ICriteria<TEntity>` via `ISearchManager<TDbContext>` e registre com `AddEntityFrameworkSearches<TDbContext>(cfg => cfg.Add<TEntity>())`.
- SmartProblems/SmartValidations: valide entrada e converta falhas para `ProblemDetails` antes da busca.

Antipadrões (evitar)
- Criar `Where` manual com strings de propriedade sem usar atributos/convenções da lib.
- Usar expressões não traduzíveis pelo EF em `Selector`.
- Ignorar valores vazios e forçar filtros que resultam em consultas ineficientes.

Exemplos de prompts corretos
- "Implemente um filtro com `[Disjuction("g1")]` para Email/Phone e aplique com `ICriteria<User>.FilterBy(filter)` retornando `ResultList`."
- "Crie um `Selector` para `Order -> OrderDto` e projete com `criteria.Select<OrderDto>().AsSearch().ToList()`."
- "Configure `AddOrderBy<Order,string>(\"CustomerName\", x => x.Customer.Name)` e gere `criteria.OrderBy(new Sorting { OrderBy = \"CustomerName\" })`."
