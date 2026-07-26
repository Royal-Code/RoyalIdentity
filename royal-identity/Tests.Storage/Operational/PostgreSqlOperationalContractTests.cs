using Tests.Storage.Configuration;
using Tests.Storage.Configuration.Support;
using Tests.Storage.Contracts;
using Tests.Storage.Operational.Support;
using Tests.Storage.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// Runs the exact provider-neutral Operational contract scenarios against PostgreSQL (plan Fase 7). The
/// concrete suites stay private so xUnit does not discover their inherited facts when the opt-in connection is
/// unavailable; each public aggregate is skipped by <see cref="StoragePostgreSqlFactAttribute"/> in that case.
/// <para>
/// This is what turns "SQLite and PostgreSQL agree on casing, duplicates, absence, TTL and counts" into a
/// verified claim: the very same scenarios, unchanged, over the other provider.
/// </para>
/// </summary>
public class PostgreSqlOperationalContractTests
{
    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public Task AccessTokenContracts() => ProviderFactRunner.RunAsync(new PostgreSqlAccessTokenContracts());

    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public Task RefreshTokenContracts() => ProviderFactRunner.RunAsync(new PostgreSqlRefreshTokenContracts());

    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public Task AuthorizationCodeContracts()
        => ProviderFactRunner.RunAsync(new PostgreSqlAuthorizationCodeContracts());

    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public Task UserConsentContracts() => ProviderFactRunner.RunAsync(new PostgreSqlUserConsentContracts());

    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public Task UserSessionContracts() => ProviderFactRunner.RunAsync(new PostgreSqlUserSessionContracts());

    [StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public Task AuthorizeParametersContracts()
        => ProviderFactRunner.RunAsync(new PostgreSqlAuthorizeParametersContracts());

    private sealed class PostgreSqlAccessTokenContracts : AccessTokenStoreContractTests
    {
        protected override Task<StorageContractHarness> CreateHarnessAsync()
            => PostgreSqlOperationalStorageHarness.CreateAsync();
    }

    private sealed class PostgreSqlRefreshTokenContracts : RefreshTokenStoreContractTests
    {
        protected override Task<StorageContractHarness> CreateHarnessAsync()
            => PostgreSqlOperationalStorageHarness.CreateAsync();
    }

    private sealed class PostgreSqlAuthorizationCodeContracts : AuthorizationCodeStoreContractTests
    {
        protected override Task<StorageContractHarness> CreateHarnessAsync()
            => PostgreSqlOperationalStorageHarness.CreateAsync();
    }

    private sealed class PostgreSqlUserConsentContracts : UserConsentStoreContractTests
    {
        protected override Task<StorageContractHarness> CreateHarnessAsync()
            => PostgreSqlOperationalStorageHarness.CreateAsync();
    }

    private sealed class PostgreSqlUserSessionContracts : UserSessionStoreContractTests
    {
        protected override Task<StorageContractHarness> CreateHarnessAsync()
            => PostgreSqlOperationalStorageHarness.CreateAsync();
    }

    private sealed class PostgreSqlAuthorizeParametersContracts : AuthorizeParametersStoreContractTests
    {
        protected override Task<StorageContractHarness> CreateHarnessAsync()
            => PostgreSqlOperationalStorageHarness.CreateAsync();
    }
}
