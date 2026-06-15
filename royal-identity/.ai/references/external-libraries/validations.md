# Documentação da API SmartValidations (RuleSet, IValidable)

Esta documentação apresenta os conceitos, funcionalidades e exemplos práticos para usar a biblioteca SmartValidations em projetos .NET.
Também serve de referência para ferramentas de IA (ex.: GitHub Copilot) gerarem código correto com base na API da biblioteca.

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
10. Instruções para Ferramentas de IA (GitHub Copilot)

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

- `IValidable`
  - Contrato simples com `HasProblems(out Problems?)` para permitir validação de objetos e value objects (structs).

- Predicados internos (`BuildInPredicates`)
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
  - Regras de e-mail e URL baseadas em `EmailAddressAttribute` e `UrlAttribute`.

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
        .GreaterThanOrEqual(price, 0)
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
            .GreaterThanOrEqual(Amount, 0)
            .Length(Currency, 3, 3)
            .HasProblems(out problems);
    }
}

var prices = new[] { new Money(-1, ""), new Money(10, "USD") };
var set = Rules.Set().Validate((IEnumerable<Money>)prices);
if (set.HasProblems(out var problems))
{
    // problems conterá caminhos com índices: [0], [1]
}
```

## 5. Exemplos de uso aninhados (objetos e coleções)

Validando objetos opcionais com `NotNullNested` e objetos sempre presentes com `Nested`:

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
                .NotEmpty(addr.Street)
                .NotEmpty(addr.City)
                .NotEmpty(addr.ZipCode))
            .Nested(PastAddresses, addr => Rules.Set<Address>()
                .NotEmpty(addr.Street)
                .NotEmpty(addr.City)
                .NotEmpty(addr.ZipCode))
            .HasProblems(out problems);
    }
}
```

Usando `WithPropertyPrefix` para normalizar nomes ao compor validadores reutilizáveis:

```csharp
Problems? ValidateAddress(Address a)
    => Rules.Set<Address>()
        .WithPropertyPrefix(nameof(a))
        .NotEmpty(a.Street)
        .NotEmpty(a.City)
        .NotEmpty(a.ZipCode);

var set2 = Rules.Set()
    .Nested(order.ShippingAddress, a => ValidateAddress(a));
```

No exemplo acima, se `Street` estiver vazio, o `Property` do `Problem` será `ShippingAddress.Street`.

Caso não usasse `WithPropertyPrefix`, o `Property` seria `ShippingAddress.a.Street`, incluindo o nome do parâmetro como nome da propriedade.

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
    s => s.Min(totalAmount, 100));     // alternativo
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
  - Configure seus display names (DataAnnotations ou provedor customizado) para mensagens amigáveis.

## 7. Referência da API

Tipos principais:
- `RuleSet` (fluent API de validação)
- `IValidable` (contrato para validação)
- `BuildInPredicates` (predicados usados pelas regras)

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

Igualdade/Desigualdade
- `Equal` e `NotEqual` para `string` (com `StringComparison`) e tipos `IEquatable<T>` (inclui versões `Nullable<T>`)
- Duais: `BothEqual` e `BothNotEqual` para `string` e tipos `IEquatable<T>`

Strings e padrões
- `Matches` / `NotMatches` com `string pattern` ou `Regex`
- `StartsWith` / `EndsWith` / `Contains` / `NotContain`
- `OnlyLetters` / `OnlyDigits` / `OnlyLettersOrDigits` / `NoWhiteSpace`

Numéricos e faixas
- `Min` / `Max` / `MinMax` (e variantes `NullOrMin`, `NullOrMax`, `NullOrMinMax`)
- Tamanho de string: `MinLength` / `MaxLength` / `Length` (e `NullOrMinLength`, `NullOrMaxLength`, `NullOrLength`)

Comparações relativas
- `LessThan` / `LessThanOrEqual` / `GreaterThan` / `GreaterThanOrEqual` (para tipos `IComparable<T>` e suas variantes `Nullable`)

Datas e horários (via `BuildInPredicates`)
- `InPast` / `InFuture` / `Today` para `DateTime`, `DateTimeOffset`, `DateOnly`
- `After` / `Before` / `Between` para os mesmos tipos

E-mail e URL
- `Email(string?)` e `Url(string?)`

Customização
- `Must<T>(value, predicate, messageFormatter[, ruleName])`
- `Must<TValue, TParam>(value, param, predicate, messageFormatter[, ruleName])`
- `BothMust<T1, T2>(...)` e `BothMust<T1, T2, TParam>(...)`

Validação aninhada
- Objetos: `Nested(value, Problems? validator)` / `Nested(value, Func<T, ValidateFunc>)` / `Nested(value) where T: IValidable`
- Coleções: `Nested(IEnumerable<T>, ...)` com indexação automática
- Garantindo não-nulo: `NotNullNested(...)` (mesmas variações)

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
- Em coleções, valide cada item com `Nested`/`Validate` para obter caminhos com índice.

