namespace Tests.Architecture;

/// <summary>
/// Keeps persistence contract suites on provider/module implementations instead of a general storage double.
/// </summary>
public class PersistenceTestCompositionBoundaryTests
{
    public static TheoryData<string, string[]> ProjectAllowlists => new()
    {
        {
            "Tests.Storage/Tests.Storage.csproj",
            [
                "RoyalIdentity.Security",
                "RoyalIdentity.Data.Configuration",
                "RoyalIdentity.Data.Operational",
                "RoyalIdentity.Storage.EntityFramework",
                "RoyalIdentity.Storage.EntityFramework.Sqlite",
                "RoyalIdentity.Storage.EntityFramework.PostgreSql",
                "RoyalIdentity.Migrations",
            ]
        },
        {
            "Tests.UserAccounts/Tests.UserAccounts.csproj",
            [
                "RoyalIdentity.Security",
                "RoyalIdentity.UserAccounts",
                "RoyalIdentity.UserAccounts.Integration",
                "RoyalIdentity.UserAccounts.PostgreSql",
                "RoyalIdentity.UserAccounts.Sqlite",
            ]
        },
    };

    [Theory]
    [MemberData(nameof(ProjectAllowlists))]
    public void PersistenceTestProject_ReferencesMatchItsProviderAllowlist(
        string project,
        string[] expectedReferences)
    {
        var references = ProjectReferenceReader
            .ReadProjectReferences(project)
            .Select(reference => Path.GetFileNameWithoutExtension(reference)!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(expectedReferences.ToHashSet(StringComparer.Ordinal), references);
    }
}
