namespace Tests.Architecture;

/// <summary>
/// Keeps the integration suite on its single persistent composition after Plan 4 Fase 6.
/// </summary>
public class IntegrationCompositionBoundaryTests
{
    [Fact]
    public void IntegrationTests_ProjectReferences_AreThePersistentCompositionAllowlist()
    {
        var references = ProjectReferenceReader
            .ReadProjectReferences("Tests.Integration/Tests.Integration.csproj")
            .Select(reference => Path.GetFileNameWithoutExtension(reference)!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "RoyalIdentity.Security",
                "RoyalIdentity.Server",
                "RoyalIdentity.Demo",
                "RoyalIdentity.UserAccounts.Integration",
                "RoyalIdentity.UserAccounts.Sqlite",
                "Tests.Host",
                "RoyalIdentity.Migrations",
                // The default composition stays on SQLite. This one exists for the opt-in acceptance of
                // plan-replay-protection Fase 3, which has to run the durable replay backing on the engine the
                // production Server runs; it is skipped unless a PostgreSQL server is published for the suite.
                "RoyalIdentity.Storage.EntityFramework.PostgreSql",
            },
            references);
    }

    [Fact]
    public void IntegrationTests_OnlyConcreteStorageRoot_DerivesFromAppFactoryBase()
    {
        var directory = Path.Combine(
            ProjectReferenceReader.FindRepositoryRoot(),
            "Tests.Integration",
            "Prepare");
        var derivations = Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .SelectMany(File.ReadLines)
            .Where(line => line.Contains(": AppFactoryBase", StringComparison.Ordinal))
            .Select(line => line.Trim())
            .ToArray();

        Assert.Equal(
            ["public class PersistentStorageAppFactory : AppFactoryBase"],
            derivations);
    }
}
