# SmartSelector — Regras para IA

Regras operacionais para gerar código com SmartSelector em projetos .NET. Para explicações e contexto, consulte [`selector.md`](selector.md).

> **Verificado contra:** `RoyalCode.SmartSelector` e `RoyalCode.SmartSelector.Generators` **0.5.0** — .NET 8 / 9 / 10; generator `netstandard2.0` como analyzer.
> **Precedência das fontes:** documentação XML/IntelliSense da versão instalada > este arquivo > [`selector.md`](selector.md).
> Com versão divergente, confirme atributos, membros gerados e diagnósticos no IDE.

## 1. Pacotes e `using`

Instale os dois pacotes na mesma versão:

```xml
<ItemGroup>
  <PackageReference Include="RoyalCode.SmartSelector" Version="0.5.0" />
  <PackageReference Include="RoyalCode.SmartSelector.Generators"
                    Version="0.5.0"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

| API | `using` | Pacote |
|---|---|---|
| `AutoSelect`, `AutoProperties`, `AutoDetails`, `MapFrom` | `RoyalCode.SmartSelector` | `RoyalCode.SmartSelector` |
| arquivos gerados e diagnósticos `RCSS*` | nenhum namespace consumidor | `RoyalCode.SmartSelector.Generators` |

Nunca referencie o projeto/pacote do generator como assembly de runtime.

## 2. Regras invioláveis

1. Declare todo DTO processado como `partial`.
2. Declare também como `partial` cada tipo que contém um DTO aninhado.
3. Coloque DTOs de `AutoSelect` dentro de um namespace.
4. Não gere DTO de destino genérico nem dentro de tipo genérico.
5. Use `[AutoSelect<TFrom>, AutoProperties]`, nunca `[AutoSelect<TFrom>, AutoProperties<TFrom>]`.
6. Use `[AutoProperties<TFrom>]` somente quando quiser propriedades sem expressão, `From` ou extensões.
7. Não presuma que `[AutoSelect<TFrom>]` sozinho cria propriedades; declare-as ou adicione `AutoProperties`.
8. Trate `Exclude` e `Flattening` como case-sensitive e prefira `nameof` para membros diretos.
9. Use `MapFrom` para renome ou caminho explícito; cada segmento deve ser propriedade pública legível.
10. Faça o destino nullable quando a origem ou uma navegação do caminho puder ser null; não ignore `RCSS015`.
11. Não edite arquivos `.g.cs`.
12. Não invente resolvers, callbacks, formatters ou configuração DI: essas APIs não existem no SmartSelector 0.5.0.
13. Para cálculos e transformações customizadas, escreva uma expressão LINQ manual.
14. Em `IQueryable<T>`, use a expressão/extensão gerada e valide tradução com o provider real.

## 3. Matriz de decisão

| Necessidade | Gere |
|---|---|
| DTO simples com propriedades automáticas | `[AutoSelect<Entity>, AutoProperties] partial class EntityDetails` |
| DTO com subconjunto explícito | `[AutoSelect<Entity>]` e propriedades manuais |
| DTO automático sem campos sensíveis | `AutoProperties(Exclude = [nameof(...)])` |
| opções diretamente no `AutoSelect` | `AutoSelect<Entity>(Exclude = [...], Flattening = [...])` |
| gerar somente propriedades | `[AutoProperties<Entity>]` |
| alias de membro direto | `[MapFrom(nameof(Entity.Member))]` |
| caminho aninhado explícito | `[MapFrom("Navigation.Member")]` |
| flattening profundo já conhecido | propriedade manual `CustomerAddressCity` |
| gerar flattening de uma navegação raiz | `Flattening = [nameof(Entity.Address)]` |
| gerar/completar tipo de detalhe aninhado | `[AutoDetails]` na propriedade |
| objeto aninhado com forma customizada | declare o DTO aninhado manualmente |
| coleção de objetos | declare `IReadOnlyList<ItemDetails>` ou `ItemDetails[]` |
| cálculo, agregação, formatação | `Expression<Func<Entity,Dto>>` manual |

## 4. Padrão canônico

```csharp
using RoyalCode.SmartSelector;

namespace MyApp.Users;

public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}

