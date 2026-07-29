# RoyalIdentity.Migrations

Runner externo e genérico para as três famílias persistentes:

- Configuration;
- Operational;
- UserAccounts.

`RoyalIdentity.Server` não referencia nem executa este projeto. O runner recebe um único provider por execução
(`sqlite` ou `postgresql`) e uma connection string explícita para cada família selecionada. Não existe combinação
SQLite + PostgreSQL no mesmo processo.

## SQLite compartilhado

As três chaves continuam explícitas mesmo quando apontam para o mesmo arquivo. `--database-topology shared`
declara que uma falha deve interromper as famílias seguintes naquele mesmo banco.

```powershell
$env:ROYALIDENTITY_CONFIGURATION_DB = 'Data Source=C:\data\royalidentity.db'
$env:ROYALIDENTITY_OPERATIONAL_DB = 'Data Source=C:\data\royalidentity.db'
$env:ROYALIDENTITY_USER_ACCOUNTS_DB = 'Data Source=C:\data\royalidentity.db'

dotnet run --project RoyalIdentity.Migrations -- `
  --provider sqlite `
  --families all `
  --configuration-connection-env ROYALIDENTITY_CONFIGURATION_DB `
  --operational-connection-env ROYALIDENTITY_OPERATIONAL_DB `
  --user-accounts-connection-env ROYALIDENTITY_USER_ACCOUNTS_DB `
  --database-topology shared
```

Configuration, Operational e UserAccounts mantêm histories distintas. A execução repetida é idempotente e não
usa transação distribuída entre as famílias. Configuration e UserAccounts já usaram a history default em versões
anteriores; por isso o runner atribui `__EFMigrationsHistory` pelos migration ids antes de relocá-la. Uma history
inteiramente UserAccounts é preservada até ser movida para `__UserAccountsMigrationsHistory`, mesmo quando
Configuration executa primeiro. History mista ou ambígua falha fechado para resolução manual.

## PostgreSQL de produto

O seed é uma seleção independente do provider e grava somente Configuration. `product` exige ao menos um redirect
administrativo e um protector explícito:

```powershell
$env:ROYALIDENTITY_CONFIGURATION_DB = '<connection supplied by deployment secret>'
$env:ROYALIDENTITY_OPERATIONAL_DB = '<connection supplied by deployment secret>'
$env:ROYALIDENTITY_USER_ACCOUNTS_DB = '<connection supplied by deployment secret>'

dotnet run --project RoyalIdentity.Migrations -- `
  --provider postgresql `
  --families all `
  --configuration-connection-env ROYALIDENTITY_CONFIGURATION_DB `
  --operational-connection-env ROYALIDENTITY_OPERATIONAL_DB `
  --user-accounts-connection-env ROYALIDENTITY_USER_ACCOUNTS_DB `
  --database-topology shared `
  --seed product `
  --server-admin-redirect-uri https://admin.example.com/signin-oidc `
  --server-admin-redirect-uri https://admin.example.com/callback `
  --key-protector data-protection `
  --data-protection-key-ring C:\royalidentity\keys `
  --data-protection-app-name RoyalIdentity.Configuration
```

No Server oficial, Data Protection é o protector transitório até a futura integração KMS. O key ring precisa ser
persistente, compartilhado pelas instâncias e protegido em repouso.

O runner não executa cleanup de registros Operational expirados nem reset administrativo. Depois do
provisionamento, a composição escolhe explicitamente `RoyalIdentity:Cleanup:Mode`: `External` deixa o agendamento
fora do processo web; `Hosted` registra o worker periódico e deve ter apenas uma instância responsável.

## Bancos separados e seleção parcial

Sem `--database-topology shared`, múltiplas famílias são tratadas como bancos separados. É possível migrar apenas
uma família ou um subconjunto:

```powershell
$env:ROYALIDENTITY_OPERATIONAL_DB = '<operational connection>'
$env:ROYALIDENTITY_USER_ACCOUNTS_DB = '<user accounts connection>'

dotnet run --project RoyalIdentity.Migrations -- `
  --provider postgresql `
  --families operational,user-accounts `
  --operational-connection-env ROYALIDENTITY_OPERATIONAL_DB `
  --user-accounts-connection-env ROYALIDENTITY_USER_ACCOUNTS_DB `
  --database-topology separate
```

Valores aceitos em `--families`: `configuration`, `operational`, `user-accounts`, uma lista separada por vírgulas,
ou `all`. A opção antiga `--configuration-provider` permanece aceita como alias de compatibilidade para
`--provider`, mas não seleciona apenas Configuration.

## Seeds e protectors

Os modos são `none`, `product`, `demo` e `all`. Há uma única implementação de `ConfigurationSeed`; o runner não
infere seed a partir do provider:

- `--seed demo` adiciona somente o realm e clients do demo;
- `--seed product` adiciona os realms de produto e exige `--server-admin-redirect-uri`;
- `--seed all` combina os dois;
- qualquer seed exige `--key-protector`.

Também estão disponíveis `--key-protector aes` com `--aes-key-env` e `--key-protector plain`. Plain é inseguro,
exige opt-in e emite warning. Trocar o protector de uma base que já contém signing keys requer
migração/reproteção própria; o runner não converte material persistido.

Prefira sempre as opções `*-connection-env`. Connections, senhas e chaves não devem aparecer na linha de comando
nem ser versionadas.

O processo retorna `64` para uso inválido, `1` quando qualquer família falha e `0` quando todas as famílias
selecionadas concluem. O relatório identifica `Applied`, `Failed`, `Skipped` ou `NotAttempted` por família e nunca
afirma rollback conjunto.

Para produção, os scripts revisáveis em `scripts/sql/` continuam disponíveis para Configuration e Operational.
O ambiente local em `Aspire/Aspire.AppHost` executa este runner como job separado e só inicia o Server depois de
seu sucesso; hosts nunca aplicam migrations implicitamente.
