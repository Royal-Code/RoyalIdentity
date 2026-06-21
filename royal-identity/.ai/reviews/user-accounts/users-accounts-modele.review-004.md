# Review 004 - UserAccounts completo: implementacao, design, padroes e completude

**Data:** 2026-06-20  
**Escopo:** `plan-users-accounts-module-v2.md` completo, com foco em `RoyalIdentity.UserAccounts`,
`.Integration`, `.PostgreSql`, `.Sqlite`, fake in-memory e suites `Tests.UserAccounts` /
`Tests.Integration` opt-in.  
**Veredito:** aprovado parcialmente como incremento arquitetural, mas **nao esta completo/seguro para troca
real do fake**. A implementacao preserva bem as fronteiras de projeto, mas ainda tem bugs de politica de email,
lacunas de persistencia de producao e validacoes de configuracao que existem como helper, nao como enforcement.

---

## Resumo executivo

Pontos bons:

- A familia de projetos existe e a fronteira principal foi respeitada: o modulo puro nao referencia o core, os
  providers nao referenciam o core e a `.Integration` e a ponte correta.
- O modelo de conta foi corrigido depois da review 001: filhos com identidade/realm/FK, estado `IsActive` vs
  bloqueio administrativo, credencial 1:1 e token `Version`.
- A modelagem de propriedades por escopo resolveu os principais pontos da review 002: `ClaimType` denormalizado,
  metodos de filhos `internal`, eventos de schema em update/activate/deactivate, e `ActiveVersionId` reconciliado
  depois do save.
- As suites focadas passam: `Tests.UserAccounts` e `Tests.Architecture` estao verdes com `--no-build`.

Problemas principais:

- Login por email implementa uma regra diferente da ADR/plano e autentica casos ambiguos.
- `AllowDuplicateEmail = false` nao e seguro sob concorrencia porque falta backstop fisico/guard.
- Colisao fixed claim x dynamic claim e apenas detectavel por helper, nao rejeitada no fluxo real.
- O token de concorrencia so e efetivo no PostgreSQL; a suite SQLite que valida o modulo nao detecta lost update.
- Providers nao entregam migrations, apesar do plano pedir provider/mappings/migrations.
- O host real nao tem caminho opt-in para o modulo; apenas o test factory tem.

---

## Achados

### P1 - Login por email viola a regra planejada e autentica dados ambiguos

**Arquivos:**  
[`UserAccountReader.cs`](../../../RoyalIdentity.UserAccounts/Features/Accounts/Commons/UserAccountReader.cs) linhas 51, 57-70  
[`UserAccountUseCasesTests.cs`](../../../Tests.UserAccounts/UserAccountUseCasesTests.cs) linhas 172-245

A ADR-015 e o plano fixam que login por email deve ser deterministico: `LoginWithEmail`/`EmailAsUsername`
exigem `AllowDuplicateEmail = false`, e o login por email resolve **somente email primario e verificado**;
duplicado/ambiguo nunca autentica.

O codigo atual faz outra coisa:

- se `VerifyEmail = false`, aceita email nao verificado;
- consulta qualquer email da conta, nao apenas o primario;
- quando ha mais de uma linha com o mesmo email, ordena por `IsPrimary` e `UserAccountId`, pega a primeira e autentica.

Os testes codificam esse desvio: `FindByLogin_ByEmail_AllowsUnverifiedEmail_WhenVerificationIsNotRequired` e
`FindByLogin_ByEmail_PrefersPrimaryEmail_WhenDuplicateRowsExist`.

**Risco:** em importacao, migracao, race condition ou mudanca posterior de politica, um endereco duplicado deixa de
ser erro e passa a escolher uma conta. Isso e especialmente sensivel porque o login por email e credencial de
autenticacao.

**Correcao sugerida:** alinhar o reader com a ADR: filtrar `IsPrimary && IsVerified`, buscar os candidatos e retornar
`null`/falha se a cardinalidade for diferente de 1. Se o produto realmente quer aceitar nao verificado quando
`VerifyEmail = false`, a ADR e o plano precisam ser emendados explicitamente; a regra de ambiguidade, porem, deve
continuar falhando fechada.

---

### P1 - Unicidade de email cross-account nao e segura com `AllowDuplicateEmail = false`

**Arquivos:**  
[`CreateUserAccount.cs`](../../../RoyalIdentity.UserAccounts/Features/Accounts/UseCases/CreateUserAccount.cs) linhas 206-208  
[`UserAccountEmailMap.cs`](../../../RoyalIdentity.UserAccounts/Infrastructure/Data/Mappings/UserAccountEmailMap.cs) linhas 29-30  
[`UserAccountsRealmOptions.cs`](../../../RoyalIdentity.UserAccounts/Options/UserAccountsRealmOptions.cs) linhas 148-159

