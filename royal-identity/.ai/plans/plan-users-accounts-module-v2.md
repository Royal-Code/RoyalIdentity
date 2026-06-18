# Plan: Módulo de Contas de Usuário (`RoyalIdentity.UserAccounts`) - V2

## Status: EM EXECUÇÃO - Fases 1 (governança/docs) e 2 (emenda da borda no core) concluídas; próximo: Fase 3 (pré-flight)

## Progresso

`██░░░░░░░░` **20%** - 2 de 10 fases

| Fase | Estado |
|---|---|
| Fase 1 - Governança, ADRs e emendas de documentação | Concluida |
| Fase 2 - Emenda da borda de claims no core | Concluida |
| Fase 3 - Pré-flight RoyalCode + esqueleto da família de projetos | Não iniciada |
| Fase 4 - Options do módulo + split de `AccountOptions` | Não iniciada |
| Fase 5 - Domínio de contas (`UserAccount`) | Não iniciada |
| Fase 6 - Propriedades dinâmicas por escopo | Não iniciada |
| Fase 7 - Persistência própria EFCore + providers | Não iniciada |
| Fase 8 - Casos de uso mínimos para integração com o IdP | Não iniciada |
| Fase 9 - Integração com a borda do IdP | Não iniciada |
| Fase 10 - Contract tests, DI opt-in, seeds e regressão | Não iniciada |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluida, `%` e `X de 10`). Ex.: 4 fases ⇒ `████░░░░░░` **40%** - 4 de 10.

---

## Contexto

Este plano substitui o rascunho [plan-users-accounts-module.md](plan-users-accounts-module.md) e incorpora as decisões
das análises:

- [an-users-plan-01.md](../analisys/an-users-plan-01.md)
- [an-users-plan-02.md](../analisys/an-users-plan-02.md)
- [an-users-plan-avail-A.md](../analisys/an-users-plan-avail-A.md)
- [an-users-plan-03.md](../analisys/an-users-plan-03.md)
- [an-users-plan-04.md](../analisys/an-users-plan-04.md)
- [an-users-plan-05.md](../analisys/an-users-plan-05.md)

O plano de borda/sessão [plan-users-edge-session.md](plan-users-edge-session.md) está **COMPLETED**. A borda do IdP
já fala por facades (`IUserDirectory`, `ISubjectStore`, `ILocalUserAuthenticator`, provider de claims e sessão).
Este plano cria a camada B: o módulo real de contas, com domínio rico, persistência própria, properties por escopo,
credencial local mínima e integração opt-in com a borda.

Correções consolidadas:

- Nome do módulo: **`UserAccounts`**, não `UsersAccounts`.
- Target real da solução: **`net10.0`** via `Directory.Build.props`.
- `plan-resources-redesign.md` está **COMPLETED**; `IdentityScope.Name` é base estável para a integração por string.
- O módulo puro **não referencia `RoyalIdentity`**. A adaptação para o IdP fica em projeto separado.
- O seam de claims da borda passa a ser `IUserClaimsProvider` retornando `IReadOnlyList<Claim>`.
- Refinamentos de revisão incorporados: identidade de eventos por `SubjectId`; regra de login por email × `AllowDuplicateEmail`;
  semântica de `AccountStatus` vs lockout; `ClaimType` denormalizado/imutável; fonte/ciclo das `UserAccountsRealmOptions`;
  `SearchAccounts` como read-model groundwork; independência da Fase 2 vs pré-flight.

---

## Objetivo

1. Criar a família `RoyalIdentity.UserAccounts`:
   - `RoyalIdentity.UserAccounts`
   - `RoyalIdentity.UserAccounts.Integration`
   - `RoyalIdentity.UserAccounts.PostgreSql`
   - `RoyalIdentity.UserAccounts.Sqlite`
2. Manter o módulo puro livre de dependência do IdP.
3. Modelar `UserAccount` como agregado rico, realm-scoped, com `Id` físico `long` e `SubjectId` protocolar imutável.
4. Modelar emails, roles, `ExternalId`, credencial local mínima, lockout e estado de conta.
5. Modelar properties dinâmicas por escopo com schema relacional explícito.
6. Criar `UserAccountsRealmOptions` e separar as opções de conta que hoje vivem em `AccountOptions`.
7. Implementar a integração com as portas da borda do IdP no projeto `.Integration`.
8. Manter fake/in-memory durante a migração, com contract tests e DI opt-in até a paridade estar provada.

---

## Fora de escopo

- API/UI administrativa do módulo. Fica para `plan-admin-api-ui`.
- Ciclo completo de segurança da conta: recuperação de senha, verificação de email/phone, histórico de senha,
  expiração com enforcement completo, `SecurityStamp` + invalidação de sessão/cookie. Fica para
  `plan-users-security-lifecycle`.
- Federação/login externo, MFA e passwordless. Apenas costuras/modelos reservados quando necessário.
- Outbox/inbox, persistência/despacho de eventos e replicação entre instâncias. Eventos de domínio entram no agregado,
  mas não são persistidos neste plano.
- Persistência de dados do IdP (`Data.Configuration`, `Data.Operational`, `Storage.EntityFramework`, caching).
  O módulo tem persistência própria e não é adaptado pelo storage EF do IdP.
- Troca global obrigatória do fake. A integração do módulo é opt-in até a regressão estar verde.

---

## Decisões fechadas

### Nome, projetos e dependência

- Nome base: `RoyalIdentity.UserAccounts`.
- Projetos ficam fisicamente no mesmo diretório dos demais projetos da solução.
- Na solution, ficam agrupados na mesma folder virtual do `RoyalIdentity`.
- Família final:

```text
RoyalIdentity.UserAccounts
RoyalIdentity.UserAccounts.Integration
RoyalIdentity.UserAccounts.PostgreSql
RoyalIdentity.UserAccounts.Sqlite
```

