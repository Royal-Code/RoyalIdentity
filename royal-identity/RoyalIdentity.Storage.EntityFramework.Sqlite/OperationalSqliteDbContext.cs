using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Operational;

namespace RoyalIdentity.Storage.EntityFramework.Sqlite;

/// <summary>
/// SQLite default context for the Operational family. It carries no mappings itself: the base
/// <see cref="OperationalDbContext.ApplyOperationalModel"/> hook is overridden to call the single public SQLite
/// extension (plan DF1/DF2), the very same extension a third-party combined context would call. Migrations are
/// generated against this context; the host never applies them (plan DF23).
/// </summary>
public class OperationalSqliteDbContext : OperationalDbContext
{
    public OperationalSqliteDbContext(DbContextOptions<OperationalSqliteDbContext> options) : base(options)
    {
    }

    protected override void ApplyOperationalModel(ModelBuilder modelBuilder)
        => modelBuilder.ApplyRoyalIdentityOperationalSqliteMappings();
}
