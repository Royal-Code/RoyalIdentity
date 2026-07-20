# Documentação da API SmartSelector

SmartSelector é uma família de bibliotecas .NET que gera projeções fortemente tipadas entre entidades e DTOs. O Source Generator cria expressões LINQ reutilizáveis, métodos de conversão, extensões de consulta e, quando solicitado, propriedades do DTO.

Este é o guia conceitual e prático. Para instruções objetivas destinadas a ferramentas de IA, consulte também [`selector.ai-rules.md`](selector.ai-rules.md).

> **Verificado contra:** `RoyalCode.SmartSelector` e `RoyalCode.SmartSelector.Generators` **0.5.0** — runtime em .NET 8, .NET 9 e .NET 10; generator `netstandard2.0`, empacotado como analyzer.
> **Precedência das fontes:** documentação XML/IntelliSense da versão instalada > [`selector.ai-rules.md`](selector.ai-rules.md) > este guia.
> Se a versão do pacote for diferente, confirme as assinaturas e os diagnósticos no IDE antes de gerar código.

Sumário

1. Visão geral e conceitos
2. Pacotes, namespace e instalação
3. Escolhendo o atributo
4. Início rápido
5. Membros gerados
6. Como as propriedades são mapeadas
7. Propriedades automáticas
8. Flattening
9. `MapFrom`
10. Objetos, coleções e arrays
11. `AutoDetails`
12. Política de nulabilidade
13. DTOs aninhados, herança e tipos genéricos
14. Uso com LINQ e Entity Framework Core
15. Referência dos atributos
16. Diagnósticos
17. Erros comuns
18. Boas práticas

## 1. Visão geral e conceitos

O SmartSelector transforma uma declaração curta:

```csharp
[AutoSelect<User>, AutoProperties]
public partial class UserDetails { }
```

em uma projeção equivalente a:

```csharp
Expression<Func<User, UserDetails>> expression = user => new UserDetails
{
    Id = user.Id,
    Name = user.Name
};
```

Além da expressão, são gerados um método `From` e extensões para `IQueryable<T>`, `IEnumerable<T>` e uma instância isolada.

Conceitos principais:

| Conceito | Papel |
|---|---|
| tipo de origem (`TFrom`) | Entidade, POCO, record ou outro tipo público do qual os valores são lidos |
| DTO de destino | Classe `partial` que recebe a projeção |
| `AutoSelect<TFrom>` | Gera a projeção e os métodos de consumo |
| `AutoProperties` | Gera propriedades no DTO cuja origem já foi definida por `AutoSelect<TFrom>` |
| `AutoProperties<TFrom>` | Gera somente propriedades, sem projeção nem extensões |
| `AutoDetails` | Gera ou completa o tipo de uma propriedade aninhada do DTO |
| flattening | Mapeia um caminho aninhado por concatenação de nomes, como `AddressCity` → `Address.City` |
| `MapFrom` | Define explicitamente o nome ou caminho da propriedade de origem |

O objetivo é mapeamento declarativo 1:1. O projeto não oferece resolvers, formatters, callbacks ou políticas globais de nomes. Quando a projeção exige cálculo ou transformação de domínio, escreva uma expressão LINQ manual.

## 2. Pacotes, namespace e instalação

Instale o runtime e o generator:

