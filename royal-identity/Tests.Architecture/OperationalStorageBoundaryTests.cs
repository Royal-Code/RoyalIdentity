using System.Reflection;
using RoyalIdentity.Data.Operational;

namespace Tests.Architecture;

/// <summary>
/// Enforces the boundaries of the Operational storage family (ADR-013 §2.1,
/// plan-data-operational-storage DF1/DF2/DF6 e Fase 1): the pure Data project references neither the IdP core,
/// nor the Configuration data project, nor the adapter, nor ASP.NET. The complementary direction — that only
/// the adapter knows core and Data, and that neither the host nor the core gains a reference to the family —
/// is asserted in <see cref="ConfigurationStorageBoundaryTests"/>, which owns the shared adapter/host/core
/// graph checks.
/// </summary>
public class OperationalStorageBoundaryTests
{
	private static readonly Assembly DataOperational = typeof(OperationalDataAssemblyMarker).Assembly;

	private const string CoreName = "RoyalIdentity";
	private const string ConfigurationDataName = "RoyalIdentity.Data.Configuration";
	private const string AdapterName = "RoyalIdentity.Storage.EntityFramework";

	[Fact]
	public void DataOperational_DoesNotReference_Core_Configuration_Adapter_Or_AspNetCore()
	{
		var refs = DataOperational.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

		Assert.DoesNotContain(refs, n => n == CoreName);
		Assert.DoesNotContain(refs, n => n == ConfigurationDataName);
		Assert.DoesNotContain(refs, n => n.StartsWith(AdapterName, StringComparison.Ordinal));
		Assert.DoesNotContain(refs, n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
	}

	[Fact]
	public void DataOperational_DependsOn_EntityFrameworkCore_Only_AsDataStack()
	{
		var refs = DataOperational.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

		Assert.Contains(refs, n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
		Assert.DoesNotContain(refs, n => n.StartsWith("RoyalIdentity", StringComparison.Ordinal));
	}

	[Fact]
	public void DataOperational_Project_HasNoProjectReferences()
	{
		var projectReferences = ProjectReferenceReader.ReadProjectReferences(
			"RoyalIdentity.Data.Operational/RoyalIdentity.Data.Operational.csproj");

		Assert.Empty(projectReferences);
	}
}
