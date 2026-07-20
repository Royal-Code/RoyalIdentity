# SmartValidations AI Rules

Use SmartValidations to produce structured `Problems` with `RuleSet` and `IValidable`.

## Goal

- Generate synchronous validation code for .NET models, requests, DTOs, value objects and aggregates.
- Return `Problems?` or implement `bool HasProblems(out Problems? problems)`.
- Let `RuleSet` create `Problems.InvalidParameter(...)` and fill metadata.
- Use SmartProblems conversion/response helpers from the target project after validation.

## Core Rules

- Use `Rules.Set<T>()` when validating from inside type `T`.
- Use `Rules.Set()` for standalone helper methods.
- Use `RuleSet.For<T>()` only when matching an existing local style that prefers it.
- End object validation with `.HasProblems(out problems)`.
- Return the `RuleSet` directly from helpers returning `Problems?`; implicit conversion is supported.
- Keep `RuleSet` local and synchronous. It is a `readonly ref struct`.
- Do not store `RuleSet` in fields, properties, arrays, closures or long-lived state.
- Do not capture `RuleSet` across `await`, async lambdas, iterators or deferred execution.
- Do not throw exceptions for expected validation failures.
- Do not create `Problem` manually for common validation rules.
- Do not use expression selectors such as `x => x.Property`; `RuleSet` uses `CallerArgumentExpression`.
- Do not pass property names as strings when the validated expression can be passed directly.

## Decision Matrix

- Required string: `NotEmpty(value)`.
- Optional string that may be null but not empty when provided: `NullOrNotEmpty(value)`.
- Required collection: `NotEmpty(values)` and then `Nested(values, ...)` or `Validate(values)` when items must be validated.
- Optional collection: `Nested(values, ...)`; add `NotEmpty(values)` only when empty collection is invalid.
- Required nested object: `NotNullNested(value, validator)` or `NotNullNested(value)` when it implements `IValidable`.
- Optional nested object: `Nested(value, validator)` or `Nested(value)` when it implements `IValidable`.
- Required struct value object: `Validate(value)`.
- Struct value object collection: `Validate(values)`.
- Inclusive lower bound: `Min(value, min)`.
- Inclusive upper bound: `Max(value, max)`.
- Inclusive range: `MinMax(value, min, max)`.
- Positive number: `Positive(value)`.
- Non-zero number: `NotZero(value)`.
- Compare two values: `LessThan`, `LessThanOrEqual`, `GreaterThan`, `GreaterThanOrEqual`.
- Value must equal a fixed constant: `Equal(value, expected)`; must differ from it: `NotEqual(value, expected)`.
- Two properties must match (e.g. password confirmation): `BothEqual(value1, value2)`; must differ: `BothNotEqual(value1, value2)`.
- Two fields must be filled together or both null: `BothNullOrNotEmpty(value1, value2)`.
- String format with no built-in rule: `Matches(value, pattern, patternDescription)`.
- Required email: `NotEmpty(email).Email(email)`.
- Optional email: `When(email is not null, s => s.Email(email))`; `Email(null)` reports a problem, so guard optional fields with `When`.
- Absolute URL: `Url(value)` or `AbsoluteUrl(value)`.
- HTTPS URL: `HttpsUrl(value)`.
- Relative URL/path: `RelativeUrl(value)`.
- Future date: `InFuture(value)`.
- Past date: `InPast(value)`.
- Date must be today: `Today(value)`.
- Date after/before fixed values: `After`, `Before`, `Between`.
- Domain-specific condition not covered by built-ins: `Must` or `BothMust` with a stable `ruleName`.

## Standard DTO Pattern

```csharp
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

public sealed class RegisterUserRequest : IValidable
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }

    public bool HasProblems(out Problems? problems)
    {
        return Rules.Set<RegisterUserRequest>()
            .NotEmpty(Name)
            .NotEmpty(Email)
            .Email(Email)
            .Min(Age, 18)
            .HasProblems(out problems);
    }
}
```