Para email real, o command faz um `AnyAsync` antes de inserir. O schema, corretamente para uma politica por realm,
nao tem `unique (RealmId, NormalizedAddress)` global; ele tem unique por conta e um indice nao-unico por email.

Isso deixa uma janela de concorrencia:

1. request A e B verificam `AnyAsync` para o mesmo email;
2. ambos veem "nao existe";
3. ambos inserem;
4. o banco aceita, porque nao ha unique/guard cross-account.

O ajuste pos-review para email ficticio foi feito na configuracao (`FictitiousEmailPattern` deve conter `{subjectId}`
quando duplicate email e falso), mas o command tambem nao chama `Options.EnsureValid()`. Entao o fluxo direto do
modulo ainda depende de callers disciplinados.

**Risco:** quebra a invariante de `AllowDuplicateEmail = false` justamente no ponto que sustenta login por email
deterministico.

**Correcao sugerida:** adicionar um mecanismo fisico de reserva/guard por `(RealmId, NormalizedAddress)` quando a
politica do realm nao permite duplicata, ou executar a criacao sob uma estrategia transacional que de fato serialize
essa checagem no banco-alvo. Tambem vale chamar `Options.EnsureValid()` nos commands que dependem dessas politicas,
ou documentar formalmente que options invalidas sao precondicao proibida para qualquer caller do modulo.

---

### P2 - Colisao entre claims fixas e dinamicas e detectavel, mas nao e enforced

**Arquivos:**  
[`ValidateClaimProjectionConfiguration.cs`](../../../RoyalIdentity.UserAccounts/Features/ScopeProperties/Commons/ValidateClaimProjectionConfiguration.cs) linha 25  
[`UserAccountsRealmBindingFactory.cs`](../../../RoyalIdentity.UserAccounts.Integration/UserAccountsRealmBindingFactory.cs) linha 16  
[`PropertyScope.cs`](../../../RoyalIdentity.UserAccounts/Features/ScopeProperties/Domain/PropertyScope.cs) linha 160  
[`UserAccountClaimProjector.cs`](../../../RoyalIdentity.UserAccounts/Features/ScopeProperties/Commons/UserAccountClaimProjector.cs) linhas 37-38

O plano exige que `ClaimType` seja unico por realm atraves de projecoes fixas + definitions dinamicas. Existe o
helper `ValidateClaimProjectionConfiguration`, e ha teste unitario provando que ele detecta `email` duplicado.
Mas o fluxo real nao o usa:

- `UserAccountsRealmBindingFactory.Create()` chama `options.EnsureValid()` sem carregar dynamic claim types;
- `PropertyScope.AddDefinition()` nao recebe options nem validador;
- o projector emite fixed fields e dynamic values em sequencia.

Resultado: uma definition dinamica `email`/`name`/`role` pode coexistir com a projecao fixa default e o provider
emitira claims duplicadas.

**Risco:** tokens/userinfo com claims duplicadas ou conflitantes. Dependendo do consumidor, "primeira vence" ou
"ultima vence" muda autorizacao e perfil de usuario.

**Correcao sugerida:** mover essa validacao para o ponto de ativacao/aprovacao de schema, ou para um resolver de
options/configuracao que carregue as definitions ativas do realm antes de expor as portas. Adicionar teste de
integracao criando dynamic claim `email` e garantindo que a configuracao falha antes da emissao.

---

### P2 - Concorrencia otimista nao e efetiva no SQLite, e a suite principal usa SQLite

**Arquivos:**  
[`UserAccountMap.cs`](../../../RoyalIdentity.UserAccounts/Infrastructure/Data/Mappings/UserAccountMap.cs) linha 30  
[`UserAccountsPostgreSqlModelBuilderExtensions.cs`](../../../RoyalIdentity.UserAccounts.PostgreSql/UserAccountsPostgreSqlModelBuilderExtensions.cs) linhas 22-26  
[`UserAccountsSqliteModelBuilderExtensions.cs`](../../../RoyalIdentity.UserAccounts.Sqlite/UserAccountsSqliteModelBuilderExtensions.cs) linhas 16-24

O plano adicionou `Version` para evitar lost update em lockout/troca de primario. No PostgreSQL isso e mapeado para
`xmin`, com `ValueGeneratedOnAddOrUpdate()`. No SQLite, o campo fica apenas como `IsConcurrencyToken()` herdado do
mapping base, sem geracao/incremento.

