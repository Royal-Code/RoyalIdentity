namespace Tests.Architecture;

public class DemoBoundaryTests
{
    [Fact]
    public void ModuleBoundary_DemoHasOnlyItsExplicitCompositionDependencies()
    {
        var projectReferences = ProjectReferenceReader.ReadProjectReferences(
            "RoyalIdentity.Demo/RoyalIdentity.Demo.csproj");
        string[] allowed =
        [
            "RoyalIdentity/RoyalIdentity.csproj",
            "RoyalIdentity.Razor/RoyalIdentity.Razor.csproj",
            "RoyalIdentity.Storage.EntityFramework/RoyalIdentity.Storage.EntityFramework.csproj",
            "RoyalIdentity.Storage.EntityFramework.Sqlite/RoyalIdentity.Storage.EntityFramework.Sqlite.csproj",
            "RoyalIdentity.UserAccounts.Integration/RoyalIdentity.UserAccounts.Integration.csproj",
            "RoyalIdentity.UserAccounts.Sqlite/RoyalIdentity.UserAccounts.Sqlite.csproj",
            "RoyalIdentity.Migrations/RoyalIdentity.Migrations.csproj",
        ];

        Assert.Equal(allowed.Length, projectReferences.Count);
        Assert.All(projectReferences, reference => Assert.Contains(
            allowed,
            candidate => reference.EndsWith(candidate, StringComparison.Ordinal)));
        Assert.DoesNotContain(projectReferences, reference =>
            reference.Contains("RoyalIdentity.Server", StringComparison.Ordinal));
        Assert.DoesNotContain(projectReferences, reference =>
            reference.Contains("PostgreSql", StringComparison.Ordinal));
        Assert.DoesNotContain(projectReferences, reference =>
            reference.Contains("RoyalIdentity.Data.", StringComparison.Ordinal));

        var compiledReferences = typeof(RoyalIdentity.Demo.DemoProgram).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ToArray();
        Assert.DoesNotContain(compiledReferences, name => name == "RoyalIdentity.Server");
        Assert.DoesNotContain(compiledReferences, name => name.Contains("PostgreSql", StringComparison.Ordinal));
        Assert.DoesNotContain(compiledReferences, name => name.StartsWith("RoyalIdentity.Data.", StringComparison.Ordinal));
        Assert.DoesNotContain(compiledReferences, name => name == "RoyalIdentity.Storage.InMemory");
    }

    [Fact]
    public void ModuleBoundary_DemoSourceDoesNotUsePostgreSqlOrDataModels()
    {
        var directory = Path.Combine(ProjectReferenceReader.FindRepositoryRoot(), "RoyalIdentity.Demo");
        var sourceFiles = Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        foreach (var file in sourceFiles)
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("PostgreSql", source, StringComparison.Ordinal);
            Assert.DoesNotContain("RoyalIdentity.Data.", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ModuleBoundary_HostEntryPointsRemainIndependent()
    {
        var serverReferences = typeof(RoyalIdentity.Server.ServerProgram).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ToArray();
        var migrationsReferences = typeof(RoyalIdentity.Migrations.StorageMigrationRunner).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ToArray();

        Assert.DoesNotContain(serverReferences, name => name == "RoyalIdentity.Demo");
        Assert.DoesNotContain(serverReferences, name => name.Contains("Sqlite", StringComparison.Ordinal));
        Assert.DoesNotContain(serverReferences, name => name == "RoyalIdentity.Migrations");
        Assert.DoesNotContain(serverReferences, name => name == "RoyalIdentity.Storage.InMemory");
        Assert.DoesNotContain(migrationsReferences, name => name == "RoyalIdentity.Demo");

        var demoAssembly = typeof(RoyalIdentity.Demo.DemoProgram).Assembly;
        Assert.Null(demoAssembly.GetType("Program"));
        Assert.True(typeof(RoyalIdentity.Demo.DemoProgram).IsPublic);
        Assert.NotNull(typeof(RoyalIdentity.Demo.DemoProgram).GetMethod(
            nameof(RoyalIdentity.Demo.DemoProgram.Main),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));
    }
}
