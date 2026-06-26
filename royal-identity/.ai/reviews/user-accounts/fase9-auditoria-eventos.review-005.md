# Review 005 - Fase 9: auditoria, eventos e outbox seletivo

**Data:** 2026-06-25  
**Fase:** 9 - Auditoria, eventos e outbox seletivo  
**Plano:** `.ai/plans/plan-users-security-lifecycle.md`  
**Escopo:** eventos de dominio do modulo `UserAccounts`, despacho pos-commit, auditoria por categorias,
policy por realm e ausencia de outbox.

---

## Avaliacao geral

A direcao da fase esta boa: o modulo puro continua sem depender do core, nao foi introduzido outbox, o dispatcher
pos-commit e simples, e `NoopSecurityAuditSink` mantem a persistencia duravel diferida para um plano futuro.

O principal problema esta na classificacao da auditoria: as categorias existem, mas alguns eventos nao carregam dados
suficientes para serem classificados corretamente, e outros eventos sensiveis ja existentes nao sao mapeados. Tambem
ha dois pontos de robustez: lifetime de DI da policy realm-aware e comportamento divergente entre `SaveChanges()` e
`SaveChangesAsync()`.

---

## Achados

### P1 - Auditoria nao consegue classificar resets/acoes administrativas de senha

**Severidade:** alta

**Arquivos:**
- `RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccount.cs`
- `RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccountEvents.cs`
- `RoyalIdentity.UserAccounts/Infrastructure/Audit/SecurityAuditObserver.cs`
- `RoyalIdentity.UserAccounts/Features/Accounts/UseCases/ResetPasswordWithToken.cs`

`UserAccount.SetPassword(...)` recebe `PasswordChangeReason`, mas emite `UserAccountPasswordChanged` sem carregar esse
motivo:

```csharp
AddEvent(new UserAccountPasswordChanged(RealmId, SubjectId));
```

Depois, `SecurityAuditObserver` classifica todo `UserAccountPasswordChanged` como `SecurityAuditCategories.Credential`.
Isso faz reset por recuperacao, reset administrativo e troca voluntaria de senha parecerem o mesmo evento de auditoria.

Consequencia:

- reset por recuperacao nao cai em `Recovery`;
- reset/set administrativo nao cai em `AdminSecurity`;
- filtros por categoria de auditoria ficam semanticamente errados;
- hosts que desabilitam `Credential` mas mantem `Recovery` podem deixar de auditar reset por recuperacao.

**Acao sugerida:** adicionar `PasswordChangeReason` e, se necessario, `ChangedBySubjectId` ao evento
`UserAccountPasswordChanged`, e mapear a categoria conforme o motivo:

- `Change` / troca voluntaria -> `Credential`;
- `Reset` por recovery -> `Recovery`;
- set/reset administrativo -> `AdminSecurity`.

Adicionar testes cobrindo pelo menos troca voluntaria, reset por token de recuperacao e reset administrativo quando o
caso existir.

---

### P2 - Eventos sensiveis existentes ficam fora da auditoria

**Severidade:** media

**Arquivos:**
- `RoyalIdentity.UserAccounts/Infrastructure/Audit/SecurityAuditObserver.cs`
- `RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccountEvents.cs`

O mapper de auditoria cobre senha, lockout, block/unblock e verificacao de email/telefone, mas ignora eventos ja
existentes que tambem sao sensiveis:

- `UserAccountRoleAdded`
- `UserAccountRoleRemoved`
- `UserAccountActivated`
- `UserAccountDeactivated`
- possivelmente `UserAccountPrimaryEmailChanged` e `UserAccountPrimaryPhoneChanged`, se forem tratados como mudancas
  sensiveis de perfil.

Pelo plano, a auditoria e por categoria, nao por catalogo fechado, mas esses eventos ja nasceram de casos de uso reais
e afetam seguranca/autorizacao.

**Acao sugerida:** mapear roles e ativacao/desativacao como `AdminSecurity`. Avaliar email/telefone primarios como
`Verification` ou `AdminSecurity`, dependendo da semantica de quem executa a mudanca. Adicionar testes de auditoria
para cada categoria habilitada/desabilitada.

---

### P2 - Policy realm-aware de auditoria e singleton, mas depende de resolver potencialmente scoped

**Severidade:** media

**Arquivos:**
- `RoyalIdentity.UserAccounts.Integration/UserAccountsIntegrationExtensions.cs`
- `RoyalIdentity.UserAccounts.Integration/RealmSecurityAuditPolicyProvider.cs`
- `Tests.UserAccounts/UserAccountsIntegrationTests.cs`

`AddUserAccountsForRoyalIdentity()` registra:

```csharp
services.Replace(ServiceDescriptor.Singleton<ISecurityAuditPolicyProvider, RealmSecurityAuditPolicyProvider>());
```

Mas `RealmSecurityAuditPolicyProvider` depende de `IUserAccountsRealmOptionsResolver`. O proprio teste
`AddUserAccountsForRoyalIdentity_AllowsScopedRealmOptionsResolver` valida que esse resolver pode ser scoped para a
binding de realm. Se um host usar resolver scoped, a policy singleton cria uma incompatibilidade de lifetime.

**Acao sugerida:** registrar `RealmSecurityAuditPolicyProvider` como scoped. Adicionar teste com
`IUserAccountsRealmOptionsResolver` scoped e `ValidateScopes = true` resolvendo `ISecurityAuditPolicyProvider` dentro de
um escopo.

---

### P2 - `SaveChanges()` sincrono nao despacha eventos

**Severidade:** media

**Arquivo:** `RoyalIdentity.UserAccounts/Infrastructure/Data/UserAccountsDbContext.cs`

`SaveChangesAsync(...)` coleta, limpa, commita e despacha eventos pos-commit. Ja `SaveChanges(bool)` apenas salva e
reconcilia `ActiveVersionId`:

```csharp
public override int SaveChanges(bool acceptAllChangesOnSuccess)
{
    var result = base.SaveChanges(acceptAllChangesOnSuccess);
    return ReconcileActiveVersionIds()
        ? result + base.SaveChanges(acceptAllChangesOnSuccess)
        : result;
}
```

Com isso, qualquer caller que use o caminho sincrono persiste mutacoes sem despacho de eventos e sem auditoria.

**Acao sugerida:** ou implementar coleta/despacho tambem no caminho sincrono, ou bloquear o caminho sincrono com uma
excecao/documentacao explicita se o modulo exige persistencia async para eventos. Se mantiver ambos, adicionar teste
para `SaveChanges()` despachando ou falhando de forma intencional.

---

## Gaps de teste relevantes

- Falta teste para classificar `UserAccountPasswordChanged` por motivo (`Change`, `Reset`, admin).
- Falta teste de auditoria para roles e ativacao/desativacao.
- Falta teste de DI com `IUserAccountsRealmOptionsResolver` scoped e `ISecurityAuditPolicyProvider`.
- Falta teste do caminho sincrono `SaveChanges()`.
- Falta teste provando que falha no sink/observer nao quebra a request, ou decisao explicita de que pode quebrar.

---

## Verificacao executada

- `dotnet test Tests.UserAccounts --nologo` - verde: 173/173.
- `dotnet test Tests.Architecture --nologo` - verde: 15/15.

Warnings observados:

- `NU1903` para `SQLitePCLRaw.lib.e_sqlite3` 2.1.11.
