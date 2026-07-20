# Documentação da API SmartValidations (RuleSet, IValidable)

Esta documentação apresenta os conceitos, funcionalidades e exemplos práticos para usar a biblioteca SmartValidations em projetos .NET.
Para instruções objetivas de uso por ferramentas de IA, consulte também `.docs/validations.ai-rules.md`.

Projetos alvo: .NET 8, .NET 9 e .NET 10.

Sumário
1. Introdução
2. Funcionalidades Principais
3. Exemplos de uso base
4. Exemplos de uso com structs (IValidable)
5. Exemplos de uso aninhados (objetos e coleções)
6. Exemplos de uso avançados (condicionais, regras personalizadas, internacionalização)
7. Referência da API
8. Boas práticas
9. Resumo
10. Documentação para IA

## 1. Introdução

SmartValidations é uma biblioteca de validação fluente e orientada a modelos para .NET. Ela produz `Problems` estruturados (em vez de exceções) ao aplicar regras em objetos, valores e coleções. É construída sobre SmartProblems, permitindo respostas padronizadas e serializáveis em APIs e UIs.

Benefícios principais:
- Validação fluente em uma única passagem com `RuleSet`.
- Sem exceções no fluxo esperado: retorna `Problems` padronizados.
- Composição forte e tipada: uso de `INumber<T>`, `CallerArgumentExpression` e genéricos.
- Validação aninhada de primeira classe: objetos e coleções, com caminho de propriedade encadeado automaticamente.
- Preparada para APIs: mensagens localizáveis e metadados ricos para diagnóstico.

## 2. Funcionalidades Principais

- `RuleSet`
  - DSL fluente para aplicar regras. Cada falha gera um `Problem` via `Problems.InvalidParameter(...)` com metadados:
    - `rule` (nome da regra), `current` (valor atual), `expected` (valor(es) esperado(s)), `pattern` (em regras de regex), `properties` e `values` (em regras com 2 operandos).
  - Integra com display names e prefixos de propriedades, removendo prefixos configurados ao encadear problemas.
  - Conversão implícita para `Problems?` e método `HasProblems(out Problems?)`.
  - É um `readonly ref struct`: use localmente em métodos síncronos de validação, especialmente em `HasProblems(out Problems?)`.

- `IValidable`
  - Contrato simples com `HasProblems(out Problems?)` para permitir validação de objetos e value objects (structs).

- Predicados públicos de apoio (`BuildInPredicates`)
  - Conjunto abrangente de verificações: vazios, igualdade, comparações, faixas, tamanhos, padrões de string, e utilitários de data/tempo.
  - Utilizados por `RuleSet` para implementar as regras fluentes.

- Validações aninhadas
  - `Nested(...)` e `NotNullNested(...)` para objetos, coleções e tipos que implementam `IValidable`.
  - Encadeia `Property` com `ChainProperty` e índices (ex.: `Items[2].Quantity`).

- Regras condicionais e customizadas
  - `When(...)` e `Unless(...)` (várias sobrecargas) para aplicar grupos de regras sob condição ou de forma alternativa.
  - `Must(...)` e `BothMust(...)` para regras personalizadas com formatadores de mensagem.

- Utilidades
  - `WithPropertyPrefix(...)` para normalizar caminhos removendo prefixos conhecidos.
  - Regras de e-mail, URL, URL HTTPS, URL absoluta e URL relativa.

## 3. Exemplos de uso base

Validação simples em um DTO usando `RuleSet` e retornando `Problems` via `HasProblems`:

```csharp
using RoyalCode.SmartValidations;
using RoyalCode.SmartProblems;

public sealed class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }

    public bool HasProblems(out Problems? problems)
    {
        return Rules.Set<CreateUserRequest>()
            .NotEmpty(Name)
            .Min(Age, 18)
            .HasProblems(out problems);
    }
}
```

Usando como serviço/handler, retornando `Problems?` diretamente:

```csharp
public static Problems? ValidateProduct(string sku, string? name, decimal price)
{
    return Rules.Set()
        .NotEmpty(sku)
        .NullOrLength(name, 3, 120)
        .Min(price, 0m)
        ; // conversão implícita para Problems?
}
```

