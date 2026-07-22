using System.Reflection;
using RoyalIdentity.Data.Configuration;
using RoyalIdentity.Storage.EntityFramework;
using RoyalIdentity.Storage.EntityFramework.PostgreSql;
using RoyalIdentity.Storage.EntityFramework.Sqlite;

namespace Tests.Architecture;

/// <summary>
/// Enforces the boundaries of the core Configuration storage family (ADR-013 §2.1/§2.3,
/// plan-data-configuration-storage DF1 e Fase 1): the pure Data project references neither the IdP core,
/// nor the adapter, nor ASP.NET; only the adapter knows core and Data; providers sit on adapter/Data; the
/// runner sits on providers; and neither the host nor the core gains a reference to the new family.
/// Assembly-level checks catch accidental <c>using</c>s; csproj-graph checks pin the intended references
/// even before later phases bind them in code.
/// </summary>
public class ConfigurationStorageBoundaryTests
{
	private static readonly Assembly DataConfiguration = typeof(ConfigurationDataAssemblyMarker).Assembly;
	private static readonly Assembly Adapter = typeof(EntityFrameworkStorageAssemblyMarker).Assembly;
	private static readonly Assembly SqliteProvider = typeof(EntityFrameworkSqliteAssemblyMarker).Assembly;
	private static readonly Assembly PostgreSqlProvider = typeof(EntityFrameworkPostgreSqlAssemblyMarker).Assembly;

	private const string CoreName = "RoyalIdentity";
	private const string DataName = "RoyalIdentity.Data.Configuration";
	private const string AdapterName = "RoyalIdentity.Storage.EntityFramework";

	public static TheoryData<string, Assembly> ProviderAssemblies => new()
	{
		{ "Sqlite", SqliteProvider },
		{ "PostgreSql", PostgreSqlProvider }
	};

	[Fact]
	public void DataConfiguration_DoesNotReference_Core_Adapter_Or_AspNetCore()
	{
		var refs = DataConfiguration.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

		Assert.DoesNotContain(refs, n => n == CoreName);
		Assert.DoesNotContain(refs, n => n.StartsWith(AdapterName, StringComparison.Ordinal));
		Assert.DoesNotContain(refs, n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
	}

	[Fact]
	public void DataConfiguration_DependsOn_EntityFrameworkCore_Only_AsDataStack()
	{
		var refs = DataConfiguration.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

		Assert.Contains(refs, n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
		Assert.DoesNotContain(refs, n => n.StartsWith("RoyalIdentity", StringComparison.Ordinal));
	}

	[Fact]
	public void DataConfiguration_Project_HasNoProjectReferences()
	{
		var projectReferences = ProjectReferenceReader.ReadProjectReferences(
			"RoyalIdentity.Data.Configuration/RoyalIdentity.Data.Configuration.csproj");

		Assert.Empty(projectReferences);
	}

	[Fact]
	public void Adapter_ProjectGraph_References_Core_And_Data_Only()
	{
		var projectReferences = ProjectReferenceReader.ReadProjectReferences(
			"RoyalIdentity.Storage.EntityFramework/RoyalIdentity.Storage.EntityFramework.csproj");

		Assert.Equal(2, projectReferences.Count);
		Assert.Contains(projectReferences, r => r.EndsWith("RoyalIdentity/RoyalIdentity.csproj", StringComparison.Ordinal));
		Assert.Contains(projectReferences, r => r.EndsWith(
			"RoyalIdentity.Data.Configuration/RoyalIdentity.Data.Configuration.csproj", StringComparison.Ordinal));
	}

	[Theory]
	[MemberData(nameof(ProviderAssemblies))]
	public void Providers_DoNotBind_Core_Directly(string _, Assembly provider)
	{
		var refs = provider.GetReferencedAssemblies().Select(a => a.Name!);

		Assert.DoesNotContain(refs, n => n == CoreName);
	}

	[Theory]
	[InlineData("RoyalIdentity.Storage.EntityFramework.Sqlite/RoyalIdentity.Storage.EntityFramework.Sqlite.csproj")]
	[InlineData("RoyalIdentity.Storage.EntityFramework.PostgreSql/RoyalIdentity.Storage.EntityFramework.PostgreSql.csproj")]
	public void Providers_ProjectGraph_References_Adapter_And_Data_Only(string providerProject)
	{
		var projectReferences = ProjectReferenceReader.ReadProjectReferences(providerProject);

		Assert.Equal(2, projectReferences.Count);
		Assert.Contains(projectReferences, r => r.EndsWith(
			"RoyalIdentity.Storage.EntityFramework/RoyalIdentity.Storage.EntityFramework.csproj", StringComparison.Ordinal));
		Assert.Contains(projectReferences, r => r.EndsWith(
			"RoyalIdentity.Data.Configuration/RoyalIdentity.Data.Configuration.csproj", StringComparison.Ordinal));
	}

	[Fact]
	public void MigrationsRunner_ProjectGraph_References_Providers_Only()
	{
		var projectReferences = ProjectReferenceReader.ReadProjectReferences(
			"RoyalIdentity.Migrations/RoyalIdentity.Migrations.csproj");

		Assert.Equal(2, projectReferences.Count);
		Assert.All(projectReferences, r => Assert.Contains("RoyalIdentity.Storage.EntityFramework.", r));
	}

	[Fact]
	public void Host_DoesNotReference_TheEntityFrameworkFamily()
	{
		// DF11/DF20: no database, runner or EF storage becomes a prerequisite of the default host.
		var projectReferences = ProjectReferenceReader.ReadProjectReferences(
			"RoyalIdentity.Server/RoyalIdentity.Server.csproj");

		Assert.DoesNotContain(projectReferences, r => r.Contains("RoyalIdentity.Storage.EntityFramework", StringComparison.Ordinal));
		Assert.DoesNotContain(projectReferences, r => r.Contains("RoyalIdentity.Data.Configuration", StringComparison.Ordinal));
		Assert.DoesNotContain(projectReferences, r => r.Contains("RoyalIdentity.Migrations", StringComparison.Ordinal));
	}

	[Fact]
	public void Core_DoesNotReference_DataOrAdapter()
	{
		var projectReferences = ProjectReferenceReader.ReadProjectReferences("RoyalIdentity/RoyalIdentity.csproj");

		Assert.DoesNotContain(projectReferences, r => r.Contains("RoyalIdentity.Data.Configuration", StringComparison.Ordinal));
		Assert.DoesNotContain(projectReferences, r => r.Contains("RoyalIdentity.Storage.EntityFramework", StringComparison.Ordinal));

		var refs = typeof(RoyalIdentity.Contracts.Storage.IStorage).Assembly
			.GetReferencedAssemblies().Select(a => a.Name!);
		Assert.DoesNotContain(refs, n => n == DataName || n.StartsWith(AdapterName, StringComparison.Ordinal));
	}
}
