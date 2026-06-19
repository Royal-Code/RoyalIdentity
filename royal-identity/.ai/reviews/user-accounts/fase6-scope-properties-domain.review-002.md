# Review 002 — Fase 6: Propriedades dinâmicas por escopo

**Data:** 2026-06-19
**Fase:** 6 — Propriedades dinâmicas por escopo
**Arquivos avaliados:**
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Domain/PropertyScope.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Domain/PropertyScopeVersion.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Domain/PropertyDefinition.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Domain/PropertyDefinitionVersion.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Domain/PropertyDefinitionSettings.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Domain/PropertyValidationRules.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Domain/PropertyRangeRule.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Domain/PropertyValueType.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Domain/PropertyScopeVersionStatus.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Domain/PropertyScopeEvents.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Domain/PropertyCustomValidationRef.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Domain/IUserAccountPropertyValidator.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Domain/UserAccountPropertyValidationContext.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Domain/UserAccountPropertyValue.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Domain/UserAccountPropertyValueService.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Commons/AccountClaimValue.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Commons/UserAccountClaimProjector.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Commons/ValidateClaimProjectionConfiguration.cs`
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Commons/SeedDefaultPropertyScopes.cs`
- `Tests.UserAccounts/PropertyScopeDomainTests.cs`

---

## Avaliação geral

A implementação é **sólida** no essencial: o modelo de domínio reflete o design do plano, as invariantes de versionamento estão corretas, a validação declarativa é robusta e a separação `AccountClaimValue` / `Claim` da BCL é uma escolha muito boa. Os problemas abaixo são reais mas circunscritos — nenhum invalida a fase.

---

## 1. Qualidade da implementação

### Pontos fortes

- Padrão EF (construtor protegido + `#nullable disable` + navegações virtuais protegidas) consistente com a Fase 5.
- `PropertyDefinition` como identidade estável + `PropertyDefinitionVersion` como configuração mutável é DDD correto.
- `UserAccountPropertyValueService` como serviço de domínio (fora do agregado) está certo — o agregado recebe valores já validados via `ReplacePropertyValues(...)`.
- `IUserAccountPropertyValidator` / `PropertyCustomValidationRef` / `UserAccountPropertyValidationContext` formam um ponto de extensão bem modelado.
- `ValidateDeclarativeRules` — tratamento correto de regex em tipo não-texto como `InvalidState` (erro de configuração, não de input).

### Problemas

#### P1 — `ActiveVersionId` sempre `null` em criação in-memory

```csharp
// PropertyScope.ApproveVersion:
ActiveVersionId = version.Id == default ? null : version.Id;
```

Quando `SeedDefaultPropertyScopes` cria e aprova um scope em memória, `ActiveVersionId` fica `null` porque o `Id` ainda não foi atribuído pelo banco. A propriedade `ActiveVersion` usa `Status`, então funciona corretamente em memória. Mas o FK persistido na Fase 7 precisará de reconhecimento pós-`SaveChanges` (ex.: `UpdateAfterSave` / reaplicação após insert).

**Ação:** documentar como dívida da Fase 7.

#### P2 — `PropertyDefinitionVersion.ClaimType` computado via navegação nullable

```csharp
public string ClaimType => PropertyDefinition?.ClaimType ?? string.Empty;
```

Se a navegação não estiver carregada (projeção parcial, lazy loading desabilitado), retorna silenciosamente `string.Empty`. Isso pode causar bugs sutis no `UserAccountClaimProjector` quando a query não incluir o join.

**Ação:** na Fase 7, mapear `ClaimType` como coluna persistida em `PropertyDefinitionVersion` (como ocorre em `UserAccountPropertyValue`), não como computed via navegação.

#### P3 — `PropertyScopeVersion.AttachTo(PropertyScope)` é código morto

O construtor de `PropertyScopeVersion` já atribui `PropertyScope`, `PropertyScopeId` e `RealmId`. O método `AttachTo(PropertyScope)` existe mas nunca é chamado em nenhum ponto do código.

**Ação:** remover.

#### P4 — `AllowedValues` re-parseia todos os valores permitidos a cada validação

```csharp
foreach (var allowedValue in rules.AllowedValues)
{
    if (!TryParse(definitionVersion.ValueType, allowedValue, out _, out var canonicalAllowedValue)) { ... }
    allowedValues.Add(canonicalAllowedValue);
}
```

Baixo impacto hoje, mas os valores permitidos canônicos poderiam ser pré-computados no `PropertyDefinitionVersion` ou memoizados no serviço se a validação for chamada em lote.

**Ação:** deixar para otimização futura; apenas registrar.

---

## 2. Valor do design

### Decisões excelentes

- **`AccountClaimValue`** como DTO interno sem `System.Security.Claims` mantém o módulo puro isolado da BCL. A Fase 9 (`.Integration`) é a única que produz `Claim`.
- **Versionamento sem impacto na version ativa** — draft aprovado arquiva o anterior; a projeção usa sempre `ActiveVersion`. Semântica clara e correta.
- **`IsRequired` prospectivo** — validado na escrita, não bloqueia leitura/projeção para contas existentes.
- **Denormalização de `ClaimType`/`ValueType` em `UserAccountPropertyValue`** — correto para leituras; fonte de verdade permanece no schema.

### Problemas de design

#### D1 — Violação de boundary do aggregate root (alta prioridade)

Vários métodos de mutação de estado estão `public` em entidades filhas, bypassando `PropertyScope`:

| Entidade | Método público problemático |
|---|---|
| `PropertyScopeVersion` | `SubmitForApproval()`, `Reject()` |
| `PropertyDefinitionVersion` | `Update(settings)`, `Activate()`, `Deactivate()` |

