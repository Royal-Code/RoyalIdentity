# RoyalIdentity Demo

This executable is a fixed, zero-configuration local demo. It provisions Configuration and Operational in one
named SQLite in-memory database, UserAccounts in another, and seeds only `demo_realm`.

Run it with:

```powershell
dotnet run --project RoyalIdentity.Demo
```

The account is `alice` / `Demo!Pass123`. All databases, users, consents, sessions and signing keys are ephemeral:
stopping the process discards them. The PostgreSQL and `Data.*` assemblies visible through the migration runner
are transitive dependencies only; this project neither configures nor invokes their providers.

The Demo declares `AddInMemoryReplayProtection()`, so replayed single-use handles are refused by this process and
only this one. That is coherent with what the Demo is — a single ephemeral process whose whole database dies with
it — and it is **not** a production configuration: a replicated deployment must declare
`AddOperationalReplayProtection()`, which the Server does. The in-memory backing logs a warning saying so when it
starts.

ASP.NET Core may log `No XML encryptor configured` while creating this process-local Data Protection key ring.
That warning is expected in the Demo: the ring lives in its private temporary directory, is shared only by the
provisioner and runtime of that process, and is deleted when the Demo stops. It is not a production configuration.