## 9. Resumo

SmartValidations fornece uma maneira fluente, fortemente tipada e performática de validar modelos .NET. Ao invés de lançar exceções, as falhas são representadas por `Problems` ricos em contexto, integráveis com `ProblemDetails` em APIs. Suas APIs cobrem desde regras básicas de vazio/igualdade até validações aninhadas, condicionais e customizadas, com metadados para rastreabilidade e mensagens prontas para localização.

## 10. Instruções para Ferramentas de IA (GitHub Copilot)

Objetivo: gerar validações seguindo o contrato da API (`RuleSet`, `IValidable`) e produzir `Problems` com metadados corretos.

Princípios obrigatórios
- Sempre valide com `Rules.Set<T>()` (quando dentro do tipo) ou `Rules.Set()` (fora), e finalize com `HasProblems(out Problems?)` ou conversão implícita para `Problems?`.
- Use os métodos de regra do `RuleSet` (ex.: `NotEmpty`, `Min`, `Equal`, `Matches`, `Nested`, `Validate`) em vez de criar `Problem` manualmente.
- Preserve o `Property` via `CallerArgumentExpression`: passe o próprio argumento observado à regra, não strings de nome manual.
- Em objetos opcionais, primeiro `NotNullNested(...)`; em objetos obrigatórios, use `Nested(...)` diretamente.
- Em coleções, use `Nested(IEnumerable<T>, ...)` ou `Validate(IEnumerable<struct>)` para indexar automaticamente (`Items[i].Prop`).

Padrões de implementação
- DTO/entrada
  - Estrutura:
    - Método `bool HasProblems(out Problems? problems)`.
    - `return Rules.Set<ThisType>() ... .HasProblems(out problems);`
  - Campos opcionais: `NullOr*` (ex.: `NullOrLength`) ou `NotNullNested` antes de regras internas.
  - Campos obrigatórios: `NotEmpty`/`Min`/`Max`/`Length` conforme tipo.

- Objetos aninhados
  - `Nested(child, c => Rules.Set<Child>()...)` para validar filhos obrigatórios.
  - `NotNullNested(child, c => Rules.Set<Child>()...)` para filhos opcionais.
  - Para reuso, normalize com `WithPropertyPrefix(prefix)` no validador reutilizável.

- Value objects (struct)
  - Implementar `IValidable` com `HasProblems(out Problems?)` interno ao struct.
  - Em agregados: `Rules.Set().Validate(collectionOfStructs)` para coletar e indexar problemas.

- Condicionais
  - `When(cond, builder)` para aplicar regras sob condição.
  - `Unless(cond, builder)` para aplicar regras quando a condição não vale.
  - Alternativas: `Unless(conditionRules, alternativeRules)` (se ambos falham, agregue ambos).

- Regras customizadas
  - `Must(value, predicate, messageFormatter[, ruleName])` e `BothMust(...)` quando não houver regra pronta.
  - O `messageFormatter` deve usar o display name (`prop`) e indicar claramente o requisito violado.

- URLs/e-mails e datas
  - Use `Email(value)` e `Url(value)` para formatos; prefira `IsHttpsUrl`/`IsAbsoluteUrl` (via predicados) dentro de `Must` quando necessário.
  - Datas: utilize comparações (`LessThan/GreaterThanOrEqual`) e predicados de tempo (`InPast/InFuture`) conforme o caso.

Formato esperado de saída
- Funções devem retornar `Problems?` ou `bool HasProblems(out Problems?)`.
- Não lançar exceções para fluxo de validação.
- Problemas conterão `rule/current/expected/pattern/properties/values` automaticamente via `RuleSet`.

Exemplos de prompts corretos
- "Gere `HasProblems(out Problems?)` para `RegisterUser` com `NotEmpty(Name)`, `Min(Age,18)`, e `NotNullNested(Address, addr => ... )`."
- "Valide `Order.Items` com `Nested(items, item => Rules.Set<OrderItem>().NotEmpty(item.ProductId).GreaterThan(item.Quantity,0))`."
- "Crie regra customizada com `Must(Password, predicate, formatter, ruleName:"password.policy")` e alternativa com `Unless(s => s.NotEmpty(PromoCode), s => s.Min(Total,100))`."
- "Implemente `IValidable` em `struct Price` e valide uma lista com `Rules.Set().Validate(prices)` retornando `Problems?`."

Antipadrões (evitar)
- Construir `Problem` manualmente para validação de entrada (use `RuleSet`).
- Passar nomes de propriedade como string (quebra refactors e `CallerArgumentExpression`).
- Usar exceções para fluxo esperado de validação.
- Usar Expression para a propriedade (x => x.Prop) (não funciona).