- `RoyalIdentity.UserAccounts` contém domínio, features, contratos próprios do módulo, `DbContext` base,
  modelo comum e persistência própria comum.
- `RoyalIdentity.UserAccounts.PostgreSql` contém provider/configurações/mapeamentos/migrations PostgreSQL.
- `RoyalIdentity.UserAccounts.Sqlite` contém provider/configurações/mapeamentos/migrations SQLite, usado também
  em testes com conexão in-memory.
- `RoyalIdentity.UserAccounts.Integration` referencia `RoyalIdentity` e `RoyalIdentity.UserAccounts`, implementa
  as portas do IdP e expõe `AddUserAccountsForRoyalIdentity(...)`.
- O core `RoyalIdentity` não referencia o módulo.
- O módulo puro não referencia `RoyalIdentity`.

### Borda e claims

- Renomear no core:
  - `IUserPropertyProvider` -> `IUserClaimsProvider`
  - `IUserDirectory.GetPropertyProvider(realm)` -> `GetClaimsProvider(realm)`
  - `MemoryUserPropertyProvider` -> `MemoryUserClaimsProvider`
  - remover/aposentar `UserClaimDto`
- Contrato alvo:

```csharp
using System.Security.Claims;

public interface IUserClaimsProvider
{
	Task<IReadOnlyList<Claim>> GetClaimsAsync(
		string subjectId,
		IReadOnlyCollection<string> identityScopeNames,
		IReadOnlyCollection<string> claimTypes,
		CancellationToken ct = default);
}
```

- `Claim` aparece apenas na borda do IdP e no projeto `.Integration`.
- O domínio/persistência do módulo não usa `Claim` como vocabulário interno.
- O módulo fala em conta, email, role, propriedade, definição, valor e projeção.

### Semântica de escopos e claims

O IdP continua autoritativo sobre:

- identity scopes existentes;
- claim types declarados para cada identity scope;
- discovery/consent.

O `IUserClaimsProvider` recebe:

- nomes dos identity scopes solicitados;
- claim types associados a esses scopes.

Regra de emissão:

```text
claim emitida =
	existe no UserAccounts para o scope solicitado
	AND claim type foi solicitado/autorizado pelo IdP
```

Adicionar uma propriedade dinâmica envolve configurar os dois lados:

- `PropertyDefinition` no `UserAccounts`;
- claim type em `IdentityScope.UserClaims` no IdP.

O módulo não valida contra o catálogo rico do IdP. A coerência é operacional/administrativa; automação futura
por eventos (`PropertyScope` draft -> active -> criar/atualizar identity scope no IdP) fica fora deste plano.

### Campos fixos e projeção

Campos fixos de conta não entram na tabela de propriedades dinâmicas:

- `Username`
- `DisplayName`
- `Emails`
- `Roles`
- `ExternalId`

Eles são projetados para claims por opções do módulo, por realm:

```text
{ FixedField, ScopeName, ClaimType, Include }
```

Exemplos:

```text
Username      -> { ScopeName = "profile", ClaimType = "preferred_username", Include = true }
DisplayName   -> { ScopeName = "profile", ClaimType = "name", Include = true }
PrimaryEmail  -> { ScopeName = "email",   ClaimType = "email", Include = true }
EmailVerified -> { ScopeName = "email",   ClaimType = "email_verified", Include = true }
Roles         -> { ScopeName = "profile", ClaimType = "role", Include = true }
ExternalId    -> { ScopeName = "profile", ClaimType = "...", Include = false/true }
```

`ClaimType` deve ser único por realm através de projeções fixas + definições dinâmicas. A configuração deve rejeitar
colisões para evitar claim duplicada.

### Options

O módulo tem options próprias por realm: `UserAccountsRealmOptions` (nome a confirmar na ADR-015).
Segue o padrão copy-on-create por realm já adotado no IdP.

**Fonte de verdade e ciclo de vida (a fixar na ADR-015 / Fase 4):** as `UserAccountsRealmOptions` são **do módulo**,
persistidas/configuradas pelo próprio módulo por realm (não vivem na `RealmOptions` do IdP). A `.Integration` **resolve**
as options do realm a partir do `RealmId` (não da `Realm` rica) e as injeta nas portas realm-bound. Durante a **coexistência**
com o fake — enquanto a política ainda vier da `AccountOptions` do IdP — a `.Integration` faz a **ponte** (lê do IdP e popula
as options do módulo), **sem duplicar a regra**: assim que o módulo entra, a fonte de verdade passa a ser `UserAccountsRealmOptions`.

Migra para `UserAccounts`:

- `AllowRegistration`
- `AllowForgotPassword`
- `AllowChangePassword`
- `AllowUpdateProfile`
- `AllowChangeEmail`
- `AllowChangeUsername`
- `AllowChangePhoneNumber`
- `AllowDeleteAccount`
- `EmailAsUsername`
- `LoginWithEmail`
- `AllowDuplicateEmail`
- `VerifyEmail`
- `PasswordOptions`

Permanece no IdP:

- `AllowLocalLogin`
- `AllowRememberLogin`
- `RememberMeLoginDuration`
- `AutomaticRedirectAfterSignOut`
- mensagens genéricas de login (`InvalidCredentialsErrorMessage`, `InactiveUserErrorMessage`, `BlockedUserErrorMessage`)

Fora deste plano:

- `AllowTwoFactorAuthentication`
- `AllowSocialLogin`

`PasswordOptions` pertence ao `UserAccounts`. Neste plano:

- `MaxFailedAccessAttempts` e `AccountLockoutDurationMinutes` têm enforcement no autenticador.
- Regras de complexidade têm enforcement em `SetPassword`/`ChangePassword`.
- Expiração e histórico são políticas persistidas, mas enforcement completo fica para o plano de lifecycle.
- Duplicatas `AllowForgotPassword`/`AllowChangePassword` devem ser consolidadas em um único lugar.