```xml
<ItemGroup>
  <PackageReference Include="RoyalCode.SmartSelector" Version="0.5.0" />
  <PackageReference Include="RoyalCode.SmartSelector.Generators"
                    Version="0.5.0"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

| Necessidade | Namespace | Pacote |
|---|---|---|
| `AutoSelect`, `AutoProperties`, `AutoDetails`, `MapFrom` | `RoyalCode.SmartSelector` | `RoyalCode.SmartSelector` |
| geração dos arquivos `.g.cs` e diagnósticos `RCSS*` | não adiciona namespace ao código consumidor | `RoyalCode.SmartSelector.Generators` |

Importe no arquivo do DTO:

```csharp
using RoyalCode.SmartSelector;
```

O runtime suporta .NET 8, 9 e 10. O generator é distribuído como analyzer e não deve virar referência de runtime da aplicação.

## 3. Escolhendo o atributo

| Necessidade | Declaração recomendada |
|---|---|
| projeção com propriedades declaradas manualmente | `[AutoSelect<TEntity>]` |
| projeção com todas as propriedades simples compatíveis | `[AutoSelect<TEntity>, AutoProperties]` |
| projeção automática com exclusões ou flattening configurado | `[AutoSelect<TEntity>(Exclude = [...], Flattening = [...])]` |
| gerar somente a forma de um DTO, sem projeção | `[AutoProperties<TEntity>]` |
| renomear uma propriedade ou escolher um caminho exato | `[MapFrom(...)]` na propriedade do DTO |
| gerar a classe de uma propriedade aninhada | `[AutoDetails]` na propriedade do DTO |
| cálculo, agregação ou conversão customizada | expressão LINQ escrita manualmente |

`AutoSelect<TFrom>` sozinho não gera propriedades. A exceção é quando `Exclude` ou `Flattening` aparece como argumento nomeado: essa configuração também ativa a geração automática de propriedades.

## 4. Início rápido

Entidade:

```csharp
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}
```

DTO:

```csharp
using RoyalCode.SmartSelector;

namespace MyApp.Users;

[AutoSelect<User>, AutoProperties(Exclude = [nameof(User.PasswordHash)])]
public partial class UserDetails { }
```

Consumo:

```csharp
// IQueryable: aplica a Expression e permite tradução pelo provider.
var users = await db.Users
    .SelectUserDetails()
    .ToListAsync(ct);

// Instância isolada: usa o delegate compilado e armazenado em cache.
UserDetails details = UserDetails.From(user);
UserDetails details2 = user.ToUserDetails();

// Composição explícita.
Expression<Func<User, UserDetails>> selector =
    UserDetails.SelectUserExpression;
```

## 5. Membros gerados

Para `[AutoSelect<User>]` em `UserDetails`, o contrato usual é:

```csharp
public partial class UserDetails
{
    public static Expression<Func<User, UserDetails>> SelectUserExpression { get; }
    public static UserDetails From(User user);
}

public static class UserDetails_Extensions
{
    public static IQueryable<UserDetails> SelectUserDetails(
        this IQueryable<User> query);

    public static IEnumerable<UserDetails> SelectUserDetails(
        this IEnumerable<User> enumerable);