Use this shape for request/command/DTO classes when the project accepts validation on the model itself.

## Standalone Validator Pattern

```csharp
public static Problems? ValidateProduct(string sku, string? name, decimal price)
{
    return Rules.Set()
        .NotEmpty(sku)
        .NullOrLength(name, 3, 120)
        .Min(price, 0m);
}
```

Use this shape for service-level or handler-level validation when the model type should not implement `IValidable`.

## Required And Optional Values

```csharp
return Rules.Set<CreateProductRequest>()
    .NotEmpty(Sku)
    .NotEmpty(Name)
    .NullOrLength(Description, 0, 500)
    .NullOrNotEmpty(ExternalReference)
    .HasProblems(out problems);
```

- Use `NullOr*` only when `null` is valid.
- Use `NotEmpty` when `null`, empty, zero, default date, empty GUID or empty collection is invalid.
- Use `NotNull` when default value is acceptable but `null` is not.

## Numeric And Range Rules

```csharp
return Rules.Set<OrderItem>()
    .Positive(Quantity)
    .Min(UnitPrice, 0m)
    .Max(DiscountPercent, 100m)
    .MinMax(Score, 0, 100)
    .NotZero(ExternalId)
    .HasProblems(out problems);
```

- Prefer `Min`, `Max`, `MinMax`, `Positive`, `Negative`, `Zero` and `NotZero` for fixed constraints.
- Use comparison rules only when both operands are meaningful values from the model.
- Nullable comparison overloads treat `null` as the smallest possible value (same convention as `Comparer<T>.Default`); add `NotNull` before them only when `null` itself must be invalid.

```csharp
return Rules.Set<Period>()
    .LessThan(Start, End)
    .GreaterThanOrEqual(End, Start)
    .HasProblems(out problems);
```

## String Rules

```csharp
return Rules.Set<Account>()
    .Length(Currency, 3, 3)
    .MinLength(Password, 8)
    .MaxLength(DisplayName, 80)
    .OnlyLettersOrDigits(UserName)
    .NoWhiteSpace(UserName)
    .StartsWith(Code, "USR-", StringComparison.Ordinal)
    .HasProblems(out problems);
```

- Use `Length(value, n, n)` for exact length.
- Pass `StringComparison` explicitly for `StartsWith`, `EndsWith`, `Contains`, `NotContain`, `Equal` and `NotEqual` when case or culture matters.
- Use `Matches`/`NotMatches` for regex only when no built-in string rule exists.

## Email, URL And Date Rules

```csharp
return Rules.Set<CallbackRequest>()
    .NotEmpty(Email)
    .Email(Email)
    .HttpsUrl(WebhookUrl)
    .RelativeUrl(ReturnPath)
    .InFuture(ExpiresAt)
    .HasProblems(out problems);
```

```csharp
return Rules.Set<EventPeriod>()
    .After(EndAt, StartAt)
    .Between(StartAt, windowStart, windowEnd)
    .HasProblems(out problems);
```

- Use `Email` after `NotEmpty` for required email fields.
- For optional email or URL fields, wrap the format rule in `When(value is not null, s => s.Email(value))`; format rules report a problem for null values.
- Use `Url` for generic absolute URL accepted by `UrlAttribute`.
- Use `AbsoluteUrl` when an absolute URL is semantically required.
- Use `HttpsUrl` when HTTPS is required.
- Use `RelativeUrl` for paths such as `/orders/1`, `orders/1`, `../orders/1` or query-only relative targets.
- Use date rules directly in `RuleSet`; do not wrap `BuildInPredicates` in `Must` for these cases.
- `InPast`, `InFuture` and `Today` read the current time from `BuildInPredicates.Clock`, a `TimeProvider` that defaults to `TimeProvider.System`.
- In tests, assign a fixed `TimeProvider` to `BuildInPredicates.Clock` for deterministic date validations, and restore the original value afterwards.
- Do not use `DateTime.Now` workarounds to make date rules testable; replace `BuildInPredicates.Clock` instead.

