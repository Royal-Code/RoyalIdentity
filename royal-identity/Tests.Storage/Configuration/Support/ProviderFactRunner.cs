using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Tests.Storage.Configuration.Support;

internal static class ProviderFactRunner
{
	public static async Task RunAsync(object suite)
	{
		var facts = suite.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
			.Where(method => method.GetCustomAttribute<FactAttribute>() is not null)
			.OrderBy(method => method.Name, StringComparer.Ordinal)
			.ToArray();
		Assert.NotEmpty(facts);

		foreach (var fact in facts)
		{
			try
			{
				switch (fact.Invoke(suite, null))
				{
					case Task task:
						await task;
						break;
					case ValueTask valueTask:
						await valueTask;
						break;
				}
			}
			catch (TargetInvocationException exception) when (exception.InnerException is not null)
			{
				ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
				throw;
			}
		}
	}
}
