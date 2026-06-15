# Documentação da API SmartSelector (Selectors, AutoSelect, AutoProperties)

Esta documentação apresenta os conceitos, funcionalidades e exemplos práticos para usar a família de bibliotecas SmartSelector em projetos .NET (alvo: .NET 8, .NET 9 e .NET 10 runtime; Generator .NET Standard 2.0 como Analyzer). Também serve como referência para ferramentas de IA (ex.: GitHub Copilot) gerarem código correto com base na API e nos padrões desta solução. SmartSelector integra-se naturalmente com SmartSearch para compor consultas EF Core com projeções tipadas.

Sumário
1. Introdução
2. Pacotes e responsabilidades
3. Conceitos centrais
4. Atributos disponíveis e regras
5. Membros gerados pelo Source Generator
6. Propriedades suportadas em `AutoProperties`
7. Flattening (caminhos aninhados por convenção)
8. MapFrom (alias explícito de origem)
9. Extensões de `IQueryable`/`IEnumerable`
10. Integração com SmartSearch
11. Exemplos de uso
12. Boas práticas
13. Limitações e diagnósticos
14. Instruções para Ferramentas de IA (GitHub Copilot)

## 1. Introdução

SmartSelector é um Roslyn Source Generator que cria automaticamente projeções fortemente tipadas para DTOs, reduzindo boilerplate em consultas LINQ/EF Core. A partir de classes parciais anotadas com atributos, o gerador produz:
- `Expression<Func<TFrom, TModel>>` reutilizáveis e traduzíveis pelo EF Core.
- Métodos de fábrica `From(TFrom)` com cache de delegate.
- Métodos de extensão para `IQueryable<TFrom>` e `IEnumerable<TFrom>`.
- Propriedades simples em DTOs via `AutoProperties`.
- Flattening por convenção (nomes concatenados em PascalCase → caminhos aninhados).

Foco: mapeamento 1x1 estilo Adapt (Mapster), sem resolvers customizados nem naming policies.

## 2. Pacotes e responsabilidades

- `RoyalCode.SmartSelector` (runtime):
  - Tipos base e atributos: `AutoSelectAttribute`, `AutoPropertiesAttribute`, `MapFromAttribute`.
  - Extensões de seleção para `IQueryable`/`IEnumerable` geradas por modelo.

- `RoyalCode.SmartSelector.Generators` (analyzer .NET Standard 2.0):
  - Gera código parcial (*.g.cs) a partir dos atributos.
  - Emite diagnósticos quando o uso é incorreto.

## 3. Conceitos centrais

- `Model`/`Details`: DTO alvo da projeção (classe parcial).
- `TFrom`: tipo origem da projeção (entidade EF, record, classe).
- `AutoSelect<TFrom>`: habilita geração de expressão e extensões.
- `AutoProperties` / `AutoProperties<TFrom>`: gera propriedades simples automaticamente.
- Flattening: casamento por convenção de PascalCase para caminhos aninhados.
- `MapFrom`: alias explícito de origem para propriedades do DTO.

## 4. Atributos disponíveis e regras

### 4.1 `AutoSelect<TFrom>`
- Declara o tipo de origem e ativa geração de:
  - `Select{TFrom}Expression` em `Model`.
  - Métodos de extensão `Select{Model}` para `IQueryable<TFrom>`/`IEnumerable<TFrom>`.
- Regras:
  - A classe alvo deve ser `partial`.
  - Opcionalmente combine com `AutoProperties`.

### 4.2 `AutoProperties` e `AutoProperties<TFrom>`
- Gera propriedades simples no DTO com base em `TFrom`.
- Quando usado sem genérico, o `TFrom` é inferido do `AutoSelect<TFrom>`.
- Suporta exclusão por `Exclude = [ nameof(T.Prop) ]`.

### 4.3 `MapFromAttribute`
- Permite mapear explicitamente uma propriedade do DTO a um membro de `TFrom` por nome.
- Assinatura:
  - `new MapFromAttribute(string propertyName)`.
- Preferir `nameof(TFrom.Member)` para segurança em refactors.

## 5. Membros gerados pelo Source Generator

Em um `Model` anotado:
- Campo de cache: `private static Func<TFrom, Model> select{TFrom}Func;`
- Expressão: `public static Expression<Func<TFrom, Model>> Select{TFrom}Expression => a => new Model { ... }`.
- Fábrica: `public static Model From(TFrom a) => (selectFunc ??= SelectExpression.Compile())(a);`
- Extensões (classe estática):
  - `IQueryable<Model> SelectModel(this IQueryable<TFrom> query)`.
  - `IEnumerable<Model> SelectModel(this IEnumerable<TFrom> enumerable)`.
  - `Model ToModel(this TFrom source)`.

Todos visam manter consultas traduzíveis pelo EF Core.

## 6. Propriedades suportadas em `AutoProperties`

- Primitivos numéricos, `bool`, `string`, `char`.
- `DateTime` e nullables simples.
- `enum`, `struct`.
- `IEnumerable<T>` onde `T` é dos tipos acima/enum/struct.
- Exclusões por `Exclude` para membros indesejados.

## 7. Flattening (caminhos aninhados por convenção)

- Nomes de propriedades do DTO em PascalCase mapeiam cadeias da origem:
  - `CustomerAddressCity` → `a.Customer.Address.City`.
  - `CustomerAddressCountryRegionName` → `a.Customer.Address.Country.Region.Name`.
- Regras:
  - Segmentos devem corresponder a propriedades navegáveis.
  - Último segmento é o valor terminal (simples/suportado).
  - Evite colisões de prefixo; declare DTOs aninhados ou use `MapFrom`.

