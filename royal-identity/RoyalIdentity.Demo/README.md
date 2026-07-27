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
