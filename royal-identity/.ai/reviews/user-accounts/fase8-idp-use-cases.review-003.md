# Review 003 - Fase 8: casos de uso mínimos para integração com o IdP

**Data:** 2026-06-20
**Fase:** 8 - Casos de uso mínimos para integração com o IdP
**Arquivos avaliados:**
- `RoyalIdentity.UserAccounts/Features/Accounts/Commons/UserAccountReader.cs`
- `RoyalIdentity.UserAccounts/Features/Accounts/Commons/UserAccountsServiceCollectionExtensions.cs`
- `RoyalIdentity.UserAccounts/Features/Accounts/UseCases/CreateUserAccount.cs`
- `RoyalIdentity.UserAccounts/Features/Accounts/UseCases/AuthenticateLocalCredential.cs`
- `RoyalIdentity.UserAccounts/Features/Accounts/UseCases/ChangeUserAccountPassword.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/UseCases/SetUserAccountScopeProperty.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Commons/UserAccountClaimsReader.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Commons/UserAccountClaimProjector.cs`
- `Tests.UserAccounts/UserAccountUseCasesTests.cs`

---

## Avaliação geral

A fase está bem encaminhada. A decisão de usar SmartCommands para escritas e read services para leituras com grafo carregado por `Include` é compatível com as limitações documentadas do SmartSearch e mantém o módulo puro sem HTTP. A normalização centralizada, `ISubjectIdGenerator`, `TimeProvider` injetado e hasher como seam do host são boas escolhas.

Os achados abaixo não são falta de teste: são pontos de comportamento/política que precisam ser corrigidos ou explicitados como decisão.

---

## Achados

### P1 - Email fictício ignora a política `AllowDuplicateEmail = false`

**Severidade:** média

No branch de email real, `CreateUserAccount` verifica duplicidade por realm quando `AllowDuplicateEmail = false`:

```csharp
if (!Options.AllowDuplicateEmail &&
	await db.Set<UserAccountEmail>().AnyAsync(
		e => e.RealmId == RealmId && e.NormalizedAddress == normalizedAddress, ct))
{
	return Problems.InvalidState(...);
}
```

Mas no branch de email fictício a criação vai direto para `account.AddEmail(...)`, sem checar duplicidade:

```csharp
var address = Options.FictitiousEmailPattern.Replace("{subjectId}", subjectId, StringComparison.Ordinal);
var normalizedAddress = normalizer.NormalizeEmail(address);
var email = new UserAccountEmail(...);
return account.AddEmail(email, now);
```

Isso é seguro com o default atual (`{subjectId}@fictitious.local`), mas deixa uma brecha quando o realm configura `FictitiousEmailPattern` customizado e não único, por exemplo `user@fictitious.local`. Nesse caso, duas contas podem receber o mesmo email fictício mesmo com `AllowDuplicateEmail = false`, quebrando a garantia da Fase 8.

**Ação sugerida:** aplicar a mesma checagem de duplicidade ao email fictício ou validar que `FictitiousEmailPattern` contém `{subjectId}` quando `AllowDuplicateEmail = false`.

### P2 - `ChangeUserAccountPassword` ignora `AllowChangePassword`

**Severidade:** média

`ChangeUserAccountPassword` recebe `UserAccountsRealmOptions`, mas usa apenas `PasswordOptions` para validar complexidade:

```csharp
var policyResult = passwordPolicy.Validate(NewPassword, Options.PasswordOptions, account.Username);
```

Ele não verifica `Options.AllowChangePassword`. Se esse comando representa fluxo de usuário, a política do realm pode ser contornada. Se a intenção é que este comando seja apenas seed/admin/teste, o nome e contrato ficam ambíguos, porque hoje ele parece o caso de uso natural para troca local de senha.

**Ação sugerida:** se for fluxo de usuário, retornar `Problems.NotAllowed(...)` quando `AllowChangePassword = false`. Se for comando administrativo/seed, renomear ou documentar explicitamente essa semântica e criar outro caso de uso para a troca feita pelo usuário.

---

## Observações positivas

- `UserAccountReader` mantém filtro por `RealmId` em todas as consultas principais.
- O uso de `IUserAccountNormalizer` evita normalização duplicada espalhada por command/read service.
- `AuthenticateLocalCredential` preserva motivo interno (`NotFound`, `PasswordNotSet`, `InvalidCredentials`, `LockedOut`, etc.) sem impor a semântica externa do IdP nesta fase.
- `UserAccountClaimsReader` e `UserAccountClaimProjector` respeitam a interseção entre scopes solicitados e claim types permitidos, deixando a criação de `Claim` da BCL para `.Integration`.

---

## Verificação executada

- `dotnet test Tests.UserAccounts` - verde.
- `dotnet test Tests.Architecture` - verde.
- `dotnet build RoyalIdentity.sln` - verde, com warnings já conhecidos.