    public static UserDetails ToUserDetails(this User user);
}
```

Internamente, `From` compila `SelectUserExpression` uma vez e mantém o delegate em um campo estático. A extensão de `IQueryable<T>` usa a expressão original; a de `IEnumerable<T>` usa `From`.

Os nomes são formados a partir dos nomes dos tipos:

| Origem / destino | Membro gerado |
|---|---|
| `User` → `UserDetails` | `SelectUserExpression` |
| `IQueryable<User>` | `SelectUserDetails()` |
| `IEnumerable<User>` | `SelectUserDetails()` |
| `User` | `ToUserDetails()` |

O código gerado inclui cabeçalho de arquivo, anotações nullable, documentação XML e `[GeneratedCode]`. Se um membro herdado acessível tiver o mesmo nome de um membro gerado, o generator usa `new` para tornar a ocultação explícita.

## 6. Como as propriedades são mapeadas

Para cada propriedade declarada no DTO, o generator tenta montar uma atribuição nesta ordem conceitual:

1. caminho explícito de `[MapFrom("A.B")]`;
2. membro direto indicado por `[MapFrom(nameof(Entity.Member))]`;
3. membro direto com o mesmo nome da propriedade do DTO;
4. caminho por flattening do nome concatenado;
5. projeção estrutural de objeto ou coleção compatível.

Um `MapFrom` explícito prevalece sobre uma propriedade direta homônima. Por exemplo, `[MapFrom("Address.City")]` não será substituído por uma propriedade `AddressCity` existente na raiz da entidade.

O generator suporta atribuição direta, casts simples, casts entre enums, tratamento de `Nullable<T>`, criação de objetos aninhados e projeção de coleções. Tipos sem correspondência ou incompatíveis produzem diagnósticos em vez de código silenciosamente incorreto.

Propriedades públicas herdadas também participam do mapeamento. No DTO, propriedades já declaradas pelo usuário não são geradas novamente por `AutoProperties`, inclusive propriedades somente leitura.

## 7. Propriedades automáticas

### 7.1 Com `AutoSelect`

```csharp
[AutoSelect<Product>, AutoProperties]
public partial class ProductDetails { }
```

O tipo de origem de `AutoProperties` é inferido de `AutoSelect<Product>`. Não use `AutoProperties<Product>` na mesma classe de `AutoSelect<Product>`; essa combinação é inválida.

### 7.2 Sem projeção

```csharp
[AutoProperties<Product>]
public partial class ProductSnapshot { }
```

Essa forma gera as propriedades, mas não gera `SelectProductExpression`, `From` nem extensões.

### 7.3 Tipos gerados automaticamente

O filtro automático inclui:

- `bool`, `char`, `string` e tipos numéricos primitivos;
- `decimal` e `DateTime`;
- enums e structs, inclusive value objects definidos pela aplicação;
- versões nullable suportadas;
- arrays de tipos simples, enums ou structs;
- tipos genéricos que implementam `IEnumerable<T>` quando `T` é simples, enum ou struct.

Classes complexas não são adicionadas automaticamente como propriedades comuns. Para elas, declare a propriedade no DTO, use `AutoDetails`, configure flattening ou escreva o DTO aninhado explicitamente.

As propriedades de origem precisam ser públicas, de instância e legíveis. Propriedades estáticas ou sem getter público não entram na geração automática.

## 8. Flattening

Há dois usos relacionados, mas diferentes.

### 8.1 Casamento por convenção de uma propriedade declarada

O nome da propriedade do DTO representa a concatenação exata dos segmentos do caminho:

```csharp
public sealed class Order
{
    public Customer Customer { get; set; } = new();
}

[AutoSelect<Order>]
public partial class OrderDetails
{
    public string CustomerAddressCountryRegionName { get; set; } = string.Empty;
}
```

Trecho gerado:

```csharp
CustomerAddressCountryRegionName = source.Customer.Address.Country.Region.Name
```

Esse casamento pode atravessar vários níveis e não exige atributo.

### 8.2 Geração automática configurada com `Flattening`

`Flattening` recebe nomes de propriedades complexas da raiz. Para cada propriedade indicada, o generator cria propriedades combinando o nome da raiz com seus membros terminais suportados:

```csharp
[AutoSelect<Customer>(Flattening = [nameof(Customer.Address)])]
public partial class CustomerDetails { }
```

Se `Address` tiver `City` e `Zip`, serão geradas `AddressCity` e `AddressZip`. A propriedade complexa `Address` não será gerada automaticamente.

Também é válido:

```csharp
[AutoSelect<Customer>,
 AutoProperties(Flattening = [nameof(Customer.Address)])]
public partial class CustomerDetails { }

[AutoProperties<Customer>(Flattening = [nameof(Customer.Address)])]
public partial class CustomerSnapshot { }
```

Os nomes são case-sensitive. Se o mesmo nome concatenado puder representar dois caminhos, o generator emite o warning `RCSS010`. Renomeie a propriedade ou use `MapFrom` para eliminar a ambiguidade.

## 9. `MapFrom`

Use `MapFrom` quando o nome do DTO não coincide com a origem ou quando quiser declarar o caminho sem depender da convenção.

Membro direto:

```csharp
[AutoSelect<Product>]
public partial class ProductDetails
{
    [MapFrom(nameof(Product.Name))]
    public string DisplayName { get; set; } = string.Empty;
}
```

Caminho aninhado:

```csharp
[AutoSelect<Supplier>]
public partial class SupplierDetails
{
    [MapFrom("Warehouse.Location")]
    public string? WarehouseLocation { get; set; }
}
```

Regras:

- para membro direto, prefira `nameof(Product.Name)`;
- caminhos aninhados são strings separadas por ponto;
- cada segmento deve ser uma propriedade pública legível;
- os nomes e segmentos são case-sensitive;
- caminho aninhado inexistente ou ilegível produz `RCSS017` na propriedade do DTO;
- nome direto inválido não encontra correspondência e produz `RCSS001`;
- a atribuição continua dentro da expressão, preservando a possibilidade de tradução pelo EF Core.

O C# não permite `nameof` produzir o caminho completo com pontos. Para reduzir strings espalhadas, centralize caminhos recorrentes em constantes da aplicação.

## 10. Objetos, coleções e arrays

### 10.1 Objeto aninhado declarado manualmente

```csharp
[AutoSelect<Post>]
public partial class PostDetails
{
    public string Title { get; set; } = string.Empty;
    public AuthorDetails Author { get; set; } = new();
}

