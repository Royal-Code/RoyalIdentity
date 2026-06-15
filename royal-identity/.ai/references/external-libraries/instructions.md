# Copilot Instructions

## General Guidelines
- Follow coding standards for consistency and maintainability.
- Use XML documentation to describe domain functionality for classes, methods, properties, services, and interfaces.
- For archtecture-specific guidelines, refer to the relevant documentation (file .docs/architecture.md).

## Code Style
- Value objects implement `IValidable` using `RuleSet` in `HasProblems`.
- Class member order should be: private fields, constructors, properties, methods.
- Entities require a protected parameterless constructor for deserialization. Not sealed. Use #nullable disable/restore for that constructor.
- Validations must not throw exceptions; business methods should return `Result`/`Result<T>` (SmartProblems).
- Use `SmartValidation` `RuleSet` for validation processes (file .docs/validation.md).
- Use `Problems`, `Problem`, `Result`, and `Result<T>` from SmartProbelms for error handling and reporting (file .docs/problems.md).
- Use SmartSelection for create Details and Summary DTOs (file .docs/selector.md).
- Use Filter-Specifier pattern for filtering and querying data (file .docs/search.md).
- Use IWorkContext for data access operations (file .docs/workcontext.md).
- Create entities with base class `Entity` from SmartDomain (file .docs/domain.md).
- Create commands using SmartCommands (file .docs/commands.md).
- Configure Minimal APIs following using SmartCommands (file .docs/commands.md).