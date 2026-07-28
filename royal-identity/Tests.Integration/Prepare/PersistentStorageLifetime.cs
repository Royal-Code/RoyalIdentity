using Microsoft.Data.Sqlite;

namespace Tests.Integration.Prepare;

/// <summary>Owns the two isolated, named SQLite in-memory databases of one persistent test factory.</summary>
internal sealed class PersistentStorageLifetime : IDisposable
{
    private readonly SqliteConnection idpKeepAlive;
    private readonly SqliteConnection userAccountsKeepAlive;
    private bool disposed;

    public PersistentStorageLifetime()
    {
        var instanceId = Guid.NewGuid().ToString("N");
        IdpConnectionString =
            $"Data Source=royalidentity-tests-idp-{instanceId};Mode=Memory;Cache=Shared;Pooling=False";
        UserAccountsConnectionString =
            $"Data Source=royalidentity-tests-users-{instanceId};Mode=Memory;Cache=Shared;Pooling=False";
        idpKeepAlive = new SqliteConnection(IdpConnectionString);
        userAccountsKeepAlive = new SqliteConnection(UserAccountsConnectionString);
    }

    public string IdpConnectionString { get; }

    public string UserAccountsConnectionString { get; }

    public async Task OpenAsync(CancellationToken ct = default)
    {
        await idpKeepAlive.OpenAsync(ct);
        await userAccountsKeepAlive.OpenAsync(ct);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        userAccountsKeepAlive.Dispose();
        idpKeepAlive.Dispose();
    }
}
