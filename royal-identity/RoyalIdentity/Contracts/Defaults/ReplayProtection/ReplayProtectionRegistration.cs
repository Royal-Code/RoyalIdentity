using RoyalIdentity.Contracts.Storage;

namespace RoyalIdentity.Contracts.Defaults.ReplayProtection;

/// <summary>
/// <para>
///     Marker a replay-protection extension registers alongside its <see cref="IReplayProtectionStore"/>, so the
///     composition's choice is a declaration and not an inference from whatever happened to be registered last.
/// </para>
/// <para>
///     <see cref="ReplayProtectionStartupValidator"/> reads these markers to refuse a host whose choice is
///     missing, doubled or inconsistent with the store actually resolved.
/// </para>
/// </summary>
public sealed class ReplayProtectionRegistration
{
    /// <summary>Creates a marker for one replay-protection strategy.</summary>
    /// <param name="strategyName">Short name of the strategy, used only in diagnostics.</param>
    /// <param name="extensionName">
    /// The extension method that produced this marker, named in the startup failure message so the operator
    /// knows what to call.
    /// </param>
    /// <param name="storeType">
    /// The implementation this strategy promises to resolve. The validator compares it against the instance the
    /// container actually returns, so a later registration overriding the store cannot pass unnoticed.
    /// </param>
    public ReplayProtectionRegistration(string strategyName, string extensionName, Type storeType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(extensionName);
        ArgumentNullException.ThrowIfNull(storeType);

        if (!typeof(IReplayProtectionStore).IsAssignableFrom(storeType))
        {
            throw new ArgumentException(
                $"'{storeType.FullName}' does not implement {nameof(IReplayProtectionStore)}.",
                nameof(storeType));
        }

        StrategyName = strategyName;
        ExtensionName = extensionName;
        StoreType = storeType;
    }

    /// <summary>Short name of the declared strategy.</summary>
    public string StrategyName { get; }

    /// <summary>The extension method that declared the strategy.</summary>
    public string ExtensionName { get; }

    /// <summary>The store implementation the strategy promises to resolve.</summary>
    public Type StoreType { get; }
}
