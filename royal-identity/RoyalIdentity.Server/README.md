# RoyalIdentity Server — local PostgreSQL

`RoyalIdentity.Server` is a PostgreSQL-only host. It never creates a database, applies migrations or seeds data;
run the migration runner first. The commands below use one local PostgreSQL 17 database while still supplying the
three explicit DbContext connections required by the Server.

Choose a local password in the current PowerShell session (do not commit it), then start PostgreSQL:

```powershell
$env:RI_POSTGRES_PASSWORD = "<choose-a-local-password>"
podman run --name royalidentity-postgres --rm -d `
  -e POSTGRES_USER=royalidentity `
  -e POSTGRES_PASSWORD=$env:RI_POSTGRES_PASSWORD `
  -e POSTGRES_DB=royalidentity `
  -p 5432:5432 `
  docker.io/library/postgres:17
```

Prepare the three explicit connections and a persistent Data Protection key ring:

```powershell
$env:RI_CONFIGURATION_CONNECTION = "Host=localhost;Port=5432;Database=royalidentity;Username=royalidentity;Password=$env:RI_POSTGRES_PASSWORD"
$env:RI_OPERATIONAL_CONNECTION = $env:RI_CONFIGURATION_CONNECTION
$env:RI_USER_ACCOUNTS_CONNECTION = $env:RI_CONFIGURATION_CONNECTION
$env:RI_DATA_PROTECTION_KEY_RING = Join-Path (Get-Location) ".local-data-protection"
```

Provision all storage families and the product seed:

```powershell
dotnet run --project RoyalIdentity.Migrations -- `
  --provider postgresql `
  --families all `
  --configuration-connection-env RI_CONFIGURATION_CONNECTION `
  --operational-connection-env RI_OPERATIONAL_CONNECTION `
  --user-accounts-connection-env RI_USER_ACCOUNTS_CONNECTION `
  --database-topology shared `
  --seed product `
  --server-admin-redirect-uri https://localhost:7185/callback `
  --key-protector data-protection `
  --data-protection-key-ring $env:RI_DATA_PROTECTION_KEY_RING `
  --data-protection-app-name RoyalIdentity.Server
```

Pass the same connections and Data Protection identity to the Server:

```powershell
$env:RoyalIdentity__Connections__Configuration__ConnectionString = $env:RI_CONFIGURATION_CONNECTION
$env:RoyalIdentity__Connections__Operational__ConnectionString = $env:RI_OPERATIONAL_CONNECTION
$env:RoyalIdentity__Connections__UserAccounts__ConnectionString = $env:RI_USER_ACCOUNTS_CONNECTION
$env:RoyalIdentity__DataProtection__KeyRingPath = $env:RI_DATA_PROTECTION_KEY_RING
$env:RoyalIdentity__DataProtection__ApplicationName = "RoyalIdentity.Server"
$env:RoyalIdentity__DataProtection__OperationalPayloadProfileId = "default"
dotnet run --project RoyalIdentity.Server
```

`RoyalIdentity:Cleanup:Mode` is explicit. The checked-in local default is `External`, so the web process does not
run a cleanup scheduler; an external job must invoke the same Operational maintenance operation. `Hosted` is
appropriate only when exactly one process is intentionally responsible for the periodic worker. Neither mode
performs an administrative reset.

## Replay protection

The Server declares `AddOperationalReplayProtection()`. Handles of single-use artifacts — today the `jti` of a
`private_key_jwt` client assertion — live in the Operational table `replay_handles`, so every replica reading the
same database shares them. There is no default: a composition that declares no backing, or more than one, fails at
startup in every environment rather than turning the gap into an `invalid_client` per request.

Do **not** switch this host to `AddInMemoryReplayProtection()`. That backing only refuses a replay the process
that saw it first receives, which in a replicated deployment lets most of them through.

`replay_handles` is cleaned by the same Operational maintenance as every other expired record, so the cleanup mode
above already covers it.

### How long a client assertion may live

`Authentication.ClientAssertionMaxLifetime` caps how far ahead of the server's own clock an assertion may claim to
expire. Default **10 minutes**; accepted range **1 second to 1 hour**, inclusive; configurable per realm.

Ten minutes is the floor coherent with the five-minute clock skew the assertion validation already tolerates: a
client emitting a five-minute assertion from a clock five minutes ahead produces `exp = now + 10min` on the
server's clock and stays accepted. One hour exists as an override for legacy integrations, not as a target — it
widens the window in which a leaked assertion can be used, and it lengthens how long the server retains handles.

**Clients should emit assertions of one to five minutes.** A value below the tolerated skew is allowed as
deliberate hardening of a realm, at the cost of refusing clients whose clock runs ahead.

Stop the local database with `podman stop royalidentity-postgres`.

To exercise replay protection against a real PostgreSQL — the same client assertion presented twice to the token
endpoint, accepted then refused — run `./scripts/Test-ReplayProtectionPostgreSql.ps1`.

To validate the complete disposable sequence without keeping local state, run
`./scripts/Test-ServerPostgreSql.ps1`. It allocates non-default dynamic PostgreSQL and Server ports, applies the
three families with the Product seed, starts the Server, validates OIDC discovery and drives an authorization-code
request through the interactive login challenge. Product provisioning deliberately creates no user account, so
token issuance begins only after an administrator creates one.