Checando e serializando em API:

```csharp
var set = Rules.Set().NotEmpty(request.Email).Email(request.Email);
if (set.HasProblems(out var problems))
{
    // converter para ProblemDetails usando SmartProblems e retornar 400/422 conforme categoria
}
```

## 4. Exemplos de uso com structs (IValidable)

Implemente `IValidable` em value objects e use `RuleSet.Validate` para coleções:

```csharp
public readonly struct Money : IValidable
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public bool HasProblems(out Problems? problems)
    {
        return Rules.Set<Money>()
            .Min(Amount, 0m)
            .Length(Currency, 3, 3)
            .HasProblems(out problems);
    }
}

var prices = new[] { new Money(-1, ""), new Money(10, "USD") };
var set = Rules.Set().Validate(prices);
if (set.HasProblems(out var problems))
{
    // apenas o item inválido gera problemas; o Property de cada problema
    // será o nome do argumento com índice: "prices[0]"
}
```

Observação: `Validate` substitui o `Property` dos problemas internos pelo nome do argumento (com índice em coleções). No exemplo, tanto a falha de `Amount` quanto a de `Currency` do primeiro item terão `Property == "prices[0]"`; o campo específico permanece no texto da mensagem. Esse comportamento é pensado para value objects, onde o nome externo é mais significativo que o campo interno — para preservar o caminho completo (ex.: `Items[0].Quantity`), use `Nested` com classes.

## 5. Exemplos de uso aninhados (objetos e coleções)

Validando objetos obrigatórios com `NotNullNested` e objetos opcionais com `Nested`:

```csharp
public sealed class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
}

public sealed class CheckoutRequest : IValidable
{
    public string CustomerId { get; set; } = string.Empty;
    public Address? Shipping { get; set; }
    public List<Address>? PastAddresses { get; set; }

    public bool HasProblems(out Problems? problems)
    {
        return Rules.Set<CheckoutRequest>()
            .NotEmpty(CustomerId)
            .NotNullNested(Shipping, addr => Rules.Set<Address>()
                .WithPropertyPrefix(nameof(addr))
                .NotEmpty(addr.Street)
                .NotEmpty(addr.City)
                .NotEmpty(addr.ZipCode))
            .Nested(PastAddresses, addr => Rules.Set<Address>()
                .WithPropertyPrefix(nameof(addr))
                .NotEmpty(addr.Street)
                .NotEmpty(addr.City)
                .NotEmpty(addr.ZipCode))
            .HasProblems(out problems);
    }
}
```

Usando `WithPropertyPrefix` para normalizar nomes ao compor validadores reutilizáveis:

```csharp
Problems? ValidateAddress(Address address)
    => Rules.Set<Address>()
        .WithPropertyPrefix(nameof(address))
        .NotEmpty(address.Street)
        .NotEmpty(address.City)
        .NotEmpty(address.ZipCode);

var order = new Order { ShippingAddress = new Address() };

var set2 = Rules.Set<Order>()
    .WithPropertyPrefix(nameof(order))
    .Nested(order.ShippingAddress, address => ValidateAddress(address));
```

No exemplo acima, se `Street` estiver vazio, o `Property` do `Problem` será `ShippingAddress.Street`.

Sem o `WithPropertyPrefix` externo, o caminho manteria o nome da variável (`order.ShippingAddress.Street`). Sem o `WithPropertyPrefix` interno, o caminho incluiria o nome do parâmetro do validador (`ShippingAddress.address.Street`).

## 6. Exemplos de uso avançados

- Regras condicionais (`When`/`Unless`):

```csharp
var set = Rules.Set()
    .When(isGuest, s => s.NotEmpty(email).Email(email))
    .Unless(hasAddressOnFile, s => s
        .NotEmpty(addr.Street)
        .NotEmpty(addr.City)
        .NotEmpty(addr.ZipCode));

// Grupos alternativos: adiciona problemas de ambos se ambos falharem
set = set.Unless(
    s => s.NotEmpty(promoCode),        // condição
    s => s.Min(totalAmount, 100m));    // alternativo
```

