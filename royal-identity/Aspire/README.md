# RoyalIdentity local environment (Aspire)

`Aspire.AppHost` orchestrates the local PostgreSQL environment for `RoyalIdentity.Server`.

```powershell
dotnet run --project Aspire/Aspire.AppHost
```

## What comes up, and in which order

1. **`postgres`** — PostgreSQL 17 on host port **55432**. The port is pinned away from 5432 so it never competes
   with a locally installed server, and the opt-in test suites publish their own containers on dynamic ports, so
   they never collide with this environment either.
2. **`configuration`, `operational`, `useraccounts`** — one database per storage family
   (`royalidentity_configuration`, `royalidentity_operational`, `royalidentity_useraccounts`). They are separate
   databases, not schemas of one, because each family owns its connection, migrations and history.
3. **`storage-migrations`** — the product's own `RoyalIdentity.Migrations` runner, executed as a job once the three
   databases are healthy. It applies all three families and the `product` seed, then exits. It is not a new
   worker: migrations and seed never run inside a web process, and this runner is the supported route for all
   three families.
4. **`royalidentity`** — `RoyalIdentity.Server` on http://localhost:5200, started only after the job **completed
   successfully**. Any future project that needs a provisioned schema declares the same dependency:

   ```csharp
   builder.AddProject<Projects.Some_Future_Project>("something")
       .WaitForCompletion(migrations);
   ```

The Server receives its three connection strings and its Data Protection settings from the AppHost. It never
migrates, never seeds and never inspects schema state.

## Data Protection

The runner protects the seeded signing keys with ASP.NET Core Data Protection, and the Server has to unprotect
them at startup. Both processes therefore share one key ring directory —
`Aspire/Aspire.AppHost/.local/data-protection` — and one application name, `RoyalIdentity.Server`. Changing one
without the other makes the Server fail at startup with unreadable key material.

Connection strings reach the runner through environment variables, never on its command line.

## Seeded content

`--seed product` creates the internal realms `server`, `account` and `admin`, the `server_admin` client and usable
signing keys. There is no demo realm and no demo account here — for a zero-configuration playground use
`RoyalIdentity.Demo`, which is ephemeral and self-provisioned.

## Resetting

The database lives in the named volume `royalidentity-aspire-pgdata` and survives restarts, so the runner reports
the families as already applied on the next start. To start from an empty environment:

```powershell
podman volume rm royalidentity-aspire-pgdata   # or: docker volume rm royalidentity-aspire-pgdata
```

## Environment acceptance

`Aspire.Tests` contains one acceptance that starts the whole environment, waits for the job to finish and for the
Server to answer as a seeded realm. It is **opt-in**, because this project belongs to `RoyalIdentity.sln` and a
solution-wide `dotnet test` must not require a container runtime:

```powershell
$env:ROYALIDENTITY_ASPIRE_TESTS = "1"
dotnet test Aspire/Aspire.Tests
```

## Template leftovers

The starter-template sample (`Aspire.ApiService`, `Aspire.Web`) was removed. `Aspire.ServiceDefaults` survives it
but currently has no consumer: wiring it into `RoyalIdentity.Server` would add an Aspire dependency to the
production host, which its project-graph guard does not allow. Remove it, or keep it for a future project that is
not the Server.