[AutoSelect<User>,
 AutoProperties(Exclude = [nameof(User.PasswordHash)])]
public partial class UserDetails { }
```

Consumo:

```csharp
var list = await db.Users
    .SelectUserDetails()
    .ToListAsync(ct);

UserDetails dto = UserDetails.From(user);
UserDetails dto2 = user.ToUserDetails();

Expression<Func<User, UserDetails>> expression =
    UserDetails.SelectUserExpression;
```

Nomes gerados para origem `User` e destino `UserDetails`:

```csharp
UserDetails.SelectUserExpression;
UserDetails.From(User user);
query.SelectUserDetails();       // IQueryable<User>
items.SelectUserDetails();       // IEnumerable<User>
user.ToUserDetails();            // User
```

## 5. Escolha de `AutoProperties`

Projeção e propriedades:

```csharp
[AutoSelect<Product>, AutoProperties]
public partial class ProductDetails { }
```

Somente propriedades:

```csharp
[AutoProperties<Product>]
public partial class ProductSnapshot { }
```

Configuração direta também ativa propriedades automáticas:

```csharp
[AutoSelect<Order>(
    Exclude = [nameof(Order.InternalCode)],
    Flattening = [nameof(Order.Customer)])]
public partial class OrderDetails { }
```

Não gere:

```csharp
[AutoSelect<Product>, AutoProperties<Product>] // RCSS003
public partial class ProductDetails { }
```

Nem:

```csharp
[AutoProperties] // RCSS007: falta AutoSelect<TFrom>
public partial class ProductDetails { }
```

## 6. Tipos de propriedades automáticas

Inclua automaticamente apenas:

- `bool`, `char`, `string`;
- tipos numéricos e `decimal`;
- `DateTime`;
- enums e structs;
- versões nullable suportadas;
- arrays de tipos simples, enums ou structs;
- coleções genéricas que implementam `IEnumerable<T>` de tipo simples, enum ou struct.

Não espere geração automática de classes complexas. Para uma classe complexa, escolha uma destas formas:

```csharp
// Forma customizada, declarada manualmente.
public AddressDetails Address { get; set; } = new();

// Geração do tipo declarado.
[AutoDetails]
public AddressDetails Address { get; set; } = new();

// Propriedades achatadas automáticas.
[AutoSelect<Customer>(Flattening = [nameof(Customer.Address)])]
```

`AutoProperties` não duplica propriedades públicas já declaradas no DTO.

## 7. Precedência de mapeamento

Ao gerar uma propriedade do DTO, aplique esta preferência:

1. `MapFrom` com caminho aninhado explícito;
2. `MapFrom` com membro direto;
3. membro direto de mesmo nome;
4. flattening por nome concatenado;
5. projeção estrutural de objeto/coleção compatível.

Use `MapFrom` para eliminar ambiguidade. Não dependa da ordem de descoberta quando houver `RCSS010`.

## 8. `MapFrom`

Membro direto:

```csharp
[MapFrom(nameof(Product.Name))]
public string DisplayName { get; set; } = string.Empty;
```

Caminho aninhado:

```csharp
[MapFrom("Warehouse.Location")]
public string? Location { get; set; }
```

Regras:

- use `nameof` para membro direto;
- use string com pontos para caminho aninhado;
- exija propriedade pública legível em cada segmento;
- preserve maiúsculas/minúsculas exatas;
- faça o destino nullable se qualquer segmento puder ser null;
- caminho explícito prevalece sobre membro direto homônimo;
- trate `RCSS017` em caminho aninhado e `RCSS001` em nome direto sem correspondência como erros de contrato.

Não gere chamadas de método, indexadores ou expressões dentro de `MapFrom`:

```csharp
[MapFrom("Items[0].Name")] // inválido: caminho aninhado não legível
[MapFrom("GetName()")]     // inválido: não corresponde a uma propriedade direta
```

## 9. Flattening

Convenção para propriedade manual:

```csharp
[AutoSelect<Order>]
public partial class OrderDetails
{
    // source.Customer.Address.City
    public string CustomerAddressCity { get; set; } = string.Empty;
}
```

Geração automática a partir de navegação raiz:

```csharp
[AutoSelect<Customer>(Flattening = [nameof(Customer.Address)])]
public partial class CustomerDetails { }
```

Se `Address` contém `City` e `Zip`, espere `AddressCity` e `AddressZip`. Não espere a propriedade complexa `Address` nessa geração automática.

Se mais de um caminho produzir o mesmo nome, use `MapFrom`; `RCSS010` é warning de ambiguidade.

## 10. Objetos, coleções e arrays

Objeto aninhado manual:

```csharp
[AutoSelect<Post>]
public partial class PostDetails
{
    public AuthorDetails Author { get; set; } = new();
}

