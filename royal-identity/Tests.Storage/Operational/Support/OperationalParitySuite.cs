using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;
using RoyalIdentity.Storage.EntityFramework.Operational.Stores;

namespace Tests.Storage.Operational.Support;

/// <summary>
/// Base of every suite that must run identically on every provider. It declares the single seam a provider
/// specialization fills in — how to build a fixture — so the suites themselves only declare their scenarios.
/// </summary>
public abstract class OperationalParitySuite
{
    /// <param name="handleGenerator">
    /// Replaces the authorize-parameters handle generator, so a scenario can stage a collision instead of
    /// waiting for one (plan DF16).
    /// </param>
    /// <param name="cleanup">Configures the maintenance options the fixture exposes.</param>
    private protected abstract Task<IOperationalParityHarness> CreateHarnessAsync(
        IAuthorizeParametersHandleGenerator? handleGenerator = null,
        Action<OperationalCleanupOptions>? cleanup = null);
}
