using Microsoft.Data.Sqlite;

namespace RoyalIdentity.Demo;

/// <summary>Owns the process-local databases and Data Protection key ring used by one Demo host.</summary>
public sealed class DemoStorageLifetime : IDisposable
{
    private readonly SqliteConnection idpKeepAlive;
    private readonly SqliteConnection userAccountsKeepAlive;
    private bool disposed;

    public DemoStorageLifetime()
    {
        var instanceId = Guid.NewGuid().ToString("N");
        IdpConnectionString = $"Data Source=royalidentity-demo-idp-{instanceId};Mode=Memory;Cache=Shared";
        UserAccountsConnectionString =
            $"Data Source=royalidentity-demo-users-{instanceId};Mode=Memory;Cache=Shared";
        KeyRingPath = Path.Combine(Path.GetTempPath(), $"royalidentity-demo-keys-{instanceId}");

        idpKeepAlive = new SqliteConnection(IdpConnectionString);
        userAccountsKeepAlive = new SqliteConnection(UserAccountsConnectionString);
    }

    public string IdpConnectionString { get; }

    public string UserAccountsConnectionString { get; }

    public string KeyRingPath { get; }

    public async Task OpenAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(KeyRingPath);
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
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(KeyRingPath))
            Directory.Delete(KeyRingPath, recursive: true);
    }
}