public class AuthorDetails
{
    public string Name { get; set; } = string.Empty;
}
```

O generator cria `new AuthorDetails { Name = source.Author.Name }` dentro da expressão.

### 10.2 Coleção de objetos

```csharp
[AutoSelect<Post>]
public partial class PostDetails
{
    public IReadOnlyList<CommentDetails> Comments { get; set; } = [];
}

public class CommentDetails
{
    public string Content { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
}
```

O generator projeta cada item com `Select` e materializa conforme o tipo declarado no destino:

| Tipo do destino | Materializacao |
| --- | --- |
| `IEnumerable<T>` | nenhuma |
| `List<T>`, `IList<T>`, `ICollection<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>` | `.ToList()` |
| `T[]` | `.ToArray()` |
| `HashSet<T>`, `ISet<T>` | `.ToHashSet()` |

Um destino de colecao que o generator nao sabe materializar (um `Dictionary<K,V>`, por exemplo) e reportado como
propriedade nao-assinavel, em vez de gerar codigo invalido.

Quando os elementos nao sao objetos a mapear, mas apenas a converter — uma colecao de enums equivalentes, por
exemplo —, o `Select` emite so a conversao: `Status = source.Status.Select(b => (StatusDto)b).ToList()`.

> Antes do SmartSelector 0.5.1, um destino declarado com o tipo concreto `List<T>` gerava codigo invalido
> (mapeava `Capacity`/`this[]` do proprio `List<T>`); o contorno era declarar a colecao como `IReadOnlyList<T>`.
> Corrigido — qualquer um dos tipos da tabela acima funciona.

### 10.3 Arrays

Arrays simples podem ser criados por `AutoProperties`:

```csharp
public int[] Scores { get; set; } = [];
```

Um array de DTOs declarado manualmente também é suportado:

```csharp
public ItemDetails[] Items { get; set; } = [];
```

Quando a origem é uma coleção ou array de objetos compatíveis, a expressão usa `Select(...).ToArray()`. Uma conversão implícita entre arrays compatíveis continua sendo atribuição direta.

## 11. `AutoDetails`

`AutoDetails` gera ou completa o tipo declarado em uma propriedade aninhada:

```csharp
[AutoSelect<Customer>, AutoProperties]
public partial class CustomerDetails
{
    [AutoDetails]
    public AddressDto Address { get; set; } = new();
}
```

Se `Customer.Address` for do tipo `Address`, o generator cria `AddressDto` com as propriedades simples de `Address`. O nome declarado na propriedade é a fonte da verdade; ele não precisa seguir a convenção `AddressDetails`.

Também é possível completar um tipo existente:

```csharp
public partial class AddressDto
{
    public string City { get; set; } = string.Empty;
}
```

As propriedades já declaradas não são duplicadas; as demais propriedades suportadas são geradas na outra parte da classe.

Regras importantes:

- `AutoDetails` só pode ser usado na propriedade de um DTO processado por `AutoProperties` ou por `AutoSelect` com geração automática de propriedades;
- se o tipo já existe no projeto, ele deve ser `partial`;
- a acessibilidade do tipo não pode ser menor que a acessibilidade da propriedade;
- duas propriedades não podem solicitar a geração do mesmo tipo;
- `Exclude` e `Flattening` também podem ser configurados em `AutoDetails`.

## 12. Política de nulabilidade

Com nullable reference types habilitado, a direção origem → destino determina a expressão e os diagnósticos:

| Origem | Destino | Comportamento |
|---|---|---|
| escalar nullable | nullable | `null` flui normalmente |
| navegação nullable | objeto DTO nullable | condicional propaga `null` |
| caminho por pai nullable | escalar nullable | condicional propaga `null` |
| coleção nullable | coleção nullable | condicional propaga `null` |
| coleção nullable | coleção não nullable | coleção vazia + `RCSS016` (Info) |
| array nullable | array não nullable | `Array.Empty<T>()` + `RCSS016` (Info) |
| escalar ou navegação nullable | destino não nullable | comportamento anterior + `RCSS015` (Warning) |

Exemplos de emissão:

```csharp
Warehouse = source.Warehouse == null
    ? null
    : new WarehouseDetails { Location = source.Warehouse.Location };

AddressCity = source.Address == null
    ? null
    : source.Address.City;

Items = source.Items == null
    ? new List<ItemDetails>()
    : source.Items.Select(...).ToList();

ItemsArray = source.Items == null
    ? Array.Empty<ItemDetails>()
    : source.Items.Select(...).ToArray();
```

`RCSS015` não inventa um fallback para escalares ou objetos. Ele preserva a atribuição anterior e avisa que o destino não consegue representar `null`; conforme o caminho, `From` pode receber `null` ou lançar ao navegar pelo grafo. Corrija tornando o destino nullable, alterando o modelo ou excluindo a propriedade.

Em código nullable-oblivious (`#nullable disable`), o generator mantém o comportamento tradicional e não adiciona essa política baseada em anotações.

Os condicionais fazem parte da árvore de expressão e os cenários de navegação nullable e `MapFrom` aninhado são exercitados pelo projeto Demo com EF Core/SQLite.

## 13. DTOs aninhados, herança e tipos genéricos

Um DTO pode ser declarado dentro de outro tipo:

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

O DTO e todos os tipos que o contêm devem ser `partial`. A cadeia e as acessibilidades são preservadas no código gerado.

Restrições do destino:

- o DTO deve estar dentro de um namespace;
- DTO genérico não é suportado;
- DTO dentro de tipo genérico não é suportado;
- um tipo contêiner não `partial` produz erro.

O tipo de origem de `AutoSelect<TFrom>` pode usar sintaxe qualificada, alias `global::`, tipo aninhado ou tipo genérico construído, como `Envelope<string>`.

Propriedades herdadas públicas participam da projeção. O tipo parcial gerado não repete a lista de bases; ele completa a declaração existente e inicializa propriedades herdadas quando houver correspondência.

## 14. Uso com LINQ e Entity Framework Core

Para `IQueryable<T>`, prefira a extensão ou a expressão:

```csharp
var page = await db.Orders
    .Where(order => order.Active)
    .OrderBy(order => order.Id)
    .SelectOrderDetails()
    .Take(50)
    .ToListAsync(ct);

var query = db.Orders.Select(OrderDetails.SelectOrderExpression);
```

Isso mantém a projeção na árvore LINQ enviada ao provider. Aplique filtros e ordenações sobre a entidade antes da projeção quando eles dependem de membros que não existem no DTO.

Para objetos já materializados:

```csharp
OrderDetails one = OrderDetails.From(order);
OrderDetails two = order.ToOrderDetails();
IEnumerable<OrderDetails> many = orders.SelectOrderDetails();
```

`From` compila e executa a mesma expressão em memória. Ele não é uma operação assíncrona e não consulta o banco.

Mesmo usando apenas construções normalmente traduzíveis — acesso a membros, condicionais, `Select`, `ToList` e `ToArray` — a capacidade final depende do provider e da versão do EF Core. Valide consultas relevantes com o provider real da aplicação.

### 14.1 Uso em conjunto com o SmartSearch

As duas bibliotecas foram feitas para trabalhar juntas, mas **nenhuma depende da outra**: o SmartSelector é usável sem
o SmartSearch (é o que esta seção mostra), e o SmartSearch é usável sem o SmartSelector.

Quando as duas estão presentes, a integração é automática e **não exige registro nenhum**. O membro gerado

```csharp
public static Expression<Func<Order, OrderDetails>> SelectOrderExpression { get; }
```

satisfaz o contrato por convenção que o SmartSearch usa para descobrir projeções: ao resolver `Select<TDto>()`, ele
procura no DTO uma propriedade `public static` do tipo `Expression<Func<TEntity, TDto>>`. Encontrando, usa a expressão
gerada — que é verificada em tempo de compilação — em vez de construir uma projeção por reflexão em runtime.

```csharp
[AutoSelect<Order>, AutoProperties]
public partial class OrderDetails
{
    public List<OrderItemDetails> Items { get; set; } = [];
}

// o SmartSearch encontra SelectOrderExpression sozinho:
var result = await criteria.FilterBy(filter).Select<OrderDetails>().ToListAsync(ct);
```

O mesmo contrato aceita uma expressão escrita à mão — o que importa é o tipo da propriedade estática, não o nome nem
quem a produziu. É também por ele que os endpoints gerados pelo SmartCommands (`MapFind`, `MapSearch`) projetam DTOs sem
que o SmartCommands conheça o SmartSelector. Detalhes da ordem de resolução estão na documentação do SmartSearch, seção
"Projeção para DTO".

## 15. Referência dos atributos

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AutoSelectAttribute<TFrom> : AutoPropertiesAttributeBase { }

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AutoPropertiesAttribute : AutoPropertiesAttributeBase { }

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AutoPropertiesAttribute<TFrom> : AutoPropertiesAttributeBase { }

[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class AutoDetailsAttribute : AutoPropertiesAttributeBase { }

[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class MapFromAttribute : Attribute
{
    public MapFromAttribute(string propertyName)
    {
        PropertyName = propertyName;
    }

    public string PropertyName { get; set; }
}
```

Configuração comum:

```csharp
public abstract class AutoPropertiesAttributeBase : Attribute
{
    public string[]? Exclude { get; set; }
    public string[]? Flattening { get; set; }
}
```

Os quatro atributos concretos são `sealed`. `AutoPropertiesAttributeBase` compartilha o contrato público, mas um atributo customizado derivado apenas dessa base não é reconhecido automaticamente pelo generator.

`Exclude` e `Flattening` são case-sensitive. Quando mais de um atributo de configuração válido participa, suas listas são combinadas.

## 16. Diagnósticos

| ID | Severidade | Significado e correção usual |
|---|---|---|
| `RCSS000` | Error | uso inválido de `AutoSelect`, inclusive DTO ou contêiner não `partial`; corrija a declaração indicada |
| `RCSS001` | Error | não foi encontrada propriedade correspondente; alinhe o nome, use `MapFrom` ou exclua a propriedade |
| `RCSS002` | Error | os tipos correspondentes não podem ser atribuídos ou projetados; alinhe os tipos ou escreva projeção manual |
| `RCSS003` | Error | `AutoProperties<TFrom>` foi usado com `AutoSelect`; troque pela forma não genérica |
| `RCSS004` | Error | `AutoProperties` e `AutoProperties<TFrom>` foram usados juntos; mantenha apenas a forma correta |
| `RCSS005` | Error | argumento de tipo de `AutoProperties<TFrom>` inválido; use classe ou struct válida |
| `RCSS006` | Error | classe com `AutoProperties<TFrom>` não é `partial` |
| `RCSS007` | Error | `AutoProperties` não genérico está sem `AutoSelect<TFrom>` |
| `RCSS008` | Error | DTO ou tipo contêiner genérico não suportado |
| `RCSS010` | Warning | flattening ambíguo; use `MapFrom` ou renomeie |
| `RCSS011` | Error | DTO de `AutoSelect` está no namespace global; mova-o para um namespace |
| `RCSS012` | Error | tipo existente solicitado por `AutoDetails` não é `partial` |
| `RCSS013` | Error | mais de uma propriedade tenta gerar o mesmo tipo com `AutoDetails` |
| `RCSS014` | Error | tipo existente de `AutoDetails` tem acessibilidade insuficiente |
| `RCSS015` | Warning | origem nullable flui para destino não nullable; torne o destino nullable ou exclua a propriedade |
| `RCSS016` | Info | coleção nullable será convertida em coleção vazia no destino não nullable |
| `RCSS017` | Error | caminho aninhado de `MapFrom` inexistente ou não legível |

Não há `RCSS009` na versão 0.5.0.

## 17. Erros comuns

### 17.1 Esperar propriedades de `AutoSelect` sem ativar `AutoProperties`

Incorreto:

```csharp
[AutoSelect<User>]
public partial class UserDetails { } // não há propriedades para mapear
```

Correto:

```csharp
[AutoSelect<User>, AutoProperties]
public partial class UserDetails { }
```

Ou declare manualmente somente as propriedades desejadas.

### 17.2 Usar a forma genérica junto com `AutoSelect`

```csharp
[AutoSelect<User>, AutoProperties<User>] // RCSS003
public partial class UserDetails { }
```

Use `[AutoProperties]` porque `TFrom` já vem de `AutoSelect<User>`.

### 17.3 Confundir flattening configurado com `MapFrom`

`Flattening = [nameof(Customer.Address)]` gera automaticamente `AddressCity`, `AddressZip` etc. `[MapFrom("Address.City")]` escolhe o caminho de uma propriedade específica já declarada.

### 17.4 Tornar o destino não nullable e ignorar `RCSS015`

O warning não garante fallback. Em uma navegação anulável, o delegate de `From` pode falhar ao percorrer o caminho. Modele o DTO conforme a realidade da origem.

### 17.5 Editar arquivos `.g.cs`

Arquivos gerados são derivados dos atributos e modelos. Altere o DTO ou a entidade; qualquer edição direta será perdida na próxima geração.

### 17.6 Usar SmartSelector para projeção calculada

Não há API para soma, formatação, chamada de serviço ou resolver customizado. Escreva uma `Expression<Func<TEntity,TDto>>` manual para esse cenário.

### 17.7 Supor que toda expressão traduz em qualquer provider

Os padrões do generator são orientados a LINQ/EF Core, mas tradução é responsabilidade do provider. Cubra consultas críticas com testes de integração.

## 18. Boas práticas

- Mantenha DTO e entidades com nullable reference types habilitado.
- Use `[AutoSelect<TEntity>, AutoProperties]` para o caso simples e declare manualmente exceções estruturais.
- Use `Exclude` para dados sensíveis e campos internos; prefira `nameof`.
- Use `MapFrom` para renome e desambiguação; prefira `nameof` em membros diretos.
- Use `AutoDetails` quando o formato aninhado deve espelhar propriedades simples da navegação.
- Prefira DTOs aninhados explícitos a nomes de flattening excessivamente longos.
- Trate `RCSS015` como decisão de contrato, não como ruído de build.
- Projete no banco com `IQueryable`; use `From` apenas para objetos já materializados.
- Inspecione os arquivos gerados ao diagnosticar correspondência ou nulabilidade, mas não os edite.
- Teste com o provider EF Core real quando a projeção contiver objetos, coleções, arrays ou caminhos nullable.

Checklist antes de entregar:

- [ ] Runtime e generator estão referenciados na mesma versão.
- [ ] DTO e todos os tipos contêineres são `partial` e não genéricos.
- [ ] DTO de `AutoSelect` está em um namespace.
- [ ] A forma de `AutoProperties` corresponde ao cenário.
- [ ] Dados sensíveis foram excluídos.
- [ ] `MapFrom` aninhado usa caminho público legível e case-sensitive.
- [ ] Nulabilidade do DTO representa a nulabilidade da origem.
- [ ] Warnings `RCSS010` e `RCSS015` foram resolvidos conscientemente.
- [ ] A consulta `IQueryable` usa a expressão ou a extensão gerada.
- [ ] Projeções relevantes foram validadas com o provider real.