## Nested Objects

For required nested object:

```csharp
public bool HasProblems(out Problems? problems)
{
    return Rules.Set<CheckoutRequest>()
        .NotEmpty(CustomerId)
        .NotNullNested(ShippingAddress, address => Rules.Set<Address>()
            .WithPropertyPrefix(nameof(address))
            .NotEmpty(address.Street)
            .NotEmpty(address.City)
            .NotEmpty(address.ZipCode))
        .HasProblems(out problems);
}
```

For optional nested object:

```csharp
public bool HasProblems(out Problems? problems)
{
    return Rules.Set<CustomerProfile>()
        .Nested(BillingAddress, address => Rules.Set<Address>()
            .WithPropertyPrefix(nameof(address))
            .NotEmpty(address.Street)
            .NotEmpty(address.City)
            .NotEmpty(address.ZipCode))
        .HasProblems(out problems);
}
```

Rules:

- `Nested(null, ...)` does not add a problem.
- `NotNullNested(null, ...)` adds a problem for the nested property.
- Always use `WithPropertyPrefix(nameof(parameter))` inside reusable nested validators when validating parameter expressions such as `address.Street`.
- Use an outer `.WithPropertyPrefix(nameof(rootVariable))` when the root expression includes a local variable name that should not appear in the final path.

Reusable validator:

```csharp
static Problems? ValidateAddress(Address address)
    => Rules.Set<Address>()
        .WithPropertyPrefix(nameof(address))
        .NotEmpty(address.Street)
        .NotEmpty(address.City)
        .NotEmpty(address.ZipCode);

var set = Rules.Set<Order>()
    .WithPropertyPrefix(nameof(order))
    .NotNullNested(order.ShippingAddress, address => ValidateAddress(address));
```

Expected path for invalid street: `ShippingAddress.Street`.

## Nested Collections

For required collection with required valid items:

```csharp
public bool HasProblems(out Problems? problems)
{
    return Rules.Set<Order>()
        .NotEmpty(Items)
        .When(Items is not null, s => s.NotNullNested(Items, item =>
            Rules.Set<OrderItem>()
                .WithPropertyPrefix(nameof(item))
                .NotEmpty(item.ProductId)
                .Positive(item.Quantity)
                .Min(item.Price, 0m)))
        .HasProblems(out problems);
}
```

For optional collection:

```csharp
return Rules.Set<Order>()
    .Nested(OptionalItems, item => Rules.Set<OrderItem>()
        .WithPropertyPrefix(nameof(item))
        .NotEmpty(item.ProductId)
        .Positive(item.Quantity))
    .HasProblems(out problems);
```

Rules:

- Collection item paths include indexes, for example `Items[0].ProductId`.
- Use `NotEmpty(values)` when an empty collection is invalid.
- Use `NotNullNested(values, ...)` when a `null` collection is invalid; it also reports `null` items with indexed paths such as `Items[2]`.
- Use `Nested(values, ...)` when a `null` collection is valid; `null` items are skipped.
- For required non-empty collections with non-null items, use `NotEmpty(values)` plus `When(values is not null, s => s.NotNullNested(values, ...))` to avoid duplicate null problems.

## IValidable Classes

When a nested class implements `IValidable`, call the overload without a validator:

```csharp
public sealed class Customer : IValidable
{
    public Address? Address { get; set; }

    public bool HasProblems(out Problems? problems)
    {
        return Rules.Set<Customer>()
            .NotNullNested(Address)
            .HasProblems(out problems);
    }
}
```

- Use `Nested(Address)` if `Address` is optional.
- Use `NotNullNested(Address)` if `Address` is required.

## Struct Value Objects

Implement `IValidable` on structs and validate with `Validate`.

