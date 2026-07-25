# Code Style Rules

## GENERAL

- Target framework is `net10.0` (via `Directory.Build.props`).
- Nullable and implicit usings are enabled globally.
- Use 4 spaces for C# indentation.
- Prefer file-scoped namespace style unless the surrounding file uses otherwise.
- Primary constructors are preferred for very simple cases.
- Keep changes scoped to the task and follow nearby patterns.
- For the `UserAccounts` module family, follow "External RoyalCode Libraries" above for library-specific patterns.

## LINQ

- Prefer method-chain LINQ with lambdas (`Where`, `Select`, `SelectMany`, `OrderBy`, etc.) over query expression syntax (`from ... in ... where ... select ...`).
- Treat query expression syntax as a code smell in this repository unless it clearly improves readability for a complex multi-join/grouping query.
- When refactoring nearby code, convert simple query expressions to method-chain LINQ instead of preserving or copying the query syntax.

Example:

```csharp
var apiScopes = resources.Scopes
	.Where(scope => scope.ShowInDiscoveryDocument)
	.Select(scope => scope.Name);
```