### Identidade da conta

Usar duas identidades distintas:

- `UserAccount.Id`: PK física interna, `long` / PostgreSQL `bigint generated ... as identity`.
- `UserAccount.SubjectId`: identificador de protocolo/negócio, `string`, usado como OIDC `sub`.

`SubjectId`:

- é gerado por padrão com `CryptoRandom.CreateUniqueId()`;
- pode ser informado no cadastro quando `UserAccountsRealmOptions` permitir;
- é imutável depois da criação;
- é único por realm: unique `(RealmId, SubjectId)`;
- nunca deriva de username/email;
- nunca substitui o `Id` interno em FKs.

### Conta, email, roles e external id

- `RealmId` é estrutural em todas as raízes/tabelas realm-scoped.
- `Username` é campo fixo de primeira classe, normalizado para busca e único por realm.
- `AccountStatus` é o estado **administrativo** da conta: `Active`, `Inactive` (desabilitada por admin) e `Blocked`
  (bloqueio administrativo indefinido). É **distinto do lockout**, que é temporário e mora na credencial (`LockoutEndAt`).
  O autenticador mapeia: status `Inactive` -> reason `Inactive`; status `Blocked` **ou** lockout ativo -> reason `Blocked`.
- `Email` é coleção dedicada: `Address`, `IsPrimary`, `IsVerified`, `IsFictitious`.
- Email pode ser múltiplo; duplicidade entre contas respeita `AllowDuplicateEmail`.
- **Login por email** só é determinístico quando o endereço é único no realm. Regra (a ratificar na ADR-015):
  `LoginWithEmail`/`EmailAsUsername` exigem `AllowDuplicateEmail = false`; quando habilitados, o login por email
  resolve **apenas pelo email primário e verificado**. Endereço duplicado/ambíguo **nunca autentica** (evita login não-determinístico).
- Email fictício é gerado por policy por realm, com pattern configurável e `IsVerified` default configurável.
- `ExternalId?` é um campo opcional, índice não único por realm, não usado como credencial de login.
- `Roles` são primeira classe no agregado e projetadas como claim `role` quando configuradas/autorizadas.

### Eventos

Eventos de domínio entram no agregado, mas não são persistidos, despachados ou gravados em outbox neste plano.
Documentar isso na ADR-015 e no roadmap/backlog para evitar leitura como código inútil.

Os eventos **chaveiam por `(RealmId, SubjectId)`**, nunca pelo `UserAccount.Id` físico (interno, pode ser `0` antes do
insert). O `Id` físico não cruza a fronteira do agregado nem entra em eventos/outbox/replicação futura.

Eventos esperados:

- `UserAccountCreated`
- `UsernameChanged`
- `EmailAdded`
- `EmailRemoved`
- `PrimaryEmailChanged`
- `CredentialChanged`
- `AccountActivated`
- `AccountDeactivated`
- `AccountBlocked`
- `AccountUnblocked`
- `PropertyValueChanged`
- `PropertyDefinitionChanged`

---

## Modelo relacional mínimo

### `UserAccount`

```text
UserAccount
  Id                    bigint identity primary key
  RealmId               string not null
  SubjectId             string not null
  Username              string not null
  NormalizedUsername    string not null
  DisplayName           string not null
  AccountStatus         string/int not null
  ExternalId            string null
  CreatedAt             timestamp not null
  UpdatedAt             timestamp null

  unique (RealmId, SubjectId)
  unique (RealmId, NormalizedUsername)
  index  (RealmId, ExternalId) where ExternalId is not null
  index  (RealmId, AccountStatus, CreatedAt, Id)
```