## 8. MapFrom (alias explícito de origem)

- Use quando o nome do DTO não segue convenção de flattening ou há ambiguidade.
- Exemplo:
  - `CustomName` mapeado de `nameof(Product.Name)`.
- Continua traduzível pelo EF Core por estar na `Expression` gerada.

## 9. Extensões de `IQueryable`/`IEnumerable`

- `Select{Model}(this IQueryable<TFrom>)` aplica a `Expression` gerada.
- `Select{Model}(this IEnumerable<TFrom>)` usa `From`.
- `To{Model}(this TFrom)` cria instância única.

## 10. Integração com SmartSearch

- Combine `SmartSelector` com `SmartSearch` para compor `Criteria` e projeção:
  - `criteria.Select<Dto>()` (SmartSearch) pode utilizar mapeamento por nome/caminho.
  - Alternativamente, use os métodos `Select{Model}` gerados pelo SmartSelector.
- Diretrizes:
  - Projeções devem ser traduzíveis (evite lógica client-side não suportada).
  - Ordenação/paginação em SmartSearch funciona sobre `IQueryable` antes da projeção.

## 11. Exemplos de uso

### 11.1 Projeção simples + propriedades auto
```csharp
[AutoSelect<User>, AutoProperties]
public partial class UserDetails { }

// Uso EF Core
var list = db.Users.SelectUserDetails().ToList();
var dto  = UserDetails.From(user);
var expr = UserDetails.SelectUserExpression;
```

### 11.2 Objeto aninhado + exclusão
```csharp
[AutoSelect<Book>, AutoProperties(Exclude = [ nameof(Book.Sku) ])]
public partial class BookDetails
{
    public ShelfDetails Shelf { get; set; }
}

[AutoProperties<Shelf>]
public partial class ShelfDetails { }

// Trecho gerado
// new BookDetails {
//   Shelf = new ShelfDetails { Id = a.Shelf.Id, Location = a.Shelf.Location },
//   Price = a.Price,
//   // Sku excluído
// }
```

### 11.3 Flattening profundo
```csharp
public class Order { public Customer Customer { get; set; } }

[AutoSelect<Order>]
public partial class OrderDetails
{
    public string CustomerAddressCountryRegionName { get; set; }
}

// Expressão: CustomerAddressCountryRegionName = a.Customer.Address.Country.Region.Name
```

### 11.4 Alias explícito com `MapFrom`
```csharp
[AutoSelect<Product>]
public partial class CustomProductDetails
{
    [MapFrom(nameof(Product.Id))]
    public Guid CustomId { get; set; }

    [MapFrom(nameof(Product.Name))]
    public string CustomName { get; set; }
}
```

### 11.5 Apenas propriedades automáticas
```csharp
[AutoProperties<Product>]
public partial class ProductSnapshot { }
```

## 12. Boas práticas

- Prefira consumir a `Expression` gerada (`Select{TFrom}Expression`) para reutilização e composição LINQ.
- Use `nameof` em `Exclude` e `MapFrom` para segurança.
- Em DTOs com caminhos longos, avalie modelos aninhados por clareza.
- Integre com SmartSearch antes da projeção quando precisar de paginação/ordenadores.
- Evite adicionar lógica não traduzível na projeção.

## 13. Limitações e diagnósticos

- Sem resolvers customizados ou transformações de tipo.
- Sem naming policies/renome global; use `MapFrom` para alias.
- Colisões de prefixo em flattening exigem desambiguação manual.
- Principais diagnósticos (exemplos):
  - Classe não `partial` ou uso inválido (`RCSS000`).
  - Propriedade não encontrada (`RCSS001`).
  - Tipos incompatíveis (`RCSS002`).
  - Uso incorreto de atributos (`RCSS003–RCSS005`).

## 14. Instruções para Ferramentas de IA (GitHub Copilot)

Objetivo: gerar projeções seguindo o contrato da API (`AutoSelect`, `AutoProperties`, `MapFrom`) e produzir código traduzível pelo EF Core.

Princípios obrigatórios
- Anote DTOs com `AutoSelect<TFrom>` e (opcional) `AutoProperties`.
- Use `MapFrom` quando o nome do DTO não seguir a convenção ou houver ambiguidade.
- Não crie resolvers customizados; foque em mapeamento 1x1.
- Utilize métodos gerados `Select{Model}`/`To{Model}` em consultas.

Padrões de implementação
- DTO mínimo:
  - `public partial class Dto { }` com `[AutoSelect<T>, AutoProperties]`.
- Flattening:
  - Propriedades `PascalCase` concatenadas para acessar caminhos aninhados.
- Exclusões:
  - `Exclude = [ nameof(T.Prop) ]` em `AutoProperties`.
- Integração com SmartSearch:
  - Aplique `criteria.FilterBy(...)`/`OrderBy(...)` sobre `IQueryable` e depois `Select{Model}`.

Antipadrões (evitar)
- Inserir lógica não traduzível em expressões geradas.
- Criar mapeamentos com conversões complexas/formatters.
- Usar exceções como fluxo de composição; prefira expressão + métodos gerados.

Exemplos de prompts corretos
- "Crie um DTO com `[AutoSelect<User>, AutoProperties]` e projete com `db.Users.SelectUserDetails().ToList()`."
- "Declare `OrderDetails` com flattening `CustomerAddressCountryRegionName` e consuma `SelectOrderExpression`."
- "Mapeie `CustomProductDetails` usando `[MapFrom(nameof(Product.Name))]` e gere expressões traduzíveis." 

---
Happy coding!