public class AuthorDetails
{
    public string Name { get; set; } = string.Empty;
}
```

Coleção de DTOs:

```csharp
public IReadOnlyList<CommentDetails> Comments { get; set; } = [];
```

Espere emissão com `Select(...).ToList()` quando o destino exigir lista.

Array de DTOs:

```csharp
public ItemDetails[] Items { get; set; } = [];
```

Espere emissão com `Select(...).ToArray()`. Arrays simples compatíveis podem ser gerados por `AutoProperties` e atribuídos diretamente.

## 11. `AutoDetails`

Padrão:

```csharp
[AutoSelect<Customer>, AutoProperties]
public partial class CustomerDetails
{
    [AutoDetails]
    public AddressDto Address { get; set; } = new();
}
```

Regras:

- use o tipo escrito na propriedade como nome exato do detalhe gerado;
- se esse tipo já existir, declare-o `partial`;
- não solicite o mesmo tipo em duas propriedades;
- mantenha a acessibilidade do tipo compatível com a propriedade;
- use `Exclude`/`Flattening` no próprio `AutoDetails` quando necessário;
- use somente dentro de um DTO cujo processamento automático esteja ativo.

Tipo parcial preexistente:

```csharp
public partial class AddressDto
{
    public string City { get; set; } = string.Empty;
}
```

O generator completa somente as propriedades ausentes.

## 12. Nulabilidade

Use esta matriz:

| Origem | Destino a gerar | Resultado |
|---|---|---|
| escalar nullable | nullable | propagação natural |
| navegação nullable | DTO nullable | condicional com `null` |
| caminho por pai nullable | valor nullable | condicional com `null`/`default(T?)` |
| coleção nullable | coleção nullable | condicional com `null` |
| coleção nullable | coleção não nullable | coleção vazia + `RCSS016` |
| array nullable | array não nullable | `Array.Empty<T>()` + `RCSS016` |
| qualquer origem nullable | destino não nullable escalar/objeto | `RCSS015`; corrija o contrato |

Preferência:

```csharp
public WarehouseDetails? Warehouse { get; set; }
public string? AddressCity { get; set; }
public IReadOnlyList<ItemDetails> Items { get; set; } = [];
```

Não silencie `RCSS015` sem decidir como o domínio trata `null`. O generator preserva o comportamento anterior; ele não cria valor default seguro para escalar ou objeto.

Código `#nullable disable` não recebe a mesma inferência baseada em anotações. Prefira `#nullable enable` em código novo.

## 13. DTOs aninhados e tipos de origem

DTO aninhado válido:

```csharp
public partial class Contracts
{
    [AutoSelect<User>]
    public partial class UserDetails
    {
        public int Id { get; set; }
    }
}
```

Exija `partial` no DTO e em `Contracts`.

Não gere:

```csharp
public partial class Details<T> { }       // RCSS008
public partial class Container<T>
{
    public partial class Details { }      // RCSS008
}
```

O tipo de origem pode ser qualificado, `global::`, aninhado ou genérico construído:

```csharp
[AutoSelect<global::Domain.Users.User>]
[AutoSelect<Domain.Envelope<string>>]
```

## 14. LINQ e Entity Framework Core

Para consulta, gere:

```csharp
var result = await db.Orders
    .Where(order => order.Active)
    .OrderBy(order => order.Id)
    .SelectOrderDetails()
    .ToListAsync(ct);
```

Ou:

```csharp
var query = db.Orders.Select(OrderDetails.SelectOrderExpression);
```

Regras:

- filtre e ordene antes da projeção quando usar membros exclusivos da entidade;
- não compile a expressão para passá-la ao `IQueryable`;
- use `From`/`ToDetails` somente para objetos já materializados;
- propague `CancellationToken` nos terminais async do EF Core;
- teste objetos, coleções, arrays e caminhos nullable com o provider real.

## 15. Diagnósticos — ação obrigatória

| ID | Ação |
|---|---|
| `RCSS000` | torne DTO e contêineres `partial` e corrija o uso de `AutoSelect` |
| `RCSS001` | alinhe nome, use `MapFrom` ou exclua a propriedade |
| `RCSS002` | alinhe os tipos ou use expressão manual |
| `RCSS003` | troque `AutoProperties<TFrom>` por `AutoProperties` junto de `AutoSelect` |
| `RCSS004` | remova uma das duas formas de `AutoProperties` |
| `RCSS005` | use classe ou struct válida como `TFrom` |
| `RCSS006` | adicione `partial` à classe de `AutoProperties<TFrom>` |
| `RCSS007` | adicione `AutoSelect<TFrom>` ou use `AutoProperties<TFrom>` |
| `RCSS008` | remova genéricos do DTO e dos tipos contêineres |
| `RCSS010` | desambigue com `MapFrom` ou renomeie |
| `RCSS011` | mova o DTO para um namespace |
| `RCSS012` | torne o tipo existente de `AutoDetails` parcial |
| `RCSS013` | deixe somente uma propriedade gerar o tipo de `AutoDetails` |
| `RCSS014` | aumente a acessibilidade do tipo de `AutoDetails` |
| `RCSS015` | torne o destino nullable, altere a origem ou exclua a propriedade |
| `RCSS016` | confirme conscientemente a semântica null → coleção vazia |
| `RCSS017` | corrija o caminho aninhado público legível de `MapFrom` |

Não invente `RCSS009`; ele não existe na versão 0.5.0.

## 16. Anti-padrões

- Não use `[AutoSelect<T>]` em classe vazia esperando propriedades.
- Não combine `AutoSelect<T>` com `AutoProperties<T>`.
- Não use `AutoProperties` não genérico sem `AutoSelect<T>`.
- Não declare DTO ou contêiner não `partial`.
- Não declare DTO de destino genérico.
- Não coloque DTO de `AutoSelect` no namespace global.
- Não use string para membro direto quando `nameof` é possível.
- Não use caminho `MapFrom` com método, campo, indexador ou segmento privado.
- Não ignore `RCSS010`, `RCSS015` ou `RCSS016` automaticamente.
- Não modele propriedade não nullable apenas para evitar `?`.
- Não espere que `Exclude` remova uma propriedade manual já declarada do DTO; ele controla geração automática.
- Não edite ou versione uma correção manual em `.g.cs` como fonte da solução.
- Não use `From` dentro de `IQueryable`; use a expressão/extensão gerada.
- Não crie configuração DI, resolver ou formatter inexistente.
- Não force SmartSelector em projeções calculadas; escreva expressão manual.
- Não assuma tradução idêntica entre providers EF Core.

## 17. Checklist antes de entregar o código

- [ ] Runtime e generator usam a mesma versão.
- [ ] `using RoyalCode.SmartSelector` está presente.
- [ ] DTO e tipos contêineres são `partial`.
- [ ] DTO está em namespace e não é genérico.
- [ ] A matriz de decisão escolheu a forma correta de `AutoProperties`.
- [ ] Campos sensíveis/internos estão em `Exclude` ou fora do DTO.
- [ ] `nameof` foi usado em `Exclude`, `Flattening` e `MapFrom` direto.
- [ ] Caminhos aninhados de `MapFrom` são públicos, legíveis e case-sensitive.
- [ ] Ambiguidade de flattening foi eliminada.
- [ ] Nulabilidade do destino representa a origem inteira do caminho.
- [ ] `RCSS015` e `RCSS016` foram avaliados conscientemente.
- [ ] Objetos e itens de coleção possuem propriedades compatíveis.
- [ ] `AutoDetails` não disputa o mesmo tipo e respeita acessibilidade/`partial`.
- [ ] `IQueryable` usa `Select{Dto}()` ou `Select{Source}Expression`.
- [ ] Projeção relevante foi testada com o provider real.