- URLs especializadas, sinais numéricos e datas:

```csharp
var set = Rules.Set()
    .HttpsUrl(callbackUrl)
    .RelativeUrl(returnPath)
    .Positive(quantity)
    .Min(price, 0m)
    .InFuture(expiresAt)
    .After(periodEnd, periodStart);
```

- Regras personalizadas (`Must`/`BothMust`) com metadados de regra:

```csharp
var strong = Rules.Set()
    .Must(password,
        p => p is { Length: >= 8 } && p.Any(char.IsDigit) && p.Any(char.IsUpper),
        (prop, _) => $"{prop} must contain at least 8 chars, an uppercase and a digit.",
        ruleName: "password.policy")
    .BothMust(start, end,
        (s, e) => s < e,
        (p1, p2, _, _) => $"{p1} must be before {p2}.",
        ruleName: "period.order");
```

- Internacionalização
  - As mensagens são formatadas por templates (ex.: `R.MinMessageTemplate`) e nomes de exibição via `DisplayNames`.
  - Os templates são recursos localizáveis (`R.resx`); a biblioteca inclui inglês (padrão) e `pt-BR`, selecionados pela `CultureInfo.CurrentUICulture` da thread.
  - Configure seus display names (DataAnnotations ou provedor customizado) para mensagens amigáveis.

## 7. Referência da API

Tipos principais:
- `RuleSet` (fluent API de validação)
- `IValidable` (contrato para validação)
- `BuildInPredicates` (predicados públicos de apoio usados pelas regras)

Escopo de uso do `RuleSet`
- `RuleSet` é `readonly ref struct`.
- Use em escopo local e síncrono; não armazene em campos, não capture em lambdas assíncronas e não tente atravessar `await`.
- O uso principal esperado é dentro de `HasProblems(out Problems?)` ou funções síncronas que retornam `Problems?`.
- Use como uma única cadeia fluente: após a primeira falha, as cópias de um `RuleSet` compartilham a mesma coleção de `Problems` — não ramifique um `RuleSet` intermediário em cadeias independentes.

Criação e inspeção
- `Rules.Set()` / `Rules.Set<T>()` / `RuleSet.For<T>()`
- `HasProblems(out Problems? problems)`
- Conversão implícita `RuleSet -> Problems?`
- `WithPropertyPrefix(string)`

Nulos e vazios
- `NotNull(value)`
- `NotEmpty` para: `string`, `INumber<T>`, `T? where T: struct, INumber<T>`, arrays, `ICollection<T>`, `IReadOnlyCollection<T>`, `IEnumerable<T>`, `DateTime(Offset)`, `DateOnly`, `Guid`
- `NullOrNotEmpty` para: `string`, `INumber<T>`, `T? where T: struct, INumber<T>`
- Duais: `BothNullOrNotEmpty(string?, string?)`
- Semântica de "vazio": zero para números, `MinValue` para datas, `Guid.Empty` para GUIDs, nula/em branco para strings, sem itens para coleções.

Igualdade/Desigualdade
- `Equal` e `NotEqual` para `string` (com `StringComparison`) e tipos `IEquatable<T>` (inclui versões `Nullable<T>`)
- Duais: `BothEqual` e `BothNotEqual` para `string` e tipos `IEquatable<T>`

Strings e padrões
- `Matches` / `NotMatches` com `string pattern` ou `Regex`
- `StartsWith` / `EndsWith` / `Contains` / `NotContain`
- `OnlyLetters` / `OnlyDigits` / `OnlyLettersOrDigits` / `NoWhiteSpace`
- As sobrecargas com `string pattern` aplicam `BuildInPredicates.RegexMatchTimeout` (1s) como proteção contra backtracking catastrófico.

Numéricos e faixas
- `Min` / `Max` / `MinMax` (e variantes `NullOrMin`, `NullOrMax`, `NullOrMinMax`)
- `Positive` / `Negative` / `Zero` / `NotZero`
- Tamanho de string: `MinLength` / `MaxLength` / `Length` (e `NullOrMinLength`, `NullOrMaxLength`, `NullOrLength`)