O aggregate root (`PropertyScope`) controla aprovação e arquivo de versões via `ApproveVersion(...)`, mas as transições draft→pending, pending→rejected e definition activate/deactivate estão expostas diretamente nos filhos. Casos de uso externos podem alterar estado sem passar pela raiz.

**Ação:** tornar `SubmitForApproval`, `Reject`, `Update`, `Activate` e `Deactivate` das entidades filhas `internal`; expor via métodos correspondentes em `PropertyScope`. Exemplo:

```csharp
// Em PropertyScope:
public Result SubmitVersionForApproval(PropertyScopeVersion version) { ... }
public Result RejectVersion(PropertyScopeVersion version) { ... }
public Result UpdateDefinition(PropertyScopeVersion version, string claimType, PropertyDefinitionSettings settings) { ... }
public Result ActivateDefinition(PropertyScopeVersion version, string claimType) { ... }
public Result DeactivateDefinition(PropertyScopeVersion version, string claimType) { ... }
```

#### D2 — `PropertyDefinitionVersion.Update()` permite trocar `ValueType` sem restrição

Se uma version draft tem definition `birthdate: Date` e `Update()` troca para `Text`, após aprovação os `UserAccountPropertyValue` existentes continuam com `ValueType = Date` denormalizado enquanto a nova versão declara `Text`. A validação na próxima escrita usaria `Text`, mas os valores históricos têm tipo divergente.

**Ação:** na Fase 7, definir regra: troca de `ValueType` só é válida se não há valores persistidos para a definition, ou deve exigir migração explícita dos valores. Registrar na ADR-015 ou como dívida de Fase 7.

---

## 3. Terminologia

Consistente e alinhada ao plano. Sem desvios graves.

- **`PropertyDefinitionSettings`** — correto, mas poderia ser mais expressivo como `PropertyDefinitionVersionSettings` para evidenciar que é o DTO de configuração de uma *version*, não da definition estável.
- **`AccountClaimValue.ScopeName`** — campo útil na projeção interna; verificar se a Fase 9 precisará dele ao criar `Claim` da BCL (por exemplo, para separar claims por scope antes de emitir).

---

## 4. Fluxo

O fluxo de escrita e leitura está correto e bem orquestrado.

**Escrita:**
`PropertyScope.AddDefinition` → `ApproveVersion` → `UserAccountPropertyValueService.SetValuesAsync` → `account.ReplacePropertyValues`

**Leitura/projeção:**
`UserAccountClaimProjector.Project` → filtra scope ativo → filtra definition ativa → filtra `PropertyValues` por `ClaimType` → retorna `IReadOnlyList<AccountClaimValue>`

### Gap no fluxo — eventos de mudança de definition

`PropertyDefinitionChanged` é emitido apenas em `AddDefinition`. As operações `definitionVersion.Update(...)`, `definitionVersion.Activate()` e `definitionVersion.Deactivate()` são silenciosas.

O plano lista `PropertyDefinitionChanged` como evento esperado sem especificar todos os triggers. Se o propósito é sinalizar mudanças no schema para sincronização futura com o IdP (roadmap automação `PropertyScope` draft → active → criar/atualizar identity scope), as atualizações de version também deveriam emitir o evento.

**Ação:** emitir `PropertyDefinitionChanged` também em `Update`/`Activate`/`Deactivate` da definition version — ou registrar a omissão como decisão explícita.

---

## 5. Completude

| Tarefa do plano | Estado |
|---|---|
| `PropertyScope` com `Name`, `IsActive`, `ActiveVersionId` | ✅ |
| `PropertyScopeVersion` com status, timestamps e aprovação | ✅ |
| `PropertyDefinition` como identidade estável de `ClaimType` | ✅ |
| `PropertyDefinitionVersion` com todas as configurações | ✅ |
| `PropertyValidationRules` com todos os campos declarados | ✅ |
| `UserAccountPropertyValue` com colunas denormalizadas | ✅ |
| `ClaimType` único por realm entre definitions dinâmicas | ✅ (modelo; runtime enforcement na Fase 7) |
| Validação de colisão fixed ↔ dynamic | ✅ |
| `IsRequired` prospectivo | ✅ |
| Projeção combinando campos fixos + roles + values dinâmicos | ✅ |
| Interseção scope + claim type | ✅ |
| Seed de scopes `profile` e `email` | ✅ |
| Conta inativa não emite claims | ✅ |

### Lacunas menores

- **Seeds sem `PropertyDefinition`:** os seeds criam apenas containers de scope sem definitions. OK para esta fase, mas a Fase 8/9 precisará criar definitions para os claim types que a integração espera (ex.: campos fixos dinâmicos extras, se houver).
- **`PropertyValidationRules.AllowedValues` e `CustomValidators` como arrays:** serialização JSON para EF é responsabilidade da Fase 7, mas nenhum atributo ou comentário na classe sinaliza essa expectativa de mapeamento.

---

## Resumo das ações

| Prioridade | Ref | Ação |
|---|---|---|
| Alta | D1 | Tornar `SubmitForApproval`, `Reject`, `Update`, `Activate`, `Deactivate` das entidades filhas `internal`; expor via `PropertyScope` |
| Alta | P2 | Mapear `PropertyDefinitionVersion.ClaimType` como coluna persistida na Fase 7 (não computed via navegação) |
| Média | P1 | Registrar como dívida da Fase 7 a reconexão de `ActiveVersionId` pós-`SaveChanges` |
| Média | D2 | Definir regra para troca de `ValueType` em draft quando existem valores persistidos |
| Baixa | P3 | Remover `PropertyScopeVersion.AttachTo(PropertyScope)` (código morto) |
| Baixa | — | Emitir `PropertyDefinitionChanged` também em `Update`/`Activate`/`Deactivate` da definition version, ou documentar a omissão como decisão explícita |
