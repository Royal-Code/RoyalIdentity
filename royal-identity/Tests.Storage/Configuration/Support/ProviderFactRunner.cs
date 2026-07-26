using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Tests.Storage.Configuration.Support;

/// <summary>
/// Runs a provider suite's scenarios by reflection, so the suite itself can stay private and xUnit does not
/// discover its facts when the opt-in provider is unavailable. <c>[Theory]</c> derives from <c>[Fact]</c>, so
/// theories are picked up too and each <c>[InlineData]</c> case is executed with its own arguments — a theory
/// silently invoked with no arguments would fail on parameter count, not on what it asserts.
/// </summary>
internal static class ProviderFactRunner
{
    public static async Task RunAsync(object suite)
    {
        var scenarios = suite.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.GetCustomAttribute<FactAttribute>() is not null)
            .OrderBy(method => method.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(scenarios);

        foreach (var scenario in scenarios)
        {
            foreach (var arguments in CasesOf(scenario))
                await InvokeAsync(suite, scenario, arguments);
        }
    }

    /// <summary>One case per <c>[InlineData]</c>, or a single parameterless case for a plain fact.</summary>
    private static IReadOnlyList<object?[]?> CasesOf(MethodInfo scenario)
    {
        var inlineData = scenario.GetCustomAttributes<InlineDataAttribute>().ToArray();
        if (inlineData.Length is 0)
        {
            Assert.Empty(scenario.GetParameters());
            return [null];
        }

        return [.. inlineData.Select(data => data.GetData(scenario).Single())];
    }

    private static async Task InvokeAsync(object suite, MethodInfo scenario, object?[]? arguments)
    {
        try
        {
            switch (scenario.Invoke(suite, arguments))
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