> O índice `(RealmId, AccountStatus, CreatedAt, Id)` e o caso de uso `SearchAccounts` são **groundwork de read model**
> (oportunidade), **não requeridos** pela integração com a borda; o enforcement administrativo e a API/UI ficam no
> `plan-admin-api-ui` (#5). Mantidos por baixo custo; podem ser adiados sem impacto na integração.

### `UserAccountEmail`

```text
UserAccountEmail
  Id                    bigint identity primary key
  RealmId               string not null
  UserAccountId         bigint not null foreign key -> UserAccount.Id
  Address               string not null
  NormalizedAddress     string not null
  IsPrimary             bool not null
  IsVerified            bool not null
  IsFictitious          bool not null

  index  (RealmId, UserAccountId)
  unique (RealmId, UserAccountId, NormalizedAddress)
  unique (RealmId, UserAccountId) where IsPrimary = true
  index  (RealmId, NormalizedAddress)
```

Como `AllowDuplicateEmail` é opção por realm, não criar unique global `(RealmId, NormalizedAddress)`.
Quando `AllowDuplicateEmail = false`, a unicidade entre contas deve ser garantida pelo caso de uso/repositório
em transação. Se for necessário reforço físico no banco, usar uma tabela de guard/claim de unicidade ou índice
parcial com marcador persistido de política; decidir isso na ADR-015 antes da implementação.

### `UserAccountRole`

```text
UserAccountRole
  Id                    bigint identity primary key
  RealmId               string not null
  UserAccountId         bigint not null foreign key -> UserAccount.Id
  Name                  string not null
  NormalizedName        string not null

  unique (RealmId, UserAccountId, NormalizedName)
  index  (RealmId, NormalizedName)
```

### `UserAccountCredential`

```text
UserAccountCredential
  UserAccountId                 bigint primary key foreign key -> UserAccount.Id
  RealmId                       string not null
  PasswordHash                  string null
  FailedPasswordAttempts        int not null
  LastPasswordFailureAt         timestamp null
  LockoutEndAt                  timestamp null
  PasswordChangedAt             timestamp null
  MustChangePassword            bool not null

  index (RealmId, UserAccountId)
```

O shape pode ser owned/mesma tabela se a implementação EF local preferir, mas o plano considera estes campos
como parte da conta e não como store separado.

### `PropertyScope`

```text
PropertyScope
  Id                    bigint identity primary key
  RealmId               string not null
  Name                  string not null   -- casa com IdentityScope.Name
  Status                string/int not null  -- Draft/Active
  DisplayName           string null

  unique (RealmId, Name)
```

### `PropertyDefinition`

```text
PropertyDefinition
  Id                    bigint identity primary key
  RealmId               string not null
  PropertyScopeId       bigint not null foreign key -> PropertyScope.Id
  ClaimType             string not null
  ValueType             string null
  DisplayName           string null
  Help                  string null
  IsSensitive           bool not null
  IsRequired            bool not null
  Multiplicity          string/int not null  -- Single/Multi
  Validation            json/string null
  IsActive              bool not null

  unique (RealmId, ClaimType)
  index  (RealmId, PropertyScopeId)
```

### `UserAccountPropertyValue`

```text
UserAccountPropertyValue
  Id                    bigint identity primary key
  RealmId               string not null
  UserAccountId         bigint not null foreign key -> UserAccount.Id
  PropertyDefinitionId  bigint not null foreign key -> PropertyDefinition.Id
  ClaimType             string not null
  Value                 string not null
  ValueType             string null
  Ordinal               int not null default 0

  unique (UserAccountId, PropertyDefinitionId, Ordinal)
  index  (RealmId, UserAccountId, ClaimType)
  index  (RealmId, ClaimType)
```

Para `Multiplicity = Single`, o domínio aceita apenas `Ordinal = 0`. Para `Multi`, aceita N linhas com ordinal distinto.

`PropertyDefinition.ClaimType` é **imutável** (é a identidade do claim no realm). As colunas `ClaimType`/`ValueType` em
`UserAccountPropertyValue` são **cópias denormalizadas** da definição, mantidas em sincronia na escrita apenas para acelerar
leituras; a **fonte de verdade** é a `PropertyDefinition` (a projeção canônica faz join e lê `d.claim_type`/`d.value_type`).

### Queries de referência

Login por username:

```sql
select *
from user_accounts
where realm_id = @realmId
  and normalized_username = @login;
```

Login por email:

```sql
select a.*
from user_account_emails e
join user_accounts a on a.id = e.user_account_id
where e.realm_id = @realmId
  and e.normalized_address = @email;
```

Lookup de subject:

```sql
select id, subject_id, display_name, account_status
from user_accounts
where realm_id = @realmId
  and subject_id = @subjectId;
```

Projeção de claims dinâmicas:

```sql
select d.claim_type, v.value, v.value_type
from user_accounts a
join user_account_property_values v on v.user_account_id = a.id
join property_definitions d on d.id = v.property_definition_id
join property_scopes s on s.id = d.property_scope_id
where a.realm_id = @realmId
  and a.subject_id = @subjectId
  and s.name = any(@identityScopeNames)
  and d.claim_type = any(@claimTypes)
  and d.is_active = true;
```

---

## Contratos da borda alvo

Após a emenda da ADR-014 e do core, a integração implementa:

| Porta | Assinatura alvo | Comportamento |
|---|---|---|
| `ISubjectStore` | `FindBySubjectIdAsync(subjectId, ct) -> Subject?`; `IsActiveAsync(subjectId, ct) -> bool` | lookup por `(RealmId, SubjectId)`; retorna `Subject(SubjectId, DisplayName, IsActive)`. |
| `ILocalUserAuthenticator` | `AuthenticateLocalAsync(login, password, ct) -> AuthenticationResult` | resolve username/email conforme options; ordem `NotFound -> Inactive -> Blocked -> InvalidCredentials`; falha incrementa; sucesso zera. |
| `IUserClaimsProvider` | `GetClaimsAsync(subjectId, identityScopeNames, claimTypes, ct) -> IReadOnlyList<Claim>` | conta inexistente/inativa -> `[]`; emite somente interseção scope + claim type; combina campos fixos, roles e properties dinâmicas. |
| `IUserDirectory` | `GetSubjectStore(realm)`; `GetLocalAuthenticator(realm)`; `GetClaimsProvider(realm)` | fábrica realm-bound das três portas. |

`Realm` continua sendo recebido pelo gateway do core (`IUserDirectory`) porque esta é a forma atual da borda.
O projeto `.Integration` é quem conhece `Realm`; o módulo puro recebe apenas `RealmId`/options próprias.

---

## Arquitetura alvo

```text
RoyalIdentity.UserAccounts/
  Features/
    Accounts/
      Domain/
        UserAccount
        UserAccountEmail
        UserAccountRole
        Credential
        SubjectId
        Username
        AccountStatus
        Events/
      Commons/
        CreateAccount
        SeedAccount
        GetAccountForSubject
        GetAccountForLogin
        SearchAccounts            (read-model groundwork; admin -> #5)
      ChangePassword/
      Activate/
      Deactivate/
      SetScopeProperties/
    ScopeProperties/
      Domain/
        PropertyScope
        PropertyDefinition
        UserAccountPropertyValue
      Commons/
        SeedDefaultPropertyScopes
        ValidateClaimProjectionConfiguration
  Infrastructure/
    Data/
      UserAccountsDbContext
      ConfigureUserAccounts
      mappings
    Searches/
  Options/
    UserAccountsRealmOptions
    fixed-field claim projection options

RoyalIdentity.UserAccounts.PostgreSql/
  provider-specific mappings, indexes and migrations

RoyalIdentity.UserAccounts.Sqlite/
  provider-specific mappings, indexes and migrations
  SQLite in-memory test support

RoyalIdentity.UserAccounts.Integration/
  SubjectStore : ISubjectStore
  LocalUserAuthenticator : ILocalUserAuthenticator
  UserClaimsProvider : IUserClaimsProvider
  UserAccountsUserDirectory : IUserDirectory
  AddUserAccountsForRoyalIdentity(...)
```

API/UI administrativas ficam em projetos futuros (`*.Api`, `*.Web`), fora deste plano.

---

## Ordem de execução

1. Fase 1 fecha documentação/ADRs.
2. Fase 2 altera a borda de claims no core e mantém os testes da borda verdes.
3. Fase 3 valida pacotes RoyalCode e cria a família de projetos.
4. Fases 4-6 modelam options, domínio e properties.
5. Fase 7 persiste o modelo e prova round-trip.
6. Fase 8 cria apenas casos de uso necessários para a borda.
7. Fase 9 adapta ao IdP no projeto `.Integration`.
8. Fase 10 prova paridade, seeds, DI opt-in e regressão.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
$env:Logging__EventLog__LogLevel__Default = "None"
dotnet test RoyalIdentity.sln --no-build --nologo
```

---

## Fase 1 - Governança, ADRs e emendas de documentação

**O que/como:** registrar o módulo como arquitetura aceita antes de codar. A ADR-015 é o documento autoritativo
do módulo; ADR-014 precisa ser emendada para a mudança do seam de claims; ADR-013/architecture/roadmap precisam
parar de falar em `UsersAccounts`, `.Postgre` e `Integration/` interno.

**Tarefas:**

- [x] Criar `adrs/ADR-015.md` para `RoyalIdentity.UserAccounts`.
- [x] Registrar na ADR-015: família de projetos, projeto `.Integration` separado, `RealmId` estrutural,
      `UserAccount.Id` PK física, `SubjectId` protocolar, schema de properties, options do módulo,
      split de `AccountOptions`, eventos não persistidos.
- [x] Emendar `adrs/ADR-014.md`: `IUserPropertyProvider` -> `IUserClaimsProvider`, retorno `Claim`,
      remoção de `UserClaimDto`, `GetPropertyProvider` -> `GetClaimsProvider`.
- [x] Emendar `adrs/ADR-013.md`: `UserAccounts`, projeto `.Integration`, `.PostgreSql`/`.Sqlite`.
- [x] Atualizar `.ai/foundation/architecture.md` para refletir o projeto `.Integration` separado.
- [x] Atualizar `plans-roadmap-01.md` para `UserAccounts`, `IUserClaimsProvider`, `.PostgreSql`.
- [x] Atualizar `AGENTS.md`/`CLAUDE.md` onde ainda citarem `net9.0`, `UsersAccounts`, resources `IN_PROGRESS`
      ou `IUserPropertyProvider`.

**Critérios de aceite:** documentação não contradiz o v2; ADR-015 existe; ADR-014 registra a mudança de contrato.

**Testes:** n/a, documentação.

### Resultado da Fase 1

Concluida (documentação). Entregáveis:

- **`adrs/ADR-015.md`** criado — módulo `RoyalIdentity.UserAccounts`: família de projetos (puro + `.Integration` +
  `.PostgreSql`/`.Sqlite`), `UserAccount.Id` (PK física `long`) × `SubjectId` (protocolar, imutável, único por realm),
  `RealmId` estrutural, schema de properties por escopo, `UserAccountsRealmOptions` + split de `AccountOptions`,
  seam `IUserClaimsProvider`/`Claim` (interseção), eventos não persistidos, coexistência opt-in.
- **`adrs/ADR-014.md` emendada** (§4 + banner) — `IUserPropertyProvider`→`IUserClaimsProvider`,
  `GetPropertyProvider`→`GetClaimsProvider`, retorno `IReadOnlyList<Claim>`, remoção de `UserClaimDto`. (Código: Fase 2.)
- **`adrs/ADR-013.md` emendada** (§5 + banner) — nome `UserAccounts`, `.Integration` como padrão de adapter, grafia `.PostgreSql`.
- **`.ai/foundation/architecture.md` reescrita** — `.Integration` como projeto separado; módulo puro sem ref ao core;
  família `.PostgreSql`/`.Sqlite`; seam `IUserClaimsProvider`/`Claim`; §1/§2/§3/§7/§9/§10 atualizados.
- **`plans-roadmap-01.md`** — `UserAccounts`, `IUserClaimsProvider`, `.PostgreSql`, plano sugerido apontando para o v2.
- **`CLAUDE.md`** — `resources`/`users-edge-session` movidos para COMPLETED; novo plano ativo; ADRs `..015`; bullet `UserAccounts`.
- **`AGENTS.md`** — `net9.0`→`net10.0`; `UsersAccounts`→`UserAccounts`; `IUserPropertyProvider`→`IUserClaimsProvider` (com nota); ADRs `..015`.

**Nota de ordenação:** os docs forward-looking (ADR-015, architecture, roadmap) já usam os nomes-alvo
(`IUserClaimsProvider`, `.Integration`, `UserAccounts`); o **código** ainda tem `IUserPropertyProvider`/`UserClaimDto` —
a renomeação no core acontece na **Fase 2**. As notas em ADR-014/AGENTS sinalizam essa janela.

**Critérios de aceite:** atendidos — ADR-015 existe; ADR-014 registra a mudança de contrato; documentação alinhada ao v2.

---

## Fase 2 - Emenda da borda de claims no core

**O que/como:** aplicar no código do core a mudança já decidida para o seam de claims. Esta fase é pequena e
deve ser concluída antes do projeto `.Integration`. É **independente das libs RoyalCode** (mexe só em core/borda),
então vale por si mesma mesmo que o pré-flight da Fase 3 reprove alguma versão — não há acoplamento entre as duas.

**Tarefas:**

- [x] Renomear `IUserPropertyProvider` para `IUserClaimsProvider`.
- [x] Renomear `IUserDirectory.GetPropertyProvider` para `GetClaimsProvider`.
- [x] Alterar retorno do provider para `Task<IReadOnlyList<Claim>>`.
- [x] Remover/aposentar `UserClaimDto`.
- [x] Atualizar `DefaultProfileService` para receber `Claim` diretamente.
- [x] Renomear `MemoryUserPropertyProvider` para `MemoryUserClaimsProvider`.
- [x] Atualizar testes da borda e contract tests existentes.

**Critérios de aceite:** comportamento efetivo de claims não muda; suite atual continua verde.

**Testes:** `dotnet test Tests.Identity`; `dotnet test Tests.Integration`; suite completa se houver impacto amplo.

### Resultado da Fase 2

**Concluída (2026-06-17).** O seam de claims foi renomeado no core/borda, com o retorno passando a `Claim` da BCL
(sem DTO intermediário). Sem mudança de comportamento efetivo de claims.

**Arquivos novos:**
- `RoyalIdentity/Users/Contracts/IUserClaimsProvider.cs` — contrato renomeado; `GetClaimsAsync(...) → Task<IReadOnlyList<Claim>>`.
- `RoyalIdentity.Storage.InMemory/MemoryUserClaimsProvider.cs` — fake renomeado; projeta `Claim` direto (mesma regra de filtro por claim type).

**Arquivos removidos** (`git rm`):
- `RoyalIdentity/Users/Contracts/IUserPropertyProvider.cs`
- `RoyalIdentity/Users/UserClaimDto.cs`
- `RoyalIdentity.Storage.InMemory/MemoryUserPropertyProvider.cs`

**Arquivos alterados:**
- `RoyalIdentity/Users/Contracts/IUserDirectory.cs` — `GetPropertyProvider` → `GetClaimsProvider`, retorno `IUserClaimsProvider`.
- `RoyalIdentity/Contracts/Defaults/DefaultProfileService.cs` — chama `GetClaimsProvider`; adiciona os `Claim` recebidos
  direto em `IssuedClaims` (removida a reconstrução `UserClaimDto`→`Claim`).
- `RoyalIdentity.Storage.InMemory/MemoryUserDirectory.cs` — instancia `MemoryUserClaimsProvider` no getter renomeado.
- Comentários XML alinhados (`MemoryUserAccount`, `DefaultSubjectPrincipalFactory`, `ISubjectPrincipalFactory`,
  `ClaimsSeamCharacterizationTests`).

**Verificação:** `dotnet build RoyalIdentity.sln` — 0 erros. Suíte completa **verde**: Tests.Pipelines 3/3,
Tests.Identity 9/9, Tests.Integration 194/194 (inclui `ClaimsSeamCharacterizationTests` — id_token/userinfo/access
token mantêm a projeção por identity scope e o filtro de claim type). Sem grep residual de `IUserPropertyProvider`/
`UserClaimDto`/`GetPropertyProvider` em `*.cs`.

**Nota:** com isto, **código e documentação ficam alinhados** — encerra a janela "docs à frente do código" aberta na Fase 1.

---

## Fase 3 - Pré-flight RoyalCode + esqueleto da família de projetos

**O que/como:** provar cedo que as libs RoyalCode estão disponíveis e compatíveis com `net10.0`, e criar os projetos
com dependências corretas.

**Tarefas:**

- [ ] Validar pacotes RoyalCode em `nuget.org`: SmartCommands, SmartSearch, SmartSelector, SmartValidations,
      SmartProblems, WorkContext/domain.
- [ ] Fixar versões compatíveis com `net10.0`.
- [ ] Criar um smoke test mínimo compilando `[Command]`, `ICriteria<T>` e `IWorkContextBuilder`.
- [ ] Criar `RoyalIdentity.UserAccounts`.
- [ ] Criar `RoyalIdentity.UserAccounts.PostgreSql`.
- [ ] Criar `RoyalIdentity.UserAccounts.Sqlite`.
- [ ] Criar `RoyalIdentity.UserAccounts.Integration`.
- [ ] Adicionar projetos à solution.
- [ ] Referências:
      - módulo puro: RoyalCode/EFCore, sem `RoyalIdentity`;
      - providers: módulo puro + provider EF;
      - integration: `RoyalIdentity` + módulo puro.
- [ ] Testes de arquitetura: core não referencia módulo; módulo puro não referencia core; `Domain` não depende de ASP.NET;
      `Features` não depende de ASP.NET; `.Integration` é o único projeto que conhece as portas do IdP.

**Critérios de aceite:** solução compila; smoke RoyalCode passa; fronteiras de projeto protegidas por testes.

**Testes:** build + testes de arquitetura.

### Resultado da Fase 3

*a preencher*

---

## Fase 4 - Options do módulo + split de `AccountOptions`

**O que/como:** criar o modelo de options por realm do módulo e mover a fonte de verdade das políticas de conta para ele,
sem duplicar regra entre IdP e módulo.

**Tarefas:**

- [ ] Modelar `UserAccountsRealmOptions`.
- [ ] Incluir políticas de login identifier: `EmailAsUsername`, `LoginWithEmail`.
- [ ] Incluir políticas de email: múltiplos, `AllowDuplicateEmail`, `VerifyEmail`, fictício pattern,
      fictício `IsVerified` default, `AllowChangeEmail`.
- [ ] Incluir políticas de username, registro, perfil, phone, exclusão.
- [ ] Incluir `PasswordOptions` do módulo e consolidar duplicatas.
- [ ] Incluir política de `SubjectId` informado no cadastro.
- [ ] Incluir projeção de campos fixos para claims.
- [ ] Definir como a integração resolve options do realm para o módulo durante coexistência.
- [ ] Manter no IdP apenas opções de fluxo/cookie/UI.

**Critérios de aceite:** cada opção tem um único dono; login UI lê affordances de conta via integração/política do módulo
quando necessário.

**Testes:** unidade de options/copy-on-create; teste de split onde aplicável.

### Resultado da Fase 4

*a preencher*

---

## Fase 5 - Domínio de contas (`UserAccount`)

**O que/como:** modelar o agregado e invariantes principais, sem persistência ainda.

**Tarefas:**

- [ ] `UserAccount : AggregateRoot<long>` com `Id` físico interno.
- [ ] `RealmId` obrigatório no agregado.
- [ ] `SubjectId` string, imutável, gerado por default e externo-opcional por policy.
- [ ] `Username`/`NormalizedUsername`, `DisplayName`, `AccountStatus`.
- [ ] `ExternalId?` opcional, não credencial.
- [ ] Emails com primário/verificado/fictício.
- [ ] Roles primeira classe.
- [ ] Credencial local mínima: hash, lockout counters, timestamps.
- [ ] `SetPassword`/`ChangePassword` com complexidade básica.
- [ ] `AuthenticateLocal` ou serviço de domínio equivalente: verificar senha + lockout.
- [ ] Eventos de domínio via `AddEvent`, sem persistir/despachar.
- [ ] Resultados com `Result`/`Problems`, sem throw para fluxo esperado.

**Critérios de aceite:** invariantes de conta cobertas; `SubjectId` imutável; troca de username não troca `sub`;
lockout incrementa/zera/expira; senha ausente bloqueia login por senha.

**Testes:** unidade de domínio.

### Resultado da Fase 5

*a preencher*

---

## Fase 6 - Propriedades dinâmicas por escopo

**O que/como:** modelar schema e valores de properties de perfil, separados de campos fixos.

**Tarefas:**

- [ ] Modelar `PropertyScope` por realm, `Name` casando com `IdentityScope.Name` por string.
- [ ] Modelar `PropertyDefinition` com `ClaimType`, `ValueType`, `DisplayName`, `Help`, `IsSensitive`,
      `IsRequired`, `Multiplicity`, validação e `IsActive`.
- [ ] Modelar `UserAccountPropertyValue` como valor por conta.
- [ ] Garantir `ClaimType` único por realm entre definitions dinâmicas.
- [ ] Validar colisão entre projections fixas e properties dinâmicas.
- [ ] Implementar projeção interna que combine:
      - campos fixos;
      - roles;
      - values dinâmicos.
- [ ] Aplicar interseção com `identityScopeNames` + `claimTypes`.
- [ ] Seed de scopes padrão (`profile`, `email`) e projeções padrão por realm.

**Critérios de aceite:** conta não carrega schema próprio; definição e valor são separados; escopo não solicitado
não emite; claim type não solicitado não emite; conta inativa não emite claims.

**Testes:** unidade de projeção e validação de schema.

### Resultado da Fase 6

*a preencher*

---

## Fase 7 - Persistência própria EFCore + providers

**O que/como:** persistir o domínio e o schema de properties em banco relacional, com providers separados.

**Tarefas:**

- [ ] Criar `UserAccountsDbContext`.
- [ ] Mapear `UserAccount`, emails, roles, credencial, property scopes, definitions e values.
- [ ] Implementar índices/chaves do "Modelo relacional mínimo".
- [ ] Implementar provider PostgreSql com indexes específicos.
- [ ] Implementar provider Sqlite com conexão in-memory para testes.
- [ ] Configurar `ConfigureUserAccounts(IWorkContextBuilder)`.
- [ ] Criar searches/selectors para consultas por subject, login, email e claims.
- [ ] Testar round-trip do agregado e properties.

**Critérios de aceite:** persistência realm-scoped; queries principais usam índices; providers não dependem do IdP;
Sqlite in-memory cobre testes sem DB externo.

**Testes:** integração de persistência com Sqlite in-memory; build completo.

### Resultado da Fase 7

*a preencher*

---

## Fase 8 - Casos de uso mínimos para integração com o IdP

**O que/como:** implementar apenas os casos de uso necessários à integração com a borda. CRUD/admin completo fica para
API/UI administrativa.

**Tarefas:**

- [ ] Criar/semear conta (`CreateAccount`/`SeedAccount`) com `SubjectId` determinístico quando informado.
- [ ] Buscar conta por `(RealmId, SubjectId)`.
- [ ] Buscar conta por login username/email conforme `UserAccountsRealmOptions`.
- [ ] Autenticar localmente e aplicar lockout.
- [ ] Ler status ativo/inativo.
- [ ] Projetar claims por scopes e claim types.
- [ ] Definir properties de escopo para seeds/testes.
- [ ] Alterar senha para suportar testes de credencial mínima.

**Critérios de aceite:** casos de uso suficientes para `ISubjectStore`, `ILocalUserAuthenticator` e `IUserClaimsProvider`;
sem endpoints HTTP; sem casos administrativos completos.

**Testes:** unidade/integração dos casos mínimos.

### Resultado da Fase 8

*a preencher*

---

## Fase 9 - Integração com a borda do IdP

**O que/como:** criar a adaptação entre o IdP e o módulo no projeto `.Integration`.

**Tarefas:**

- [ ] `SubjectStore : ISubjectStore`.
- [ ] `LocalUserAuthenticator : ILocalUserAuthenticator`.
- [ ] `UserClaimsProvider : IUserClaimsProvider`.
- [ ] `UserAccountsUserDirectory : IUserDirectory`.
- [ ] `AddUserAccountsForRoyalIdentity(...)`.
- [ ] Adaptar `Realm` do core para `RealmId` + `UserAccountsRealmOptions`.
- [ ] Garantir que portas retornadas são realm-bound e não recebem realm em método.
- [ ] Garantir que integration não acessa internals do módulo.

**Critérios de aceite:** `.Integration` é a única ponte com o IdP; módulo puro permanece independente; comportamento
equivalente ao fake para os contratos de borda.

**Testes:** testes unitários das portas e arquitetura.

### Resultado da Fase 9

*a preencher*

---

## Fase 10 - Contract tests, DI opt-in, seeds e regressão

**O que/como:** provar paridade de comportamento e preparar a migração conservadora do fake para o módulo.

**Tarefas:**

- [ ] Criar contract tests compartilhados para `IUserDirectory`:
      `ISubjectStore`, `ILocalUserAuthenticator`, `IUserClaimsProvider`.
- [ ] Rodar contract tests contra fake in-memory.
- [ ] Rodar contract tests contra `UserAccounts` + Sqlite in-memory.
- [ ] Seeds de paridade Alice/Bob:
      - `SubjectId` determinístico;
      - username/displayName;
      - email;
      - roles;
      - property scopes `profile`/`email`;
      - projections fixas.
- [ ] Registrar integração opt-in no host/test factory.
- [ ] Manter fake como referência/default até suite verde.
- [ ] Rodar testes do módulo.
- [ ] Rodar suite do IdP contra fake.
- [ ] Rodar suite do IdP contra módulo opt-in.
- [ ] Documentar diferidos: admin, lifecycle, federação, MFA/passwordless, outbox/inbox/replicação.

**Critérios de aceite:** contract tests passam nas duas implementações; suíte do IdP verde contra módulo opt-in;
fake permanece disponível; diferidos registrados.

**Testes:** suite completa (`dotnet test RoyalIdentity.sln`) + testes do módulo/providers.

### Resultado da Fase 10

*a preencher*

---

## Invariantes a preservar

1. Conta é realm-scoped; nada cruza realm.
2. `sub` = `SubjectId`, estável, imutável, separado de username/email.
3. `UserAccount.Id` é interno: nunca aparece como `sub`, não cruza a fronteira do agregado e não entra em eventos/outbox (que chaveiam por `(RealmId, SubjectId)`).
4. Erro de login é genérico por default; reason interno preservado para evento/auditoria.
5. Falha de senha incrementa contador; sucesso zera.
6. Lockout respeita `MaxFailedAccessAttempts` e `AccountLockoutDurationMinutes`.
7. Senha ausente/credencial desabilitada implica sem login local por senha.
8. Conta inativa implica sem autenticação e sem claims de perfil.
9. Claims são filtradas por identity scopes solicitados e claim types autorizados pelo IdP.
10. Roles saem pelo provider/profile service, não pelo cookie mínimo de sessão.
11. Resolução de login é responsabilidade do autenticador/módulo, não do store.
12. O módulo puro não conhece `IdentityScope`, `RequestedResources`, `Client`, `Realm` ou `HttpContext`.

---

## Critérios globais de aceite

1. Família `RoyalIdentity.UserAccounts` criada com projetos separados para módulo, integração e providers.
2. Módulo puro sem referência a `RoyalIdentity`.
3. `.Integration` implementa as portas do IdP.
4. Core sem referência ao módulo.
5. `IUserClaimsProvider`/`Claim` substitui `IUserPropertyProvider`/`UserClaimDto` na borda.
6. `UserAccount` rico com PK física `long`, `RealmId`, `SubjectId`, username, status, emails, roles,
   `ExternalId`, credencial mínima e events.
7. Properties por escopo têm schema relacional explícito e valores por conta.
8. `UserAccountsRealmOptions` é a fonte de verdade das políticas de conta.
9. Persistência própria do módulo com EFCore, PostgreSql e Sqlite.
10. Casos de uso mínimos suportam a integração com o IdP sem HTTP no módulo.
11. Contract tests provam paridade entre fake e módulo.
12. Suite do IdP passa com integração opt-in do módulo.
13. Diferidos documentados e não implementados acidentalmente.

---

## Riscos

- **Pacotes RoyalCode:** disponíveis, mas ainda precisam ser validados em `net10.0` com API real.
- **Emenda da borda:** `IUserClaimsProvider` toca core, fake e testes; fazer antes do módulo evita acoplamento posterior.
- **Options split:** risco de duplicar regra entre IdP e módulo; cada opção deve ter um único dono.
- **Paridade de seeds:** Alice/Bob dependem de `SubjectId` determinístico e claims efetivas atuais.
- **Properties por escopo:** risco de virar bag genérico ou subdomínio grande demais; manter schema pequeno com nomes corretos.
- **Índices condicionais por provider:** PostgreSql e Sqlite podem divergir em detalhes; contract tests e testes de persistência devem cobrir.

---

## Referências

- Plano base: [plan-users-accounts-module.md](plan-users-accounts-module.md)
- Análises do plano: [an-users-plan-01.md](../analisys/an-users-plan-01.md),
  [an-users-plan-02.md](../analisys/an-users-plan-02.md),
  [an-users-plan-avail-A.md](../analisys/an-users-plan-avail-A.md),
  [an-users-plan-03.md](../analisys/an-users-plan-03.md),
  [an-users-plan-04.md](../analisys/an-users-plan-04.md),
  [an-users-plan-05.md](../analisys/an-users-plan-05.md)
- Base de usuários: [an-users-arch.md](../analisys/an-users-arch.md),
  [an-users-final.md](../analisys/an-users-final.md),
  [an-users-pontos2.md](../analisys/an-users-pontos2.md)
- ADRs: [ADR-013](../../adrs/ADR-013.md), [ADR-014](../../adrs/ADR-014.md)
- Arquitetura: [architecture.md](../foundation/architecture.md)
- Planos relacionados: [plan-users-edge-session.md](plan-users-edge-session.md),
  [plan-resources-redesign.md](plan-resources-redesign.md),
  [plans-roadmap-01.md](plans-roadmap-01.md)
- Código de referência: `RoyalIdentity/Users/Contracts/`, `RoyalIdentity/Contracts/Defaults/DefaultProfileService.cs`,
  `RoyalIdentity.Storage.InMemory/Memory*`, `RoyalIdentity/Options/AccountOptions.cs`,
  `RoyalIdentity/Options/PasswordOptions.cs`