Na pratica, uma coluna de concorrencia que nao muda nao detecta duas escritas concorrentes. Como `Tests.UserAccounts`
e os contract tests rodam em SQLite, a suite que diz validar o modulo nao cobre exatamente o risco que motivou
`Version`.

**Risco:** em testes e em qualquer uso SQLite, falhas simultaneas de senha e alteracoes concorrentes podem sobrescrever
estado sem `DbUpdateConcurrencyException`.

**Correcao sugerida:** implementar token app-managed no SQLite (incrementar `Version` em `SaveChanges` para entidades
modificadas) ou outro mecanismo equivalente. Adicionar teste com dois `DbContext` carregando a mesma conta, ambos
mutando contador/estado, e o segundo save falhando.

---

### P2 - Providers foram entregues sem migrations

**Arquivos:**  
[`UserAccountsPostgreSqlExtensions.cs`](../../../RoyalIdentity.UserAccounts.PostgreSql/UserAccountsPostgreSqlExtensions.cs) linhas 19-25  
[`UserAccountsSqliteExtensions.cs`](../../../RoyalIdentity.UserAccounts.Sqlite/UserAccountsSqliteExtensions.cs) linhas 19-25, 34-40

O plano define os providers como contendo provider/configuracoes/mapeamentos/migrations. A implementacao tem
`DbContext`, mappings e extensions, mas nao ha diretorios/arquivos de migrations em `.PostgreSql` nem em `.Sqlite`.
O unico caminho que cria schema automaticamente e `AddUserAccountsSqliteInMemory().EnsureDatabaseCreated()`, voltado
a testes/efemero.

**Risco:** a familia parece pronta para persistencia propria, mas nao ha artefato versionado para criar/evoluir banco
real. Isso impede validar PostgreSQL de verdade e deixa a entrega aquem do criterio da Fase 7.

**Correcao sugerida:** gerar migration inicial por provider suportado ou ajustar formalmente o plano para dizer que
migrations ficaram fora desta entrega. Para PostgreSQL, eu trataria como obrigatorio antes de chamar a fase de
persistencia de "concluida".

---

### P3 - O opt-in existe nos testes, mas nao no host real

**Arquivos:**  
[`HostServices.cs`](../../../RoyalIdentity.Server/HostServices.cs) linha 15  
[`RoyalIdentity.Server.csproj`](../../../RoyalIdentity.Server/RoyalIdentity.Server.csproj) linhas 4-6  
[`UserAccountsAppFactory.cs`](../../../Tests.Integration/Prepare/UserAccountsAppFactory.cs) linhas 31-34

O test factory registra `AddUserAccountsSqliteInMemory()` + `AddUserAccountsForRoyalIdentity()`. O host real continua
referenciando apenas `RoyalIdentity.Razor`, `RoyalIdentity.Storage.InMemory` e `RoyalIdentity`, e `HostServices`
sempre chama `AddInMemoryStorage()`.

O status do plano diz "fake permanece default e modulo opt-in validado", entao manter o fake default esta certo. O
problema e que o opt-in validado nao esta disponivel na composicao principal: so um test host customizado consegue
trocar a borda.

**Risco:** o usuario/produto nao tem caminho operacional para ligar o modulo sem editar codigo do host.

**Correcao sugerida:** adicionar um caminho configuravel no host (por exemplo `UserAccounts:Provider = InMemory|Sqlite|PostgreSql`)
que preserve o fake como default, mas registre provider + integration quando opt-in. Se isso ficou deliberadamente
fora, atualizar o plano para dizer "test factory only".

---

## Gaps de teste relevantes

- Falta teste de concorrencia de email duplicate e de `Version`/lost update.
- Falta teste negativo para login por email ambiguo: deve falhar, nao escolher por ordenacao.
- Falta teste para "email secundario nao autentica" se a ADR continuar valendo.
- Falta teste de emissao duplicada quando dynamic claim colide com fixed projection.
- Falta teste real de provider PostgreSQL/migrations; hoje a prova de persistencia e SQLite in-memory.

---

## Verificacao executada

Primeira tentativa rodei `Tests.UserAccounts` e `Tests.Architecture` em paralelo; a build falhou por lock de arquivo
em `obj` (`CS2012`, processo usando DLL), nao por falha de teste. Em seguida rodei sequencialmente:

- `dotnet test Tests.UserAccounts --no-build --nologo` - verde: 91/91.
- `dotnet test Tests.Architecture --no-build --nologo` - verde: 10/10.

Durante a tentativa com build apareceu tambem `NU1903` para `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (alta severidade).