Comparações relativas
- `LessThan` / `LessThanOrEqual` / `GreaterThan` / `GreaterThanOrEqual` (para tipos `IComparable<T>` e suas variantes `Nullable`)
- Nas variantes `Nullable`, `null` é tratado como o menor valor possível (mesma convenção de `Comparer<T>.Default`).

Datas e horários
- `InPast` / `InFuture` / `Today` para `DateTime`, `DateTimeOffset`, `DateOnly`
- `After` / `Before` / `Between` para os mesmos tipos
- As regras relativas (`InPast`, `InFuture`, `Today`) usam `BuildInPredicates.Clock` (`TimeProvider`, padrão `TimeProvider.System`); substitua em testes para resultados determinísticos.

E-mail e URL
- `Email(string?)` e `Url(string?)`
- `HttpsUrl(string?)`, `AbsoluteUrl(string?)` e `RelativeUrl(string?)`

Customização
- `Must<T>(value, predicate, messageFormatter[, ruleName])`
- `Must<TValue, TParam>(value, param, predicate, messageFormatter[, ruleName])`
- `BothMust<T1, T2>(...)` e `BothMust<T1, T2, TParam>(...)`

Validação aninhada
- Objetos opcionais: `Nested(value, Func<T, Problems?>)` / `Nested(value, Func<T, ValidateFunc>)` / `Nested(value) where T: IValidable`
- Coleções opcionais: `Nested(IEnumerable<T>, ...)` com indexação automática; itens `null` são ignorados
- Objetos e coleções obrigatórios: `NotNullNested(...)` com as mesmas variações; gera problema quando o valor/coleção é `null` e, em coleções, quando um item é `null` (com propriedade indexada, ex.: `Items[2]`).

Structs com `IValidable`
- `Validate<T>(value) where T: struct, IValidable`
- `Validate<T>(IEnumerable<T>) where T: struct, IValidable`

Condicionais
- `When(bool, RuleSetBuilder)`
- `Unless(...)` com múltiplas sobrecargas: combinando builders, fábricas e resultados previamente avaliados

Metadados em `Problem` (SmartProblems)
- `rule`, `current`, `expected`, `pattern`, `properties`, `values` preenchidos conforme a regra.

## 8. Boas práticas

- Modele validação por request/DTO em uma única função que retorna `Problems?`.
- Use `IValidable`/`ValidateFunc` para compor validações de agregados, objetos aninhados e value objects.
- Prefira regras não-explosivas: use `Result`/`Problems` em vez de exceções em controle de fluxo.
- Defina `StringComparison` explicitamente quando relevante.
- Use `WithPropertyPrefix` para normalizar caminhos ao reutilizar validadores.
- Padronize mensagens com templates localizáveis e display names consistentes.
- Garanta cobertura com regras `NullOr*` quando campos forem opcionais.
- Para objetos aninhados opcionais, use `Nested`; para obrigatórios, use `NotNullNested`.
- Para limites fixos, prefira `Min`, `Max`, `MinMax`, `Positive`, `Negative`, `Zero` e `NotZero`; deixe `LessThan`/`GreaterThan` para comparação entre valores.
- Use `HttpsUrl`, `AbsoluteUrl`, `RelativeUrl` e regras de data diretamente no `RuleSet` antes de recorrer a `Must`.
- Não armazene `RuleSet` fora do escopo local de validação; ele é um `ref struct`.
- Trate o `RuleSet` como uma cadeia fluente única; não ramifique um set intermediário em cadeias independentes (as cópias compartilham os `Problems`).
- Em coleções, valide cada item com `Nested`/`Validate` para obter caminhos com índice.

## 9. Resumo

SmartValidations fornece uma maneira fluente, fortemente tipada e performática de validar modelos .NET. Ao invés de lançar exceções, as falhas são representadas por `Problems` ricos em contexto, integráveis com `ProblemDetails` em APIs. Suas APIs cobrem desde regras básicas de vazio/igualdade até validações aninhadas, condicionais e customizadas, com metadados para rastreabilidade e mensagens prontas para localização.

## 10. Documentação para IA

Use `.docs/validations.ai-rules.md` como documento de instruções para ferramentas de IA em outros projetos e repositórios.