```csharp
public readonly struct Money : IValidable
{
    public decimal Amount { get; }
    public string Currency { get; }

    public bool HasProblems(out Problems? problems)
    {
        return Rules.Set<Money>()
            .Min(Amount, 0m)
            .Length(Currency, 3, 3)
            .HasProblems(out problems);
    }
}

return Rules.Set<Order>()
    .Validate(Total)
    .Validate(Prices)
    .HasProblems(out problems);
```

- `Validate(value)` validates one struct.
- `Validate(values)` validates a collection and indexes paths.

## Conditional Rules

Use `When` for conditional requirements:

```csharp
return Rules.Set<CheckoutRequest>()
    .When(IsGuest, s => s
        .NotEmpty(Email)
        .Email(Email))
    .HasProblems(out problems);
```

Use `Unless` for rules that apply when a condition is false:

```csharp
return Rules.Set<CheckoutRequest>()
    .Unless(HasAddressOnFile, s => s
        .NotNullNested(ShippingAddress, address => ValidateAddress(address)))
    .HasProblems(out problems);
```

Use alternative groups when one of two validation groups must pass:

```csharp
var set = Rules.Set<Order>()
    .Unless(
        s => s.NotEmpty(PromoCode),
        s => s.Min(TotalAmount, 100m));
```

## Custom Rules

Use `Must` only when there is no built-in rule:

```csharp
return Rules.Set<RegisterUserRequest>()
    .Must(Password,
        p => p is { Length: >= 8 } && p.Any(char.IsDigit) && p.Any(char.IsUpper),
        (property, _) => $"{property} must contain at least 8 chars, an uppercase letter and a digit.",
        ruleName: "password.policy")
    .HasProblems(out problems);
```

Use `BothMust` for custom validation involving two values:

```csharp
return Rules.Set<Period>()
    .BothMust(Start, End,
        (start, end) => start < end,
        (startProperty, endProperty, _, _) => $"{startProperty} must be before {endProperty}.",
        ruleName: "period.order")
    .HasProblems(out problems);
```

Rules:

- Always provide a stable `ruleName`.
- Use the display-name parameter from `messageFormatter`.
- Prefer built-in methods over `Must`.
- Do not use `Must` for URL, email, numeric sign, length, range or date rules already covered by `RuleSet`.

## Metadata

- Rely on `RuleSet` to set metadata.
- `Rules.RuleProperty` stores `rule`.
- `Rules.CurrentValueProperty` stores `current`.
- `Rules.ExpectedValueProperty` stores `expected`.
- `Rules.PatternProperty` stores `pattern`.
- Two-operand rules attach `properties` and `values`.
- Do not manually set these metadata fields for built-in rules.

## Output And API Integration

Use this shape in handlers:

```csharp
if (request.HasProblems(out var problems))
{
    return problems;
}
```

or:

```csharp
var problems = ValidateProduct(sku, name, price);
if (problems is not null)
{
    return problems;
}
```

- Convert `Problems` to the response format used by the project.
- In ASP.NET APIs using SmartProblems, use the project’s existing SmartProblems integration for `ProblemDetails`.
- Do not serialize validation failures by hand when SmartProblems helpers are available.

## Anti-Patterns

- Do not write `Problems.InvalidParameter(...)` manually for common input validation.
- Do not pass `"Name"` or `"Address.Street"` when `.NotEmpty(Name)` or `.NotEmpty(address.Street)` can capture the expression.
- Do not use `GreaterThan(value, 0)` for positive numbers; use `Positive(value)`.
- Do not use `GreaterThanOrEqual(value, 0)` for lower bounds; use `Min(value, 0)`.
- Do not use `BuildInPredicates.IsHttpsUrl` inside `Must`; use `HttpsUrl(value)`.
- Do not use `BuildInPredicates.InFuture` inside `Must`; use `InFuture(value)`.
- Do not call async APIs inside `RuleSet` builders.
- Do not keep `RuleSet` in a variable that outlives the validation method.
- Do not validate required nested objects with `Nested`; use `NotNullNested`.
- Do not validate optional nested objects with `NotNullNested`; use `Nested`.
