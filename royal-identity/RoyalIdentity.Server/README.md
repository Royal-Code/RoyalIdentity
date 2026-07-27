# RoyalIdentity.Server

`RoyalIdentity.Server` é o host produtivo fixo em PostgreSQL. O processo web não aplica migrations nem seed.

Durante a transição da Fase 1 para a Fase 3 do `plan-data-test-migration`, o backing runtime ainda é in-memory,
mas a configuração PostgreSQL alvo já é validada no startup. Por isso, as três connection strings vazias do
`appsettings.json` fazem o processo falhar fechado até receber valores por configuração externa:

- `RoyalIdentity__Connections__Configuration__ConnectionString`
- `RoyalIdentity__Connections__Operational__ConnectionString`
- `RoyalIdentity__Connections__UserAccounts__ConnectionString`

Os valores podem ser iguais no ambiente local, mas as três chaves permanecem obrigatórias. Não versione senha:
forneça os valores por variável de ambiente, user-secrets ou secret store.

As seções `RoyalIdentity:Snapshot` e `RoyalIdentity:Cleanup` também são vinculadas e validadas nesta etapa, mas
só passam a controlar os serviços persistentes quando a composição PostgreSQL substituir o backing in-memory na
Fase 3. Essa fase também entrega o runbook Podman → runner → Server.
