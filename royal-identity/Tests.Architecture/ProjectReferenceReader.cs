using System.Xml.Linq;

namespace Tests.Architecture;

/// <summary>
/// Reads <c>ProjectReference</c> entries straight from a csproj so boundary tests can assert the intended
/// dependency graph even when an assembly-level reference is not bound by the compiler yet.
/// </summary>
internal static class ProjectReferenceReader
{
	public static IReadOnlyList<string> ReadProjectReferences(string relativeProjectPath)
	{
		var projectPath = Path.Combine(FindRepositoryRoot(), relativeProjectPath);
		var document = XDocument.Load(projectPath);

		return document
			.Descendants("ProjectReference")
			.Select(e => e.Attribute("Include")?.Value)
			.Where(v => !string.IsNullOrWhiteSpace(v))
			.Select(v => v!.Replace('\\', '/'))
			.ToArray();
	}

	public static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);

		while (directory is not null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "RoyalIdentity.sln")))
				return directory.FullName;

			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
	}
}
