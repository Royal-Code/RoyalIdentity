# Code Style Rules

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
