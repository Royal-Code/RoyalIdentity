// Local environment for RoyalIdentity: one PostgreSQL server with the three storage databases, the product's
// own migration/seed runner as a job, and the Server starting only after that job succeeds.

var builder = DistributedApplication.CreateBuilder(args);

// Pinned away from 5432 so this environment never competes with a locally installed PostgreSQL, and away from
// the ephemeral containers scripts/Test-*PostgreSql.ps1 publish on dynamic ports for the opt-in test suites.
const int PostgresHostPort = 55432;

// The Server publishes a fixed port because the runner has to seed the server_admin redirect URI before the
// Server exists; a proxy port chosen at runtime could not be written into the seed.
const int ServerHttpPort = 5200;

// Data Protection protects signing keys and Operational payloads (plan DF20). The runner writes that material
// and the Server reads it back, so both must share one key ring directory and one application name. This is the
// pairing that fails closed and silently when it drifts, so it is declared once, here.
const string DataProtectionApplicationName = "RoyalIdentity.Server";
var keyRingPath = Path.Combine(builder.AppHostDirectory, ".local", "data-protection");
Directory.CreateDirectory(keyRingPath);

var postgres = builder
    .AddPostgres("postgres", port: PostgresHostPort)
    .WithImageTag("17")
    .WithDataVolume("royalidentity-aspire-pgdata");

// One server, three databases. Configuration, Operational and UserAccounts keep independent connections,
// migrations and histories (plan DF9/DF19), so they are separate databases rather than schemas of one.
var configurationDb = postgres.AddDatabase("configuration", "royalidentity_configuration");
var operationalDb = postgres.AddDatabase("operational", "royalidentity_operational");
var userAccountsDb = postgres.AddDatabase("useraccounts", "royalidentity_useraccounts");

// The provisioning job is the product's own runner, not a new worker: migrations and seed never run inside the
// web process (plan DF8), and the runner already owns all three families (plan DF21). It runs to completion on
// every start and is idempotent, so a warm volume simply reports the families as already applied.
var migrations = builder
    .AddProject<Projects.RoyalIdentity_Migrations>("storage-migrations")
    .WithEnvironment("ROYALIDENTITY_CONFIGURATION_DB", configurationDb)
    .WithEnvironment("ROYALIDENTITY_OPERATIONAL_DB", operationalDb)
    .WithEnvironment("ROYALIDENTITY_USER_ACCOUNTS_DB", userAccountsDb)
    .WithArgs(
        "--provider", "postgresql",
        "--families", "all",
        "--database-topology", "separate",
        // Connections travel by environment variable so no credential ever reaches the command line.
        "--configuration-connection-env", "ROYALIDENTITY_CONFIGURATION_DB",
        "--operational-connection-env", "ROYALIDENTITY_OPERATIONAL_DB",
        "--user-accounts-connection-env", "ROYALIDENTITY_USER_ACCOUNTS_DB",
        "--seed", "product",
        "--server-admin-redirect-uri", $"http://localhost:{ServerHttpPort}/server-admin/callback",
        "--key-protector", "data-protection",
        "--data-protection-key-ring", keyRingPath,
        "--data-protection-app-name", DataProtectionApplicationName)
    .WaitFor(configurationDb)
    .WaitFor(operationalDb)
    .WaitFor(userAccountsDb);

builder
    .AddProject<Projects.RoyalIdentity_Server>("royalidentity", launchProfileName: null)
    .WithHttpEndpoint(port: ServerHttpPort, name: "http")
    .WithExternalHttpEndpoints()
    .WithEnvironment("RoyalIdentity__Connections__Configuration__ConnectionString", configurationDb)
    .WithEnvironment("RoyalIdentity__Connections__Operational__ConnectionString", operationalDb)
    .WithEnvironment("RoyalIdentity__Connections__UserAccounts__ConnectionString", userAccountsDb)
    .WithEnvironment("RoyalIdentity__DataProtection__KeyRingPath", keyRingPath)
    .WithEnvironment("RoyalIdentity__DataProtection__ApplicationName", DataProtectionApplicationName)
    // Every seeded realm selects the default Operational payload profile; the Server must register that id or
    // its startup validator refuses to serve traffic.
    .WithEnvironment("RoyalIdentity__DataProtection__OperationalPayloadProfileId", "default")
    // The Server never migrates nor seeds. It starts only after the runner exited successfully, and any future
    // project that needs a provisioned schema declares the same dependency.
    .WaitForCompletion(migrations);

builder.Build().Run();
